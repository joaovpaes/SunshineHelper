<#
.SYNOPSIS
    Phase 1 gate, item 5 (docs/fase1-portao.md): line coverage of SunshineHelper.Domain and
    SunshineHelper.Application must be at least the minimum.

.DESCRIPTION
    Reads every Cobertura file the test run produced. Each test project reports the
    assemblies it touched, so the same line can appear in several files: a line counts as
    covered if any test project hit it. Writes a table to the job summary and fails when an
    assembly is below the minimum.
#>
param(
    [string] $ResultsDirectory = 'TestResults',
    [double] $Minimum = 80,
    [string[]] $Assemblies = @('SunshineHelper.Domain', 'SunshineHelper.Application')
)

$ErrorActionPreference = 'Stop'

$files = Get-ChildItem -Path $ResultsDirectory -Recurse -Filter 'coverage.cobertura.xml'
if ($files.Count -eq 0) {
    Write-Error "Nenhum arquivo de cobertura encontrado em '$ResultsDirectory'."
}

# assembly -> (file:line -> covered)
$lines = @{}
foreach ($assembly in $Assemblies) { $lines[$assembly] = @{} }

foreach ($file in $files) {
    [xml] $xml = Get-Content -Raw -Path $file.FullName
    # Coverlet writes class file names relative to a <source> root in some reports and as
    # full paths in others; the same file must count once, so every name is made full.
    $roots = @($xml.coverage.sources.source | Where-Object { $_ })
    foreach ($package in $xml.coverage.packages.package) {
        if (-not $lines.ContainsKey($package.name)) { continue }
        $seen = $lines[$package.name]
        foreach ($class in $package.classes.class) {
            $path = $class.filename
            if (-not [System.IO.Path]::IsPathRooted($path)) {
                foreach ($root in $roots) {
                    $candidate = Join-Path $root $path
                    if (Test-Path -LiteralPath $candidate) { $path = $candidate; break }
                }
            }
            $path = [System.IO.Path]::GetFullPath($path).ToLowerInvariant()
            foreach ($line in $class.lines.line) {
                $key = "$($path):$($line.number)"
                $hit = [int] $line.hits -gt 0
                if ($seen.ContainsKey($key)) { $seen[$key] = $seen[$key] -or $hit }
                else { $seen[$key] = $hit }
            }
        }
    }
}

$failed = $false
$summary = @('| Assembly | Linhas cobertas | Total | Cobertura |', '| --- | ---: | ---: | ---: |')
foreach ($assembly in $Assemblies) {
    $seen = $lines[$assembly]
    $total = $seen.Count
    $covered = @($seen.Values | Where-Object { $_ }).Count
    $percent = if ($total -eq 0) { 0 } else { [math]::Round(100 * $covered / $total, 1) }
    $summary += "| $assembly | $covered | $total | $percent% |"
    Write-Host "$assembly`: $covered/$total linhas ($percent%)"
    # Where the missing lines are, so the next tests can go straight there.
    $seen.GetEnumerator() |
        Where-Object { -not $_.Value } |
        Group-Object { $_.Key.Substring(0, $_.Key.LastIndexOf(':')) } |
        Sort-Object Count -Descending |
        Select-Object -First 15 |
        ForEach-Object { Write-Host ("    {0,4} sem teste  {1}" -f $_.Count, $_.Name) }

    if ($total -eq 0 -or $percent -lt $Minimum) {
        Write-Host "::error::$assembly com $percent% de cobertura, abaixo do minimo de $Minimum%."
        $failed = $true
    }
}

if ($env:GITHUB_STEP_SUMMARY) {
    ("## Cobertura (mínimo $Minimum%)", '') + $summary | Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding utf8
}

if ($failed) { exit 1 }
