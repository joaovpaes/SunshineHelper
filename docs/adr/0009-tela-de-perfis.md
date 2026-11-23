# 0009. Tela de perfis (PR 11)

- Status: aceita (11a: lógica do formulário; 11b: tela, feita)
- Data: 2026-11-23

## Contexto

Até aqui os perfis só existiam escritos à mão no `settings.json`, o que impede o teste de ponta
a ponta com o Moonlight e não serve à comunidade. A tela Perfis deixa o usuário criar, editar
e excluir perfis sem abrir o arquivo.

## Decisões

- **A lógica do formulário fica no Domain** (`ProfileForm`): converte o texto digitado em
  `ClientProfile` e de volta, e devolve erros como valores (`ProfileFormError`), que a tela
  traduz para o idioma do usuário. Assim as regras são testadas no CI sem WPF.
- **Todo perfil que sai do formulário passa no `SettingsValidator`.** Resolução fixa:
  `LARGURAxALTURA` de 1 a 16384; limite de FPS fixo: 1 a 1000; atraso do launcher do perfil:
  vazio (usa o global) ou 0 a 30000.
- **O id nasce do nome** ("Sala TV 4K" vira `sala-tv-4k`, com `-2`, `-3`... se já existir) e
  **nunca muda** depois: `defaultProfileId` aponta para ele.
- **Intervalos de IP não aparecem na tela.** O seletor de perfil hoje só compara o nome do
  cliente (ADR 0002); editar um perfil preserva o que o arquivo tiver.
- Cada valor do perfil tem três modos: seguir o cliente, valor fixo ou deixar como está.

## Dividido em dois PRs

- **11a:** `ProfileForm` e testes. Nenhuma mudança visível no app.
- **11b:** a tela (lista, formulário, novo, salvar, excluir, perfil padrão), pt-BR e en. Código em
  `MainWindow.Profiles.cs`; excluir pede confirmação; "perfil padrão" liga `defaultProfileId`, e
  excluir esse perfil limpa o campo. Mudanças valem a partir da próxima transmissão.
