@echo off
rem Teste 1: grava as variaveis SUNSHINE_* que o Sunshine entrega ao Prep Command.
rem Nao grava as demais variaveis do Windows (podem ter dados pessoais).
set OUT=%~dp0dump-env.log
echo ================================================== >> "%OUT%"
echo [%date% %time%] DUMP ENV (arquivo: %~nx0) >> "%OUT%"
set SUNSHINE >> "%OUT%" 2>&1
if errorlevel 1 echo (nenhuma variavel SUNSHINE_* encontrada) >> "%OUT%"
echo [%date% %time%] FIM >> "%OUT%"
exit /b 0
