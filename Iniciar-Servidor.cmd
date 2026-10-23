@echo off
setlocal
cd /d "%~dp0"
title VanderCake - Servidor
echo Site do administrador: https://localhost:7080/Account/Login
echo Mantenha esta janela aberta. Para parar o servidor, pressione Ctrl+C.
echo O servidor usa o banco persistente configurado. Nao migra, apaga ou cria dados automaticamente.
echo.
dotnet run --project src/VanderCake.Server --launch-profile https
set "result=%errorlevel%"
echo.
echo O servidor foi encerrado. Codigo: %result%
pause
exit /b %result%
