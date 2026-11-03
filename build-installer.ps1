param([switch]$PrivateBuild)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'installer\get-prerequisites.ps1')
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Se requiere Inno Setup 6 para generar el instalador.' }
if ($PrivateBuild) { & $compiler /DPRIVATE_BUILD (Join-Path $PSScriptRoot 'installer\setup.iss') }
else { & $compiler (Join-Path $PSScriptRoot 'installer\setup.iss') }
if ($LASTEXITCODE -ne 0) { throw 'Error al compilar el instalador.' }
$setup = Join-Path $PSScriptRoot 'release\eDelphyn-Hemograma-Setup-1.6.9.exe'
if ($PrivateBuild) { $setup = $setup.Replace('.exe', '-Privado.exe') }
$thumbprint = (Get-Content (Join-Path $PSScriptRoot 'signing\thumbprint.txt') -Raw).Trim()
$certificate = Get-Item "Cert:\CurrentUser\My\$thumbprint"
$signature = Set-AuthenticodeSignature -LiteralPath $setup -Certificate $certificate -HashAlgorithm SHA256
if ($signature.Status -ne 'Valid') { throw "Firma del instalador: $($signature.Status)" }
Get-FileHash -LiteralPath $setup -Algorithm SHA256 | Format-List | Out-File ($setup + '.sha256.txt')
Write-Output "Instalador firmado: $setup"










