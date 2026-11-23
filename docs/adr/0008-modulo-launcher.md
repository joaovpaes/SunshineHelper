# 0008. Módulo do launcher (PR 10)

- Status: aceita (10a: lógica; 10b: Windows e calibração)
- Data: 2026-11-23

## Contexto

A ADR 0002 ("Fechamento do launcher") e a Fase 0 (`docs/fase0-resultados.md`, "O fechamento do
Playnite") mostraram que o Sunshine nem sempre fecha o launcher (Playnite) direito no fim da
sessão: às vezes perde o rastro dele, às vezes mata à força depois de 5 s. O teste A/B mostrou
que um atraso antes de o launcher abrir ajuda. A decisão foi tirar esse fechamento do
`Exit Timeout` do Sunshine e trazê-lo para o app, com um atraso configurável a calibrar.

## Decisão

### Um módulo como os outros, o último a aplicar

`LauncherModule` (Application) segue o contrato `IEnvironmentModule`, com `Order = 4`: é
aplicado depois de tela, HDR e FPS e, portanto, **revertido primeiro**. Assim o launcher fecha
antes de a tela voltar à resolução do usuário.

- **Capture:** anota quais processos da lista já estavam abertos (PID + horário de início) e
  grava junto a própria lista e o teto de espera. O fechamento no fim usa **a política do
  início da sessão**, não a de agora; uma recuperação depois de queda age igual.
- **Apply:** espera `launcherDelayMs` (o do perfil, senão o global; padrão 0). O Sunshine só
  abre o app depois que o Prep Command responde, então esperar aqui atrasa a abertura do
  launcher. O teto do auxiliar continua valendo (ADR 0002, "Orçamento de tempo"). Responde
  "mudou" só quando há lista, para que o Revert rode no fim.
- **Revert:** para cada processo da lista que **abriu durante a sessão**, pede o fechamento
  (fechar a janela principal, como o usuário faria), espera até o teto (padrão 10 s) e, se
  ainda estiver aberto, encerra o processo e os filhos dele.
- **Não sobrevive a reinício:** depois de um reinício não há processo para fechar.

### Proteções (o que este módulo nunca faz)

- **Só nomes que o usuário escreveu.** A lista (`launcher.processNames` no `settings.json`)
  começa vazia: sem lista, o módulo não procura nem fecha nada. Nenhum launcher é assumido.
- **Nunca o que já estava aberto antes da sessão.** Comparado por PID **e** horário de início,
  porque o Windows reaproveita PIDs; e, por garantia, nunca um processo que começou antes da
  sessão (com 1 s de folga para relógios).
- **Nunca processos do Windows, do Sunshine ou deste app** (`explorer`, `svchost`, `winlogon`,
  `sunshine`, `SunshineHelper`...). O validador recusa esses nomes no `settings.json` e o módulo
  os ignora de novo, por precaução. Nome com pasta ou curinga também é recusado.
- **Um processo teimoso não trava nada.** Se não fechar nem encerrado, a sessão termina com
  **aviso** (`RevertOutcome.SucceededWithWarning`), o snapshot é apagado e as próximas sessões
  seguem normais. Manter o snapshot por causa de um processo que o usuário fecha à mão
  bloquearia todas as sessões seguintes.

### Desconexão simples não fecha o launcher

Com "reverter ao desconectar" ligado (ADR 0004), uma desconexão simples restaura a tela, mas o
cliente ainda pode voltar com `/resume`, e o Sunshine encerra o stream se o app que ele abriu
sumir. Então o leitor do log manda o fim de sessão com `reason = "disconnect"` e o orquestrador
**deixa o módulo do launcher de fora** (`RestoreAsync(modulesToLeave)`), apagando o snapshot
mesmo assim. Só o fim de verdade (`/cancel`, pelo pipe ou pelo leitor como reserva) fecha o
launcher. O auxiliar não manda motivo; ausente vale como `cancel`.

Consequência aceita: depois de um `/resume` nesse caso, a sessão nova anota o launcher como
"já aberto", e no `/cancel` final quem fecha é o próprio Sunshine, como antes deste módulo.

## Dividido em dois PRs

- **10a (este):** configuração, validação, módulo, mudanças no orquestrador e no canal, tudo
  com testes no CI usando um controlador de processos falso. O app ainda **não** liga o módulo.
- **10b:** controlador de processos real (Windows), ligação no `AppHost`, campo da lista na
  tela Geral e o roteiro de calibração do atraso na máquina do autor
  (`docs/testes-manuais.md`).

## Consequências

- O núcleo ganhou duas coisas pequenas e testadas: revert com aviso e restore que deixa
  módulos de fora. Nenhum módulo existente mudou de comportamento.
- O atraso padrão segue 0 até a calibração do 10b dizer outra coisa.
