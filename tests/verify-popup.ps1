$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type 'using System; using System.Runtime.InteropServices; public static class PopupNative { [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr handle); }'
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'dist\eDelphyn.Hemograma.exe'))
$method = $assembly.GetType('Hemograma.PopupForm').GetMethod('ShowDimmed',[Reflection.BindingFlags]'Instance,NonPublic')
$owner = New-Object Windows.Forms.Form
$owner.Text = 'Verificación de popup'
$owner.Size = New-Object Drawing.Size(1000,700)
$owner.ShowInTaskbar = $false
$owner.Show()
try {
    foreach ($dark in @($false,$true)) {
        foreach ($name in @('AboutForm','SettingsForm')) {
            $popup = [Activator]::CreateInstance($assembly.GetType('Hemograma.'+$name),[object[]]@($dark,[Drawing.Icon]$owner.Icon))
            $timer = New-Object Windows.Forms.Timer
            $timer.Interval = 250
            $script:failure = $null
            $timer.Add_Tick({
                $timer.Stop()
                try {
                    $backdrop = @([Windows.Forms.Application]::OpenForms | Where-Object { $_.Text -eq 'Fondo del diálogo' })[0]
                    if ($backdrop.Text -ne 'Fondo del diálogo' -or $backdrop.Owner -ne $owner) { throw ('Incorrect ownership: ' + [string]$backdrop.Text + ' / ' + [string]$backdrop.Owner.Text) }
                    if ($backdrop.Bounds -ne $owner.Bounds) { throw 'Backdrop must cover the owner' }
                    if ($backdrop.Opacity -ne 0.45 -or $backdrop.BackColor -ne [Drawing.Color]::Black) { throw 'Incorrect dimming' }
                    if ($popup.Opacity -ne 1 -or -not $popup.Enabled) { throw 'Popup must remain opaque and interactive' }
                    if ([PopupNative]::IsWindowEnabled($owner.Handle)) { throw 'Background must be disabled during the modal' }
                } catch { $script:failure = $_ }
                finally { $popup.Close() }
            })
            try {
                $timer.Start()
                [void]$method.Invoke($popup,[object[]]@([Windows.Forms.Form]$owner))
                if ($script:failure) { throw $script:failure }
                if (-not $owner.Enabled -or -not [PopupNative]::IsWindowEnabled($owner.Handle)) { throw 'Owner was not restored' }
                foreach ($form in [Windows.Forms.Application]::OpenForms) {
                    if ($form.Text -eq 'Fondo del diálogo') { throw 'Backdrop was not removed' }
                }
            } finally { $timer.Dispose(); $popup.Dispose() }
        }
    }
} finally { $owner.Close(); $owner.Dispose() }
'PASS: About/Settings in light/dark; dim background, opaque popup, blocked owner and cleanup after closing.'

