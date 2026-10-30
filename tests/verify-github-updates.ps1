$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http,System.Web.Extensions
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'dist\eDelphyn.Hemograma.exe'))
$type = $assembly.GetType('Hemograma.GitHubUpdates')
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$parse = $type.GetMethod('ParseRelease',$flags)
function Parse($data) { $parse.Invoke($null,[object[]]@([string]($data | ConvertTo-Json -Depth 5 -Compress))) }
$data = @{ draft=$false; prerelease=$false; tag_name='v1.2.1'; assets=@(@{name='eDelphyn-Hemograma-Setup-1.2.1.exe'; size=1000; browser_download_url='https://github.com/floresroldanluis/MBeDelphynHemograma/releases/download/v1.2.1/eDelphyn-Hemograma-Setup-1.2.1.exe'; digest=$null}) }
$release=Parse $data
if ($release.Version -ne [version]'1.2.1.0' -or $release.Size -ne 1000) { throw 'Release parsing failed' }
$data.assets[0].browser_download_url='https://example.com/unsafe.exe'
$rejected=$false; try { Parse $data } catch { $rejected=$true }; if (-not $rejected) { throw 'Foreign download accepted' }
$data.prerelease=$true
$rejected=$false; try { Parse $data } catch { $rejected=$true }; if (-not $rejected) { throw 'Prerelease accepted' }
$data.prerelease=$false; $data.assets=@()
if ((Parse $data).Url) { throw 'Missing installer must not enable download' }
$data.tag_name='not-a-version'
$rejected=$false; try { Parse $data } catch { $rejected=$true }; if (-not $rejected) { throw 'Invalid version accepted' }
'PASS: release parsing, version normalization, missing installer, foreign download, prerelease and invalid tag.'
$task = $type.GetMethod('CheckAsync',$flags).Invoke($null,[object[]]@([Threading.CancellationToken]::None))
try { $task.GetAwaiter().GetResult() | Out-Null; 'LIVE: GitHub release found.' }
catch {
    $errorText=$_.Exception.ToString()
    if ($errorText -notlike '*No hay una versi*') { throw }
    'PASS: live GitHub request reports no stable release available.'
}
