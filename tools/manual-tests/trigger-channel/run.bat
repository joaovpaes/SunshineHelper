@echo off
rem Constroi e roda o harness do canal de named pipe (auxiliar de gatilho).
rem Cobre o checklist de docs/testes-manuais.md, "PR 7 - Auxiliar de gatilho e canal".
rem Rode fora de uma transmissao do Sunshine. Nao precisa ser administrador.
setlocal
cd /d "%~dp0"
dotnet run
endlocal
