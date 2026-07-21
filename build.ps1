param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$RunPreview
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath($PSScriptRoot)
$modProject = Join-Path $root "Mod\CheryFramework.csproj"
$previewProject = Join-Path $root "Preview\scr\code\CheryFramework.Preview.csproj"
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $root "artifacts"))

if ([System.IO.Path]::GetDirectoryName($artifacts) -ne $root) {
    throw "拒绝清理工作区之外的目录：$artifacts"
}

if (Test-Path -LiteralPath $artifacts) {
    Remove-Item -LiteralPath $artifacts -Recurse -Force
}
New-Item -ItemType Directory -Path $artifacts | Out-Null

Write-Host "[1/3] Build Mod artifacts" -ForegroundColor Cyan
dotnet build $modProject -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Mod build failed." }

Write-Host "[2/3] Publish Direct3D 12 Preview" -ForegroundColor Cyan
$previewOutput = [System.IO.Path]::GetFullPath((Join-Path $root "Preview\exe"))
if ([System.IO.Path]::GetDirectoryName($previewOutput) -ne [System.IO.Path]::GetFullPath((Join-Path $root "Preview"))) {
    throw "拒绝清理 Preview 之外的 EXE 目录：$previewOutput"
}
if (Test-Path -LiteralPath $previewOutput) {
    Remove-Item -LiteralPath $previewOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $previewOutput | Out-Null
dotnet publish $previewProject -c $Configuration -r win-x64 --self-contained true -o $previewOutput
if ($LASTEXITCODE -ne 0) { throw "Preview publish failed." }

$previewBuild = [System.IO.Path]::GetFullPath((Join-Path $root "build\Preview"))
if (Test-Path -LiteralPath $previewBuild) {
    Remove-Item -LiteralPath $previewBuild -Recurse -Force
}

Write-Host "[3/3] Collect UMM files" -ForegroundColor Cyan
$modOutput = Join-Path $root "Mod\out"
$modArtifact = Join-Path $artifacts "Mod"
Copy-Item -LiteralPath $modOutput -Destination $modArtifact -Recurse
Compress-Archive -Path (Join-Path $modArtifact "*") -DestinationPath (Join-Path $artifacts "CheryFramework-UMM.zip")

Write-Host "Build completed: $artifacts" -ForegroundColor Green

if ($RunPreview) {
    Start-Process -FilePath (Join-Path $previewOutput "CheryFramework.Preview.exe")
}
