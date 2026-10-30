param([string]$Gh = 'gh')
$ErrorActionPreference = 'Stop'
$repo = 'floresroldanluis/MBeDelphynHemograma'
Push-Location $PSScriptRoot
try {
    if (git status --porcelain) { throw 'Confirme los cambios revisados en Git antes de publicar.' }
    & "$PSScriptRoot\build-installer.ps1"
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo("$PSScriptRoot\dist\eDelphyn.Hemograma.exe").FileVersion
    $version = ([version]$version).ToString(3)
    $setup = "$PSScriptRoot\release\eDelphyn-Hemograma-Setup-$version.exe"
    if (-not (Test-Path $setup)) { throw 'La versión del instalador no coincide.' }
    foreach ($test in @('verify-filters','verify-sorting','verify-deployment','verify-github-updates')) {
        powershell -NoProfile -File "$PSScriptRoot\tests\$test.ps1"
        if ($LASTEXITCODE -ne 0) { throw "Falló $test" }
    }
    git push origin HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Falló git push.' }
    $commit = git rev-parse HEAD
    & $Gh release create "v$version" $setup "$PSScriptRoot\release\SHA256.txt" --repo $repo --target $commit --title "eDelphyn Hemograma $version" --generate-notes
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación de la release.' }
} finally { Pop-Location }
