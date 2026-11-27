# 0016. Fechar o jogo pelo controle sem atingir outro programa

- Status: proposta
- Data: 2026-11-27 (revista no mesmo dia, depois do primeiro teste no PC: jogo do launcher em foco)

## Contexto

Hoje o autor usa um script do AutoHotkey: segurar um combo no controle fecha o jogo e ele volta ao Playnite.
Ele pediu que o app faça isso, com estas condições (respostas de 2026-11-24, ADR 0015):

- combo padrão **Select (Back) + Y segurando 5 s**, configurável;
- o comando **não pode passar para outro programa**: depois que o jogo fecha, o Playnite vem para a frente e não
  pode ser fechado também porque o botão continuava apertado ou foi apertado de novo;
- **há jogos sem menu "Sair"**, então pedir com educação nem sempre basta;
- ele aceita uma solução melhor que o AutoHotkey.

Fatos que pesam:

- Fechar o processo errado pode perder progresso, derrubar a transmissão (Sunshine) ou o próprio app (risco R9 do
  plano técnico).
- O app roda **sem privilégio de administrador** (ADR 0008 e 0012). O Windows não deixa um programa comum mandar
  mensagens para a janela de um programa elevado nem encerrá-lo. Um jogo aberto por um launcher elevado também
  fica elevado. Na configuração do autor, o Playnite está marcado como "executar como administrador" no Sunshine
  (risco R10), então os jogos que ele abre provavelmente estão elevados.
- Pedir para fechar a janela (`WM_CLOSE`) é o mesmo que clicar no X: o jogo pode salvar, perguntar "tem certeza?"
  ou ignorar. Encerrar o processo é imediato, mas não salva nada.
- O detector do ADR 0015 dispara **uma vez** por aperto e só se rearma quando todos os botões do combo são soltos.

## Decisão

### 1. Quem é o alvo: a janela em foco no instante do disparo, e só se passar nas proteções

Quando o combo dispara, o app olha a janela em primeiro plano, descobre o processo dela e guarda a identidade
(PID **e** horário de início, como no ADR 0008). Daí em diante só esse processo pode ser fechado; o app **nunca**
lê o foco de novo para decidir o que fechar na mesma rodada.

O app **recusa** (não faz nada, escreve o motivo no `app.log`) quando o processo em foco:

1. está na lista fixa de nomes proibidos (`LauncherSettings.ForbiddenNames`: Sunshine, este app, `explorer`,
   `dwm` e outros do Windows), ampliada com `ApplicationFrameHost` (janela de apps da loja, que hospeda vários);
2. está na **lista do launcher** (`launcher.processNames`, por exemplo o Playnite): o launcher nunca é alvo;
3. está na lista de protegidos do usuário (`controller.closeGame.protectedNames`, começa vazia, mesmo formato e
   validação da lista do launcher);
4. **começou antes da sessão** (com 1 s de folga): navegador, Discord, Steam e tudo que já estava aberto ficam a
   salvo; só o que abriu durante a transmissão pode ser fechado;
5. não pode ser inspecionado (o Windows nega o acesso): o app não adivinha.

**Quando o launcher está em foco** (regra 2), o launcher continua intocável, mas o app procura o jogo que ele
abriu: um processo **descendente do launcher** (filho, neto...) com **janela visível**, que **começou durante a
sessão** e que não cai em nenhuma das proteções acima. Se houver **exatamente um**, ele vira o alvo, com a mesma
identidade guardada (PID e horário de início) e os mesmos passos abaixo. Com nenhum, o app não faz nada; com mais de
um, também não adivinha e escreve no log os nomes que viu. Se o filho com janela não puder ser inspecionado
(administrador), o app dá o aviso da seção 6.

Motivo (teste no PC do autor, 2026-11-27): com o Playnite em tela cheia, o jogo aparecia na tela, mas o Windows
dizia que a janela em foco era a do Playnite, e o combo não fazia nada. Jogos abertos por outro launcher a partir
do Playnite (Steam, EA) não são filhos dele; esse caso fica para depois, se aparecer.

A regra 2 é a que resolve o caso do Playnite. Se a lista do launcher estiver vazia, a tela avisa ao ligar a
função: "Sem o nome do launcher, o combo pode fechá-lo se ele estiver em foco."

### 2. Como fecha: primeiro pede, depois encerra com confirmação pelo próprio controle

1. **Pedir:** manda `WM_CLOSE` para a janela em foco (como clicar no X) e espera até o teto
   (`controller.closeGame.politeTimeoutMs`, padrão 5000 ms, de 1000 a 30000). Se o jogo abrir um "tem certeza?",
   o jogador responde com o controle, como faria normalmente.
2. **Se ainda estiver aberto**, o app não encerra sozinho: fica uma **confirmação pendente** por 15 s. **Segurar o
   combo de novo** nesse tempo encerra **o mesmo processo** (mesma identidade guardada no passo anterior), com os
   filhos dele. É o caso dos jogos sem menu "Sair". Passados os 15 s sem confirmação, a pendência some e o app
   registra no log.
3. Antes de encerrar a árvore, o app confere os filhos: se algum filho estiver em uma lista de proteção, encerra
   só o processo do jogo e avisa.
4. Opção para quem quer o comportamento do AutoHotkey: **"Encerrar sem pedir confirmação"**
   (`controller.closeGame.forceWithoutConfirm`, **desligada** de fábrica). Ligada, o passo 2 encerra direto
   quando o teto acaba.

Assim, uma segunda segurada **nunca** escolhe um alvo novo: ou confirma o encerramento do mesmo jogo, ou não faz
nada.

### 3. Não vazar para o próximo programa: pausa depois de cada rodada

- O detector já exige soltar todos os botões antes de disparar de novo (ADR 0015).
- Depois que o alvo fecha (por pedido ou encerrado) ou a rodada termina, o combo de fechar fica **ignorado por
  5 s** (`cooldownMs`, fixo na v1). O que aparecer em foco nesse tempo, quase sempre o Playnite, não pode ser
  atingido nem por uma segunda segurada rápida.
- Somando as camadas, o Playnite fica protegido três vezes: pela lista do launcher, pela pausa e pela exigência de
  soltar os botões.

### 4. Voltar ao launcher (o que o AutoHotkey não garantia)

Quando o jogo some e há um launcher da lista aberto, o app **tenta pôr a janela do launcher em primeiro plano**.
O Windows pode recusar a troca de foco para um programa em segundo plano; se recusar, o app registra e segue (o
Windows normalmente já mostra a próxima janela). Opção "Voltar ao launcher depois de fechar", ligada de fábrica.

### 5. Ligar e desligar

- A função vem **desligada** de fábrica: fechar programa é destrutivo, e quem baixa o app não deve ter isso
  ligado sem saber. O autor liga com uma caixa na tela Geral ("Fechar o jogo segurando Select + Y por 5 s").
- Só funciona **durante a transmissão**, como todo o controle na v1.

### 6. Administrador: limite aceito e explicado

O app continua sem elevação. Quando o jogo está elevado, o pedido e o encerramento falham por acesso negado; o
app registra um **aviso claro** no `app.log` e na tela Log: "O jogo roda como administrador; o app não consegue
fechá-lo. No Sunshine, desmarque 'Executar como administrador' no app do launcher." O guia (`preview-como-usar.md`)
e o README ganham essa orientação. Um ajudante elevado (tarefa agendada com privilégio máximo) fica fora da v1 e,
se vier, terá ADR e revisão de segurança próprios.

### 7. Onde fica cada parte

- **Domain:** o tipo das opções (`CloseGameSettings`) e a validação.
- **Application:** `CloseGameService`, a máquina "ocioso → pedindo → confirmação pendente → pausa", com as
  proteções em `CloseGameProtection`, testada com relógio, janelas e processos falsos. As operações de janela
  (janela em foco, pedir para fechar a janela, filhos do processo, encerrar só o processo, trazer à frente) ficam
  numa interface nova, `IGameWindowController`; esperar, encerrar a árvore e achar o launcher reaproveitam o
  `IProcessController` do ADR 0008, sem mudar a interface dele.
- **Infrastructure:** `GetForegroundWindow` e `GetWindowThreadProcessId` (CsWin32), `PostMessage(WM_CLOSE)`,
  encerramento pelo `WindowsProcessController` já existente (que confere PID e horário de novo antes de agir) e
  `SetForegroundWindow` para o launcher.

## Alternativas consideradas

- **Encerrar direto, como o AutoHotkey:** mais simples, mas perde o progresso dos jogos que salvam ao fechar e
  não deixa responder o "tem certeza?". Fica como opção desligada.
- **Fechar sempre o último processo aberto pelo launcher** (filho do Playnite) em vez da janela em foco: não
  depende do foco, mas erra com jogos que abrem por outro launcher (Steam, EA) ou por um processo intermediário.
  A janela em foco é o que o jogador está vendo. Ficou só como **segunda opção**, quando o foco está no próprio
  launcher, e só com um candidato único (ver "Quem é o alvo").
- **Confirmar numa janela na tela:** precisa do overlay, que vem depois. A segunda segurada funciona já, sem
  janela; quando o overlay existir, ele pode mostrar "Segure de novo para encerrar".
- **Rodar o app como administrador:** resolveria o R10, mas aumenta muito o estrago de qualquer erro e contraria
  os ADRs 0008 e 0012.

## Consequências

- O caso "fechei o jogo e o Playnite fechou junto" fica bloqueado por três regras independentes, todas testadas no
  CI.
- Nada que estava aberto antes da transmissão pode ser fechado pelo combo.
- Na configuração atual do autor (Playnite elevado no Sunshine), a função provavelmente vai avisar "roda como
  administrador" até ele desmarcar essa opção no Sunshine. Esse é o primeiro teste no PC.
- `settings.json` ganha `controller.closeGame` (ligado, combo, tempo de segurar, teto do pedido, encerrar sem
  confirmação, voltar ao launcher, protegidos); arquivo antigo carrega os padrões.
- Roteiros novos em `docs/testes-manuais.md`: jogo com menu "Sair", jogo sem menu, jogo que pergunta "tem certeza?",
  combo segurado com o Playnite em foco (nada acontece), segunda segurada logo depois de fechar (nada acontece) e
  jogo elevado (aviso).
