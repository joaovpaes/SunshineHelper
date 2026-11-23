# Estado do projeto

Uma página para retomar o trabalho sem reler tudo. O Claude atualiza este arquivo no fim de cada PR
(regra no `CLAUDE.md`); não há trava no CI. Última atualização: **2026-11-23**, `main` após o PR 51.

## Onde estamos

**Fase 1 (núcleo): feita.** Estão prontos (o que já foi testado no PC do autor está em `docs/testes-manuais.md`): snapshot atômico e recuperação,
orquestrador, módulos de resolução, HDR, FPS (RTSS) e launcher, canal do pipe com o auxiliar de gatilho,
leitor do log do Sunshine, bandeja e janela (Painel, Geral, Perfis), log de diagnóstico e relatório de
diagnóstico. As decisões D2 (falha de um módulo vira aviso) e D3 (sessão anterior presa é restaurada antes da
nova) estão no ADR 0011 e passaram no teste do PC. Situação do portão: `docs/fase1-portao.md`.

**Último PR mesclado:** #51 (o app restaura sozinho quando o Sunshine encerra, ADR 0013; testado no PC).

## Próximos PRs

Ordem completa e modelos em `docs/fase1-plano.md`, "Próximos PRs".

| PR | O quê | Modelo |
| --- | --- | --- |
| 53 | Workflow manual de pré-lançamento (`v0.1.0-preview.1`): zip com o app, o auxiliar de gatilho e SHA256 | Sonnet · médio |

## Decisões em aberto

- **D1, FPS sem administrador:** decidida no ADR 0012 (permissão só no arquivo `Profiles\Global`, concedida
  uma vez pelo UAC). Confirmada no PC (teste do 15b-1).

## Pendências de teste manual

Lista viva em `docs/testes-manuais.md`, "Ainda pendente":

- Calibração do atraso do launcher (0 / 3000 / 5000 ms, 3 rodadas cada).
- Opcional: relatório de diagnóstico com o caminho do log do Sunshine vazio.

## Backlog (sem PR marcado)

- Instalador (PR próprio, ainda não existe): tarefa agendada para o auxiliar iniciar o SunshineHelper quando ele não
  estiver aberto (ADR 0002 e 0003; hoje o auxiliar desiste em 2 s e a transmissão começa sem perfil, então o app
  precisa estar aberto antes), concessão da permissão do RTSS (ADR 0012) e, a decidir, abrir o RTSS sozinho se ele
  estiver fechado.
- Opção na tela Geral para **iniciar com o Windows e abrir já minimizado na bandeja** (pedido do autor em 2026-11-23).
  Hoje, na versão de testes, isso é feito à mão com um atalho em `shell:startup`. Combina com a tarefa agendada do
  instalador (acima): as duas resolvem o mesmo problema, o app estar aberto antes da transmissão.
- Mostrar a **versão do app** na janela (por exemplo na tela Geral, "Sobre"), para o usuário saber qual versão está
  usando (pedido do autor em 2026-11-23). A versão já existe no código (`AppHost.AppVersion`, usada no relatório de
  diagnóstico e no `app.log`); falta só exibi-la. O pré-lançamento já grava a versão da tag no `.exe`.
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
