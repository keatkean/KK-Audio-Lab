@echo off
setlocal

title KK Audio Lab - Clean, Build and Launch

:: Set current working directory to this script's directory
cd /d "%~dp0"

echo ========================================================
echo   KK Audio Lab - Clean, Build and Launch
echo ========================================================
echo.

:: 1. Clean Solution
echo [1/4] Cleaning solution (KKAudioLab.slnx)...
call dotnet clean KKAudioLab.slnx
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] dotnet clean failed!
    echo Ensure Visual Studio or other running processes have released file locks.
    echo.
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Solution cleaned successfully.
echo.

:: 2. Build Solution
echo [2/4] Building solution (KKAudioLab.slnx)...
call dotnet build KKAudioLab.slnx
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] dotnet build failed! Please check compiler errors above.
    echo.
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Solution built successfully.
echo.

:: 3. Launch Backend (Port 5100)
echo [3/4] Launching Backend WebAPI (http://localhost:5100)...
start "KK Audio Lab - Backend (Port 5100)" cmd /k "cd /d "%~dp0Backend" && dotnet run"

:: Wait 3 seconds for Backend LiteDB initialization and endpoint binding
echo Waiting for Backend service to spin up...
ping 127.0.0.1 -n 4 >nul

:: 4. Launch Frontend (Port 5200)
echo [4/4] Launching Frontend Blazor App (http://localhost:5200)...
start "KK Audio Lab - Frontend (Port 5200)" cmd /k "cd /d "%~dp0Frontend" && dotnet run"

:: Wait 2 seconds before opening browser
ping 127.0.0.1 -n 3 >nul
start http://localhost:5200

echo.
echo ========================================================
echo   KK Audio Lab services are running!
echo     - Backend  : http://localhost:5100 (Swagger: /swagger)
echo     - Frontend : http://localhost:5200
echo ========================================================
echo.
echo Both Backend and Frontend are running in separate terminal windows.
echo To stop them, close their respective terminal windows.
echo.
endlocal
pause
