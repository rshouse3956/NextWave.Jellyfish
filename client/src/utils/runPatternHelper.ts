import { runPattern } from '../api/jellyfish'
import { getPattern } from '../api/patterns'

/**
 * Runs a pattern via controller, ensuring details are included when SyncRequired is true.
 * - For both state 1 (On) and state 0 (Off): if SyncRequired=true, details must exist; otherwise an error is thrown.
 * - If SyncRequired=false, optional preferData will be sent if provided; otherwise null is sent.
 */
export async function runPatternWithSyncCheck(
  folderName: string,
  name: string,
  state: 0 | 1,
  zoneNames: string[],
  preferData?: any | null,
  scheduleName?: string
) {
  let data: any = null

  try {
    const full = await getPattern(folderName, name)
    if (full?.syncRequired) {
      if (!full.details) {
        throw new Error(
          'This pattern requires details to run, but none are stored. Add details or sync from the controller, then try again.'
        )
      }
      data = full.details
    } else {
      // Not required; honor caller-provided data if any
      if (typeof preferData !== 'undefined') data = preferData
    }
  } catch (e: any) {
    // If fetching pattern failed and caller provided explicit data, allow it when not known to be required.
    if (typeof preferData !== 'undefined') {
      data = preferData
    } else {
      // Re-throw original error to surface to UI
      throw e
    }
  }

  return await runPattern(folderName, name, state, zoneNames, data, scheduleName)
}

