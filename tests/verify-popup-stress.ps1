$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type 'using System; using System.Runtime.InteropServices; public static class PopupNative { [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr handle); }'
$root = Split-Path $PSScriptRoot -Parent
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'dist\eDelphyn.Hemograma.exe'))
& "$PSScriptRoot\initialize-test-catalog.ps1" -Assembly $assembly
$method = $assembly.GetType('Hemograma.PopupForm').GetMethod('ShowDimmed',[Reflection.BindingFlags]'Instance,NonPublic')
$owner = [Activator]::CreateInstance($assembly.GetType('Hemograma.MainForm'),[object[]]@('Data Source=localhost;Initial Catalog=eDelphyn;Integrated Security=True'))
$grid=$owner.GetType().GetField('grid',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($owner)
$table=New-Object Data.DataTable
[void]$table.Columns.Add('Selected',[bool]); [void]$table.Columns.Add('DAT_DONATION',[datetime]); [void]$table.Columns.Add('COD_DONATION',[string])
foreach($id in @(72,73,74,75,76,77,78,92,93)){[void]$table.Columns.Add(('T'+$id),[string])}
for($i=0;$i -lt 1000;$i++){ $row=$table.NewRow();$row['Selected']=$true;$row['DAT_DONATION']=[datetime]::Today;$row['COD_DONATION']='TEST'+$i; $table.Rows.Add($row)}
$grid.DataSource=$table
$times=New-Object 'System.Collections.Generic.List[double]'
$owner.ShowInTaskbar = $false
$owner.Show()
try {
    foreach ($dark in @($false,$true,$false,$true,$false,$true)) {
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
                    if ($backdrop.Bounds -ne $owner.RectangleToScreen($owner.ClientRectangle)) { throw 'Backdrop must cover the owner' }
                    if ($backdrop.Opacity -ne 0.45 -or $backdrop.BackColor -ne [Drawing.Color]::Black) { throw 'Incorrect dimming' }
                    if ($popup.Opacity -ne 1 -or -not $popup.Enabled) { throw 'Popup must remain opaque and interactive' }
                    if ([PopupNative]::IsWindowEnabled($owner.Handle)) { throw 'Background must be disabled during the modal' }
                } catch { $script:failure = $_ }
                finally { $script:clock=[Diagnostics.Stopwatch]::StartNew(); $popup.Close() }
            })
            try {
                $timer.Start()
                [void]$method.Invoke($popup,[object[]]@([Windows.Forms.Form]$owner))
                $script:clock.Stop();$times.Add($script:clock.Elapsed.TotalMilliseconds)
                $script:responded=$false
                [void]$owner.BeginInvoke([System.Action]{ $script:responded=$true })
                $deadline=[datetime]::UtcNow.AddSeconds(2)
                do { [Windows.Forms.Application]::DoEvents() } while (-not $script:responded -and [datetime]::UtcNow -lt $deadline)
                if (-not $script:responded) { throw 'Main window message loop stopped responding after popup close' }
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


$times | Measure-Object -Average -Maximum | Select-Object Count,Average,Maximum
