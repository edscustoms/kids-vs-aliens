param([string]$OutputDirectory = 'Temp/ScriptValidation')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null
$sdkVersion = (& dotnet --version).Trim()
$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
$compiler = Join-Path $dotnetRoot "sdk/$sdkVersion/Roslyn/bincore/csc.dll"
foreach ($assembly in @('Assembly-CSharp', 'Assembly-CSharp-Editor')) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot "$assembly.csproj")
    $arguments = [System.Collections.Generic.List[string]]::new()
    $arguments.Add('/nologo')
    $arguments.Add('/target:library')
    $arguments.Add('/langversion:9')
    $arguments.Add('/nostdlib+')
    $arguments.Add('/warn:4')
    $arguments.Add('/nowarn:0649') # Inspector-assigned fields; preserve all other warnings.
    $arguments.Add('/define:' + ($project.Project.PropertyGroup.DefineConstants | Where-Object { $_ } | Select-Object -First 1))
    $arguments.Add('/out:"' + (Join-Path $validationRoot "$assembly.dll") + '"')
    foreach ($reference in $project.Project.ItemGroup.Reference) {
        if (!$reference.HintPath) { continue }
        $referencePath = [string]$reference.HintPath
        if (![IO.Path]::IsPathRooted($referencePath)) { $referencePath = Join-Path $projectRoot $referencePath }
        $arguments.Add('/reference:"' + $referencePath + '"')
    }
    if ($assembly -eq 'Assembly-CSharp-Editor') {
        $arguments.Add('/reference:"' + (Join-Path $validationRoot 'Assembly-CSharp.dll') + '"')
    }
    $sources = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($source in $project.Project.ItemGroup.Compile) {
        if ($source.Include) {
            $sourcePath = Join-Path $projectRoot $source.Include
            # Unity's generated IDE project can still list a recently removed script.
            if (Test-Path -LiteralPath $sourcePath) { [void]$sources.Add($sourcePath) }
        }
    }
    # Include newly authored Game scripts even before Unity regenerates its IDE projects.
    foreach ($source in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Game') -Filter '*.cs' -Recurse) {
        $isEditor = $source.FullName -match '[\\/]Editor[\\/]'
        if ($isEditor -eq ($assembly -eq 'Assembly-CSharp-Editor')) { [void]$sources.Add($source.FullName) }
    }
    foreach ($source in $sources) { $arguments.Add('"' + $source + '"') }
    $responsePath = Join-Path $validationRoot "$assembly.rsp"
    [IO.File]::WriteAllLines($responsePath, $arguments)
    & dotnet $compiler "@$responsePath"
    if ($LASTEXITCODE -ne 0) { throw "$assembly compilation failed ($LASTEXITCODE)." }
    Write-Output "$assembly compiled successfully ($($sources.Count) source files)."
}
