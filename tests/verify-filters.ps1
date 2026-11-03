$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.Data
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\dist\eDelphyn.Hemograma.exe'))
& "$PSScriptRoot\initialize-test-catalog.ps1" -Assembly $assembly
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
    # Explicitly chosen dates must also be discarded when typing/pasting a number.
    $fromDate.Value = [datetime]::Today.AddDays(-4); $toDate.Value = [datetime]::Today.AddDays(-2)
    $fromCode.Text = 'C260730'; $toCode.Text = 'C260740'
    $numberOnly = Get-CurrentFilter
    if ($null -ne $numberOnly.FromDate -or $null -ne $numberOnly.ToDate) { throw 'Donation range must ignore ALL dates' }
    if ($fromDate.CustomFormat -ne ' ' -or $toDate.CustomFormat -ne ' ') { throw 'Both dates must appear blank' }
    if ($fromDate.Enabled -or $toDate.Enabled) { throw 'Dates must stay disabled while a donation number is present' }
    # Rebuilding the search filter cannot accidentally reintroduce dates.
    $fromDate.Value = [datetime]::Today; $toDate.Value = [datetime]::Today.AddDays(-5)
    $numberOnly=Get-CurrentFilter
    if ($null -ne $numberOnly.FromDate -or $null -ne $numberOnly.ToDate) { throw 'Number search must ignore even reversed dates' }
    $numberOnly.Validate()
    $fromCode.Text=''; if ($null -ne (Get-CurrentFilter).FromDate -or $fromDate.Enabled) { throw 'Until-only number still excludes dates' }
    $toCode.Text=''; if (-not $fromDate.Enabled -or -not $toDate.Enabled) { throw 'Clear numbers must enable date searching' }
    $fromDate.Value=[datetime]::Today; $toDate.Value=[datetime]::Today
    $closeUp = [Windows.Forms.DateTimePicker].GetMethod('OnCloseUp',$flags)
    [void]$closeUp.Invoke($fromDate,[object[]]@([EventArgs]::Empty))
    [void]$closeUp.Invoke($toDate,[object[]]@([EventArgs]::Empty))
    if ((Get-CurrentFilter).FromDate -ne [datetime]::Today -or (Get-CurrentFilter).ToDate -ne [datetime]::Today) { throw 'Date-only search must apply selected dates' }
    $onKeyDown = [Windows.Forms.Control].GetMethod('OnKeyDown',$flags)
    $keyEvent = [Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Delete)
    [void]$onKeyDown.Invoke($fromDate,[object[]]@($keyEvent))
    if ($null -ne (Get-CurrentFilter).FromDate) { throw 'Delete must clear date filter' }
    if ($fromDate.ShowCheckBox -or $toDate.ShowCheckBox) { throw 'Date checkboxes must remain hidden' }
    'PASS: dates default to today; numbers clear and ignore ALL dates; either numeric bound disables dates; clearing numbers enables date-only search. No database writes.'
} finally { $form.Dispose() }

