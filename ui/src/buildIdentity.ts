type ApiBaseUrlEnv = {
  VITE_API_BASE_URL?: string
  PROD?: boolean
}

// A production build defaults API calls to the same origin that served the UI.
// An explicit VITE_API_BASE_URL override (including an empty relative value) is
// honored, and the existing development fallback is preserved.
export function resolveApiBaseUrl(env: ApiBaseUrlEnv): string {
  if (env.VITE_API_BASE_URL !== undefined) {
    return env.VITE_API_BASE_URL
  }

  return env.PROD ? '' : 'http://localhost:5000'
}

// Only a full, lowercase 40-character commit SHA is a valid build identity.
// Anything else (including the literal "unknown") degrades to "Build unknown".
export function extractCommitSha(payload: unknown): string | null {
  if (typeof payload !== 'object' || payload === null) {
    return null
  }

  const value = (payload as { commitSha?: unknown }).commitSha
  if (typeof value !== 'string') {
    return null
  }

  return /^[0-9a-f]{40}$/.test(value) ? value : null
}
