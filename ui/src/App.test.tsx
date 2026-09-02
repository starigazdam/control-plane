import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

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

describe('App project name display', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) => {
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
