<#
.SYNOPSIS
    Builds a release of Sea Power Borders and stages the folder that goes to the Steam Workshop.

.DESCRIPTION
    Release build without the automatic deploy, then dist\SeaPowerBorders\ with exactly what players get:
    SeaPowerBorders.dll, _info.ini, LICENSE.txt, preview.jpg (the Workshop preview, which the game's upload screen
    only lets you pick from inside StreamingAssets) and the data folders borders\ and places\.
    With -Install the staged folder also replaces <GameDir>\Sea Power_Data\StreamingAssets\SeaPowerBorders\, the
    folder to pick in the game's Create Mod screen. Sea Power must be closed for that.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
#>
param(
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { 'E:\Games\steamapps\common\Sea Power' }),
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\SeaPowerBorders\SeaPowerBorders.csproj'
$dist = Join-Path $root 'dist\SeaPowerBorders'

dotnet build $project -c Release -p:Deploy=false -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$out = Join-Path $root 'src\SeaPowerBorders\bin\Release\netstandard2.1'
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $out 'SeaPowerBorders.dll') $dist
Copy-Item (Join-Path $root 'src\SeaPowerBorders\mod\_info.ini') $dist
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $dist 'LICENSE.txt')
Copy-Item (Join-Path $root 'workshop\preview.jpg') $dist
# The data the mod reads at run time: borders\borders.bin + eras.csv, places\world.csv + naval_bases.csv + eras.csv.
Copy-Item -Recurse (Join-Path $root 'src\SeaPowerBorders\mod\borders') (Join-Path $dist 'borders')
Copy-Item -Recurse (Join-Path $root 'src\SeaPowerBorders\mod\places') (Join-Path $dist 'places')

$version = (Select-String -Path (Join-Path $root 'src\SeaPowerBorders\Plugin.cs') -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
Write-Host "Staged Sea Power Borders $version in $dist"
Get-ChildItem -Recurse $dist | Where-Object { -not $_.PSIsContainer } | ForEach-Object { Write-Host ("  {0,10:N0}  {1}" -f $_.Length, $_.FullName.Substring($dist.Length + 1)) }

if ($Install) {
    if (Get-Process -Name 'Sea Power' -ErrorAction SilentlyContinue) { throw 'Sea Power is running - close it first.' }
    $target = Join-Path $GameDir 'Sea Power_Data\StreamingAssets\SeaPowerBorders'
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    Copy-Item -Recurse $dist $target
    Write-Host "Installed to $target - pick this folder in Mods > Create Mod."
}
