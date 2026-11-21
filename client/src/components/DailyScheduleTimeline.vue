<script setup lang="ts">
import { onMounted, onUnmounted, ref, computed } from 'vue'
import { getTimelineToday, type ScheduleTimelineItemDto } from '../api/schedules'

const loading = ref(false)
const error = ref('')
const items = ref<ScheduleTimelineItemDto[]>([])
const currentTime = ref(new Date())


const selectedDate = ref<string>('')




async function load() {
  loading.value = true
  error.value = ''
  try {
    const dateParam = selectedDate.value && selectedDate.value.trim() !== '' ? selectedDate.value : undefined
    const data = await getTimelineToday(dateParam)
    items.value = data
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to load schedule timeline'
  } finally {
    loading.value = false
  }
}



// List from server already includes only schedules active today
const todaySchedules = computed(() => {
  const list = items.value.slice()
  const priorityOrder: Record<string, number> = { High: 3, Normal: 2, Low: 1 }
  list.sort((a, b) => {
    const aPri = priorityOrder[(a.priority as string)] || 2
    const bPri = priorityOrder[(b.priority as string)] || 2
    return bPri - aPri
  })
  return list
})

// Format hour for display
function formatTimeLabel(hour: number): string {
  if (hour === 0 || hour === 24) return '12 AM'
  if (hour === 12) return '12 PM'
  if (hour < 12) return `${hour} AM`
  return `${hour - 12} PM`
}

// Format hour for hover text (with 2 decimal precision for sunrise/sunset)
function formatHoverTime(hour: number): string {
  const roundedHour = Math.round(hour * 100) / 100 // Round to 2 decimal places
  if (roundedHour === 0 || roundedHour === 24) return '12:00 AM'
  if (roundedHour === 12) return '12:00 PM'

  const h = Math.floor(roundedHour)
  const m = Math.round((roundedHour - h) * 60)
  const displayH = h > 12 ? h - 12 : (h === 0 ? 12 : h)
  const ampm = h < 12 ? 'AM' : 'PM'

  return `${displayH}:${m.toString().padStart(2, '0')} ${ampm}`
}

// Get current time as decimal hour
const currentHour = computed(() => {
  const now = currentTime.value
  return now.getHours() + now.getMinutes() / 60
})

// Update current time every minute
let timeInterval: number | null = null

const handleImmediate = () => { load() }
let es: EventSource | null = null

onMounted(() => {
  load()

  // Update current time every minute
  timeInterval = window.setInterval(() => {
    currentTime.value = new Date()
  }, 60000)

  // Listen for state changes
  window.addEventListener('jellyfish:state-changed', handleImmediate)
  try {
    es = new EventSource('/api/events/state')
    es.onmessage = () => handleImmediate()
  } catch {}
})

onUnmounted(() => {
  if (timeInterval) clearInterval(timeInterval)
  window.removeEventListener('jellyfish:state-changed', handleImmediate)
  if (es) { try { es.close() } catch {} es = null }
})
</script>

<template>
  <div class="panel">
    <h2 class="section-title">
      <span>Today's Schedule Timeline</span>
      <div class="actions" style="display:flex; gap:.5rem; align-items:center;">
        <input type="date" v-model="selectedDate" @change="load" />
        <button v-if="selectedDate" class="icon" @click="selectedDate=''; load()" title="Clear date">✖</button>
        <button class="icon" @click="load" :disabled="loading" title="Refresh">🔄</button>
      </div>
    </h2>

    <div v-if="loading">Loading…</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <div v-else>
      <div v-if="!todaySchedules.length" class="empty">
        <em>No schedules active today.</em>
      </div>
      <div v-else class="timeline-list">
        <div v-for="item in todaySchedules" :key="item.name" class="timeline-card">
          <div class="timeline-header">
            <div class="timeline-title">
              <span class="name">{{ item.name }}</span>
              <span class="priority-badge" :data-p="item.priority">{{ item.priority }}</span>
            </div>
            <div class="pattern-info">{{ item.folderName }} / {{ item.patternName }}</div>
          </div>

          <div class="timeline-container">
            <!-- Time labels -->
            <div class="time-labels">
              <span class="time-label" style="left: 0%">{{ formatTimeLabel(0) }}</span>
              <span class="time-label" style="left: 25%">{{ formatTimeLabel(6) }}</span>
              <span class="time-label" style="left: 50%">{{ formatTimeLabel(12) }}</span>
              <span class="time-label" style="left: 75%">{{ formatTimeLabel(18) }}</span>
              <span class="time-label" style="left: 100%">{{ formatTimeLabel(24) }}</span>
            </div>

            <!-- Timeline bar -->
            <div class="timeline-bar">
              <!-- Active segments (green) -->
              <div
                v-for="(range, idx) in item.ranges"
                :key="idx"
                class="active-segment"
                :style="{
                  left: `${(range.startHour / 24) * 100}%`,
                  width: `${((range.endHour - range.startHour) / 24) * 100}%`
                }"
                :title="`Active: ${formatHoverTime(range.startHour)} - ${formatHoverTime(range.endHour)}`"
              ></div>

              <!-- Current time marker -->
              <div
                class="current-time-marker"
                :style="{ left: `${(currentHour / 24) * 100}%` }"
                :title="`Current time: ${formatHoverTime(currentHour)}`"
              ></div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.panel {
  padding: 1rem;
  border: 1px solid rgba(68, 68, 68, 0.5);
  border-radius: 8px;
  background-color: transparent;
}

.section-title {
  margin: 0 0 .5rem;
  padding-bottom: .25rem;
  border-bottom: 1px solid rgba(68, 68, 68, 0.5);
  font-size: 1.25rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: .5rem;
}

.error {
  color: #e33;
}

.empty {
  padding: 1rem 0;
  text-align: center;
  opacity: 0.7;
}

.timeline-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.timeline-card {
  border: 1px solid rgba(68, 68, 68, 0.5);
  border-radius: 6px;
  padding: 0.75rem;
  background: rgba(26, 26, 26, 0.3);
}

.timeline-header {
  margin-bottom: 0.75rem;
}

.timeline-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.25rem;
}

.name {
  font-weight: 600;
  font-size: 1rem;
}

.priority-badge {
  font-size: 0.75rem;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  border: 1px solid rgba(85, 85, 85, 0.5);
}

.priority-badge[data-p="High"] {
  background: rgba(139, 0, 0, 0.2);
  border-color: #8b0000;
  color: #dc143c;
}

.priority-badge[data-p="Normal"] {
  background: rgba(44, 62, 80, 0.2);
  border-color: #2c3e50;
  color: #5a7a9a;
}

.priority-badge[data-p="Low"] {
  background: rgba(34, 139, 34, 0.2);
  border-color: #228b22;
  color: #32cd32;
}

.pattern-info {
  font-size: 0.85rem;
  opacity: 0.7;
}

.timeline-container {
  position: relative;
}

.time-labels {
  position: relative;
  height: 20px;
  margin-bottom: 0.25rem;
}

.time-label {
  position: absolute;
  font-size: 0.7rem;
  opacity: 0.6;
  transform: translateX(-50%);
  white-space: nowrap;
}

.timeline-bar {
  position: relative;
  height: 32px;
  background: rgba(10, 10, 10, 0.8);
  border: 1px solid rgba(51, 51, 51, 0.5);
  border-radius: 4px;
  overflow: hidden;
}

.active-segment {
  position: absolute;
  top: 0;
  height: 100%;
  background: #22c55e;
  opacity: 0.8;
  transition: opacity 0.2s;
}

.active-segment:hover {
  opacity: 1;
}

.current-time-marker {
  position: absolute;
  top: 0;
  width: 2px;
  height: 100%;
  background: #ef4444;
  z-index: 10;
  pointer-events: none;
}

.current-time-marker::before {
  content: '';
  position: absolute;
  top: -4px;
  left: -3px;
  width: 8px;
  height: 8px;
  background: #ef4444;
  border-radius: 50%;
}

@media (max-width: 600px) {
  .time-label:nth-child(2),
  .time-label:nth-child(4) {
    display: none;
  }
}

/* Light theme overrides */
@media (prefers-color-scheme: light) {
  .panel {
    border-color: rgba(68, 68, 68, 0.3);
  }

  .section-title {
    border-bottom-color: rgba(68, 68, 68, 0.3);
  }

  .timeline-card {
    border-color: rgba(68, 68, 68, 0.3);
    background: rgba(249, 249, 249, 0.5);
  }

  .priority-badge[data-p="High"] {
    background: rgba(220, 20, 60, 0.15);
    border-color: #dc143c;
    color: #b22222;
  }

  .priority-badge[data-p="Normal"] {
    background: rgba(44, 62, 80, 0.15);
    border-color: #34495e;
    color: #2c3e50;
  }

  .priority-badge[data-p="Low"] {
    background: rgba(34, 139, 34, 0.15);
    border-color: #228b22;
    color: #228b22;
  }

  .timeline-bar {
    background: rgba(230, 230, 230, 0.8);
    border-color: rgba(51, 51, 51, 0.3);
  }

  .time-label {
    opacity: 0.8;
  }
}
</style>

