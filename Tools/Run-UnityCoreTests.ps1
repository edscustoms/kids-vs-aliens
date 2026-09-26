param([string]$ResultName = '')
# Partial Core category only; graphics and isolation are supplied by the shared runner.
& (Join-Path $PSScriptRoot 'Run-UnityTests.ps1') -Suite Core -ResultName $ResultName -ReuseCopy
exit $LASTEXITCODE
