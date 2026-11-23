# CLAUDE.md — SunshineHelper

Instruções para o Claude Code neste repositório. Resumo das regras completas guardadas no Projeto "Automação Sunshine" (`regras-git-github.md` e `padrao-modelo-esforco.md`).

## O projeto

App WPF (C# 14, .NET 10 LTS) para Windows que detecta o início de uma transmissão Sunshine, identifica o cliente, aplica perfil (resolução, HDR, limite de FPS via RTSS; a taxa de atualização não muda) e restaura o estado anterior. Overlay por controle. Não modifica Sunshine nem Moonlight. Deve servir à comunidade: nada do PC do autor fica fixo no código.

Arquitetura em camadas: Domain ← Application ← Infrastructure/UI. Módulos com Capture/Apply/Revert, snapshot atômico, orquestrador, `settings.json` com `schemaVersion`. Decisões em `docs/adr/`.

## Git (o usuário não sabe git: o Claude faz tudo e explica em português simples)

- Nunca push em `main` nem force-push. Branch `tipo/descricao-curta`, PR pequeno, squash merge.
- Commits em inglês (Conventional Commits); título do PR em inglês, corpo em português (O que mudou / Por quê / Como testar / Riscos / O que o usuário precisa fazer).
- Só mesclar com CI verde **e** um "pode mesclar" explícito do usuário. Tags e releases só quando ele pedir.
- O ambiente do Claude não tem .NET nem Windows: o CI é o compilador. Não afirmar que compila ou passa sem o CI confirmar.
- No fim de cada PR mesclado, atualizar `docs/ESTADO.md` (último PR, próximos, pendências de teste), junto com o PR seguinte ou em um PR de docs.
- **Notas de release:** toda versão publicada, mesmo de testes, tem `docs/releases/<tag>.md`, bem explicada (o que mudou, como atualizar, limites, como conferir) e **em inglês e português (inglês primeiro)**. O workflow recusa criar a release sem esse arquivo. Modelo em `docs/releases/_modelo.md`.
- Segredos nunca entram. Logs do Sunshine têm `rikey`, `uniqueid`, IPs e MACs: redigir antes de virarem fixture.
- Ações do GitHub fixadas por SHA; `permissions:` mínimo por job.

## AVISO DE TROCA DE MODELO/ESFORÇO (obrigatório)

O Claude não troca o próprio modelo nem o esforço; quem troca é o usuário. Em **todo início de tarefa e em toda mudança de etapa**, a **primeira coisa da resposta** é um destes blocos (citação, negrito):

> **🔀 TROCAR AGORA → Opus · esforço alto**
> Etapa: ... Como: seletor de modelo do app ou `/model opus`, e depois o esforço "alto".
> Responda **"troquei"** e eu começo. Até lá não faço nada além deste aviso.

> **✅ MANTER → Sonnet · esforço médio** (a sessão já está no nível certo)

> **⬇️ PODE VOLTAR → Sonnet · esforço médio** (a parte difícil terminou; diga qual etapa vem agora)

- Nível necessário maior que o atual, ou nível atual desconhecido: **TROCAR AGORA** e esperar o "troquei". Não adivinhar.
- Se o usuário recusar a troca, seguir no nível atual, avisar o risco em uma frase e registrar no PR.
- Falhou 2 vezes no mesmo problema: **TROCAR AGORA** para o nível acima.
- Citar a linha da tabela de `padrao-modelo-esforco.md` que justifica o nível.

## Modelo e esforço por etapa (padrão do projeto)

- Começar pelo mais baixo que a tarefa permite; subir um nível depois de 2 falhas; voltar ao padrão ao terminar a parte difícil.
- **Opus, esforço alto:** análise da Fase 0, arquitetura do núcleo, snapshot/restauração, orquestrador, máquina de estados do combo do controle, proteção/encerramento de processos, revisão de PR de segurança.
- **Sonnet, esforço médio:** módulos, interface WPF, testes, CI, instalador, documentação técnica. Usar `opusplan` quando houver plano e execução.
- **Sonnet/Haiku, esforço baixo:** textos, commits, ajustes triviais.
- Uma sessão por PR; `/clear` ao trocar de assunto; plano antes de mudanças em mais de 2 ou 3 arquivos.

## Diagnóstico prévio (para economizar tokens)

Quando um problema ficar complexo (falhou 2 vezes, ou o `app.log` não explica a causa), **parar e recomendar** ao usuário gerar o relatório pelo botão "Gerar relatório de diagnóstico" (tela Geral) e passá-lo à ferramenta externa dele, que lê só o código-fonte baixado. Como funciona e o modelo de pedido: `docs/diagnostico.md`. Só tentar uma terceira correção depois da resposta dessa ferramenta. O relatório é redigido (IP, MAC, chaves do Sunshine, usuário do Windows), mas o Claude lembra o usuário de ler o arquivo antes de compartilhar.

## Armadilhas conhecidas do compilador e do CI

O ambiente do Claude não compila, então cada erro aqui custa um ciclo de CI. Antes de dar push, reler o código novo contra esta lista. Avisos são erros (`TreatWarningsAsErrors`).

- **Ordem dos `using`** (`dotnet format`): `System.*` primeiro, depois `SunshineHelper.*`. Nos testes: `Shouldly`, `SunshineHelper.*`, `Xunit`.
- **CA1305:** texto com número ou data usa `string.Create(CultureInfo.InvariantCulture, $"...")`. Sem interpolação aninhada: calcular antes em variáveis locais (`clientText`, `elapsedText`).
- **CA1861:** array constante em argumento (`new[] { "a", "b" }`) vira campo `static readonly`.
- **CA1826 e CA1859:** usar o indexador ou propriedade em vez de LINQ em lista/array, e o tipo concreto em campo ou variável privada quando der.
- **IDE1006:** campo privado `static readonly` também leva prefixo `_` (`_fakeSecret1`).
- **xUnit1051:** nos testes, passar `TestContext.Current.CancellationToken` a quem aceita token.
- **`T?` em tipo valor:** `int?` e `T?` com struct são `Nullable<T>`, não anotação. Conferir `.Value` e `is { } x`. Evitar `new(...)` de tipo-alvo quando o tipo for ambíguo.
- **WPF e WinForms:** nomes ambíguos (`ComboBox`, `MessageBox`) pedem o nome completo (`System.Windows.Controls.ComboBox`). Os usings implícitos do projeto WPF não incluem `System.IO`.
- **Cobertura:** o CI exige 80% em Domain e Application (`scripts/check-coverage.ps1`). Código novo nessas camadas sem teste derruba o CI.
- **gitleaks lê todos os commits do PR**, não só o último. Texto falso de teste que pareça segredo (`rikey=ABC...`) precisa ser plano e de pouca variedade (`qqqqqqqqqqqqqqqq`). Se um valor ruim já entrou num commit, recriar o branch a partir de um commit limpo e abrir outro PR. Nunca force-push nem arquivo de ignorar.
- **Actions e permissões:** ação nova fixada por SHA e `permissions:` mínimo por job.
- **CI só roda em PR com base `main`:** não empilhar PRs. Depois de outro merge, atualizar o branch (`update-branch`) antes de mesclar.

## Estilo

Português para falar com o usuário (frases curtas; dizer "mesclar" e "versão"). Código, commits e identificadores em inglês. UI com arquivos de idioma (pt-BR e en).
