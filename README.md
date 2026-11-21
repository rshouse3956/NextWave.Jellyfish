# NextWave.Jellyfish
The Jellyfish Lighting System is a custom-built client/server system for controlling Jellyfish Lighting system controllers.  The system is a non-destructive solution, pulling data from the Jellyfish controller and storing the patterns, zones, and folders into a MongoDB database.  The system also provides a web interface for managing the schedules and holidays that drive the lighting system.  The system is designed to be run as a Docker container on a TrueNAS Scale system.  The system is built using .NET 10 and Vue.js.

You can create custom patterns and folders, and use the patterns within schedules, but you cannot persist the new patterns back to the controller.  The patterns are never modified or deleted from the controller, so you can always revert to the original patterns if needed.

The reason for creating the solution was to provide a more robust and flexible solution for managing the lighting system.  I thought the scheduling within the Jellyfish mobile app was too limited, confusing, and tedious for very simple scenarios.

This is an independent project. It is not affiliated with, endorsed by, or supported by JellyFish Lighting; "Jellyfish" is
used only to say which controllers it works with.

The controller integration is based on JellyFish Lighting's publicly available API documentation:
[jellyfishlighting.com/support/api](https://www.jellyfishlighting.com/support/api) (overview) and the
[JellyFish Lighting API Explorer](https://github.com/parkerjfl/JellyfishLightingAPIExplorer) it links to, which has the
full API documentation. That documentation is not included here. The project also relies on some behavior that isn't
documented (fields such as `ledOnPos` and `fireworkSettings`, and how effects render), found by testing on a real
controller, so it may change with controller firmware updates.

**Security:** the API and web UI have no login, and anyone who can reach them can control the lights. Run it on your home
network only; don't forward its ports (8085 API, 8086 mongo-express) to the internet.

## Local configuration

Settings that identify your home (Mongo connection string, controller address, latitude/longitude) are not in the repo.

- **Docker / TrueNAS:** in `.env` (copy `.env.example`); the compose files pass them to the container.
- **`dotnet run` (Development):** in .NET user secrets, which live outside the repo:

```powershell
cd server/JellyFish.Api
dotnet user-secrets set "Mongo:ConnectionString" "mongodb://user:pass@host:27017/"
dotnet user-secrets set "JellyfishController:WebSocketUri" "ws://<controller-ip>:9000/ws"
dotnet user-secrets set "Schedule:Latitude" "<lat>"
dotnet user-secrets set "Schedule:Longitude" "<lon>"
dotnet user-secrets set "Schedule:ElevationMeters" "<metres>"
dotnet user-secrets list
```

Without a location, sunrise/sunset schedules fail with "requires Schedule:Latitude, Longitude, ElevationMeters
configured"; fixed-time schedules still work.

## Tests and Code Coverage

This repository uses xUnit for unit tests with a separate, tests-only solution:

- Tests solution: `NextWave.Jellyfish.Tests.sln`
- Unit test project: `tests/JellyFish.Api.Tests` (targets `server/JellyFish.Api`)
- Coverage: collected via Coverlet, HTML report generated via ReportGenerator

### Prerequisites

- .NET 10 SDK installed

### Run all tests

- From repo root:

```powershell
# tests-only solution
dotnet test NextWave.Jellyfish.Tests.sln --nologo
```

### Generate coverage (Cobertura XML)

- From repo root (uses Coverlet runsettings):

```powershell
dotnet test NextWave.Jellyfish.Tests.sln --nologo --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

- Cobertura XML output will be under the test project's `TestResults` folder, for example:
  `tests/JellyFish.Api.Tests/TestResults/<guid>/coverage.cobertura.xml`

### HTML coverage report (recommended)

A convenience script runs tests, collects coverage, and generates an HTML report using the local ReportGenerator tool.

- From repo root (or any directory):

```powershell
# Runs tests with coverage, restores dotnet tools if needed, writes HTML to ./coverage
./scripts/Test-WithCoverage.ps1

# To automatically open the HTML report after generation
./scripts/Test-WithCoverage.ps1 -Open
```

- The script outputs:
  - TRX test results under `tests/JellyFish.Api.Tests/TestResults/`
  - Cobertura XML under the same `TestResults` subfolder
  - HTML coverage report at `./coverage/index.html`

### Notes

- The script resolves paths relative to the script location; you can run it from any working directory.
- The repository includes a local dotnet tool manifest. The script will run `dotnet tool restore` to ensure `reportgenerator` is available.
- You can customize which assemblies are included/excluded via `coverlet.runsettings`.

## Continuous Integration

GitHub Actions (`.github/workflows/`):

- **CI** (`ci.yml`) runs on every push and pull request: builds and tests the server (`NextWave.Jellyfish.Tests.sln`), builds the Vue client, then builds the container image. On `main` and version tags it also publishes the image to GitHub Container Registry (GHCR).
- **Cleanup** (`cleanup.yml`) runs weekly and from the Actions tab: deletes old workflow runs, artifacts and stale caches. Run it by hand with **Also prune images** to delete old image versions from GHCR (keeps the newest 15 by default).

### Publishing the image (GHCR)

Images are published by CI; there is no manual push step. Image: `ghcr.io/rshouse3956/jellyfish`

| Tag | When |
|---|---|
| `latest` | Every push to `main` |
| `sha-<commit>` | Every published build |
| `X.Y.Z`, `X.Y` | Pushing a version tag, e.g. `git tag v2026.11.1 && git push origin v2026.11.1` |

Branches and pull requests build the image without publishing it.

One-time package settings (GitHub > Packages > jellyfish > Package settings), after the first publish:

- **Manage Actions access:** give this repository the **Admin** role so the Cleanup workflow can prune old versions.
- **Visibility:** packages start private. Either make it public, or sign TrueNAS in to GHCR (below).

To build the image locally instead:

```powershell
docker buildx build --pull -t jellyfish:local .
```

### Deployment to TRUENAS SCALE
1. If the package is private, add a GHCR registry credential in TrueNAS (Apps > Configuration > Manage Container Images / registries):
   - Registry: `ghcr.io`
   - Username: your GitHub username
   - Password: a GitHub personal access token (classic) with the `read:packages` scope
2. Copy `.env.example` to `.env` beside the compose file and fill it in: Mongo credentials, `ME_USER`/`ME_PASS` (the
   mongo-express login), `CONTROLLER_WS_URI`, and `SCHEDULE_LATITUDE`/`SCHEDULE_LONGITUDE`/`SCHEDULE_ELEVATION_METERS`.
   The compose file stops with "Set … in .env" if a required one is missing. Mongo's port isn't published; uncomment
   `27018:27017` in the compose file if another machine needs it.
   The image is set once at the top of `docker-compose.truenas.yml` (`x-image`) from `JELLYFISH_IMAGE` and `JELLYFISH_TAG`;
   both default to `ghcr.io/rshouse3956/jellyfish:latest` when unset.
3. Pull the image from Apps/Configuration/Manage Container Images/Pull Image
   - `ghcr.io/rshouse3956/jellyfish:latest` (or the tag set in `JELLYFISH_TAG`)
4. Through Apps/Discover Apps/Install via YML, use the docker-compose.truenas.yml file in the root of this repository.
5. To update: set `JELLYFISH_TAG` in `.env` to the new `sha-<commit>` or `X.Y.Z` tag (or leave it `latest` and pull again), then redeploy the app.
   Pinning a tag gives repeatable deploys; to roll back, set the previous tag and redeploy.
6. **When `docker-compose.truenas.yml` changes in the repo, paste it into TrueNAS again** (Apps > the app > Edit). TrueNAS keeps
   its own copy of the YAML from "Install via YML" and doesn't pick up changes: new settings in `.env` have no effect until
   the YAML that passes them to the container is updated. Check with `docker exec jellyfish-api printenv`.


### Swagger in Production

Swagger UI is enabled automatically in Development. In Production it is disabled by default. To enable it, set the configuration key `Swagger:Enabled` to `true` in one of the following ways:

- appsettings.Production.json

```json
{
  "Swagger": { "Enabled": true }
}
```

- Environment variable (ASP.NET Core uses `__` as a section separator):

```bash
# Linux / shell
export Swagger__Enabled=true
```

- docker-compose (recommended for deployments)

```yaml
services:
  jellyfish:
    image: ghcr.io/rshouse3956/jellyfish:latest # or a specific tag
    environment:
      - Swagger__Enabled=true
```

Once enabled, Swagger UI is available at `/swagger` and the OpenAPI JSON at `/swagger/v1/swagger.json` (default Kestrel binding is `http://0.0.0.0:5300`).

### Diagnostics endpoint (Schedules)

The schedules diagnostics route is disabled by default and can be toggled via configuration.

- Endpoint (when enabled): `GET /api/schedules/diagnostics`
- Config key: `Diagnostics:Enabled` (bool)
- Default: `false`

Enable it in one of the following ways, then restart the app/container:

- appsettings.Production.json

```json
{
  "Diagnostics": { "Enabled": true }
}
```

- Environment variable

```bash
# Linux / shell
export Diagnostics__Enabled=true

# Windows PowerShell
$env:Diagnostics__Enabled = "true"
```

- docker-compose (recommended)

```yaml
services:
  jellyfish:
    image: ghcr.io/rshouse3956/jellyfish:<tag>
    environment:
      - Diagnostics__Enabled=true
```

Turn it off (or remove the variable) when you are done troubleshooting.


## Pattern Assistant (AI)

Describe a pattern in plain words ("a fading block of 2 green and 2 red", "slow red and white candy cane chase") and the
assistant designs it with an open-source model running on [Ollama](https://ollama.com). You can preview it on the lights,
refine it ("slower", "add some white") and save it as a custom pattern. Nothing is written to the controller.

The assistant is off by default. Saved patterns and schedules never need it, so if the Ollama server is down, only the
assistant is unavailable.

### Using it

1. Open **Patterns**, pick the folder to save into, and click ✨ (shown only when the assistant is enabled).
2. Describe the pattern and click **Generate** (Ctrl+Enter). Replies take a few seconds with a GPU, longer on CPU.
3. Check the strip, colors and settings, then **Preview on lights** for the zones you tick. **Stop preview** (or closing
   the panel) turns those zones off and hands them back to the active schedule.
4. Type a change and click **Refine** as often as you like, then **Save to &lt;folder&gt;**.

Tips for requests:

- **Counts are per color:** "2 green and 2 red" means 2 lights of each.
- **Motion:** patterns stay still unless you ask for motion ("chase", "moving", "running", "scrolling").
- **Fading and blending:** "fading" dims between colors (`Fade`). "Blend", "gradient" and "rainbow" pass through the
  colors in between (`Progression`). Without those words you get hard-edged blocks (`Repeat`).
- **Animations:** describe the animation, or name it as the Jellyfish app does. The panel shows which one you got.

  | Animation | Ask for it with |
  |---|---|
  | Color (static) | the default; "static", "not moving" |
  | Chase | "chase", "moving", "running", "scrolling" |
  | Chase with Progression | a chase plus "blend" or "gradient" |
  | Chase with Fill Black | a chase plus "gaps" or "spaced out" |
  | Sequence | "sequence", "cycle through", "switch between" |
  | Sequence with Fade | "breathing", "pulsing" |
  | Paint | "paint", "wipe", "fill in" |
  | Multi-Paint | "multi-paint", "filling in sections" |
  | Stacker | "stack", "building up" |
  | Fireworks | "fireworks", "bursts", "exploding" |

  Add **Twinkle** ("twinkling", "sparkly") or **Lightning** ("lightning", "storm") to any of them; "no twinkle"
  removes it. Lightning on its own ("purple lightning storm") becomes a slow blending chase, like the app's Purple
  Lightning Storm; say "static" to keep it still. Every look was checked on the lights (2026-11-20). Paint's Center
  direction fills from alternate ends (Left always fills from the left).

  Stacker and breathing set their own pace from speeds measured on the lights, and "faster" or "slower" moves one
  step. Stacker changes its lights per step (2, 4 or 8: about 5 s, 1 s or faster between stacked lights; 8 hasn't
  been checked). Breathing changes its speed (10, 3 or 1: a cycle of about 10 s, 6 s or 3 s). By default Stacker
  uses 4 and breathing uses 1 ("slowly" gives 3).

  **Fireworks** isn't in the controller API doc. It's the `Firework` type plus a `fireworkSettings` block
  (`spawnRate`, `colorPerLED`, `fireworkSpeed`), learned from a pattern saved in the Jellyfish app. The assistant
  picks the colors and uses that pattern's fields. "Faster" or "slower" moves `fireworkSpeed` by 30 within 20–80
  (higher is faster; the default is 50). `spawnRate` and `colorPerLED` made no visible difference, so they stay at 0
  and 2.
- **Wrong colors:** the model sometimes gets team or brand colors wrong. Refine with the right ones ("use orange and navy").

### Setting it up

1. **Run Ollama** somewhere the API can reach, and pull a model:
   - **On another machine (recommended if it has a GPU):** run Ollama there, e.g. Docker Desktop with
     `docker run -d --gpus all -p 11434:11434 -v ollama:/root/.ollama --name ollama ollama/ollama`. Ollama outside
     Docker only listens on localhost by default; set `OLLAMA_HOST=0.0.0.0` so other machines can reach it. Give the
     machine a fixed IP (DHCP reservation), and allow port 11434 through its firewall.
   - **On TrueNAS:** uncomment the `ollama` service in `docker-compose.truenas.yml`. It's CPU-only: expect 20–40+ seconds
     per reply with a 7B model and about 5–6 GB of RAM. If replies time out, add
     `- PatternAssistant__TimeoutSeconds=300` to the API's environment.
   - **Locally:** `docker compose --profile assistant up -d` starts the `ollama` service in `docker-compose.yml`.

   Then pull a model on that Ollama server, e.g. `docker exec <ollama-container> ollama pull qwen2.5:14b`.
2. **Check that the API host can reach it**, e.g. from the TrueNAS shell: `curl http://<ollama-host>:11434/api/version`.
3. **Set these in `.env`** (they are passed to the API by both compose files) and redeploy:

   ```
   PATTERN_ASSISTANT_ENABLED=true
   OLLAMA_BASE_URL=http://<ollama-host>:11434
   OLLAMA_MODEL=qwen2.5:14b
   ```

   On TrueNAS, make sure the app's YAML includes the `PatternAssistant__*` lines (step 6 of the deployment section).
4. **Check the status** at `GET /api/pattern-assistant/status`. `"ready": true` means it's working. Otherwise the
   `message` says why (not enabled, Ollama unreachable, or "run: ollama pull <model>").

### Models

| Model | Notes |
|---|---|
| `qwen2.5:14b` | Recommended with a GPU (about 9 GB). Best at following instructions; replies in about 3–4 s on a desktop GPU. |
| `qwen2.5:7b` | The default setting; smaller (about 5 GB) and the practical choice on CPU. Not yet tested here. |
| `llama3.1:8b` | Works, but ignored parts of refine requests in testing. |

### Settings

| Setting (`appsettings.json` / env var) | Default | |
|---|---|---|
| `PatternAssistant:Enabled` / `PatternAssistant__Enabled` | `false` | Turns the assistant on |
| `PatternAssistant:BaseUrl` | `http://localhost:11434` | Ollama URL |
| `PatternAssistant:Model` | `qwen2.5:7b` | Must be pulled on the Ollama server |
| `PatternAssistant:TimeoutSeconds` | `120` | Per reply; the browser waits up to 3 minutes |
| `PatternAssistant:Temperature` | `0.2` | Low, so the same request gives the same pattern |
| `PatternAssistant:MaxExamples` | `4` | Stored patterns shown to the model as examples |

In the compose files, `PATTERN_ASSISTANT_ENABLED`, `OLLAMA_BASE_URL` and `OLLAMA_MODEL` in `.env` map to the first three.
Development (`appsettings.Development.json`) enables the assistant against `http://localhost:11434` with `qwen2.5:14b`.

### How it works

`POST /api/pattern-assistant/generate` sends the request to the model with a JSON schema (Ollama structured output), so
every reply is valid pattern JSON. The server then:

- **Fixes the settings:** clamps every value into range.
- **Applies rules from your wording:** animation type, effect, run effect (Twinkle, Lightning), minimum gradient width
  and Multi-Paint section size are set by deterministic rules based on what you wrote. The newest request wins, so
  "make it a chase" overrides an earlier "breathing". The animations and their trigger words are one table,
  `Features/PatternAssistant/PatternAnimations.cs`, which also writes the animation list in the prompt.
- **Converts speed:** maps the model's 1–10 speed level to the controller's inverted speed.
- **Logs every request:** each generate or refine is written to the audit log as `GeneratePattern`, with the prompt,
  model, examples used and the resulting pattern, or the error and the model's last raw reply when it failed.

Examples come from your stored patterns: up to 2 of the animation you asked for, then ones matching your words,
preferring patterns synced from the controller over ones created in the app. How
each effect actually renders was measured on the lights; see
[docs/pattern-assistant-feasibility.md](docs/pattern-assistant-feasibility.md) for the measurements and design.


### TODO

- [X] Add support for multiple zones in a pattern (currently only one zone is supported)
- [X] Add Calendar option to select a range of dates instead of using Cron syntax
- [X] Review constraints to make sure we can have multiple patterns with the same name in different folders
