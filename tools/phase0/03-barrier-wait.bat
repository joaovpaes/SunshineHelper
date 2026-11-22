@echo off
rem Teste 3: segura o inicio da transmissao por N segundos (padrao 30).
rem Uso como Prep Command: "C:\caminho\03-barrier-wait.bat" 45
set SECS=%~1
if "%SECS%"=="" set SECS=30
set OUT=%~dp0barrier-wait.log
echo [%date% %time%] INICIO espera de %SECS%s >> "%OUT%"
timeout /t %SECS% /nobreak >nul
echo [%date% %time%] FIM espera de %SECS%s >> "%OUT%"
exit /b 0
