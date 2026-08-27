[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'fetch-assets.ps1')
dotnet run --configuration Release --project (Join-Path $PSScriptRoot 'AssetVisualSpike.csproj') -- --root $PSScriptRoot
