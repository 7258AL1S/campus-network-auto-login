@echo off
setlocal

set "APP=%~dp0CampusAutoLogin.exe"
if not exist "%APP%" (
  echo CampusAutoLogin.exe was not found next to this batch file.
  exit /b 1
)

reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "CampusAutoLoginWin7" /t REG_SZ /d "\"%APP%\" --silent" /f
if errorlevel 1 (
  echo Could not register automatic startup.
  exit /b 1
)

echo Automatic startup was registered for the current Windows user.
exit /b 0
