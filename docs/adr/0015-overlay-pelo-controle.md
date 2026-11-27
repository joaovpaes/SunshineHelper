# 0015. Overlay pelo controle: leitura do controle, combo e janela (Fase 2)

- Status: proposta
- Data: 2026-11-23 (revista em 2026-11-27 com as respostas do autor)

## Contexto

O plano técnico pede um overlay aberto por um combo do controle (RF7 a RF10): trocar de perfil, ajustar
resolução, HDR e limite de FPS na hora, listar e fechar janelas e processos e disparar atalhos de teclado,
tudo de longe, só com o controle. O autor definiu: combo e tempo de segurar configuráveis, e **Start + Select
não pode ser o atalho**. A tabela de modelo e esforço põe a máquina de estados do combo (Fase 2) e a proteção e o
encerramento de processos (Fase 3) em Opus · esforço alto.

Respostas do autor (2026-11-24) que mudaram esta proposta:

- **Select + Y segurando 5 s fecha o jogo em foco** (combo e tempo configuráveis). A ideia veio do AutoHotkey
  que ele usa hoje para fechar o jogo e voltar ao Playnite. Esse comando **não pode vazar** para outro programa
  (fechar o Playnite sem querer logo depois de fechar o jogo), e há jogos sem menu "Sair". Essa ação ganhou ADR
  próprio, o [0016](0016-fechar-jogo-pelo-controle.md), e vem **antes** das telas do overlay.
- O overlay abre com **outro combo**, também configurável.
- Combinação exata (um botão a mais cancela): confirmada.
- Na v1, o controle só é lido durante a transmissão: confirmado.

Fatos que pesam na decisão:

- Durante a transmissão, o controle do aparelho chega ao PC como um **controle virtual** criado pelo Sunshine
  (ViGEmBus). Ele é do sistema: o jogo em primeiro plano também recebe os botões (risco R8 do plano).
- A opção `gamepad` do Sunshine, em `auto`, segue o tipo de controle que o cliente informa e pode criar um
  controle no estilo **PlayStation (DualShock 4)** quando o cliente informa sensor de movimento ou touchpad
  ([documentação de configuração do Sunshine](https://docs.lizardbyte.dev/projects/sunshine/master/md_docs_2configuration.html)).
  O XInput só enxerga controles no estilo Xbox.
- Em tela cheia exclusiva de verdade, uma janela por cima pode não aparecer na imagem transmitida (risco R7).
- O botão Guide no Xbox abre a guia do console e não chega ao Moonlight; Start + Select + LB + RB é o atalho do
  Moonlight para encerrar a transmissão.

## Decisão

### 1. Leitura do controle: XInput, só durante a sessão

- O app lê os controles por **XInput** (`XInputGetState` de `xinput1_4.dll`, por P/Invoke gerado pelo CsWin32),
  nos quatro índices, em um serviço em segundo plano (Infrastructure).
- A leitura acontece **só enquanto há sessão ativa** (o mesmo estado que o Painel mostra), a cerca de 60 vezes por
  segundo (a cada 16 ms). Fora da sessão o serviço não consulta nada, para não gastar CPU (meta do plano: menos de
  1% em repouso).
- O botão Guide não é lido (exigiria a função não documentada `XInputGetStateEx`) e não pode fazer parte do combo.
- **Limite aceito na v1:** controle virtual no estilo PlayStation não é visto. O guia explica como resolver no
  Sunshine (opção `gamepad` em `x360`). Ler esses controles (HID ou Windows.Gaming.Input) fica para depois, com
  ADR próprio.

### 2. Máquina de estados do combo (Domain, pura e testada)

O detector recebe "botões apertados agora + instante" de cada controle e decide quando o combo dispara. Fica no
Domain, sem relógio nem Windows, para ser testado com sequências inventadas.

- Estados por controle: **Ocioso** → **Segurando** (exatamente os botões do combo apertados; marca o instante) →
  **Disparou** (passou o tempo de segurar; o evento sai **uma vez**) → **Esperando soltar** (só volta a Ocioso
  quando todos os botões do combo forem soltos).
- **Combinação exata:** o combo só conta se os botões apertados forem exatamente os do combo, nem mais nem
  menos. Apertar um botão a mais durante a contagem cancela (volta a Ocioso). Isso evita disparo no meio do jogo,
  quando o jogador aperta vários botões juntos.
- Soltar qualquer botão do combo antes do tempo cancela. Controle desconectado no meio cancela e zera o estado
  daquele controle.
- Os gatilhos analógicos (LT e RT) e os direcionais analógicos **não** entram no combo na v1: só botões digitais
  (A, B, X, Y, LB, RB, Start, Back, L3, R3 e o direcional).
- Cada controle tem o seu estado; qualquer um dos quatro pode disparar.
- O mesmo detector vigia **vários combos** (hoje dois: "fechar o jogo" e "abrir o overlay"); cada um tem o seu
  tempo de segurar. Como a combinação é exata, Select + Y e Select + Y + X são combos diferentes e não disparam
  um ao outro.

Regras de validação de cada combo (o app recusa salvar e diz por quê):

- de **2 a 4 botões**;
- **não pode conter Start e Back juntos** (cobre Start + Select e o atalho de encerrar do Moonlight);
- tempo de segurar de **500 a 10000 ms**;
- os dois combos não podem ter os mesmos botões (mesmo com o "fechar o jogo" desligado, para não surpreender quem ligar depois).

Padrões (no `settings.json`, seção `controller`). A seção é opcional: um arquivo antigo carrega os padrões, sem subir o `schemaVersion`, como já acontece com `launcher` e `language`:

| Combo | Botões | Segurar | Ligado de fábrica |
| --- | --- | --- | --- |
| Fechar o jogo (ADR 0016) | Select (Back) + Y | 5000 ms | não: o usuário liga com uma caixa de marcar |
| Abrir o overlay | L3 + R3 | 3000 ms | sim, quando a janela existir (PR F) |

O padrão do overlay é **proposta**: L3 + R3 quase nunca é apertado junto de propósito, e abrir o overlay não
destrói nada, então pode segurar menos que o "fechar o jogo". O autor confirma ou troca.

### 3. Janela do overlay

- Janela WPF própria, sem borda, sempre no topo, fora do Alt+Tab (`WS_EX_TOOLWINDOW`), **sempre escura**,
  ancorada à direita com cerca de um terço da largura e o resto da tela levemente escurecido (layout do plano).
- A navegação vem do mesmo leitor XInput (direcional, A, B, X, Y), então a janela **não precisa de foco** para
  funcionar.
- Para reduzir o risco R8 (botões chegando ao jogo), ao abrir o overlay o app **tenta trazê-lo para o primeiro
  plano** e, ao fechar, **devolve o foco à janela que estava antes**. O Windows pode recusar essa troca para um
  programa em segundo plano; se recusar, o overlay funciona assim mesmo, o app registra no `app.log` e o jogo pode
  receber os botões. A opção fica configurável ("Tirar o foco do jogo ao abrir", ligada por padrão). A solução
  completa (controle virtual intermediário com ViGEm e HidHide) continua na Fase 4, opcional.
- Enquanto o overlay está aberto, o combo não o reabre; **B** na tela inicial fecha.

### 4. O que entra em cada PR (ordem)

Primeiro o "fechar o jogo" (pedido do autor), depois o overlay.

| PR | O quê | Modelo |
| --- | --- | --- |
| A | Domain: detector de combo (vários combos) e validação; `settings.json` com a seção `controller` e migração; testes | Opus · alto |
| B | Infrastructure: leitor XInput só durante a sessão; quando um combo dispara, só uma linha no `app.log`. Teste no PC: segurar Select + Y numa transmissão | Sonnet · médio |
| C | Application: regras do "fechar o jogo" (ADR 0016: alvo, proteção, confirmação, pausa), com testes | Opus · alto |
| D | Infrastructure e tela: janela em foco, pedido de fechar, encerrar, devolver o foco ao launcher; caixa "Fechar o jogo com o controle" na tela Geral. Teste no PC com jogos de verdade | Sonnet · médio |
| E | Tela de configurações "Controle": gravar os combos ("Aperte os botões...") com a barra do tempo de segurar e os avisos das regras | Sonnet · médio |
| — | Protótipo navegável do overlay (artifact HTML) para o autor aprovar antes da janela, como o plano pede | Sonnet · médio |
| F | Janela do overlay: tela Início, navegação, "Restaurar agora", foco | Sonnet · médio |
| G | Telas Perfis e Ajustes: trocar de perfil e ajustar no meio da sessão. **Pede ADR próprio**: o orquestrador precisa aplicar um novo ambiente sem perder a foto tirada antes da sessão, para o fim da sessão ainda voltar ao estado original | Opus · alto |
| H | Tela Atalhos: combinações de teclado criadas pelo usuário (SendInput) | Sonnet · médio |

O que resta da **Fase 3** (lista de janelas e processos no overlay, combo de emergência, tela cheia exclusiva)
terá ADR próprio, em Opus · alto, e reaproveita a lista de proteção do ADR 0016.

## Alternativas consideradas

- **Windows.Gaming.Input** em vez de XInput: enxerga também controles no estilo PlayStation, mas o
  comportamento com a janela sem foco e em segundo plano precisa ser medido no PC. Fica como caminho para ler o
  DualShock 4 depois, não como base da v1.
- **Raw Input / HID direto:** mais trabalho (um formato por fabricante) sem ganho para o controle Xbox, que é o
  caso comum no Moonlight.
- **Combo "contém" (aceitar botões a mais):** mais fácil de acertar, mas dispara por acidente no meio do jogo.
- **Ler o controle o tempo todo:** permitiria o overlay fora da transmissão, mas gasta CPU sempre. Pode virar
  opção depois, se houver pedido.
- **Janela que nunca toma o foco (`WS_EX_NOACTIVATE`):** não incomoda o jogo, mas deixa os botões chegarem a ele
  com certeza. Preferido tentar tomar o foco, com a opção de desligar.
- **Bloquear os botões no jogo já na v1 (ViGEm + HidHide):** resolve o R8, mas exige instalar drivers de
  terceiros. Fica para a Fase 4.

## Consequências

- O detector de combo é testável no CI sem controle nenhum; o leitor XInput e a janela só se validam no PC do autor
  (roteiros em `docs/testes-manuais.md`).
- `settings.json` ganha a seção `controller`; versões antigas do arquivo continuam abrindo com os padrões.
- Quem usa controle no estilo PlayStation precisa pôr o `gamepad` do Sunshine em `x360` até haver suporte; o guia
  e o README dizem isso.
- O risco R8 continua aberto na v1: o guia explica que, se o Windows recusar a troca de foco, o jogo pode receber
  os botões enquanto o overlay está aberto.
- A troca de perfil no meio da sessão mexe no núcleo e fica para um ADR próprio antes do PR G.
