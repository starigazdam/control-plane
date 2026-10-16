import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { extractCommitSha, resolveApiBaseUrl } from './buildIdentity'

const validSha = '0123456789abcdef0123456789abcdef01234567'

const overviewResponse = {
  generatedAtUtc: '2026-09-02T12:00:00Z',
  projects: [
    {
      projectId: 'placeholder',
      projectName: 'Aurora Platform',
      statusLevel: 'Healthy',
      activeAlerts: 0,
      availableOperations: 2,
    },
  ],
}

const projectDetailsResponse = {
  project: {
    id: 'placeholder',
    name: 'Aurora Platform',
  },
  statusLevel: 'Healthy',
  statusSnapshots: [],
  operations: [],
}

function jsonResponse(body: unknown) {
  return Promise.resolve({
    ok: true,
    status: 200,
    json: () => Promise.resolve(body),
  } as Response)
}

function jsonErrorResponse(status: number) {
  return Promise.resolve({
    ok: false,
    status,
    json: () => Promise.resolve({}),
  } as Response)
}

function stubFetch(versionHandler: (url: string) => Promise<Response>) {
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) => {
      if (url.includes('/api/version')) {
        return versionHandler(url)
      }

      if (url.includes('/api/overview')) {
        return jsonResponse(overviewResponse)
      }

      if (url.includes('/api/projects/placeholder')) {
        return jsonResponse(projectDetailsResponse)
      }

      if (url.includes('/api/operations/history')) {
        return jsonResponse([])
      }

      return Promise.reject(new Error(`Unexpected fetch to ${url}`))
    }),
  )
}

describe('App project name display', () => {
  beforeEach(() => {
    stubFetch(() => jsonResponse({ commitSha: validSha }))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the backend-provided project name in the workbench header', async () => {
    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('project-name')).toHaveTextContent('Aurora Platform')
    })

    // The old hardcoded label must be gone once the real name loads.
    expect(screen.queryByText('Placeholder Project')).not.toBeInTheDocument()
  })

  it('shows the backend-provided project name on the Projects page', async () => {
    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('project-name')).toHaveTextContent('Aurora Platform')
    })

    fireEvent.click(screen.getByRole('button', { name: 'Projects' }))

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { level: 3, name: 'Aurora Platform' }),
      ).toBeInTheDocument()
    })

    // The old hardcoded label must not appear anywhere on the Projects page.
    expect(screen.queryByText('Placeholder Project')).not.toBeInTheDocument()
  })
})

describe('App build badge', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the abbreviated commit and exposes the full SHA in its title', async () => {
    stubFetch(() => jsonResponse({ commitSha: validSha }))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('build-badge')).toHaveTextContent(
        `Build ${validSha.slice(0, 7)}`,
      )
    })

    expect(screen.getByTestId('build-badge')).toHaveAttribute('title', `Build ${validSha}`)
  })

  it('degrades to Build unknown on a missing version property without blocking tabs', async () => {
    stubFetch(() => jsonResponse({}))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('build-badge')).toHaveTextContent('Build unknown')
    })

    fireEvent.click(screen.getByRole('button', { name: 'Projects' }))

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { level: 3, name: 'Aurora Platform' }),
      ).toBeInTheDocument()
    })
  })

  it('maps the literal unknown value to Build unknown', async () => {
    stubFetch(() => jsonResponse({ commitSha: 'unknown' }))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('build-badge')).toHaveTextContent('Build unknown')
    })
  })

  it('degrades to Build unknown on a network failure', async () => {
    stubFetch(() => Promise.reject(new Error('network down')))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('build-badge')).toHaveTextContent('Build unknown')
    })
  })

  it('degrades to Build unknown on an HTTP failure', async () => {
    stubFetch(() => jsonErrorResponse(500))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('build-badge')).toHaveTextContent('Build unknown')
    })
  })

  it('degrades to Build unknown on wrong-type data', async () => {
    stubFetch(() => jsonResponse({ commitSha: 42 }))

    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('build-badge')).toHaveTextContent('Build unknown')
    })
  })
})

describe('resolveApiBaseUrl', () => {
  it('defaults a production build to the same origin', () => {
    expect(resolveApiBaseUrl({ PROD: true })).toBe('')
  })

  it('preserves an empty relative override', () => {
    expect(resolveApiBaseUrl({ PROD: true, VITE_API_BASE_URL: '' })).toBe('')
  })

  it('honors an explicit override', () => {
    expect(
      resolveApiBaseUrl({ PROD: true, VITE_API_BASE_URL: 'https://api.example.test' }),
    ).toBe('https://api.example.test')
  })

  it('preserves the development fallback when no override is set', () => {
    expect(resolveApiBaseUrl({ PROD: false })).toBe('http://localhost:5000')
  })
})

describe('extractCommitSha', () => {
  it('accepts a full lowercase 40-character SHA', () => {
    expect(extractCommitSha({ commitSha: validSha })).toBe(validSha)
  })

  it('rejects unknown, missing, null, wrong-type, and malformed values', () => {
    expect(extractCommitSha({ commitSha: 'unknown' })).toBeNull()
    expect(extractCommitSha({})).toBeNull()
    expect(extractCommitSha({ commitSha: null })).toBeNull()
    expect(extractCommitSha({ commitSha: 42 })).toBeNull()
    expect(extractCommitSha(null)).toBeNull()
    expect(extractCommitSha({ commitSha: validSha.toUpperCase() })).toBeNull()
    expect(extractCommitSha({ commitSha: validSha.slice(0, 39) })).toBeNull()
  })
})
