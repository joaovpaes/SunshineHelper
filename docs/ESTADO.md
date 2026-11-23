# Estado do projeto

Uma página para retomar o trabalho sem reler tudo. O Claude atualiza este arquivo no fim de cada PR
(regra no `CLAUDE.md`); não há trava no CI. Última atualização: **2026-11-23**, `main` após o PR 44.

## Onde estamos

**Fase 1 (núcleo): feita.** Estão prontos (o que já foi testado no PC do autor está em `docs/testes-manuais.md`): snapshot atômico e recuperação,
orquestrador, módulos de resolução, HDR, FPS (RTSS) e launcher, canal do pipe com o auxiliar de gatilho,
leitor do log do Sunshine, bandeja e janela (Painel, Geral, Perfis), log de diagnóstico e relatório de
diagnóstico. As decisões D2 (falha de um módulo vira aviso) e D3 (sessão anterior presa é restaurada antes da
nova) estão no ADR 0011 e passaram no teste do PC. Situação do portão: `docs/fase1-portao.md`.

**Último PR mesclado:** #44 (`ESTADO.md` e índice de ADRs).

## Próximos PRs

Ordem completa e modelos em `docs/fase1-plano.md`, "Próximos PRs".

| PR | O quê | Modelo |
| --- | --- | --- |
| 15a | Teste do RTSS só em memória (roda na máquina do autor) | Sonnet · médio |
| 15b | Permissão do RTSS e botão "Reparar" (só se o 15a mostrar que precisa), ADR 0012 | Opus · alto |
| 16 | O app percebe que o Sunshine morreu e restaura sozinho (ADR novo) | Opus · alto |

## Decisões em aberto

- **D1, FPS sem administrador:** hoje, sem administrador, o módulo de FPS falha e a sessão segue só com
  resolução e HDR, com aviso no Painel. O caminho (teste em memória, depois permissão no arquivo do RTSS) está
  em `docs/fase1-plano.md`, "Decisões D1–D3".

## Pendências de teste manual

Lista viva em `docs/testes-manuais.md`, "Ainda pendente":

- Calibração do atraso do launcher (0 / 3000 / 5000 ms, 3 rodadas cada).
- Opcional: relatório de diagnóstico com o caminho do log do Sunshine vazio.

## Backlog (sem PR marcado)

- Depois do instalador: release com `release-please`, `SHA256SUMS.txt`, atestado de procedência; assinatura de
  código (SignPath) a cargo do autor.
- Fontes embutidas, ícone do produto e opção manual de tema.
- Reler o nível de log do Sunshine quando o arquivo for recriado (hoje só depois de reiniciar o app).
- Modelo de pedido para abrir cada sessão (Etapa, Ler, Fora do escopo), a cargo do autor.

## Mapa dos documentos

- Decisões: `docs/adr/README.md` (índice).
- Plano e portão: `docs/fase1-plano.md`, `docs/fase1-portao.md`.
- Testes no PC: `docs/testes-manuais.md`. Diagnóstico: `docs/diagnostico.md`.
- Origem dos fatos do Sunshine: `docs/fase0-resultados.md`, `docs/fase0-analise-de-logs.md`.
