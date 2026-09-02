import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
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

        return Promise.reject(new Error(`Unexpected fetch to ${url}`))
      }),
    )
  })

  it('shows the backend-provided project name in the workbench header', async () => {
    render(<App />)

    await waitFor(() => {
      expect(screen.getByTestId('project-name')).toHaveTextContent('Aurora Platform')
    })

    // The old hardcoded label must be gone once the real name loads.
    expect(screen.queryByText('Placeholder Project')).not.toBeInTheDocument()
  })
})
