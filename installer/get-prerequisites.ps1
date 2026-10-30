$ErrorActionPreference = 'Stop'
$file = Join-Path $PSScriptRoot 'prerequisites\NDP48-x86-x64-AllOS-ENU.exe'
if (-not (Test-Path -LiteralPath $file)) {
    New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
    Invoke-WebRequest 'https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe' -OutFile $file
}
if ((Get-FileHash $file -Algorithm SHA256).Hash -ne '0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40') { throw 'SHA256 incorrecto de .NET Framework.' }
$signature = Get-AuthenticodeSignature $file
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Firma de Microsoft inválida.' }
