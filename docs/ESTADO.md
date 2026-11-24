# Estado do projeto

Uma página para retomar o trabalho sem reler tudo. O Claude atualiza este arquivo no fim de cada PR
(regra no `CLAUDE.md`); não há trava no CI. Última atualização: **2026-11-23**, `main` após o PR 62.

## Onde estamos

**Fase 1 (núcleo): feita.** Estão prontos (o que já foi testado no PC do autor está em `docs/testes-manuais.md`): snapshot atômico e recuperação,
orquestrador, módulos de resolução, HDR, FPS (RTSS) e launcher, canal do pipe com o auxiliar de gatilho,
leitor do log do Sunshine, bandeja e janela (Painel, Geral, Perfis), log de diagnóstico e relatório de
diagnóstico. As decisões D2 (falha de um módulo vira aviso) e D3 (sessão anterior presa é restaurada antes da
nova) estão no ADR 0011 e passaram no teste do PC. Situação do portão: `docs/fase1-portao.md`.

**Último PR mesclado:** #62 (botão "Criar perfil para este aparelho"). **Versão de testes atual:**
`v0.1.0-preview.3` (com a tela Dispositivos); a `v0.1.0-preview.4` (com o botão) está sendo publicada. A preview.3 foi publicada em 2026-11-23 pelo workflow `preview-release.yml` (zip com o app e o
auxiliar de gatilho; as anteriores, `preview.1` e `preview.2`, foram testadas no PC do autor e funcionaram; o Windows mostra
o aviso do SmartScreen porque não há assinatura de código).

## Próximos PRs

Ordem completa e modelos em `docs/fase1-plano.md`, "Próximos PRs".

| PR | O quê | Modelo |
| --- | --- | --- |
| a seguir | Tela Log (PR 63); depois o overlay pelo controle (Opus · alto, com ADR) | ver backlog |

## Decisões em aberto

- **D1, FPS sem administrador:** decidida no ADR 0012 (permissão só no arquivo `Profiles\Global`, concedida
  uma vez pelo UAC). Confirmada no PC (teste do 15b-1).

## Pendências de teste manual

Lista viva em `docs/testes-manuais.md`, "Ainda pendente":

- Botão "Criar perfil para este aparelho" (PR 62), na `v0.1.0-preview.4`: testar (passo a passo no arquivo). A tela Dispositivos já foi conferida no PC na preview.3.
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
- **Atualização automática do app** (pedido do autor em 2026-11-23, deixada para uma etapa mais consolidada). Plano em dois
  PRs: (1) só avisar que há versão nova, consultando as releases do GitHub, com opção de desligar a consulta; (2) baixar,
  conferir o SHA256 e trocar os dois `.exe`, que pede ADR, Opus · esforço alto e revisão de segurança. Sem assinatura de
  código e sem instalador, o SHA256 só garante que o arquivo não veio corrompido, não quem o publicou.
- **Longo prazo (só depois de todo o escopo pronto):** telemetria própria do app, no estilo do OSD do RivaTuner
  (monitoramento de desempenho na tela; a decidir, sempre local, nada enviado pela internet), e um **limitador de FPS
  próprio**, para o app não depender do RTSS. Para o limitador, a direção a investigar é o driver da placa de vídeo
  (cada fabricante tem uma interface própria, a do autor é AMD), que não injeta código no jogo como o RTSS faz.
  Pede ADR e Opus · esforço alto.
- Reler o nível de log do Sunshine quando o arquivo for recriado (hoje só depois de reiniciar o app).
- Modelo de pedido para abrir cada sessão (Etapa, Ler, Fora do escopo), a cargo do autor.

## Mapa dos documentos

- Decisões: `docs/adr/README.md` (índice).
- Plano e portão: `docs/fase1-plano.md`, `docs/fase1-portao.md`.
- Testes no PC: `docs/testes-manuais.md`. Diagnóstico: `docs/diagnostico.md`.
- Origem dos fatos do Sunshine: `docs/fase0-resultados.md`, `docs/fase0-analise-de-logs.md`.
