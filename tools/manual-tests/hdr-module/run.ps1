<#
Constrói e roda a ferramenta de teste manual do módulo de HDR.
Cobre o checklist de docs/testes-manuais.md, "PR 5b - Modulo de HDR".

Rode fora de uma transmissao do Sunshine. Nao precisa ser administrador.

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
