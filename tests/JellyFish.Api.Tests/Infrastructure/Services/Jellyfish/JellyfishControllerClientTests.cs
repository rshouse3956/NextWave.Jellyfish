using System.Text.Json;
using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Infrastructure.Services.Jellyfish;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace JellyFish.Api.Tests.Infrastructure.Services.Jellyfish;

public class JellyfishControllerClientTests
{
    private static IOptions<JellyfishControllerOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(new JellyfishControllerOptions
        {
            WebSocketUri = "ws://localhost:0/ws",
            RequestTimeoutMs = 500
        });

    [Fact]
    public async Task GetFolders_Unwraps_Wrapper_With_String_Data()
    {
        // Arrange: controller returns wrapper with string Data
        var transport = new Func<JsonDocument, CancellationToken, Task<Result<JsonDocument>>>(
            (req, ct) =>
            {
                var data = JsonSerializer.Serialize(new { patternFileList = Array.Empty<object>() });
                var wrapper = new { Succeeded = true, Data = data, Exception = string.Empty };
                var doc = JsonDocument.Parse(JsonSerializer.Serialize(wrapper));
                return Task.FromResult(Result.Success(doc));
            });
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance, transport);

        // Act
        var res = await client.GetFoldersAsync(CancellationToken.None);

        // Assert
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldNotBeNull();
        res.Value.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RunPattern_Failure_When_Wrapper_Succeeded_False()
    {
        // Arrange: controller returns failure wrapper
        var transport = new Func<JsonDocument, CancellationToken, Task<Result<JsonDocument>>>(
            (req, ct) =>
            {
                var wrapper = new { Succeeded = false, Data = "{}", Exception = "Bad pattern" };
                var doc = JsonDocument.Parse(JsonSerializer.Serialize(wrapper));
                return Task.FromResult(Result.Success(doc));
            });
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance, transport);

        // Act
        var res = await client.RunPatternAsync("Accent", "White", 1, new[] { "All Lights" }, null, CancellationToken.None);

        // Assert
        res.IsFailure.ShouldBeTrue();
        res.Error.Description.ShouldContain("Bad pattern");
    }

    [Fact]
    public async Task Timeout_Or_Transport_Failure_Propagates()
    {
        // Arrange: transport returns failure (simulating timeout)
        var transport = new Func<JsonDocument, CancellationToken, Task<Result<JsonDocument>>>(
            (req, ct) => Task.FromResult(Result.Failure<JsonDocument>(Error.Problem("Request timed out communicating with Jellyfish controller"))));
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance, transport);

        // Act
        var res = await client.GetZonesAsync(CancellationToken.None);

        // Assert
        res.IsFailure.ShouldBeTrue();
        res.Error.Description.ShouldContain("timed out");
    }

    [Fact]
    public async Task RunPattern_Builds_Escaped_CamelCase_Subset_Data_String()
    {
        // Arrange: capture outbound request
        JsonDocument? captured = null;
        var transport = new Func<JsonDocument, CancellationToken, Task<Result<JsonDocument>>>(
            (req, ct) =>
            {
                captured = req;
                // Simulate a simple success response
                var doc = JsonDocument.Parse("{}");
                return Task.FromResult(Result.Success(doc));
            });
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance, transport);

        var details = new PatternDetails
        {
            Colors = new[] { 255, 120, 30 },
            SpaceBetweenPixels = 2,
            EffectBetweenPixels = "No Color Transform",
            // Fields that should be excluded from subset
            ColorPos = new[] { 1, 2, 3 },
            SoffitZone = "Front",
            Cursor = 5,
            Type = "Color",
            Skip = 1,
            NumOfLeds = 1,
            RunData = new PatternRunData
            {
                Speed = 10,
                Brightness = 100,
                Effect = "No Effect",
                EffectValue = 0,
                RgbAdj = new[] { 100, 100, 100 }
            },
            Direction = "Left"
        };

        // Act
        var res = await client.RunPatternAsync("Accent", "Color", 1, new[] { "All Lights" }, details, CancellationToken.None);

        // Assert transport call
        res.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();

        var root = captured!.RootElement;
        root.TryGetProperty("cmd", out var cmdEl).ShouldBeTrue();
        cmdEl.GetString().ShouldBe("toCtlrSet");

        root.TryGetProperty("runPattern", out var rpEl).ShouldBeTrue();
        rpEl.ValueKind.ShouldBe(JsonValueKind.Object);

        // state and zones
        rpEl.GetProperty("state").GetInt32().ShouldBe(1);
        var zonesEl = rpEl.GetProperty("zoneName");
        zonesEl.ValueKind.ShouldBe(JsonValueKind.Array);
        zonesEl.GetArrayLength().ShouldBe(1);
        zonesEl[0].GetString().ShouldBe("All Lights");

        // data must be an escaped JSON string
        var dataEl = rpEl.GetProperty("data");
        dataEl.ValueKind.ShouldBe(JsonValueKind.String);
        var dataStr = dataEl.GetString();
        dataStr.ShouldNotBeNull();
        dataStr!.ShouldContain("\"colors\""); // quick camelCase presence check

        using var inner = JsonDocument.Parse(dataStr);
        var d = inner.RootElement;
        d.TryGetProperty("colors", out var colorsEl).ShouldBeTrue();
        colorsEl.GetArrayLength().ShouldBe(3);
        colorsEl[0].GetInt32().ShouldBe(255);
        colorsEl[1].GetInt32().ShouldBe(120);
        colorsEl[2].GetInt32().ShouldBe(30);

        d.GetProperty("spaceBetweenPixels").GetInt32().ShouldBe(2);
        d.GetProperty("effectBetweenPixels").GetString().ShouldBe("No Color Transform");
        d.GetProperty("type").GetString().ShouldBe("Color");
        d.GetProperty("skip").GetInt32().ShouldBe(1);
        d.GetProperty("numOfLeds").GetInt32().ShouldBe(1);
        d.GetProperty("direction").GetString().ShouldBe("Left");

        var rd = d.GetProperty("runData");
        rd.GetProperty("speed").GetInt32().ShouldBe(10);
        rd.GetProperty("brightness").GetInt32().ShouldBe(100);
        rd.GetProperty("effect").GetString().ShouldBe("No Effect");
        rd.GetProperty("effectValue").GetInt32().ShouldBe(0);
        var adj = rd.GetProperty("rgbAdj");
        adj.GetArrayLength().ShouldBe(3);
        adj[0].GetInt32().ShouldBe(100);
        adj[1].GetInt32().ShouldBe(100);
        adj[2].GetInt32().ShouldBe(100);

        // ensure excluded fields are absent
        d.TryGetProperty("colorPos", out _).ShouldBeFalse();
        d.TryGetProperty("soffitZone", out _).ShouldBeFalse();
        d.TryGetProperty("cursor", out _).ShouldBeFalse();
        // no ledOnPos on this pattern, so the payload is unchanged
        d.TryGetProperty("ledOnPos", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RunPattern_Sends_LedOnPos_After_EffectBetweenPixels_When_Present()
    {
        JsonDocument? captured = null;
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance,
            (req, ct) => { captured = req; return Task.FromResult(Result.Success(JsonDocument.Parse("{}"))); });

        var details = new PatternDetails
        {
            Colors = [255, 0, 0],
            SpaceBetweenPixels = 2,
            EffectBetweenPixels = "No Color Transform",
            LedOnPos = new Dictionary<string, int[]> { ["0"] = [], ["1"] = [0] },
            Type = "Paint",
            Skip = 2,
            NumOfLeds = 1,
            RunData = new PatternRunData { Speed = 1, Brightness = 100, Effect = "No Effect", EffectValue = 0, RgbAdj = [100, 100, 100] },
            Direction = "Center"
        };

        (await client.RunPatternAsync("Legacy", "Paint", 1, ["All Lights"], details, CancellationToken.None)).IsSuccess.ShouldBeTrue();

        using var inner = JsonDocument.Parse(captured!.RootElement.GetProperty("runPattern").GetProperty("data").GetString()!);
        // The controller is order-sensitive; follow the API doc's example order.
        inner.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["colors", "spaceBetweenPixels", "effectBetweenPixels", "ledOnPos", "type", "skip", "numOfLeds", "runData", "direction"]);
        var led = inner.RootElement.GetProperty("ledOnPos");
        led.GetProperty("0").GetArrayLength().ShouldBe(0);
        led.GetProperty("1")[0].GetInt32().ShouldBe(0);
    }

    // A Firework as the controller stores one saved from the Jellyfish app (2026-11-20).
    private const string FireworkJson =
        "{\"type\":\"Firework\",\"colors\":[250,146,51,125,125,125],\"spaceBetweenPixels\":2,\"effectBetweenPixels\":\"No Color Transform\"," +
        "\"colorPos\":[],\"skip\":1,\"numOfLeds\":1,\"runData\":{\"speed\":1,\"brightness\":100,\"effect\":\"No Effect\",\"effectValue\":0," +
        "\"rgbAdj\":[100,100,100]},\"direction\":\"Left\",\"readOnly\":true,\"isDistribution\":true,\"isDownloaded\":false," +
        "\"fireworkSettings\":{\"spawnRate\":0,\"colorPerLED\":2,\"fireworkSpeed\":50}}";

    [Fact]
    public async Task GetPatternDetail_Reads_FireworkSettings()
    {
        var reply = JsonSerializer.Serialize(new { cmd = "fromCtlr", patternFileData = new { folders = "New Years", jsonData = FireworkJson, name = "New Years Fireworks" } });
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance,
            (req, ct) => Task.FromResult(Result.Success(JsonDocument.Parse(reply))));

        var res = await client.GetPatternDetailAsync("New Years", "New Years Fireworks", CancellationToken.None);

        res.IsSuccess.ShouldBeTrue();
        res.Value.Type.ShouldBe("Firework");
        var fw = res.Value.FireworkSettings.ShouldNotBeNull();
        fw.SpawnRate.ShouldBe(0);
        fw.ColorPerLed.ShouldBe(2);
        fw.FireworkSpeed.ShouldBe(50);
    }

    [Fact]
    public async Task RunPattern_Sends_Firework_In_The_Controllers_Order_With_Its_Settings()
    {
        JsonDocument? captured = null;
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance,
            (req, ct) => { captured = req; return Task.FromResult(Result.Success(JsonDocument.Parse("{}"))); });
        var details = JsonSerializer.Deserialize<PatternDetails>(FireworkJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        (await client.RunPatternAsync("New Years", "New Years Fireworks", 1, ["All Lights"], details, CancellationToken.None)).IsSuccess.ShouldBeTrue();

        var data = captured!.RootElement.GetProperty("runPattern").GetProperty("data").GetString()!;
        using var inner = JsonDocument.Parse(data);
        inner.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["type", "colors", "spaceBetweenPixels", "effectBetweenPixels", "skip", "numOfLeds", "runData", "direction", "fireworkSettings"]);
        inner.RootElement.GetProperty("type").GetString().ShouldBe("Firework");
        // exact controller spelling, not camelCase "colorPerLed"
        data.ShouldContain("\"fireworkSettings\":{\"spawnRate\":0,\"colorPerLED\":2,\"fireworkSpeed\":50}");
        data.ShouldNotContain("readOnly");
    }

    [Fact]
    public async Task RunPattern_Firework_Without_Settings_Uses_The_Usual_Payload()
    {
        JsonDocument? captured = null;
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance,
            (req, ct) => { captured = req; return Task.FromResult(Result.Success(JsonDocument.Parse("{}"))); });
        var details = JsonSerializer.Deserialize<PatternDetails>(FireworkJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        details.FireworkSettings = null;   // e.g. synced before fireworkSettings was kept

        await client.RunPatternAsync("New Years", "New Years Fireworks", 1, ["All Lights"], details, CancellationToken.None);

        using var inner = JsonDocument.Parse(captured!.RootElement.GetProperty("runPattern").GetProperty("data").GetString()!);
        inner.RootElement.EnumerateObject().First().Name.ShouldBe("colors");
        inner.RootElement.TryGetProperty("fireworkSettings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetPatterns_Keeps_Exact_Controller_Names_Alongside_Trimmed_Names()
    {
        var list = new
        {
            patternFileList = new object[]
            {
                new { folders = "Special Effects", name = "", readOnly = true },
                new { folders = "Special Effects", name = "Paint By Numbers ", readOnly = true }
            }
        };
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance,
            (req, ct) => Task.FromResult(Result.Success(JsonDocument.Parse(JsonSerializer.Serialize(list)))));

        var res = await client.GetPatternsAsync(CancellationToken.None);

        res.IsSuccess.ShouldBeTrue();
        var p = res.Value.ShouldHaveSingleItem();
        p.Name.ShouldBe("Paint By Numbers");
        p.ControllerName.ShouldBe("Paint By Numbers ");
        p.ControllerFolderName.ShouldBe("Special Effects");
    }

    [Fact]
    public async Task GetPatternDetail_Reads_LedOnPos()
    {
        var jsonData = "{\"colors\":[255,0,0],\"spaceBetweenPixels\":2,\"effectBetweenPixels\":\"No Color Transform\"," +
                       "\"ledOnPos\":{\"0\":[],\"1\":[0]},\"type\":\"Paint\",\"skip\":2,\"numOfLeds\":1,\"direction\":\"Center\"," +
                       "\"runData\":{\"speed\":1,\"brightness\":100,\"effect\":\"No Effect\",\"effectValue\":0,\"rgbAdj\":[100,100,100]}}";
        var reply = JsonSerializer.Serialize(new { cmd = "fromCtlr", patternFileData = new { folders = "Legacy", jsonData, name = "25-Paint Red Orange " } });
        var client = new JellyfishControllerClient(Options(), NullLogger<JellyfishControllerClient>.Instance,
            (req, ct) => Task.FromResult(Result.Success(JsonDocument.Parse(reply))));

        var res = await client.GetPatternDetailAsync("Legacy", "25-Paint Red Orange ", CancellationToken.None);

        res.IsSuccess.ShouldBeTrue();
        res.Value.LedOnPos.ShouldNotBeNull();
        res.Value.LedOnPos!["0"].ShouldBeEmpty();
        res.Value.LedOnPos["1"].ShouldBe([0]);
    }



}
