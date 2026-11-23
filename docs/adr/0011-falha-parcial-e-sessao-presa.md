# 0011. Falha de um módulo vira aviso (e sessão anterior presa)

- Status: aceita (parte D2); a parte D3 entra no PR 13b
- Data: 2026-11-23
- Complementa: 0002 (passo 4 de "Aplicar")

## Contexto

O ADR 0002 diz que, se um módulo falha ao aplicar, o orquestrador desfaz todos os já aplicados e
encerra a sessão como falha. Na prática isso deixava o perfil inteiro sem efeito por causa de um
detalhe:

- o app roda **sem** administrador, e o módulo do RTSS só grava o perfil (em Program Files) com
  administrador;
- então todo perfil com FPS em "Seguir o dispositivo" ou "Valor fixo" falhava sempre;
- resolução e HDR eram aplicados e logo desfeitos (a tela piscava duas vezes) e a transmissão
  começava sem perfil nenhum.

Perder a resolução e o HDR porque o limite de FPS não pôde ser gravado é pior para o usuário do que
transmitir sem o limite de FPS e ser avisado.

## Decisão (D2)

Passo 4 de "Aplicar" do ADR 0002 passa a ser:

```
  4. para cada módulo, na ordem:
        marca o módulo como "aplicando" e regrava o snapshot
        Apply
        marca o resultado (aplicado / sem mudança / falhou) e regrava o snapshot
        se falhar:
            se a aplicação foi cancelada (o app está fechando) → desfaz tudo, como antes
            Revert só deste módulo
            se o Revert falhar → desfaz tudo; se ainda falhar, o snapshot fica (como antes)
            senão → marca o módulo como "desfeito" (RolledBack), regrava, guarda o aviso
                    e segue para o próximo módulo
  5. marca a sessão como "aplicada" (com os avisos, se houver)
```

- Um módulo `RolledBack` não é revertido de novo no fim da sessão nem na recuperação após queda:
  ele já voltou, e um segundo Revert de um módulo que não funciona aqui (RTSS sem permissão)
  falharia e manteria o snapshot à toa.
- Continuam "tudo ou nada": falha na captura e falha ao gravar o snapshot. Sem estado lido ou sem
  snapshot gravado não há como garantir a volta.
- O canal responde `ok` e `applied` com o aviso no campo `error`; o auxiliar continua saindo com
  código 0. O Painel mostra "Não foi aplicado nesta transmissão: Limite de FPS" lendo o snapshot,
  e o motivo fica no `app.log`.
- Se todos os módulos falharem e voltarem, a sessão segue sem mudança nenhuma e termina limpa.
- Para isso funcionar sem administrador, o Revert do RTSS primeiro **lê** o limite atual (ler não
  precisa de permissão) e, se já é o valor capturado, termina com sucesso sem gravar. Sem isso, o
  "desfazer só este módulo" também falharia por falta de permissão e a sessão inteira seria
  desfeita, como antes.
- `CanApply` (consultar antes de aplicar) não é necessário agora: o RTSS já confere a permissão
  antes de mexer, então falha sem meio-termo.

## Consequências

- O item 1 do portão da Fase 1 muda de "o ambiente volta na hora" para "o módulo que falhou volta
  na hora; os outros voltam no fim da sessão, inclusive depois de uma queda do app". Os testes
  foram ajustados (`SessionOrchestratorApplyTests`, `PhaseOneGateTests`).
- `ModuleApplyState` ganha o valor `RolledBack` (gravado como `"rolledBack"`). A versão do envelope
  do snapshot não muda: um snapshot de uma versão anterior continua legível. Uma versão anterior do
  app **não** lê um snapshot com `rolledBack` (recusa com mensagem e mantém o arquivo); isso só
  acontece ao voltar para uma versão antiga no meio de uma transmissão.
- O usuário passa a transmitir com parte do perfil. O aviso no Painel e no `app.log` é o que o
  impede de achar que o limite de FPS está ativo.

## D3 — sessão anterior presa

A decidir no PR 13b (restaurar um snapshot que sobrou antes de aplicar uma sessão nova).
