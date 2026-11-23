# Estado do projeto

Uma página para retomar o trabalho sem reler tudo. O Claude atualiza este arquivo no fim de cada PR
(regra no `CLAUDE.md`); não há trava no CI. Última atualização: **2026-11-23**, `main` após o PR 57.

## Onde estamos

**Fase 1 (núcleo): feita.** Estão prontos (o que já foi testado no PC do autor está em `docs/testes-manuais.md`): snapshot atômico e recuperação,
orquestrador, módulos de resolução, HDR, FPS (RTSS) e launcher, canal do pipe com o auxiliar de gatilho,
leitor do log do Sunshine, bandeja e janela (Painel, Geral, Perfis), log de diagnóstico e relatório de
diagnóstico. As decisões D2 (falha de um módulo vira aviso) e D3 (sessão anterior presa é restaurada antes da
nova) estão no ADR 0011 e passaram no teste do PC. Situação do portão: `docs/fase1-portao.md`.

**Último PR mesclado:** #57 (iniciar com o Windows, minimizado na bandeja). **Pré-lançamento `v0.1.0-preview.1` publicado** em 2026-11-23 (zip com o app e o
auxiliar de gatilho, gerado pelo workflow `preview-release.yml`; testado no PC do autor, tudo funcionou; o Windows mostra
o aviso do SmartScreen porque não há assinatura de código).

## Próximos PRs

Ordem completa e modelos em `docs/fase1-plano.md`, "Próximos PRs".

| PR | O quê | Modelo |
| --- | --- | --- |
| 59 | Tela Dispositivos: registro dos aparelhos que conectaram (`devices.json`) e lista | Sonnet · médio |
| 60 | Botão "Criar perfil para este aparelho" na tela Dispositivos | Sonnet · médio |
| a seguir | Tela Log; depois o overlay pelo controle (Opus · alto, com ADR) | ver backlog |

## Decisões em aberto

- **D1, FPS sem administrador:** decidida no ADR 0012 (permissão só no arquivo `Profiles\Global`, concedida
  uma vez pelo UAC). Confirmada no PC (teste do 15b-1).

## Pendências de teste manual

Lista viva em `docs/testes-manuais.md`, "Ainda pendente":

- Versão no pacote de pré-lançamento (PR 56): conferir que "Sobre" mostra `0.1.0-preview.2`.
- Calibração do atraso do launcher (0 / 3000 / 5000 ms, 3 rodadas cada).
- Opcional: relatório de diagnóstico com o caminho do log do Sunshine vazio.

## Backlog (sem PR marcado)

- Instalador (PR próprio, ainda não existe): tarefa agendada para o auxiliar iniciar o SunshineHelper quando ele não
  estiver aberto (ADR 0002 e 0003; hoje o auxiliar desiste em 2 s e a transmissão começa sem perfil, então o app
  precisa estar aberto antes), concessão da permissão do RTSS (ADR 0012) e, a decidir, abrir o RTSS sozinho se ele
  estiver fechado.
- Segunda cópia do app: hoje ela sai em silêncio; melhor seria trazer a janela da primeira para a frente.
- `LEIA-ME.md` do pacote também em inglês (hoje só em português; as notas da release já são nas duas línguas).
- Depois do instalador: release oficial com `release-please` e atestado de procedência (o `SHA256SUMS.txt` e o zip já saem
  do workflow do pré-lançamento); assinatura de código (SignPath) a cargo do autor.
- Ícone no instalador (o `.exe`, a janela e a bandeja já têm o ícone; o instalador ainda não existe). Fontes embutidas e
  opção manual de tema (claro/escuro) seguem no pacote de design.
- **Aviso do SmartScreen:** some com assinatura de código (a Fundação SignPath assina projetos de código aberto de
  graça; o pedido é do autor e leva dias ou semanas) e, depois, com a reputação do programa; em certificado pago
  (validação estendida) a reputação vem mais rápido. Até lá o `LEIA-ME.md` do pré-lançamento explica como seguir.
- Reler o nível de log do Sunshine quando o arquivo for recriado (hoje só depois de reiniciar o app).
- Modelo de pedido para abrir cada sessão (Etapa, Ler, Fora do escopo), a cargo do autor.

## Mapa dos documentos

- Decisões: `docs/adr/README.md` (índice).
- Plano e portão: `docs/fase1-plano.md`, `docs/fase1-portao.md`.
- Testes no PC: `docs/testes-manuais.md`. Diagnóstico: `docs/diagnostico.md`.
- Origem dos fatos do Sunshine: `docs/fase0-resultados.md`, `docs/fase0-analise-de-logs.md`.
