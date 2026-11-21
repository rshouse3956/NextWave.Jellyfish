import { http } from './http'

export interface AuditLogDto {
  id: string
  timestamp: string
  actionType: string
  sourceType: string
  sourceId: string
  folderName: string
  patternName: string
  zoneNames: string[]
  state: number | null
  success: boolean
  errorMessage: string
  requestPayload: string
  responsePayload: string
  durationMs: number
}

export interface AuditLogStatsDto {
  totalCount: number
  todayCount: number
  successRate: number
  actionTypeCounts: Record<string, number>
  recentFailures: AuditLogDto[]
}

export interface AuditLogFilters {
  actionType?: string
  sourceType?: string
  startDateUtc?: string
  endDateUtc?: string
  skip?: number
  take?: number
}

export async function getAuditLogs(filters?: AuditLogFilters) {
  const params = new URLSearchParams()
  
  if (filters?.actionType) params.append('actionType', filters.actionType)
  if (filters?.sourceType) params.append('sourceType', filters.sourceType)
  if (filters?.startDateUtc) params.append('startDateUtc', filters.startDateUtc)
  if (filters?.endDateUtc) params.append('endDateUtc', filters.endDateUtc)
  if (filters?.skip !== undefined) params.append('skip', filters.skip.toString())
  if (filters?.take !== undefined) params.append('take', filters.take.toString())

  const queryString = params.toString()
  const url = queryString ? `/api/audit-logs/?${queryString}` : '/api/audit-logs/'
  
  const { data } = await http.get<AuditLogDto[]>(url)
  return data
}

export async function getAuditLogStats(tzOffsetMinutes?: number) {
  const url = tzOffsetMinutes !== undefined
    ? `/api/audit-logs/stats?tzOffsetMinutes=${tzOffsetMinutes}`
    : '/api/audit-logs/stats'
  const { data } = await http.get<AuditLogStatsDto>(url)
  return data
}

export async function getAuditLog(id: string) {
  const { data } = await http.get<AuditLogDto>(`/api/audit-logs/${encodeURIComponent(id)}`)
  return data
}

