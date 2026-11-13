param(
    [string]$Unity = "${env:ProgramFiles}/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe",
    [switch]$Build,
    [ValidateRange(1, 8)][int]$Workers = 2
)
$ErrorActionPreference = 'Stop'
$previousGcServer = $env:DOTNET_gcServer
$previousProcessorCount = $env:DOTNET_PROCESSOR_COUNT
try {
    $env:DOTNET_gcServer = '0'
    $env:DOTNET_PROCESSOR_COUNT = "$Workers"
    $project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    if (!(Test-Path -LiteralPath $Unity)) { throw "Unity nao encontrado: $Unity" }
    $drive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($project))
    $minimumSpace = if ($Build) { 2GB } else { 1GB }
    if ($drive.AvailableFreeSpace -lt $minimumSpace) {
        throw "Espaco insuficiente em $($drive.Name). Libere pelo menos $($minimumSpace / 1GB) GB antes de iniciar o Unity."
    }
    New-Item -ItemType Directory -Path (Join-Path $project 'Logs') -Force | Out-Null
    foreach ($platform in @('EditMode', 'PlayMode')) {
        $result = Join-Path $project "Logs/$($platform.ToLower())-results.xml"
        $log = Join-Path $project "Logs/$($platform.ToLower()).log"
        $arguments = "-batchmode -nographics -job-worker-count $Workers -projectPath `"$project`" -runTests -testPlatform $platform -testResults `"$result`" -logFile `"$log`""
        $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Falha em $platform. Consulte $log" }
        [xml]$report = Get-Content -LiteralPath $result -Raw
        $run = $report.'test-run'
        if ($run.failed -ne '0' -or $run.total -eq '0') { throw "Testes falharam ou nao foram encontrados: $result" }
        Write-Output "$platform : $($run.passed)/$($run.total) passaram"
    }
    if ($Build) {
        $log = Join-Path $project 'Logs/build.log'
        $arguments = "-batchmode -nographics -job-worker-count $Workers -projectPath `"$project`" -executeMethod Troia.Editor.GameplaySetup.BuildWindows -quit -logFile `"$log`""
        $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Build falhou. Consulte $log" }
        Write-Output (Join-Path $project 'Builds/Windows/Iliada.exe')
    }
} finally {
    $env:DOTNET_gcServer = $previousGcServer
    $env:DOTNET_PROCESSOR_COUNT = $previousProcessorCount
}
