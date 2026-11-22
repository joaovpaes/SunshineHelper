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

- [ ] Com um jogo ou o próprio Windows em 2560×1440 @ 180 Hz (ou a resolução
      que você usa), rodar um teste manual que capture o modo atual, aplique
      3840×2160 e confira visualmente que a tela mudou de resolução sem mudar
      a taxa de atualização.
- [ ] Reverter e confirmar que a tela volta exatamente a 2560×1440 @ 180 Hz.
- [ ] Repetir pedindo uma resolução que o monitor não suporta (ex.: 7680×4320)
      e confirmar que `ApplyAsync` reporta falha, sem travar a tela num estado
      estranho.
- [ ] Conferir que a resolução aplicada **não** aparece como permanente em
      Configurações > Sistema > Vídeo depois de um logoff/logon (prova de que
      `dwFlags = 0` não gravou no registro).

## PR 5b — Módulo de HDR

- [ ] A definir junto com o PR.
