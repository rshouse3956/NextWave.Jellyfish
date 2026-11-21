<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { FolderDto } from '../api/folders'
import { getFolders, createFolder, updateFolder, deleteFolder } from '../api/folders'
import { getPatternsByFolder } from '../api/patterns'

const loading = ref(false)
const folders = ref<FolderDto[]>([])

// Filter
const filter = ref('')
const filteredFolders = computed(() => {
  const q = filter.value.trim().toLowerCase()
  if (!q) return folders.value
  return folders.value.filter(f =>
    f.name.toLowerCase().includes(q) ||
    (f.isReadOnly ? 'read-only' : 'read write').includes(q)
  )
})

// Add
const adding = ref(false)
const form = ref({ name: '', isReadOnly: false })
const createError = ref('')

function startAdd() {
  adding.value = true
  editing.value = false
  form.value = { name: '', isReadOnly: false }
  createError.value = ''
}

async function submitNew() {
  if (!form.value.name.trim()) { createError.value = 'Name is required'; return }
  loading.value = true
  try {
    await createFolder({ name: form.value.name.trim(), isReadOnly: form.value.isReadOnly })
    await load()
    adding.value = false
  } catch (e: any) {
    createError.value = e?.response?.data?.message || 'Failed to create folder'
  } finally {
    loading.value = false
  }
}

// Edit
const editing = ref(false)
const editForm = ref<{ name: string; isReadOnly: boolean }>({ name: '', isReadOnly: false })

function startEdit(f: FolderDto) {
  editing.value = true
  adding.value = false
  editForm.value = { name: f.name, isReadOnly: f.isReadOnly }
}

async function submitEdit() {
  loading.value = true
  try {
    await updateFolder(editForm.value.name, { isReadOnly: editForm.value.isReadOnly })
    await load()
    editing.value = false
  } catch (e) {
    // noop; surface via toast in future
  } finally {
    loading.value = false
  }
}

function cancelEdit() { editing.value = false }

// Delete
const confirmOpen = ref(false)
const deleteTarget = ref<FolderDto | null>(null)
function askDelete(f: FolderDto) { deleteTarget.value = f; confirmOpen.value = true }
function cancelDelete() { confirmOpen.value = false; deleteTarget.value = null }
async function confirmDelete() {
  if (!deleteTarget.value) return
  // Final guard: prevent delete if patterns exist
  if ((patternsByFolder.value[deleteTarget.value.name] || 0) > 0) {
    confirmOpen.value = false
    deleteTarget.value = null
    return
  }
  loading.value = true
  try {
    await deleteFolder(deleteTarget.value.name)
    await load()
  } finally {
    loading.value = false
    confirmOpen.value = false
    deleteTarget.value = null
  }
}

// Toggle read-only
async function toggleReadonly(f: FolderDto) {
  loading.value = true
  try {
    await updateFolder(f.name, { isReadOnly: !f.isReadOnly })
    await load()
  } finally {
    loading.value = false
  }
}

function formatDate(s: string) {
  try { return new Date(s).toLocaleString() } catch { return s }
}

const patternsByFolder = ref<Record<string, number>>({})

async function load() {
  loading.value = true
  try {
    const list = await getFolders()
    folders.value = list
    // Fetch pattern counts per folder
    const entries = await Promise.all(list.map(async f => {
      try {
        const pats = await getPatternsByFolder(f.name)
        return [f.name, pats.length] as const
      } catch {
        return [f.name, 0] as const
      }
    }))
    const map: Record<string, number> = {}
    for (const [name, count] of entries) map[name] = count
    patternsByFolder.value = map
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="header">
      <h1>Folders</h1>
      <div class="actions">
        <input class="filter-input" v-model.trim="filter" placeholder="Filter folders..." />
        <button class="icon" @click="startAdd" :disabled="adding || editing">➕</button>
      </div>
    </div>

    <div v-if="adding" class="add">
      <div class="row"><label>Name</label><input v-model.trim="form.name" placeholder="Folder name" /></div>
      <div class="row"><label>Read-only</label><input type="checkbox" v-model="form.isReadOnly" /></div>
      <div v-if="createError" class="error">{{ createError }}</div>
      <div class="add-actions"><button :disabled="loading" @click="submitNew">Create</button><button :disabled="loading" @click="adding=false">Cancel</button></div>
    </div>

    <div v-if="editing" class="add">
      <div class="row"><label>Name</label><input v-model.trim="editForm.name" disabled /></div>
      <div class="row"><label>Read-only</label><input type="checkbox" v-model="editForm.isReadOnly" /></div>
      <div class="add-actions"><button :disabled="loading" @click="submitEdit">Save</button><button :disabled="loading" @click="cancelEdit">Cancel</button></div>
    </div>

    <div v-else class="cards">
      <div v-if="!filteredFolders.length"><em>No matching folders.</em></div>
      <div v-else class="grid">
        <div v-for="f in filteredFolders" :key="f.name" class="card">
          <div class="card-header">
            <div class="title">{{ f.name }}</div>
            <div class="right">
              <div class="badge" :class="{ ro: f.isReadOnly }">{{ f.isReadOnly ? 'Read-only' : 'Read/Write' }}</div>
              <div class="header-actions">
                <button class="icon" title="Edit" @click="startEdit(f)">✏️</button>
                <button class="icon" :title="(patternsByFolder[f.name]||0) > 0 ? 'Cannot delete: folder has patterns' : 'Delete'" :disabled="(patternsByFolder[f.name]||0) > 0" @click="askDelete(f)">🗑️</button>
              </div>
            </div>
          </div>
          <div class="rows">
            <div class="row"><strong>Patterns:</strong> <span>{{ (patternsByFolder[f.name] || 0) }}</span> <router-link class="link" :to="{ name: 'patterns', query: { folder: f.name } }">View</router-link></div>
            <div class="row"><strong>Created:</strong> <span>{{ formatDate(f.createDate) }}</span></div>
            <div class="row"><strong>Modified:</strong> <span>{{ formatDate(f.lastModifiedDate) }}</span></div>
          </div>
          <div class="card-actions">
            <button :disabled="loading" @click="toggleReadonly(f)">{{ f.isReadOnly ? 'Make Read/Write' : 'Make Read-only' }}</button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="confirmOpen" class="modal-backdrop" @click.self="cancelDelete">
      <div class="modal">
        <h3 style="margin-top:0">Delete folder</h3>
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
.link { color: #6aa0ff; text-decoration: underline; margin-left: .5rem; }
@media (max-width: 700px) { .add .row.two { grid-template-columns: 1fr; } }
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: #1e1e1e; color: inherit; border: 1px solid #555; border-radius: 8px; padding: 1rem; max-width: 420px; width: calc(100% - 2rem); box-shadow: 0 10px 30px rgba(0,0,0,.6); }
</style>

