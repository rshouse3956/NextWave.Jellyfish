<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getAuditLogStats, type AuditLogStatsDto } from '../api/auditLogs'

const loading = ref(false)
const error = ref('')
const stats = ref<AuditLogStatsDto | null>(null)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const tz = new Date().getTimezoneOffset()
    stats.value = await getAuditLogStats(tz)
  } catch (e: any) {
    error.value = e?.response?.data?.message || 'Failed to load audit statistics'
  } finally {
    loading.value = false
  }
}

function formatTimestamp(ts: string) {
  try {
    return new Date(ts).toLocaleString()
  } catch {
    return ts
  }
}

onMounted(() => load())
</script>

<template>
  <div class="stats-card">
    <h2>Audit Statistics</h2>
    
    <p v-if="loading">Loading statistics...</p>
    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="!loading && stats" class="stats-grid">
      <div class="stat-item">
        <div class="stat-value">{{ stats.totalCount }}</div>
        <div class="stat-label">Total Actions</div>
      </div>

      <div class="stat-item">
        <div class="stat-value">{{ stats.todayCount }}</div>
        <div class="stat-label">Actions Today</div>
      </div>

      <div class="stat-item">
        <div class="stat-value">{{ stats.successRate.toFixed(1) }}%</div>
        <div class="stat-label">Success Rate</div>
      </div>

      <div class="stat-section">
        <h3>Action Type Breakdown</h3>
        <div class="action-types">
          <div v-for="(count, actionType) in stats.actionTypeCounts" :key="actionType" class="action-type-row">
            <span class="action-type-name">{{ actionType }}</span>
            <span class="action-type-count">{{ count }}</span>
          </div>
        </div>
      </div>

      <div class="stat-section" v-if="stats.recentFailures.length > 0">
        <h3>Recent Failures ({{ stats.recentFailures.length }})</h3>
        <div class="recent-failures">
          <div v-for="failure in stats.recentFailures" :key="failure.id" class="failure-row">
            <div class="failure-header">
              <strong>{{ failure.actionType }}</strong>
              <span class="failure-time">{{ formatTimestamp(failure.timestamp) }}</span>
            </div>
            <div class="failure-error">{{ failure.errorMessage }}</div>
          </div>
        </div>
      </div>
    </div>

    <button @click="load" :disabled="loading" class="refresh-btn">Refresh</button>
  </div>
</template>

<style scoped>
.stats-card {
  border: 1px solid #444;
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1rem;
}

.stats-card h2 {
  margin-top: 0;
  margin-bottom: 1rem;
}

.stats-grid {
  display: grid;
  gap: 1.5rem;
}

.stat-item {
  text-align: center;
  padding: 1rem;
  background: rgba(44, 62, 80, 0.1);
  border: 1px solid #444;
  border-radius: 8px;
}

.stat-value {
  font-size: 2rem;
  font-weight: bold;
}

.stat-label {
  font-size: 0.9rem;
  opacity: 0.8;
  margin-top: 0.25rem;
}

.stat-section {
  padding: 1rem;
  background: rgba(44, 62, 80, 0.1);
  border: 1px solid #444;
  border-radius: 8px;
}

.stat-section h3 {
  margin-top: 0;
  margin-bottom: 0.75rem;
  font-size: 1rem;
}

.action-types {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.action-type-row {
  display: flex;
  justify-content: space-between;
  padding: 0.5rem;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid #555;
  border-radius: 6px;
}

.action-type-name {
  font-weight: 500;
}

.action-type-count {
  opacity: 0.8;
  font-weight: bold;
}

.recent-failures {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.failure-row {
  padding: 0.75rem;
  background: rgba(0, 0, 0, 0.2);
  border-left: 3px solid #f44336;
  border: 1px solid #555;
  border-left: 3px solid #f44336;
  border-radius: 6px;
}

.failure-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.25rem;
}

.failure-time {
  font-size: 0.85rem;
  opacity: 0.8;
}

.failure-error {
  font-size: 0.9rem;
  color: #ff6b6b;
}

.refresh-btn {
  margin-top: 1rem;
}

.error {
  color: #ff6b6b;
  padding: 0.5rem;
  background: rgba(255, 107, 107, 0.1);
  border: 1px solid #ff6b6b;
  border-radius: 4px;
}

@media (min-width: 768px) {
  .stats-grid {
    grid-template-columns: repeat(3, 1fr);
  }

  .stat-section {
    grid-column: 1 / -1;
  }
}
</style>

