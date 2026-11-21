<script setup lang="ts">
import { ref } from 'vue'
import { syncFolders, syncZones, syncPatterns, syncPatternDetail, syncAllPatternDetails, type SyncPatternDetailsSummary } from '../api/jellyfish'

const syncOpen = ref(false)

// Per-action state
const loadingFolders = ref(false), errFolders = ref(''), msgFolders = ref('')
const loadingZones = ref(false), errZones = ref(''), msgZones = ref('')
const loadingPatterns = ref(false), errPatterns = ref(''), msgPatterns = ref('')
const loadingDetail = ref(false), errDetail = ref(''), msgDetail = ref('')
const loadingAllDetails = ref(false), errAllDetails = ref(''), msgAllDetails = ref('')

const detailFolder = ref('')
const detailName = ref('')

async function runWith(loadingRef: any, errRef: any, msgRef: any, action: () => Promise<any>) {
  loadingRef.value = true
  errRef.value = ''
  msgRef.value = ''
  try {
    const res = await action()
    msgRef.value = typeof res === 'string' ? res : 'Sync completed.'
  } catch (e: any) {
    errRef.value = e?.response?.data?.message || e?.message || 'Operation failed'
  } finally {
    loadingRef.value = false
  }
}

function syncFoldersClick() { return runWith(loadingFolders, errFolders, msgFolders, () => syncFolders()) }
function syncZonesClick() { return runWith(loadingZones, errZones, msgZones, () => syncZones()) }
function syncPatternsClick() { return runWith(loadingPatterns, errPatterns, msgPatterns, () => syncPatterns()) }
function syncPatternDetailClick() {
  if (!detailFolder.value.trim() || !detailName.value.trim()) {
    errDetail.value = 'Folder and Pattern are required'
    return
  }
  return runWith(loadingDetail, errDetail, msgDetail, () => syncPatternDetail(detailFolder.value.trim(), detailName.value.trim()))
}

function describeDetailsSync(s: SyncPatternDetailsSummary): string {
  const lines = [`Synced details for ${s.synced} of ${s.total} patterns.`]
  if (s.noDetails.length) lines.push(`No details on the controller: ${s.noDetails.join(', ')}`)
  if (s.failed.length) {
    lines.push(`Failed (${s.failed.length}); run Sync ALL Details again to retry them:`)
    for (const f of s.failed) lines.push(`  ${f.folderName}/${f.name}: ${f.error}`)
  }
  return lines.join('\n')
}

function syncAllDetailsClick() {
  return runWith(loadingAllDetails, errAllDetails, msgAllDetails, async () => {
    const summary = await syncAllPatternDetails()
    const text = describeDetailsSync(summary)
    // Partial failures come back as a summary, not an HTTP error; show them in the error style.
    if (summary.failed.length) throw new Error(text)
    return text
  })
}
</script>

<template>
  <div class="page">
    <div class="header">
      <h1>Jellyfish</h1>
    </div>
    <p style="margin:.25rem 0 .25rem">
      Use this view to synchronize data to and from the Jellyfish lighting system and the Jellyfish Web application.
    </p>
    <ul class="intro-list">
      <li><strong>Sync Folders</strong>: Imports folders (names and read-only flags) from the controller into MongoDB. Creates missing and updates existing. No deletions.</li>
      <li><strong>Sync Zones</strong>: Imports zones from the controller into MongoDB. Creates/updates as needed. No deletions.</li>
      <li><strong>Sync Patterns</strong>: Imports the pattern list (by folder) into MongoDB. Upserts pattern records. Does not fetch pattern details.</li>
      <li><strong>Sync Pattern Detail</strong>: Imports the detailed data for a specific pattern and stores it in the separate PatternDetail collection.</li>
    </ul>

    <div v-if="syncOpen" class="modal-backdrop" @click.self="syncOpen = false">
      <div class="modal" style="max-width: 720px">
        <div style="display:flex; align-items:center; justify-content: space-between; gap:.5rem; margin-bottom:.5rem">
          <h3 style="margin:0">Synchronization</h3>
          <button class="icon" @click="syncOpen = false">✖️</button>
        </div>

        <h4 style="margin:.25rem 0 .5rem">Jellyfish → MongoDB</h4>

        <div class="row">
          <label>Sync folders</label>
          <div>
            <div class="time-row">
              <button :disabled="loadingFolders" @click="syncFoldersClick">Sync Folders</button>
              <span v-if="loadingFolders">Processing…</span>
            </div>
            <div v-if="errFolders" class="error">{{ errFolders }}</div>
            <div v-else-if="msgFolders" class="ok">{{ msgFolders }}</div>
          </div>
        </div>

        <div class="row">
          <label>Sync zones</label>
          <div>
            <div class="time-row">
              <button :disabled="loadingZones" @click="syncZonesClick">Sync Zones</button>
              <span v-if="loadingZones">Processing…</span>
            </div>
            <div v-if="errZones" class="error">{{ errZones }}</div>
            <div v-else-if="msgZones" class="ok">{{ msgZones }}</div>
          </div>
        </div>

        <div class="row">
          <label>Sync patterns</label>
          <div>
            <div class="time-row">
              <button :disabled="loadingPatterns" @click="syncPatternsClick">Sync Patterns</button>
              <span v-if="loadingPatterns">Processing…</span>
            </div>
            <div v-if="errPatterns" class="error">{{ errPatterns }}</div>
            <div v-else-if="msgPatterns" class="ok">{{ msgPatterns }}</div>
          </div>
        </div>

        <div class="row">
          <label>Sync pattern detail</label>
          <div>
            <div class="time-row">
              <input v-model.trim="detailFolder" placeholder="Folder name" />
              <input v-model.trim="detailName" placeholder="Pattern name" />
              <button :disabled="loadingDetail" @click="syncPatternDetailClick">Sync Detail</button>
              <span v-if="loadingDetail">Processing…</span>
            </div>
            <div class="hint">Provide a specific folder and pattern name.</div>
            <div v-if="errDetail" class="error">{{ errDetail }}</div>
            <div v-else-if="msgDetail" class="ok">{{ msgDetail }}</div>
          </div>
        </div>

        <div class="row">
          <label>Sync all pattern details</label>
          <div>
            <div class="time-row">
              <button :disabled="loadingAllDetails" @click="syncAllDetailsClick">Sync ALL Details</button>
              <span v-if="loadingAllDetails">Processing… This may take several minutes.</span>
            </div>
            <div class="hint">Fetches details for every pattern from the controller and upserts into MongoDB.</div>
            <div v-if="errAllDetails" class="error" style="white-space:pre-line">{{ errAllDetails }}</div>
            <div v-else-if="msgAllDetails" class="ok" style="white-space:pre-line">{{ msgAllDetails }}</div>
          </div>
        </div>

        <h4 style="margin:.75rem 0 .5rem">MongoDB → Jellyfish</h4>
        <div class="row">
          <label>Coming soon</label>
          <div>
            <div class="hint">Push local data back to the controller (folders, zones, patterns, details).</div>
          </div>
        </div>

        <div class="add-actions" style="margin-top:.75rem">
          <button @click="syncOpen = false">Close</button>
        </div>
      </div>
    </div>
    <div class="add-actions" style="margin-top:.75rem">
      <button class="icon" @click="syncOpen = true">Open Sync</button>
    </div>
  </div>
</template>

<style scoped>
.page { max-width: 1100px; margin: 0 auto; padding: 1rem; }
.header { display:flex; align-items:center; justify-content: space-between; margin-bottom:.5rem; }
.actions .icon { height: 2rem; padding: 0 .75rem; }
.intro-list { margin: 0 0 1rem 1.25rem; }
.time-row { display: flex; gap: .5rem; align-items: center; flex-wrap: wrap; }
.hint { font-size: .85rem; opacity: .8; margin-top: .25rem; }
.error { color: #e33; }
.ok { color: #3a6; }
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: #1e1e1e; color: inherit; border: 1px solid #555; border-radius: 8px; padding: 1rem; max-width: 720px; width: calc(100% - 2rem); box-shadow: 0 10px 30px rgba(0,0,0,.6); }
.add-actions { display: flex; gap: .5rem; justify-content: center; }
.modal input { padding: .4rem .5rem; background: transparent; border: 1px solid #555; border-radius: 6px; color: inherit; }
</style>
