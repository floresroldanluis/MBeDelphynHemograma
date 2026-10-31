$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$a=[Reflection.Assembly]::LoadFrom((Join-Path $root 'dist\eDelphyn.Hemograma.exe'))
$form=[Activator]::CreateInstance($a.GetType('Hemograma.LoginForm'),[object[]]@('Data Source=invalid;Connect Timeout=1'))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$user=$form.GetType().GetField('user',$flags).GetValue($form)
$password=$form.GetType().GetField('password',$flags).GetValue($form)
$access=$form.GetType().GetField('access',$flags).GetValue($form)
$state=$form.GetType().GetField('state',$flags).GetValue($form)
$key=$form.GetType().GetMethod('ProcessDialogKey',$flags)
try {
$form.ShowInTaskbar=$false;$form.Show();[Windows.Forms.Application]::DoEvents()
if(-not $user.Focused){throw 'Initial focus must be Usuario'}
[void]$key.Invoke($form,[object[]]@([Windows.Forms.Keys]::Enter))
if(-not $password.Focused){throw 'Enter must move to password'}
[void]$key.Invoke($form,[object[]]@([Windows.Forms.Keys]::Enter))
if(-not $user.Focused -or $state.Text -ne 'Ingrese su usuario de eDelphyn.'){throw 'Password Enter must invoke Acceder validation'}
$user.Text='TEST'
[void]$key.Invoke($form,[object[]]@([Windows.Forms.Keys]::Enter))
[void]$key.Invoke($form,[object[]]@([Windows.Forms.Keys]::Enter))
if(-not $password.Focused -or -not $access.Enabled){throw 'Empty password must retain focus without disabling login'}
if($form.GetType().GetField('failures',$flags).GetValue($form) -ne 0){throw 'Empty fields must not consume attempts'}
'PASS: initial user focus; Enter advances then invokes Acceder; empty fields validated locally without SQL, delay or failed attempt.'
} finally {$form.Dispose()}
