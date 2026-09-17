param(
    [string]$UnityEditor = 'E:\Unity\6000.5.9f1\Editor\Unity.exe',
    [switch]$Verify
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$log = Join-Path $project 'Logs\WaterBuild.log'
New-Item -ItemType Directory -Path (Join-Path $project 'Logs') -Force | Out-Null
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw 'Supply the Unity 6000.5.9f1 executable with -UnityEditor.' }
# Some embedded shells omit ALLUSERSPROFILE; UPM requires it. Change only the child environment.
$start = [System.Diagnostics.ProcessStartInfo]::new()
$start.FileName = $UnityEditor
$start.Arguments = '-batchmode -quit -projectPath "' + $project + '" -executeMethod WaterCourtyard.Editor.CourtyardBuilder.BuildWindows -logFile "' + $log + '"'
$start.UseShellExecute = $false
$start.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$start.EnvironmentVariables['ALLUSERSPROFILE'] = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
$process = [System.Diagnostics.Process]::Start($start)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity build failed; see $log" }
$exe = Join-Path $project 'Builds\WaterCourt-Windows\WaterCourt.exe'
if ($Verify) {
    $verifyLog = Join-Path $project 'Logs\WaterAcceptance.log'
    # The acceptance tour is a visible interactive render, so screenshots have a swapchain.
    $run = Start-Process -FilePath $exe -ArgumentList @('--verify-water','-screen-width','1600','-screen-height','1000','-screen-fullscreen','0','-logFile',('"' + $verifyLog + '"')) -WindowStyle Normal -PassThru
    $run.WaitForExit()
    if ($run.ExitCode -ne 0) { throw "Runtime verification failed; see $verifyLog" }
}
Write-Output "Playable build: $exe"
