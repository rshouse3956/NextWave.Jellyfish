import { http } from './http'

export interface PatternRunDataDto {
  speed: number
  brightness: number
  effect: string
  effectValue: number
  rgbAdj: number[]
}

export interface PatternDetailsDto {
  colors: number[]
  spaceBetweenPixels: number
  effectBetweenPixels: string
  /** Undocumented controller field (e.g. {"0":[],"1":[0]}), kept as synced; mostly on Paint patterns. */
  ledOnPos?: Record<string, number[]> | null
  colorPos?: number[] | null
  soffitZone?: string | null
  cursor?: number | null
  type: string
  skip: number
  numOfLeds: number
  runData: PatternRunDataDto
  direction: string
  /** Only on the "Firework" type (saved from the Jellyfish app); not in the controller API doc. */
  fireworkSettings?: FireworkSettingsDto | null
}

export interface FireworkSettingsDto {
  spawnRate: number
  colorPerLed: number
  fireworkSpeed: number
}

export interface PatternDto {
  folderName: string
  name: string
  isReadOnly: boolean
  createDate: string
  lastModifiedDate: string
  details?: PatternDetailsDto | null
  syncRequired?: boolean
}

export async function getPatternsByFolder(folderName: string) {
  const { data } = await http.get<PatternDto[]>(`/api/folders/${encodeURIComponent(folderName)}/patterns/`)
  return data
}

export async function getPattern(folderName: string, name: string) {
  const { data } = await http.get<PatternDto>(`/api/folders/${encodeURIComponent(folderName)}/patterns/${encodeURIComponent(name)}`)
  return data
}

export async function createPattern(folderName: string, body: { name: string; isReadOnly: boolean }) {
  const { data } = await http.post<PatternDto>(`/api/folders/${encodeURIComponent(folderName)}/patterns/`, body)
  return data
}

export async function updatePattern(folderName: string, name: string, body: { isReadOnly: boolean }) {
  const { data } = await http.put<PatternDto>(`/api/folders/${encodeURIComponent(folderName)}/patterns/${encodeURIComponent(name)}`, body)
  return data
}

export async function deletePattern(folderName: string, name: string) {
  const { data } = await http.delete(`/api/folders/${encodeURIComponent(folderName)}/patterns/${encodeURIComponent(name)}`)
  return data
}

export async function upsertPatternDetails(folderName: string, name: string, details: PatternDetailsDto) {
  const { data } = await http.put<PatternDto>(`/api/folders/${encodeURIComponent(folderName)}/patterns/${encodeURIComponent(name)}/details`, { folderName, name, details })
  return data
}

export async function deletePatternDetails(folderName: string, name: string) {
  const { data } = await http.delete(`/api/folders/${encodeURIComponent(folderName)}/patterns/${encodeURIComponent(name)}/details`)
  return data
}

