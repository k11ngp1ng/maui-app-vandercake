@echo off
setlocal
cd /d "%~dp0"
title VanderCake - Abrir sessao de testes
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0scripts\Local-Testing.ps1"
if errorlevel 1 echo Nao foi possivel abrir a sessao. Leia o erro acima.
pause
