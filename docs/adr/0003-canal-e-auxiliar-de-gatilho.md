# 0003. Canal e auxiliar de gatilho

- Status: aceita
- Data: 2026-11-22
- Depende de: `0002-arquitetura-do-nucleo.md`

## Contexto

0002 já define o protocolo do canal (named pipe, JSON por linha, campo `v`, ACL restrita)
e o orçamento de tempo. Esta ADR registra as decisões de implementação que ficaram de
fora dela, tomadas ao construir o PR 7 (`docs/fase1-plano.md`).

## Decisão

**Projeto `SunshineHelper.Protocol` novo, separado de Domain e Application.** As
mensagens do canal (`TriggerRequest`, `ChannelResponse`, `ClientInfo`, as constantes de
op/estado) são compartilhadas literalmente pelos dois lados — o auxiliar
(`SunshineHelper.Trigger`, compilado em NativeAOT) e o servidor do pipe em
`SunshineHelper.Infrastructure`. Colocar isso em Domain misturaria "o formato da
mensagem" com "as regras do negócio"; colocar em Application obrigaria o auxiliar a
referenciar o orquestrador inteiro só para falar o protocolo. Protocol não depende de
nada, é `IsAotCompatible`, e tanto Trigger quanto Infrastructure o referenciam.

**Uma mensagem só, não polimórfica.** `TriggerRequest` tem campos para `session-start` e
`session-end` ao mesmo tempo (anuláveis onde não se aplicam), em vez de um tipo por
operação com um envelope por cima. Polimorfismo em JSON exige resolução de tipo em
tempo de execução, que é exatamente o que o corte do NativeAOT torna frágil; um registro
só, com campos opcionais, serializa com o gerador de código do `System.Text.Json`
(`ProtocolJsonContext`) sem reflexão nenhuma.

**`System.Text.Json` com gerador de código, nunca `JsonSerializer` refletido.** Os dois
lados usam o mesmo `ProtocolJsonContext`: um ponto só para manter o formato em
sincronia, e compatível com o publish em NativeAOT do auxiliar desde o início, em vez de
descobrir a incompatibilidade só quando alguém publicar de verdade.

**Convenção de argumento do auxiliar: `start` / `end`.** `SunshineHelper.Trigger` recebe
`args[0]` igual a `"start"` (Do Command) ou `"end"` (Undo Command) e lê o resto do que
precisa das variáveis `SUNSHINE_CLIENT_*` que o Sunshine já exporta
(`docs/fase0-resultados.md`). Simples de configurar nos comandos do Sunshine e fácil de
testar manualmente na linha de comando.

**O auxiliar sempre volta `0`, mesmo com exceção não tratada.** Todo o `Main` fica
dentro de um `try/catch (Exception)` cujo único corpo é "não fazer nada" — a regra do
0002 (saída diferente de zero aborta a transmissão) não pode depender de nenhum caminho
de código estar certo.

**Separar o prazo da resposta do prazo do trabalho real.** Dentro do app,
`TriggerChannelHandler` passa ao `SessionOrchestrator` um `CancellationToken` de vida do
servidor (nunca o da conexão do pipe) e corre só a *resposta* contra `waitMs`, via
`Task.WhenAny`. Se o prazo vence primeiro, responde `"timeout"` (ainda com `ok:true`) e
deixa o trabalho real terminando em segundo plano, exatamente como 0002 pede. Isso evita
dois problemas: um auxiliar que desistiu não pode abortar um `ApplyAsync`/`RestoreAsync`
que já começou, e duas chamadas reais não podem correr ao mesmo tempo contra os mesmos
módulos (poderia deixar um módulo sendo revertido e aplicado ao mesmo tempo). Um
`SemaphoreSlim(1,1)` serializa essas chamadas reais; a próxima só espera a vez, do mesmo
jeito que o auxiliar esperaria se continuasse perguntando.

**Leitura de linha com limite.** `StreamReader.ReadLineAsync()` sozinho não tem limite de
tamanho. O servidor do pipe lê byte a byte até achar `\n` ou atingir 64 KB
(`NamedPipeChannelServer.MaxLineBytes`), o que é generoso para a maior mensagem prevista
e pequeno ao lado do que um processo local malicioso ou com bug poderia tentar escrever.

**Escolha de perfil nesta PR: nome do cliente, depois perfil padrão.**
`ClientProfileSelector` (Domain, função pura) casa `ClientRequest.Name` sem diferenciar
maiúsculas contra `ClientMatchRule.ClientNames` de cada perfil; sem nome que bata, usa
`AppSettings.DefaultProfileId`; sem nenhum dos dois, devolve `null` e
`TriggerChannelHandler` responde com um erro claro. Faixas de IP
(`ClientMatchRule.IpRanges`, que já existe em settings.json) e "perguntar na primeira
conexão" ficam de fora de propósito — são os dois sinais que 0002 já antecipa, mas que
dependem de mais UI do que esta PR entrega.

**`ICurrentSettingsProvider` em vez de `SettingsService` direto.** Um cliente conectando
não pode esperar a E/S assíncrona de `SettingsService` em cima do próprio orçamento de
tempo. A interface expõe só uma leitura síncrona do que já está carregado; quem mantém
isso atualizado (carga na inicialização, edição pela UI) é trabalho da PR da "Casca da
interface" (PR 9).

**NativeAOT ligado nesta PR, validado no CI.** `PublishAot=true` entra junto com a lógica
real do canal (a dependência final — `System.IO.Pipes`, `System.Text.Json` com gerador de
código — já está definida), e o workflow de CI publica o `SunshineHelper.Trigger` com
`-p:PublishAot=true` a cada execução, para pegar uma incompatibilidade de corte
imediatamente, não só quando alguém publicar uma release de verdade.

## Consequências

- Faixa de IP e "perguntar na primeira conexão" ficam pendentes para uma PR futura de
  perfis (acompanhado no plano de Fase 1 e no documento do projeto).
- "Se o canal não responde, o auxiliar tenta iniciar o app uma vez" (0002) depende da
  tarefa agendada que só a PR do instalador vai criar; até lá, o auxiliar simplesmente
  desiste depois do tempo de conexão e a transmissão segue sem o ambiente configurado.
- `tests/SunshineHelper.Infrastructure.Tests` é o primeiro projeto de teste de
  Infrastructure: pipes nomeados funcionam de verdade no runner `windows-latest`, ao
  contrário de Display/HDR/RTSS, que dependem de hardware real — então aqui dá para
  testar com um cliente e servidor reais, não só com `tools/manual-tests`.
