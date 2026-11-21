import { isAxiosError } from 'axios'
import { http } from './http'
import type { PatternDetailsDto } from './patterns'

export interface PatternAssistantStatusDto {
  enabled: boolean
  model: string
  reachable: boolean
  modelInstalled: boolean
  ready: boolean
  message?: string | null
}

export interface GeneratedPatternDto {
  suggestedName: string
  explanation: string
  details: PatternDetailsDto
  /** Jellyfish animation name, e.g. "Sequence with Fade" or "Chase + Twinkle". */
  animation: string
}

export interface GeneratePatternRequest {
  prompt: string
  currentDetails?: PatternDetailsDto | null
  currentName?: string | null
  history?: string[]
}

/** Model replies can take 20+ seconds on CPU, well past the shared client's 10 s timeout. */
const GENERATE_TIMEOUT_MS = 180_000

export async function getPatternAssistantStatus() {
  const { data } = await http.get<PatternAssistantStatusDto>('/api/pattern-assistant/status', { timeout: 8000 })
  return data
}

export async function generatePattern(body: GeneratePatternRequest, signal?: AbortSignal) {
  const { data } = await http.post<GeneratedPatternDto>('/api/pattern-assistant/generate', body, {
    timeout: GENERATE_TIMEOUT_MS,
    signal,
  })
  return data
}

/** A readable message from an API error: { error } (400/404/409) or ProblemDetails { detail } (500). */
export function apiErrorMessage(e: unknown, fallback = 'Request failed'): string {
  if (isAxiosError(e)) {
    if (e.code === 'ECONNABORTED') return 'The request timed out'
    const d: any = e.response?.data
    if (d && typeof d === 'object') return d.error || d.detail || d.title || fallback
    if (typeof d === 'string' && d.trim()) return d
    return e.message || fallback
  }
  return (e as any)?.message ?? fallback
}
