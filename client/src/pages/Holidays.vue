<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { getHolidays, type HolidayDto, createHoliday, type CreateHolidayCommand, updateHoliday, type UpdateHolidayCommand, deleteHoliday } from '../api/holidays'

const loading = ref(false)
const error = ref('')
const holidays = ref<HolidayDto[]>([])

const adding = ref(false)
const editing = ref(false)
const editName = ref('')

const form = ref<CreateHolidayCommand>({
  name: '',
  category: 'US Federal',
  cronExpression: '',
  comment: '',
  isEnabled: true,
})
const editForm = ref<UpdateHolidayCommand>({
  category: 'US Federal',
  cronExpression: '',
  comment: '',
  isEnabled: true,
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    holidays.value = await getHolidays()
  } catch (e: any) {
    error.value = e?.message || 'Failed to load holidays'
  } finally { loading.value = false }
}

function startAdd() {
  adding.value = true
  form.value = { name: '', category: 'US Federal', cronExpression: '', comment: '', isEnabled: true }
}

async function submitNew() {
  if (!form.value.name.trim()) { error.value = 'Name is required'; return }
  loading.value = true
  error.value = ''
  try {
    await createHoliday(form.value)
    adding.value = false
    await load()
  } catch (e: any) { error.value = e?.message || 'Failed to create holiday' } finally { loading.value = false }
}

function startEdit(h: HolidayDto) {
  editing.value = true
  editName.value = h.name
  editForm.value = { category: h.category, cronExpression: h.cronExpression, comment: h.comment, isEnabled: h.isEnabled }
}

async function submitEdit() {
  loading.value = true
  error.value = ''
  try {
    await updateHoliday(editName.value, editForm.value)
    editing.value = false
    await load()
  } catch (e: any) { error.value = e?.message || 'Failed to update holiday' } finally { loading.value = false }
}

function cancelEdit() { editing.value = false }

const confirmOpen = ref(false)
const deleteTarget = ref<HolidayDto | null>(null)
function askDelete(h: HolidayDto) { deleteTarget.value = h; confirmOpen.value = true }
function cancelDelete() { confirmOpen.value = false; deleteTarget.value = null }
async function confirmDelete() {
  if (!deleteTarget.value) return
  loading.value = true
  error.value = ''
  try {
    await deleteHoliday(deleteTarget.value.name)
    cancelDelete()
    await load()
  } catch (e: any) { error.value = e?.message || 'Failed to delete holiday' } finally { loading.value = false }
}

async function toggleEnabled(h: HolidayDto) {
  loading.value = true
  error.value = ''
  try {
    await updateHoliday(h.name, { category: h.category, cronExpression: h.cronExpression, comment: h.comment, isEnabled: !h.isEnabled })
    await load()
  } catch (e: any) { error.value = e?.message || 'Failed to update' } finally { loading.value = false }
}

// Filter and cron validation helpers
const filter = ref('')
const filteredHolidays = computed(() => {
  const q = filter.value.trim().toLowerCase()
  if (!q) return holidays.value
  return holidays.value.filter(h =>
    h.name.toLowerCase().includes(q) ||
    (h.category || '').toLowerCase().includes(q) ||
    (h.comment || '').toLowerCase().includes(q)
  )
})

function cronFieldCount(expr: string) {
  const t = (expr || '').trim()
  return t ? t.split(/\s+/).filter(Boolean).length : 0
}
const createCronCount = computed(() => cronFieldCount(form.value.cronExpression))
const createCronValid = computed(() => createCronCount.value === 5 || createCronCount.value === 6)
const editCronCount = computed(() => cronFieldCount(editForm.value.cronExpression))
const editCronValid = computed(() => editCronCount.value === 5 || editCronCount.value === 6)

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="header">
      <h1>Holidays</h1>
      <div class="actions">
        <input class="filter-input" v-model.trim="filter" placeholder="Filter holidays..." />
        <button class="icon" @click="startAdd" :disabled="adding || editing">➕</button>
      </div>
    </div>

    <div v-if="adding" class="add">
      <div class="row"><label>Name</label><input v-model.trim="form.name" placeholder="e.g., Independence Day" /></div>
      <div class="row"><label>Category</label>
        <select v-model="form.category">
          <option>US Federal</option>
          <option>State</option>
          <option>Special</option>
        </select>
      </div>
      <div class="row"><label>Cron Expression</label>
        <div>
          <input v-model.trim="form.cronExpression" placeholder="sec min hr dom mon dow" />
          <div class="hint" :class="{ invalid: !createCronValid }">
            <span v-if="createCronValid">Fields: {{ createCronCount }} (OK)</span>
            <span v-else>Expected 5 or 6 fields (got {{ createCronCount }})</span>
          </div>
          <div class="hint">Examples: 6-field "0 0 0 25 12 *" (Dec 25), 5-field "0 0 25 12 *" (Dec 25)</div>
        </div>
      </div>
      <div class="row"><label>Comment</label><input v-model.trim="form.comment" placeholder="Optional" /></div>
      <div class="row"><label>Enabled</label><input type="checkbox" v-model="form.isEnabled" /></div>
      <div class="add-actions"><button :disabled="loading" @click="submitNew">Create</button><button :disabled="loading" @click="adding=false">Cancel</button></div>
    </div>

    <div v-if="editing" class="add">
      <div class="row"><label>Name</label><input :value="editName" disabled /></div>
      <div class="row"><label>Category</label>
        <select v-model="editForm.category">
          <option>US Federal</option>
          <option>State</option>
          <option>Special</option>
        </select>
      </div>
      <div class="row"><label>Cron Expression</label>
        <div>
          <input v-model.trim="editForm.cronExpression" placeholder="sec min hr dom mon dow" />
          <div class="hint" :class="{ invalid: !editCronValid }">
            <span v-if="editCronValid">Fields: {{ editCronCount }} (OK)</span>
            <span v-else>Expected 5 or 6 fields (got {{ editCronCount }})</span>
          </div>
          <div class="hint">Examples: 6-field "0 0 0 25 12 *" (Dec 25), 5-field "0 0 25 12 *" (Dec 25)</div>
        </div>
      </div>
      <div class="row"><label>Comment</label><input v-model.trim="editForm.comment" placeholder="Optional" /></div>
      <div class="row"><label>Enabled</label><input type="checkbox" v-model="editForm.isEnabled" /></div>
      <div class="add-actions"><button :disabled="loading" @click="submitEdit">Save</button><button :disabled="loading" @click="cancelEdit">Cancel</button></div>
    </div>

    <div v-if="loading">Loading…</div>
    <div v-else-if="error" class="error">{{ error }}</div>

    <div v-else class="cards">
      <div v-if="!filteredHolidays.length"><em>No matching holidays.</em></div>
      <div v-else class="grid">
        <div v-for="h in filteredHolidays" :key="h.name" class="card">
          <div class="card-header">
            <div class="title">{{ h.name }}</div>
            <div class="right">
              <div class="category">{{ h.category }}</div>
              <div class="header-actions">
                <button class="icon" @click="startEdit(h)" title="Edit">✏️</button>
                <button class="icon" @click="askDelete(h)" title="Delete">🗑️</button>
              </div>
            </div>
          </div>
          <div class="rows">
            <div class="row"><strong>Cron:</strong> <span>{{ h.cronExpression }}</span></div>
            <div class="row"><strong>Comment:</strong> <span>{{ h.comment || '—' }}</span></div>
            <div class="row"><strong>Enabled:</strong> <span>{{ h.isEnabled ? 'Yes' : 'No' }}</span></div>
          </div>
          <div class="card-actions">
            <button @click="toggleEnabled(h)">{{ h.isEnabled ? 'Disable' : 'Enable' }}</button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="confirmOpen" class="modal-backdrop" @click.self="cancelDelete">
      <div class="modal">
        <h3 style="margin-top:0">Delete holiday</h3>
        <p>Are you sure you want to delete "{{ deleteTarget ? deleteTarget.name : '' }}"? This cannot be undone.</p>        <div class="add-actions">
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
.add { border: 1px dashed #555; border-radius: 8px; padding: .75rem; margin-bottom: .75rem; display: grid; gap: .5rem; }
.add .row { display: grid; gap: .25rem; }
.add .row.two { grid-template-columns: 1fr 1fr; gap: .75rem; }
.add input { padding: .4rem .5rem; background: transparent; border: 1px solid #555; border-radius: 6px; color: inherit; }
.add select { padding: .4rem .5rem; background: #fff; color: #111; border: 1px solid #555; border-radius: 6px; }
.filter-input { padding: .4rem .5rem; background: #fff; color: #111; border: 1px solid #555; border-radius: 6px; min-width: 16rem; }
.hint { font-size: .85rem; opacity: .8; margin-top: .25rem; }
.hint.invalid { color: #e33; opacity: 1; }

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
.category { font-size: .85rem; padding: .15rem .5rem; border-radius: 999px; border: 1px solid #555; background: #2c3e5015; }
@media (max-width: 700px) { .add .row.two { grid-template-columns: 1fr; } }
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: #1e1e1e; color: inherit; border: 1px solid #555; border-radius: 8px; padding: 1rem; max-width: 420px; width: calc(100% - 2rem); box-shadow: 0 10px 30px rgba(0,0,0,.6); }
</style>
