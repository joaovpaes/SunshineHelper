# Índice das decisões (ADR)

Formato e modelo: `0000-template.md`. Um ADR por arquivo; quando um deles for trocado por outro, o antigo recebe
"substituída por NNNN" no campo Status (e o novo diz o que substitui).

| ADR | Decisão | Status | Data |
| --- | --- | --- | --- |
| 0001 | Stack e gatilho de sessão | aceita | 2026-11-22 |
| 0002 | Arquitetura do núcleo (Fase 1) | aceita; elevação substituída por 0012 | 2026-11-22 |
| 0003 | Canal (named pipe) e auxiliar de gatilho | aceita | 2026-11-22 |
| 0004 | Leitor do log do Sunshine | aceita | 2026-11-23 |
| 0005 | Casca da interface (PR 9a) | aceita | 2026-11-23 |
| 0006 | Hospedagem do app, bandeja e Painel (PR 9b) | aceita | 2026-11-23 |
| 0007 | Tela Geral: idioma e caminho do log (PR 9c) | aceita | 2026-11-23 |
| 0008 | Módulo do launcher (PR 10) | aceita | 2026-11-23 |
| 0009 | Tela de perfis (PR 11) | aceita | 2026-11-23 |
| 0010 | HDR é aplicado antes da resolução | aceita | 2026-11-23 |
| 0011 | Falha de um módulo vira aviso e sessão anterior presa é restaurada (complementa o 0002) | aceita | 2026-11-23 |
| 0012 | Permissão de escrita só no perfil global do RTSS; o app não é elevado | aceita | 2026-11-23 |
| 0013 | Restaurar sozinho quando o Sunshine encerra no meio da sessão (PR 16) | aceita | 2026-11-23 |
| 0014 | Registro dos aparelhos que conectaram (`devices.json`) | proposta | 2026-11-23 |
| 0015 | Overlay pelo controle: leitura do controle (XInput), máquina de estados do combo e janela (Fase 2) | proposta | 2026-11-23 |
| 0016 | Fechar o jogo pelo controle sem atingir outro programa (Select + Y, proteções, confirmação, pausa) | proposta | 2026-11-27 |

Estado geral do projeto: `docs/ESTADO.md`.
