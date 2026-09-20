$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$outputRoot = Join-Path $projectRoot 'Temp/LightStateTests'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$testSource = [System.Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'Program.cs'))
$coreSource = [System.Security.SecurityElement]::Escape((Join-Path $projectRoot 'Assets/Scripts/Gameplay/Light/LightActivationState.cs'))
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
 <ItemGroup><Compile Include="$testSource"/><Compile Include="$coreSource"/></ItemGroup>
</Project>
"@
$projectPath = Join-Path $outputRoot 'LightStateTests.csproj'
Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8
& dotnet run --project $projectPath
if ($LASTEXITCODE -ne 0) { throw 'Light activation tests failed.' }
