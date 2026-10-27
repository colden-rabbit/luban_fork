param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$publishArguments = @('publish', (Join-Path $sourceRoot 'src/Luban/Luban.csproj'),
    '-c', 'Release', '-o', $outputPath, '--disable-build-servers', '-p:UseSharedCompilation=false')
if ($NoRestore) {
    $publishArguments += '--no-restore'
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Luban publish failed with exit code $LASTEXITCODE"
}

$templateRoot = Join-Path $sourceRoot 'custom/et/Templates'
foreach ($template in Get-ChildItem -LiteralPath $templateRoot -Recurse -File) {
    $relativePath = [IO.Path]::GetRelativePath($templateRoot, $template.FullName)
    $destination = Join-Path $outputPath (Join-Path 'Templates' $relativePath)
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $template.FullName -Destination $destination -Force
}
Write-Output "ET Luban package: $outputPath"
