param([ValidateSet('win-x64','win-arm64')][string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
  $out=Join-Path $PSScriptRoot "dist/1.1.1/$Runtime"
  dotnet run --project Tests/SalaryBeat.Tests.csproj -c Release
  if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
  dotnet publish Desktop/SalaryBeat.Desktop.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out
  if($LASTEXITCODE -ne 0){throw 'Windows publish failed'}
  Copy-Item ../LICENSE (Join-Path $out 'LICENSE.SalaryGlow')
  Copy-Item README.md (Join-Path $out 'README.md')
  Copy-Item THIRD_PARTY_NOTICES.md (Join-Path $out 'THIRD_PARTY_NOTICES.md')
  foreach($folder in @('licenses','docs')) {
    New-Item -ItemType Directory -Path (Join-Path $out $folder) -Force | Out-Null
    Copy-Item "$folder/*" (Join-Path $out $folder) -Recurse -Force
  }
  Write-Output (Join-Path $out 'SalaryBeat.exe')
} finally { Pop-Location }
