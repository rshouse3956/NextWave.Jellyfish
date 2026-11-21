import { http } from './http'

export type SchedulePriority = 'Low' | 'Normal' | 'High'

export interface ScheduleDto {
  name: string
  folderName: string
  patternName: string
  priority: SchedulePriority | string
  onTime: string
  onDay: string
  offTime: string
  offDay: string
  zoneNames: string[]
  createDate: string
  lastModifiedDate: string
  nextEvent: string | null
  nextEventKind: string
  isManualOverride?: boolean
}

export interface CreateScheduleCommand {
  name: string
  folderName: string
  patternName: string
  priority: SchedulePriority
  onTime: string
  onDay: string
  offTime: string
  offDay: string
  zoneNames: string[]
}

export async function getSchedules() {
  const { data } = await http.get<ScheduleDto[]>('/api/schedules')
  return data
}

export async function createSchedule(cmd: CreateScheduleCommand) {
  const { data } = await http.post('/api/schedules', cmd)
  return data
}

export async function getActiveWindows() {
  const { data } = await http.get<string[]>('/api/schedules/active-windows')
  return data
}

export interface ScheduleTimelineRangeDto {
  startHour: number
  endHour: number
}

export interface ScheduleTimelineItemDto {
  name: string
  folderName: string
  patternName: string
  priority: SchedulePriority | string
  zoneNames: string[]
  ranges: ScheduleTimelineRangeDto[]
}

export async function getTimelineToday(date?: string) {
  const { data } = await http.get<ScheduleTimelineItemDto[]>('/api/schedules/timeline-today', {
    params: date ? { date } : undefined
  })
  return data
}


export type ManualOverrideDto = {
  mode: 'On' | 'BlockOff'
  scheduleName: string
  folderName: string
  patternName: string
  setAtUtc: string
  suppressUntilUtc?: string | null
}

export async function getManualOverride() {
  const { data } = await http.get<ManualOverrideDto | null>('/api/schedules/override')
  return data
}

export async function clearManualOverride() {
  const { data } = await http.post('/api/schedules/override/clear', {})
  return data
}

export interface UpdateScheduleCommand {
  folderName: string
  patternName: string
  priority: SchedulePriority
  onTime: string
  onDay: string
  offTime: string
  offDay: string
  zoneNames: string[]
}

export async function updateSchedule(name: string, cmd: UpdateScheduleCommand) {
  const { data } = await http.put(`/api/schedules/${encodeURIComponent(name)}`, cmd)
  return data
}


export async function deactivateScheduleNow(name: string) {
  const { data } = await http.post(`/api/schedules/${encodeURIComponent(name)}/deactivate-now`, {})
  return data
}

export async function deleteSchedule(name: string) {
  const { data } = await http.delete(`/api/schedules/${encodeURIComponent(name)}`)
  return data
}

// Diagnostic endpoints
export interface IsActiveDebugRequest {
  onTime: string
  onDay: string
  offTime: string
  offDay: string
  nowUtc?: string | null
}

export interface IsActiveDebugResponse {
  isActive: boolean
  evaluatedAt: string
  onTime: string
  onDay: string
  offTime: string
  offDay: string
}

export interface MultiDayEventsDebugRequest {
  onTime: string
  onDay: string
  offTime: string
  offDay: string
  fromUtc?: string | null
  toUtc?: string | null
}

export interface ScheduleEvent {
  eventUtc: string
  kind: string
  localDate: string
}

export interface MultiDayEventsDebugResponse {
  events: ScheduleEvent[]
  fromUtc: string
  toUtc: string
  onTime: string
  onDay: string
  offTime: string
  offDay: string
  eventCount: number
}

export async function debugIsActive(req: IsActiveDebugRequest) {
  const { data } = await http.post<IsActiveDebugResponse>('/api/schedules/debug/is-active', req)
  return data
}

export async function debugMultiDayEvents(req: MultiDayEventsDebugRequest) {
  const { data } = await http.post<MultiDayEventsDebugResponse>('/api/schedules/debug/multi-day-events', req)
  return data
}
