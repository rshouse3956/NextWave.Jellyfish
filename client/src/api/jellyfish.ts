import { http } from './http'

export interface ControllerFolderDto {
  name: string
  isReadOnly: boolean
}

export interface ControllerPatternListItemDto {
  folderName: string
  name: string
  isReadOnly: boolean
}

export interface ControllerZoneDto {
  name: string
}

export async function getFolders() {
  const { data } = await http.get<ControllerFolderDto[]>('/api/jellyfish-controller/folders')
  return data
}

export async function getPatterns() {
  const { data } = await http.get<ControllerPatternListItemDto[]>('/api/jellyfish-controller/patterns')
  return data
}

export async function getZones() {
  const { data } = await http.get<ControllerZoneDto[]>('/api/jellyfish-controller/zones')
  return data
}

export async function runPattern(folderName: string, name: string, state: 0|1, zoneNames: string[], data: any = null, scheduleName?: string) {
  const body: any = { state, zoneNames, data }
  if (scheduleName) body.scheduleName = scheduleName
  const { data: res } = await http.post(`/api/jellyfish-controller/folders/${encodeURIComponent(folderName)}/patterns/${encodeURIComponent(name)}/run`, body)
  return res
}

// Sync (Jellyfish → MongoDB)
// Syncs make one controller request (and several database calls) per pattern, so they outlast the default
// 10 s timeout. The server finishes a sync even if the browser stops waiting.
const SYNC_TIMEOUT_MS = 5 * 60 * 1000
const SYNC_ALL_DETAILS_TIMEOUT_MS = 15 * 60 * 1000

export interface SyncPatternFailure { folderName: string; name: string; error: string }
export interface SyncPatternDetailsSummary {
  total: number
  synced: number
  noDetails: string[]
  failed: SyncPatternFailure[]
}

export async function syncFolders() {
  const { data } = await http.post('/api/jellyfish-controller/sync/folders', {}, { timeout: SYNC_TIMEOUT_MS })
  return data
}

export async function syncZones() {
  const { data } = await http.post('/api/jellyfish-controller/sync/zones', {}, { timeout: SYNC_TIMEOUT_MS })
  return data
}

export async function syncPatterns() {
  const { data } = await http.post('/api/jellyfish-controller/sync/patterns', {}, { timeout: SYNC_TIMEOUT_MS })
  return data
}

export async function syncPatternDetail(folderName: string, name: string) {
  const { data } = await http.post(`/api/jellyfish-controller/sync/patterns/${encodeURIComponent(folderName)}/${encodeURIComponent(name)}`, {}, { timeout: SYNC_TIMEOUT_MS })
  return data
}

export async function syncAllPatternDetails(): Promise<SyncPatternDetailsSummary> {
  const { data } = await http.post('/api/jellyfish-controller/sync/patterns/details', {}, { timeout: SYNC_ALL_DETAILS_TIMEOUT_MS })
  return data
}
