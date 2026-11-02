#define AppVersion "1.6.4"
#ifdef PRIVATE_BUILD
  #include "private-defaults.iss"
  #define SetupSuffix "-Privado"
#else
  #define DefaultSqlPassword ""
  #define SetupSuffix ""
#endif
[Setup]
AppId={{CBED70EC-F181-469B-B350-4477CA2D7698}
AppName=eDelphyn Hemograma
AppVersion={#AppVersion}
AppVerName=eDelphyn Hemograma {#AppVersion}
AppPublisher=eDelphyn - Utilidad local de hemograma
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=eDelphyn Hemograma Installer
DefaultDirName={localappdata}\Programs\eDelphyn Hemograma
DefaultGroupName=eDelphyn Hemograma
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
WizardStyle=modern
SetupIconFile=..\assets\edelphyn.ico
WizardImageFile=..\assets\wizard.bmp
WizardSmallImageFile=..\assets\wizard-small.bmp
UninstallDisplayIcon={app}\eDelphyn.Hemograma.exe
OutputDir=..\release
OutputBaseFilename=eDelphyn-Hemograma-Setup-{#AppVersion}{#SetupSuffix}
Compression=lzma2
SolidCompression=yes
MinVersion=10.0
AppMutex=Local\eDelphyn.Hemograma.Running
CloseApplications=no
RestartApplications=no
DisableWelcomePage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
UninstallDisplayName=eDelphyn Hemograma
InfoBeforeFile=installation-notes.txt

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "..\dist\eDelphyn.Hemograma.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\eDelphyn.Hemograma.exe.config"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\assets\edelphyn.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "prerequisites\NDP48-x86-x64-AllOS-ENU.exe"; Flags: dontcopy nocompression
Source: "..\dist\ConnectionSetup.exe"; Flags: dontcopy
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\eDelphyn Hemograma"; Filename: "{app}\eDelphyn.Hemograma.exe"; WorkingDir: "{app}"; IconFilename: "{app}\edelphyn.ico"
Name: "{autodesktop}\eDelphyn Hemograma"; Filename: "{app}\eDelphyn.Hemograma.exe"; WorkingDir: "{app}"; IconFilename: "{app}\edelphyn.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\eDelphyn.Hemograma.exe"; Description: "Abrir eDelphyn Hemograma"; Flags: nowait postinstall skipifsilent; Check: not IsAppUpdate
Filename: "{app}\eDelphyn.Hemograma.exe"; Flags: nowait; Check: IsAppUpdate

[Code]
var
  ConnectionPage: TInputQueryWizardPage;
  TestButton: TNewButton;
  HelperReady: Boolean;

function IsAppUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:APPUPDATE|0}') = '1';
end;

function HasNet48(): Boolean;
var ReleaseValue: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue) and (ReleaseValue >= 528040);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if HasNet48() then exit;
  ExtractTemporaryFile('NDP48-x86-x64-AllOS-ENU.exe');
  if not ShellExec('runas', ExpandConstant('{tmp}\NDP48-x86-x64-AllOS-ENU.exe'), '/passive /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then begin
    Result := 'No se pudo instalar .NET Framework 4.8. Se requiere autorización de administrador.'; exit;
  end;
  if (Code = 3010) or (Code = 1641) then begin
    NeedsRestart := True;
    Result := '.NET Framework requiere reiniciar Windows. Reinicie y vuelva a abrir este instalador.'; exit;
  end;
  if (Code <> 0) or not HasNet48() then Result := 'No se completó la instalación de .NET Framework 4.8. Código: ' + IntToStr(Code);
end;

procedure LoadConnection();
var ReportFile, Parameters: String; Code: Integer; Response: AnsiString;
begin
  ConnectionPage.Values[0] := GetIniString('SQL', 'Server', ConnectionPage.Values[0], ExpandConstant('{app}\connection.ini'));
  ConnectionPage.Values[1] := GetIniString('SQL', 'Database', ConnectionPage.Values[1], ExpandConstant('{app}\connection.ini'));
  ConnectionPage.Values[2] := GetIniString('SQL', 'User', ConnectionPage.Values[2], ExpandConstant('{app}\connection.ini'));
  if HasNet48() and FileExists(ExpandConstant('{app}\connection.ini')) then begin
    if not HelperReady then begin ExtractTemporaryFile('ConnectionSetup.exe'); HelperReady := True; end;
    ReportFile := ExpandConstant('{tmp}\connection-settings.ini');
    try
      Parameters := '--read-settings "' + ExpandConstant('{tmp}\unused-request.ini') + '" "' + ExpandConstant('{app}\connection.ini') + '" "' + ReportFile + '"';
      if Exec(ExpandConstant('{tmp}\ConnectionSetup.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0) then
      begin
        ConnectionPage.Values[0] := GetIniString('SQL', 'Server', ConnectionPage.Values[0], ReportFile);
        ConnectionPage.Values[1] := GetIniString('SQL', 'Database', ConnectionPage.Values[1], ReportFile);
        ConnectionPage.Values[2] := GetIniString('SQL', 'User', ConnectionPage.Values[2], ReportFile);
      end;
    finally DeleteFile(ReportFile); end;
  end;
end;

function RunConnectionHelper(Mode: String; ShowSuccess: Boolean): Boolean;
var RequestFile, ReportFile, Payload, Parameters: String; Code: Integer; Response: AnsiString;
begin
  Result := False;
  if not HelperReady then begin ExtractTemporaryFile('ConnectionSetup.exe'); HelperReady := True; end;
  RequestFile := ExpandConstant('{tmp}\connection-request.ini');
  ReportFile := ExpandConstant('{tmp}\connection-result.txt');
  Payload := 'Server=' + Trim(ConnectionPage.Values[0]) + #13#10 + 'Database=' + Trim(ConnectionPage.Values[1]) + #13#10 +
    'User=' + Trim(ConnectionPage.Values[2]) + #13#10 + 'Password=' + ConnectionPage.Values[3] + #13#10;
  if not SaveStringToFile(RequestFile, UTF8Encode(Payload), False) then exit;
  try
    Parameters := Mode + ' "' + RequestFile + '" "' + ExpandConstant('{app}\connection.ini') + '" "' + ReportFile + '"';
    Result := Exec(ExpandConstant('{tmp}\ConnectionSetup.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
    if Mode = '--test' then begin
      if Result then begin
        if ShowSuccess then MsgBox('Conexión correcta con SQL Server y la base eDelphyn.', mbInformation, MB_OK);
      end else if LoadStringFromFile(ReportFile, Response) then MsgBox(UTF8Decode(Response), mbError, MB_OK)
      else MsgBox('No se pudo probar la conexión. Verifique los datos y la disponibilidad de SQL Server.', mbError, MB_OK);
    end;
  finally
    DeleteFile(RequestFile); DeleteFile(ReportFile);
  end;
end;

procedure TestConnectionClick(Sender: TObject);
begin
  TestButton.Enabled := False;
  try RunConnectionHelper('--test', True); finally TestButton.Enabled := True; end;
end;

procedure InitializeWizard();
begin
  ConnectionPage := CreateInputQueryPage(wpSelectTasks, 'Conexión a SQL Server', 'Configure el acceso a la base eDelphyn.',
    'Autenticación SQL Server. La contraseña se oculta y se guarda cifrada para este usuario de Windows. En actualizaciones, déjela vacía para conservar la guardada.');
  ConnectionPage.Add('Servidor SQL:', False);
  ConnectionPage.Add('Base de datos:', False);
  ConnectionPage.Add('Usuario:', False);
  ConnectionPage.Add('Contraseña:', True);
  ConnectionPage.Values[0] := ExpandConstant('{param:SQLSERVER|localhost}');
  ConnectionPage.Values[1] := ExpandConstant('{param:DATABASE|eDelphyn}');
  ConnectionPage.Values[2] := 'eDelphyn';
  ConnectionPage.Values[3] := '{#DefaultSqlPassword}';
  SendMessage(ConnectionPage.Edits[3].Handle, $00CC, 42, 0);

  TestButton := TNewButton.Create(WizardForm);
  TestButton.Parent := ConnectionPage.Surface;
  TestButton.Caption := 'Probar conexión';
  TestButton.SetBounds(0, ConnectionPage.Edits[3].Top + ConnectionPage.Edits[3].Height + ScaleY(10), ScaleX(145), ScaleY(26));
  TestButton.OnClick := @TestConnectionClick;
  if WizardSilent then LoadConnection();
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = ConnectionPage.ID then LoadConnection();
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ConnectionPage.ID then begin
    Result := (Trim(ConnectionPage.Values[0]) <> '') and (Trim(ConnectionPage.Values[1]) <> '') and (Trim(ConnectionPage.Values[2]) <> '');
    if not Result then MsgBox('Ingrese servidor, base de datos y usuario.', mbError, MB_OK)
    else Result := RunConnectionHelper('--test', False);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if not RunConnectionHelper('--save', False) then RaiseException('No se pudo guardar una conexión SQL válida. Revise usuario y contraseña antes de abrir el programa.');
end;




