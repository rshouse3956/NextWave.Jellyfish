import { http } from './http'

export interface ZoneMapEntryDto {
  port: number
  startIdx: number
  endIdx: number
  rgbStartIdx: number
}

export interface ZoneDto {
  name: string
  ctlrName: string
  isReadOnly: boolean
  createDate: string
  lastModifiedDate: string
  zoneMap: ZoneMapEntryDto[]
  syncRequired?: boolean
}

export interface CreateZoneCommand {
  name: string
  ctlrName: string
  isReadOnly: boolean
  zoneMap: ZoneMapEntryDto[]
}

export interface UpdateZoneCommand {
  ctlrName: string
  isReadOnly: boolean
  zoneMap: ZoneMapEntryDto[]
}

export async function getZones() {
  const { data } = await http.get<ZoneDto[]>('/api/zones/')
  return data
}

export async function createZone(cmd: CreateZoneCommand) {
  const { data } = await http.post('/api/zones/', cmd)
  return data
}

export async function updateZone(name: string, cmd: UpdateZoneCommand) {
  const { data } = await http.put(`/api/zones/${encodeURIComponent(name)}`, cmd)
  return data
}

export async function deleteZone(name: string) {
  const { data } = await http.delete(`/api/zones/${encodeURIComponent(name)}`)
  return data
}

