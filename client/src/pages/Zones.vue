<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { ZoneDto, ZoneMapEntryDto, CreateZoneCommand, UpdateZoneCommand } from '../api/zones'
import { getZones, createZone, updateZone, deleteZone } from '../api/zones'
import { getSchedules, type ScheduleDto } from '../api/schedules'

const loading = ref(false)
const zones = ref<ZoneDto[]>([])

// Filter
const filter = ref('')
const filteredZones = computed(() => {
  const q = filter.value.trim().toLowerCase()
  if (!q) return zones.value
  return zones.value.filter(z =>
    z.name.toLowerCase().includes(q) ||
    z.ctlrName.toLowerCase().includes(q) ||
    (z.isReadOnly ? 'read-only' : 'read write').includes(q)
  )
})

// Add
const adding = ref(false)
const form = ref<CreateZoneCommand>({ name: '', ctlrName: '', isReadOnly: false, zoneMap: [] })
const createError = ref('')

function addRow() { form.value.zoneMap.push({ port: 0, startIdx: 0, endIdx: 0, rgbStartIdx: 0 }) }
function removeRow(i: number) { form.value.zoneMap.splice(i, 1) }

function startAdd() {
  adding.value = true
  editing.value = false
  form.value = { name: '', ctlrName: '', isReadOnly: false, zoneMap: [] }
  createError.value = ''
}

async function submitNew() {
  if (!form.value.name.trim()) { createError.value = 'Name is required'; return }
  if (!form.value.ctlrName.trim()) { createError.value = 'Controller name is required'; return }
  if (form.value.zoneMap.length === 0) { createError.value = 'At least one Zone Map entry is required'; return }
  loading.value = true
  try {
    // Normalize numbers
    form.value.zoneMap = form.value.zoneMap.map(e => ({
      port: Number(e.port) || 0,
      startIdx: Number(e.startIdx) || 0,
      endIdx: Number(e.endIdx) || 0,
      rgbStartIdx: Number(e.rgbStartIdx) || 0,
    }))
    await createZone({
      name: form.value.name.trim(),
      ctlrName: form.value.ctlrName.trim(),
      isReadOnly: form.value.isReadOnly,
      zoneMap: form.value.zoneMap,
    })
    await load()
    adding.value = false
  } catch (e: any) {
    createError.value = e?.response?.data?.message || 'Failed to create zone'
  } finally {
    loading.value = false
  }
}

// Edit
const editing = ref(false)
const editForm = ref<{ name: string; ctlrName: string; isReadOnly: boolean; zoneMap: ZoneMapEntryDto[] }>({ name: '', ctlrName: '', isReadOnly: false, zoneMap: [] })

function editAddRow() { editForm.value.zoneMap.push({ port: 0, startIdx: 0, endIdx: 0, rgbStartIdx: 0 }) }
function editRemoveRow(i: number) { editForm.value.zoneMap.splice(i, 1) }

function startEdit(z: ZoneDto) {
  editing.value = true
  adding.value = false
  editForm.value = { name: z.name, ctlrName: z.ctlrName, isReadOnly: z.isReadOnly, zoneMap: z.zoneMap.map(e => ({ ...e })) }
}

async function submitEdit() {
  loading.value = true
  try {
    const body: UpdateZoneCommand = {
      ctlrName: editForm.value.ctlrName.trim(),
      isReadOnly: editForm.value.isReadOnly,
      zoneMap: editForm.value.zoneMap.map(e => ({
        port: Number(e.port) || 0,
        startIdx: Number(e.startIdx) || 0,
        endIdx: Number(e.endIdx) || 0,
        rgbStartIdx: Number(e.rgbStartIdx) || 0,
      })),
    }
    await updateZone(editForm.value.name, body)
    await load()
    editing.value = false
  } finally {
    loading.value = false
  }
}

function cancelEdit() { editing.value = false }

// Delete
const confirmOpen = ref(false)
const deleteTarget = ref<ZoneDto | null>(null)
function askDelete(z: ZoneDto) { deleteTarget.value = z; confirmOpen.value = true }
function cancelDelete() { confirmOpen.value = false; deleteTarget.value = null }
async function confirmDelete() {
  if (!deleteTarget.value) return
  // Final guard: prevent delete if schedules exist for this zone (hooked up once available)
  if ((schedulesByZone.value[deleteTarget.value.name] || 0) > 0) {
    confirmOpen.value = false
    deleteTarget.value = null
    return
  }
  loading.value = true
  try {
    await deleteZone(deleteTarget.value.name)
    await load()
  } finally {
    loading.value = false
    confirmOpen.value = false
    deleteTarget.value = null
  }
}

// Toggle read-only from card
async function toggleReadonly(z: ZoneDto) {
  loading.value = true
  try {
    const body: UpdateZoneCommand = {
      ctlrName: z.ctlrName, // preserved by server if read-only
      isReadOnly: !z.isReadOnly,
      zoneMap: z.zoneMap,   // preserved by server if read-only
    }
    await updateZone(z.name, body)
    await load()
  } finally {
    loading.value = false
  }
}

function formatDate(s: string) {
  try { return new Date(s).toLocaleString() } catch { return s }
}

// Schedules per zone (to be wired once server/model exposes relationship)
const schedulesByZone = ref<Record<string, number>>({})

async function load() {
  loading.value = true
  try {
    const [list, scheds] = await Promise.all([
      getZones(),
      getSchedules().catch(() => [] as any as ScheduleDto[]),
    ])
    zones.value = list

    const map: Record<string, number> = {}
    for (const z of list) map[z.name] = 0
    for (const s of scheds as any as ScheduleDto[]) {
      const zn = (s as any).zoneNames || []
      for (const name of zn) {
        if (map[name] !== undefined) map[name]++
      }
    }
    schedulesByZone.value = map
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="header">
      <h1>Zones</h1>
      <div class="actions">
        <input class="filter-input" v-model.trim="filter" placeholder="Filter zones..." />
        <button class="icon" @click="startAdd" :disabled="adding || editing">➕</button>
      </div>
    </div>

    <div v-if="adding" class="add">
      <div class="row"><label>Name</label><input v-model.trim="form.name" placeholder="Zone name" /></div>
      <div class="row"><label>Controller</label><input v-model.trim="form.ctlrName" placeholder="Controller name" /></div>
      <div class="row"><label>Read-only</label><input type="checkbox" v-model="form.isReadOnly" /></div>
      <div class="row">
        <label>Zone Map</label>
        <div class="zmap">
          <div class="zhead">
            <div>Port</div><div>StartIdx</div><div>EndIdx</div><div>RgbStartIdx</div><div></div>
          </div>
          <div v-for="(e,i) in form.zoneMap" :key="i" class="zrow">
            <input type="number" v-model.number="e.port" />
            <input type="number" v-model.number="e.startIdx" />
            <input type="number" v-model.number="e.endIdx" />
            <input type="number" v-model.number="e.rgbStartIdx" />
            <button class="icon" title="Remove" @click="removeRow(i)">✖️</button>
          </div>
          <button class="sm" @click="addRow">Add row</button>
        </div>
      </div>
      <div v-if="createError" class="error">{{ createError }}</div>
      <div class="add-actions"><button :disabled="loading" @click="submitNew">Create</button><button :disabled="loading" @click="adding=false">Cancel</button></div>
    </div>

    <div v-if="editing" class="add">
      <div class="row"><label>Name</label><input v-model.trim="editForm.name" disabled /></div>
      <div class="row"><label>Controller</label><input v-model.trim="editForm.ctlrName" :disabled="zones.find(z=>z.name===editForm.name)?.isReadOnly" /></div>
      <div class="row"><label>Read-only</label><input type="checkbox" v-model="editForm.isReadOnly" /></div>
      <div class="row">
        <label>Zone Map</label>
        <div class="zmap">
          <div class="zhead">
            <div>Port</div><div>StartIdx</div><div>EndIdx</div><div>RgbStartIdx</div><div></div>
          </div>
          <div v-for="(e,i) in editForm.zoneMap" :key="i" class="zrow">
            <input type="number" v-model.number="e.port" :disabled="zones.find(z=>z.name===editForm.name)?.isReadOnly" />
            <input type="number" v-model.number="e.startIdx" :disabled="zones.find(z=>z.name===editForm.name)?.isReadOnly" />
            <input type="number" v-model.number="e.endIdx" :disabled="zones.find(z=>z.name===editForm.name)?.isReadOnly" />
            <input type="number" v-model.number="e.rgbStartIdx" :disabled="zones.find(z=>z.name===editForm.name)?.isReadOnly" />
            <button class="icon" title="Remove" @click="editRemoveRow(i)" :disabled="zones.find(z=>z.name===editForm.name)?.isReadOnly">✖️</button>
          </div>
          <button class="sm" @click="editAddRow" :disabled="zones.find(z=>z.name===editForm.name)?.isReadOnly">Add row</button>
        </div>
      </div>
      <div class="add-actions"><button :disabled="loading" @click="submitEdit">Save</button><button :disabled="loading" @click="cancelEdit">Cancel</button></div>
    </div>

    <div v-else class="cards">
      <div v-if="!filteredZones.length"><em>No matching zones.</em></div>
      <div v-else class="grid">
        <div v-for="z in filteredZones" :key="z.name" class="card">
          <div class="card-header">
            <div class="title">{{ z.name }}</div>
            <div class="right">
              <div class="badge" :class="{ ro: z.isReadOnly }">{{ z.isReadOnly ? 'Read-only' : 'Read/Write' }}</div>
              <div class="header-actions">
                <button class="icon" title="Edit" @click="startEdit(z)">✏️</button>
                <button class="icon" :title="(schedulesByZone[z.name]||0) > 0 ? 'Cannot delete: zone has schedules' : 'Delete'" :disabled="(schedulesByZone[z.name]||0) > 0" @click="askDelete(z)">🗑️</button>
              </div>
            </div>
          </div>
          <div class="rows">
            <div class="row"><strong>Controller:</strong> <span>{{ z.ctlrName }}</span></div>
            <div class="row"><strong>ZoneMap entries:</strong> <span>{{ z.zoneMap.length }}</span></div>
            <div class="row"><strong>Schedules:</strong> <span>{{ (schedulesByZone[z.name] || 0) }}</span></div>
            <div class="row"><strong>Created:</strong> <span>{{ formatDate(z.createDate) }}</span></div>
            <div class="row"><strong>Modified:</strong> <span>{{ formatDate(z.lastModifiedDate) }}</span></div>
          </div>
          <div class="card-actions">
            <button :disabled="loading" @click="toggleReadonly(z)">{{ z.isReadOnly ? 'Make Read/Write' : 'Make Read-only' }}</button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="confirmOpen" class="modal-backdrop" @click.self="cancelDelete">
      <div class="modal">
        <h3 style="margin-top:0">Delete zone</h3>
        <p>Are you sure you want to delete "{{ deleteTarget ? deleteTarget.name : '' }}"? This cannot be undone.</p>
        <div class="add-actions">
          <button :disabled="loading" @click="confirmDelete" style="background:#a00;color:#fff;border-color:#a00">Delete</button>
          <button :disabled="loading" @click="cancelDelete">Cancel</button>
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
.filter-input { padding: .4rem .5rem; background: #fff; color: #111; border: 1px solid #555; border-radius: 6px; min-width: 16rem; }
.add { border: 1px dashed #555; border-radius: 8px; padding: .75rem; margin-bottom: .75rem; display: grid; gap: .5rem; }
.add .row { display: grid; gap: .25rem; }
.add .row.two { grid-template-columns: 1fr 1fr; gap: .75rem; }
.add input { padding: .4rem .5rem; background: transparent; border: 1px solid #555; border-radius: 6px; color: inherit; }
.add select { padding: .4rem .5rem; background: #fff; color: #111; border: 1px solid #555; border-radius: 6px; }
.add option { color: #111; }
.add-actions { display: flex; gap: .5rem; justify-content: center; }
.error { color: #e33; }
.grid { display: flex; flex-wrap: wrap; gap: 1rem; align-items: stretch; }
.grid > .card { flex: 0 0 320px; width: 320px; max-width: 100%; }
@media (max-width: 700px) { .grid > .card { flex: 1 1 100%; width: 100%; } }
.card { border: 1px solid #444; border-radius: 8px; padding: .75rem; display: flex; flex-direction: column; gap: .5rem; }
.card-header { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
.title { font-weight: 600; }
.right { display: flex; align-items: center; gap: .5rem; }
.header-actions { display: flex; gap: .25rem; }
.header-actions .icon { width: 1.75rem; height: 1.75rem; display: inline-flex; align-items: center; justify-content: center; font-size: .95rem; }
.rows { display: grid; gap: .25rem; }
.row { display: flex; gap: .5rem; }
.card-actions { display: flex; gap: .5rem; margin-top: .25rem; }
.badge { font-size: .85rem; padding: .15rem .5rem; border-radius: 999px; border: 1px solid #555; background: #2c3e5015; }
.badge.ro { background: #8a2be21a; border-color: #7a1bd2; }
.zmap { display: grid; gap: .4rem; }
.zhead { display: grid; grid-template-columns: 1fr 1fr 1fr 1fr auto; gap: .5rem; font-weight: 600; font-size: .9rem; }
.zrow { display: grid; grid-template-columns: 1fr 1fr 1fr 1fr auto; gap: .5rem; align-items: center; }
.sm { width: fit-content; padding: .25rem .5rem; }
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: #1e1e1e; color: inherit; border: 1px solid #555; border-radius: 8px; padding: 1rem; max-width: 420px; width: calc(100% - 2rem); box-shadow: 0 10px 30px rgba(0,0,0,.6); }
</style>
