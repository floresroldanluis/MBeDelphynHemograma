$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $root 'dist\eDelphyn.Hemograma.exe'))
& "$PSScriptRoot\initialize-test-catalog.ps1" -Assembly $assembly
$form=[Activator]::CreateInstance($assembly.GetType('Hemograma.MainForm'),[object[]]@('Data Source=localhost;Initial Catalog=eDelphyn;Integrated Security=True'))
$theme=$assembly.GetType('Hemograma.AppTheme').GetMethod('Apply',[Reflection.BindingFlags]'Static,NonPublic')
function Render($window,$name) {
    $window.ShowInTaskbar=$false; $window.Opacity=0; $window.Show(); $window.Refresh()
    $bitmap=New-Object Drawing.Bitmap($window.Width,$window.Height)
    $window.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$window.Width,$window.Height))
    $bitmap.Save((Join-Path $PSScriptRoot $name)); $bitmap.Dispose()
}
try {
    [void]$theme.Invoke($null,[object[]]@($form,$false)); Render $form 'main-light.png'
    [void]$theme.Invoke($null,[object[]]@($form,$true)); Render $form 'main-dark.png'
    $login=[Activator]::CreateInstance($assembly.GetType('Hemograma.LoginForm'),[object[]]@('Data Source=localhost;Initial Catalog=eDelphyn;Integrated Security=True'))
    try { Render $login 'login.png' } finally { $login.Dispose() }
    $settings=[Activator]::CreateInstance($assembly.GetType('Hemograma.SettingsForm'),[object[]]@($true,[Drawing.Icon]$form.Icon))
    try { Render $settings 'settings-dark.png' } finally { $settings.Dispose() }
    $settingsLight=[Activator]::CreateInstance($assembly.GetType('Hemograma.SettingsForm'),[object[]]@($false,[Drawing.Icon]$form.Icon))
    try { Render $settingsLight 'settings-light.png' } finally { $settingsLight.Dispose() }
    $about=[Activator]::CreateInstance($assembly.GetType('Hemograma.AboutForm'),[object[]]@($false,[Drawing.Icon]$form.Icon))
    try { Render $about 'about-light.png' } finally { $about.Dispose() }
    $aboutDark=[Activator]::CreateInstance($assembly.GetType('Hemograma.AboutForm'),[object[]]@($true,[Drawing.Icon]$form.Icon))
    try { Render $aboutDark 'about-dark.png' } finally { $aboutDark.Dispose() }
} finally { $form.Dispose() }
'PASS: rendered main window in light/dark and Settings in dark mode.'
