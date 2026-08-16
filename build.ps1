$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot "src\Flow.Launcher.Plugin.CustomBangs\Flow.Launcher.Plugin.CustomBangs.csproj"
$testProject = Join-Path $projectRoot "tests\Flow.Launcher.Plugin.CustomBangs.Tests\Flow.Launcher.Plugin.CustomBangs.Tests.csproj"
$manifestPath = Join-Path $projectRoot "src\Flow.Launcher.Plugin.CustomBangs\plugin.json"
$version = (Get-Content -Raw $manifestPath | ConvertFrom-Json).Version
$artifactRoot = Join-Path $projectRoot "artifacts"
$publishDirectory = Join-Path $artifactRoot "package-$version"
$zipPath = Join-Path $artifactRoot "Flow.Launcher.Plugin.CustomBangs-$version.zip"

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

dotnet restore $projectFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build $projectFile -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project $testProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet publish $projectFile -c Release --no-restore --no-build -o $publishDirectory
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The Flow Launcher plugin store expects the published files at the archive
# root, without an additional containing directory.
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $manifestEntry = $archive.Entries | Where-Object {
        ($_.FullName -replace "\\", "/") -eq "plugin.json"
    }
    if ($null -eq $manifestEntry) {
        throw "Package validation failed: plugin.json is missing from the archive root."
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Plugin package: $zipPath"
