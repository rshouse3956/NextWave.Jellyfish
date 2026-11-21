import { http } from './http'

export interface FolderDto {
  name: string
  createDate: string
  lastModifiedDate: string
  isReadOnly: boolean
  syncRequired?: boolean
}

export interface CreateFolderCommand {
  name: string
  isReadOnly: boolean
}

export interface UpdateFolderCommand {
  isReadOnly: boolean
}

export async function getFolders() {
  const { data } = await http.get<FolderDto[]>('/api/folders/')
  return data
}

export async function createFolder(cmd: CreateFolderCommand) {
  const { data } = await http.post('/api/folders/', cmd)
  return data
}

export async function updateFolder(name: string, cmd: UpdateFolderCommand) {
  const { data } = await http.put(`/api/folders/${encodeURIComponent(name)}`, cmd)
  return data
}

export async function deleteFolder(name: string) {
  const { data } = await http.delete(`/api/folders/${encodeURIComponent(name)}`)
  return data
}

