# Estado do projeto

Uma página para retomar o trabalho sem reler tudo. O Claude atualiza este arquivo no fim de cada PR
(regra no `CLAUDE.md`); não há trava no CI. Última atualização: **2026-11-28**, `main` após o PR 81.

## Onde estamos

**Fase 1 (núcleo): feita.** Estão prontos (o que já foi testado no PC do autor está em `docs/testes-manuais.md`): snapshot atômico e recuperação,
orquestrador, módulos de resolução, HDR, FPS (RTSS) e launcher, canal do pipe com o auxiliar de gatilho,
leitor do log do Sunshine, bandeja e janela (Painel, Geral, Perfis), log de diagnóstico e relatório de
diagnóstico. As decisões D2 (falha de um módulo vira aviso) e D3 (sessão anterior presa é restaurada antes da
nova) estão no ADR 0011 e passaram no teste do PC. Situação do portão: `docs/fase1-portao.md`.

**Último PR mesclado:** #81 (notas da `preview.6`). Antes, #80 (README e guia atualizados com o combo, revisão do analisador) e #79 (resultados do teste do D2c). Antes, #78 (ADR 0017, D2c: o combo fecha jogos de administrador pela tarefa agendada; testado no PC com o Mario Recomp elevado, com o Playnite com e sem administrador). Antes, #77 (ADR 0017, D2b: permitir e remover pela tela Geral, com cópia em Program Files e tarefa agendada; testado no PC: a tarefa roda sem aviso do Windows). Antes, #76 (ADR 0017, D2a: modo `elevated-close` no auxiliar; testado à mão no PC com o MKW Recomp elevado: fechou), #75 (ADR 0017) e #74 (fechar o jogo pelo controle no Windows, desligado de fábrica; testado no PC com um jogo comum: fecha e o Playnite volta). Antes, #73 regras do "fechar o jogo", #72 regra A/B do overlay, #71 leitor XInput, #70 detector de combo e seção `controller`, #69 ADRs 0015 e 0016.
**Versão de testes atual:** `v0.1.0-preview.6` (combo de fechar o jogo e jogos de administrador), publicada em 2026-11-28 pelo workflow `preview-release.yml` (zip com o app e o
auxiliar de gatilho). A `preview.6` ainda não foi instalada e testada como pacote no PC do autor; as versões `preview.1` a `preview.5` foram testadas e funcionaram; o Windows mostra
o aviso do SmartScreen porque não há assinatura de código.

## Próximos PRs

Ordem completa e modelos em `docs/fase1-plano.md`, "Próximos PRs".

| PR | O quê | Modelo |
| --- | --- | --- |
| a seguir | Testar a `preview.6` instalada (zip) no PC e os itens que faltam do roteiro D2c | (autor) |
| a seguir | PR E do ADR 0015: tela "Controle" para gravar os combos | Sonnet · médio |
| depois | PR F a H do ADR 0015 (janela do overlay seguindo o protótipo aprovado, perfis e ajustes com ADR próprio, atalhos) | ver ADR 0015 |

## Decisões em aberto

- **D1, FPS sem administrador:** decidida no ADR 0012 (permissão só no arquivo `Profiles\Global`, concedida
  uma vez pelo UAC). Confirmada no PC (teste do 15b-1).

## Pendências de teste manual

Lista viva em `docs/testes-manuais.md`, "Ainda pendente":

- Calibração do atraso do launcher (0 / 3000 / 5000 ms, 3 rodadas cada).
- Fechar o jogo (PR D): o caminho principal passou (jogo comum fecha e o Playnite volta). Faltam os outros itens do roteiro
  "PR D do ADR 0016" (segunda segurada encerra, pausa, jogo sem menu).
- ADR 0017: do roteiro "PR D2c do ADR 0017" faltam a segunda segurada (encerrar), sem a permissão, jogo aberto fora do
  Sunshine e jogo comum. Fechar pedindo já passou, com o Playnite com e sem administrador.
- Controle: numa transmissão (2026-11-28 05:04) o app não viu o controle até o celular reconectar, e o combo não
  disparou. Observar se repete (o controle virtual é criado pelo Sunshine).
- No teste do PR D o log disse que o Windows não deixou trazer o Playnite para a frente (ele já estava na frente). Rever a
  mensagem para não parecer erro.
- Opcional: relatório de diagnóstico com o caminho do log do Sunshine vazio.

## Backlog (sem PR marcado)

- Instalador (PR próprio, ainda não existe): tarefa agendada para o auxiliar iniciar o SunshineHelper quando ele não
  estiver aberto (ADR 0002 e 0003; hoje o auxiliar desiste em 2 s e a transmissão começa sem perfil, então o app
  precisa estar aberto antes), concessão da permissão do RTSS (ADR 0012) e, a decidir, abrir o RTSS sozinho se ele
  estiver fechado.
- Segunda cópia do app: hoje ela sai em silêncio; melhor seria trazer a janela da primeira para a frente.
- Tela Log: quebrar as linhas longas (ou opção) e deixar Copiar e Abrir a pasta sempre visíveis, sem rolar a página.
- Overlay: mostrar na tela que o combo de fechar foi reconhecido (pedido do autor no teste do PR D; vem com a janela do
  overlay, PR F).
- Overlay: ler controles no estilo PlayStation (DualShock 4 virtual do Sunshine), que o XInput não vê (ADR 0015).
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
