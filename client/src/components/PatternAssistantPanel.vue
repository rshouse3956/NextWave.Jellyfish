<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import {
  apiErrorMessage,
  generatePattern,
  getPatternAssistantStatus,
  type GeneratedPatternDto,
  type PatternAssistantStatusDto,
} from '../api/patternAssistant'
import { createPattern, upsertPatternDetails } from '../api/patterns'
import { runPattern } from '../api/jellyfish'
import { getZones } from '../api/zones'

const props = defineProps<{
  /** Folder the pattern is saved to (and previews are logged under). */
  folder: string
  /** Names already in the folder, to catch duplicates before saving. */
  existingNames: string[]
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', name: string): void
}>()

/** Previews run inline data, so the controller ignores this name; it shows in active zones and the audit log. */
const PREVIEW_NAME = 'AI Preview'
const MAX_HISTORY = 10
const STRIP_PIXELS = 72

const status = ref<PatternAssistantStatusDto | null>(null)
const statusError = ref('')

const prompt = ref('')
const history = ref<string[]>([])
const result = ref<GeneratedPatternDto | null>(null)
const name = ref('')
const nameEdited = ref(false)
const showJson = ref(false)

const zones = ref<string[]>([])
const selectedZones = ref<string[]>([])
const previewing = ref(false)
const previewZones = ref<string[]>([])

const busy = ref<'' | 'generate' | 'preview' | 'stop' | 'save'>('')
const error = ref('')
const notice = ref('')
let abort: AbortController | null = null

const ready = computed(() => !!status.value?.ready)
const canGenerate = computed(() => ready.value && !busy.value && prompt.value.trim().length > 0)
const details = computed(() => result.value?.details ?? null)

const nameProblem = computed(() => {
  const n = name.value.trim()
  if (!n) return 'Enter a name'
  if (/[\/\\]/.test(n)) return 'Names cannot contain / or \\'
  if (props.existingNames.some(x => x.toLowerCase() === n.toLowerCase())) return `"${n}" already exists in ${props.folder}`
  return ''
})

// What spaceBetweenPixels means depends on the effect (see the strip simulation below)
const widthLabel = computed(() =>
  details.value?.effectBetweenPixels?.toLowerCase() === 'fill with black' ? 'Gap after each color' : 'Lights per color')
const widthValue = computed(() => {
  const d = details.value
  if (!d) return ''
  const n = d.effectBetweenPixels?.toLowerCase() === 'no color transform' ? 1 : d.spaceBetweenPixels
  return `${n} ${n === 1 ? 'light' : 'lights'}`
})

const swatches = computed(() => toRgb(details.value?.colors ?? []))
const strip = computed(() => (details.value ? simulateStrip(details.value, STRIP_PIXELS) : []))

onMounted(async () => {
  try {
    status.value = await getPatternAssistantStatus()
  } catch (e) {
    statusError.value = apiErrorMessage(e, 'Could not get the assistant status')
  }
  try {
    zones.value = (await getZones()).map(z => z.name)
    selectedZones.value = zones.value.slice()
  } catch { /* zones only matter for previews */ }
})

onBeforeUnmount(() => abort?.abort())

async function generate() {
  if (!canGenerate.value) return
  error.value = ''
  notice.value = ''
  busy.value = 'generate'
  abort = new AbortController()
  const request = prompt.value.trim()
  try {
    const refining = !!result.value
    const res = await generatePattern(
      {
        prompt: request,
        currentDetails: refining ? result.value!.details : null,
        currentName: refining ? name.value.trim() || result.value!.suggestedName : null,
        history: refining ? history.value.slice(-MAX_HISTORY) : [],
      },
      abort.signal,
    )
    result.value = res
    history.value = refining ? [...history.value, request].slice(-MAX_HISTORY) : [request]
    // Keep a name the user typed; otherwise follow the model's suggestion
    if (!refining || !nameEdited.value) {
      name.value = res.suggestedName
      nameEdited.value = false
    }
    prompt.value = ''
    if (previewing.value) await preview()
  } catch (e: any) {
    if (e?.code !== 'ERR_CANCELED') error.value = apiErrorMessage(e, 'Generation failed')
  } finally {
    busy.value = ''
    abort = null
  }
}

function cancelGenerate() { abort?.abort() }

function startOver() {
  result.value = null
  history.value = []
  prompt.value = ''
  name.value = ''
  nameEdited.value = false
  error.value = ''
  notice.value = ''
}

async function preview() {
  if (!details.value) return
  if (!selectedZones.value.length) { error.value = 'Select at least one zone to preview on'; return }
  error.value = ''
  busy.value = 'preview'
  try {
    await runPattern(props.folder, PREVIEW_NAME, 1, selectedZones.value, details.value)
    previewing.value = true
    previewZones.value = selectedZones.value.slice()
    window.dispatchEvent(new Event('jellyfish:state-changed'))
  } catch (e) {
    error.value = apiErrorMessage(e, 'Preview failed')
  } finally {
    if (busy.value === 'preview') busy.value = ''
  }
}

/** Turns the preview off; the server then hands the zones back to whatever schedule is active. */
async function stopPreview() {
  if (!previewing.value) return
  error.value = ''
  busy.value = 'stop'
  try {
    await runPattern(props.folder, PREVIEW_NAME, 0, previewZones.value, null)
    previewing.value = false
    previewZones.value = []
    window.dispatchEvent(new Event('jellyfish:state-changed'))
  } catch (e) {
    error.value = apiErrorMessage(e, 'Could not stop the preview')
  } finally {
    busy.value = ''
  }
}

async function save() {
  if (!details.value || nameProblem.value) return
  error.value = ''
  busy.value = 'save'
  const n = name.value.trim()
  try {
    await createPattern(props.folder, { name: n, isReadOnly: false })
    await upsertPatternDetails(props.folder, n, details.value)
    notice.value = `Saved "${n}" to ${props.folder}`
    emit('saved', n)
  } catch (e) {
    error.value = apiErrorMessage(e, 'Save failed')
  } finally {
    busy.value = ''
  }
}

async function close() {
  abort?.abort()
  if (previewing.value) await stopPreview()
  emit('close')
}

function onPromptKey(e: KeyboardEvent) {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); generate() }
}

// --- Strip simulation, following the controller's measured behaviour (2026-11-18):
//   No Color Transform: 1 light per color (width ignored) · Repeat: hard blocks of width
//   Fade: width lights per color, dimming down and up where colors change (no in-between colors)
//   Progression: width lights per color, through the hues in between (green -> orange -> red)
//   Fill with Black: 1 light, then width unlit
// Motion and the exact blend curve aren't simulated. ---

type Rgb = [number, number, number]

function toRgb(flat: number[]): Rgb[] {
  const out: Rgb[] = []
  for (let i = 0; i + 2 < flat.length; i += 3) out.push([flat[i], flat[i + 1], flat[i + 2]])
  return out
}

function css([r, g, b]: Rgb, scale = 1) {
  const s = (v: number) => Math.round(Math.max(0, Math.min(255, v * scale)))
  return `rgb(${s(r)}, ${s(g)}, ${s(b)})`
}

function scaleRgb([r, g, b]: Rgb, k: number): Rgb {
  return [r * k, g * k, b * k]
}

/** Fade: dims down and comes back up as the next color; a color next to itself stays even. */
function dipFade(a: Rgb, b: Rgb, t: number): Rgb {
  if (a[0] === b[0] && a[1] === b[1] && a[2] === b[2]) return a
  return t < 0.5 ? scaleRgb(a, 1 - 2 * t) : scaleRgb(b, 2 * t - 1)
}

/** Interpolate around the color wheel (shortest way), keeping saturation and brightness: green -> yellow -> red. */
function mixHue(a: Rgb, b: Rgb, t: number): Rgb {
  const [h1, s1, v1] = toHsv(a)
  const [h2, s2, v2] = toHsv(b)
  let dh = h2 - h1
  if (dh > 180) dh -= 360
  if (dh < -180) dh += 360
  return fromHsv((h1 + dh * t + 360) % 360, s1 + (s2 - s1) * t, v1 + (v2 - v1) * t)
}

function toHsv([r, g, b]: Rgb): [number, number, number] {
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min
  let h = 0
  if (d) {
    if (max === r) h = 60 * (((g - b) / d) % 6)
    else if (max === g) h = 60 * ((b - r) / d + 2)
    else h = 60 * ((r - g) / d + 4)
  }
  return [(h + 360) % 360, max ? d / max : 0, max / 255]
}

function fromHsv(h: number, s: number, v: number): Rgb {
  const c = v * s, x = c * (1 - Math.abs(((h / 60) % 2) - 1)), m = v - c
  const [r, g, b] = h < 60 ? [c, x, 0] : h < 120 ? [x, c, 0] : h < 180 ? [0, c, x] : h < 240 ? [0, x, c] : h < 300 ? [x, 0, c] : [c, 0, x]
  return [(r + m) * 255, (g + m) * 255, (b + m) * 255]
}

function simulateStrip(d: NonNullable<typeof details.value>, count: number): string[] {
  const colors = toRgb(d.colors)
  if (!colors.length) return []
  // Brightness shown at a floor of 35% so dim patterns stay visible on screen
  const scale = 0.35 + 0.65 * Math.max(0, Math.min(100, d.runData?.brightness ?? 100)) / 100
  const effect = (d.effectBetweenPixels || '').toLowerCase()
  const space = Math.max(0, d.spaceBetweenPixels || 0)
  const pixels: string[] = []

  if (effect === 'fill with black') {
    // each color: 1 lit light followed by `space` unlit lights
    for (let i = 0; pixels.length < count; i++) {
      pixels.push(css(colors[i % colors.length], scale))
      for (let g = 0; g < space && pixels.length < count; g++) pixels.push(css([0, 0, 0]))
    }
    return pixels
  }

  const width = effect === 'no color transform' ? 1 : Math.max(1, space)
  for (let i = 0; i < count; i++) {
    const block = Math.floor(i / width)
    const c = colors[block % colors.length]
    const next = colors[(block + 1) % colors.length]
    const t = (i % width) / width
    const px = effect === 'fade' ? dipFade(c, next, t) : effect === 'progression' ? mixHue(c, next, t) : c
    pixels.push(css(px, scale))
  }
  return pixels
}
</script>

<template>
  <div class="modal-backdrop" @click.self="close">
    <div class="modal assistant">
      <div class="head">
        <h3>✨ Pattern assistant</h3>
        <button class="icon" title="Close" @click="close">✕</button>
      </div>
      <p class="sub">Describe a look and the assistant designs a pattern. Preview it on the lights, refine it, then save it to <strong>{{ folder }}</strong>.</p>

      <div v-if="statusError" class="error">{{ statusError }}</div>
      <div v-else-if="!status" class="muted">Checking the assistant…</div>
      <div v-else-if="!status.ready" class="warn">
        {{ status.message || 'The pattern assistant is not ready.' }}
      </div>

      <!-- Prompt -->
      <div class="section">
        <label for="pa-prompt">{{ result ? 'Change it' : 'Describe the pattern' }}</label>
        <textarea
          id="pa-prompt"
          v-model="prompt"
          rows="3"
          maxlength="500"
          :disabled="!ready || busy === 'generate'"
          :placeholder="result ? 'e.g. slower, and add some white' : 'e.g. slow red and white candy cane chase'"
          @keydown="onPromptKey"
        ></textarea>
        <div class="actions">
          <button :disabled="!canGenerate" @click="generate">{{ result ? 'Refine' : 'Generate' }}</button>
          <button v-if="busy === 'generate'" @click="cancelGenerate">Cancel</button>
          <button v-if="result && busy !== 'generate'" :disabled="!!busy" @click="startOver">Start over</button>
          <span v-if="busy === 'generate'" class="muted">Thinking… this can take up to a minute.</span>
          <span v-else-if="ready" class="muted hint">Ctrl+Enter · {{ status?.model }}</span>
        </div>
        <div v-if="history.length" class="history">
          <span class="muted">So far:</span>
          <span v-for="(h, i) in history" :key="i" class="chip small">{{ h }}</span>
        </div>
      </div>

      <div v-if="error" class="error">{{ error }}</div>
      <div v-if="notice" class="ok">{{ notice }}</div>

      <!-- Result -->
      <div v-if="result && details" class="section result">
        <div class="name-row">
          <label for="pa-name">Name</label>
          <input id="pa-name" v-model.trim="name" maxlength="40" @input="nameEdited = true" />
        </div>
        <div v-if="nameProblem" class="field-error">{{ nameProblem }}</div>
        <p class="explanation">{{ result.explanation }}</p>

        <div class="strip" title="Approximate: use Preview on lights to see the real thing">
          <span v-for="(c, i) in strip" :key="i" class="px" :style="{ background: c }"></span>
        </div>
        <div class="swatches">
          <span v-for="(c, i) in swatches" :key="i" class="swatch" :style="{ background: css(c) }" :title="`rgb(${c.join(', ')})`"></span>
        </div>

        <dl class="facts">
          <div><dt>Animation</dt><dd>{{ result.animation || details.type }}</dd></div>
          <div><dt>Type</dt><dd>{{ details.type }}</dd></div>
          <div><dt>Between colors</dt><dd>{{ details.effectBetweenPixels }}</dd></div>
          <div><dt>{{ widthLabel }}</dt><dd>{{ widthValue }}</dd></div>
          <div v-if="details.type === 'Multi-Paint'"><dt>Section size</dt><dd>{{ details.numOfLeds }} lights</dd></div>
          <div v-if="details.fireworkSettings"><dt>Firework</dt><dd>spawn rate {{ details.fireworkSettings.spawnRate }}, {{ details.fireworkSettings.colorPerLed }} colors per light, speed {{ details.fireworkSettings.fireworkSpeed }}</dd></div>
          <div><dt>Speed</dt><dd>{{ details.runData.speed }} <span class="muted">(1 = fastest)</span></dd></div>
          <div><dt>Brightness</dt><dd>{{ details.runData.brightness }}%</dd></div>
          <div><dt>Direction</dt><dd>{{ details.direction }}</dd></div>
        </dl>

        <button class="link" @click="showJson = !showJson">{{ showJson ? 'Hide' : 'Show' }} JSON</button>
        <pre v-if="showJson" class="json">{{ JSON.stringify(details, null, 2) }}</pre>

        <!-- Preview -->
        <div class="preview">
          <label>Preview on</label>
          <div class="chips">
            <label v-for="z in zones" :key="z" class="chip"><input type="checkbox" :value="z" v-model="selectedZones" /> {{ z }}</label>
            <span v-if="!zones.length" class="muted">No zones loaded</span>
          </div>
        </div>

        <div class="actions">
          <button :disabled="!!busy || !selectedZones.length" @click="preview">{{ previewing ? 'Update preview' : 'Preview on lights' }}</button>
          <button v-if="previewing" :disabled="!!busy" @click="stopPreview">Stop preview</button>
          <button class="primary" :disabled="!!busy || !!nameProblem" @click="save">Save to {{ folder }}</button>
        </div>
        <p v-if="previewing" class="muted small-text">Previewing on {{ previewZones.join(', ') }}. Stopping hands the zones back to the active schedule.</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { --pa-bg: #1e1e1e; --pa-fg: rgba(255,255,255,.87); --pa-border: #555; --pa-line: #444; --pa-code: #111; --pa-link: #8ab4ff; --pa-error: #f06a6a; --pa-warn: #e0b050; --pa-ok: #5c9;
  background: var(--pa-bg); color: var(--pa-fg); border: 1px solid var(--pa-border); border-radius: 8px; padding: 1rem; width: calc(100% - 2rem); box-shadow: 0 10px 30px rgba(0,0,0,.6); }
.assistant { max-width: 760px; max-height: calc(100vh - 2rem); overflow-y: auto; display: grid; gap: .75rem; }
.head { display: flex; align-items: center; justify-content: space-between; }
.head h3 { margin: 0; }
.icon { width: 2rem; height: 2rem; padding: 0; display: inline-flex; align-items: center; justify-content: center; }
.sub { margin: 0; opacity: .8; }
.section { display: grid; gap: .4rem; }
.result { border-top: 1px solid var(--pa-line); padding-top: .75rem; }
label { font-weight: 600; }
textarea, input { width: 100%; box-sizing: border-box; background: transparent; color: inherit; border: 1px solid var(--pa-border); border-radius: 6px; padding: .45rem .55rem; font: inherit; }
textarea { resize: vertical; }
.actions { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
.primary { background: #2f6f4f; border-color: #3a8; color: #fff; }
.link { background: none; border: none; padding: 0; color: var(--pa-link); cursor: pointer; justify-self: start; }
.muted { opacity: .7; }
.hint { font-size: .85rem; }
.small-text { font-size: .85rem; margin: 0; }
.error { color: var(--pa-error); }
.field-error { color: var(--pa-error); font-size: .85rem; }
.warn { color: var(--pa-warn); }
.ok { color: var(--pa-ok); }
.history { display: flex; flex-wrap: wrap; gap: .35rem; align-items: center; }
.chips { display: flex; flex-wrap: wrap; gap: .35rem; }
.chip { display: inline-flex; align-items: center; gap: .35rem; border: 1px solid var(--pa-border); border-radius: 999px; padding: .15rem .55rem; font-weight: normal; }
.chip.small { font-size: .8rem; }
.name-row { display: grid; grid-template-columns: 4rem 1fr; gap: .5rem; align-items: center; }
.explanation { margin: 0; }
.strip { display: flex; height: 18px; border-radius: 9px; overflow: hidden; background: #000; border: 1px solid #333; }
.px { flex: 1 1 0; }
.swatches { display: flex; flex-wrap: wrap; gap: .35rem; }
.swatch { width: 1.5rem; height: 1.5rem; border-radius: 4px; border: 1px solid var(--pa-border); }
.facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: .35rem .75rem; margin: 0; }
.facts div { display: flex; gap: .4rem; }
.facts dt { opacity: .7; }
.facts dd { margin: 0; font-weight: 600; }
.json { margin: 0; max-height: 220px; overflow: auto; background: var(--pa-code); border: 1px solid var(--pa-line); border-radius: 6px; padding: .5rem; font-size: .8rem; }
.preview { display: grid; gap: .3rem; }
/* Follows the app theme (style.css switches text to dark in light mode) */
@media (prefers-color-scheme: light) {
  .modal { --pa-bg: #ffffff; --pa-fg: #213547; --pa-border: #c8ccd2; --pa-line: #e2e5e9; --pa-code: #f5f6f8; --pa-link: #3557c9; --pa-error: #c62828; --pa-warn: #9a6400; --pa-ok: #1d7a4d; }
  .modal-backdrop { background: rgba(0,0,0,.35); }
}
@media (max-width: 560px) {
  .name-row { grid-template-columns: 1fr; }
}
</style>
