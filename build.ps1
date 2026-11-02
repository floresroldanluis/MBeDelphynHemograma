$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path (Join-Path $root 'dist') -Force | Out-Null
& $compiler /nologo /target:winexe /optimize+ /utf8output "/out:$root\dist\eDelphyn.Hemograma.exe" "/win32icon:$root\assets\edelphyn.ico" "/resource:$root\assets\edelphyn.ico,Hemograma.App.ico" "/resource:$root\assets\edelphyn-logo.png,Hemograma.Logo.png" /reference:System.Security.dll /reference:System.dll /reference:System.Core.dll /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Configuration.dll /reference:System.Xml.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll "$root\src\Program.cs" "$root\src\Authentication.cs" "$root\src\LoginForm.cs" "$root\src\Repository.cs" "$root\src\Backups.cs" "$root\src\BackupsForm.cs" "$root\src\MainForm.cs" "$root\src\Deployment.cs" "$root\src\SqlSettings.cs" "$root\src\TestSettings.cs" "$root\src\AppSettings.cs" "$root\src\ThemedControls.cs" "$root\src\AboutForm.cs" "$root\src\SettingsForm.cs" "$root\src\GitHubUpdates.cs" "$root\src\AssemblyInfo.cs" "$root\tests\SelfTest.cs"
if ($LASTEXITCODE -ne 0) { throw 'Error de compilación.' }
Copy-Item -LiteralPath "$root\src\App.config" -Destination "$root\dist\eDelphyn.Hemograma.exe.config"
if (Test-Path -LiteralPath "$root\signing\thumbprint.txt") { & "$root\sign.ps1" }
Write-Output "Aplicación: $root\dist\eDelphyn.Hemograma.exe"



& $compiler /nologo /target:winexe /optimize+ /utf8output "/out:$root\dist\ConnectionSetup.exe" /reference:System.dll /reference:System.Data.dll /reference:System.Security.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Configuration.dll "$root\installer\ConnectionSetup.cs" "$root\src\SqlSettings.cs" "$root\src\TestSettings.cs"
if ($LASTEXITCODE -ne 0) { throw 'Error compilando el asistente SQL.' }
if (Test-Path -LiteralPath "$root\signing\thumbprint.txt") {
    $thumbprint = (Get-Content "$root\signing\thumbprint.txt" -Raw).Trim()
    $signature = Set-AuthenticodeSignature -LiteralPath "$root\dist\ConnectionSetup.exe" -Certificate (Get-Item "Cert:\CurrentUser\My\$thumbprint") -HashAlgorithm SHA256
    if ($signature.Status -ne 'Valid') { throw 'Firma inválida del asistente SQL.' }
}
