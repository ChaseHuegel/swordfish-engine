<#
.SYNOPSIS
    Packages Wayward Beyond Asset Studio and AssetTools CLI as a self-contained folder archive.
.DESCRIPTION
    Produces a self-contained folder distribution (.zip for win-x64, .tar.gz for linux-x64).
    No single-file build, no installer, and no auto-update.
#>
param(
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Rid = "win-x64",

    [string]$Version = "0.1.0",

    [string]$OutputDir = "",

    [switch]$CliOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts/asset-studio"
}

$artifactName = if ($CliOnly) {
    "wayward-beyond-asset-tools-cli-$Version-$Rid"
} else {
    "wayward-beyond-asset-studio-$Version-$Rid"
}

$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("wb-package-" + [Guid]::NewGuid().ToString("N"))
$bundleDir = Join-Path $stageRoot $artifactName

try {
    New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

    $publishArgs = @(
        "-c", "Release",
        "-r", $Rid,
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "-p:ErrorOnDuplicatePublishOutputFiles=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:Version=$Version",
        "--nologo",
        "-v", "minimal"
    )

    if (-not $CliOnly) {
        $studioProject = Join-Path $repoRoot "WaywardBeyond.AssetStudio/WaywardBeyond.AssetStudio.csproj"
        if (-not (Test-Path $studioProject)) {
            throw "Missing AssetStudio project: $studioProject"
        }

        Write-Host "Publishing WaywardBeyond.AssetStudio ($Rid)..."
        & dotnet publish $studioProject @publishArgs -o $bundleDir
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for WaywardBeyond.AssetStudio (exit $LASTEXITCODE)."
        }
    }

    $cliProject = Join-Path $repoRoot "WaywardBeyond.AssetTools.Cli/WaywardBeyond.AssetTools.Cli.csproj"
    Write-Host "Publishing WaywardBeyond.AssetTools.Cli ($Rid)..."
    & dotnet publish $cliProject @publishArgs -o $bundleDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for WaywardBeyond.AssetTools.Cli (exit $LASTEXITCODE)."
    }

    foreach ($relPath in @("qps-ploc", "manifest.toml", "assets")) {
        $unwanted = Join-Path $bundleDir $relPath
        if (Test-Path $unwanted) {
            Remove-Item -Recurse -Force $unwanted
        }
    }

    $readmeContent = @"
Wayward Beyond Asset Studio $Version ($Rid)
===============================================

Self-contained folder distribution. No installer and no auto-update.

Supported platforms:
- Windows 10 x64 (win-x64)
- Linux x64, glibc 2.35 or newer (linux-x64)

Linux runtime library dependencies:
- libgl1
- libx11-6
- libfontconfig1
- libgtk-3-0

Portable mode:
Create an empty file named "portable" beside the executable to store settings
and drafts in this folder instead of the per-user data directory.

CLI commands:
  WaywardBeyond.AssetTools.Cli validate <root> [--install <dir>] [--json]
  WaywardBeyond.AssetTools.Cli roundtrip <root> [--check] [--json]
  WaywardBeyond.AssetTools.Cli format <root> [--json]
  WaywardBeyond.AssetTools.Cli new pack <dir> [--json]
  WaywardBeyond.AssetTools.Cli ids <root> [--install <dir>] [--json]
"@
    Set-Content -Path (Join-Path $bundleDir "README.txt") -Value $readmeContent -Encoding UTF8

    if ($Rid -eq "win-x64") {
        $archivePath = Join-Path $OutputDir "$artifactName.zip"
        if (Test-Path $archivePath) {
            Remove-Item -Force $archivePath
        }

        Compress-Archive -Path $bundleDir -DestinationPath $archivePath -Force
    } else {
        $archivePath = Join-Path $OutputDir "$artifactName.tar.gz"
        if (Test-Path $archivePath) {
            Remove-Item -Force $archivePath
        }

        & tar -czf $archivePath -C $stageRoot $artifactName
        if ($LASTEXITCODE -ne 0) {
            throw "tar failed creating $archivePath (exit $LASTEXITCODE)."
        }
    }

    Write-Host "Created archive: $archivePath"
}
finally {
    if (Test-Path $stageRoot) {
        Remove-Item -Recurse -Force $stageRoot
    }
}
