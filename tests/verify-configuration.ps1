$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $root 'dist\ConfigurationTest.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$destination" /reference:System.Configuration.dll /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Security.dll /reference:System.Xml.dll /reference:System.Core.dll "$root\tests\ConfigurationTest.cs" "$root\src\TestSettings.cs" "$root\src\SqlSettings.cs" "$root\src\Repository.cs" "$root\src\Backups.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
$config=Get-Content "$root\src\App.config" -Raw
$config.Replace('93|Hb|false','93|Hb|false;800|Prueba adicional|false') | Set-Content "$destination.config" -Encoding UTF8
& $destination
if($LASTEXITCODE -ne 0){throw 'Configuration tests failed'}
