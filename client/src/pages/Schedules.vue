<script setup lang="ts">
import { onMounted, onUnmounted, onActivated, ref, computed, watch } from 'vue'
import { getSchedules, type ScheduleDto, createSchedule, type CreateScheduleCommand, getActiveWindows, deactivateScheduleNow, updateSchedule, type UpdateScheduleCommand, deleteSchedule, getManualOverride, clearManualOverride, type ManualOverrideDto } from '../api/schedules'
import { getActiveZones, type ActiveZoneDto } from '../api/activeZones'
import { getZones as getZonesApp } from '../api/zones'
import { getFolders as getFoldersApp, type FolderDto } from '../api/folders'
import { getPatternsByFolder } from '../api/patterns'

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

import { getHolidays, type HolidayDto } from '../api/holidays'
import { runPatternWithSyncCheck } from '../utils/runPatternHelper'

const loading = ref(false)
const error = ref('')
const schedules = ref<ScheduleDto[]>([])
const zones = ref<string[]>([])

// Active indicator state
const activeZones = ref<ActiveZoneDto[]>([])
const inWindows = ref<Set<string>>(new Set())

// Manual override state
const overrideState = ref<ManualOverrideDto | null>(null)
async function loadOverride() {
  try { overrideState.value = await getManualOverride() } catch {}
}
const isBlockOff = computed(() => overrideState.value?.mode === 'BlockOff')
async function removeBlock() {
  try {
    await clearManualOverride()
    window.dispatchEvent(new Event('jellyfish:state-changed'))
    await Promise.all([load(), loadOverride()])
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to remove block'
  }
}

function isLit(s: ScheduleDto) {
  // When manual override is ON, only the originating schedule is considered lit
  if (overrideState.value?.mode === 'On' && overrideState.value?.scheduleName) {
    return s.name === overrideState.value.scheduleName
  }
  // Lit if any of its zones are currently active with the same folder/pattern
  const zones = (s as any).zoneNames as string[] | undefined
  if (!zones || zones.length === 0) return false
  return activeZones.value.some(az => az.folderName === s.folderName && az.patternName === s.patternName && zones.includes(az.zoneName))
}

function isPreempted(s: ScheduleDto) {
  return inWindows.value.has(s.name) && !isLit(s)
}

function preemptorsFor(s: ScheduleDto): { manual: boolean; names: string[] } {
  const zs = (s as any).zoneNames as string[] | undefined
  if (!zs?.length) return { manual: false, names: [] }
  const overlapping = activeZones.value.filter(az => zs.includes(az.zoneName) && !(az.folderName === s.folderName && az.patternName === s.patternName))
  const manual = overlapping.some(az => (az.sourceType || '').toLowerCase() === 'manual')
  const names = Array.from(new Set(overlapping
    .map(az => ((az.sourceType || '').toLowerCase() === 'manual' ? 'Manual' : (az.sourceId || '')))
    .map(x => (x || '').trim())
    .filter(Boolean)))
    .filter(x => x !== 'Manual')
  return { manual, names }
}

function tooltip(s: ScheduleDto) {
  if (isLit(s)) return 'Currently active'
  if (isPreempted(s)) {
    const parts: string[] = []
    // Prefer explicit manual override state
    if (overrideState.value?.mode === 'On') {
      parts.push('In window (preempted by manual override)')
    } else {
      const p = preemptorsFor(s)
      if (p.manual) parts.push('In window (preempted by manual override)')
      else if (p.names.length) parts.push(`In window (preempted by ${p.names.join(', ')})`)
      else parts.push('In window (preempted)')
    }
    if ((s.nextEventKind || '').toLowerCase() === 'off' && s.nextEvent) {
      parts.push(`Off until ${fmt(s.nextEvent)}`)
    }
    return parts.join('\n')
  }
  return 'Outside window'
}

async function loadActiveZones() {
  try { activeZones.value = await getActiveZones() } catch { /* ignore */ }
}

async function loadActiveWindows() {
  try {
    const names = await getActiveWindows()
    inWindows.value = new Set(names ?? [])
  } catch { /* ignore */ }
}

const handleImmediate = async () => { await loadActiveZones(); await loadActiveWindows(); await loadOverride() }


// Add form state
const adding = ref(false)
const folders = ref<FolderDto[]>([])
const patterns = ref<PatternListItem[]>([])
const form = ref<CreateScheduleCommand>({
  name: '',

  folderName: '',
  patternName: '',
  priority: 'Normal',
  onTime: '19:00',
  onDay: '* * *',
  offTime: '23:00',
  offDay: '* * *',
  zoneNames: [],
})

// Time macro modes for creation
const onTimeMode = ref<'time'|'sunrise'|'sunset'>(['sunrise','sunset'].includes(form.value.onTime.toLowerCase() as any) ? (form.value.onTime.toLowerCase() as any) : 'time')
const offTimeMode = ref<'time'|'sunrise'|'sunset'>(['sunrise','sunset'].includes(form.value.offTime.toLowerCase() as any) ? (form.value.offTime.toLowerCase() as any) : 'time')

watch(onTimeMode, (m) => {
  if (m === 'sunrise') form.value.onTime = 'Sunrise'
  else if (m === 'sunset') form.value.onTime = 'Sunset'
  else if (m === 'time') {
    // ensure we have a HH:mm when switching back
    if (!/^\d{2}:\d{2}$/.test(form.value.onTime)) form.value.onTime = '19:00'
  }
})
watch(offTimeMode, (m) => {
  if (m === 'sunrise') form.value.offTime = 'Sunrise'
  else if (m === 'sunset') form.value.offTime = 'Sunset'
  else if (m === 'time') {
    if (!/^\d{2}:\d{2}$/.test(form.value.offTime)) form.value.offTime = '23:00'
  }
})

const filteredPatterns = computed(() => patterns.value.filter(p => !form.value.folderName || p.folderName === form.value.folderName))
watch(() => form.value.folderName, () => { if (!filteredPatterns.value.some(p => p.name === form.value.patternName)) form.value.patternName = '' })

// When opening the Add form, default-select all zones
watch(adding, async (v) => {
  if (v) {
    if (!zones.value.length) {
      try { const z = await getZonesApp(); zones.value = z.map(zz => zz.name) } catch {}
    }
    form.value.zoneNames = zones.value.slice()
  }
})

// Uniqueness guard for new schedule name (case-insensitive)
const nameExists = computed(() => {
  const n = (form.value.name || '').trim().toLowerCase()
  if (!n) return false
  return schedules.value.some(s => (s.name || '').trim().toLowerCase() === n)
})

// Holidays support for Create
const holidays = ref<HolidayDto[]>([])
const selectedHoliday = ref<string>('')
const enabledHolidays = computed(() => holidays.value.filter(h => h.isEnabled))

function cronToDayExpr(expr: string) {
  const parts = expr.trim().split(/\s+/)
  if (parts.length >= 6) {
    const dom = parts[3], mon = parts[4], dow = parts[5]
    return `${dom} ${mon} ${dow}`
  } else if (parts.length >= 5) {
    let dom = (parts[2] || '*').trim()
    let mon = (parts[3] || '*').trim()
    let dow = (parts[4] || '*').trim()
    if (dom === '?') dom = '*'
    if (dow === '?') dow = '*'
    return `${dom} ${mon} ${dow}`
  }
  return '* * *'
}

// Day selection modes for Create
const dayMode = ref<'calendar'|'date'|'custom'>('date')
const rangeStart = ref<string>('') // YYYY-MM-DD
const rangeEnd = ref<string>('')
const onDate = ref<string>('')
const offDate = ref<string>('')
const onPickerMode = ref<'date'|'holiday'>('date')
const offPickerMode = ref<'date'|'holiday'>('date')


function dateToDayExpr(dStr: string) {
  if (!dStr) return '* * *'
  // dStr: YYYY-MM-DD
  const m = dStr.match(/^(\d{4})-(\d{2})-(\d{2})$/)
  if (!m) return '* * *'
  const day = parseInt(m[3], 10)
  const mon = parseInt(m[2], 10)
  return `${day} ${mon} *`
}


function plusOneDateStr(dStr: string) {
  const m = dStr?.match(/^(\d{4})-(\d{2})-(\d{2})$/)
  if (!m) return dStr
  const y = parseInt(m[1], 10), mon = parseInt(m[2], 10), d = parseInt(m[3], 10)
  const dt = new Date(Date.UTC(y, mon - 1, d))
  dt.setUTCDate(dt.getUTCDate() + 1)
  const yyyy = dt.getUTCFullYear()
  const mm = String(dt.getUTCMonth() + 1).padStart(2, '0')
  const dd = String(dt.getUTCDate()).padStart(2, '0')
  return `${yyyy}-${mm}-${dd}`
}




// Add 1 day to a DOM MON * expression, with month wrap
function plusOneDayExpr(expr: string) {
  const m = expr.match(/^\s*([1-9]|[12]\d|3[01])\s+([1-9]|1[0-2])\s+\*\s*$/)
  if (!m) return expr
  const d = parseInt(m[1], 10)
  const mon = parseInt(m[2], 10)
  // Use a fixed year for roll-over logic; 2024 is leap year-safe
  const dt = new Date(Date.UTC(2024, mon - 1, d))
  dt.setUTCDate(dt.getUTCDate() + 1)
  const newD = dt.getUTCDate()
  const newM = dt.getUTCMonth() + 1
  return `${newD} ${newM} *`
}

// Holiday selection affects on/off day when in date/custom modes and picker is Holiday
watch(selectedHoliday, (name) => {
  if (!name) return
  if (dayMode.value === 'calendar') return
  // In 'date' mode, only act when On picker is Holiday; in 'custom' mode, always allow
  if (dayMode.value === 'date' && onPickerMode.value !== 'holiday') return
  const h = holidays.value.find(x => x.name === name)
  if (h) {
    const day = cronToDayExpr(h.cronExpression)
    form.value.onDay = day
    // If On=Sunset and Off=Sunrise across a holiday, set OffDay to Holiday+1
    if (onTimeMode.value === 'sunset' && offTimeMode.value === 'sunrise') {
      if (!selectedHolidayOff.value) selectedHolidayOff.value = name
      form.value.offDay = plusOneDayExpr(day)
    }
  }
})

const selectedHolidayOff = ref<string>('')
watch(selectedHolidayOff, (name) => {
  if (!name) return
  if (dayMode.value === 'calendar') return
  // In 'date' mode, only act when Off picker is Holiday; in 'custom' mode, always allow
  if (dayMode.value === 'date' && offPickerMode.value !== 'holiday') return
  const h = holidays.value.find(x => x.name === name)
  if (h) {
    const day = cronToDayExpr(h.cronExpression)
    if (onTimeMode.value === 'sunset' && offTimeMode.value === 'sunrise') {
      form.value.offDay = plusOneDayExpr(day)
    } else {
      form.value.offDay = day
    }
  }
})
// Re-apply off-day +1 when toggling time modes after picking a Holiday
watch([onTimeMode, offTimeMode, dayMode, onPickerMode, offPickerMode, selectedHoliday, selectedHolidayOff], () => {
  if (dayMode.value === 'calendar') return
  const holidayName = selectedHoliday.value
  if (!holidayName) return
  const h = holidays.value.find(x => x.name === holidayName)
  if (!h) return
  const day = cronToDayExpr(h.cronExpression)
  if (onTimeMode.value === 'sunset' && offTimeMode.value === 'sunrise') {
    // In date mode, only shift if Off picker is Holiday or not explicitly set; in custom, always okay
    if (dayMode.value === 'date' && offPickerMode.value !== 'holiday') return
    if (!selectedHolidayOff.value || selectedHolidayOff.value === holidayName) {
      if (!selectedHolidayOff.value) selectedHolidayOff.value = holidayName
      form.value.offDay = plusOneDayExpr(day)
    }
  }
})


// Calendar mode: keep form.onDay/offDay synced to date range
watch([dayMode, rangeStart, rangeEnd], () => {
  if (dayMode.value !== 'calendar') return
  if (rangeStart.value) form.value.onDay = dateToDayExpr(rangeStart.value)
  if (rangeEnd.value) form.value.offDay = dateToDayExpr(rangeEnd.value)
})

// Specific Date mode: keep form.onDay/offDay synced to chosen dates
watch([dayMode, onPickerMode, onDate], () => {
  if (dayMode.value !== 'date') return
  if (onPickerMode.value === 'date') {
    form.value.onDay = dateToDayExpr(onDate.value)
    if (onTimeMode.value === 'sunset' && offTimeMode.value === 'sunrise' && offPickerMode.value === 'date') {
      const target = onDate.value ? plusOneDateStr(onDate.value) : ''
      if (!offDate.value || offDate.value === onDate.value) {
        offDate.value = target
      }
      if (target) form.value.offDay = dateToDayExpr(target)
    }
  }
})
watch([dayMode, offPickerMode, offDate], () => {
  if (dayMode.value !== 'date') return
  if (offPickerMode.value === 'date') form.value.offDay = dateToDayExpr(offDate.value)
})



// Edit form state
const editing = ref(false)
const editName = ref('')
const editForm = ref<UpdateScheduleCommand>({
  folderName: '',
  patternName: '',
  priority: 'Normal',
  onTime: '19:00',
  onDay: '* * *',
  offTime: '23:00',
  offDay: '* * *',
  zoneNames: [],
})
const editOnTimeMode = ref<'time'|'sunrise'|'sunset'>('time')
const editOffTimeMode = ref<'time'|'sunrise'|'sunset'>('time')

const editFilteredPatterns = computed(() => patterns.value.filter(p => !editForm.value.folderName || p.folderName === editForm.value.folderName))
watch(() => editForm.value.folderName, () => { if (!editFilteredPatterns.value.some(p => p.name === editForm.value.patternName)) editForm.value.patternName = '' })

watch(editOnTimeMode, (m) => {
  if (m === 'sunrise') editForm.value.onTime = 'Sunrise'
  else if (m === 'sunset') editForm.value.onTime = 'Sunset'
  else if (m === 'time') {
    if (!/^\d{2}:\d{2}$/.test(editForm.value.onTime)) editForm.value.onTime = '19:00'
  }
})

// Edit Day selection state
const editDayMode = ref<'calendar'|'date'|'custom'>('custom')
const editRangeStart = ref<string>('')
const editRangeEnd = ref<string>('')
const editOnDate = ref<string>('')
const editOffDate = ref<string>('')
const editOnPickerMode = ref<'date'|'holiday'>('date')
const editOffPickerMode = ref<'date'|'holiday'>('date')

// Holiday pickers for Edit
const selectedEditHolidayOn = ref<string>('')
const selectedEditHolidayOff = ref<string>('')
watch(selectedEditHolidayOn, (name) => {
  if (!name) return
  if (editDayMode.value === 'calendar') return
  // In 'date' mode, only act when On picker is Holiday; in 'custom' mode, always allow
  if (editDayMode.value === 'date' && editOnPickerMode.value !== 'holiday') return
  const h = holidays.value.find(x => x.name === name)
  if (h) {
    const day = cronToDayExpr(h.cronExpression)
    editForm.value.onDay = day
    if (editOnTimeMode.value === 'sunset' && editOffTimeMode.value === 'sunrise') {
      if (!selectedEditHolidayOff.value) selectedEditHolidayOff.value = name
      editForm.value.offDay = plusOneDayExpr(day)
    }
  }
})
watch(selectedEditHolidayOff, (name) => {
  if (!name) return
  if (editDayMode.value === 'calendar') return
  // In 'date' mode, only act when Off picker is Holiday; in 'custom' mode, always allow
  if (editDayMode.value === 'date' && editOffPickerMode.value !== 'holiday') return
  const h = holidays.value.find(x => x.name === name)
  if (h) {
    const day = cronToDayExpr(h.cronExpression)
    if (editOnTimeMode.value === 'sunset' && editOffTimeMode.value === 'sunrise') {
      editForm.value.offDay = plusOneDayExpr(day)
    } else {
      editForm.value.offDay = day
    }
  }
})

// Re-apply off-day +1 when toggling time modes after picking a Holiday (Edit)
watch([editOnTimeMode, editOffTimeMode, editDayMode, editOnPickerMode, editOffPickerMode, selectedEditHolidayOn, selectedEditHolidayOff], () => {
  if (editDayMode.value === 'calendar') return
  const holidayName = selectedEditHolidayOn.value
  if (!holidayName) return
  const h = holidays.value.find(x => x.name === holidayName)
  if (!h) return
  const day = cronToDayExpr(h.cronExpression)
  if (editOnTimeMode.value === 'sunset' && editOffTimeMode.value === 'sunrise') {
    if (editDayMode.value === 'date' && editOffPickerMode.value !== 'holiday') return
    if (!selectedEditHolidayOff.value || selectedEditHolidayOff.value === holidayName) {
      if (!selectedEditHolidayOff.value) selectedEditHolidayOff.value = holidayName
      editForm.value.offDay = plusOneDayExpr(day)
    }
  }
})

// Calendar mode (Edit): sync on/off day from date range
watch([editDayMode, editRangeStart, editRangeEnd], () => {
  if (editDayMode.value !== 'calendar') return
  if (editRangeStart.value) editForm.value.onDay = dateToDayExpr(editRangeStart.value)
  if (editRangeEnd.value) editForm.value.offDay = dateToDayExpr(editRangeEnd.value)
})

// Specific Date mode (Edit): sync from chosen dates
watch([editDayMode, editOnPickerMode, editOnDate], () => {
  if (editDayMode.value !== 'date') return
  if (editOnPickerMode.value === 'date') {
    editForm.value.onDay = dateToDayExpr(editOnDate.value)
    if (editOnTimeMode.value === 'sunset' && editOffTimeMode.value === 'sunrise' && editOffPickerMode.value === 'date') {
      const target = editOnDate.value ? plusOneDateStr(editOnDate.value) : ''
      if (!editOffDate.value || editOffDate.value === editOnDate.value) {
        editOffDate.value = target
      }
      if (target) editForm.value.offDay = dateToDayExpr(target)
    }
  }
})
watch([editDayMode, editOffPickerMode, editOffDate], () => {
  if (editDayMode.value !== 'date') return
  if (editOffPickerMode.value === 'date') editForm.value.offDay = dateToDayExpr(editOffDate.value)
})

// Holiday pickers for Edit

watch(editOffTimeMode, (m) => {
  if (m === 'sunrise') editForm.value.offTime = 'Sunrise'
  else if (m === 'sunset') editForm.value.offTime = 'Sunset'
  else if (m === 'time') {
    if (!/^\d{2}:\d{2}$/.test(editForm.value.offTime)) editForm.value.offTime = '23:00'
  }
})

async function startEdit(s: ScheduleDto) {
  error.value = ''
  try {
    // Ensure lookups are loaded
    if (!folders.value.length) { try { folders.value = await getFoldersApp() } catch {} }
    if (!patterns.value.length) { try { patterns.value = await getAllPatternsFromApp() } catch {} }
    if (!zones.value.length) { try { const z = await getZonesApp(); zones.value = z.map(zz => zz.name) } catch {} }
    editName.value = s.name
    const existingZones = Array.isArray((s as any).zoneNames) ? ((s as any).zoneNames as string[]) : []
    const zoneSelection = existingZones.length ? existingZones : zones.value.slice()

    editForm.value = {
      folderName: s.folderName,
      patternName: s.patternName,
      priority: (s.priority as any) || 'Normal',
      onTime: s.onTime,
      onDay: s.onDay,
      offTime: s.offTime,
      offDay: s.offDay,
      zoneNames: zoneSelection,
    }
    editOnTimeMode.value = (/^sunrise$/i.test(s.onTime) ? 'sunrise' : (/^sunset$/i.test(s.onTime) ? 'sunset' : 'time'))
    editOffTimeMode.value = (/^sunrise$/i.test(s.offTime) ? 'sunrise' : (/^sunset$/i.test(s.offTime) ? 'sunset' : 'time'))
    // Default to Custom mode with existing values to avoid clobbering on/off day during edit
    editDayMode.value = 'custom'
    // Users can switch to Specific Date or Calendar if desired

    editing.value = true
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to start edit'
  }
}

function cancelEdit() {
  editing.value = false
}

async function submitEdit() {
  try {
    loading.value = true
    error.value = ''
    if (!editName.value) throw new Error('Missing schedule name')
    if (!editForm.value.folderName || !editForm.value.patternName) throw new Error('Folder and Pattern are required')
    if (!editForm.value.onTime || !editForm.value.offTime) throw new Error('On/Off time are required')
    if (!editForm.value.onDay || !editForm.value.offDay) throw new Error('On/Off day are required')

    await updateSchedule(editName.value, editForm.value)
    editing.value = false
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to update schedule'
  } finally {
    loading.value = false
  }
}

function fmt(dt: string | null) {
  if (!dt) return '—'
  try { return new Date(dt).toLocaleString() } catch { return dt }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [scheds, z, f, p, h] = await Promise.all([
      getSchedules(),
      zones.value.length ? Promise.resolve(zones.value) : getZonesApp().then(x => x.map(xx => xx.name)).catch(() => []),
      folders.value.length ? Promise.resolve(folders.value) : getFoldersApp().catch(() => []),
      patterns.value.length ? Promise.resolve(patterns.value) : getAllPatternsFromApp().catch(() => []),
      holidays.value.length ? Promise.resolve(holidays.value) : getHolidays().catch(() => []),
    ])
    schedules.value = scheds
    if (zones.value.length === 0 && z.length) zones.value = z as string[]
    if (!folders.value.length && Array.isArray(f)) folders.value = f as FolderDto[]
    if (!patterns.value.length && Array.isArray(p)) patterns.value = p as PatternListItem[]
    if (!holidays.value.length && Array.isArray(h)) holidays.value = h as HolidayDto[]
    await loadActiveZones()
    await loadActiveWindows()

  } catch (e: any) {
    error.value = e?.message ?? 'Failed to load schedules'
  } finally {
    loading.value = false
  }
}

async function activateNow(s: ScheduleDto) {
  if (!zones.value.length) {
    try { const z = await getZonesApp(); zones.value = z.map((zz: any) => zz.name) } catch {}
  }
  try {
    await runPatternWithSyncCheck(
      s.folderName,
      s.patternName,
      1,
      (s as any).zoneNames || [],
      undefined,
      s.name
    )
    window.dispatchEvent(new Event('jellyfish:state-changed'))
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to activate schedule'
  }
}

async function deactivateNow(s: ScheduleDto) {
  try {
    await deactivateScheduleNow(s.name)
    window.dispatchEvent(new Event('jellyfish:state-changed'))
    await Promise.all([loadActiveZones(), loadActiveWindows()])
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to deactivate schedule'
  }
}


const confirmOpen = ref(false)
const deleteTarget = ref<ScheduleDto | null>(null)

function askDelete(s: ScheduleDto) {
  deleteTarget.value = s
  confirmOpen.value = true
}

function cancelDelete() {
  confirmOpen.value = false
  deleteTarget.value = null
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  try {
    loading.value = true
    error.value = ''
    await deleteSchedule(deleteTarget.value.name)
    await load()
  } catch (e: any) {
    error.value = e?.message ?? 'Failed to delete schedule'
  } finally {
    loading.value = false
    confirmOpen.value = false
    deleteTarget.value = null
  }
}


async function submitNew() {
  try {
    loading.value = true
    error.value = ''
    if (!form.value.name || !form.value.folderName || !form.value.patternName) throw new Error('Name, Folder and Pattern are required')
    if (nameExists.value) throw new Error(`Schedule '${form.value.name}' already exists`)
    await createSchedule(form.value)
    adding.value = false
    form.value = { name: '', folderName: '', patternName: '', priority: 'Normal', onTime: '19:00', onDay: '* * *', offTime: '23:00', offDay: '* * *', zoneNames: zones.value.slice() }
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.error || e?.message || 'Failed to create schedule'
  } finally {
    loading.value = false
  }
}

let es: EventSource | null = null

onMounted(() => {
  load()
  handleImmediate()
  loadOverride()
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




onActivated(() => {
  handleImmediate()
});

</script>

<template>
  <div class="page">
    <div v-if="isBlockOff" class="notice" style="margin:.5rem 0; padding:.5rem; border:1px solid #555; border-radius:6px;">
      <strong>Manual Off-block active</strong>
      <span> — {{ overrideState?.scheduleName }} until {{ fmt(overrideState?.suppressUntilUtc || null) }}</span>
      <button :disabled="loading" @click="removeBlock" style="margin-left:.5rem">Remove block</button>
    </div>
    <div class="header">
      <h1>Schedules</h1>
      <div class="actions" style="padding: 1rem">
        <button class="icon" :disabled="loading" @click="load" title="Refresh">🔄</button>
        <button class="icon" :disabled="loading" @click="adding = !adding" :title="adding ? 'Close' : 'Add new'">➕</button>
      </div>
    </div>

    <div v-if="adding" class="add">
      <div class="row"><label>Name</label><input v-model.trim="form.name" placeholder="My Schedule" /></div>
      <div v-if="nameExists" class="hint error" style="color:#a00;margin:-.25rem 0 .5rem 5.5rem">A schedule with this name already exists.</div>
      <div class="row"><label>Folder</label>
        <select v-model="form.folderName">
          <option value="">Select folder</option>
          <option v-for="f in folders" :key="f.name" :value="f.name">{{ f.name }}</option>
        </select>
      </div>
      <div class="row"><label>Pattern</label>
        <select v-model="form.patternName">
          <option value="">Select pattern</option>
          <option v-for="p in filteredPatterns" :key="p.name" :value="p.name">{{ p.name }}</option>
        </select>
      </div>
      <div class="row"><label>Zones</label>
        <div class="chips">
          <label v-for="z in zones" :key="z" class="chip"><input type="checkbox" :value="z" v-model="form.zoneNames" /> {{ z }}</label>
        </div>
      </div>
      <div class="row"><label>Priority</label>
        <select v-model="form.priority">
          <option>Low</option>
          <option selected>Normal</option>
          <option>High</option>
        </select>
      </div>
      <div class="row two">
        <div>
      <div class="row"><label>Day selection</label>
        <div class="radio-row">
          <label class="cb"><input type="radio" value="calendar" v-model="dayMode" /> Calendar (range → On/Off)</label>
          <label class="cb"><input type="radio" value="date" v-model="dayMode" /> Specific Date or Holiday</label>
          <label class="cb"><input type="radio" value="custom" v-model="dayMode" /> Custom (cron)</label>
        </div>
      </div>

      <!-- Calendar mode -->
      <template v-if="dayMode === 'calendar'">
        <div class="row"><label>Date Range</label>
          <div>
            <div class="time-row">
              <input type="date" v-model="rangeStart" />
              <span>to</span>
              <input type="date" v-model="rangeEnd" />
            </div>
            <div style="opacity:.8; font-size:.85rem; margin-top:.25rem">
              On Day → {{ form.onDay }} | Off Day → {{ form.offDay }}
            </div>
          </div>
        </div>
        <div class="row"><label>On Time</label>
          <div class="time-row">
            <select v-model="onTimeMode">
              <option value="time">At time…</option>
              <option value="sunrise">Sunrise</option>
              <option value="sunset">Sunset</option>
            </select>
            <input v-if="onTimeMode === 'time'" v-model.trim="form.onTime" placeholder="HH:mm" style="min-width: 8rem" />
          </div>
        </div>
        <div class="row"><label>Off Time</label>
          <div class="time-row">
            <select v-model="offTimeMode">
              <option value="time">At time…</option>
              <option value="sunrise">Sunrise</option>
              <option value="sunset">Sunset</option>
            </select>
            <input v-if="offTimeMode === 'time'" v-model.trim="form.offTime" placeholder="HH:mm" style="min-width: 8rem" />
          </div>
        </div>
      </template>


      <!-- Specific Date mode -->
      <template v-else-if="dayMode === 'date'">
        <div class="row two">
          <div>
            <label>On Day</label>
            <div class="time-row">
              <label class="cb"><input type="radio" value="date" v-model="onPickerMode" /> Date</label>
              <label class="cb"><input type="radio" value="holiday" v-model="onPickerMode" /> Holiday</label>
            </div>
            <input v-if="onPickerMode === 'date'" type="date" v-model="onDate" />
            <select v-else v-model="selectedHoliday" style="max-width: 16rem">
              <option value="">Pick a holiday…</option>
              <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
            </select>
            <div style="opacity:.8; font-size:.85rem; margin-top:.25rem">Value → {{ form.onDay }}</div>
          </div>
          <div>
            <label>On Time</label>
            <div class="time-row">
              <select v-model="onTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="onTimeMode === 'time'" v-model.trim="form.onTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
        <div class="row two">
          <div>
            <label>Off Day</label>
            <div class="time-row">
              <label class="cb"><input type="radio" value="date" v-model="offPickerMode" /> Date</label>
              <label class="cb"><input type="radio" value="holiday" v-model="offPickerMode" /> Holiday</label>
            </div>
            <input v-if="offPickerMode === 'date'" type="date" v-model="offDate" />
            <select v-else v-model="selectedHolidayOff" style="max-width: 16rem">
              <option value="">Pick a holiday…</option>
              <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
            </select>
            <div style="opacity:.8; font-size:.85rem; margin-top:.25rem">Value → {{ form.offDay }}</div>
          </div>
          <div>
            <label>Off Time</label>
            <div class="time-row">
              <select v-model="offTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="offTimeMode === 'time'" v-model.trim="form.offTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
      </template>

      <!-- Custom mode -->
      <template v-else>
        <div class="row two">
          <div>
            <label>On Day</label>
            <input v-model.trim="form.onDay" placeholder="* * * (DOM MON DOW)" />
            <div style="margin-top:.25rem; display:flex; gap:.5rem; align-items:center">
              <label style="font-weight:normal; opacity:.8; margin:0">or Holiday</label>
              <select v-model="selectedHoliday" style="max-width: 16rem">
                <option value="">Pick a holiday…</option>
                <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
              </select>
            </div>
          </div>
          <div>
            <label>On Time</label>
            <div class="time-row">
              <select v-model="onTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="onTimeMode === 'time'" v-model.trim="form.onTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
        <div class="row two">
          <div>
            <label>Off Day</label>
            <input v-model.trim="form.offDay" placeholder="* * * (DOM MON DOW)" />
            <div style="margin-top:.25rem; display:flex; gap:.5rem; align-items:center">
              <label style="font-weight:normal; opacity:.8; margin:0">or Holiday</label>
              <select v-model="selectedHolidayOff" style="max-width: 16rem">
                <option value="">Pick a holiday…</option>
                <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
              </select>
            </div>
          </div>
          <div>
            <label>Off Time</label>
            <div class="time-row">
              <select v-model="offTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="offTimeMode === 'time'" v-model.trim="form.offTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
      </template>
        </div>
      </div>
      <div class="add-actions">
        <button :disabled="loading || nameExists" @click="submitNew">Create</button>
        <button :disabled="loading" @click="adding = false">Cancel</button>
      </div>
    </div>

    <div v-if="editing" class="add">
      <div class="row"><label>Name</label><input :value="editName" disabled /></div>
      <div class="row"><label>Folder</label>
        <select v-model="editForm.folderName">
          <option value="">Select folder</option>
          <option v-for="f in folders" :key="f.name" :value="f.name">{{ f.name }}</option>
        </select>
      </div>
      <div class="row"><label>Pattern</label>
        <select v-model="editForm.patternName">
          <option value="">Select pattern</option>
          <option v-for="p in editFilteredPatterns" :key="p.name" :value="p.name">{{ p.name }}</option>
        </select>
      </div>
      <div class="row"><label>Zones</label>
        <div class="chips">
          <label v-for="z in zones" :key="z" class="chip"><input type="checkbox" :value="z" v-model="editForm.zoneNames" /> {{ z }}</label>
        </div>
      </div>
      <div class="row"><label>Priority</label>
        <select v-model="editForm.priority">
          <option>Low</option>
          <option>Normal</option>
          <option>High</option>
        </select>
      </div>
      <div class="row"><label>Day selection</label>
        <div class="radio-row">
          <label class="cb"><input type="radio" value="calendar" v-model="editDayMode" /> Calendar (range → On/Off)</label>
          <label class="cb"><input type="radio" value="date" v-model="editDayMode" /> Specific Date or Holiday</label>
          <label class="cb"><input type="radio" value="custom" v-model="editDayMode" /> Custom (cron)</label>
        </div>
      </div>

      <!-- Calendar mode (Edit) -->
      <template v-if="editDayMode === 'calendar'">
        <div class="row"><label>Date Range</label>
          <div>
            <div class="time-row">
              <input type="date" v-model="editRangeStart" />
              <span>to</span>
              <input type="date" v-model="editRangeEnd" />
            </div>
            <div style="opacity:.8; font-size:.85rem; margin-top:.25rem">
              On Day → {{ editForm.onDay }} | Off Day → {{ editForm.offDay }}
            </div>
          </div>
        </div>
        <div class="row"><label>On Time</label>
          <div class="time-row">
            <select v-model="editOnTimeMode">
              <option value="time">At time…</option>
              <option value="sunrise">Sunrise</option>
              <option value="sunset">Sunset</option>
            </select>
            <input v-if="editOnTimeMode === 'time'" v-model.trim="editForm.onTime" placeholder="HH:mm" style="min-width: 8rem" />
          </div>
        </div>
        <div class="row"><label>Off Time</label>
          <div class="time-row">
            <select v-model="editOffTimeMode">
              <option value="time">At time…</option>
              <option value="sunrise">Sunrise</option>
              <option value="sunset">Sunset</option>
            </select>
            <input v-if="editOffTimeMode === 'time'" v-model.trim="editForm.offTime" placeholder="HH:mm" style="min-width: 8rem" />
          </div>
        </div>
      </template>

      <!-- Specific Date mode (Edit) -->
      <template v-else-if="editDayMode === 'date'">
        <div class="row two">
          <div>
            <label>On Day</label>
            <div class="time-row">
              <label class="cb"><input type="radio" value="date" v-model="editOnPickerMode" /> Date</label>
              <label class="cb"><input type="radio" value="holiday" v-model="editOnPickerMode" /> Holiday</label>
            </div>
            <input v-if="editOnPickerMode === 'date'" type="date" v-model="editOnDate" />
            <select v-else v-model="selectedEditHolidayOn" style="max-width: 16rem">
              <option value="">Pick a holiday…</option>
              <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
            </select>
            <div style="opacity:.8; font-size:.85rem; margin-top:.25rem">Value → {{ editForm.onDay }}</div>
          </div>
          <div>
            <label>On Time</label>
            <div class="time-row">
              <select v-model="editOnTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="editOnTimeMode === 'time'" v-model.trim="editForm.onTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
        <div class="row two">
          <div>
            <label>Off Day</label>
            <div class="time-row">
              <label class="cb"><input type="radio" value="date" v-model="editOffPickerMode" /> Date</label>
              <label class="cb"><input type="radio" value="holiday" v-model="editOffPickerMode" /> Holiday</label>
            </div>
            <input v-if="editOffPickerMode === 'date'" type="date" v-model="editOffDate" />
            <select v-else v-model="selectedEditHolidayOff" style="max-width: 16rem">
              <option value="">Pick a holiday…</option>
              <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
            </select>
            <div style="opacity:.8; font-size:.85rem; margin-top:.25rem">Value → {{ editForm.offDay }}</div>
          </div>
          <div>
            <label>Off Time</label>
            <div class="time-row">
              <select v-model="editOffTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="editOffTimeMode === 'time'" v-model.trim="editForm.offTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
      </template>

      <!-- Custom mode (Edit) -->
      <template v-else>
        <div class="row two">
          <div>
            <label>On Day</label>
            <input v-model.trim="editForm.onDay" placeholder="* * * (DOM MON DOW)" />
            <div style="margin-top:.25rem; display:flex; gap:.5rem; align-items:center">
              <label style="font-weight:normal; opacity:.8; margin:0">or Holiday</label>
              <select v-model="selectedEditHolidayOn" style="max-width: 16rem">
                <option value="">Pick a holiday…</option>
                <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
              </select>
            </div>
          </div>
          <div>
            <label>On Time</label>
            <div class="time-row">
              <select v-model="editOnTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="editOnTimeMode === 'time'" v-model.trim="editForm.onTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
        <div class="row two">
          <div>
            <label>Off Day</label>
            <input v-model.trim="editForm.offDay" placeholder="* * * (DOM MON DOW)" />
            <div style="margin-top:.25rem; display:flex; gap:.5rem; align-items:center">
              <label style="font-weight:normal; opacity:.8; margin:0">or Holiday</label>
              <select v-model="selectedEditHolidayOff" style="max-width: 16rem">
                <option value="">Pick a holiday…</option>
                <option v-for="h in enabledHolidays" :key="h.name" :value="h.name">{{ h.name }}</option>
              </select>
            </div>
          </div>
          <div>
            <label>Off Time</label>
            <div class="time-row">
              <select v-model="editOffTimeMode">
                <option value="time">At time…</option>
                <option value="sunrise">Sunrise</option>
                <option value="sunset">Sunset</option>
              </select>
              <input v-if="editOffTimeMode === 'time'" v-model.trim="editForm.offTime" placeholder="HH:mm" style="min-width: 8rem" />
            </div>
          </div>
        </div>
      </template>
      <div class="add-actions">
        <button :disabled="loading" @click="submitEdit">Save</button>
        <button :disabled="loading" @click="cancelEdit">Cancel</button>
      </div>
    </div>


    <div v-if="loading">Loading…</div>
    <div v-else-if="error" class="error">{{ error }}</div>

    <div v-else class="cards">
      <div v-if="!schedules.length"><em>No schedules found.</em></div>
      <div v-else class="grid">
        <div v-for="s in schedules" :key="s.name" class="card">
          <div class="card-header">


            <div class="title"><span class="bulb" :title="tooltip(s)" :class="{ on: isLit(s), dim: isPreempted(s) }">{{ (isLit(s) || isPreempted(s)) ? '💡' : '💤' }}</span>{{ s.name }} <span v-if="overrideState?.mode === 'On' && overrideState?.scheduleName === s.name" class="badge override">Manual override</span></div>
            <div class="right">
              <div class="priority" :data-p="s.priority">{{ s.priority }}</div>
              <div class="header-actions">
                <button class="icon" @click="startEdit(s)" title="Edit">✏️</button>
                <button class="icon" @click="askDelete(s)" title="Delete">🗑️</button>
              </div>
            </div>
          </div>
          <div class="rows">
            <div class="row"><strong>Folder:</strong> <span>{{ s.folderName }}</span></div>
            <div class="row"><strong>Pattern:</strong> <span>{{ s.patternName }}</span></div>
            <div class="row"><strong>On:</strong> <span>{{ s.onDay }} @ {{ s.onTime }}</span></div>
            <div class="row"><strong>Off:</strong> <span>{{ s.offDay }} @ {{ s.offTime }}</span></div>
            <div class="row"><strong>Created:</strong> <span>{{ fmt(s.createDate) }}</span></div>
            <div class="row"><strong>Modified:</strong> <span>{{ fmt(s.lastModifiedDate) }}</span></div>
          </div>
          <div class="card-actions">
            <button @click="isLit(s) ? deactivateNow(s) : activateNow(s)">{{ isLit(s) ? 'Deactivate now' : 'Activate now' }}</button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="confirmOpen" class="modal-backdrop" @click.self="cancelDelete">
      <div class="modal">
        <h3 style="margin-top:0">Delete schedule</h3>
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
.add { border: 1px dashed #555; border-radius: 8px; padding: .75rem; margin-bottom: .75rem; display: grid; gap: .5rem; }
.add .row { display: grid; gap: .25rem; }
.add .row.two { grid-template-columns: 1fr 1fr; gap: .75rem; }
.add input { padding: .4rem .5rem; background: transparent; border: 1px solid #555; border-radius: 6px; color: inherit; }
.add select { padding: .4rem .5rem; background: #fff; color: #111; border: 1px solid #555; border-radius: 6px; }
.add option { color: #111; }
.time-row { display: flex; align-items: center; gap: .5rem; }
.radio-row { display: flex; gap: .75rem; flex-wrap: wrap; }
.add-actions { display: flex; gap: .5rem; justify-content: center; }
.error { color: #e33; }

.grid { display: flex; flex-wrap: wrap; gap: 1rem; align-items: stretch; }
.grid > .card { flex: 0 0 320px; width: 320px; max-width: 100%; }
@media (max-width: 700px) { .grid > .card { flex: 1 1 100%; width: 100%; } }
.card { border: 1px solid #444; border-radius: 8px; padding: .75rem; display: flex; flex-direction: column; gap: .5rem; }
.card-header { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
.title { font-weight: 600; }
.badge { display:inline-block; padding:.1rem .4rem; border-radius:999px; font-size:.75rem; margin-left:.35rem; border:1px solid #555; }
.badge.override { background:#8b000022; border-color:#8b0000; color:#f2b2b2; }
.right { display: flex; align-items: center; gap: .5rem; }
.header-actions { display: flex; gap: .25rem; }
.header-actions .icon { width: 1.75rem; height: 1.75rem; display: inline-flex; align-items: center; justify-content: center; font-size: .95rem; }
.bulb { margin-right: .35rem; }
.bulb.on { opacity: 1; }
.bulb.dim { opacity: .45; }

.priority { font-size: .85rem; padding: .15rem .5rem; border-radius: 999px; border: 1px solid #555; }
.priority[data-p="High"] { background: #8b000022; border-color: #8b0000; }
.priority[data-p="Normal"] { background: #2c3e5015; border-color: #2c3e50; }
.priority[data-p="Low"] { background: #228b2215; border-color: #228b22; }
.rows { display: grid; gap: .25rem; }
.row { display: flex; gap: .5rem; }
.card-actions { display: flex; gap: .5rem; margin-top: .25rem; }
@media (max-width: 700px) { .add .row.two { grid-template-columns: 1fr; } }
.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal { background: #1e1e1e; color: inherit; border: 1px solid #555; border-radius: 8px; padding: 1rem; max-width: 420px; width: calc(100% - 2rem); box-shadow: 0 10px 30px rgba(0,0,0,.6); }
</style>

