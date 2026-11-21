@echo off
REM ============================================================
REM  Unity command-line Android build
REM
REM  Usage:
REM    build-android.bat              release APK
REM    build-android.bat apk dev      development APK
REM    build-android.bat aab          release AAB (Google Play)
REM
REM  Override the Unity install with:  set UNITY_PATH=D:\...\Unity.exe
REM  Release signing: set ANDROID_KEYSTORE_PATH / _PASS,
REM                   ANDROID_KEYALIAS_NAME / _PASS before running.
REM  Close the Unity Editor first - batch mode can't open a project
REM  that is already open.
REM ============================================================
setlocal EnableExtensions

set "PROJECT=%~dp0"
if "%PROJECT:~-1%"=="\" set "PROJECT=%PROJECT:~0,-1%"

if not defined UNITY_PATH if exist "E:\game\editors\6000.3.28f1\Editor\Unity.exe" set "UNITY_PATH=E:\game\editors\6000.3.28f1\Editor\Unity.exe"
if not defined UNITY_PATH set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe"
if not exist "%UNITY_PATH%" (
    echo [ERROR] Unity not found at "%UNITY_PATH%"
    echo         Install 6000.3.28f1 via Unity Hub, or set UNITY_PATH to your Unity.exe.
    exit /b 1
)

for %%I in ("%UNITY_PATH%") do set "UNITY_DIR=%%~dpI"
if not exist "%UNITY_DIR%Data\PlaybackEngines\AndroidPlayer" (
    echo [ERROR] Android Build Support is not installed for this Unity version.
    echo         Unity Hub ^> Installs ^> 6000.3.28f1 ^> Add modules ^>
    echo         Android Build Support ^(+ OpenJDK and Android SDK ^& NDK Tools^).
    exit /b 1
)

set "FORMAT=%~1"
if "%FORMAT%"=="" set "FORMAT=apk"
set "DEVFLAG="
if /I "%~2"=="dev" set "DEVFLAG=-development"

if not exist "%PROJECT%\Builds\Android" mkdir "%PROJECT%\Builds\Android"
set "LOG=%PROJECT%\Builds\Android\build.log"

echo Building %FORMAT% %DEVFLAG% for Android...
echo Log: "%LOG%"
echo (this can take several minutes on the first run)

"%UNITY_PATH%" -batchmode -quit ^
  -projectPath "%PROJECT%" ^
  -buildTarget Android ^
  -executeMethod AndroidBuilder.Build ^
  -format %FORMAT% %DEVFLAG% ^
  -logFile "%LOG%"

set "RC=%ERRORLEVEL%"

if not "%RC%"=="0" (
    echo.
    echo [FAILED] Unity exited with code %RC%. Last lines of the log:
    powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG%' -Tail 40"
    findstr /C:"another Unity instance" "%LOG%" >nul && echo. && echo Hint: close the Unity Editor and try again.
    exit /b %RC%
)

echo.
echo [OK] Build finished. Output in "%PROJECT%\Builds\Android"
dir /b "%PROJECT%\Builds\Android\*.apk" "%PROJECT%\Builds\Android\*.aab" 2>nul
exit /b 0
