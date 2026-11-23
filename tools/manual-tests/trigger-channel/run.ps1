<#
Constrói e roda o harness do canal de named pipe (auxiliar de gatilho).
Cobre o checklist de docs/testes-manuais.md, "PR 7 - Auxiliar de gatilho e canal".

Rode fora de uma transmissao do Sunshine. Nao precisa ser administrador
(mas vale testar uma vez como administrador tambem - veja o checklist).

  powershell -ExecutionPolicy Bypass -File .\run.ps1
#>
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run
}
finally {
    Pop-Location
}
