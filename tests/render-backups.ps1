$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$a=[Reflection.Assembly]::LoadFrom((Join-Path $root 'dist\eDelphyn.Hemograma.exe'))
$repo=[Activator]::CreateInstance($a.GetType('Hemograma.Repository'),[object[]]@('Data Source=localhost;Initial Catalog=eDelphyn;Integrated Security=True'))
$form=$a.GetType('Hemograma.BackupsForm').GetConstructors()[0].Invoke([object[]]@($repo.PSObject.BaseObject,[string](Join-Path $root 'dist\test-results'),[bool]$false,[Drawing.Icon][Drawing.SystemIcons]::Application))
try {
$form.Opacity=0; $form.ShowInTaskbar=$false; $form.Show()
$busy=$form.GetType().GetField('busy',[Reflection.BindingFlags]'NonPublic,Instance')
$deadline=[DateTime]::UtcNow.AddSeconds(20)
do { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 } while ($busy.GetValue($form) -and [DateTime]::UtcNow -lt $deadline)
if ($busy.GetValue($form)) { throw 'Timeout loading backup viewer' }
$form.Refresh()
$bmp=New-Object Drawing.Bitmap($form.Width,$form.Height)
$form.DrawToBitmap($bmp,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height))
$bmp.Save((Join-Path $PSScriptRoot 'backups-light.png'));$bmp.Dispose()
} finally {$form.Dispose()}
'PASS: rendered backup viewer using temporary-table test backups. No writes.'
