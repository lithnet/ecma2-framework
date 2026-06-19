<#
.SYNOPSIS
    Builds and packs the three Lithnet ECMA2 framework packages as a LOCAL prerelease (<VersionPrefix>-dev.<N>) into
    the repo's local NuGet feed, bumping a persistent counter on every run.

.DESCRIPTION
    The three packable framework projects carry a <VersionPrefix> (no fixed <Version>); this script reads that prefix
    from the holding project and supplies -p:VersionSuffix=dev.<N>, producing a prerelease <VersionPrefix>-dev.<N>.

    The point is the local dev loop: NuGet extracts a given package id+version into the global cache exactly once and
    never re-extracts an already-present version. A fixed prefix alone therefore caches the first local pack forever,
    so a framework change never reaches a package-consuming consumer (e.g. the Okta MA) until the cache is manually
    evicted. A monotonically increasing -dev.<N> sidesteps the cache entirely: every pack is a NEW version the
    consumer's floating reference (<VersionPrefix>-dev.*) re-resolves on a normal `dotnet build`, with no cache games.

    The counter is a single integer persisted in src\build\.local-pack-counter (gitignored). It starts at 0,
    is incremented on every run, and is written back before packing so each run produces a distinct N. All three
    packages produced in ONE run share the SAME N so the Sdk / Hosting / holding versions match.

.NOTES
    Run after editing the framework. Build/pack uses the local 'dotnet'. Produces packages into <repo>\artifacts\nupkg,
    the local feed both the in-repo PackageTests consumer and the side-by-side Okta MA restore from.
#>
[CmdletBinding()]
param(
    # The build configuration to pack. Debug by default so the sibling DLLs the holding package bundles
    # (the generator, the Serialization assembly) exist in their Debug output, matching a normal dev build.
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

# src\build\ -> src\ -> repo root.
$scriptDir = $PSScriptRoot
$srcDir = Split-Path -Parent $scriptDir
$repoRoot = Split-Path -Parent $srcDir

$counterFile = Join-Path $scriptDir '.local-pack-counter'
$nupkgOutputDir = Join-Path $repoRoot 'artifacts\nupkg'
$solution = Join-Path $srcDir 'Lithnet.Ecma2Framework.sln'

$packableProjects = @(
    Join-Path $srcDir 'Lithnet.Ecma2Framework.Sdk\Lithnet.Ecma2Framework.Sdk.csproj'
    Join-Path $srcDir 'Lithnet.Ecma2Framework.Hosting\Lithnet.Ecma2Framework.Hosting.csproj'
    Join-Path $srcDir 'Lithnet.Ecma2Framework\Lithnet.Ecma2Framework.csproj'
)

# Read the persistent counter (default 0 when the file is absent or unparseable), increment, and write it back
# BEFORE packing so a distinct N is committed even if a later pack step fails.
$counter = 0
if (Test-Path $counterFile)
{
    $raw = (Get-Content -Path $counterFile -Raw).Trim()
    $parsed = 0
    if ([int]::TryParse($raw, [ref]$parsed))
    {
        $counter = $parsed
    }
    else
    {
        Write-Warning "Counter file '$counterFile' did not contain an integer ('$raw'); resetting to 0."
    }
}

$counter++
Set-Content -Path $counterFile -Value $counter -NoNewline

$versionSuffix = "dev.$counter"
# Derive the prefix from the holding project's VersionPrefix so this script never needs editing on a version bump.
$holdingCsproj = Join-Path $srcDir 'Lithnet.Ecma2Framework\Lithnet.Ecma2Framework.csproj'
$versionPrefix = ([regex]::Match((Get-Content -Raw $holdingCsproj), '<VersionPrefix>([^<]+)</VersionPrefix>')).Groups[1].Value
$version = "$versionPrefix-$versionSuffix"

Write-Host "Local pack version: $version (counter=$counter)" -ForegroundColor Cyan

# Build the whole solution first so every sibling DLL the packages need exists (the Sdk/Hosting assemblies, the
# generator the holding package bundles into build\tools\, the Serialization assembly Hosting bundles into lib\).
Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0)
{
    throw "Solution build failed (exit code $LASTEXITCODE)."
}

# Pack all three projects with the SAME VersionSuffix so the Sdk / Hosting / holding versions match. --no-build:
# the solution build above already produced every output, and packing with VersionSuffix here just versions the
# already-built assemblies into the package.
foreach ($project in $packableProjects)
{
    Write-Host "Packing $([System.IO.Path]::GetFileName($project))..." -ForegroundColor Cyan
    & dotnet pack $project -c $Configuration -o $nupkgOutputDir --no-build -p:VersionSuffix=$versionSuffix
    if ($LASTEXITCODE -ne 0)
    {
        throw "Pack failed for '$project' (exit code $LASTEXITCODE)."
    }
}

Write-Host ""
Write-Host "Packed $version into $nupkgOutputDir" -ForegroundColor Green
Write-Host "Consumers referencing Lithnet.Ecma2Framework Version='$versionPrefix-dev.*' will resolve this on the next build." -ForegroundColor Green
