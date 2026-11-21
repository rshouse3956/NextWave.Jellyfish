<script setup lang="ts">
import { ref, computed } from 'vue'
import { debugIsActive, debugMultiDayEvents, type IsActiveDebugRequest, type MultiDayEventsDebugRequest, type IsActiveDebugResponse, type MultiDayEventsDebugResponse } from '../api/schedules'

// IsActive Test State
const isActiveForm = ref<IsActiveDebugRequest>({
  onTime: '18:00',
  onDay: '* * 1-5',
  offTime: '23:00',
  offDay: '* * 1-5',
  nowUtc: null
})

const isActiveResult = ref<IsActiveDebugResponse | null>(null)
const isActiveError = ref('')
const isActiveLoading = ref(false)

// MultiDayEvents Test State
const multiDayForm = ref<MultiDayEventsDebugRequest>({
  onTime: '18:00',
  onDay: '1 * *',
  offTime: '06:00',
  offDay: '5 * *',
  fromUtc: null,
  toUtc: null
})

const multiDayResult = ref<MultiDayEventsDebugResponse | null>(null)
const multiDayError = ref('')
const multiDayLoading = ref(false)

// Preset schedules
const presets = [
  { name: 'Weekday Evening', onTime: '18:00', onDay: '* * 1-5', offTime: '23:00', offDay: '* * 1-5' },
  { name: 'Weekend All-Night', onTime: '18:00', onDay: '* * 6-7', offTime: '06:00', offDay: '* * 6-7' },
  { name: 'Multi-Day (1st-5th)', onTime: '18:00', onDay: '1 * *', offTime: '23:00', offDay: '5 * *' },
  { name: 'Daily Sunrise-Sunset', onTime: 'Sunrise', onDay: '* * *', offTime: 'Sunset', offDay: '* * *' },
  { name: 'Christmas Day', onTime: '17:00', onDay: '25 12 *', offTime: '23:59', offDay: '25 12 *' },
]

function applyPreset(preset: typeof presets[0]) {
  isActiveForm.value.onTime = preset.onTime
  isActiveForm.value.onDay = preset.onDay
  isActiveForm.value.offTime = preset.offTime
  isActiveForm.value.offDay = preset.offDay
  
  multiDayForm.value.onTime = preset.onTime
  multiDayForm.value.onDay = preset.onDay
  multiDayForm.value.offTime = preset.offTime
  multiDayForm.value.offDay = preset.offDay
}

function setNowToCurrentTime() {
  // Format for datetime-local input: YYYY-MM-DDTHH:mm:ss (local time)
  const now = new Date()
  // Format as local time for datetime-local input
  const localIso = now.getFullYear() + '-' +
    String(now.getMonth() + 1).padStart(2, '0') + '-' +
    String(now.getDate()).padStart(2, '0') + 'T' +
    String(now.getHours()).padStart(2, '0') + ':' +
    String(now.getMinutes()).padStart(2, '0') + ':' +
    String(now.getSeconds()).padStart(2, '0')
  isActiveForm.value.nowUtc = localIso
}

function clearNowUtc() {
  isActiveForm.value.nowUtc = null
}

function formatLocalDateTime(date: Date): string {
  return date.getFullYear() + '-' +
    String(date.getMonth() + 1).padStart(2, '0') + '-' +
    String(date.getDate()).padStart(2, '0') + 'T' +
    String(date.getHours()).padStart(2, '0') + ':' +
    String(date.getMinutes()).padStart(2, '0') + ':' +
    String(date.getSeconds()).padStart(2, '0')
}

function setFromToCurrentTime() {
  const now = new Date()
  multiDayForm.value.fromUtc = formatLocalDateTime(now)
}

function setToOneWeekFromNow() {
  const date = new Date()
  date.setDate(date.getDate() + 7)
  multiDayForm.value.toUtc = formatLocalDateTime(date)
}

function clearFromUtc() {
  multiDayForm.value.fromUtc = null
}

function clearToUtc() {
  multiDayForm.value.toUtc = null
}

async function testIsActive() {
  isActiveLoading.value = true
  isActiveError.value = ''
  isActiveResult.value = null

  try {
    // Convert datetime-local format to ISO if provided
    const request = {
      ...isActiveForm.value,
      nowUtc: isActiveForm.value.nowUtc ? new Date(isActiveForm.value.nowUtc).toISOString() : null
    }
    const result = await debugIsActive(request)
    isActiveResult.value = result
  } catch (e: any) {
    isActiveError.value = e?.response?.data?.error || e?.message || 'Failed to test IsActive'
  } finally {
    isActiveLoading.value = false
  }
}

async function testMultiDayEvents() {
  multiDayLoading.value = true
  multiDayError.value = ''
  multiDayResult.value = null

  try {
    // Convert datetime-local format to ISO if provided
    const request = {
      ...multiDayForm.value,
      fromUtc: multiDayForm.value.fromUtc ? new Date(multiDayForm.value.fromUtc).toISOString() : null,
      toUtc: multiDayForm.value.toUtc ? new Date(multiDayForm.value.toUtc).toISOString() : null
    }
    const result = await debugMultiDayEvents(request)
    multiDayResult.value = result
  } catch (e: any) {
    multiDayError.value = e?.response?.data?.error || e?.message || 'Failed to test MultiDayEvents'
  } finally {
    multiDayLoading.value = false
  }
}

function formatDateTime(dt: string | null | undefined) {
  if (!dt) return '—'
  try {
    return new Date(dt).toLocaleString()
  } catch {
    return dt
  }
}

function formatDate(dt: string | null | undefined) {
  if (!dt) return '—'
  try {
    return new Date(dt).toLocaleDateString()
  } catch {
    return dt
  }
}

const onEvents = computed(() => multiDayResult.value?.events.filter(e => e.kind === 'On') || [])
const offEvents = computed(() => multiDayResult.value?.events.filter(e => e.kind === 'Off') || [])
</script>

<template>
  <div class="page">
    <h2>Schedule Diagnostics</h2>
    <p style="opacity: 0.8; margin-bottom: 1.5rem">
      Test schedule activation logic with specific datetime values. These endpoints are only available in Development mode.
    </p>

    <!-- Presets -->
    <div class="presets">
      <h3>Quick Presets</h3>
      <div class="preset-buttons">
        <button v-for="preset in presets" :key="preset.name" @click="applyPreset(preset)">
          {{ preset.name }}
        </button>
      </div>
    </div>

    <!-- IsActive Test -->
    <div class="test-section">
      <h3>Test IsActive</h3>
      <p style="opacity: 0.8; font-size: 0.9rem">Check if a schedule would be active at a specific datetime.</p>
      
      <div class="form">
        <div class="row">
          <label>On Time</label>
          <input v-model.trim="isActiveForm.onTime" placeholder="HH:mm or Sunrise/Sunset" />
        </div>
        <div class="row">
          <label>On Day</label>
          <input v-model.trim="isActiveForm.onDay" placeholder="* * * (DOM MON DOW)" />
        </div>
        <div class="row">
          <label>Off Time</label>
          <input v-model.trim="isActiveForm.offTime" placeholder="HH:mm or Sunrise/Sunset" />
        </div>
        <div class="row">
          <label>Off Day</label>
          <input v-model.trim="isActiveForm.offDay" placeholder="* * * (DOM MON DOW)" />
        </div>
        <div class="row">
          <label>Now (Local)</label>
          <div style="display: flex; gap: 0.5rem; align-items: center; flex: 1">
            <input
              v-model.trim="isActiveForm.nowUtc"
              placeholder="Leave empty for current time"
              style="flex: 1"
              type="datetime-local"
              step="1"
              title="Enter time in your local timezone. Will be converted to UTC before sending to API."
            />
            <button @click="setNowToCurrentTime" class="small">Now</button>
            <button @click="clearNowUtc" class="small">Clear</button>
          </div>
        </div>
        
        <div class="actions">
          <button @click="testIsActive" :disabled="isActiveLoading">
            {{ isActiveLoading ? 'Testing...' : 'Test IsActive' }}
          </button>
        </div>
      </div>

      <div v-if="isActiveError" class="error">{{ isActiveError }}</div>
      
      <div v-if="isActiveResult" class="result">
        <h4>Result</h4>
        <div class="result-grid">
          <div class="result-item">
            <span class="label">Is Active:</span>
            <span class="value" :class="{ active: isActiveResult.isActive, inactive: !isActiveResult.isActive }">
              {{ isActiveResult.isActive ? '✓ YES' : '✗ NO' }}
            </span>
          </div>
          <div class="result-item">
            <span class="label">Evaluated At:</span>
            <span class="value">{{ formatDateTime(isActiveResult.evaluatedAt) }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- MultiDayEvents Test -->
    <div class="test-section">
      <h3>Test MultiDayEvents</h3>
      <p style="opacity: 0.8; font-size: 0.9rem">Compute all ON/OFF events within a date range.</p>
      
      <div class="form">
        <div class="row">
          <label>On Time</label>
          <input v-model.trim="multiDayForm.onTime" placeholder="HH:mm or Sunrise/Sunset" />
        </div>
        <div class="row">
          <label>On Day</label>
          <input v-model.trim="multiDayForm.onDay" placeholder="* * * (DOM MON DOW)" />
        </div>
        <div class="row">
          <label>Off Time</label>
          <input v-model.trim="multiDayForm.offTime" placeholder="HH:mm or Sunrise/Sunset" />
        </div>
        <div class="row">
          <label>Off Day</label>
          <input v-model.trim="multiDayForm.offDay" placeholder="* * * (DOM MON DOW)" />
        </div>
        <div class="row">
          <label>From (Local)</label>
          <div style="display: flex; gap: 0.5rem; align-items: center; flex: 1">
            <input
              v-model.trim="multiDayForm.fromUtc"
              placeholder="Leave empty for current time"
              style="flex: 1"
              type="datetime-local"
              step="1"
              title="Enter time in your local timezone. Will be converted to UTC before sending to API."
            />
            <button @click="setFromToCurrentTime" class="small">Now</button>
            <button @click="clearFromUtc" class="small">Clear</button>
          </div>
        </div>
        <div class="row">
          <label>To (Local)</label>
          <div style="display: flex; gap: 0.5rem; align-items: center; flex: 1">
            <input
              v-model.trim="multiDayForm.toUtc"
              placeholder="Leave empty for +7 days"
              style="flex: 1"
              type="datetime-local"
              step="1"
              title="Enter time in your local timezone. Will be converted to UTC before sending to API."
            />
            <button @click="setToOneWeekFromNow" class="small">+7 Days</button>
            <button @click="clearToUtc" class="small">Clear</button>
          </div>
        </div>
        
        <div class="actions">
          <button @click="testMultiDayEvents" :disabled="multiDayLoading">
            {{ multiDayLoading ? 'Computing...' : 'Compute Events' }}
          </button>
        </div>
      </div>

      <div v-if="multiDayError" class="error">{{ multiDayError }}</div>
      
      <div v-if="multiDayResult" class="result">
        <h4>Result ({{ multiDayResult.eventCount }} events)</h4>
        <div class="result-summary">
          <div><strong>From:</strong> {{ formatDateTime(multiDayResult.fromUtc) }}</div>
          <div><strong>To:</strong> {{ formatDateTime(multiDayResult.toUtc) }}</div>
        </div>
        
        <div class="events-grid">
          <div class="events-column">
            <h5>ON Events ({{ onEvents.length }})</h5>
            <div class="event-list">
              <div v-for="(event, idx) in onEvents" :key="idx" class="event-item on">
                <div class="event-time">{{ formatDateTime(event.eventUtc) }}</div>
                <div class="event-date">{{ formatDate(event.localDate) }}</div>
              </div>
              <div v-if="onEvents.length === 0" class="no-events">No ON events</div>
            </div>
          </div>
          
          <div class="events-column">
            <h5>OFF Events ({{ offEvents.length }})</h5>
            <div class="event-list">
              <div v-for="(event, idx) in offEvents" :key="idx" class="event-item off">
                <div class="event-time">{{ formatDateTime(event.eventUtc) }}</div>
                <div class="event-date">{{ formatDate(event.localDate) }}</div>
              </div>
              <div v-if="offEvents.length === 0" class="no-events">No OFF events</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.page {
  max-width: 1100px;
  margin: 0 auto;
  padding: 1rem;
}

h2 {
  margin-bottom: 0.5rem;
}

h3 {
  margin-top: 1.5rem;
  margin-bottom: 0.75rem;
}

.presets {
  margin-bottom: 2rem;
  padding: 1rem;
  border: 1px solid #444;
  border-radius: 8px;
}

.presets h3 {
  margin-top: 0;
}

.preset-buttons {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.preset-buttons button {
  padding: 0.5rem 1rem;
  font-size: 0.9rem;
}

.test-section {
  margin-bottom: 2rem;
  padding: 1rem;
  border: 1px solid #444;
  border-radius: 8px;
}

.form {
  display: grid;
  gap: 0.75rem;
  margin-top: 1rem;
}

.row {
  display: grid;
  grid-template-columns: 150px 1fr;
  gap: 0.5rem;
  align-items: center;
}

.row label {
  font-weight: 600;
}

.row input,
.row select {
  padding: 0.4rem 0.5rem;
  background: transparent;
  border: 1px solid #555;
  border-radius: 6px;
  color: inherit;
}

.actions {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

button {
  padding: 0.5rem 1rem;
  border-radius: 6px;
  border: 1px solid #555;
  background: #1a1a1a;
  color: inherit;
  cursor: pointer;
}

button:hover:not(:disabled) {
  border-color: #646cff;
}

button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

button.small {
  padding: 0.3rem 0.6rem;
  font-size: 0.85rem;
}

.error {
  margin-top: 1rem;
  padding: 0.75rem;
  background: #4a1a1a;
  border: 1px solid #a00;
  border-radius: 6px;
  color: #faa;
}

.result {
  margin-top: 1rem;
  padding: 1rem;
  background: #1a2a1a;
  border: 1px solid #4a4;
  border-radius: 6px;
}

.result h4 {
  margin-top: 0;
  margin-bottom: 0.75rem;
}

.result-grid {
  display: grid;
  gap: 0.5rem;
}

.result-item {
  display: flex;
  gap: 0.5rem;
}

.result-item .label {
  font-weight: 600;
  min-width: 120px;
}

.result-item .value {
  flex: 1;
}

.result-item .value.active {
  color: #4f4;
  font-weight: 700;
}

.result-item .value.inactive {
  color: #f44;
  font-weight: 700;
}

.result-summary {
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #444;
}

.events-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.events-column h5 {
  margin-top: 0;
  margin-bottom: 0.5rem;
}

.event-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.event-item {
  padding: 0.5rem;
  border-radius: 4px;
  font-size: 0.9rem;
}

.event-item.on {
  background: #1a3a1a;
  border-left: 3px solid #4f4;
}

.event-item.off {
  background: #3a1a1a;
  border-left: 3px solid #f44;
}

.event-time {
  font-weight: 600;
}

.event-date {
  font-size: 0.85rem;
  opacity: 0.8;
}

.no-events {
  opacity: 0.6;
  font-style: italic;
}
</style>

