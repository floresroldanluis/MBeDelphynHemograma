$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'dist\eDelphyn.Hemograma.exe'))
$type = $assembly.GetType('Hemograma.Deployment')
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$method = $type.GetMethod('ValidateInstaller',$flags,$null,[type[]]@([string],[version]),$null)
$setup = Join-Path $root 'release\eDelphyn-Hemograma-Setup-1.6.0.exe'
$version = $method.Invoke($null,[object[]]@([string]$setup,[version]'1.0.0.0'))
if ($version -ne [version]'1.6.0.0') { throw 'Signed newer version not accepted' }
$sameRejected = $false
try { [void]$method.Invoke($null,[object[]]@([string]$setup,[version]'1.6.0.0')) } catch { $sameRejected = $_.Exception.InnerException.Message -like '*misma versi*' }
if (-not $sameRejected) { throw 'Same version not rejected correctly' }
$wrongProductRejected = $false
try { [void]$method.Invoke($null,[object[]]@([string](Join-Path $root 'dist\eDelphyn.Hemograma.exe'),[version]'1.0.0.0')) } catch { $wrongProductRejected = $_.Exception.InnerException.Message -like '*no es un instalador*' }
if (-not $wrongProductRejected) { throw 'Wrong product not rejected' }
$tampered = Join-Path $PSScriptRoot 'tampered-installer.bin'
$bytes = [IO.File]::ReadAllBytes($setup); $bytes[80] = $bytes[80] -bxor 1; [IO.File]::WriteAllBytes($tampered,$bytes)
$tamperRejected = $false
try { [void]$method.Invoke($null,[object[]]@([string]$tampered,[version]'1.0.0.0')) } catch { $tamperRejected = $_.Exception.InnerException.Message -like '*no valida la firma*' }
Remove-Item -LiteralPath $tampered
if (-not $tamperRejected) { throw 'Tampered installer not rejected' }
'PASS: trusted publisher signature, newer version accepted, same version rejected, wrong product rejected, modified file rejected. No installer executed by this test.'











