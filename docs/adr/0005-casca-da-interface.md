# 0005. Casca da interface (PR 9a)

- Status: aceita
- Data: 2026-11-23

## Contexto

O PR 9 do plano (`docs/fase1-plano.md`) pede bandeja, janela principal, tokens do design
system "Sinal" e idiomas pt-BR e en. É grande demais para um PR só, então foi dividido:

- **9a (esta ADR):** tokens do Sinal, idiomas e a janela principal com a navegação.
- **9b:** ícone na bandeja, hospedagem do app (orquestrador, canal e leitor de log ligados
  de verdade) e a tela Painel com o estado ao vivo.

## Decisão

### Tokens em `ResourceDictionary`, tema trocado ao vivo

Cores, espaçamento, tipografia e movimento ficam em arquivos XAML em
`src/SunshineHelper.App/Themes/`. As cores têm um arquivo por tema (`Colors.Dark.xaml`,
`Colors.Light.xaml`) com **as mesmas chaves**; os controles usam `DynamicResource`, então
trocar o dicionário recolore a janela sem reabri-la. `Metrics.xaml` (espaçamento, raios,
escala de texto, movimento) não depende do tema e é carregado uma vez.

O tema segue o Windows (`AppsUseLightTheme`) e muda sozinho se o Windows mudar com o app
aberto. O padrão, quando a configuração não pode ser lida, é o escuro, que é o tema padrão
do design. O overlay (Fase 2) será sempre escuro, por conta própria.

### Valores: os do plano, com os contrastes medidos

O plano estimava os contrastes e pedia validar com um script. Medidos, os valores do tema
escuro passam: texto principal 17:1 (o plano dizia cerca de 15:1), texto secundário 7,8:1,
azul de foco 7:1, botão primário 5,4:1. O tema claro **não** tinha valores definidos além
do fundo, da superfície, do texto e do azul; os tons de estado (OK, aviso, perigo) e o
texto secundário foram escolhidos aqui para passar 4,5:1 sobre o fundo e a superfície.

`SinalThemeTests` (em `SunshineHelper.Domain.Tests`, que lê os XAML como XML) faz essa
conferência a cada CI: os dois temas definem as mesmas cores, texto principal ≥ 7:1,
texto secundário e estados ≥ 4,5:1, botão primário ≥ 4,5:1. `TextMute` é só metadado ("nunca
texto essencial", diz o design) e tem o piso de 3:1.

### Idiomas: tabela por arquivo, escolha em código testado

`Localization/Strings.pt-BR.xaml` e `Strings.en.xaml` têm as mesmas chaves (um teste falha
se divergirem: chave faltando viraria texto vazio na tela, sem aviso). Qual idioma mostrar é
decisão do `LanguageResolver` (Domain, sem WPF): preferência do usuário, senão o idioma do
Windows; qualquer português (`pt`, `pt-PT`, `pt-BR`) lê a tabela pt-BR; qualquer outro
idioma sem tabela cai em inglês. A troca é ao vivo, pelo mesmo mecanismo do tema.

A preferência de idioma ainda não existe em `settings.json`: o 9a só segue o Windows. O
campo e o seletor na tela Geral chegam com o 9b, junto de quem lê as configurações.

### Fontes: ainda não embutidas

O design pede Hanken Grotesk e JetBrains Mono (as duas OFL), dentro do app. Embutir exige
baixar e versionar os arquivos de fonte, o que merece um PR próprio. Até lá, `Metrics.xaml`
usa as fontes que o Windows 11 já traz (Segoe UI Variable e Cascadia Mono) e a troca é
em duas linhas.

## O que fica para depois

- 9b: bandeja, hospedagem, Painel com estado ao vivo, idioma e tema escolhidos nas
  configurações.
- Fontes embutidas; ícones (Fluent System Icons); fundo Mica; "Galeria" de componentes em
  modo debug; os demais componentes da prancha 2 (interruptor, seletor, selos, etc.).

## Consequências

- Nada de cor, medida ou texto fica fixo em tela: tudo vem de uma chave, então os dois
  temas e os dois idiomas funcionam por construção.
- O que o CI não vê é o resultado na tela. A janela compila e os dados dos temas e das
  tabelas são testados, mas se ficou legível e bonito só a máquina do autor confirma
  (`docs/testes-manuais.md`).
