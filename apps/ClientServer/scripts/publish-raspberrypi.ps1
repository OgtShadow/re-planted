param(
    [ValidateSet("linux-arm64", "linux-arm")]
    [string]$Runtime = "linux-arm64",
    [string]$OutputDirectory = "publish\raspberrypi"
)

$ErrorActionPreference = "Stop"
$projectDirectory = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectDirectory "ClientServer.csproj"
$outputPath = Join-Path $projectDirectory $OutputDirectory

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

dotnet publish $projectFile `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $outputPath

Remove-Item (Join-Path $outputPath "data") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $outputPath "re-planted_clientserver_collection") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $outputPath "appsettings.Development.json") -Force -ErrorAction SilentlyContinue

Copy-Item (Join-Path $projectDirectory "appsettings.json") $outputPath
Copy-Item (Join-Path $projectDirectory "deploy\clientserver.raspberrypi.env.example") $outputPath
Copy-Item (Join-Path $projectDirectory "deploy\re-planted-clientserver.service") $outputPath
Copy-Item (Join-Path $projectDirectory "deploy\install-native-debian.sh") $outputPath
New-Item -ItemType Directory -Path (Join-Path $outputPath "scripts") -Force | Out-Null
Copy-Item (Join-Path $projectDirectory "scripts\smoke-test.sh") (Join-Path $outputPath "scripts")

$archivePath = Join-Path $projectDirectory ("publish\re-planted-clientserver-{0}.zip" -f $Runtime)
if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}
Compress-Archive -Path (Join-Path $outputPath "*") -DestinationPath $archivePath

Write-Host "Published self-contained application: $outputPath"
Write-Host "Executable: $(Join-Path $outputPath 'ClientServer')"
Write-Host "Transfer archive: $archivePath"
