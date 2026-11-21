<#
.SYNOPSIS
    Builds a release of Sea Power Borders and stages the folder that goes to the Steam Workshop.

.DESCRIPTION
    Release build without the automatic deploy, then dist\SeaPowerBorders\ with exactly what players get:
    SeaPowerBorders.dll (one build for the game's current main version), SeaPowerRigging.dll (the built-in copy of R.A.T.S.
    Rigging, which the mod needs; the loader runs the newest copy of it), _info.ini, LICENSE.txt, CHANGELOG.md, preview.jpg
    (the Workshop preview, which the game's upload screen only lets you pick from inside StreamingAssets) and the data
    folders borders\ and places\.
    The game's assemblies are the installed game's (-ManagedDir: a saved copy of its Managed folder when the installed game is
    not the current main version). The DLL is checked against them and against R.A.T.S. Rigging with the workspace's
    tools\compat-check: nothing it uses may be missing.
    With -Install the staged folder also replaces <GameDir>\Sea Power_Data\StreamingAssets\SeaPowerBorders\, the folder to
    pick in the game's Create Mod screen. Sea Power must be closed for that. -Zip also writes dist\SeaPowerBorders-<version>.zip.
    The steps are the shared ones of every R.A.T.S. mod (SeaPowerRigging\build\RatsPackage.psm1); the workspace is found
    above this repo, -Workspace names it for a clone elsewhere, -RatsRigging another copy of the Rigging repo.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install
#>
param(
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { 'E:\Games\steamapps\common\Sea Power' }),
    [string]$Workspace = $env:RatsWorkspace,
    [string]$RatsRigging = $env:RatsRigging,
    # The game's assemblies when the installed game isn't the current main version (a saved copy of its Managed).
    [string]$ManagedDir = $env:ManagedDir,
    [switch]$Install,
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Workspace) {
    $Workspace = $root
    while ($Workspace -and -not (Test-Path (Join-Path $Workspace 'mods.json'))) { $Workspace = Split-Path -Parent $Workspace }
    if (-not $Workspace) { throw 'No workspace (a folder with mods.json) above this repo - pass -Workspace.' }
}
if (-not $RatsRigging) { $RatsRigging = Join-Path $Workspace 'SeaPowerRigging' }
Import-Module (Join-Path $RatsRigging 'build\RatsPackage.psm1') -Force

$name = 'SeaPowerBorders'
$project = Join-Path $root "src\$name\$name.csproj"
$dist = Join-Path $root "dist\$name"
if (-not $ManagedDir) { $ManagedDir = Join-Path $GameDir 'Sea Power_Data\Managed' }

Build-RatsMod -Project $project -Dist $dist -GameDir $GameDir -ManagedDir $ManagedDir -Workspace $Workspace -RiggingDir $RatsRigging | Out-Null
Test-RatsCompat -Workspace $Workspace -Dist $dist -RootNamespace $name -ManagedDir $ManagedDir -RiggingDir $RatsRigging
Copy-RatsCommon -Root $root -Dist $dist -InfoIni (Join-Path $root "src\$name\mod\_info.ini")
Copy-RatsRiggingCopy -Workspace $Workspace -Dist $dist -RiggingDir $RatsRigging
# The data the mod reads at run time: borders\borders.bin + eras.csv, places\world.csv + naval_bases.csv + eras.csv.
Copy-Item -Recurse (Join-Path $root "src\$name\mod\borders") (Join-Path $dist 'borders')
Copy-Item -Recurse (Join-Path $root "src\$name\mod\places") (Join-Path $dist 'places')
$version = Get-RatsModVersion -PluginFile (Join-Path $root "src\$name\Plugin.cs")
Write-Host "Staged Sea Power Borders $version in $dist"
Show-RatsStage -Dist $dist
if ($Zip) { New-RatsZip -Dist $dist -Version $version }
if ($Install) { Install-RatsMod -Dist $dist -GameDir $GameDir }
