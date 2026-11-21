import { http } from './http'

export interface SolarTimesDto {
  sunriseUtc: string | null
  sunsetUtc: string | null
  solarNoonUtc: string
  dayLengthMinutes: number
}

export async function getSolarTimes(lat: number, lon: number, elevationMeters: number, date: string) {
  const { data } = await http.get<SolarTimesDto>('/api/solar-times', {
    params: { lat, lon, elevationMeters, date }
  })
  return data
}

