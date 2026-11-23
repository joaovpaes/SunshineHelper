# Testes manuais

O que o CI não consegue verificar sozinho: Infrastructure e UI, por mexerem em
Windows de verdade (vídeo, HDR, RTSS, processos, janelas), não têm teste
automático até a Fase 2 (`docs/fase1-plano.md`, "Riscos desta fase"). O CI
confirma que o código compila; só a máquina do autor confirma que ele
funciona.

Cada item abaixo corresponde a um PR. Marque quando tiver testado na própria
máquina; um PR com item aqui pendente ainda não está validado de ponta a
ponta, mesmo com o CI verde.

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

## Automatizar isto (pendência, sem PR ainda)

Pedido do autor: transformar estes itens em testes automáticos de verdade, em
vez de checklist manual. Para isso precisa de um jeito de rodar código contra
um Windows real no CI — hoje o `windows-latest` do GitHub Actions roda os
testes de Domain/Application, mas um teste que troque a resolução da tela
precisaria de um runner com monitor (self-hosted, ou aceitar que só valida a
leitura/API sem mudar o modo de verdade). Decidir a abordagem (e se vale a
pena antes da Fase 2, que é quando o `fase1-plano.md` já previa isso) fica
para quando este item for puxado.
