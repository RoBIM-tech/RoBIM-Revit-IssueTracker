param([string]$ManagerPath,
    [Parameter(Mandatory=$true)][string]$CommonFeed,
    [string]$RevitPath = "$env:ProgramW6432/Autodesk/Revit 2026")
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
python (Join-Path $PSScriptRoot 'scripts/validate_button.py') --source $PSScriptRoot --host revit --repository RoBIM-tech/RoBIM-Revit-IssueTracker
if ($LASTEXITCODE -ne 0) { throw 'Self-described metadata validation failed' }
if ($ManagerPath) {
    python (Join-Path $ManagerPath 'scripts/validate_registration.py') --source $PSScriptRoot --host revit --repository RoBIM-tech/RoBIM-Revit-IssueTracker
    if ($LASTEXITCODE -ne 0) { throw 'Manager catalog validation failed' }
}
$runtime = Get-Content -LiteralPath (Join-Path $RevitPath 'RevitNET.runtimeconfig.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($runtime.runtimeOptions.tfm -ne 'net10.0') { throw 'Revit 2026.5+ / .NET 10 required' }
$CommonFeed = [IO.Path]::GetFullPath($CommonFeed)
$metadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package/robim-module.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$stage = Join-Path $PSScriptRoot ('artifacts/staging/' + [guid]::NewGuid().ToString('N'))
$bundle = Join-Path $stage 'RoBIM.Feature.IssueTracker.bundle'
$contents = Join-Path $bundle 'Contents/2026'
dotnet publish (Join-Path $PSScriptRoot 'src/Feature.csproj') -c Release "-p:RevitPath=$RevitPath" "-p:Version=$($metadata.version)" --source 'https://api.nuget.org/v3/index.json' --source $CommonFeed -o $contents
if ($LASTEXITCODE -ne 0) { throw 'Native feature build failed' }
if (Get-ChildItem -LiteralPath $contents -File | Where-Object { $_.Name -match '^RevitAPI.*\.dll$' }) { throw 'Autodesk DLL redistribution forbidden' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package/robim-module.json') -Destination $bundle
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package/robim-release.json') -Destination $bundle
if ($metadata.version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid package version directory' }
$artifactDirectory = Join-Path $PSScriptRoot ('artifacts/' + $metadata.version)
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$zip = Join-Path $artifactDirectory 'RoBIM.Feature.IssueTracker-Revit2026.zip'
Compress-Archive -LiteralPath $bundle -DestinationPath $zip -Force
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
Write-Output $zip
