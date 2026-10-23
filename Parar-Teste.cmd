@echo off
setlocal
cd /d "%~dp0"
title VanderCake - Parar servidor de testes
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0scripts\Local-Testing.ps1" -Stop
pause
