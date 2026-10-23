@echo off
setlocal
cd /d "%~dp0"
title VanderCake - Configuracao inicial
echo Configuracao inicial do VanderCake.
echo As senhas serao solicitadas em particular. Contas existentes nao terao a senha alterada.
echo.
dotnet tool restore
if errorlevel 1 goto :failure
dotnet build VanderCake.slnx -c Debug
if errorlevel 1 goto :failure
dotnet dev-certs https --trust
if errorlevel 1 goto :failure
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0scripts\Initialize-Development.ps1" -AcademicFixture
if errorlevel 1 goto :failure
echo.
echo Configuracao concluida. Agora abra Iniciar-Servidor.cmd e Iniciar-App-Windows.cmd.
pause
exit /b 0
:failure
echo.
echo A configuracao falhou. Leia a mensagem de erro acima antes de continuar.
pause
exit /b 1
