# Testes da Fase 0 (scripts temporários)

Scripts para medir o que só a sua máquina responde. **São temporários**: servem à Fase 0 e saem do repositório depois. Rode cada um, junte os arquivos `.log` que eles geram e envie ao Claude.

> Nada aqui precisa ser instalado. Copie a pasta `tools/phase0` para o PC e use os caminhos abaixo.

## Teste 1: o Sunshine entrega o nome do cliente? (o mais importante)

1. No Sunshine (aba Applications > Playnite > Command Preparations), **adicione uma linha nova** com o Do Command: `"C:\...\tools\phase0\01-dump-env.bat"` (deixe as outras como estão).
2. Conecte pela **TV** e depois pelo **celular**. Desconecte e feche cada vez.
3. Envie o arquivo `dump-env.log` (fica ao lado do script). Ele contém só variáveis `SUNSHINE_*`.
4. Remova a linha depois.

## Teste 2: tempo real de cada passo

1. **Fora de transmissão**, abra o PowerShell **como administrador**.
2. Rode (troque o caminho pela pasta onde estão `QRes.exe` e `HDRSwitch.exe`):
   `powershell -ExecutionPolicy Bypass -File .\02-measure-steps.ps1 -ToolsDir "C:\...\Automacoes"`
3. A tela vai piscar (resolução, HDR e RTSS mudam 3 vezes). No fim tudo volta ao normal (2560x1440, HDR desligado, RTSS no limite normal).
4. Envie `measure-steps.log`.

## Teste 3: até quando o Moonlight espera

1. Adicione um Prep Command com o Do Command `"C:\...\tools\phase0\03-barrier-wait.bat" 45` (o número é a espera em segundos).
2. Conecte pela TV e pelo celular. Anote se o Moonlight conectou, desistiu ou deu erro, e depois quanto tempo levou.
3. Repita com 60 e 90 se o de 45 funcionar. Envie `barrier-wait.log` e as anotações.
4. Remova a linha depois.

## O que já sabemos sem teste

- **Monitor capturado (teste 4):** o log do Sunshine lista um único monitor, `Q27G4` (AOC), principal, 2560x1440 a 180 Hz, HDR desligado em repouso; não há `output_name` configurado.
- Os testes 5 a 7 (HDR por API, reinício do RTSS, piso do Windows) dependem dos resultados acima e entram na sequência.
