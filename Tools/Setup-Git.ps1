[CmdletBinding()]
param([string]$UnityEditor)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    & git rev-parse --show-toplevel | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Run this script from a Git clone of the project.' }
    $versionLine = Get-Content -LiteralPath 'ProjectSettings/ProjectVersion.txt' |
        Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    $unityVersion = ($versionLine -replace '^m_EditorVersion: ', '').Trim()
    if (-not $unityVersion) { throw 'Cannot read the required Unity version.' }
    if (-not $UnityEditor) {
        $candidates = @(
            "$env:ProgramFiles/Unity/Hub/Editor/$unityVersion/Editor/Unity.exe",
            "E:/Unity/$unityVersion/Editor/Unity.exe"
        )
        $UnityEditor = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if (-not $UnityEditor -or -not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
        throw "Pass -UnityEditor with the path to Unity $unityVersion's Unity.exe."
    }
    $mergeTool = Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $UnityEditor).Path) 'Data/Tools/UnityYAMLMerge.exe'
    if (-not (Test-Path -LiteralPath $mergeTool -PathType Leaf)) { throw 'UnityYAMLMerge.exe was not found beside this editor.' }

    & git lfs install --local
    if ($LASTEXITCODE -ne 0) { throw 'Git LFS setup failed. Install Git LFS and check existing hooks.' }
    $mergeTool = $mergeTool.Replace('\', '/')
    # Git runs merge drivers through sh. Single quotes survive Windows PowerShell 5.1.
    $quotedMergeTool = "'" + $mergeTool.Replace("'", "'\''") + "'"
    $settings = [ordered]@{
        'core.autocrlf' = 'false'
        'pull.ff' = 'only'
        'merge.unityyamlmerge.name' = 'Unity Smart Merge'
        'merge.unityyamlmerge.recursive' = 'binary'
        'merge.unityyamlmerge.driver' = ('{0} merge -p %O %B %A %A' -f $quotedMergeTool)
    }
    foreach ($setting in $settings.GetEnumerator()) {
        & git config --local $setting.Key $setting.Value
        if ($LASTEXITCODE -ne 0) { throw "Git configuration failed: $($setting.Key)" }
    }
    Write-Host "Git collaboration configured for this clone. Required Unity: $unityVersion"
    Write-Host "Smart Merge: $mergeTool"
    Write-Host 'Use your own Git commit identity and run git lfs pull before opening Unity.'
}
finally {
    Pop-Location
}
