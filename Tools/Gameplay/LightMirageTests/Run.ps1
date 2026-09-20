param([switch]$MissingBaseline)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$outputRoot=Join-Path $projectRoot 'Temp/LightMirageTests'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$testSource=[System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'Program.cs'))
$corePath=if($MissingBaseline){Join-Path $PSScriptRoot 'MissingFeature.cs'}else{Join-Path $projectRoot 'Assets/Scripts/Gameplay/Mechanisms/WaterMirageMath.cs'}
$coreSource=[System.Security.SecurityElement]::Escape($corePath)
$project=@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="$testSource"/><Compile Include="$coreSource"/></ItemGroup></Project>
"@
$path=Join-Path $outputRoot 'LightMirageTests.csproj'
Set-Content -LiteralPath $path -Value $project -Encoding utf8
& dotnet run --project $path
if($LASTEXITCODE -ne 0){throw 'Water mirage math checks failed.'}
