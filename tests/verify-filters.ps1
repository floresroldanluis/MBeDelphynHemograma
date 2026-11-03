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
    if (-not $fromDate.Enabled -or -not $toDate.Enabled) { throw 'Dates must remain available to switch search mode' }
    $fromDate.Value = [datetime]::Today.AddDays(-10)
    $dateOnly=Get-CurrentFilter
    if ($fromCode.Text -ne '' -or $toCode.Text -ne '' -or $dateOnly.FromCode -ne '' -or $dateOnly.ToCode -ne '') { throw 'Choosing From date must clear BOTH donation numbers' }
    if ($dateOnly.FromDate -ne [datetime]::Today.AddDays(-10) -or $null -ne $dateOnly.ToDate) { throw 'Date mode must use only chosen dates' }
    $toDate.Value=[datetime]::Today
    if ((Get-CurrentFilter).ToDate -ne [datetime]::Today) { throw 'Date end must apply' }
    $fromCode.Text='C260730'; $toCode.Text='C260740'
    if ($null -ne (Get-CurrentFilter).FromDate -or $null -ne (Get-CurrentFilter).ToDate) { throw 'Switching back to numbers clears both dates' }
    $closeUp = [Windows.Forms.DateTimePicker].GetMethod('OnCloseUp',$flags)
    # Choosing the same held value in the calendar must also switch mode.
    [void]$closeUp.Invoke($toDate,[object[]]@([EventArgs]::Empty))
    if ($fromCode.Text -ne '' -or $toCode.Text -ne '' -or (Get-CurrentFilter).ToDate -ne [datetime]::Today) { throw 'Same-day Until selection must clear both numbers' }
    $fromCode.Text='C260730'; $toCode.Text='C260740'
    $onKeyDown = [Windows.Forms.Control].GetMethod('OnKeyDown',$flags)
    [void]$onKeyDown.Invoke($fromDate,[object[]]@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Enter)))
    if ($fromCode.Text -ne '' -or $toCode.Text -ne '' -or $null -eq (Get-CurrentFilter).FromDate) { throw 'Enter on blank date must switch to date mode' }
    $onKeyDown = [Windows.Forms.Control].GetMethod('OnKeyDown',$flags)
    $keyEvent = [Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Delete)
    [void]$onKeyDown.Invoke($fromDate,[object[]]@($keyEvent))
    if ($null -ne (Get-CurrentFilter).FromDate) { throw 'Delete must clear date filter' }
    $prepare=$type.GetMethod('PrepareSearchFilter',$flags)
    $fromDate.Value=[datetime]::Today.AddMonths(-1)
    [void]$onKeyDown.Invoke($toDate,[object[]]@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Delete)))
    $prepared=$prepare.Invoke($form,@())
    if($prepared.ToDate -ne [datetime]::Today -or $toDate.CustomFormat -eq ' '){throw 'From-only search must fill today visibly in Until'}
    $prepared.Validate()
    $toDate.Value=[datetime]::Today.AddDays(-2)
    if($prepare.Invoke($form,@()).ToDate -ne [datetime]::Today.AddDays(-2)){throw 'An explicit Until date must be preserved'}
    $fromCode.Text='C260730';$toCode.Text='C260740'
    $prepared=$prepare.Invoke($form,@())
    if($null -ne $prepared.FromDate -or $null -ne $prepared.ToDate -or $toDate.CustomFormat -ne ' '){throw 'Number search must not autofill dates'}
    [void]$closeUp.Invoke($toDate,[object[]]@([EventArgs]::Empty))
    if($null -ne $prepare.Invoke($form,@()).FromDate){throw 'Until-only search must not invent a From date'}
    [void]$onKeyDown.Invoke($toDate,[object[]]@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Delete)))
    if($null -ne $prepare.Invoke($form,@()).ToDate){throw 'Empty date filter must remain empty'}
    if ($fromDate.ShowCheckBox -or $toDate.ShowCheckBox) { throw 'Date checkboxes must remain hidden' }
    'PASS: date defaults; numbers clear both dates; either date clears both numbers; repeated switches, same-date calendar choice and Enter/Delete work. From-only search fills today; explicit Until preserved; number-only and empty filters unaffected. No database writes.'
} finally { $form.Dispose() }

