$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.Data
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\dist\eDelphyn.Hemograma.exe'))
$formType = $assembly.GetType('Hemograma.MainForm')
$form = [Activator]::CreateInstance($formType, [object[]]@('Data Source=localhost;Initial Catalog=eDelphyn'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
try {
    $donations = New-Object Data.DataTable
    [void]$donations.Columns.Add('ID_DONATION', [decimal])
    [void]$donations.Columns.Add('COD_DONATION', [string])
    [void]$donations.Columns.Add('DAT_DONATION', [datetime])
    [void]$donations.Rows.Add(1,'C000003',[datetime]'2026-10-28')
    [void]$donations.Rows.Add(2,'C000001',[datetime]'2026-10-29')
    [void]$donations.Rows.Add(3,'C000002',[datetime]'2026-10-27')
    $tests = New-Object Data.DataTable
    [void]$tests.Columns.Add('ID_DONATION', [decimal])
    [void]$tests.Columns.Add('ID_TEST', [decimal])
    [void]$tests.Columns.Add('DES_RESULT', [string])
    [void]$tests.Rows.Add(1,93,'10')
    [void]$tests.Rows.Add(2,93,'2')
    [void]$tests.Rows.Add(3,93,'9,5')
    $resultType = $assembly.GetType('Hemograma.SearchResult')
    $result = [Activator]::CreateInstance($resultType, $true)
    $resultType.GetField('Donations').SetValue($result, $donations)
    $resultType.GetField('Tests').SetValue($result, $tests)
    [void]$formType.GetMethod('Bind',$flags).Invoke($form, [object[]]@($result))
    $grid = $formType.GetField('grid',$flags).GetValue($form)
    $checks = $formType.GetField('testChecks',$flags).GetValue($form)
    if ($checks.Count -ne 9 -or ($checks | Where-Object Checked).Count -ne 8 -or $checks[8].Checked) { throw 'Expected eight selected tests and Hb unchecked' }
    $all = $formType.GetField('selectAll',$flags).GetValue($form)
    $none = $formType.GetField('selectNone',$flags).GetValue($form)
    $none.Checked = $true
    if (($grid.DataSource.Rows | Where-Object { $_['Selected'] }).Count -ne 0 -or $all.Checked) { throw 'Clear all failed' }
    $all.Checked = $true
    if (($grid.DataSource.Rows | Where-Object { $_['Selected'] }).Count -ne 3 -or $none.Checked) { throw 'Select all failed' }
    $delete = $formType.GetField('delete',$flags).GetValue($form)
    if (-not $delete.Enabled) { throw 'Delete must be enabled with selected tests even when matching record count is zero' }
    foreach ($check in $checks) { $check.Checked = $false }
    if ($delete.Enabled) { throw 'Delete must be disabled with no selected tests' }
    $checks[8].Checked = $true
    if (-not $delete.Enabled) { throw 'Selecting Hb must enable Delete' }
    $none.Checked = $true
    if ($delete.Enabled) { throw 'Delete must be disabled with no selected donations' }
    $all.Checked = $true
    if (-not $delete.Enabled) { throw 'Reselecting donations must enable Delete' }
    $view = $grid.DataSource.DefaultView
    if ($view[0]['COD_DONATION'] -ne 'C000001') { throw 'Default donation sort failed' }
    $sort = $formType.GetMethod('SortColumn',$flags)
    [void]$sort.Invoke($form, [object[]]@(2,$false))
    if ($view[0]['COD_DONATION'] -ne 'C000003') { throw 'Descending donation sort failed' }
    [void]$sort.Invoke($form, [object[]]@(1,$true))
    if ($view[0]['ID_DONATION'] -ne 3) { throw 'Date sort failed' }
    [void]$sort.Invoke($form, [object[]]@(11,$true))
    if (($view | ForEach-Object { $_['T93'] }) -join ',' -ne '2,9,5,10') { throw 'Numeric ascending sort failed' }
    [void]$sort.Invoke($form, [object[]]@(11,$false))
    if ($view[0]['T93'] -ne '10') { throw 'Numeric descending sort failed' }
    $grid.DataSource.Rows[1]['Selected'] = $false
    [void]$sort.Invoke($form, [object[]]@(0,$true))
    if ($view[0]['ID_DONATION'] -ne 2) { throw 'Selection sort failed' }
    $selected = $formType.GetMethod('SelectedIds',$flags).Invoke($form, @())
    if ($selected.Count -ne 2 -or $selected -contains 2) { throw 'Selection identity changed after sorting' }
    foreach ($column in $grid.Columns) {
        if ($column.SortMode -ne 'Programmatic') { throw 'Column sorting not enabled' }
    }
    'PASS: default donation order, ascending/descending, dates, numeric results and selection identity. No database connection.'
} finally { $form.Dispose() }
