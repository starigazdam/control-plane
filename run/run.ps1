$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProjectPath = Join-Path $repoRoot 'src\ControlPlane.Api\ControlPlane.Api.csproj'
$uiPath = Join-Path $repoRoot 'ui'
$uiNodeModules = Join-Path $uiPath 'node_modules'

if (-not (Test-Path $apiProjectPath)) {
    throw "API project not found at: $apiProjectPath"
}

if (-not (Test-Path $uiPath)) {
    throw "UI folder not found at: $uiPath"
}

if (-not (Test-Path $uiNodeModules)) {
    throw "UI dependencies are missing. Run 'npm --prefix ui install' first."
}

Write-Host "Starting API: $apiProjectPath"
Start-Process -FilePath $env:ComSpec -ArgumentList "/c dotnet run --project `"$apiProjectPath`"" -WorkingDirectory $repoRoot | Out-Null

Write-Host "Starting UI: $uiPath"
Start-Process -FilePath $env:ComSpec -ArgumentList '/c npm run dev -- --host' -WorkingDirectory $uiPath | Out-Null

Write-Host 'Started Control Plane API and UI dev servers.'
