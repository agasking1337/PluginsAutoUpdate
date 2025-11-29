$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Set-Location $projectRoot

# Publish in Release so build/publish/PluginsAutoUpdate is created
dotnet publish -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed. Aborting copy."
    exit 1
}

# Source is the publish output folder for this plugin
$sourcePath = Join-Path $projectRoot "build\publish\PluginsAutoUpdate"
$targetRoot = "C:\Users\desktop\Desktop\PANEL\servers\1\serverfiles\game\csgo\addons\swiftlys2\plugins"

if (-not (Test-Path $sourcePath)) {
    Write-Error "Source path '$sourcePath' does not exist. Ensure the project built/published correctly."
    exit 1
}

if (-not (Test-Path $targetRoot)) {
    New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
}

Copy-Item -Path $sourcePath -Destination $targetRoot -Recurse -Force
Write-Host "Copied '$sourcePath' to '$targetRoot'"
