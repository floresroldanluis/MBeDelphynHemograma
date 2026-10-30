$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.Data
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\dist\eDelphyn.Hemograma.exe'))
$type = $assembly.GetType('Hemograma.MainForm')
$form = [Activator]::CreateInstance($type, [object[]]@('Data Source=localhost;Initial Catalog=eDelphyn'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
function Field($name) { $type.GetField($name,$flags).GetValue($form) }
function Get-CurrentFilter { $type.GetMethod('CurrentFilter',$flags).Invoke($form,@()) }
try {
    $fromCode = Field 'fromCode'; $toCode = Field 'toCode'
    $fromDate = Field 'fromDate'; $toDate = Field 'toDate'
    $initial = Get-CurrentFilter
    if ($initial.FromDate -ne [datetime]::Today -or $initial.ToDate -ne [datetime]::Today) { throw 'Initial dates must be today' }
    $fromCode.Text = 'C260730'; $toCode.Text = 'C260740'
    $numberOnly = Get-CurrentFilter
    if ($null -ne $numberOnly.FromDate -or $null -ne $numberOnly.ToDate) { throw 'Donation range must not use implicit dates' }
    if ($fromDate.CustomFormat -ne ' ' -or $toDate.CustomFormat -ne ' ') { throw 'Inactive dates must appear blank' }
    # Choosing the internally held date must work even without ValueChanged.
    $closeUp = [Windows.Forms.DateTimePicker].GetMethod('OnCloseUp',$flags)
    [void]$closeUp.Invoke($fromDate,[object[]]@([EventArgs]::Empty))
    if ((Get-CurrentFilter).FromDate -ne [datetime]::Today -or $null -ne (Get-CurrentFilter).ToDate) { throw 'Same-day selection not applied independently' }
    $toDate.Value = [datetime]::Today.AddDays(1)
    $toCode.Text = 'C260741'
    if ((Get-CurrentFilter).ToDate -ne [datetime]::Today.AddDays(1) -or (Get-CurrentFilter).FromDate -ne [datetime]::Today) { throw 'Explicit dates lost while changing numbers' }
    $onKeyDown = [Windows.Forms.Control].GetMethod('OnKeyDown',$flags)
    $keyEvent = [Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Delete)
    [void]$onKeyDown.Invoke($fromDate,[object[]]@($keyEvent))
    if ($null -ne (Get-CurrentFilter).FromDate) { throw 'Delete must clear date filter' }
    if ($fromDate.ShowCheckBox -or $toDate.ShowCheckBox) { throw 'Date checkboxes must remain hidden' }
    'PASS: today by default, number-only clears implicit dates, same-day activation, combined filters, explicit date preservation and clearing. No database writes.'
} finally { $form.Dispose() }

