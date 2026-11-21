using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Services.Jellyfish.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JellyFish.Api.Infrastructure.Services.Jellyfish;

public sealed class JellyfishControllerClient : IJellyfishControllerClient
{
    private readonly Uri _uri;
    private readonly ILogger<JellyfishControllerClient> _logger;
    private readonly int _timeoutMs;
    private readonly Func<JsonDocument, CancellationToken, Task<Result<JsonDocument>>> _transport;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions RunPatternJsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// The runPattern data payload. The controller is sensitive to field order, so the order here is the order
    /// sent, following the API doc's example. ledOnPos is only sent when the pattern has one, so patterns
    /// without it send the same payload as before.
    /// </summary>
    private sealed record RunPatternData(
        int[] Colors,
        int SpaceBetweenPixels,
        string EffectBetweenPixels,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        Dictionary<string, int[]>? LedOnPos,
        string Type,
        int Skip,
        int NumOfLeds,
        RunPatternRunData RunData,
        string Direction);

    private sealed record RunPatternRunData(int Speed, int Brightness, string Effect, int EffectValue, int[] RgbAdj);

    /// <summary>
    /// The runPattern data for the undocumented "Firework" type, in the order the controller stores a Firework
    /// saved from the Jellyfish app (type first, fireworkSettings last). The app's file metadata (readOnly,
    /// isDistribution, isDownloaded) isn't sent.
    /// </summary>
    private sealed record FireworkRunPatternData(
        string Type,
        int[] Colors,
        int SpaceBetweenPixels,
        string EffectBetweenPixels,
        int Skip,
        int NumOfLeds,
        RunPatternRunData RunData,
        string Direction,
        FireworkRunSettings FireworkSettings);

    /// <summary>Names as the controller spells them ("colorPerLED", not camelCase "colorPerLed").</summary>
    private sealed record FireworkRunSettings(
        [property: System.Text.Json.Serialization.JsonPropertyName("spawnRate")] int SpawnRate,
        [property: System.Text.Json.Serialization.JsonPropertyName("colorPerLED")] int ColorPerLed,
        [property: System.Text.Json.Serialization.JsonPropertyName("fireworkSpeed")] int FireworkSpeed);

    public const string FireworkType = "Firework";

    public JellyfishControllerClient(IOptions<JellyfishControllerOptions> options, ILogger<JellyfishControllerClient> logger)
    {
        _logger = logger;
        var u = options.Value.WebSocketUri;
        _uri = string.IsNullOrWhiteSpace(u) ? throw new ArgumentException("WebSocketUri is required.") : new Uri(u);
        _timeoutMs = options.Value.RequestTimeoutMs <= 0 ? 3000 : options.Value.RequestTimeoutMs;
        _transport = SendOverWebSocketAsync;
    }

    // Testing/override constructor
    public JellyfishControllerClient(IOptions<JellyfishControllerOptions> options, ILogger<JellyfishControllerClient> logger,
        Func<JsonDocument, CancellationToken, Task<Result<JsonDocument>>> transport)
        : this(options, logger)
    {
        _transport = transport ?? SendOverWebSocketAsync;
    }

    public async Task<Result<IReadOnlyList<ControllerFolderDto>>> GetFoldersAsync(CancellationToken ct)
    {
        var res = await SendAsync(JsonDocument.Parse("{\"cmd\":\"toCtlrGet\",\"get\":[[\"patternFileList\"]]}"), ct);
        if (res.IsFailure) return Result.Failure<IReadOnlyList<ControllerFolderDto>>(res.Error);
        var root = res.Value.RootElement;
        if (!root.TryGetProperty("patternFileList", out var list) || list.ValueKind != JsonValueKind.Array)
            return Result.Failure<IReadOnlyList<ControllerFolderDto>>(Error.Problem("Invalid response: patternFileList missing"));

        var folders = new List<ControllerFolderDto>();
        foreach (var item in list.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(name)) continue; // skip patterns
            var folder = item.TryGetProperty("folders", out var f) ? f.GetString() ?? string.Empty : string.Empty;
            var ro = item.TryGetProperty("readOnly", out var roEl) && roEl.ValueKind == JsonValueKind.True;
            if (!string.IsNullOrWhiteSpace(folder))
            {
                folders.Add(new ControllerFolderDto { Name = folder.Trim(), IsReadOnly = ro });
            }
        }
        return Result.Success<IReadOnlyList<ControllerFolderDto>>(folders);
    }

    public async Task<Result<IReadOnlyList<ControllerPatternListItemDto>>> GetPatternsAsync(CancellationToken ct)
    {
        var res = await SendAsync(JsonDocument.Parse("{\"cmd\":\"toCtlrGet\",\"get\":[[\"patternFileList\"]]}"), ct);
        if (res.IsFailure) return Result.Failure<IReadOnlyList<ControllerPatternListItemDto>>(res.Error);
        var root = res.Value.RootElement;
        if (!root.TryGetProperty("patternFileList", out var list) || list.ValueKind != JsonValueKind.Array)
            return Result.Failure<IReadOnlyList<ControllerPatternListItemDto>>(Error.Problem("Invalid response: patternFileList missing"));

        var patterns = new List<ControllerPatternListItemDto>();
        foreach (var item in list.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(name)) continue; // folders
            var folder = item.TryGetProperty("folders", out var f) ? f.GetString() ?? string.Empty : string.Empty;
            var ro = item.TryGetProperty("readOnly", out var roEl) && roEl.ValueKind == JsonValueKind.True;
            patterns.Add(new ControllerPatternListItemDto
            {
                FolderName = folder.Trim(),
                Name = name.Trim(),
                IsReadOnly = ro,
                ControllerFolderName = folder,
                ControllerName = name
            });
        }
        return Result.Success<IReadOnlyList<ControllerPatternListItemDto>>(patterns);
    }

    public async Task<Result<PatternDetails>> GetPatternDetailAsync(string folderName, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return Result.Failure<PatternDetails>(Error.Validation("FolderName is required"));
        if (string.IsNullOrWhiteSpace(name)) return Result.Failure<PatternDetails>(Error.Validation("Name is required"));

        var payload = new
        {
            cmd = "toCtlrGet",
            get = new object[][] { new object[] { "patternFileData", folderName, name } }
        };
        var json = JsonSerializer.Serialize(payload);
        var res = await SendAsync(JsonDocument.Parse(json), ct);
        if (res.IsFailure) return Result.Failure<PatternDetails>(res.Error);
        var root = res.Value.RootElement;
        if (!root.TryGetProperty("patternFileData", out var pfd))
            return Result.Failure<PatternDetails>(Error.Problem("Invalid response: patternFileData missing"));
        if (!pfd.TryGetProperty("jsonData", out var jd) || jd.ValueKind != JsonValueKind.String)
            return Result.Failure<PatternDetails>(Error.Problem("Invalid response: jsonData missing"));

        var jsonData = jd.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            _logger.LogInformation("Controller returned empty pattern details for {Folder}/{Name}; defaulting/no-op", folderName, name);
            return Result.Failure<PatternDetails>(Error.NotFound($"Pattern detail for '{name}' in folder '{folderName}' not found"));
        }
        try
        {
            var details = JsonSerializer.Deserialize<PatternDetails>(jsonData, JsonOpts);
            if (details is null) return Result.Failure<PatternDetails>(Error.Problem("Failed to parse pattern details"));
            return Result.Success(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize pattern details for {Folder}/{Name}", folderName, name);
            return Result.Failure<PatternDetails>(Error.Problem("Failed to parse pattern details"));
        }
    }

    public async Task<Result<IReadOnlyList<ControllerZoneDto>>> GetZonesAsync(CancellationToken ct)
    {
        var res = await SendAsync(JsonDocument.Parse("{\"cmd\":\"toCtlrGet\",\"get\":[[\"zones\"]]}"), ct);
        if (res.IsFailure) return Result.Failure<IReadOnlyList<ControllerZoneDto>>(res.Error);
        var root = res.Value.RootElement;
        if (!root.TryGetProperty("zones", out var zonesEl) || zonesEl.ValueKind != JsonValueKind.Object)
            return Result.Failure<IReadOnlyList<ControllerZoneDto>>(Error.Problem("Invalid response: zones missing"));

        var zones = new List<ControllerZoneDto>();
        foreach (var prop in zonesEl.EnumerateObject())
        {
            var zoneName = prop.Name;
            var obj = prop.Value;
            string ctlrName = string.Empty;
            var map = new List<ControllerZoneDto.ZoneMapEntry>();
            if (obj.TryGetProperty("portMap", out var pm) && pm.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in pm.EnumerateArray())
                {
                    if (string.IsNullOrEmpty(ctlrName) && e.TryGetProperty("ctlrName", out var cn)) ctlrName = cn.GetString() ?? string.Empty;
                    var port = e.TryGetProperty("phyPort", out var p) ? p.GetInt32() : 0;
                    var start = e.TryGetProperty("phyStartIdx", out var s) ? s.GetInt32() : 0;
                    var end = e.TryGetProperty("phyEndIdx", out var en) ? en.GetInt32() : 0;
                    var rgbStart = e.TryGetProperty("zoneRGBStartIdx", out var rs) ? rs.GetInt32() : 0;
                    map.Add(new ControllerZoneDto.ZoneMapEntry { Port = port, StartIdx = start, EndIdx = end, RgbStartIdx = rgbStart });
                }
            }
            zones.Add(new ControllerZoneDto { Name = zoneName.Trim(), CtlrName = ctlrName.Trim(), ZoneMap = map });
        }
        return Result.Success<IReadOnlyList<ControllerZoneDto>>(zones);
    }

    public async Task<Result> RunPatternAsync(string folderName, string name, int state, IReadOnlyList<string> zoneNames, PatternDetails? data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return Result.Failure(Error.Validation("FolderName is required"));
        if (string.IsNullOrWhiteSpace(name)) return Result.Failure(Error.Validation("Name is required"));
        if (zoneNames is null || zoneNames.Count == 0) return Result.Failure(Error.Validation("At least one zone name is required"));
        if (state is not 0 and not 1) return Result.Failure(Error.Validation("State must be 0 or 1"));

        var file = $"{folderName}/{name}";

        // Build controller-compliant data payload: escaped JSON string with camelCased subset
        string dataString = "";
        if (data is not null)
        {
            //file is not required
            file = "";
            var runData = new RunPatternRunData(data.RunData.Speed, data.RunData.Brightness, data.RunData.Effect, data.RunData.EffectValue, data.RunData.RgbAdj);
            if (string.Equals(data.Type?.Trim(), FireworkType, StringComparison.OrdinalIgnoreCase) && data.FireworkSettings is { } fw)
            {
                var firework = new FireworkRunPatternData(
                    FireworkType, data.Colors, data.SpaceBetweenPixels, data.EffectBetweenPixels, data.Skip, data.NumOfLeds,
                    runData, data.Direction, new FireworkRunSettings(fw.SpawnRate, fw.ColorPerLed, fw.FireworkSpeed));
                dataString = JsonSerializer.Serialize(firework, RunPatternJsonOpts);
            }
            else
            {
                var subset = new RunPatternData(
                    data.Colors,
                    data.SpaceBetweenPixels,
                    data.EffectBetweenPixels,
                    data.LedOnPos,
                    data.Type,
                    data.Skip,
                    data.NumOfLeds,
                    runData,
                    data.Direction);
                dataString = JsonSerializer.Serialize(subset, RunPatternJsonOpts);
            }
        }

        object payloadObj = new
        {
            cmd = "toCtlrSet",
            runPattern = new
            {
                file,
                data = dataString,
                id = "",
                state,
                zoneName = zoneNames
            }
        };

        var json = JsonSerializer.Serialize(payloadObj);
        var res = await SendAsync(JsonDocument.Parse(json), ct);
        return res.IsSuccess ? Result.Success() : Result.Failure(res.Error);
    }

    public async Task<Result> HealthCheckAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // prefer a short timeout (1s) regardless of global timeout
        cts.CancelAfter(Math.Min(_timeoutMs, 1000));
        var res = await GetZonesAsync(cts.Token);
        return res.IsSuccess ? Result.Success() : Result.Failure(res.Error);
    }

    private async Task<Result<JsonDocument>> SendOverWebSocketAsync(JsonDocument request, CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(_timeoutMs);
        try
        {
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(_uri, linkedCts.Token);

            var reqBytes = Encoding.UTF8.GetBytes(request.RootElement.GetRawText());
            await ws.SendAsync(new ArraySegment<byte>(reqBytes), WebSocketMessageType.Text, true, linkedCts.Token);

            using var mem = new MemoryStream();
            var buffer = new byte[32 * 1024];
            while (true)
            {
                var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), linkedCts.Token);
                if (res.MessageType == WebSocketMessageType.Close) break;
                mem.Write(buffer, 0, res.Count);
                if (res.EndOfMessage) break;
            }
            var payload = Encoding.UTF8.GetString(mem.ToArray());
            var doc = JsonDocument.Parse(payload);
            return Result.Success(doc);
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogWarning(oce, "WebSocket request timed out after {TimeoutMs} ms", _timeoutMs);
            return Result.Failure<JsonDocument>(Error.Problem("Request timed out communicating with Jellyfish controller"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket request failed");
            return Result.Failure<JsonDocument>(Error.Problem("Failed to communicate with Jellyfish controller"));
        }
    }

    private async Task<Result<JsonDocument>> SendAsync(JsonDocument request, CancellationToken ct)
    {
        var raw = await _transport(request, ct);
        if (raw.IsFailure) return raw;

        try
        {
            var root = raw.Value.RootElement;
            if (root.TryGetProperty("Succeeded", out var succeededEl))
            {
                if (succeededEl.ValueKind == JsonValueKind.False)
                {
                    var exMsg = root.TryGetProperty("Exception", out var exEl) ? (exEl.GetString() ?? string.Empty) : string.Empty;
                    var dataPreview = "";
                    if (root.TryGetProperty("Data", out var dataEl))
                    {
                        var s = dataEl.ValueKind == JsonValueKind.String ? dataEl.GetString() : dataEl.GetRawText();
                        if (!string.IsNullOrEmpty(s)) dataPreview = s!.Length > 256 ? s.Substring(0, 256) + "..." : s!;
                    }
                    _logger.LogWarning("Controller returned failure. Exception='{Exception}'. DataPreview='{DataPreview}'", exMsg, dataPreview);
                    return Result.Failure<JsonDocument>(Error.Problem(string.IsNullOrWhiteSpace(exMsg) ? "Controller reported failure" : exMsg));
                }
                // unwrap Data when present
                if (root.TryGetProperty("Data", out var dataEl2))
                {
                    if (dataEl2.ValueKind == JsonValueKind.String)
                    {
                        var str = dataEl2.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(str))
                            return Result.Success(JsonDocument.Parse(str));
                    }
                    else
                    {
                        return Result.Success(JsonDocument.Parse(dataEl2.GetRawText()));
                    }
                }
            }
            return raw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unwrap controller response");
            return raw; // fall back to raw document
        }
    }
}

