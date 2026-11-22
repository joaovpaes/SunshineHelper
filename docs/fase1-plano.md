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
| 5 | Módulo de vídeo e HDR | CsWin32, resolução preservando a taxa de atualização, HDR por API | Sonnet · médio |
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
