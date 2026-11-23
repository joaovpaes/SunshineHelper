# 0004. Leitor do log do Sunshine

- Status: aceita
- Data: 2026-11-23

## Contexto

O auxiliar de gatilho e o canal (ADR 0003) cobrem tudo que o Sunshine executa um comando
para: `/launch` (Do Cmd) e `/cancel` (Undo Cmd). Dois eventos do ciclo de vida de uma
sessão não executam comando nenhum, confirmado nos logs da Fase 0
(`docs/fase0-analise-de-logs.md`):

- **Desconexão simples** (`CLIENT DISCONNECTED` + `Session ended`, sem `/cancel`): o
  cliente pode estar só pausando — indo para o menu do Moonlight, a tela apagou, a rede
  caiu — e volta com `/resume`.
- **`/resume`**: reconecta a uma sessão que nunca foi desfeita. O Sunshine não roda
  nenhum Prep Command aqui.

Sem alguém olhando o log, o app nunca saberia de nenhum dos dois. Esta ADR cobre o PR 8
do plano (`docs/fase1-plano.md`): o leitor do log e o coordenador que reage a ele.

## Decisão

### O que o leitor reconhece

`SunshineLogLineParser` (Domain, sem I/O) procura, em cada linha, por um de cinco
marcadores literais — `CLIENT CONNECTED`, `CLIENT DISCONNECTED`,
`DESTINATION :: /cancel`, `DESTINATION :: /resume`, `Session ended` — e ignora todo o
resto. O casamento é pela string exata que o Sunshine escreve, nunca pelo timestamp ou
pelo nível (`Debug`/`Info`) que vêm antes dela, então uma mudança de formato de data ou
de nível de log não quebra a detecção. As fixtures redigidas da Fase 0
(`tests/fixtures/sunshine/`) são a fonte dos dois cenários testados ponta a ponta:
celular (desconecta, resume, desconecta de novo, cancela) e TV (conecta e cancela
direto, sem desconectar antes).

### Por que "Session ended" sozinho não basta

A mesma linha `Session ended` aparece tanto depois de uma desconexão simples quanto
depois de um `/cancel` — só o que veio imediatamente antes diz qual dos dois foi.
`SunshineSessionMonitor` (Application) guarda esse sinal (`ClientDisconnected` ou
`CancelRequested`) e decide a ação só quando `SessionEnded` chega. Uma reconexão
(`CLIENT CONNECTED`) no meio limpa o sinal pendente: se a desconexão foi seguida de uma
reconexão de verdade antes do `Session ended`, não é o caso que estava sendo rastreado.

### Ação padrão e o que a torna configurável

| Evento | Ação padrão | Por quê |
| --- | --- | --- |
| Desconexão simples | manter o ambiente | o cliente pode voltar com `/resume`; reverter sem necessidade pioraria a experiência de quem só pausou |
| `/cancel` | sempre reverter | é o fim de verdade da sessão; o Undo Command do pipe (ADR 0003) é o caminho principal, isso aqui é rede de segurança |
| `/resume` | reaplicar se o ambiente foi revertido | o Sunshine não roda nada aqui; sem isso o cliente volta numa resolução/HDR que não é a dele |

`ClientProfile.RevertOnDisconnect` (novo campo, padrão `false`) deixa um perfil pedir
reversão já na desconexão simples, em vez de esperar o `/cancel`. Quando ativado, o nome
do cliente da sessão revertida é guardado em memória (não em disco: nada aqui sobrevive
a uma queda do app, que é um cenário já raro dentro de outro raro) para o `/resume`
reaplicar o mesmo perfil.

### Por que o fallback do `/cancel` nunca disputa com o pipe

`SunshineSessionMonitor` não chama `SessionOrchestrator` direto: ele chama
`TriggerChannelHandler.HandleAsync`, o mesmo método que `NamedPipeChannelServer` chama
para cada conexão do auxiliar (ADR 0003). Os dois caminhos passam pelo mesmo
`SemaphoreSlim` interno do handler, então nunca revertem ao mesmo tempo; e
`SessionOrchestrator.RestoreAsync` já responde `NothingToDo` quando não há snapshot, então
chamar essa reversão de novo depois que o Undo Command já tinha funcionado é inofensivo.
Por isso o leitor de log pode reagir ao `/cancel` sem saber se o pipe já cuidou disso.

### Reaplicar no `/resume`: só o nome do cliente

`SessionSnapshot.ClientName` já existia com esse propósito exato (comentário original na
ADR 0002: "para diagnóstico e para reaplicar depois de um `/resume`"). O monitor lê esse
campo antes de reverter e, no `/resume`, manda um `TriggerRequest` com só
`ClientInfo.Name` preenchido — exatamente como a primeira conexão de verdade, passando de
novo por `ClientProfileSelector`. Largura, altura, FPS e HDR do pedido original **não**
são guardados: um perfil com alguma dessas seções em "seguir o cliente" reaplica sem
esses valores (comportamento já previsto em `ClientRequest`, cujos campos são opcionais
exatamente para isso). **Ponto a revisitar** se isso se mostrar insuficiente na prática:
guardar o `ClientRequest` inteiro, não só o nome.

### Como o arquivo é acompanhado

`SunshineLogFileSource` (Infrastructure) faz *polling* (padrão 500 ms,
`TimingSettings.LogPollIntervalMs`), não usa `FileSystemWatcher`. Reabre o arquivo a cada
rodada, lê só os bytes novos desde a última posição, e só entrega uma linha depois de
achar o `\n` que a termina — uma linha sendo escrita no meio de uma rodada de leitura
fica para a próxima, nunca é entregue cortada. Se o arquivo some, está sem permissão, ou
encolheu desde a última posição (o Sunshine foi reiniciado e recriou o log), a próxima
leitura começa do zero. Começar a observar sempre pela **ponta final** do arquivo evita
reprocessar `CLIENT DISCONNECTED`/`Session ended`/`/resume` de sessões antigas como se
tivessem acabado de acontecer.

`FileSystemWatcher` foi descartado por ser notoriamente propenso a perder eventos sob
carga (buffer interno limitado) e por precisar de tratamento separado para o arquivo
ainda não existir, ser recriado, ou estar momentaneamente bloqueado por outro processo —
o polling trata os três casos do mesmo jeito.

### O que fica para depois

- **Aviso de nível de log insuficiente** (ADR 0002: "o app verifica na inicialização se
  o nível de log do Sunshine permite ver essas linhas"): precisa de UI, que é o PR
  seguinte ("Casca da interface"). Por ora, nada detecta esse caso — o leitor
  simplesmente não vê nada de útil até o usuário configurar o Sunshine no nível certo.
- **Caminho do log do Sunshine** (`AppSettings.SunshineLogPath`): o campo existe, mas
  continua sem valor até a UI deixar o usuário escolher — nenhum caminho de instalação é
  comum a todo usuário do Sunshine, e fixar o do autor violaria a regra do projeto.
- **Reaplicar com o pedido completo do cliente**, não só o nome — ver a seção acima.

## Consequências

- O caminho principal (Do/Undo Cmd pelo pipe) continua não dependendo do log; o leitor
  só entra para os dois casos que o Sunshine mesmo não cobre, exatamente como a ADR 0002
  já previa.
- Tudo isto é testável sem Windows de verdade: leitura de arquivo é I/O comum, não API
  do Win32. `SunshineLogFileSourceTests` roda no runner `windows-latest` do CI só porque
  `SunshineHelper.Infrastructure` como um todo é `net10.0-windows`, não porque precisasse
  ser.
- Reaplicar só pelo nome do cliente é uma limitação conhecida e documentada, não um
  descuido — revisar se um perfil que segue largura/altura/FPS/HDR do cliente se
  mostrar insatisfatório depois de um `/resume`.
