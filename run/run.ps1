$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$appHostProjectPath = Join-Path $repoRoot 'src\ControlPlane.AppHost\ControlPlane.AppHost.csproj'

if (-not (Test-Path $appHostProjectPath)) {
    throw "Aspire AppHost project not found at: $appHostProjectPath"
}

Write-Host "Starting Control Plane through .NET Aspire: $appHostProjectPath"
dotnet run --project $appHostProjectPath
