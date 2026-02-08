param(
    [string]$Author = "stat0s2p",
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $root "DSP_CustomFastStart\DSP_CustomFastStart.csproj"
$thunderstoreDir = Join-Path $root "thunderstore"
$manifestPath = Join-Path $thunderstoreDir "manifest.json"
$readmePath = Join-Path $thunderstoreDir "README.md"
$changelogPath = Join-Path $thunderstoreDir "CHANGELOG.md"
$iconPath = Join-Path $thunderstoreDir "icon.png"

if (-not (Test-Path $manifestPath)) { throw "Missing manifest: $manifestPath" }
if (-not (Test-Path $readmePath)) { throw "Missing README: $readmePath" }
if (-not (Test-Path $iconPath)) { throw "Missing icon: $iconPath" }

$manifest = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
$packageName = [string]$manifest.name
$version = [string]$manifest.version_number
if ([string]::IsNullOrWhiteSpace($packageName)) { throw "manifest.name is empty." }
if ([string]::IsNullOrWhiteSpace($version)) { throw "manifest.version_number is empty." }

Write-Host "Building plugin ($Configuration)..."
dotnet build $projectFile -c $Configuration | Out-Host

$dllPath = Join-Path $root "DSP_CustomFastStart\bin\$Configuration\net472\DSP_CustomFastStart.dll"
if (-not (Test-Path $dllPath)) { throw "Build output missing: $dllPath" }

# Validate icon size: Thunderstore requires 256x256 png
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($iconPath)
try {
    if ($img.Width -ne 256 -or $img.Height -ne 256) {
        throw "icon.png must be 256x256, got ${($img.Width)}x${($img.Height)}."
    }
}
finally {
    $img.Dispose()
}

$buildDir = Join-Path $thunderstoreDir "build"
$distDir = Join-Path $thunderstoreDir "dist"
$stageDir = Join-Path $buildDir "package"

if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }
New-Item -ItemType Directory -Path $stageDir | Out-Null

Copy-Item $dllPath (Join-Path $stageDir "DSP_CustomFastStart.dll")
Copy-Item $manifestPath (Join-Path $stageDir "manifest.json")
Copy-Item $readmePath (Join-Path $stageDir "README.md")
if (Test-Path $changelogPath) {
    Copy-Item $changelogPath (Join-Path $stageDir "CHANGELOG.md")
}
Copy-Item $iconPath (Join-Path $stageDir "icon.png")

$zipName = "$Author-$packageName-$version.zip"
$zipPath = Join-Path $distDir $zipName
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath

Write-Host "Package created: $zipPath"
