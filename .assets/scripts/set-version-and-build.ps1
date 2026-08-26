<#
.SYNOPSIS
Builds release artifacts for all packages or one paired package family.

.DESCRIPTION
Builds the selected projects in Release and ReleaseLean, then creates one ZIP
per target framework and configuration under artifacts/releases. With no
arguments, all packages use the version reported by GitVersion. Scoped family
builds require an explicit stable three-part version and companion builds use
the published SwiftCollections dependency configured by the companion project.

.PARAMETER PackageFamily
Selects the release unit. Accepted values are All, SwiftCollections, and
SwiftCollections.FixedMathSharp. Standard and Lean packages always release
together within the selected family. The default is All.

.PARAMETER Version
Sets an explicit stable version in X.Y.Z form. It is required for a scoped
family and optional for All. When omitted for All, GitVersion supplies the
version.

.EXAMPLE
.\.assets\scripts\set-version-and-build.ps1

Builds all four packages using GitVersion.

.EXAMPLE
.\.assets\scripts\set-version-and-build.ps1 -PackageFamily SwiftCollections.FixedMathSharp -Version 7.1.0

Builds only SwiftCollections.FixedMathSharp and its Lean variant at 7.1.0.

.EXAMPLE
.\.assets\scripts\set-version-and-build.ps1 -PackageFamily SwiftCollections -Version 7.1.0

Builds only SwiftCollections and its Lean variant at 7.1.0.
#>
[CmdletBinding()]
param (
    [ValidateSet("All", "SwiftCollections", "SwiftCollections.FixedMathSharp")]
    [string]$PackageFamily = "All",

    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$startingLocation = Get-Location
$stagingDir = $null

try {
    . (Join-Path $PSScriptRoot "utilities.ps1")

    $releasePlan = Resolve-ReleasePlan -PackageFamily $PackageFamily -Version $Version
    $solutionDir = Get-SolutionDirectory -StartPath $PSScriptRoot
    Set-Location $solutionDir

    if ($releasePlan.UseGitVersion) {
        Ensure-GitVersion-Environment
    } else {
        Set-ReleaseVersionEnvironment -Version $releasePlan.Version
    }

    $configurations = @("Release", "ReleaseLean")
    $buildProperties = @{}
    if ($releasePlan.UsePublishedSwiftCollections) {
        $buildProperties.UsePublishedSwiftCollections = "true"
    }

    foreach ($config in $configurations) {
        foreach ($projectName in $releasePlan.Projects) {
            $configurationOutput = Join-Path $solutionDir "src\$projectName\bin\$config"
            if (Test-Path -LiteralPath $configurationOutput) {
                Remove-Item -LiteralPath $configurationOutput -Recurse -Force
            }
        }

        if ($releasePlan.PackageFamily -eq "All") {
            Build-Project `
                -ProjectPath (Join-Path $solutionDir "SwiftCollections.slnx") `
                -Configuration $config `
                -Properties $buildProperties
            continue
        }

        foreach ($projectName in $releasePlan.Projects) {
            $projectPath = Join-Path $solutionDir "src\$projectName\$projectName.csproj"
            Build-Project `
                -ProjectPath $projectPath `
                -Configuration $config `
                -Properties $buildProperties
        }
    }

    $archiveRoot = Join-Path $solutionDir "artifacts"
    $archiveOutputDir = Join-Path $archiveRoot "releases"
    $stagingDir = Join-Path $archiveRoot ".release-staging-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

    foreach ($config in $configurations) {
        $configLabel = $config.ToLowerInvariant()

        foreach ($projectName in $releasePlan.Projects) {
            $releaseDir = Join-Path $solutionDir "src\$projectName\bin\$config"
            $frameworkDirs = @(Get-ChildItem -LiteralPath $releaseDir -Directory)
            if ($frameworkDirs.Count -eq 0) {
                throw "No target-framework output was produced: $releaseDir"
            }

            foreach ($frameworkDir in $frameworkDirs) {
                $frameworkName = $frameworkDir.Name
                $targetDir = $frameworkDir.FullName
                $archiveItems = @(Get-ChildItem -LiteralPath $targetDir -Force)
                if ($archiveItems.Count -eq 0) {
                    throw "Release output is empty: $targetDir"
                }

                $zipFileName = "${projectName}-v$($Env:GitVersion_FullSemVer)-${frameworkName}-${configLabel}.zip"
                $stagedZipPath = Join-Path $stagingDir $zipFileName
                Compress-Archive -LiteralPath $archiveItems.FullName -DestinationPath $stagedZipPath -Force

                if (-not (Test-Path -LiteralPath $stagedZipPath -PathType Leaf)) {
                    throw "Failed to create archive: $stagedZipPath"
                }
            }
        }
    }

    New-Item -ItemType Directory -Path $archiveOutputDir -Force | Out-Null
    foreach ($stagedArchive in (Get-ChildItem -LiteralPath $stagingDir -File)) {
        $destination = Join-Path $archiveOutputDir $stagedArchive.Name
        Move-Item -LiteralPath $stagedArchive.FullName -Destination $destination -Force
        Write-Host "Created archive: $destination"
    }

    Write-Host "Release family '$($releasePlan.PackageFamily)' $($Env:GitVersion_FullSemVer) built successfully." -ForegroundColor Green
} finally {
    if ($null -ne $stagingDir -and (Test-Path -LiteralPath $stagingDir)) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force
    }
    Set-Location $startingLocation
}
