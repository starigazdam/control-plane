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
  const apiResponses: Array<{ url: string; status: number }> = []
  const pageErrors: string[] = []

  page.on('request', (outgoing) => {
    if (outgoing.url().includes('/api/')) {
      apiRequests.push(outgoing.url())
    }
  })
  page.on('response', (response) => {
    if (response.url().includes('/api/')) {
      apiResponses.push({ url: response.url(), status: response.status() })
    }
  })
  page.on('pageerror', (error) => pageErrors.push(error.message))

  // The application itself requests /api/overview on initial load. Wait for
  // that page response instead of issuing a second, concurrent request.
  const overviewResponsePromise = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/overview',
  )

  await page.goto('/')
  const baseOrigin = new URL(page.url()).origin

  const overviewResponse = await overviewResponsePromise
  const overviewStatus = overviewResponse.status()
  expect(
    overviewResponse.ok(),
    `GET /api/overview failed with status ${overviewStatus}`,
  ).toBeTruthy()

  const overview = (await overviewResponse.json()) as {
    projects: Array<{ projectId: string; projectName: string }>
  }
  const projectId = overview.projects[0]?.projectId
  const projectName = overview.projects[0]?.projectName
  expect(projectId).toBeTruthy()
  expect(projectName).toBeTruthy()

  // The header must render the project name from this page response. A fallback
  // label or a failed load would not match.
  await expect(page.getByTestId('project-name')).toHaveText(projectName ?? '')
  await expect(page.getByTestId('build-badge')).toContainText(/Build ([0-9a-f]{7}|unknown)/)

  const expectedSha = process.env.PLAYWRIGHT_BUILD_SHA
  if (expectedSha) {
    await expect(page.getByTestId('build-badge')).toHaveAttribute('title', `Build ${expectedSha}`)
  }

  await page.getByRole('button', { name: 'Projects' }).click()
  await expect(
    page.getByRole('heading', { level: 3, name: projectName ?? '' }),
  ).toBeVisible()
  // The app renders API failures in its application .error element.
  await expect(page.locator('.error')).toHaveCount(0)

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
  await expect(
    page.getByRole('cell', { name: fabricatedOperationId, exact: true }),
  ).toBeVisible()
  await expect(page.locator('.error')).toHaveCount(0)

  expect(pageErrors, `page errors: ${pageErrors.join('; ')}`).toEqual([])
  expect(apiRequests.length).toBeGreaterThan(0)

  for (const requestUrl of apiRequests) {
    expect(new URL(requestUrl).origin).toBe(baseOrigin)
  }
  for (const response of apiResponses) {
    expect(
      new URL(response.url).origin,
      `cross-origin API response: ${response.url}`,
    ).toBe(baseOrigin)
    expect(response.status, `API response failed: ${response.url}`).toBeLessThan(400)
  }
})
