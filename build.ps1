param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) { $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$refs = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Xml.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$common = @('/nologo','/optimize+','/codepage:65001') + $refs + @(
 ('/resource:'+(Join-Path $root 'src\Main.xaml')+',Main.xaml'),
 ('/resource:'+(Join-Path $root 'src\Settings.xaml')+',Settings.xaml'),
 ('/resource:'+(Join-Path $root 'src\App.ico')+',App.ico'),
 ('/win32icon:'+(Join-Path $root 'src\App.ico')),
 ('/resource:'+(Join-Path $root 'src\Discreet.xaml')+',Discreet.xaml'),
 (Join-Path $root 'src\Calendar.cs'),
 (Join-Path $root 'src\ExchangeRates.cs'),
 (Join-Path $root 'src\App.cs')
)
& (Join-Path $framework 'csc.exe') '/target:winexe' ('/out:'+(Join-Path $OutputDirectory 'SalaryGlow-v5.exe')) @common
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
& (Join-Path $framework 'csc.exe') '/target:exe' ('/out:'+(Join-Path $OutputDirectory 'SalaryGlow.Tests.exe')) @common
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
& (Join-Path $OutputDirectory 'SalaryGlow.Tests.exe') --test
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
Write-Output (Join-Path $OutputDirectory 'SalaryGlow-v5.exe')
