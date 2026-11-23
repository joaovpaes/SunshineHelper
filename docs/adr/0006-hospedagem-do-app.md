# 0006. Hospedagem do app, bandeja e Painel (PR 9b)

- Status: aceita
- Data: 2026-11-23

## Contexto

Até o PR 9a o app era só uma janela: o orquestrador, o canal do pipe e o leitor do log
existiam e estavam testados, mas nada os ligava. O 9b faz o app "ligar de verdade". A tela
Geral (idioma, caminho do log) fica para o 9c, para manter os PRs pequenos.

## Decisão

### Uma raiz de composição, na ordem certa

`AppHost` (projeto do app) monta os módulos reais (tela, HDR, limite de FPS), o
orquestrador, o canal e o leitor do log. A ordem na inicialização é fixa:

1. **Ler `settings.json`.** Arquivo ausente: padrões, sem criar nada. Arquivo ilegível ou
   inválido: o app abre **sem perfis** (então nada é aplicado) e o Painel mostra o motivo.
   Um `settings.json` quebrado nunca impede o app de abrir.
2. **Restaurar uma sessão esquecida** (`SessionOrchestrator.RestoreAsync`): se o app caiu
   ou o PC reiniciou no meio de uma transmissão, o ambiente volta antes de qualquer coisa
   nova começar. É o "ao abrir de novo, restaura" do portão da Fase 1.
3. **Só então abrir o pipe.** Assim um cliente que conecta durante a restauração nunca
   disputa com ela.
4. **Ligar o leitor do log**, se `SunshineLogPath` estiver definido. Sem caminho, o leitor
   fica desligado (o pipe funciona do mesmo jeito) e o Painel diz isso. O caminho só passa
   a ser definível no 9c; nenhum caminho do PC do autor é usado como padrão.

As configurações em uso ficam em `CurrentSettingsHolder` (Application): o canal e o
monitor só leem; quem edita publica. O leitor do log lê o caminho uma vez, na abertura;
mudar o caminho em execução pede reabrir o app (limitação do 9b, resolvida no 9c).

### Painel lê o snapshot, não ganha outra fonte de verdade

`SessionStatusReader` (Application) deriva o estado do arquivo de snapshot, que já é o
registro único de "o ambiente está modificado" (ADR 0002). Como o pipe e o leitor do log
passam pelo mesmo orquestrador, o Painel mostra o certo qualquer que seja o caminho que
iniciou a sessão, sem tocar no núcleo. A tela consulta a cada 1 s e só enquanto visível.
Estados: **ocioso** (sem snapshot), **ativo** (aplicando, aplicado ou revertendo) e
**precisa de atenção** (reversão falhou, ou o snapshot não pôde ser lido).

### Bandeja e fechar a janela

O app vive na bandeja: o X da janela só esconde; **Sair** (menu da bandeja) encerra. WPF
não tem controle de bandeja; usa-se `NotifyIcon` do WinForms (`UseWindowsForms`, só para
isso; os nomes ambíguos são qualificados no código). O ícone é o genérico do Windows até
o ícone do produto existir (junto das fontes, em PR próprio).

Sair com uma transmissão ativa **não** reverte o ambiente: o snapshot fica e a próxima
abertura restaura (passo 2). Reverter ao sair pode ser decidido depois.

### Uma cópia só

Um `Mutex` nomeado impede abrir o app duas vezes: a segunda cópia não conseguiria abrir
o pipe e brigaria pelo snapshot. A segunda cópia simplesmente fecha.

## O que fica para depois (9c)

Tela Geral: idioma (campo em `settings.json`), caminho do log do Sunshine, aviso de nível
de log insuficiente, aplicar mudança de caminho sem reabrir.

## Consequências

- `AppHost`, `TrayIcon` e a janela não rodam no CI (precisam de Windows com tela). Por isso
  a parte com lógica (`CurrentSettingsHolder`, `SessionStatusReader`) fica em Application,
  com testes; o resto tem checklist em `docs/testes-manuais.md`.
- Pedir administrador para o limite de FPS (RTSS fica em Program Files) continua sendo
  assunto do PR 10; sem isso, aplicar o FPS falha com a mensagem do módulo e o ambiente
  volta ao que era.
