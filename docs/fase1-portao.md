# Portão da Fase 1 — situação

Critério (`docs/fase1-plano.md`): **o ambiente é restaurado mesmo com falha simulada em qualquer
módulo**. Cada item abaixo diz onde está a prova automática (CI) e o que só a máquina do autor
confirma.

| # | Exigência | Prova automática (CI) | Falta na máquina do autor |
| --- | --- | --- | --- |
| 1 | Falha injetada em cada módulo, um de cada vez: o módulo que falhou volta na hora, os outros seguem e tudo volta ao estado capturado no fim da sessão (ADR 0011) | `SessionOrchestratorApplyTests` (falha, exceção e falha pela metade em cada módulo; módulo que não volta; cancelamento); `PhaseOneGateTests.Falha_de_um_modulo_vira_aviso_e_tudo_volta_mesmo_depois_de_uma_queda` e `Falha_ao_restaurar_um_modulo_e_resolvida_na_proxima_abertura` | Transmitir com FPS em "Valor fixo" sem administrador: resolução e HDR ficam, o Painel avisa do FPS |
| 2 | App encerrado à força no meio da sessão: ao abrir de novo, restaura | `PhaseOneGateTests`: queda durante a aplicação de cada módulo, durante a transmissão e durante a própria restauração, sempre com o snapshot que o app de fato gravou | Encerrar o SunshineHelper pelo Gerenciador de Tarefas com uma transmissão ativa e abrir de novo |
| 3 | Reinício do PC no meio da sessão: o perfil do RTSS volta; vídeo e HDR não são tocados | `PhaseOneGateTests.Reinicio_no_meio_da_sessao_desfaz_so_o_que_sobrevive_ao_reinicio` | Reiniciar o PC com uma transmissão ativa e conferir o limite de FPS no RTSS |
| 4 | Tempo de aplicação abaixo de 10 s | — (precisa de Windows com monitor de verdade) | Medir com uma transmissão real |
| 5 | Cobertura de Domain e Application ≥ 80% | Coletada no CI; o limite automático entra no próximo PR | — |

Os itens da última coluna entram na lista de pendências de `docs/testes-manuais.md`.
