# Como contribuir / Contributing

## Fluxo

1. `main` é protegido: nada entra direto. Todo trabalho acontece em um branch `tipo/descricao-curta` (`feat/`, `fix/`, `docs/`, `test/`, `chore/`, `ci/`, `refactor/`) e vira um Pull Request pequeno (cerca de 400 linhas alteradas, no máximo).
2. Commits seguem [Conventional Commits](https://www.conventionalcommits.org/), em inglês: `feat(display): restore resolution after session end`.
3. Título do PR em inglês; corpo em português (modelo em `.github/PULL_REQUEST_TEMPLATE.md`). Issues podem ser em português ou inglês.
4. O PR só é mesclado (squash) com o CI verde e a aprovação do mantenedor.

## Regras de qualidade

- C# 14 / .NET 10, avisos tratados como erro, formato conforme `.editorconfig` (`dotnet format --verify-no-changes`).
- Cada mudança traz testes. Parser de log: sempre com fixtures em `tests/fixtures/`.
- Nada do ambiente de uma pessoa fica fixo no código (caminhos, GPU, monitor, nomes de aparelho, idioma do Windows).

## Segurança e privacidade

- **Logs do Sunshine contêm segredos** (`rikey`, `uniqueid`, IPs e MACs). Antes de virar fixture, troque esses valores por marcadores (`<REDACTED>`, `192.0.2.x`).
- Nunca commite tokens, chaves, certificados ou binários de build.
- Vulnerabilidades: não abra issue pública; use a aba Security do repositório (aviso privado).

## Ambiente

Windows com .NET 10 SDK (`global.json` fixa a versão mínima). O CI compila e testa em `windows-latest`.
