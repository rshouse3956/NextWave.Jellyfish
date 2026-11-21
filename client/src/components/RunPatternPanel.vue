<script setup lang="ts">
import { onMounted, ref, computed, watch } from 'vue'
import { runPatternWithSyncCheck } from '../utils/runPatternHelper'
import { getFolders as getFoldersApp, type FolderDto } from '../api/folders'
import { getPatternsByFolder, getPattern } from '../api/patterns'
import { getZones as getZonesApp } from '../api/zones'

// Minimal list item for UI selection sourced from app routes
type PatternListItem = { folderName: string; name: string }

async function getAllPatternsFromApp(): Promise<PatternListItem[]> {
  try {
    const folders = await getFoldersApp()
    const perFolder = await Promise.allSettled(
      folders.map(async f => {
        const pats = await getPatternsByFolder(f.name)
        return pats.map(p => ({ folderName: p.folderName, name: p.name }))
      })
    )
    const all: PatternListItem[] = []
    for (const r of perFolder) {
      if (r.status === 'fulfilled') all.push(...r.value)
    }
    return all
  } catch { return [] }
}

const loading = ref(false)
const error = ref('')
const folders = ref<FolderDto[]>([])
const patterns = ref<PatternListItem[]>([])
const zones = ref<string[]>([])
const selectedZones = ref<string[]>([])

const selectedFolder = ref<string>('')
const selectedPattern = ref<string>('')
const allChecked = computed({
  get: () => zones.value.length > 0 && selectedZones.value.length === zones.value.length,
  set: (val: boolean) => { if (val) selectedZones.value = [...zones.value] }
})
const noneChecked = computed({
  get: () => selectedZones.value.length === 0,
  set: (val: boolean) => { if (val) selectedZones.value = [] }
})


const requiresDetails = ref(false)
const hasDetails = ref(false)

const filteredPatterns = computed(() => {
  if (!selectedFolder.value) return patterns.value
  return patterns.value.filter(p => p.folderName === selectedFolder.value)
})

watch([selectedFolder, selectedPattern], async () => {
  requiresDetails.value = false
  hasDetails.value = false
  const pat = filteredPatterns.value.find(p => p.name === selectedPattern.value)
  if (!pat) return
  try {
    const full = await getPattern(pat.folderName, pat.name)
    requiresDetails.value = !!full?.syncRequired
    hasDetails.value = !!full?.details
  } catch {}
})

onMounted(async () => {
  try {
    const [f, p, z] = await Promise.all([
      getFoldersApp(),
      getAllPatternsFromApp(),
      getZonesApp(),
    ])
    folders.value = f
    patterns.value = p
    zones.value = z.map(zz => zz.name)
    selectedZones.value = [...zones.value]
    if (folders.value.length) selectedFolder.value = folders.value[0].name
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to load controller data'
  }
})

async function trigger(state: 0|1) {
  if (!selectedPattern.value) { error.value = 'Pick a pattern'; return }
  if (!selectedZones.value.length) { error.value = 'Pick at least one zone'; return }
  loading.value = true
  error.value = ''
  try {
    const pat = filteredPatterns.value.find(p => p.name === selectedPattern.value)
    if (!pat) { error.value = 'Pattern not found'; return }
    await runPatternWithSyncCheck(pat.folderName, pat.name, state, selectedZones.value)
    // Notify other components (e.g., ActiveScheduleCard) to refresh immediately
    window.dispatchEvent(new Event('jellyfish:state-changed'))
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to run pattern'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="panel">
    <h2 class="section-title">Run Pattern</h2>
    <div v-if="error" class="error">{{ error }}</div>

    <div class="row">
      <label>Folder</label>
      <select v-model="selectedFolder">
        <option v-for="f in folders" :key="f.name" :value="f.name">{{ f.name }}</option>
      </select>
    </div>

    <div class="row">
      <label>Pattern</label>
      <select v-model="selectedPattern">
        <option value="" disabled>-- pick pattern --</option>
        <option v-for="p in filteredPatterns" :key="p.folderName + '/' + p.name" :value="p.name">{{ p.name }}</option>
      </select>
    </div>

    <div class="row">
      <label>Zones</label>
      <div class="zones">
        <div class="zones-controls">
          <label class="zone-item">
            <input type="checkbox" v-model="allChecked" />
            <span>All</span>
          </label>
          <label class="zone-item">
            <input type="checkbox" v-model="noneChecked" />
            <span>None</span>
          </label>
        </div>
        <div class="zones-grid">
          <label v-for="zn in zones" :key="zn" class="zone-item">
            <input type="checkbox" :value="zn" v-model="selectedZones" />
            <span>{{ zn }}</span>
          </label>
        </div>
      </div>
    </div>

    <div class="row" v-if="requiresDetails && !hasDetails">
      <div class="warn">This pattern requires details to run, but none are stored. Add details or sync from the controller.</div>
    </div>
    <div class="row actions">
      <button :disabled="loading || !selectedPattern || !selectedZones.length || (requiresDetails && !hasDetails)" @click="trigger(1)">Turn On</button>
      <button :disabled="loading || !selectedPattern || !selectedZones.length || (requiresDetails && !hasDetails)" @click="trigger(0)">Turn Off</button>
    </div>
  </div>
</template>

<style scoped>
.panel { padding: 1rem; border: 1px solid #444; border-radius: 8px; }
.section-title { margin: 0 0 .75rem; padding-bottom: .25rem; border-bottom: 1px solid #444; font-size: 1.25rem; text-align: left; }
.row { display: flex; gap: .5rem; align-items: center; margin: .5rem 0; }
.row > label { width: 80px; font-weight: 600; }
select { padding: .3rem .5rem; }
.actions { gap: .75rem; }
.zones { display: flex; flex-direction: column; gap: .5rem; }
.zones-controls { display: flex; gap: .75rem; flex-wrap: wrap; }
.zones-grid { display: grid; grid-template-columns: repeat(3, minmax(140px, 1fr)); gap: .4rem .75rem; align-items: start; }
.zone-item { display: inline-flex; align-items: center; gap: .2rem; }
button { padding: .4rem .9rem; }
.error { color: #e33; margin-bottom: .5rem; }
</style>

