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

## PR 5b — Módulo de HDR

- [ ] A definir junto com o PR.

## Automatizar isto (pendência, sem PR ainda)

Pedido do autor: transformar estes itens em testes automáticos de verdade, em
vez de checklist manual. Para isso precisa de um jeito de rodar código contra
um Windows real no CI — hoje o `windows-latest` do GitHub Actions roda os
testes de Domain/Application, mas um teste que troque a resolução da tela
precisaria de um runner com monitor (self-hosted, ou aceitar que só valida a
leitura/API sem mudar o modo de verdade). Decidir a abordagem (e se vale a
pena antes da Fase 2, que é quando o `fase1-plano.md` já previa isso) fica
para quando este item for puxado.
