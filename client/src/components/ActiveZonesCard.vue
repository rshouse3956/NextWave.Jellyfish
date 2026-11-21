<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { getActiveZones, stopActiveZone, type ActiveZoneDto } from '../api/activeZones'

const loading = ref(false)
const error = ref('')
const zones = ref<ActiveZoneDto[]>([])

async function load() {
  loading.value = true
  error.value = ''
  try {
    zones.value = await getActiveZones()
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to load active zones'
  } finally {
    loading.value = false
  }
}

async function stop(zn: string) {
  try {
    await stopActiveZone(zn)
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to stop zone'
  }
}

function fmt(dt: string | null | undefined) {
  if (!dt) return '—'
  try { return new Date(dt).toLocaleString() } catch { return dt }
}

const handleImmediate = () => { load() }
let es: EventSource | null = null

onMounted(() => {
  load()
  window.addEventListener('jellyfish:state-changed', handleImmediate)
  try {
    es = new EventSource('/api/events/state')
    es.onmessage = () => handleImmediate()
  } catch {}
})

onUnmounted(() => {
  window.removeEventListener('jellyfish:state-changed', handleImmediate)
  if (es) { try { es.close() } catch {} es = null }
})
</script>

<template>
  <div class="panel">
    <h2 class="section-title">
      <span>Active Zones</span>
      <button class="icon" @click="load" :disabled="loading" title="Refresh">🔄</button>
    </h2>
    <div v-if="loading">Loading…</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else>
      <div v-if="!zones.length" class="empty"><em>No zones are active.</em></div>
      <div v-else class="grid">
        <div v-for="z in zones" :key="z.zoneName" class="zone-card">
          <div class="zone-title">{{ z.patternName }}</div>
          <div class="zone-fields">
            <div class="row"><span class="label">Zone</span><span class="value">{{ z.zoneName }}</span></div>
            <div class="row"><span class="label">Source</span><span class="value">{{ z.sourceType }} {{ z.sourceId ? '(' + z.sourceId + ')' : '' }}</span></div>
            <div class="row"><span class="label">Started</span><span class="value">{{ fmt(z.activatedAtUtc) }}</span></div>
            <div class="row actions"><button @click="stop(z.zoneName)">Turn Off</button></div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.panel { padding: 1rem; border: 1px solid #444; border-radius: 8px; }
.section-title { margin: 0 0 .5rem; padding-bottom: .25rem; border-bottom: 1px solid #444; font-size: 1.25rem; display:flex; align-items:center; justify-content:space-between; gap:.5rem; }
.section-title .refresh { background: transparent; border: 1px solid #444; padding: .1rem .4rem; border-radius: 6px; line-height: 1; }
.section-title .refresh[disabled] { opacity: .6; cursor: default; }
.grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
@media (max-width: 800px) { .grid { grid-template-columns: 1fr; } }
.zone-card { padding: .75rem; border: 1px solid #444; border-radius: 6px; }
.zone-fields { display: grid; grid-template-columns: 1fr; gap: .25rem; }
.row { display: flex; align-items: baseline; gap: .5rem; margin: .25rem 0; justify-content: flex-start; }
.row.actions { justify-content: flex-start; margin-top: .25rem; }
.error { color: #e33; }
.empty { color: #bbb; }
.icon { width: 2rem; height: 2rem; display: inline-flex; align-items: center; justify-content: center; font-size: 1rem; }
</style>

