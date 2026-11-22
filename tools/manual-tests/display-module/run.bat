@echo off
rem Constroi e roda a ferramenta de teste manual do modulo de video (resolucao).
rem Cobre o checklist de docs/testes-manuais.md, "PR 5a - Modulo de video: resolucao".
rem Rode fora de uma transmissao do Sunshine. Nao precisa ser administrador.
setlocal
cd /d "%~dp0"
dotnet run
endlocal
