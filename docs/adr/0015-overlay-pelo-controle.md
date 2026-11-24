# 0015. Overlay pelo controle: leitura do controle, combo e janela (Fase 2)

- Status: proposta
- Data: 2026-11-23

## Contexto

O plano técnico pede um overlay aberto por um combo do controle (RF7 a RF10): trocar de perfil, ajustar
resolução, HDR e limite de FPS na hora, listar e fechar janelas e processos e disparar atalhos de teclado,
tudo de longe, só com o controle. O autor definiu: combo e tempo de segurar configuráveis, e **Start + Select
não pode ser o atalho**. A tabela de modelo e esforço põe a máquina de estados do combo (Fase 2) e a proteção e o
encerramento de processos (Fase 3) em Opus · esforço alto.

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
- Cada controle tem o seu estado; qualquer um dos quatro pode abrir o overlay.

Regras de validação do combo (o app recusa salvar e diz por quê):

- de **2 a 4 botões**;
- **não pode conter Start e Back juntos** (cobre Start + Select e o atalho de encerrar do Moonlight);
- tempo de segurar de **500 a 5000 ms**;
- o combo de emergência (Fase 3) tem de ser diferente do combo do overlay.

Padrão sugerido (do plano): **L3 + R3 por 1500 ms**. Fica em `settings.json` (`overlay.openCombo`,
`overlay.holdMs`), com subida do `schemaVersion` e migração que preenche o padrão.

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

| PR | O quê | Modelo |
| --- | --- | --- |
| A | Domain: detector de combo e validação; `settings.json` com o combo e migração; testes | Opus · alto |
| B | Infrastructure: leitor XInput só durante a sessão; quando o combo dispara, só uma linha no `app.log` (sem janela). Teste no PC: segurar o combo numa transmissão | Sonnet · médio |
| C | Tela de configurações "Controle": gravar o combo ("Aperte os botões...") com a barra do tempo de segurar e os avisos das regras | Sonnet · médio |
| — | Protótipo navegável do overlay (artifact HTML) para o autor aprovar antes da janela, como o plano pede | Sonnet · médio |
| D | Janela do overlay: tela Início, navegação, "Restaurar agora", foco | Sonnet · médio |
| E | Telas Perfis e Ajustes: trocar de perfil e ajustar no meio da sessão. **Pede ADR próprio**: o orquestrador precisa aplicar um novo ambiente sem perder a foto tirada antes da sessão, para o fim da sessão ainda voltar ao estado original | Opus · alto |
| F | Tela Atalhos: combinações de teclado criadas pelo usuário (SendInput) | Sonnet · médio |

A **Fase 3** (lista de janelas e processos, fechar e finalizar com lista de proteção e confirmação, combo de
emergência, tela cheia exclusiva) terá ADR próprio, em Opus · alto, porque um erro ali pode derrubar o PC.

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
- `settings.json` ganha a seção `overlay`; versões antigas do arquivo continuam abrindo (migração).
- Quem usa controle no estilo PlayStation precisa pôr o `gamepad` do Sunshine em `x360` até haver suporte; o guia
  e o README dizem isso.
- O risco R8 continua aberto na v1: o guia explica que, se o Windows recusar a troca de foco, o jogo pode receber
  os botões enquanto o overlay está aberto.
- A troca de perfil no meio da sessão mexe no núcleo e fica para um ADR próprio antes do PR E.
