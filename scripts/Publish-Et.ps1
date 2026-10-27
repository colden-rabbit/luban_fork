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

$publishedTemplates = [IO.Path]::GetFullPath((Join-Path $outputPath 'Templates'))
$outputPrefix = $outputPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $publishedTemplates.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Template directory is outside the publish directory: $publishedTemplates"
}
$templateItems = @((Get-Item -LiteralPath $publishedTemplates)) + @(Get-ChildItem -LiteralPath $publishedTemplates -Recurse -Force)
if (@($templateItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) {
    throw "Cannot prune templates through a reparse point: $publishedTemplates"
}
$requiredTemplates = @('cs-bin/bean.sbn', 'cs-bin/table.sbn', 'cs-bin/tables.sbn', 'common/cs/enum.sbn')
foreach ($relativePath in $requiredTemplates) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishedTemplates $relativePath) -PathType Leaf)) {
        throw "Required cs-bin template is missing: $relativePath"
    }
}
foreach ($file in $templateItems | Where-Object { -not $_.PSIsContainer }) {
    $relativePath = [IO.Path]::GetRelativePath($publishedTemplates, $file.FullName).Replace('\', '/')
    if ($relativePath -notin $requiredTemplates) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
}
foreach ($directory in $templateItems | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending) {
    if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0) {
        Remove-Item -LiteralPath $directory.FullName -Force
    }
}
Write-Output "ET Luban package: $outputPath"
