param([switch]$CreateCertificate, [switch]$TrustCurrentUser)
$ErrorActionPreference = 'Stop'
$signingDirectory = Join-Path $PSScriptRoot 'signing'
$thumbprintFile = Join-Path $signingDirectory 'thumbprint.txt'
$publicFile = Join-Path $signingDirectory 'eDelphyn-FirmaLocal.cer'
New-Item -ItemType Directory -Path $signingDirectory -Force | Out-Null
if (Test-Path -LiteralPath $thumbprintFile) {
    $thumbprint = (Get-Content -LiteralPath $thumbprintFile -Raw).Trim()
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$thumbprint"
} elseif ($CreateCertificate) {
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject "CN=eDelphyn - Firma local $env:USERNAME" `
        -FriendlyName 'eDelphyn Hemograma - firma local de desarrollo' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(2)
    Set-Content -LiteralPath $thumbprintFile -Value $certificate.Thumbprint -Encoding Ascii
} else { throw 'No hay certificado configurado. Use -CreateCertificate para crearlo.' }
if (-not $certificate.HasPrivateKey) { throw 'La clave privada no esta disponible para este usuario.' }
if ($certificate.NotAfter -le (Get-Date)) { throw 'El certificado ha caducado.' }
Export-Certificate -Cert $certificate -FilePath $publicFile -Force | Out-Null
if ($TrustCurrentUser) {
    # Explicit local trust only; never changes machine-wide or antivirus policy.
    Import-Certificate -FilePath $publicFile -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null
    Import-Certificate -FilePath $publicFile -CertStoreLocation 'Cert:\CurrentUser\TrustedPublisher' | Out-Null
}
$executable = Join-Path $PSScriptRoot 'dist\eDelphyn.Hemograma.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    Write-Output "Certificado preparado: $($certificate.Thumbprint). Ejecute build.ps1 para compilar y firmar."
    return
}
$signature = Set-AuthenticodeSignature -LiteralPath $executable -Certificate $certificate -HashAlgorithm SHA256
if ($signature.Status -ne 'Valid') { throw "Firma no validada: $($signature.Status) - $($signature.StatusMessage)" }
[pscustomobject]@{
    File = $executable
    Status = $signature.Status.ToString()
    Subject = $certificate.Subject
    Thumbprint = $certificate.Thumbprint
    NotAfter = $certificate.NotAfter.ToString('o')
    SHA256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    Timestamp = 'Sin sello de tiempo; volver a firmar antes del vencimiento.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $signingDirectory 'signature.json') -Encoding UTF8
Write-Output "Firma verificada: Valid. Certificado: $($certificate.Thumbprint)"
