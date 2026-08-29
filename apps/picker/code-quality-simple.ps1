#!/usr/bin/env pwsh

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Compatibility wrapper retained at the upstream path. Verification is
# centralised at the monorepo root so it also covers SDK and dependency locks.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
& (Join-Path $repositoryRoot "scripts\verify-foundation.ps1")
exit $LASTEXITCODE
