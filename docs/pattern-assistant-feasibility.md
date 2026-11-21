# Pattern Assistant (Ollama) — Feasibility & Implementation Plan

Status: Proposed · Date: 2026-11-18

## Goal

Add an AI assistant to the web UI that creates new lighting patterns from a plain-language description (e.g. *"slow warm candy-cane chase"*), using **Ollama** and open-source models that run locally. The assistant should let the user preview the pattern on the real lights, refine it, and save it as a custom pattern.

## Verdict

**Feasible and low-risk.** Calling the model is the easy part. The real challenge is that the controller's pattern semantics are only loosely documented. The repo already has what it needs to work around that: inline previews on the controller, custom-pattern storage, and synced factory patterns to use as examples.

## Why it fits

1. **The output is small and has a fixed shape.** A pattern is the `PatternDetails` object (`server/JellyFish.Api/Domain/PatternDetail.cs`). It has about 11 fields; most are numbers, and four take values from a short list. Ollama's *structured outputs* option (`format` set to a JSON schema) forces the model to return valid JSON in exactly that shape. 7–8B models handle this reliably.
2. **Live preview needs no controller changes.** `JellyfishControllerClient.RunPatternAsync` already sends `runPattern` with `file: ""` and the full pattern as `data` (the "advanced run pattern" command in the vendor API doc). Generated patterns can be shown on the lights without writing anything to the controller. This keeps to the project's rule that nothing is written back to the controller.
3. **Saving is already built.** Custom patterns are stored in Mongo through:
   - `POST /api/folders/{folderName}/patterns` (create)
   - `PUT  /api/folders/{folderName}/patterns/{name}/details` (upsert details)

   Schedules can already run them, because `runPatternWithSyncCheck` (`client/src/utils/runPatternHelper.ts`) sends the stored details.
4. **The current editor is a raw JSON text box** (`client/src/pages/Patterns.vue`), so any assistant is a big improvement.
5. **Built-in examples for the prompt.** `SyncAllPatternDetailsFromController` stores the factory patterns' details in Mongo (e.g. "Christmas Tree", "Red Yellow Green Blue"). Putting 3–5 relevant ones in the prompt as examples shows the model how `type`, `skip` and `spaceBetweenPixels` combine in practice. The vendor doc doesn't explain this well.

## Pattern schema reference

Based on the [Jellyfish Lighting API documentation](https://github.com/parkerjfl/JellyfishLightingAPIExplorer), an early data-loading script (since removed) and the domain model.

| Field | Type | Allowed values / constraints | Notes |
|---|---|---|---|
| `colors` | int[] | Flat RGB triples, each 0–255; length a multiple of 3; max 90 values (30 colors) | e.g. `[255,0,0, 255,255,255]` = red, white |
| `spaceBetweenPixels` | int | ≥ 0 | Meaning depends on `effectBetweenPixels`; see **Controller rendering (measured)** below |
| `effectBetweenPixels` | enum | `No Color Transform`, `Repeat`, `Progression`, `Fade`, `Fill with Black` | The API doc's `No Color Transformation` is **not** recognized (verified on the controller) |
| `type` | enum | `Color`, `Chase`, `Paint`, `Stacker`, `Sequence`, `Multi-Paint` | How the colors interact and move |
| `skip` | int | ≥ 1 | Either a speed boost or the number of lights per step, depending on `type` |
| `numOfLeds` | int | ≥ 1 | Only used by `Multi-Paint` |
| `runData.speed` | int | 1 = fastest; higher = slower (shown as 1/x) | **Inverted.** LLMs will get this backwards |
| `runData.brightness` | int | 0–100 (percent) | |
| `runData.effect` | string | Always `No Effect` | Not used by the controller |
| `runData.effectValue` | int | Always `0` | Not used |
| `runData.rgbAdj` | int[3] | Always `[100,100,100]` | Not used |
| `direction` | enum | `Left`, `Right`, `Center` | The doc lists Left/Right only, but its examples and real controller patterns use `Center` |

Fields stored but not sent to the controller: `colorPos`, `soffitZone`, `cursor`. The model should not generate these.

The doc says the controller is sensitive to **field order**. The existing serializer in `RunPatternAsync` already sends fields in the correct order. Reuse it; don't hand-build payloads.

## Issues to handle

| Issue | Where | Mitigation |
|---|---|---|
| `No Color Transformation` (doc's list) vs `No Color Transform` (doc examples, the early loader script, UI default) | API PDF | **Resolved (2026-11-18):** the controller treats any unknown value, including `No Color Transformation`, as no transform, so the difference can't be seen on the lights. Only `No Color Transform` is allowed |
| `direction` `Center` is not in the documented list | API PDF | **Resolved:** real controller patterns use `Center`; `Left`/`Right`/`Center` are allowed |
| The controller silently ignores unknown enum values | Controller | Validation must catch typos; the controller won't report them |
| Inverted `speed` | API PDF | The model outputs `speedLevel` 1–10 (slow→fast); the server converts it to the controller's `speed` |
| No range validation on pattern details | `UpsertPatternDetailCommandValidator.cs` | Add FluentValidation rules plus clamping on the server (useful even without AI) |
| CPU-only inference on TrueNAS: about 5–20 s per reply for a 7–8B Q4 model, ~6–8 GB RAM | deployment | Make the Ollama URL configurable (it can point to a GPU desktop); stream the reply or show progress |
| API has no authentication, so anyone on the LAN can use up CPU | `Program.cs` | Acceptable for a home network; optionally add ASP.NET rate limiting on the assistant endpoint |
| The model may not know what each `type` looks like | — | Use factory patterns as examples, and rely on the preview/refine loop |

## Architecture

```
Vue (Patterns page)                 JellyFish.Api                         Ollama
───────────────────                 ─────────────                         ──────
Prompt box ──POST /api/pattern-assistant/generate──► GeneratePatternCommandHandler
                                     │  1. load few-shot examples (IPatternDetailRepository)
                                     │  2. build system prompt + JSON schema
                                     │  3. IChatClient ─────────────────────► /api/chat (format=schema)
                                     │  4. map speedLevel → speed, validate, clamp
                         ◄───────────┘  returns { details, suggestedName, explanation }
Preview ──existing POST run-pattern (data inline, file "")──► JellyfishControllerClient ──► Controller
Save    ──existing create pattern + PUT details──► Mongo
```

### Infrastructure

- Add an optional `ollama` service to `docker-compose.yml` and `docker-compose.truenas.yml`:

  ```yaml
  ollama:
    image: ollama/ollama:latest
    container_name: jellyfish-ollama
    restart: unless-stopped
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    # GPU (optional):
    # deploy:
    #   resources:
    #     reservations:
    #       devices: [{ driver: nvidia, count: all, capabilities: [gpu] }]
  ```

- Add API settings (same pattern as `JellyfishController__*`):

  ```
  PatternAssistant__Enabled=true
  PatternAssistant__BaseUrl=http://ollama:11434
  PatternAssistant__Model=qwen2.5:7b
  PatternAssistant__TimeoutSeconds=120
  ```

  Add `OLLAMA_BASE_URL` / `OLLAMA_MODEL` to `.env.example`.
- **Candidate models:** `qwen2.5:7b` or `qwen3:8b` (good at structured output), `llama3.1:8b`. If quality isn't good enough, try `qwen2.5:14b` or `mistral-small`.
- Pull the model once: `docker exec jellyfish-ollama ollama pull qwen2.5:7b`.

### Server: `Features/PatternAssistant/`

Follow the existing MediatR feature structure.

```
Features/PatternAssistant/
  Commands/GeneratePatternCommand.cs
  Commands/GeneratePatternCommandHandler.cs
  Commands/GeneratePatternCommandValidator.cs
  GeneratedPatternDto.cs
  PatternAssistantPrompt.cs        // system prompt + JSON schema
  PatternDetailsSanitizer.cs       // clamp/normalize (shared with Upsert validator rules)
Infrastructure/Services/PatternAssistant/
  PatternAssistantOptions.cs
Api/Endpoints/PatternAssistantEndpoints.cs
```

**Endpoints**

- `GET  /api/pattern-assistant/status` → `{ enabled, model, reachable }`. The UI uses this to hide or disable the feature.
- `POST /api/pattern-assistant/generate`

  ```jsonc
  // request
  {
    "prompt": "slow warm candy-cane chase",
    "currentDetails": null,          // PatternDetailsDto, present when refining
    "history": []                    // optional prior prompts for multi-turn refine
  }
  // response
  {
    "suggestedName": "Candy Cane Chase",
    "explanation": "Red/white alternating chase, 4-pixel segments, moving right slowly.",
    "details": { /* PatternDetailsDto */ }
  }
  ```

**Client library:** `Microsoft.Extensions.AI` + `OllamaSharp` (`IChatClient`). This keeps the provider swappable (any OpenAI-compatible server works). A plain `HttpClient` against `/api/chat` is also acceptable.

**Model output schema** (sent as Ollama `format`; the server maps it to `PatternDetailsDto`):

```json
{
  "type": "object",
  "required": ["suggestedName","explanation","colors","type","effectBetweenPixels","spaceBetweenPixels","skip","numOfLeds","speedLevel","brightness","direction"],
  "properties": {
    "suggestedName":      { "type": "string", "maxLength": 40 },
    "explanation":        { "type": "string", "maxLength": 300 },
    "colors": {
      "type": "array", "minItems": 1, "maxItems": 30,
      "items": { "type": "object", "required": ["r","g","b"],
                 "properties": { "r": {"type":"integer","minimum":0,"maximum":255},
                                 "g": {"type":"integer","minimum":0,"maximum":255},
                                 "b": {"type":"integer","minimum":0,"maximum":255} } }
    },
    "type":               { "enum": ["Color","Chase","Paint","Stacker","Sequence","Multi-Paint"] },
    "effectBetweenPixels":{ "enum": ["No Color Transform","Repeat","Progression","Fade","Fill with Black"] },
    "spaceBetweenPixels": { "type": "integer", "minimum": 0, "maximum": 50 },
    "skip":               { "type": "integer", "minimum": 1, "maximum": 50 },
    "numOfLeds":          { "type": "integer", "minimum": 1, "maximum": 50 },
    "speedLevel":         { "type": "integer", "minimum": 1, "maximum": 10, "description": "1 = slowest, 10 = fastest" },
    "brightness":         { "type": "integer", "minimum": 1, "maximum": 100 },
    "direction":          { "enum": ["Left","Right","Center"] }
  }
}
```

The model returns colors as `{r,g,b}` objects rather than a flat array, which avoids length errors that aren't multiples of 3. The server flattens them. The upper limits (50) are placeholders; tune them against real factory patterns.

**`speedLevel` → controller `speed`** (starting point; tune by watching the lights):

| speedLevel | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| speed | 100 | 75 | 50 | 35 | 25 | 15 | 10 | 5 | 3 | 1 |

When refining, convert `currentDetails.runData.speed` back to the nearest `speedLevel` before sending it to the model.

**Server sets these constants:** `effect = "No Effect"`, `effectValue = 0`, `rgbAdj = [100,100,100]`. `colorPos`, `soffitZone` and `cursor` are left null or empty.

**System prompt outline** (`PatternAssistantPrompt.cs`):

1. Role: "You design patterns for a Jellyfish permanent outdoor LED lighting controller."
2. A short description of each field and enum value (from the schema table above), including what each `type` does visually as far as we know.
3. Rules: use a few colors unless asked for a rainbow; set brightness to 100 unless asked; prefer `Color` for solid static looks and `Chase` for movement; holiday and team color conventions.
4. Few-shot examples: 3–5 synced factory patterns, chosen by keyword overlap between the prompt and pattern/folder names (e.g. "christmas" → the Christmas folder). Fall back to a fixed default set. Convert them to the model schema (including `speedLevel`) before adding them.
5. When `currentDetails` is present: "Modify the current pattern according to the request; change only what is asked."

**Handler flow**

1. Return a clear error if `PatternAssistant:Enabled` is false.
2. Load examples from `IPatternDetailRepository`.
3. Call `IChatClient` with the schema, `temperature ≈ 0.4`, and the configured timeout.
4. Deserialize the reply. If it fails, retry once and include the error message.
5. Map the result to `PatternDetailsDto`, run `PatternDetailsSanitizer` (clamp ranges, cap at 30 colors, normalize enums), and return it.
6. Do **not** run the pattern or persist anything; the client does that.

### Validation improvements (do first; useful without AI)

Extend `UpsertPatternDetailCommandValidator` (and add matching rules for `RunPatternRequest.Data`):

- `Colors`: not empty, `Length % 3 == 0`, `Length <= 90`, every value 0–255
- `RunData.Brightness`: 0–100
- `RunData.Speed`: ≥ 0 (existing stored data uses 0)
- `Skip`, `NumOfLeds`, `SpaceBetweenPixels`: ≥ 0; `RunData.RgbAdj`: 3 values
- `Type`, `EffectBetweenPixels`, `Direction`: must be one of the allowed values (keep the lists in a shared `PatternVocabulary` static class used by both the validator and the assistant schema)

### Client

- `client/src/api/patternAssistant.ts`: `getStatus()`, `generate(req)`.
- `client/src/components/PatternAssistantPanel.vue`, opened from a ✨ button on the Patterns page (only shown when `status.enabled`):
  - Prompt textarea, then **Generate** (with a spinner; may take 5–20 s on CPU)
  - Result: suggested name (editable), explanation, color swatches, and the summary fields (type, effect, speed level, direction, brightness)
  - Collapsible JSON view that reuses the existing details editor style
  - **Preview**: zone chips (reuse the zone loading from `Patterns.vue`), then `runPattern(folder, name, 1, zones, details)` with the generated details inline
  - **Refine**: a follow-up prompt that sends `currentDetails`
  - **Stop preview**: `runPattern(..., 0, zones)`
  - **Save**: `createPattern(folder, { name, isReadOnly: false })`, then `upsertPatternDetails(folder, name, details)`, then refresh the list
- Optional: a **Generate pattern** button on the Schedules page later.

## Implementation checklist

- [x] Check on the controller: `No Color Transform` vs `No Color Transformation` (only `No Color Transform` is recognized); `Center` is a valid `direction`
- [x] Add a `PatternVocabulary` constants class
- [x] Strengthen `UpsertPatternDetailCommandValidator` (+ tests); structure-only rules on `RunPatternCommandValidator`
- [x] Add `PatternAssistantOptions` + configuration binding in `Program.cs`
- [x] Add NuGet `Microsoft.Extensions.AI` + `OllamaSharp`; register `IChatClient`
- [x] Add `PatternAssistantPrompt` (system prompt, schema, example selection)
- [x] Add `PatternDetailsSanitizer` + `speedLevel` mapping (+ unit tests)
- [x] Add `GeneratePatternCommand`/handler/validator (+ tests with a fake `IChatClient`)
- [x] Add `PatternAssistantEndpoints` (`/status`, `/generate`); map in `Program.cs`
- [x] Add the `ollama` service + env vars to compose files and `.env.example`
- [x] Client: `patternAssistant.ts` API + `PatternAssistantPanel.vue` + ✨ button on `Patterns.vue` (shown only when the assistant is enabled)
- [x] Tune against real lights: effect/width rendering measured; type, effect and width rules added (speed map still untested)
- [x] README: a "Pattern Assistant (AI)" section (using it, setup, models, settings)

### Server-side results (2026-11-18)

Tested end to end against a local Ollama 0.16.1 (GPU) with no stored examples (production also gets examples from Mongo):

| Model | Reply time | Notes |
|---|---|---|
| `qwen2.5:14b` | 3–4 s (11 s cold) | Best instruction following; refine changed exactly what was asked. Got team colors wrong (Broncos as orange/green). |
| `llama3.1:8b` | 2–7 s | Better team colors, but added black gaps to a plain warm white and ignored "add gold" when refining. |

- Every reply was valid JSON on the first attempt (structured output via Ollama `format`, verified by a test against OllamaSharp).
- Prompt guidance on the default effect (`No Color Transform`) and block width (3–8 pixels for stripes) fixed odd choices like `Fill with Black` with 1-pixel blocks.
- Keep a Qwen 2.5 model as the default; see the `qwen2.5:7b` comparison below.

### `qwen2.5:7b` vs `qwen2.5:14b` (2026-11-21)

The same 10 prompts plus refines, sent through `/generate` on two local API instances (one per model) with the
218 stored patterns as examples. Ollama in Docker on the desktop GPU, models warmed up first.

| Prompt | `qwen2.5:14b` | `qwen2.5:7b` |
|---|---|---|
| Reply time (typical) | 6.6–8.5 s, refines 2.5–3.6 s | 4.7–7 s, refines 2.8–3.6 s |
| slow warm candy-cane chase | Red + warm white, Chase | **Orange** (255,120,30) + white: missed the red |
| plain warm white | Warm white, Color | Same color; `Repeat` width 1 (draws the same) |
| Denver Broncos colors → "use orange and navy" | Orange + navy on the first try | Orange + royal blue; navy after the refine |
| red and green stripes → "add gold" → "faster" | Added gold; speed 10 → 1 | Added gold; speed 10 → 3 |
| smooth rainbow gradient, slow | Chase + Progression, 6 hues, width 14 | Chase + Progression, 8 hues, width 10 |
| slow blue breathing | Sequence + Fade, speed 3 | Same |
| white twinkle on dark blue | Color + Twinkle, `Repeat` width 3 | Color + Twinkle, width 1 |
| halloween orange/purple + lightning | Chase + Progression + Lightning | Same |
| fireworks | Firework, red/green/blue | Firework; explanation mentions white charges not in the colors, named "New Year's Fireworks" |
| stacking red white blue | Stacker, 2 lights per step | Same |

- Every reply from both models was valid, and the server rules (animation type, effect, run effect, speed) gave the same
  structure for both.
- 7b's differences are in color choice and wording, not structure; the one real miss (candy cane) is fixed by a refine.
- On this GPU, 7b is about 2 s faster per request. On a CPU (the TrueNAS option) the gap is much larger, so 7b stays the
  default for CPU installs; use 14b where a GPU is available (the deployed app already does).
- World-knowledge gaps (team colors) are expected; the refine step ("use orange and navy") covers them.

### Controller rendering (measured 2026-11-18)

Green and red, run directly on the lights, varying `type`, `effectBetweenPixels` and `spaceBetweenPixels`:

| Effect | `spaceBetweenPixels` means | Edges | Notes |
|---|---|---|---|
| `No Color Transform` | **Ignored**: every color is 1 light | hard | Width 2 and 8 both gave 1 light per color |
| `Repeat` | Lights per color | hard | Width 2 → 2 lights, 8 → 8 lights. Use this for stripes and blocks |
| `Fade` | Lights per color | dims between colors | No in-between colors: green→red dims down and back up. One color alone stays even. Static (`Color`): visible at 2 and 8. Moving (`Chase`): visible at 4 and 8, not at 2 |
| `Fill with Black` | Unlit lights **after** each color | gaps | Width 8 → 1 lit light, then 8 unlit |
| `Progression` | Lights per color | blended through hues | Width 8 (static): green changed to red through orange. The prompt uses it for blends, gradients and rainbows |

What the assistant does with this:

- The prompt describes each effect as measured above, and says a moving fade needs at least 4 lights per color.
- The server converts `No Color Transform` with a width above 1 to `Repeat`, since a wider hard-edged block is what the model means.
- Stored patterns shown as examples report `No Color Transform` with width 1, matching what the lights draw.
- The panel's strip simulation follows the same rules, and labels the width "Lights per color" or "Gap after each color".
- Not measured: widths 3 for a moving fade, and `Progression` while moving.

**Estimate:** about 2–4 days for the MVP, plus tuning time with the real lights.

## Open questions

- Will Ollama run on the TrueNAS host (CPU) or on a separate GPU machine?
- ~~Which folder should hold generated patterns (e.g. a dedicated `AI` folder) vs. a folder the user picks?~~
  **Decided (2026-11-21): the folder selected on the Patterns page.** Generated patterns sit next to related factory
  patterns. This is safe in the controller's read-only folders: the syncs never delete patterns, and editable patterns
  already live in those folders.
- ~~Should prompts and replies be written to the audit log (`IAuditLogService`)?~~ **Done (2026-11-21):** one
  `GeneratePattern` entry per request (prompt, model, examples, then the pattern or the error and last raw reply).
- ~~Later: should a schedule be able to generate a pattern from a description at run time?~~ **Decided (2026-11-21):
  no.** Schedules only run saved patterns, so what runs is always known and repeatable.
