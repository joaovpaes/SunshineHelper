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
- Segredos nunca entram. Logs do Sunshine têm `rikey`, `uniqueid`, IPs e MACs: redigir antes de virarem fixture.
- Ações do GitHub fixadas por SHA; `permissions:` mínimo por job.

## Modelo e esforço por etapa (padrão do projeto)

- Começar pelo mais baixo que a tarefa permite; subir um nível depois de 2 falhas; voltar ao padrão ao terminar a parte difícil.
- **Opus, esforço alto:** análise da Fase 0, arquitetura do núcleo, snapshot/restauração, orquestrador, máquina de estados do combo do controle, proteção/encerramento de processos, revisão de PR de segurança.
- **Sonnet, esforço médio:** módulos, interface WPF, testes, CI, instalador, documentação técnica. Usar `opusplan` quando houver plano e execução.
- **Sonnet/Haiku, esforço baixo:** textos, commits, ajustes triviais.
- Uma sessão por PR; `/clear` ao trocar de assunto; plano antes de mudanças em mais de 2 ou 3 arquivos.

## Estilo

Português para falar com o usuário (frases curtas; dizer "mesclar" e "versão"). Código, commits e identificadores em inglês. UI com arquivos de idioma (pt-BR e en).
