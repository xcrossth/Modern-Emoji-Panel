@echo off
setlocal

REM Compatibility wrapper retained for upstream users. The canonical build
REM now lives at the monorepo root and resolves every path from its script.
set "REPO_ROOT=%~dp0..\.."
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%REPO_ROOT%\scripts\build.ps1" -PublishSelfContained
exit /b %ERRORLEVEL%
