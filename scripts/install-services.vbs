Set UAC = CreateObject("Shell.Application")

' Install Node.js backend service
UAC.ShellExecute "nssm.exe", "install DayLoop-Node ""D:\Program Files\nodejs\node.exe"" ""D:\02.Personal\04.Code\DayLoop\backend\src\index.js""", "D:\02.Personal\04.Code\DayLoop\scripts", "runas", 0
WScript.Sleep 1000
UAC.ShellExecute "nssm.exe", "set DayLoop-Node AppDirectory ""D:\02.Personal\04.Code\DayLoop\backend""", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-Node DisplayName ""DayLoop Node.js Backend""", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-Node Start SERVICE_AUTO_START", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-Node AppNoConsole 1", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-Node AppThrottle 1000", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-Node AppRestartDelay 5000", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-Node AppStdout ""D:\02.Personal\04.Code\DayLoop\backend\logs\stdout.log""", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-Node AppStderr ""D:\02.Personal\04.Code\DayLoop\backend\logs\stderr.log""", "", "runas", 0
WScript.Sleep 500

' Install .NET backend service
UAC.ShellExecute "nssm.exe", "install DayLoop-DotNet ""C:\Program Files\dotnet\dotnet.exe"" ""run --urls http://0.0.0.0:5000""", "D:\02.Personal\04.Code\DayLoop\scripts", "runas", 0
WScript.Sleep 1000
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet AppDirectory ""D:\02.Personal\04.Code\DayLoop\backend-dotnet""", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet DisplayName ""DayLoop .NET Backend""", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet Start SERVICE_AUTO_START", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet AppNoConsole 1", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet AppThrottle 1000", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet AppRestartDelay 5000", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet AppStdout ""D:\02.Personal\04.Code\DayLoop\backend-dotnet\logs\stdout.log""", "", "runas", 0
WScript.Sleep 500
UAC.ShellExecute "nssm.exe", "set DayLoop-DotNet AppStderr ""D:\02.Personal\04.Code\DayLoop\backend-dotnet\logs\stderr.log""", "", "runas", 0
WScript.Sleep 500

' Start services
UAC.ShellExecute "nssm.exe", "start DayLoop-Node", "", "runas", 0
WScript.Sleep 2000
UAC.ShellExecute "nssm.exe", "start DayLoop-DotNet", "", "runas", 0
WScript.Sleep 2000

' Remove old startup shortcut
Set fso = CreateObject("Scripting.FileSystemObject")
startup = fso.GetSpecialFolder(&H07) ' Startup folder
If fso.FileExists(startup & "\DayLoop.lnk") Then
  On Error Resume Next
  fso.DeleteFile startup & "\DayLoop.lnk"
  On Error Goto 0
End If