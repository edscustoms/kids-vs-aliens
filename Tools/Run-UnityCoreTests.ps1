param([string]$ResultName = 'Presentation-Core', [switch]$InitializePresentationAssets)
$ErrorActionPreference = 'Stop'
if ($ResultName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a simple result filename.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Unity is already open. Run the Core category in its Test Runner, or close it before batch testing.'
}
$versionLine = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Select-Object -First 1
$editorVersion = $versionLine.Split(':')[1].Trim()
$editorPath = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
$savedResources = @{}
$timeSettings = Join-Path $projectRoot 'ProjectSettings/TimeManager.asset'
$savedResources[$timeSettings] = [IO.File]::ReadAllBytes($timeSettings)
foreach ($name in @('PerformanceTestRunInfo.json', 'PerformanceTestRunInfo.json.meta', 'PerformanceTestRunSettings.json', 'PerformanceTestRunSettings.json.meta')) {
    $path = Join-Path $projectRoot "Assets/Resources/$name"
    if (Test-Path -LiteralPath $path) { $savedResources[$path] = [IO.File]::ReadAllBytes($path) }
}
try {
    $arguments = '-batchmode -nographics -projectPath "' + $projectRoot + '" -runTests -testPlatform EditMode -testCategory Core -testResults "Logs/' + $ResultName + '-tests.xml" -logFile "Logs/' + $ResultName + '-unity.log"'
    if ($InitializePresentationAssets) { $arguments += ' -executeMethod GameplayPresentationSetup.CreateInitialAssets' }
    $testProcess = Start-Process -FilePath $editorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $testProcess.WaitForExit()
    $testExitCode = $testProcess.ExitCode
    $resultPath = Join-Path $projectRoot "Logs/$ResultName-tests.xml"
    if (Test-Path -LiteralPath $resultPath) {
        [xml]$results = Get-Content -LiteralPath $resultPath
        $results.'test-run' | Select-Object result,total,passed,failed,skipped
        $results.SelectNodes('//test-case[@result="Failed"]/failure') | ForEach-Object { Write-Output $_.InnerText }
    }
}
finally {
    foreach ($path in $savedResources.Keys) { [IO.File]::WriteAllBytes($path, $savedResources[$path]) }
}
exit $testExitCode
