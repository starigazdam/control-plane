import { useEffect, useMemo, useState } from 'react'
import './App.css'

type PageKey =
  | 'overview'
  | 'alerts'
  | 'operations'
  | 'projects'
  | 'execution-history'

type PageDefinition = {
  key: PageKey
  title: string
  subtitle: string
}

type StatusSignal = {
  id: string
  title: string
  description: string
  level: string
}

type StatusSnapshot = {
  providerId: string
  overallLevel: string
  signals: StatusSignal[]
}

type OperationParameter = {
  name: string
  displayName: string
  description: string
  isRequired: boolean
}

type OperationDefinition = {
  id: string
  displayName: string
  description: string
  parameters: OperationParameter[]
}

type OverviewProjectSummary = {
  projectId: string
  projectName: string
  statusLevel: string
  activeAlerts: number
  availableOperations: number
}

type OverviewResponse = {
  generatedAtUtc: string
  projects: OverviewProjectSummary[]
}

type ProjectDetailsResponse = {
  project: {
    id: string
    name: string
  }
  statusLevel: string
  statusSnapshots: StatusSnapshot[]
  operations: OperationDefinition[]
}

type OperationHistoryEntry = {
  projectId: string
  operationId: string
  initiatedBy: string
  requestedAtUtc: string
  result: {
    status: string
    message: string
  }
}

const pages: PageDefinition[] = [
  {
    key: 'overview',
    title: 'Overview',
    subtitle: 'High-signal project health and action summaries.',
  },
  {
    key: 'alerts',
    title: 'Alerts',
    subtitle: 'Active incidents and threshold breaches that need action.',
  },
  {
    key: 'operations',
    title: 'Operations',
    subtitle: 'Opinionated one-click operational actions.',
  },
  {
    key: 'projects',
    title: 'Projects',
    subtitle: 'Project-centric view of status, alerts, and actions.',
  },
  {
    key: 'execution-history',
    title: 'Execution History',
    subtitle: 'Operation run history with outcomes and timestamps.',
  },
]

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

function App() {
  const [activePage, setActivePage] = useState<PageKey>('overview')
  const [projectName, setProjectName] = useState<string | null>(null)
  const [projectDetails, setProjectDetails] = useState<ProjectDetailsResponse | null>(
    null,
  )
  const [history, setHistory] = useState<OperationHistoryEntry[]>([])
  const [formValues, setFormValues] = useState<Record<string, Record<string, string>>>(
    {},
  )
  const [isLoadingProject, setIsLoadingProject] = useState(false)
  const [isLoadingHistory, setIsLoadingHistory] = useState(false)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const currentPage = useMemo(
    () => pages.find((page) => page.key === activePage) ?? pages[0],
    [activePage],
  )

  useEffect(() => {
    void loadOverview()
  }, [])

  useEffect(() => {
    if (activePage === 'projects') {
      void loadProject()
      void loadHistory()
    }

    if (activePage === 'execution-history') {
      void loadHistory()
    }
  }, [activePage])

  const loadOverview = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/overview`)
      if (!response.ok) {
        throw new Error(`Failed to load overview (${response.status})`)
      }

      const payload = (await response.json()) as OverviewResponse
      setProjectName(payload.projects[0]?.projectName ?? null)
    } catch (loadError) {
      setError(
        loadError instanceof Error
          ? loadError.message
          : 'Unknown overview loading error',
      )
    }
  }

  const loadProject = async () => {
    setIsLoadingProject(true)
    setError(null)

    try {
      const response = await fetch(`${API_BASE_URL}/api/projects/placeholder`)
      if (!response.ok) {
        throw new Error(`Failed to load project (${response.status})`)
      }

      const payload = (await response.json()) as ProjectDetailsResponse
      setProjectDetails(payload)
    } catch (loadError) {
      setError(
        loadError instanceof Error
          ? loadError.message
          : 'Unknown project loading error',
      )
    } finally {
      setIsLoadingProject(false)
    }
  }

  const loadHistory = async () => {
    setIsLoadingHistory(true)

    try {
      const response = await fetch(`${API_BASE_URL}/api/operations/history?take=20`)
      if (!response.ok) {
        throw new Error(`Failed to load history (${response.status})`)
      }

      const payload = (await response.json()) as OperationHistoryEntry[]
      setHistory(payload)
    } catch (loadError) {
      setError(
        loadError instanceof Error
          ? loadError.message
          : 'Unknown history loading error',
      )
    } finally {
      setIsLoadingHistory(false)
    }
  }

  const setOperationFieldValue = (
    operationId: string,
    parameterName: string,
    value: string,
  ) => {
    setFormValues((current) => ({
      ...current,
      [operationId]: {
        ...(current[operationId] ?? {}),
        [parameterName]: value,
      },
    }))
  }

  const executeOperation = async (operationId: string) => {
    setFeedback(null)
    setError(null)

    try {
      const operationInput = formValues[operationId] ?? {}
      const response = await fetch(`${API_BASE_URL}/api/operations/execute`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          projectId: projectDetails?.project.id ?? 'placeholder',
          operationId,
          input: operationInput,
          requestedBy: 'ui-engineer',
        }),
      })

      if (!response.ok) {
        throw new Error(`Operation execution failed (${response.status})`)
      }

      const payload = (await response.json()) as OperationHistoryEntry
      setFeedback(`${payload.result.status}: ${payload.result.message}`)
      await loadHistory()
    } catch (executionError) {
      setError(
        executionError instanceof Error
          ? executionError.message
          : 'Unknown operation execution error',
      )
    }
  }

  const renderProjectsPage = () => {
    if (isLoadingProject) {
      return <p>Loading project...</p>
    }

    if (projectDetails === null) {
      return <p>No project data loaded.</p>
    }

    return (
      <div className="project-workflow">
        <section className="card">
          <h3>{projectDetails.project.name}</h3>
          <p>Status: {projectDetails.statusLevel}</p>
        </section>

        <section className="card">
          <h3>Alerts and Signals</h3>
          {projectDetails.statusSnapshots.length === 0 ? (
            <p>No status providers registered yet.</p>
          ) : (
            projectDetails.statusSnapshots.map((snapshot) => (
              <div className="snapshot" key={snapshot.providerId}>
                <p>
                  <strong>{snapshot.providerId}</strong> ({snapshot.overallLevel})
                </p>
                <ul>
                  {snapshot.signals.map((signal) => (
                    <li key={signal.id}>
                      {signal.title} - {signal.level}
                    </li>
                  ))}
                </ul>
              </div>
            ))
          )}
        </section>

        <section className="card">
          <h3>Operations</h3>
          {projectDetails.operations.length === 0 ? (
            <p>No operations registered yet.</p>
          ) : (
            <div className="operations">
              {projectDetails.operations.map((operation) => (
                <article className="operation-card" key={operation.id}>
                  <h4>{operation.displayName}</h4>
                  <p>{operation.description}</p>

                  {operation.parameters.map((parameter) => (
                    <label key={parameter.name} className="field">
                      {parameter.displayName}
                      <input
                        value={formValues[operation.id]?.[parameter.name] ?? ''}
                        onChange={(event) =>
                          setOperationFieldValue(
                            operation.id,
                            parameter.name,
                            event.target.value,
                          )
                        }
                        placeholder={parameter.description}
                      />
                    </label>
                  ))}

                  <button
                    type="button"
                    className="action-button"
                    onClick={() => executeOperation(operation.id)}
                  >
                    Execute
                  </button>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className="card">
          <h3>Execution Feedback</h3>
          <p>{feedback ?? 'No operation executed in this session yet.'}</p>
        </section>
      </div>
    )
  }

  const renderExecutionHistoryPage = () => {
    if (isLoadingHistory) {
      return <p>Loading execution history...</p>
    }

    if (history.length === 0) {
      return <p>No executions recorded yet.</p>
    }

    return (
      <section className="card">
        <h3>Recent Executions</h3>
        <table>
          <thead>
            <tr>
              <th>Time</th>
              <th>Operation</th>
              <th>Status</th>
              <th>Message</th>
            </tr>
          </thead>
          <tbody>
            {history.map((entry, index) => (
              <tr key={`${entry.requestedAtUtc}-${entry.operationId}-${index}`}>
                <td>{new Date(entry.requestedAtUtc).toLocaleString()}</td>
                <td>{entry.operationId}</td>
                <td>{entry.result.status}</td>
                <td>{entry.result.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    )
  }

  const renderPageContent = () => {
    if (activePage === 'projects') {
      return renderProjectsPage()
    }

    if (activePage === 'execution-history') {
      return renderExecutionHistoryPage()
    }

    return (
      <section className="cards">
        <article className="card">
          <h3>{projectName ?? 'Loading project…'}</h3>
          <p>Status: Healthy</p>
        </article>
        <article className="card">
          <h3>Actions</h3>
          <p>Replay DLQ, restart app, rerun pipeline.</p>
        </article>
        <article className="card">
          <h3>Signals</h3>
          <p>Alerts, failures, lag, and deployment health.</p>
        </article>
      </section>
    )
  }

  return (
    <div className="app-shell">
      <header className="top-bar">
        <div>
          <p className="eyebrow">Control Plane</p>
          <h1 data-testid="project-name">{projectName ?? 'Engineering Workbench'}</h1>
        </div>
        <p className="tagline">
          Operational visibility and one-click actions across projects.
        </p>
      </header>

      <nav className="nav-tabs" aria-label="Primary">
        {pages.map((page) => (
          <button
            key={page.key}
            type="button"
            className={page.key === activePage ? 'tab active' : 'tab'}
            onClick={() => setActivePage(page.key)}
          >
            {page.title}
          </button>
        ))}
      </nav>

      <main className="page">
        <header className="page-header">
          <h2>{currentPage.title}</h2>
          <p>{currentPage.subtitle}</p>
        </header>

        {error ? <p className="error">{error}</p> : null}
        {renderPageContent()}
      </main>
    </div>
  )
}

export default App
