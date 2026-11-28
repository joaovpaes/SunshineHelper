# Testes manuais

O que o CI não consegue verificar sozinho: Infrastructure e UI, por mexerem em
Windows de verdade (vídeo, HDR, RTSS, processos, janelas), não têm teste
automático até a Fase 2 (`docs/fase1-plano.md`, "Riscos desta fase"). O CI
confirma que o código compila; só a máquina do autor confirma que ele
funciona.

Cada item abaixo corresponde a um PR. Marque quando tiver testado na própria
máquina; um PR com item aqui pendente ainda não está validado de ponta a
ponta, mesmo com o CI verde.

## Pendências (atualizado em 2026-11-23, à tarde)

Rodada de 2026-11-23 no PC do autor (Sunshine 2026.914, RX 9070 XT, tela 2560x1440):

**Passou**
- Tela Perfis (PR 11b), sem transmitir: lista vazia, criar perfil, erro de resolução com `1920`, "Seguir o
  dispositivo" desliga a caixa, renomear mantém o `id`, troca de idioma.
- Launcher, campos da tela Geral (PR 10b): `abc` dá "Atraso inválido"; `Playnite.FullscreenApp.exe` é salvo
  sem `.exe`.
- Tempo de início da transmissão: imagem no celular em ~3 s (portão, item 4).
- App encerrado à força com transmissão ativa: ao abrir o app de novo a tela voltou, o HDR desligou e o
  Playnite foi fechado (portão, item 2).
- PC reiniciado com transmissão ativa: sem problemas no Painel (portão, item 3).
- Aviso de nível de log: aparece com o Sunshine em Info. Só aparece depois de reiniciar o **app**, não só o
  Sunshine (melhoria futura: reler o log quando ele for recriado). O log do Sunshine confirma o formato real:
  `config: 'min_log_level' = 1` é Debug.
- Restaurar ao encerrar a sessão: a tela voltou ao normal e o Playnite fechou depois do `/cancel`.
- **PR 34 (HDR antes da resolução), 2026-11-23 14:26:** perfil 1920x1080 fixo com HDR ligado. Durante a
  transmissão a tela ficou em 1920x1080 com HDR ativado. O `app.log` mostra `hdr` antes de `display` e a releitura
  aos 3 s com "unchanged since" (antes: "SOMETHING CHANGED IT"). Ao encerrar (Quit), `launcher`, `display` e
  `hdr` foram revertidos sem erro e a tela voltou a 2560x1440 sem HDR.
- Atraso do launcher `30001`: aparece "Atraso inválido" (0 a 30000).
- **PR 12d (relatório de diagnóstico), 15:19:** versão `1.0.0` (sem `<ip>`), IP da descrição virou `<ip>`,
  nome de usuário e IPs reais ausentes, `uniqueid` redigido.
- **PR 13a (falha de um módulo vira aviso), 15:49:** sem administrador, perfil 1920x1080 + HDR + FPS 60.
  `apply module 'fps': Failed` (sem permissão), `revert module 'fps': ok`, `with 1 warning(s)`; tela em
  1920x1080 com HDR; Painel com "Não foi aplicado nesta transmissão: Limite de FPS"; RTSS no limite original;
  ao encerrar, só `launcher`, `display` e `hdr` revertidos.
- **PR 13b (sessão anterior presa), 2026-11-23 16:23:** transmissão normal às 16:21; com a tela em 1920x1080 + HDR,
  `sunshinesvc.exe` foi finalizado pelo Gerenciador de Tarefas (sem Undo) e o SunshineHelper ficou aberto. Ao
  iniciar o Sunshine e transmitir de novo, o `app.log` mostra `session-start found a previous session on disk;
  restoring it first`, os 3 `revert module ... ok`, os `apply module` e a resposta `applied` com o aviso "Uma
  sessão anterior que não foi encerrada foi restaurada antes desta.". O Quit seguinte voltou tudo ao normal.
  (Reabrir o app antes, como numa tentativa anterior, testa só a restauração da inicialização, não o 13b.)

- **PR 15a (RTSS só em memória), 18:01:** sem administrador, opção 6 com limite 60 no Death Stranding. O RTSS
  aceitou e a releitura confirmou, mas o jogo seguiu em ~170 FPS e o arquivo `Profiles\Global` não mudou (mesmo
  hash). Conclusão: o caminho só em memória não serve; segue para o 15b (ADR 0012). A ferramenta compilou e rodou.

- **PR 15b-1 (permissão só no perfil global do RTSS, ADR 0012), 18:21:** sem administrador, opção 7 pediu o UAC
  (aceito) e terminou com "seu usuário já pode gravar"; opção 3 com 60: o jogo travou em 60 FPS; opção 5 devolveu
  o limite (estava em 171). A suposição do ADR 0012 (o RTSS regrava o arquivo no mesmo lugar) está confirmada.

- **PR 15b-2 (avisos e botão "Reparar"), ponta a ponta, 2026-11-23 18:40-18:53:** sem administrador, com o app novo.
  Permissão retirada com `icacls ... /remove:g`; em Perfis (FPS "Valor fixo" 60) apareceu o aviso com o botão
  "Reparar"; o UAC pediu uma vez e saiu "Pronto: o app já pode gravar o limite de FPS do RTSS.". Transmissão com o
  RTSS fechado: o Painel avisou que o RTSS não está aberto; com ele aberto o aviso sumiu e o jogo ficou em 60 FPS.
  Ao encerrar (Quit), voltou ao limite anterior (163 FPS no OSD) e à resolução 2560x1440. Observação: a janela do
  RTSS seguiu mostrando 171 no campo "Framerate limit" mesmo com o limite valendo (a janela não relê o perfil);
  não clicar nela durante a transmissão.

- **PR 16 (restaurar sozinho quando o Sunshine encerra, ADR 0013), 19:14-19:15:** transmissão com o perfil "Sala TV 4k"
  (1920x1080, HDR, FPS 60); `sunshine.exe` finalizado pelo Gerenciador de Tarefas com o SunshineHelper aberto. O
  `app.log` mostra `watching 1 Sunshine process(es)` 1 s depois do início, `the Sunshine process of this session is
  gone` e, em seguida, os 4 `revert module ... ok` e `in-process end answered: reason 'sunshine-exited', ok True,
  state 'reverted'`. Tudo voltou em cerca de 4 s, sem reabrir nada, e o Painel mostrou "Nenhuma transmissão ativa". O
  serviço reabriu o Sunshine sozinho com outro PID (27216 para 2088), o caso que a comparação por PID cobre.
  Depois de o Sunshine reabrir, uma transmissão nova com Quit funcionou normalmente.

**Falhou e foi corrigido**
- A resolução do perfil nunca era aplicada. O `app.log` (PR 12c) mostrou que a troca para 1920x1080 funcionava
  e era desfeita logo depois de o HDR ligar. Confirmado com `tools/manual-tests`: ligar o HDR depois de trocar a
  resolução devolve a tela a 2560x1440. Corrigido no PR 34 (HDR antes da resolução, ADR 0010).

**Ícone e iniciar com o Windows (PRs 56 e 57), 2026-11-23, build local no PC do autor:** o ícone novo aparece na
janela, na barra de tarefas e na bandeja; a opção "Iniciar com o Windows, minimizado na bandeja" aparece na tela
Geral e, depois de marcar e reiniciar o PC, o app abriu **só na bandeja**, sem janela. Passou. (Uma cópia antiga já
aberta na bandeja faz a cópia nova sair em silêncio; foi o que confundiu o primeiro teste.)

**Dispositivos e botão "Criar perfil" (PRs 60 e 62), 2026-11-23, `v0.1.0-preview.3` e `.4`:** a tela Dispositivos mostrou
o aparelho com nome, conexões, última vez e perfil. Na preview.4, "Criar perfil para este aparelho" abriu a tela Perfis
com o nome já preenchido, o perfil foi salvo (1920x1080, FPS 120) e a transmissão seguinte usou o perfil novo (`app.log`:
`apply start: profile 'celular'`). Passou.

**Tela Log (PR 64), 2026-11-23, `v0.1.0-preview.5`:** a tela mostrou o fim do `app.log`; com "Mostrar só avisos e erros"
ficaram só as 6 linhas `[WARNING]`/`[ERROR]`, na ordem; Copiar trouxe o mesmo texto. Passou. Ajustes de forma
anotados no backlog (linhas longas sem quebra; botões abaixo da área de texto exigem rolar a página).

**Ainda pendente**
- PR 12d, opcional: gerar o relatório com o caminho do log do Sunshine vazio (a seção diz "not configured").
- Calibração do atraso do launcher (0 / 3000 / 5000 ms, 3 rodadas cada). A resolução já está corrigida e o
  fechamento do Playnite foi confirmado; falta só medir.
- Versão no pacote de pré-lançamento (PR 56): na tela Geral, "Sobre" deve mostrar `0.1.0-preview.2` (no `dotnet run`
  aparece `1.0.0`, o que é normal).
Observação de uso: no Moonlight, só **encerrar a sessão** (Quit) faz o Sunshine rodar o comando Undo. Apenas
sair/desconectar deixa tudo como está, a menos que o perfil tenha "Restaurar a tela ao desconectar".

## PR 5a — Módulo de vídeo: resolução (`PrimaryDisplayModule`)

Ferramenta pronta em `tools/manual-tests/display-module/` — roda o módulo de
verdade (não uma simulação), com um menu interativo.

1. Copie ou clone o repositório na máquina com o monitor de teste.
2. Rode, fora de uma transmissão do Sunshine (não precisa ser administrador):
   ```
   powershell -ExecutionPolicy Bypass -File .\tools\manual-tests\display-module\run.ps1
   ```
   ou dê duplo clique em `tools\manual-tests\display-module\run.bat`.
3. Siga o checklist pelo próprio menu:

- [ ] Opção **1**: confere o modo atual mostrado bate com o que o Windows
      relata em Configurações > Sistema > Vídeo.
- [ ] Opção **2**, depois **3** pedindo uma resolução diferente da atual (ex.:
      3840×2160 se você usa 2560×1440): a tela muda de resolução **sem** mudar
      a taxa de atualização.
- [ ] Opção **4**: a tela volta exatamente ao que a opção 2 capturou.
- [ ] Opção **5**: pede 7680×4320 (8K) e confirma que falha de forma
      controlada, sem travar a tela num estado estranho.
- [ ] Depois da opção 3, conferir que a resolução aplicada **não** vira
      permanente: feche a ferramenta sem reverter, faça logoff/logon, e veja
      se o Windows volta sozinho à resolução de antes (prova de que
      `dwFlags = 0` não gravou nada no registro). Se não voltar sozinho, rode
      a ferramenta de novo e use a opção 4 para arrumar.

## PR 5b — Módulo de HDR (`HdrModule`)

Ferramenta pronta em `tools/manual-tests/hdr-module/` — roda o módulo de
verdade (não uma simulação), com um menu interativo.

1. Copie ou clone o repositório na máquina com o monitor de teste (precisa
   suportar HDR).
2. Rode, fora de uma transmissão do Sunshine (não precisa ser administrador):
   ```
   powershell -ExecutionPolicy Bypass -File .\tools\manual-tests\hdr-module\run.ps1
   ```
   ou dê duplo clique em `tools\manual-tests\hdr-module\run.bat`.
3. Siga o checklist pelo próprio menu:

- [ ] Opção **1**: confere se o estado mostrado bate com o que Configurações >
      Sistema > Tela > HDR mostra no Windows.
- [ ] Opção **2**, depois **3** (ligar) com o HDR desligado no início: o
      Windows liga o HDR (confirme olhando a tela e/ou o toggle nas
      Configurações).
- [ ] Opção **4** (desligar) com o HDR ligado: o Windows desliga.
- [ ] Opção **5**: o HDR volta exatamente ao estado que a opção 2 capturou.
- [ ] **Comparação pedida pelo autor antes de descartar a ferramenta
      externa**: repita ligar/desligar com o `HDRSwitch.exe` na mesma máquina
      e confirme que o resultado (o que aparece em Configurações > Sistema >
      Tela) é o mesmo nos dois casos. Só depois dessa comparação bater é que
      o `HDRSwitch.exe` deixa de ser necessário como alternativa.
- [ ] Testar com o HDR desligado num monitor que você saiba que **não**
      suporta HDR (se tiver um à mão): a opção 3 (ligar) deve falhar com uma
      mensagem clara, sem travar nada.

## PR 6 — Módulo de FPS via RTSS (`RtssFpsLimitModule`)

Ferramenta pronta em `tools/manual-tests/rtss-module/` — roda o módulo de
verdade (não uma simulação), com um menu interativo.

1. Copie ou clone o repositório na máquina de teste.
2. Rode, fora de uma transmissão do Sunshine:
   ```
   powershell -ExecutionPolicy Bypass -File .\tools\manual-tests\rtss-module\run.ps1
   ```
   ou dê duplo clique em `tools\manual-tests\rtss-module\run.bat`.
   As opções **1** e **2** (só leitura) funcionam num PowerShell comum. As
   opções **3**, **4** e **5** (aplicar/reverter) **precisam de um PowerShell
   como administrador** — o perfil do RTSS fica dentro de `Program Files`,
   que o Windows só deixa gravar com elevação. Isso foi confirmado testando
   na prática: sem administrador, o RTSS "concorda" em mudar o valor (a
   chamada nativa não tem como saber que a gravação falhou), mas o arquivo
   não muda — por isso o módulo confere a permissão antes de tentar, e falha
   com uma mensagem clara em vez de fingir que funcionou.
3. Siga o checklist pelo próprio menu:

- [ ] **Com o RTSS instalado e aberto, administrador**: opção **1** confere
      se o estado mostrado (instalado, limite atual) bate com o painel do
      próprio RTSS.
- [ ] Opção **2**, depois **3** pedindo um limite diferente do atual (ex.: 60):
      o RTSS passa a aplicar esse limite **sem precisar reabrir o programa**
      — confirme no OSD do RTSS durante um jogo, ou no próprio painel do RTSS.
- [ ] Opção **4** ("sem limite", o `0` que é a convenção do próprio RTSS): o
      RTSS para de limitar.
- [ ] Opção **5**: o limite volta exatamente ao que a opção 2 capturou.
- [ ] **Sem administrador**: repita a opção **3** num PowerShell comum — deve
      falhar com uma mensagem explicando que falta permissão de administrador
      para gravar o perfil, e **não** deve dizer "aplicado" com o valor sem
      ter mudado de verdade.
- [ ] **Com o RTSS instalado mas fechado** (processo não rodando), como
      administrador: repita capturar/aplicar/reverter — o módulo tem que
      achar o RTSS pelo registro ou pelo caminho padrão, sem precisar que ele
      esteja em execução, e sem precisar abri-lo.
- [ ] **Sem o RTSS instalado** (desinstale temporariamente, ou teste numa
      máquina sem RTSS): opção **1** e **2** devem dizer claramente "RTSS não
      instalado", sem travar ou lançar erro. Opção **3** (pedir um limite)
      deve falhar com uma mensagem explicando que não há RTSS para aplicar o
      limite — essa é uma falha esperada e controlada, não um bug.

## PR 7 — Auxiliar de gatilho e canal (`SunshineHelper.Trigger` + named pipe)

A parte automática (`tests/SunshineHelper.Infrastructure.Tests`) já cobre o
protocolo e o servidor do pipe com um cliente de verdade. O que só dá para
confirmar na própria máquina é o auxiliar publicado de verdade (NativeAOT) e
a ACL do pipe com contas reais do Windows.

Ferramenta pronta em `tools/manual-tests/trigger-channel/` — hospeda o canal
de verdade (sem mexer em vídeo/HDR/RTSS: o perfil de teste não muda nada na
tela) para o auxiliar de verdade se conectar.

1. Num terminal, deixe o harness rodando:
   ```
   powershell -ExecutionPolicy Bypass -File .\tools\manual-tests\trigger-channel\run.ps1
   ```
2. Em outro terminal (comum, sem administrador):
   ```
   dotnet run --project src\SunshineHelper.Trigger -- start
   echo $LASTEXITCODE
   dotnet run --project src\SunshineHelper.Trigger -- end
   echo $LASTEXITCODE
   ```
3. Siga o checklist:

- [x] Os dois comandos sempre terminam com código de saída **0** — essa é a
      regra que não pode quebrar (`docs/adr/0002-arquitetura-do-nucleo.md`):
      um valor diferente de zero aborta a transmissão de verdade.
      _Confirmado pelo autor em 2026-11-23, com e sem administrador._
- [x] `start` sem nenhuma variável `SUNSHINE_CLIENT_*` definida ainda
      funciona (cai no perfil padrão do harness).
- [x] `end` sem um `start` antes responde sem travar nem demorar (nada para
      reverter).
- [x] Repita `start`/`end` com o terminal do auxiliar aberto **como
      administrador**: deve funcionar igual ao terminal comum — a ACL do
      pipe tem que deixar o usuário interativo e Administradores passarem.
      _Confirmado: mesmo resultado com e sem administrador._
- [x] Com o harness **fechado** (canal não escutando), rode `start`: o
      auxiliar deve desistir em cerca de **2 segundos** e sair com código **0**
      de qualquer forma. Esse tempo é o que o Sunshine esperaria antes de
      começar a transmissão com o app desligado, então se demorar muito mais
      que isso é bug.
- [ ] Publique o auxiliar de verdade em NativeAOT e repita o passo 2 com o
      `.exe` publicado em vez de `dotnet run` (o CI já faz esse publish a
      cada execução, mas vale confirmar que o binário também roda na sua
      máquina):
  ```
  dotnet publish src\SunshineHelper.Trigger --configuration Release --runtime win-x64 -p:PublishAot=true
  ```

## PR 8 — Leitor do log do Sunshine

Sem passo manual. Diferente dos módulos acima, ler o log do Sunshine é I/O comum
(abrir arquivo, ler linhas) — não precisa de monitor, driver nem Win32, então a
cobertura inteira é automática: `tests/SunshineHelper.Domain.Tests/Monitoring/`
(reconhecimento das linhas, incluindo as fixtures reais de
`tests/fixtures/sunshine/`), `tests/SunshineHelper.Application.Tests/Monitoring/`
(o que fazer com cada evento) e `tests/SunshineHelper.Infrastructure.Tests/Monitoring/`
(acompanhar um arquivo de verdade crescendo, sendo truncado, ainda não existindo). Ver
`docs/adr/0004-leitor-do-log.md`.

## PR 9a — Casca da interface (janela principal, temas e idiomas)

O CI confirma que o app compila e que as cores e os textos estão coerentes
(`tests/SunshineHelper.Domain.Tests/Ui/`); só você vê se a janela ficou como
deveria. Abra o app (`dotnet run --project src\SunshineHelper.App`):

- [ ] A janela abre sem erro, com o menu lateral (Painel, Perfis, Dispositivos,
      Log, Geral) e o título e a descrição de cada tela mudando ao clicar.
- [ ] O item selecionado do menu tem a barra azul à esquerda (não só mudança de cor).
- [ ] **Tema:** com o Windows no modo escuro a janela é escura; mude o Windows para
      o modo claro (Configurações > Personalização > Cores) com o app aberto: a
      janela passa para o claro sozinha, sem reabrir, e o texto continua legível.
- [ ] **Idioma:** com o Windows em português a janela fala português; em inglês,
      inglês. (Outro idioma qualquer cai em inglês.)
- [ ] Nenhum texto fica cortado ou vazio em nenhuma das quatro combinações
      (escuro/claro x português/inglês).

## PR 9b — App ligado, bandeja e Painel

O CI só prova que compila e que a lógica do estado está certa. Abra o app
(`dotnet run --project src\SunshineHelper.App`) e confira:

- [ ] Aparece um ícone na bandeja (perto do relógio). Clique direito: **Abrir** e **Sair**
      (em inglês: Open / Exit, conforme o idioma do Windows).
- [ ] Clicar no X da janela só esconde; duplo clique no ícone (ou **Abrir**) traz de volta.
- [ ] **Sair** fecha de verdade e o ícone some da bandeja.
- [ ] Abrir o app uma segunda vez não abre outra janela nem outro ícone.
- [ ] Painel mostra "Nenhuma transmissão ativa." e "Leitor do log desligado" (ainda não há
      como definir o caminho; isso é o 9c).
- [ ] **Canal de verdade:** com o app aberto, rode o auxiliar (`SunshineHelper.Trigger`) com o
      argumento `start` (defina antes `SUNSHINE_CLIENT_NAME=Sala` no terminal) e um perfil no
      `settings.json`; o Painel passa a "Transmissão ativa"
      com o nome do dispositivo e o perfil. O argumento `end` volta a "Nenhuma transmissão".
- [ ] **Restauração na abertura:** com uma sessão ativa, encerre o app pelo Gerenciador de
      Tarefas (encerrar tarefa), abra de novo: o ambiente volta ao que era sozinho.
- [ ] `settings.json` com texto quebrado (apague uma chave `}`): o app abre mesmo assim e o
      Painel lista o problema.

## PR 9c — Tela Geral (idioma e caminho do log)

Abra o app e vá em **Geral**:

- [ ] O seletor de idioma mostra "Seguir o Windows" (ou o que foi salvo). Escolha
      **English**: a janela inteira passa para inglês na hora, sem reabrir. Feche o app
      (**Sair** na bandeja), abra de novo: continua em inglês. Volte para "Seguir o Windows".
- [ ] Digite um caminho sem letra do disco (por exemplo `sunshine.log`) e clique **Salvar**:
      aparece "Caminho inválido" e nada é salvo.
- [ ] Cole o caminho completo do `sunshine.log` do seu Sunshine (pode colar com aspas) e
      clique **Salvar**: aparece "Salvo". No **Painel**, "Leitor do log ativo." (sem reabrir).
- [ ] "Procurar..." abre a janela de arquivo e preenche a caixa com o que você escolher.
- [ ] Esvazie a caixa e **Salvar**: "O leitor do log está desligado" e o Painel volta a dizer
      que está desligado.
- [ ] Com o caminho salvo, abra `%LOCALAPPDATA%\SunshineHelper\settings.json`: tem `language`
      e `sunshineLogPath` com o que você escolheu. O resto do arquivo não foi estragado.
- [ ] **Leitor de verdade:** com o caminho certo e o app aberto, conecte pelo Moonlight com um
      perfil configurado e desconecte/retome: o log do app (ou o Painel) acompanha. (Só vale
      se você já tem um perfil no `settings.json`.)

## PR 10b — launcher (tela Geral e calibração)

> **PENDÊNCIA (adiada pelo autor em 2026-11-23):** este roteiro e a tabela de calibração ainda
> **não foram feitos**. Fazer quando estiver em casa, no PC com o Sunshine. Até lá o atraso
> padrão fica em 0 e o launcher só é fechado se a lista de nomes for preenchida.

Pré-requisito: `settings.json` com um perfil e o Sunshine configurado para chamar o
SunshineHelper no Prep Command.

- [ ] Em **Geral**, role a página: aparecem "Programas a fechar" e "Atraso". Em inglês, o
      mesmo, traduzido.
- [ ] Digite o atraso `abc` e **Salvar**: aparece "Atraso inválido". Digite `30001`: também.
- [ ] Digite `Playnite.FullscreenApp.exe` (com `.exe`) e **Salvar**: a caixa passa a mostrar
      `Playnite.FullscreenApp`. Em `settings.json`: `launcher.processNames` com esse nome.
- [ ] Escreva `explorer` e Salvar: o app aceita a tela, mas o módulo nunca o fecha (nomes
      proibidos são ignorados). Depois apague.
- [ ] **Já aberto não fecha:** abra o launcher à mão, inicie e encerre uma transmissão: ele
      continua aberto.
- [ ] **Fecha o que a transmissão abriu:** com o launcher fechado, inicie pelo Moonlight e
      encerre (/cancel): ele fecha sozinho. Anote se foi educado ou à força.
- [ ] **Desconectar sem encerrar** (com "reverter ao desconectar"): a tela volta, mas o
      launcher continua; ao retomar o stream, o app dele ainda está lá.

### Calibração do atraso (anote os resultados)

Repita 3 vezes cada valor (0, 3000, 5000 ms) e veja se o launcher abre direito na
transmissão e fecha no fim:

| Atraso | Abriu certo? | Fechou no fim? | Observação |
|-------:|:------------:|:--------------:|------------|
| 0      |              |                |            |
| 3000   |              |                |            |
| 5000   |              |                |            |

Passe a tabela preenchida: o padrão do atraso só muda com esses números.

## PR 11b — tela Perfis

- [ ] Abra **Perfis**. Sem nenhum perfil, a lista mostra só "(novo perfil)" e **Excluir** está
      desligado.
- [ ] Preencha: nome "Sala TV 4K", dispositivo (o nome do seu Moonlight), Resolução "Valor fixo"
      `3840x2160`, HDR "Valor fixo" ligado, FPS "Valor fixo" `60`. **Salvar perfil**: aparece
      "Perfil salvo" e o nome entra na lista.
- [ ] Abra `%LOCALAPPDATA%\SunshineHelper\settings.json`: o perfil tem `id` `sala-tv-4k` e os
      valores digitados. O resto do arquivo não foi estragado.
- [ ] Mude "Seguir o dispositivo" em Resolução: a caixa da resolução fica desligada.
- [ ] Digite `1920` (sem altura) com "Valor fixo" e salve: mensagem de resolução inválida e
      nada é salvo.
- [ ] Renomeie o perfil e salve: o `id` no arquivo continua o mesmo.
- [ ] Marque "Usar como perfil padrão", salve, feche e abra o app: continua marcado.
- [ ] **Excluir** pede confirmação; "Não" não apaga; "Sim" apaga e limpa o padrão.
- [ ] Troque o idioma em Geral: os textos da tela Perfis mudam na hora.
- [ ] **De ponta a ponta (fecha a pendência do launcher e do nível de log):** com um perfil que
      case com o seu Moonlight, conecte, confira que a resolução/HDR/FPS mudam, desconecte e
      confira que voltam.

## PR B do ADR 0015 — leitor do controle (XInput)

Ainda sem tela: o combo só aparece como linha no `app.log` (aba **Log** do app). O "fechar o jogo" vem
**desligado** de fábrica, então este teste começa ligando-o à mão no `settings.json` (a caixa na tela Geral vem
no PR D).

Antes: no Sunshine, deixe a opção `gamepad` em `x360` (um controle virtual no estilo PlayStation não é visto
pelo XInput). Feche o app. Abra `%LOCALAPPDATA%\SunshineHelper\settings.json` e, no fim, antes da última
chave, acrescente (ou edite, se já existir) esta parte, com uma vírgula depois da seção anterior:

```json
"controller": {
  "closeGame": { "enabled": true }
}
```

- [ ] Abra o app e **sem transmitir** segure Select + Y por 10 s num controle ligado ao PC: nada aparece no
      `app.log` (o app só lê o controle durante a transmissão).
- [ ] Inicie uma transmissão pelo Moonlight. No `app.log` aparecem
      `watching the controllers for 1 combo(s): closeGame` e `controller 0 connected (Xbox-style, read by XInput)`
      (o número pode ser outro).
- [ ] Pelo Moonlight, segure Select + Y por 5 s: aparece **uma** linha
      `combo 'closeGame' fired on controller 0 (held 5000 ms)`. Continue segurando por mais 10 s: nenhuma linha nova.
- [ ] Solte tudo e segure de novo por 5 s: aparece outra linha.
- [ ] Segure Select + Y e, antes dos 5 s, aperte X também: não dispara. Solte o X e segure o combo certinho por 5 s:
      dispara.
- [ ] Segure Select + Y por 3 s e solte: não dispara.
- [ ] Encerre a transmissão e segure o combo no controle do aparelho de novo (se ainda estiver ligado ao PC): nada
      no log.
- [ ] Veja o uso de CPU do app no Gerenciador de Tarefas durante a transmissão (esperado: menos de 1%).
- [ ] Se o controle virtual for do tipo PlayStation: confirme que **não** aparece a linha "connected". Isso é o
      limite conhecido (ADR 0015).

Depois do teste, desligue (`"enabled": false`) ou apague a seção `controller` se não quiser o combo ligado.

## PR D do ADR 0016 — fechar o jogo pelo controle

Agora a função existe de verdade: a caixa **Fechar o jogo com o controle** fica na tela **Geral** (desligada de
fábrica). O combo padrão é Select + Y por 5 s. Os textos de cada passo estão no `app.log` (aba **Log**), em
linhas que começam com `closeGame`.

Antes: no Sunshine, `gamepad` em `x360`. No app, na tela **Geral**, ponha o nome do launcher na lista de
programas (por exemplo `Playnite.FullscreenApp`) e marque **Fechar o jogo que está na frente...**. A mudança vale
na hora, sem reiniciar o app. Use um jogo que **não** rode como administrador (veja o último item).

- [x] **Jogo com menu "Sair" ou que fecha normal** (2026-11-27, Mortal Shell 2: fechou em 1,6 s e o Playnite voltou;
      o log disse que o Windows não deixou trazer o Playnite para a frente, mas ele já estava na frente): abra o jogo pelo Playnite numa transmissão e segure Select + Y
      por 5 s. O jogo fecha, o Playnite volta para a frente e o log mostra `asked '<jogo>' to close`, depois
      `'<jogo>' closed` e `brought 'Playnite...' to the front`.
- [ ] **Playnite em tela cheia com o foco** (o primeiro teste, em 2026-11-27, falhou aqui: o log dizia
      `'Playnite.FullscreenApp' is protected (it is the launcher)` com o jogo na tela): abra o jogo pelo Playnite e
      segure o combo. Agora o log deve dizer `'Playnite.FullscreenApp' (the launcher) is in front; closing the game it
      started, '<jogo>'` e o jogo fecha. Se aparecer `started 2 programs with a window ... (nomes)` ou
      `started no game with a window`, mande a linha: ela diz o que o app viu.
- [ ] **O Playnite não fecha junto:** logo depois de o jogo fechar, segure o combo de novo, de propósito, com o
      Playnite na frente. Nada pode fechar: o log mostra `combo ignored: pause after the last round` (primeiros 5 s)
      ou `'Playnite...' is protected (it is the launcher)`.
- [ ] **Combo com o Playnite na frente desde o começo** (sem jogo aberto): nada fecha e o log diz que o launcher é
      protegido.
- [ ] **Jogo sem menu "Sair" ou que ignora o pedido:** segure o combo; passados 5 s o log diz
      `did not close; hold the combo again within 15 s to end it`. Segure de novo dentro de 15 s: o jogo é
      encerrado (`ended '<jogo>'`) e o Playnite volta.
- [ ] **Segunda segurada depois dos 15 s:** espere mais de 15 s e segure: o log diz que a confirmação expirou e
      recomeça do zero (pede para fechar de novo, não encerra direto).
- [ ] **Jogo que pergunta "tem certeza?":** segure o combo, responda a pergunta com o controle. Se confirmar, o jogo
      fecha como no primeiro item.
- [ ] **Programa que já estava aberto antes da transmissão** (navegador, por exemplo) deixado na frente: o combo
      não fecha nada e o log diz `it was open before the stream began`.
- [ ] **Sem transmissão:** com a função ligada, segure o combo no controle do PC: nada acontece (o app só lê o
      controle durante a transmissão).
- [ ] **Função desligada:** desmarque a caixa e segure o combo: nada acontece e não há linha `fired` no log.
- [ ] **Encerrar sem pedir confirmação** (marque só para testar): num jogo que ignora o pedido, o jogo é encerrado
      sozinho depois de 5 s, sem segunda segurada. Desmarque depois.
- [ ] **Jogo que roda como administrador** (por exemplo aberto pelo Playnite elevado no Sunshine): o app não
      consegue fechá-lo e o log traz o aviso `... runs as administrator ... untick 'Run as administrator'`. No
      Sunshine, desmarque "Run as elevated" no app do Playnite e repita o teste do primeiro item.
- [ ] Esvazie a lista de programas do launcher e marque a caixa de novo: a tela mostra o aviso de que, sem o nome
      do launcher, o combo pode fechá-lo.

Anote o que acontecer de diferente (principalmente o Playnite não voltar para a frente, que o Windows pode
recusar: nesse caso o log diz `Windows did not let the app bring ... to the front`).

## PR D2a do ADR 0017 — modo `elevated-close` do auxiliar (chamado à mão)

Ainda sem tarefa agendada nem botão (vêm no D2b). Aqui o modo é chamado direto, de um terminal **de administrador**,
para provar que ele fecha um jogo que roda como administrador e que recusa o resto. O código de saída diz o que
aconteceu: `0` feito, `11` não achou (PID ou horário errados), `13` nome proibido, `15` não está elevado, `17` não veio
do Sunshine, `18` sem janela, `20` o Windows recusou, `30` erro inesperado.

Antes: atualize o código (`git pull`) e abra o **PowerShell como administrador** na pasta do projeto. Abra uma
transmissão e, nela, o SuperOdyssey pelo Playnite.

```powershell
$g = Get-Process SuperOdysseyRecomp
dotnet run --project src\SunshineHelper.Trigger -- elevated-close close $g.Id $g.StartTime.ToFileTime(); $LASTEXITCODE
```

- [ ] **Fechar o jogo elevado:** o comando acima mostra `0` e o jogo fecha (como clicar no X). Se o jogo não fechar
      mas o código for `0`, troque `close` por `kill` e repita: o jogo é encerrado.
- [ ] **Horário errado:** repita com `($g.StartTime.ToFileTime() + 1)` no lugar do horário: mostra `11` e nada fecha.
- [ ] **Programa comum:** troque o jogo pelo Playnite (`$g = Get-Process Playnite.FullscreenApp`): mostra `15` (o app
      fecha programas comuns sozinho) e nada fecha.
- [ ] **Nome proibido:** `$g = Get-Process explorer | Select-Object -First 1`: mostra `13` e nada fecha.
- [ ] **Fora do Sunshine:** abra o Bloco de Notas como administrador, fora da transmissão
      (`$g = Get-Process notepad`): mostra `17` e nada fecha.

## Automatizar isto (pendência, sem PR ainda)

Pedido do autor: transformar estes itens em testes automáticos de verdade, em
vez de checklist manual. Para isso precisa de um jeito de rodar código contra
um Windows real no CI — hoje o `windows-latest` do GitHub Actions roda os
testes de Domain/Application, mas um teste que troque a resolução da tela
precisaria de um runner com monitor (self-hosted, ou aceitar que só valida a
leitura/API sem mudar o modo de verdade). Decidir a abordagem (e se vale a
pena antes da Fase 2, que é quando o `fase1-plano.md` já previa isso) fica
para quando este item for puxado.

## Aviso de nível de log

- [ ] Com o Sunshine em **Debug** e o caminho do log salvo, o **Painel** só diz "Leitor do log
      ativo." (sem aviso).
- [ ] Mude o nível de log do Sunshine para **Info**, reinicie o Sunshine e o SunshineHelper: o
      Painel mostra o aviso de nível baixo. Volte para Debug depois.
