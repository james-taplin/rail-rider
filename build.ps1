param([string]$DVRoot = 'B:\SteamLibrary\steamapps\common\Derail Valley')
$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:ProgramW6432 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet).Source }
& $dotnet build (Join-Path $PSScriptRoot 'RailRider.csproj') -c Release --nologo "/p:DVRoot=$DVRoot" -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$pkg = Join-Path $PSScriptRoot 'build_out\RailRider'
New-Item -ItemType Directory -Force $pkg | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'bin\Release\net48\RailRider.dll') $pkg -Force
Copy-Item (Join-Path $PSScriptRoot 'Info.json') $pkg -Force
New-Item -ItemType Directory -Force (Join-Path $pkg "Models") | Out-Null
Get-ChildItem (Join-Path $PSScriptRoot "Models") -Include *.glb,rail_rider_inventory.png -Recurse | Copy-Item -Destination (Join-Path $pkg "Models") -Force
$audioOut = Join-Path $pkg 'Audio'
New-Item -ItemType Directory -Force $audioOut | Out-Null
Get-ChildItem (Join-Path $PSScriptRoot 'Audio') -Filter '*.wav' | Copy-Item -Destination $audioOut -Force
Write-Host "Built (NOT installed): $pkg"

if (Test-Path (Join-Path $PSScriptRoot "Models\icons")) { New-Item -ItemType Directory -Force (Join-Path $pkg "Models\icons") | Out-Null; Get-ChildItem (Join-Path $PSScriptRoot "Models\icons") -Filter *.png | Copy-Item -Destination (Join-Path $pkg "Models\icons") -Force }

if (Test-Path (Join-Path $PSScriptRoot "Audio")) { New-Item -ItemType Directory -Force (Join-Path $pkg "Audio") | Out-Null; Get-ChildItem (Join-Path $PSScriptRoot "Audio") -Filter *.wav | Copy-Item -Destination (Join-Path $pkg "Audio") -Force }
