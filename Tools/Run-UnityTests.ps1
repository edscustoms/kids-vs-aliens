param(
    [ValidateSet('Quick','Core','Full')][string]$Suite = 'Quick',
    [string]$TestFilter,
    [string]$ResultName = '',
    [switch]$ReuseCopy
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$ResultName) { $ResultName = $Suite + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') }
if ($ResultName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a simple result filename.' }
$outputRoot = Join-Path $projectRoot 'Logs/RepositoryAuditRemediation'
$copyRoot = Join-Path $outputRoot 'TestProject'
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Close Unity before starting the batch runner.'
}
if (!$ReuseCopy -and (Test-Path -LiteralPath $copyRoot)) {
    throw 'TestProject already exists. Use -ReuseCopy to refresh its source, or move it aside before creating a fresh copy.'
}
[IO.Directory]::CreateDirectory($copyRoot) | Out-Null
# Copy working-tree bytes, including new source files; never hard-link production assets.
$paths = & git -C $projectRoot ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
$copiedPaths = [System.Collections.Generic.List[string]]::new()
foreach ($relative in $paths) {
    if ($relative -notmatch '^(Assets|Packages|ProjectSettings)/') { continue }
    $source = Join-Path $projectRoot $relative
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
    $destination = Join-Path $copyRoot $relative
    [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
    [IO.File]::Copy($source, $destination, $true)
    $copiedPaths.Add($relative)
}
$manifest = Join-Path $copyRoot 'copied-source.json'
if (Test-Path -LiteralPath $manifest) {
    foreach ($old in (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)) {
        if ($copiedPaths.Contains($old)) { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $copyRoot $old))
        if (!$target.StartsWith([IO.Path]::GetFullPath($copyRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Invalid path in isolated-copy manifest.'
        }
        if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target }
    }
}
[IO.File]::WriteAllText($manifest, (ConvertTo-Json -InputObject $copiedPaths.ToArray()))
if (!$ReuseCopy -and (Test-Path (Join-Path $projectRoot 'Library'))) {
    & robocopy (Join-Path $projectRoot 'Library') (Join-Path $copyRoot 'Library') /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /XD Bee ShaderCache PramData | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Copying the Unity import cache failed.' }
}
$settings = Join-Path $copyRoot 'ProjectSettings/ProjectSettings.asset'
$text = [IO.File]::ReadAllText($settings) -replace '(?m)^  companyName:.*$', '  companyName: KidsTests' -replace '(?m)^  productName:.*$', '  productName: IsolatedRegression'
[IO.File]::WriteAllText($settings, $text)
$version = ((Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Select-Object -First 1) -split ':')[1].Trim()
$editor = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
$results = Join-Path $outputRoot "$ResultName-tests.xml"
$log = Join-Path $outputRoot "$ResultName-unity.log"
if (Test-Path -LiteralPath $results) { throw "Result already exists: $results" }
$previousSaveDirectory = $env:KIDS_TEST_SAVE_DIRECTORY
try {
    $env:KIDS_TEST_SAVE_DIRECTORY = Join-Path $outputRoot "$ResultName-saves"
    $arguments = '-batchmode -projectPath "' + $copyRoot + '" -runTests -testPlatform EditMode -testResults "' + $results + '" -logFile "' + $log + '"'
    # Graphics intentionally enabled: Core and Full include actual URP/render tests.
    if ($TestFilter) { $arguments += ' -testFilter "' + $TestFilter + '"' }
    elseif ($Suite -eq 'Quick') { $arguments += ' -testFilter "CombatHitResolverTests;VfxPoolTests;PlayerLoadoutStateTests;OwnedWeaponStateTests"' }
    elseif ($Suite -eq 'Core') { $arguments += ' -testCategory Core' }
    Write-Output "Suite: $Suite; filter: $TestFilter; results: $results; log: $log; saves: $env:KIDS_TEST_SAVE_DIRECTORY"
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    $code = $process.ExitCode
    if (!(Test-Path -LiteralPath $results)) { throw "Unity produced no test results (exit $code). Read $log" }
    [xml]$report = [IO.File]::ReadAllText($results)
    $report.'test-run' | Select-Object result,total,passed,failed,skipped
    $report.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object { Write-Output ($_.fullname + ': ' + $_.failure.message.InnerText) }
    if ($code -ne 0 -or [int]$report.'test-run'.failed -gt 0 -or [int]$report.'test-run'.total -eq 0) { exit 1 }
} finally { $env:KIDS_TEST_SAVE_DIRECTORY = $previousSaveDirectory }
exit 0
