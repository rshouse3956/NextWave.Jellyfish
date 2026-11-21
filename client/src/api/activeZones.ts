import { http } from './http'

export interface ActiveZoneDto {
  zoneName: string
  sourceType: string
  sourceId: string
  priority: number
  folderName: string
  patternName: string
  activatedAtUtc: string
  expectedEndUtc?: string | null
}

export async function getActiveZones() {
  const { data } = await http.get<ActiveZoneDto[]>('/api/active-zones')
  return data
}

export async function stopActiveZone(zoneName: string) {
  const { data } = await http.post(`/api/active-zones/${encodeURIComponent(zoneName)}/stop`, {})
  return data
}

