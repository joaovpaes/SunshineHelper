@echo off
rem Constroi e roda a ferramenta de teste manual do modulo de FPS (RTSS).
rem Cobre o checklist de docs/testes-manuais.md, "PR 6 - Modulo de FPS (RTSS)".
rem Rode fora de uma transmissao do Sunshine. Nao precisa ser administrador.
setlocal
cd /d "%~dp0"
dotnet run
endlocal
