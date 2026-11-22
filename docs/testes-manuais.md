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

## Automatizar isto (pendência, sem PR ainda)

Pedido do autor: transformar estes itens em testes automáticos de verdade, em
vez de checklist manual. Para isso precisa de um jeito de rodar código contra
um Windows real no CI — hoje o `windows-latest` do GitHub Actions roda os
testes de Domain/Application, mas um teste que troque a resolução da tela
precisaria de um runner com monitor (self-hosted, ou aceitar que só valida a
leitura/API sem mudar o modo de verdade). Decidir a abordagem (e se vale a
pena antes da Fase 2, que é quando o `fase1-plano.md` já previa isso) fica
para quando este item for puxado.
