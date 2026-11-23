# 0002. Arquitetura do núcleo (Fase 1)

- Status: aceita (2026-11-23; em uso desde a Fase 1)
- Data: 2026-11-22
- Depende de: `0001-stack-e-gatilho.md`, `docs/fase0-resultados.md`

## Contexto

A Fase 0 confirmou quatro coisas que mudam o desenho:

1. O Sunshine entrega `SUNSHINE_CLIENT_NAME` ao Prep Command, junto com HDR, resolução, FPS e áudio do cliente.
2. O trabalho real (resolução, HDR, RTSS) leva ~2,5 s; o cliente mais impaciente (celular) desiste aos ~57 s.
3. A confirmação de modo de vídeo não pode vir de processo externo (`QRes /S` mente).
4. O fechamento do launcher não pode depender do `Exit Timeout` do Sunshine.

Mais dois fatos do log que o desenho precisa respeitar:

- **`/resume` não executa Prep Commands.** Se o ambiente foi restaurado durante a desconexão, nada o reaplica.
- **Desconexão simples não executa Undo Commands.** Só `/cancel` executa.

## Decisão

### Processos

| Processo | Papel | Privilégio |
| --- | --- | --- |
| `SunshineHelper.exe` | app principal (WPF, bandeja, orquestrador, servidor do canal) | elevado, inicia no logon por tarefa agendada |
| `SunshineHelper.Trigger.exe` | chamado pelo Sunshine como Do/Undo Command | elevado (herda do Sunshine) |

O auxiliar existe por um motivo só: o Do Command **bloqueia** o início da transmissão até sair, e é o único ponto onde as variáveis `SUNSHINE_*` existem. Ele é compilado em **NativeAOT**, para iniciar em milissegundos e não depender do runtime .NET estar carregado.

**Regra absoluta: o auxiliar sempre sai com código 0.** O Sunshine aborta o início do app se um Prep Command falha. Erro, tempo esgotado ou app fechado nunca podem impedir a transmissão.

### Canal entre o auxiliar e o app

Named pipe local, `\\.\pipe\SunshineHelper.v1`, JSON por linha, com `v` (versão) em toda mensagem para degradar com elegância quando as versões divergirem. ACL restrita ao usuário interativo, a `SYSTEM` e a Administradores — o Sunshine pode rodar como serviço.

```
→ {"v":1,"op":"session-start","client":{"name":"XBOX","width":3840,"height":2160,"fps":60,"hdr":true,...},"waitMs":20000}
← {"v":1,"ok":true,"state":"applied","elapsedMs":2780,"profile":"TV 4K HDR"}
← {"v":1,"ok":true,"state":"timeout"}        // o auxiliar sai 0 assim mesmo
→ {"v":1,"op":"session-end","reason":"cancel"}
← {"v":1,"ok":true,"state":"reverted"}
```

Se o canal não responde, o auxiliar tenta iniciar o app uma vez e espera de novo, sempre dentro do orçamento de tempo.

### Orçamento de tempo

| Limite | Valor | Origem |
| --- | --- | --- |
| Alvo de aplicação | < 10 s | teste 2 (trabalho real 2,5 s) |
| Barreira padrão do auxiliar | 20 s | folga sobre o alvo |
| Teto absoluto | 55 s | teste 3 (celular desiste aos ~57 s) |

Estourando a barreira, o auxiliar sai com sucesso e o app termina de aplicar em segundo plano. Cada sessão registra o tempo por módulo; o app avisa quando passa do alvo.

### Módulos

Todo módulo implementa o mesmo contrato:

```csharp
public interface IEnvironmentModule
{
    string Id { get; }                 // "display", "hdr", "fps", "launcher"
    int Order { get; }                 // ordem de aplicação; reversão é a ordem inversa
    Task<ModuleSnapshot> CaptureAsync(CancellationToken ct);
    Task<ApplyOutcome> ApplyAsync(ProfileSection desired, CancellationToken ct);
    Task<RevertOutcome> RevertAsync(ModuleSnapshot snapshot, CancellationToken ct);
}
```

Módulos da v1: `display` (resolução, preservando a taxa de atualização), `hdr`, `fps` (RTSS), `launcher` (política de fechamento). Cada um sabe ler o próprio estado pela API nativa — nunca pela saída de um processo externo.

### Snapshot atômico e orquestrador

```
Aplicar:
  1. se já existe snapshot em disco, a sessão não começa
  2. snapshot = Capture de todos os módulos        (só leitura)
        se um Capture falhar → aborta sem aplicar nada
  3. grava o snapshot em disco de forma atômica    (temp + flush até o disco + File.Replace)
  4. para cada módulo, na ordem:
        marca o módulo como "aplicando" e regrava o snapshot
        Apply
        marca o resultado (aplicado / sem mudança / falhou) e regrava o snapshot
        se falhar → desfaz os já aplicados e encerra a sessão como falha
  5. marca a sessão como "aplicada"

Reverter:
  1. carrega o snapshot
  2. para cada módulo que mexeu no ambiente, na ordem inversa:
        Revert (best effort, acumula erros)
  3. apaga o snapshot só se tudo deu certo; senão mantém para o --restore
```

O snapshot vai para `%LOCALAPPDATA%\SunshineHelper\state\current-session.json` **antes** de qualquer mudança. Se o arquivo não puder ser gravado, a sessão não começa: sem rede de segurança, não se mexe no ambiente. O mesmo vale para a captura — um módulo que não consegue ler o próprio estado não tem volta, então ninguém aplica nada.

Cada módulo grava seu estado como um bloco de JSON **opaco, com versão própria**. O núcleo não conhece o conteúdo de módulo nenhum: um módulo novo entra sem mexer no núcleo, e um módulo que mudou de formato recusa um bloco antigo com mensagem clara, em vez de restaurar o estado errado.

Três pontos que a primeira versão desta ADR não resolvia:

- **O snapshot é regravado a cada passo.** Sem isso, uma queda no meio da aplicação não deixa como saber quais módulos chegaram a mexer no ambiente — e reverter um módulo que não foi aplicado muda um valor que o perfil mandou deixar quieto. Cada módulo carrega seu próprio estado (`pending`, `applying`, `applied`, `skipped`, `failed`); só `applying`, `applied` e `failed` são revertidos. Gravar o arquivo custa milissegundos, dentro de um orçamento de 10 s.
- **Uma sessão por vez.** Se uma transmissão começa com um snapshot já em disco (Prep Command duplicado, reconexão), capturar de novo salvaria o estado **já modificado** como se fosse o normal, e o usuário nunca voltaria ao estado real. O orquestrador recusa iniciar nesse caso. Reaplicar depois de um `/resume` é trabalho do leitor de log, não de uma segunda captura.
- **Enquanto o arquivo existe, o ambiente é considerado modificado.** O snapshot só é apagado quando uma reversão termina sem erro, então encontrar o arquivo na inicialização — em qualquer estado — significa que há o que restaurar.

### Recuperação após queda

Na inicialização (e via `--restore`), se existe um snapshot em disco e não há sessão ativa, o app reverte. O snapshot guarda a **identidade do boot**: um identificador do boot atual e quanto tempo o PC estava ligado no momento da captura.

- **Mesmo boot** (o app caiu): reverte tudo que mexeu no ambiente.
- **Boot diferente** (o PC reiniciou): resolução e HDR já voltaram sozinhos — reverter seria errado. Mas o **perfil do RTSS é um arquivo em disco e sobrevive ao reinício**, então esse módulo é revertido de qualquer forma.

Cada módulo declara se seu estado sobrevive a um reinício (`SurvivesReboot`), e o orquestrador usa isso para decidir.

Nenhum dos dois sinais é conclusivo sozinho, então a decisão usa os dois, nesta ordem:

1. O tempo ligado nunca diminui dentro de um mesmo boot. Se agora é **menor** que no momento da captura, o PC reiniciou — isso é prova, e não depende do relógio do sistema.
2. Identificadores iguais: mesmo boot.
3. Identificadores diferentes: pode ser reinício ou um ajuste do relógio do sistema. É reinício só se o PC está ligado há **menos tempo** do que o tempo decorrido desde a captura, porque aí ele não pode ter ficado ligado o tempo todo.
4. **Na dúvida, mesmo boot** — reverte tudo. Uma reversão desnecessária devolve o ambiente aos valores que o próprio usuário tinha; uma reversão que não acontece deixa o PC em configuração de transmissão, na resolução errada, com HDR e limite de FPS que ele não pediu. Vale também quando a identidade do boot não pode ser determinada.

### Escolha do perfil

Não há dois "tipos" fixos de aparelho: o app lida com qualquer cliente que o Moonlight suporte (TV, celular, tablet, Steam Deck, outro PC, etc.), cada um com seu próprio perfil, identificado por nome. TV e celular aparecem nos exemplos deste repositório só porque são os aparelhos usados nos testes do autor na Fase 0.

O perfil é escolhido pela primeira regra que casar, na ordem definida pelo usuário, com um perfil padrão no fim:

1. `SUNSHINE_CLIENT_NAME` (caminho principal — resolvido na Fase 0)
2. Características do pedido: HDR, resolução, FPS, configuração de áudio
3. Faixa de IP
4. Perguntar na primeira conexão e salvar a resposta

O perfil escolhe, por seção, entre **seguir o cliente**, **valor fixo** ou **não mexer**. O HDR passa a seguir `SUNSHINE_CLIENT_HDR` por padrão, em vez de ligar sempre.

### Desconexão e retomada

| Evento | Origem | Ação padrão |
| --- | --- | --- |
| `CLIENT DISCONNECTED` / `Session ended` | leitor de log | manter o ambiente (configurável por perfil) |
| `/resume` | leitor de log | se o ambiente foi revertido, reaplicar o último perfil |
| `/cancel` | Undo Command (auxiliar) | reverter e aplicar a política do launcher |

O leitor de log não é só plano B: ele é a **única** fonte para retomada e desconexão simples, porque o Sunshine não executa comando nenhum nesses casos. O app verifica na inicialização se o nível de log do Sunshine permite ver essas linhas e avisa o usuário se não permitir (sem alterar a configuração dele sem consentimento).

### Fechamento do launcher

O Sunshine encerra o app **antes** de rodar os Undo Commands, então o auxiliar chega tarde para um fechamento educado. A v1 trata o módulo `launcher` como rede de segurança no Revert: verifica se o processo sobreviveu, pede o fechamento (`WM_CLOSE` na janela principal), espera por condição com teto de tempo e só então encerra a árvore de processos.

**Ponto em aberto que a Fase 1 precisa medir:** o teste A/B mostrou que um atraso antes do Playnite abrir faz ele fechar direito depois. Se a causa for mesmo o atraso, reduzir a aplicação para menos de 10 s traz o problema de volta. Por isso o perfil ganha um **atraso configurável antes de liberar o app** (`launcherDelayMs`), separado do tempo de aplicação, com o valor padrão a calibrar. Se a rede de segurança acima resolver sozinha, o atraso fica em zero.

## Consequências

- O caminho principal não depende de ler log, o que o torna rápido e previsível; o leitor de log continua necessário para retomada e desconexão.
- Todo estado mexido é capturado antes e gravado em disco, então uma queda do app nunca deixa o PC num estado estranho sem caminho de volta.
- O contrato único por módulo mantém o núcleo testável sem Windows: os testes do Domain e do Application usam módulos falsos.
- O auxiliar em NativeAOT adiciona um alvo de compilação a mais, mas tira o runtime .NET do caminho crítico.
