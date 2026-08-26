<#
.SYNOPSIS
Provides shared build, version, and release-plan functions.

.DESCRIPTION
This script is intended to be dot-sourced by release scripts and workflows. It
locates the solution, loads GitVersion or explicit version values, builds a
selected project, resolves local options or release tags into package-family
plans, and rejects overlapping same-version tags.

.NOTES
Available functions and parameters:
- Get-SolutionDirectory: StartPath, SolutionPath
- Ensure-GitVersion-Environment: no parameters
- Set-ReleaseVersionEnvironment: Version
- Build-Project: ProjectPath, Configuration, Properties
- New-ReleasePlan: PackageFamily, Version, UseGitVersion
- Resolve-ReleasePlan: PackageFamily and Version, or Tag
- Assert-NoReleaseTagCollision: ReleasePlan, CurrentTag, RepositoryTags

Resolve-ReleasePlan accepts All, SwiftCollections, or
SwiftCollections.FixedMathSharp for local releases. Legacy all-family tag
values may be X.Y.Z, vX.Y.Z, or VX.Y.Z; scoped tags must be
SwiftCollections/vX.Y.Z or SwiftCollections.FixedMathSharp/vX.Y.Z.
#>

function Get-SolutionDirectory {
    param (
		[string]$StartPath = $(Get-Location),
		[string]$SolutionPath = "SwiftCollections.slnx"
	)

    $currentPath = $StartPath
    while ($true) {
        if (Test-Path (Join-Path $currentPath $SolutionPath)) {
            return $currentPath
        }
        $parent = [System.IO.Directory]::GetParent($currentPath)
        if ($parent -eq $null) { break }
        $currentPath = $parent.FullName
    }
    throw "Solution directory not found."
}

function Ensure-GitVersion-Environment {
	# Ensure GitVersion is installed and available
	if (-not (Get-Command "dotnet-gitversion" -ErrorAction SilentlyContinue)) {
		throw "GitVersion is not installed. Install it with: dotnet tool install -g GitVersion.Tool"
	}

    Write-Host "Fetching version information using GitVersion..."

    # Capture GitVersion output as JSON and convert it to PowerShell objects
    $gitVersionJson = & dotnet-gitversion -output json
    if ($LASTEXITCODE -ne 0) {
        throw "GitVersion failed with exit code $LASTEXITCODE."
    }

    $gitVersionOutput = $gitVersionJson | ConvertFrom-Json -ErrorAction Stop

    if ($null -eq $gitVersionOutput) {
        throw "GitVersion returned no version information."
    }

    # Extract key version properties
    $semVer = $gitVersionOutput.MajorMinorPatch
    $assemblySemVer = $gitVersionOutput.AssemblySemVer
    $assemblySemFileVer = $gitVersionOutput.AssemblySemFileVer
    $infoVersion = $gitVersionOutput.InformationalVersion

    # Set environment variables for the build process
    [System.Environment]::SetEnvironmentVariable('GitVersion_FullSemVer', $semVer, 'Process')
    [System.Environment]::SetEnvironmentVariable('GitVersion_AssemblySemVer', $assemblySemVer, 'Process')
    [System.Environment]::SetEnvironmentVariable('GitVersion_AssemblySemFileVer', $assemblySemFileVer, 'Process')
    [System.Environment]::SetEnvironmentVariable('GitVersion_InformationalVersion', $infoVersion, 'Process')

    Write-Host "Environment variables set:"
    Write-Host "  GitVersion_FullSemVer = $semVer"
    Write-Host "  GitVersion_AssemblySemVer = $assemblySemVer"
    Write-Host "  GitVersion_AssemblySemFileVer = $assemblySemFileVer"
    Write-Host "  GitVersion_InformationalVersion = $infoVersion"
}

function Set-ReleaseVersionEnvironment {
    param (
        [Parameter(Mandatory)]
        [string]$Version
    )

    $assemblyVersion = "$Version.0"
    [System.Environment]::SetEnvironmentVariable('GitVersion_FullSemVer', $Version, 'Process')
    [System.Environment]::SetEnvironmentVariable('GitVersion_AssemblySemVer', $assemblyVersion, 'Process')
    [System.Environment]::SetEnvironmentVariable('GitVersion_AssemblySemFileVer', $assemblyVersion, 'Process')
    [System.Environment]::SetEnvironmentVariable('GitVersion_InformationalVersion', $Version, 'Process')
}

function Build-Project {
    [CmdletBinding()]
    param (
        [Alias("SolutionPath")]
        [string]$ProjectPath = "SwiftCollections.slnx",

        [string]$Configuration = "Release",

        [hashtable]$Properties = @{}
    )

    $propertyArguments = @(
        foreach ($propertyName in ($Properties.Keys | Sort-Object)) {
            "-p:${propertyName}=$($Properties[$propertyName])"
        }
    )

    Write-Host "Cleaning $ProjectPath in $Configuration mode..."
    & dotnet clean $ProjectPath -c $Configuration @propertyArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Clean failed for '$ProjectPath' in '$Configuration'."
    }

    Write-Host "Building $ProjectPath in $Configuration mode..."
    & dotnet build $ProjectPath -c $Configuration @propertyArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for '$ProjectPath' in '$Configuration'."
    }

    Write-Host "Build succeeded!" -ForegroundColor Green
}

function New-ReleasePlan {
    param (
        [Parameter(Mandatory)]
        [string]$PackageFamily,

        [AllowNull()]
        [string]$Version,

        [bool]$UseGitVersion = $false
    )

    switch -CaseSensitive ($PackageFamily) {
        "All" {
            $projects = @("SwiftCollections", "SwiftCollections.FixedMathSharp")
        }
        "SwiftCollections" {
            $projects = @("SwiftCollections")
        }
        "SwiftCollections.FixedMathSharp" {
            $projects = @("SwiftCollections.FixedMathSharp")
        }
        default {
            throw [System.ArgumentException]::new("Unsupported package family '$PackageFamily'.", "PackageFamily")
        }
    }

    [pscustomobject]@{
        PackageFamily = $PackageFamily
        Version = $Version
        UseGitVersion = $UseGitVersion
        UsePublishedSwiftCollections = $PackageFamily -ceq "SwiftCollections.FixedMathSharp"
        Projects = $projects
    }
}

function Resolve-ReleasePlan {
    [CmdletBinding(DefaultParameterSetName = "Local")]
    param (
        [Parameter(Mandatory, ParameterSetName = "Tag")]
        [string]$Tag,

        [Parameter(ParameterSetName = "Local")]
        [string]$PackageFamily = "All",

        [Parameter(ParameterSetName = "Local")]
        [string]$Version
    )

    $stableVersionPattern = '(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)'

    if ($PSCmdlet.ParameterSetName -eq "Local") {
        if (@("All", "SwiftCollections", "SwiftCollections.FixedMathSharp") -cnotcontains $PackageFamily) {
            throw [System.ArgumentException]::new("Unsupported package family '$PackageFamily'.", "PackageFamily")
        }
        if ($PackageFamily -ceq "All" -and [string]::IsNullOrWhiteSpace($Version)) {
            return New-ReleasePlan -PackageFamily "All" -Version $null -UseGitVersion $true
        }
        if ([string]::IsNullOrWhiteSpace($Version)) {
            throw [System.ArgumentException]::new("A stable version is required for a scoped release.", "Version")
        }
        if ($Version -cnotmatch "\A$stableVersionPattern\z") {
            throw [System.ArgumentException]::new("Invalid stable release version '$Version'.", "Version")
        }

        return New-ReleasePlan -PackageFamily $PackageFamily -Version $Version
    }

    if ($Tag -cmatch "\A[vV]?(?<Version>$stableVersionPattern)\z") {
        $resolvedFamily = "All"
    } elseif ($Tag -cmatch "\ASwiftCollections/v(?<Version>$stableVersionPattern)\z") {
        $resolvedFamily = "SwiftCollections"
    } elseif ($Tag -cmatch "\ASwiftCollections\.FixedMathSharp/v(?<Version>$stableVersionPattern)\z") {
        $resolvedFamily = "SwiftCollections.FixedMathSharp"
    } else {
        throw [System.ArgumentException]::new("Unsupported release tag '$Tag'.", "Tag")
    }

    New-ReleasePlan -PackageFamily $resolvedFamily -Version $Matches.Version
}

function Assert-NoReleaseTagCollision {
    param (
        [Parameter(Mandatory)]
        [psobject]$ReleasePlan,

        [Parameter(Mandatory)]
        [string]$CurrentTag,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [string[]]$RepositoryTags
    )

    foreach ($repositoryTag in $RepositoryTags) {
        if ($repositoryTag -ceq $CurrentTag) {
            continue
        }

        try {
            $otherPlan = Resolve-ReleasePlan -Tag $repositoryTag
        } catch [System.ArgumentException] {
            continue
        }

        if ($otherPlan.Version -cne $ReleasePlan.Version) {
            continue
        }

        $overlappingProjects = @(
            $ReleasePlan.Projects |
                Where-Object { $otherPlan.Projects -ccontains $_ }
        )
        if ($overlappingProjects.Count -ne 0) {
            throw "Release tag '$CurrentTag' overlaps '$repositoryTag' at version '$($ReleasePlan.Version)' for $($overlappingProjects -join ', ')."
        }
    }
}
