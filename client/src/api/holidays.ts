import { http } from './http'

export interface HolidayDto {
  name: string
  category: 'US Federal' | 'State' | 'Special' | string
  cronExpression: string
  comment?: string
  isEnabled: boolean
  createDate: string
  lastModifiedDate: string
}

export interface CreateHolidayCommand {
  name: string
  category: HolidayDto['category']
  cronExpression: string
  comment?: string
  isEnabled: boolean
}

export interface UpdateHolidayCommand {
  category: HolidayDto['category']
  cronExpression: string
  comment?: string
  isEnabled: boolean
}

export async function getHolidays() {
  const { data } = await http.get<HolidayDto[]>('/api/holidays/')
  return data
}

export async function createHoliday(cmd: CreateHolidayCommand) {
  const { data } = await http.post('/api/holidays/', cmd)
  return data
}

export async function updateHoliday(name: string, cmd: UpdateHolidayCommand) {
  const { data } = await http.put(`/api/holidays/${encodeURIComponent(name)}`, cmd)
  return data
}

export async function deleteHoliday(name: string) {
  const { data } = await http.delete(`/api/holidays/${encodeURIComponent(name)}`)
  return data
}
