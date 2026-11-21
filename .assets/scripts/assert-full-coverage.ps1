<#
.SYNOPSIS
Requires complete line, branch and executable-method coverage for both runtime assemblies.
.PARAMETER CoverageDirectory
Fresh Coverlet results directory containing both assembly-specific test runs.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CoverageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Count([System.Xml.XmlElement]$Element, [string]$Name) {
    [long]$value = 0
    if (-not [long]::TryParse($Element.GetAttribute($Name), [ref]$value) -or $value -lt 0) {
        throw "Missing or invalid coverage count '$Name'."
    }
    return $value
}

$expectedAssemblies = @('SwiftCollections', 'SwiftCollections.FixedMathSharp')
$foundAssemblies = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$reports = @(Get-ChildItem -LiteralPath $CoverageDirectory -Filter coverage.cobertura.xml -File -Recurse | Sort-Object FullName)
if ($reports.Count -eq 0) { throw 'No Cobertura coverage reports found.' }

foreach ($report in $reports) {
    [xml]$document = Get-Content -LiteralPath $report.FullName -Raw
    $coverage = $document.DocumentElement
    if ($coverage.Name -ne 'coverage') { throw "Invalid Cobertura report: $($report.FullName)" }
    $packages = @($coverage.SelectNodes('packages/package') | Where-Object { $_.GetAttribute('name') -cin $expectedAssemblies })
    if ($packages.Count -eq 0) { throw "Report contains neither runtime assembly: $($report.FullName)" }

    $lines = Read-Count $coverage 'lines-valid'
    $coveredLines = Read-Count $coverage 'lines-covered'
    $branches = Read-Count $coverage 'branches-valid'
    $coveredBranches = Read-Count $coverage 'branches-covered'
    if ($lines -le 0 -or $branches -le 0 -or $coveredLines -ne $lines -or $coveredBranches -ne $branches) {
        throw "Coverage gate failed in $($report.FullName): lines $coveredLines/$lines; branches $coveredBranches/$branches."
    }

    foreach ($package in $packages) {
        $name = $package.GetAttribute('name')
        $methods = @($package.SelectNodes('classes/class/methods/method[lines/line]'))
        if ($methods.Count -eq 0) { throw "No executable methods reported for $name." }
        foreach ($method in $methods) {
            $covered = $false
            foreach ($line in $method.SelectNodes('lines/line')) {
                if ((Read-Count $line 'hits') -gt 0) { $covered = $true }
            }
            if (-not $covered) { throw "Uncovered method in ${name}: $($method.GetAttribute('name'))" }
        }
        $null = $foundAssemblies.Add($name)
        Write-Output "${name}: full line/branch coverage; $($methods.Count) executable methods covered."
    }
}

foreach ($name in $expectedAssemblies) {
    if (-not $foundAssemblies.Contains($name)) { throw "Missing coverage for $name." }
}
