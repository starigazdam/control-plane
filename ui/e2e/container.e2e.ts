import { expect, test } from '@playwright/test'

// Fabricated identifiers only. The operation is intentionally unknown to the
// host, so the request is rejected and recorded without triggering any external
// action.
const fabricatedOperationId = 'fabricated-unknown-operation-browser'
const fabricatedCorrelationId = 'fabricated-browser-correlation-37'

test('overview, badge, Projects and Execution History render from same-origin requests without load failures', async ({
  page,
  request,
}) => {
  const apiRequests: string[] = []
  const pageErrors: string[] = []

  page.on('request', (outgoing) => {
    if (outgoing.url().includes('/api/')) {
      apiRequests.push(outgoing.url())
    }
  })
  page.on('pageerror', (error) => pageErrors.push(error.message))

  await page.goto('/')
  const baseOrigin = new URL(page.url()).origin

  await expect(page.getByTestId('project-name')).toHaveText(/\S/)
  await expect(page.getByTestId('build-badge')).toContainText(/Build ([0-9a-f]{7}|unknown)/)

  const expectedSha = process.env.PLAYWRIGHT_BUILD_SHA
  if (expectedSha) {
    await expect(page.getByTestId('build-badge')).toHaveAttribute('title', `Build ${expectedSha}`)
  }

  await page.getByRole('button', { name: 'Projects' }).click()
  await expect(page.getByRole('heading', { level: 3, name: 'Control Plane' })).toBeVisible()

  const overviewResponse = await request.get('/api/overview')
  expect(overviewResponse.ok()).toBeTruthy()
  const overview = (await overviewResponse.json()) as {
    projects: Array<{ projectId: string }>
  }
  const projectId = overview.projects[0]?.projectId
  expect(projectId).toBeTruthy()

  const executeResponse = await request.post('/api/operations/execute', {
    data: {
      projectId,
      operationId: fabricatedOperationId,
      input: { fabricated: 'true' },
      requestedBy: 'browser-acceptance',
      correlationId: fabricatedCorrelationId,
    },
  })
  expect(executeResponse.ok()).toBeTruthy()

  await page.getByRole('button', { name: 'Execution History' }).click()
  await expect(page.getByRole('heading', { level: 2, name: 'Execution History' })).toBeVisible()
  await expect(page.getByText(fabricatedOperationId)).toBeVisible()

  expect(pageErrors, `page errors: ${pageErrors.join('; ')}`).toEqual([])
  expect(apiRequests.length).toBeGreaterThan(0)
  for (const requestUrl of apiRequests) {
    expect(new URL(requestUrl).origin).toBe(baseOrigin)
  }
})
