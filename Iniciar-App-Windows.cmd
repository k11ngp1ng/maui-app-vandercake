@echo off
setlocal
cd /d "%~dp0"
set "app=%~dp0src\VanderCake.Mobile\bin\Debug\net10.0-windows10.0.19041.0\win-x64\VanderCake.Mobile.exe"
if not exist "%app%" (
    echo Aplicativo ainda nao compilado. Execute Configurar-Projeto.cmd primeiro.
    echo Ou execute: dotnet build src/VanderCake.Mobile/VanderCake.Mobile.csproj -f net10.0-windows10.0.19041.0 -c Debug
    pause
    exit /b 1
)
start "" "%app%"
exit /b %errorlevel%
