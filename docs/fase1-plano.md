# Fase 1: plano de execução

Decisões em `docs/adr/0002-arquitetura-do-nucleo.md`. Critério do portão: **o ambiente é restaurado mesmo com falha simulada em qualquer módulo**.

## Estrutura da solução

```
src/
  SunshineHelper.Domain/          perfis, snapshot, contratos  (sem dependências)
  SunshineHelper.Application/     orquestrador, escolha de perfil, política de sessão
  SunshineHelper.Infrastructure/  Win32 (CsWin32), RTSS, leitor de log, canal
  SunshineHelper.App/             WPF, bandeja, DI, hospedagem
  SunshineHelper.Trigger/         console NativeAOT, chamado pelo Sunshine
tests/
  SunshineHelper.Domain.Tests/
  SunshineHelper.Application.Tests/
  SunshineHelper.Infrastructure.Tests/
```

Domain e Application não referenciam nada de Windows: rodam e são testados no CI sem depender da plataforma gráfica.

## Sequência de PRs

Cada linha é um PR próprio, pequeno, com testes.

| # | PR | Entrega | Modelo sugerido |
| --- | --- | --- | --- |
| 1 | CI e portões de qualidade | build e testes em `windows-latest`, formato, pacotes vulneráveis, CodeQL, gitleaks, cobertura; ações fixadas por SHA | Sonnet · médio |
| 2 | Esqueleto da solução e Domain | projetos, `Directory.Packages.props` preenchido, modelo de perfil e de snapshot, testes | Sonnet · médio |
| 3 | Configuração e perfis | `settings.json` com `schemaVersion`, leitura, validação, migração, backup diário | Sonnet · médio |
| 4a | Snapshot da sessão e recuperação | modelo do snapshot com versão por módulo, gravação atômica, regra de recuperação (queda do app x reinício do PC) | **Opus · alto** |
| 4b | Orquestrador | aplicar, reverter, rollback em falha, recusa de sessão dupla, uma só via de volta; testes com módulos falsos e falhas injetadas | **Opus · alto** |
| 5a | Módulo de vídeo: resolução | CsWin32 (`ChangeDisplaySettingsEx`/`EnumDisplaySettingsEx`), preservando a taxa de atualização, confirmação só pela API, nunca por processo externo | Sonnet · médio |
| 5b | Módulo de HDR | CsWin32 (API de Advanced Color/CCD); comparar com o `HDRSwitch.exe` na máquina do autor antes de descartar a ferramenta externa | Sonnet · médio |
| 6 | Módulo de FPS (RTSS) | cópia de perfil, reinício, espera por condição, detecção de versão | Sonnet · médio |
| 7 | Auxiliar de gatilho e canal | `SunshineHelper.Trigger` em NativeAOT, named pipe, barreira, saída sempre 0 | **Opus · alto** |
| 8 | Leitor do log do Sunshine | retomada, desconexão, fallback; testes com as fixtures da Fase 0 | Sonnet · médio |
| 9 | Casca da interface | bandeja, janela principal, tokens do design system "Sinal", idiomas pt-BR e en | Sonnet · médio |
| 10 | Módulo de launcher e fim a fim | política de fechamento, calibração do atraso, teste na máquina do autor | **Opus · alto** |

O parâmetro `--restore` sai desta lista e entra depois do PR 6: o motor de recuperação é do PR 4b, mas só há o que restaurar de verdade quando existir um módulo que mexa no ambiente.

## O que o portão da Fase 1 exige

1. Falha injetada em cada módulo, um de cada vez: o ambiente volta ao estado capturado.
2. App encerrado à força no meio de uma sessão: ao abrir de novo, restaura.
3. Reinício do PC no meio de uma sessão: o perfil do RTSS volta ao normal; vídeo e HDR não são tocados.
4. Tempo de aplicação medido e abaixo de 10 s na máquina do autor.
5. Cobertura de Domain e Application em pelo menos 80%.

## Riscos desta fase

| Risco | Mitigação |
| --- | --- |
| HDR por API nativa se comportar diferente do `HDRSwitch.exe` | PR 5 compara os dois na máquina do autor antes de descartar a ferramenta externa |
| O atraso antes do launcher ser mesmo necessário | `launcherDelayMs` configurável; PR 10 calibra |
| RTSS mudar de formato de perfil entre versões | detectar versão, ler o perfil antes de copiar, recusar com mensagem clara se o formato for desconhecido |
| Testes de interface e de Win32 não rodarem no CI | Domain e Application cobrem a lógica; Infrastructure e UI têm testes marcados para execução manual até a Fase 2 |
| A verificação de vídeo/HDR não pode acontecer no runner do CI (sem monitor de verdade) | Cada PR de Infrastructure que mexe no vídeo lista em `docs/testes-manuais.md` o que o autor precisa verificar na própria máquina antes de considerar aquele módulo pronto |

## PRs que entraram além da sequência original

| PR | Entrega | Estado |
| --- | --- | --- |
| 10a/10b | Módulo do launcher (lógica, controlador de processos do Windows, tela Geral) | mesclado; **calibração do atraso pendente na máquina do autor** |
| — | Aviso de nível de log do Sunshine no Painel | mesclado; teste manual pendente |
| 11a/11b | Tela de perfis (lógica do formulário e tela) | mesclado; teste manual pendente |
| 12a | Cenários do portão: queda do app e reinício do PC com o snapshot real | mesclado |
| 12b | Cobertura mínima de 80% (Domain e Application) exigida no CI | em revisão |

O que o autor ainda precisa testar na própria máquina está no topo de `docs/testes-manuais.md`; a situação de cada item do portão está em `docs/fase1-portao.md`.

## Melhorias de processo aceitas

Saíram da avaliação de uma revisão externa (2026-11-23), triadas item a item. Nenhuma está implementada ainda; a ordem é a recomendada. **Cada uma é um PR (ou uma configuração) à parte.**

| Ordem | Item | Quem faz | Tamanho |
| --- | --- | --- | --- |
| 1 | Ligar o reporte privado de vulnerabilidade e o Dependabot de segurança no GitHub; criar `SECURITY.md` curto (pipe local, fechamento de processos, logs do Sunshine com `rikey` e IPs). Secret scanning e push protection **já estão ligados** | autor (cliques) + Claude (PR pequeno) | P |
| 2 | Ligar "excluir branches ao mesclar" e apagar as branches antigas (32). O squash já é obrigatório pelo ruleset "Protect Main" | autor (Claude não apaga branch remota) | P |
| 3 | Lista de **armadilhas conhecidas do compilador** no `CLAUDE.md`, para cortar ciclos de CI: CA1861, CA1826, CA1859, ordem dos `using` (Shouldly, SunshineHelper, Xunit), nomes ambíguos com WinForms (`ComboBox`, `MessageBox`), `T?` em tipo valor. Substitui "rodar o build antes do push", que o ambiente do Claude não permite (sem .NET, downloads bloqueados) | Claude | P |
| 4 | `docs/ESTADO.md` de uma página (fase atual, último PR, próximo PR, decisões em aberto, **Backlog**, apontando para as pendências de teste manual). Atualizado por mim no fim de cada PR; sem trava no CI | Claude | P |
| 5 | `docs/adr/README.md` (índice manual) e marcação de "substituído por" nos ADRs; seção "Alternativas consideradas" no template. Depende da decisão D1 | Claude | P |
| 6 | Checklist no template de PR: ADR criado/atualizado, entrada em `testes-manuais.md`, sem segredo ou dado pessoal em fixture, pendências de teste anotadas | Claude | P |
| 7 | Modelo de pedido para abrir cada sessão (Etapa, Ler, Fora do escopo). "Plano primeiro" continua só para mudanças em mais de 3 arquivos | autor | P |
| Depois do instalador | Release: `release-please`, `SHA256SUMS.txt`, atestado de procedência; assinatura de código (SignPath) a cargo do autor | Claude + autor | G |

**Não adotadas** (e por quê): mover `fase0-*.md` e seções de `testes-manuais.md` para um arquivo morto (citados por caminho em comentários do código e em ADRs); trocar procedimentos do `CLAUDE.md` por skills (o aviso de troca de modelo precisa estar sempre carregado); uma issue por item do plano (duplica este arquivo; issues só para bug achado ou revisão externa aceita).

## Decisões em aberto (precisam de ADR; análise em Opus alto)

- **D1.** O app roda como administrador? O ADR 0002 diz elevado, o 0006 adia e o 0008 diz sem admin; sem admin, o limite de FPS do RTSS (em Program Files) precisa de outro caminho.
- **D2.** A falha de um módulo desfaz a sessão inteira? Avaliar uma checagem prévia (`CanApply`) e módulos "não essenciais", cuja falha vira só aviso.
- **D3.** Como sair de uma sessão presa? Hoje, com snapshot de um cliente que desconectou e não voltou, as próximas transmissões são recusadas até o app reiniciar.
