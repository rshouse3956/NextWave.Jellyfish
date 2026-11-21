<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { getAuditLogs, type AuditLogDto, type AuditLogFilters } from '../api/auditLogs'
import AuditStatsCard from '../components/AuditStatsCard.vue'

const loading = ref(false)
const error = ref('')
const logs = ref<AuditLogDto[]>([])

// Filters
const actionTypeFilter = ref('')
const sourceTypeFilter = ref('')
const startDateFilter = ref('')
const endDateFilter = ref('')

// Pagination
const skip = ref(0)
const take = ref(50)
const hasMore = computed(() => logs.value.length === take.value)

// Expanded details
const expandedId = ref<string | null>(null)

function toggleExpand(id: string) {
  expandedId.value = expandedId.value === id ? null : id
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const filters: AuditLogFilters = {
      skip: skip.value,
      take: take.value,
    }
    
    if (actionTypeFilter.value) filters.actionType = actionTypeFilter.value
    if (sourceTypeFilter.value) filters.sourceType = sourceTypeFilter.value
    if (startDateFilter.value) filters.startDateUtc = new Date(startDateFilter.value).toISOString()
    if (endDateFilter.value) filters.endDateUtc = new Date(endDateFilter.value).toISOString()

    logs.value = await getAuditLogs(filters)
  } catch (e: any) {
    error.value = e?.response?.data?.message || 'Failed to load audit logs'
  } finally {
    loading.value = false
  }
}

function clearFilters() {
  actionTypeFilter.value = ''
  sourceTypeFilter.value = ''
  startDateFilter.value = ''
  endDateFilter.value = ''
  skip.value = 0
  load()
}

function applyFilters() {
  skip.value = 0
  load()
}

function nextPage() {
  skip.value += take.value
  load()
}

function prevPage() {
  skip.value = Math.max(0, skip.value - take.value)
  load()
}

function formatTimestamp(ts: string) {
  try {
    return new Date(ts).toLocaleString()
  } catch {
    return ts
  }
}

function formatDuration(ms: number) {
  if (ms < 1000) return `${ms}ms`
  return `${(ms / 1000).toFixed(2)}s`
}

onMounted(() => load())
</script>

<template>
  <div class="page">
    <div class="header">
      <h1>Audit Logs</h1>
    </div>

    <!-- Statistics -->
    <AuditStatsCard />

    <!-- Filters -->
    <div class="filters-card">
      <h3>Filters</h3>
      <div class="filter-row">
        <label>
          Action Type:
          <select v-model="actionTypeFilter">
            <option value="">All</option>
            <option value="RunPattern">RunPattern</option>
            <option value="SyncFolders">SyncFolders</option>
            <option value="SyncZones">SyncZones</option>
            <option value="SyncPatterns">SyncPatterns</option>
            <option value="SyncPatternDetail">SyncPatternDetail</option>
            <option value="GeneratePattern">GeneratePattern</option>
          </select>
        </label>

        <label>
          Source Type:
          <select v-model="sourceTypeFilter">
            <option value="">All</option>
            <option value="Manual">Manual</option>
            <option value="Schedule">Schedule</option>
            <option value="System">System</option>
          </select>
        </label>

        <label>
          Start Date:
          <input type="datetime-local" v-model="startDateFilter" />
        </label>

        <label>
          End Date:
          <input type="datetime-local" v-model="endDateFilter" />
        </label>
      </div>

      <div class="filter-actions">
        <button @click="applyFilters" :disabled="loading">Apply Filters</button>
        <button @click="clearFilters" :disabled="loading">Clear Filters</button>
      </div>
    </div>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="loading">Loading...</p>

    <!-- Audit Log Cards -->
    <div class="cards" v-if="!loading && logs.length > 0">
      <div v-for="log in logs" :key="log.id" class="card">
        <div class="card-header" @click="toggleExpand(log.id)" style="cursor: pointer;">
          <div class="card-title">
            <span :class="['status-badge', log.success ? 'success' : 'failure']">
              {{ log.success ? '✓' : '✗' }}
            </span>
            <strong>{{ log.actionType }}</strong>
            <span class="timestamp">{{ formatTimestamp(log.timestamp) }}</span>
          </div>
          <div class="card-subtitle">
            Source: {{ log.sourceType }} {{ log.sourceId ? `(${log.sourceId})` : '' }}
            • Duration: {{ formatDuration(log.durationMs) }}
          </div>
        </div>

        <div class="card-body">
          <div v-if="log.folderName || log.patternName" class="detail-row">
            <strong>Pattern:</strong> {{ log.folderName ? `${log.folderName}/` : '' }}{{ log.patternName }}
          </div>
          <div v-if="log.zoneNames && log.zoneNames.length > 0" class="detail-row">
            <strong>Zones:</strong> {{ log.zoneNames.join(', ') }}
          </div>
          <div v-if="log.state !== null" class="detail-row">
            <strong>State:</strong> {{ log.state === 1 ? 'On' : 'Off' }}
          </div>
          <div v-if="!log.success && log.errorMessage" class="detail-row error-message">
            <strong>Error:</strong> {{ log.errorMessage }}
          </div>

          <!-- Expandable Details -->
          <div v-if="expandedId === log.id" class="expanded-details">
            <div class="detail-section">
              <strong>Request Payload:</strong>
              <pre>{{ log.requestPayload || 'N/A' }}</pre>
            </div>
            <div class="detail-section">
              <strong>Response Payload:</strong>
              <pre>{{ log.responsePayload || 'N/A' }}</pre>
            </div>
          </div>

          <button @click="toggleExpand(log.id)" class="expand-btn">
            {{ expandedId === log.id ? 'Hide Details' : 'Show Details' }}
          </button>
        </div>
      </div>
    </div>

    <p v-if="!loading && logs.length === 0">No audit logs found.</p>

    <!-- Pagination -->
    <div class="pagination" v-if="logs.length > 0">
      <button @click="prevPage" :disabled="skip === 0 || loading">Previous</button>
      <span>Showing {{ skip + 1 }} - {{ skip + logs.length }}</span>
      <button @click="nextPage" :disabled="!hasMore || loading">Next</button>
    </div>
  </div>
</template>

<style scoped>
.page {
  max-width: 1100px;
  margin: 0 auto;
  padding: 1rem;
}

.header {
  margin-bottom: 1rem;
}

.filters-card {
  border: 1px solid #444;
  border-radius: 8px;
  padding: 1rem;
  margin-bottom: 1rem;
}

.filters-card h3 {
  margin-top: 0;
  margin-bottom: 0.75rem;
}

.filter-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
}

.filter-row label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.filter-row input,
.filter-row select {
  padding: 0.4rem 0.5rem;
  background: transparent;
  border: 1px solid #555;
  border-radius: 6px;
  color: inherit;
}

.filter-actions {
  display: flex;
  gap: 0.5rem;
}

.cards {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.card {
  border: 1px solid #444;
  border-radius: 8px;
  overflow: hidden;
}

.card-header {
  padding: 1rem;
  background: rgba(44, 62, 80, 0.1);
  border-bottom: 1px solid #444;
}

.card-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.25rem;
}

.card-subtitle {
  font-size: 0.9rem;
  opacity: 0.8;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-weight: bold;
  font-size: 14px;
}

.status-badge.success {
  background: #4caf50;
  color: white;
}

.status-badge.failure {
  background: #f44336;
  color: white;
}

.timestamp {
  margin-left: auto;
  font-size: 0.9rem;
  opacity: 0.8;
}

.card-body {
  padding: 1rem;
}

.detail-row {
  margin-bottom: 0.5rem;
}

.error-message {
  color: #ff6b6b;
}

.expanded-details {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #444;
}

.detail-section {
  margin-bottom: 1rem;
}

.detail-section pre {
  background: rgba(0, 0, 0, 0.2);
  padding: 0.5rem;
  border-radius: 4px;
  overflow-x: auto;
  font-size: 0.85rem;
  border: 1px solid #444;
}

.expand-btn {
  margin-top: 0.5rem;
  padding: 0.25rem 0.75rem;
  font-size: 0.85rem;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 1rem;
  padding: 1rem;
}

.error {
  color: #ff6b6b;
  padding: 0.5rem;
  background: rgba(255, 107, 107, 0.1);
  border: 1px solid #ff6b6b;
  border-radius: 4px;
}
</style>

