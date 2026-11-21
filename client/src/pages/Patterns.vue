<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { PatternDto, PatternDetailsDto } from '../api/patterns'
import { getPatternsByFolder, createPattern, updatePattern, deletePattern, getPattern, upsertPatternDetails, deletePatternDetails } from '../api/patterns'
import { getFolders, type FolderDto } from '../api/folders'
import { runPatternWithSyncCheck } from '../utils/runPatternHelper'
import { getZones } from '../api/zones'
import { getPatternAssistantStatus } from '../api/patternAssistant'
import PatternAssistantPanel from '../components/PatternAssistantPanel.vue'


const route = useRoute()
const router = useRouter()
const loading = ref(false)
const error = ref('')
const folder = ref<string>((route.query.folder as string) || '')
const patterns = ref<PatternDto[]>([])
const folders = ref<FolderDto[]>([])
// Tracks whether a pattern has details: { [name]: true }
const detailsPresence = ref<Record<string, boolean>>({})

async function loadFolders() {
  try {
    folders.value = await getFolders()
    if (!folder.value && folders.value.length) {
      folder.value = folders.value[0].name
      router.replace({ query: { ...route.query, folder: folder.value } })
    }
  } catch {}
}
const zones = ref<string[]>([])
async function loadZones() {
  if (!zones.value.length) {
    try { const z = await getZones(); zones.value = z.map(x => x.name) } catch {}
  }
}

// Details modal state and actions
const detailsOpen = ref(false)
const detailsTargetName = ref<string>('')
const detailsJson = ref<string>('')
const detailsError = ref<string>('')

async function openDetails(p: PatternDto) {
  detailsError.value = ''
  detailsTargetName.value = p.name
  try {
    const full = await getPattern(p.folderName, p.name)
    const d = full?.details ?? null
    detailsPresence.value[p.name] = !!d
    detailsJson.value = d ? JSON.stringify(d, null, 2) : JSON.stringify({
      colors: [],
      spaceBetweenPixels: 0,
      effectBetweenPixels: 'No Color Transform',
      colorPos: [],
      soffitZone: null,
      cursor: null,
      type: '',
      skip: 0,
      numOfLeds: 0,
      runData: { speed: 0, brightness: 100, effect: 'No Effect', effectValue: 0, rgbAdj: [100,100,100] },
      direction: 'Left'
    }, null, 2)
  } catch (e: any) {
    detailsError.value = e?.message ?? 'Failed to load details'
    detailsJson.value = ''
  } finally {
    detailsOpen.value = true
  }
}

function closeDetails() { detailsOpen.value = false }

async function saveDetails() {
  try {
    detailsError.value = ''
    if (!folder.value || !detailsTargetName.value) throw new Error('Missing folder or name')
    const parsed = JSON.parse(detailsJson.value) as PatternDetailsDto
    await upsertPatternDetails(folder.value, detailsTargetName.value, parsed)
    detailsOpen.value = false
    await load()
  } catch (e: any) {
    detailsError.value = e?.message ?? 'Failed to save details'
  }
}

async function removeDetails() {
  try {
    detailsError.value = ''
    if (!folder.value || !detailsTargetName.value) throw new Error('Missing folder or name')
    await deletePatternDetails(folder.value, detailsTargetName.value)
    detailsOpen.value = false
    await load()
  } catch (e: any) {
    detailsError.value = e?.message ?? 'Failed to delete details'
  }
}

// Run Now modal state and actions
const runOpen = ref(false)
const runTargetName = ref<string>('')
const selectedZones = ref<string[]>([])
const sendDetails = ref<boolean>(false)
const runDetailsJson = ref<string>('')
const runError = ref<string>('')
const runSyncRequired = ref(false)
const runHasDetails = ref(false)

async function openRun(p: PatternDto) {
  runError.value = ''
  runTargetName.value = p.name
  await loadZones()
  selectedZones.value = zones.value.slice()
  try {
    const full = await getPattern(p.folderName, p.name)
    runSyncRequired.value = !!full?.syncRequired
    runHasDetails.value = !!full?.details
    detailsPresence.value[p.name] = runHasDetails.value
    if (full?.details) {
      sendDetails.value = true
      runDetailsJson.value = JSON.stringify(full.details, null, 2)
    } else {
      sendDetails.value = false
      runDetailsJson.value = ''
    }
  } catch {
    runSyncRequired.value = false
    runHasDetails.value = false
    sendDetails.value = false
    runDetailsJson.value = ''
  }
  runOpen.value = true
}

function cancelRun() { runOpen.value = false }

async function confirmRun() {
  try {
    runError.value = ''
    if (!folder.value || !runTargetName.value) throw new Error('Missing folder or name')
    if (!selectedZones.value.length) throw new Error('Select at least one zone')
    let prefer: any | undefined = undefined
    if (sendDetails.value && runDetailsJson.value) {
      try { prefer = JSON.parse(runDetailsJson.value) } catch { prefer = undefined }
    }
    await runPatternWithSyncCheck(folder.value, runTargetName.value, 1, selectedZones.value, prefer)
    runOpen.value = false
    window.dispatchEvent(new Event('jellyfish:state-changed'))
  } catch (e: any) {
    runError.value = e?.message ?? 'Failed to run pattern'
  }
}

function onFolderChange() {
  router.replace({ query: { ...route.query, folder: folder.value || undefined } })
}

const adding = ref(false)
const form = ref<{ name: string; isReadOnly: boolean }>({ name: '', isReadOnly: false })

const editing = ref(false)
const editName = ref('')
const editForm = ref<{ isReadOnly: boolean }>({ isReadOnly: false })

async function refreshDetailsPresence() {
  detailsPresence.value = {}
  if (!folder.value || !patterns.value.length) return
  try {
    await Promise.allSettled(patterns.value.map(async (p) => {
      const full = await getPattern(p.folderName, p.name)
      detailsPresence.value[p.name] = !!full?.details
    }))
  } catch {}
}

async function load() {
  if (!folder.value) { patterns.value = []; detailsPresence.value = {}; return }
  loading.value = true
  error.value = ''
  try {
    patterns.value = await getPatternsByFolder(folder.value)
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to load patterns'
  } finally {
    loading.value = false
    // Fire-and-forget; badges will populate as results arrive
    refreshDetailsPresence()
  }
}

async function submitNew() {
  try {
    loading.value = true
    error.value = ''
    if (!folder.value) throw new Error('Select a folder first')
    if (!form.value.name) throw new Error('Name is required')
    await createPattern(folder.value, { name: form.value.name, isReadOnly: form.value.isReadOnly })
    adding.value = false
    form.value = { name: '', isReadOnly: false }
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to create pattern'
  } finally {
    loading.value = false
  }
}

function startEdit(p: PatternDto) {
  editName.value = p.name
  editForm.value = { isReadOnly: !!p.isReadOnly }
  editing.value = true
}

async function submitEdit() {
  try {

    loading.value = true
    error.value = ''
    if (!folder.value || !editName.value) throw new Error('Missing folder or name')
    await updatePattern(folder.value, editName.value, { isReadOnly: editForm.value.isReadOnly })
    editing.value = false
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to update pattern'
  } finally {
    loading.value = false
  }
}

const confirmOpen = ref(false)
const deleteTarget = ref<PatternDto | null>(null)
function askDelete(p: PatternDto) { deleteTarget.value = p; confirmOpen.value = true }
function cancelDelete() { confirmOpen.value = false; deleteTarget.value = null }
async function confirmDelete() {
  if (!deleteTarget.value) return
  try {
    loading.value = true
    error.value = ''
    await deletePattern(deleteTarget.value.folderName, deleteTarget.value.name)
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to delete pattern'
  } finally {
    loading.value = false
    confirmOpen.value = false
    deleteTarget.value = null
  }
}

watch(() => route.query.folder, (v) => {
  folder.value = (v as string) || ''
  load()
})

// Pattern assistant: the button only shows when the server has the assistant enabled
const assistantEnabled = ref(false)
const assistantOpen = ref(false)
async function loadAssistantStatus() {
  try { assistantEnabled.value = (await getPatternAssistantStatus()).enabled } catch { assistantEnabled.value = false }
}
async function onAssistantSaved() { await load() }

onMounted(async () => { loadAssistantStatus(); await loadFolders(); await load() })
</script>

<template>
  <div class="page">
    <div class="header">
      <h1>Patterns</h1>
      <div class="actions" style="padding: 1rem; display:flex; gap:.5rem; align-items:center">
        <select v-model="folder" @change="onFolderChange">
          <option value="">Select folder…</option>
          <option v-for="f in folders" :key="f.name" :value="f.name">{{ f.name }}</option>
        </select>
        <button class="icon" :disabled="loading" @click="load" title="Refresh">🔄</button>
        <button class="icon" :disabled="loading || !folder" @click="adding = !adding" :title="adding ? 'Close' : 'Add new'">➕</button>
        <button v-if="assistantEnabled" class="icon" :disabled="loading || !folder" @click="assistantOpen = true" title="Create with the pattern assistant">✨</button>
      </div>
    </div>

    <div v-if="!folder">
      <p>Select a folder to view patterns.</p>
    </div>

    <div v-else>
      <p class="sub">Folder: <strong>{{ folder }}</strong></p>

      <div v-if="adding" class="add">
        <div class="row"><label>Name</label><input v-model.trim="form.name" placeholder="My Pattern" /></div>
        <div class="row"><label class="cb"><input type="checkbox" v-model="form.isReadOnly" /> Read-only</label></div>
        <div class="add-actions">
          <button :disabled="loading" @click="submitNew">Create</button>
          <button :disabled="loading" @click="adding = false">Cancel</button>
        </div>
      </div>

      <div v-if="editing" class="add">
        <div class="row"><label>Name</label><input :value="editName" disabled /></div>
        <div class="row"><label class="cb"><input type="checkbox" v-model="editForm.isReadOnly" /> Read-only</label></div>
        <div class="add-actions">
          <button :disabled="loading" @click="submitEdit">Save</button>
          <button :disabled="loading" @click="editing = false">Cancel</button>
        </div>
      </div>

      <div v-if="loading">Loading…</div>
      <div v-else-if="error" class="error">{{ error }}</div>

      <div v-else class="cards">
        <div v-if="!patterns.length"><em>No patterns found.</em></div>
        <div v-else class="grid">
          <div v-for="p in patterns" :key="p.name" class="card">
            <div class="card-header">
              <div class="title">
                {{ p.name }}
                <span v-if="detailsPresence[p.name]" class="badge badge-ok" title="Pattern has details">Details</span>
              </div>
              <div class="header-actions">
                <button class="icon" @click="openRun(p)" title="Run now">▶️</button>
                <button class="icon" @click="openDetails(p)" title="Details">🧩</button>
                <button class="icon" @click="startEdit(p)" title="Edit">✏️</button>
                <button class="icon" @click="askDelete(p)" :disabled="p.isReadOnly" title="Delete">🗑️</button>
              </div>
            </div>
            <div class="rows">
              <div class="row"><strong>Read-only:</strong> <span>{{ p.isReadOnly ? 'Yes' : 'No' }}</span></div>
              <div class="row"><strong>Created:</strong> <span>{{ new Date(p.createDate).toLocaleString() }}</span></div>
              <div class="row"><strong>Modified:</strong> <span>{{ new Date(p.lastModifiedDate).toLocaleString() }}</span></div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="confirmOpen" class="modal-backdrop" @click.self="cancelDelete">
        <div class="modal">
          <h3 style="margin-top:0">Delete pattern</h3>
          <p>Are you sure you want to delete "{{ deleteTarget ? deleteTarget.name : '' }}"? This cannot be undone.</p>
          <div class="add-actions">
            <button :disabled="loading" @click="confirmDelete" style="background:#a00;color:#fff;border-color:#a00">Delete</button>
            <button :disabled="loading" @click="cancelDelete">Cancel</button>
          </div>
        </div>
      </div>

      <div v-if="detailsOpen" class="modal-backdrop" @click.self="closeDetails">
        <div class="modal" style="max-width: 720px">
          <h3 style="margin-top:0">Pattern details: {{ detailsTargetName }}</h3>
          <div v-if="detailsError" class="error">{{ detailsError }}</div>
          <div class="row">
            <label>Details (JSON)</label>
            <div>
              <textarea v-model="detailsJson" rows="14" style="width:100%; background:transparent; color:inherit; border:1px solid #555; border-radius:6px; padding:.5rem; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;"></textarea>
            </div>
          </div>
          <div class="add-actions">
            <button :disabled="loading" @click="saveDetails">Save</button>
            <button :disabled="loading" @click="removeDetails" style="background:#a00;color:#fff;border-color:#a00">Delete details</button>
            <button :disabled="loading" @click="closeDetails">Close</button>
          </div>
        </div>
      </div>

      <PatternAssistantPanel
        v-if="assistantOpen"
        :folder="folder"
        :existing-names="patterns.map(p => p.name)"
        @close="assistantOpen = false"
        @saved="onAssistantSaved"
      />

      <div v-if="runOpen" class="modal-backdrop" @click.self="cancelRun">
        <div class="modal" style="max-width: 720px">
          <h3 style="margin-top:0">Run pattern: {{ runTargetName }}</h3>
          <div v-if="runError" class="error">{{ runError }}</div>
          <div class="row">
            <label>Zones</label>
            <div class="chips" style="margin-top:.25rem">
              <label v-for="z in zones" :key="z" class="chip"><input type="checkbox" :value="z" v-model="selectedZones" /> {{ z }}</label>
            </div>
          </div>
          <div class="row" style="margin-top:.5rem">
            <label>Send details</label>
            <input type="checkbox" v-model="sendDetails" />
          </div>
          <div class="row" v-if="sendDetails" style="margin-top:.25rem">
            <label>Details (JSON)</label>
            <div>
              <textarea v-model="runDetailsJson" rows="10" style="width:100%; background:transparent; color:inherit; border:1px solid #555; border-radius:6px; padding:.5rem; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;"></textarea>
            </div>
          </div>
          <div v-if="runSyncRequired && !runHasDetails" class="warn" style="margin-top:.5rem">
            This pattern requires details to run, but none are stored. Add details or sync from the controller.
          </div>
          <div class="add-actions" style="margin-top:.5rem">
            <button :disabled="loading || (runSyncRequired && !runHasDetails)" @click="confirmRun">Run</button>
            <button :disabled="loading" @click="cancelRun">Cancel</button>
          </div>
        </div>
      </div>

    </div>
  </div>
</template>

<style scoped>
.page { max-width: 1100px; margin: 0 auto; padding: 1rem; }
.header { display: flex; align-items: center; justify-content: space-between; margin-bottom: .5rem; }
.actions { display: flex; gap: .5rem; }
.actions .icon { width: 2rem; height: 2rem; display: inline-flex; align-items: center; justify-content: center; font-size: 1rem; }
.sub { opacity: .8; margin: .25rem 0 1rem; }
.add { border: 1px dashed #555; border-radius: 8px; padding: .75rem; margin-bottom: .75rem; display: grid; gap: .5rem; }
.add .row { display: grid; gap: .25rem; }
.add input { padding: .4rem .5rem; background: transparent; border: 1px solid #555; border-radius: 6px; color: inherit; }
.add-actions { display: flex; gap: .5rem; justify-content: center; }
.cb { font-weight: normal; }
.error { color: #e33; }
.grid { display: flex; flex-wrap: wrap; gap: 1rem; align-items: stretch; }
.grid > .card { flex: 0 0 320px; width: 320px; max-width: 100%; }
@media (max-width: 700px) { .grid > .card { flex: 1 1 100%; width: 100%; } }
.card { border: 1px solid #444; border-radius: 8px; padding: .75rem; display: flex; flex-direction: column; gap: .5rem; }
.card-header { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
.title { font-weight: 600; }
.header-actions { display: flex; gap: .25rem; }
.header-actions .icon { width: 1.75rem; height: 1.75rem; display: inline-flex; align-items: center; justify-content: center; font-size: .95rem; }
.rows { display: grid; gap: .25rem; }
.row { display: flex; gap: .5rem; }
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: #1e1e1e; color: inherit; border: 1px solid #555; border-radius: 8px; padding: 1rem; max-width: 420px; width: calc(100% - 2rem); box-shadow: 0 10px 30px rgba(0,0,0,.6); }

.modal .row { display: grid; gap: .25rem; grid-template-columns: 180px 1fr; align-items: center; }
.chips { display:flex; flex-wrap: wrap; gap:.35rem }
.chip { display:inline-flex; align-items:center; gap:.35rem; border: 1px solid #555; border-radius: 999px; padding: .15rem .5rem; }

.badge { display:inline-flex; align-items:center; font-size:.7rem; margin-left:.35rem; padding:.05rem .4rem; border-radius:999px; border:1px solid #3a6; color:#3a6; }
.badge-ok { background: rgba(51,170,102,.12); }

</style>
