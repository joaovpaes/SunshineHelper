# SunshineHelper (versão de testes): como usar

Esta é uma **versão de testes** (pré-lançamento). Ela já faz o trabalho principal: quando uma transmissão do
Sunshine começa, aplica o perfil do aparelho (resolução, HDR, limite de FPS pelo RTSS) e, quando a transmissão
acaba, devolve tudo como estava. Mas ainda **não tem instalador** e **não é assinada**.

## O que vem no zip

- `SunshineHelper.exe`: o app (fica na bandeja do Windows). Não precisa instalar o .NET.
- `SunshineHelper.Trigger.exe`: o auxiliar que o Sunshine chama no começo e no fim da transmissão (e que, se você permitir, fecha jogos de administrador; veja mais abaixo).
- `LICENSE` e este `LEIA-ME.md`.

Confira o zip com o `SHA256SUMS.txt` da página da versão (no PowerShell: `Get-FileHash .\arquivo.zip`; o valor
tem de ser igual ao do arquivo).

## Limites desta versão (leia antes)

- **Sem assinatura de código:** o Windows mostra "O Windows protegeu o computador" (SmartScreen). Clique em
  "Mais informações" e "Executar assim mesmo", se você confia na origem.
- **Sem instalador:** descompacte numa pasta sua (por exemplo `C:\Programas\SunshineHelper`) e abra o
  `SunshineHelper.exe`.
- **O app precisa estar aberto antes da transmissão.** Se estiver fechado, o auxiliar desiste em 2 s e a
  transmissão começa sem perfil. Para abrir junto com o Windows, marque na tela **Geral** a opção
  "Iniciar com o Windows, minimizado na bandeja" (não precisa de administrador; também dá para desligar em
  Configurações do Windows, Aplicativos, Inicializar). Se você já tinha um atalho em `shell:startup`, apague-o.
  Atenção: o Windows abre o app a partir da pasta em que ele está; se você mover a pasta, abra o `.exe` de lá
  uma vez e o app corrige o caminho sozinho.
- Testado até agora só em Windows 11 com o Sunshine para Windows, RTSS 7.3.x e Playnite.

## Configurar (uma vez)

1. Abra o `SunshineHelper.exe`. Na tela **Geral**, informe o caminho do `sunshine.log` do Sunshine (botão
   "Procurar"). No Sunshine, o nível de log deve ser **Debug** (o app avisa no Painel se estiver baixo).
2. Na tela **Perfis**, crie um perfil (nome do aparelho como aparece no Moonlight, resolução, HDR, limite de FPS).
   Um perfil marcado como padrão vale quando nenhum nome bate.
3. No Sunshine (página web, **Applications**, o seu app, **Command Preparations**), adicione:
   - **Do:** `"C:\caminho\da\pasta\SunshineHelper.Trigger.exe" start`
   - **Undo:** `"C:\caminho\da\pasta\SunshineHelper.Trigger.exe" end`

   O auxiliar lê sozinho o nome e as configurações do aparelho que o Sunshine envia. Funciona com ou sem
   "Run as elevated".
   **Opcional, para o caso de esquecer o app fechado:** acrescente, **antes** do `Trigger start`, um comando
   **Do** que abre o app sem travar o Sunshine (que espera cada comando terminar):
   `cmd /c start "" "C:\caminho\da\pasta\SunshineHelper.exe"` (deixe "Run as elevated" **desmarcado**, para o app
   abrir na sua conta). O app abre em alguns segundos e o auxiliar só espera 2 s, então, se o app estava fechado,
   a primeira transmissão pode começar sem perfil. Para dar tempo, ponha entre os dois um comando de espera:
   `cmd /c ping -n 6 127.0.0.1 >nul` (custa uns 5 s em toda transmissão). Ele não abre uma segunda cópia se o app
   já estiver aberto. Na **Undo** não é preciso nada para o app.
4. **Limite de FPS (RTSS):** se o perfil limita o FPS e o app ainda não pode gravar o perfil do RTSS, a tela
   Perfis mostra um aviso com o botão **Reparar**. O Windows pede confirmação uma vez e libera só esse arquivo.
   O RTSS precisa estar aberto para o limite valer.

## Resolução maior que a do monitor (Super Resolução)

O app só **pede ao Windows** a resolução do perfil. Se o seu monitor é 2560x1440 e o perfil pede 3840x2160, o
Windows só aceita se a placa de vídeo oferecer essa resolução como "maior que a nativa". Isso se liga no driver:

- **Placa AMD (Virtual Super Resolution, VSR):** abra o **AMD Software: Adrenalin Edition**, procure por **VSR** e
  ligue **Virtual Super Resolution** (vem desligado; a tela pode apagar por alguns segundos). Depois disso, as
  resoluções maiores aparecem nas configurações de vídeo do Windows. A AMD recomenda o Adrenalin mais recente.
- **Placa NVIDIA (DSR):** no **Painel de Controle da NVIDIA**, escolha os fatores do **DSR** que você quer usar.
  (Os nomes dos menus mudam conforme o driver.)
- Depois de ligar, confira em Configurações do Windows, Sistema, Vídeo, Vídeo avançado (ou na lista de
  resoluções) se a resolução desejada aparece. Se ela não aparecer ali, o app também não consegue aplicá-la.

Se o perfil pedir uma resolução que o driver não oferece, o app **não derruba a transmissão**: ele registra
"Troca de resolução recusada pelo driver de vídeo" no `app.log` e a sessão segue com a resolução que estava.

## Fechar o jogo com o controle (opcional)

Na tela **Geral**, a caixa **Fechar o jogo com o controle** vem desligada. Ligada, durante uma transmissão você
segura **Select + Y por 5 segundos** no controle e o app fecha o jogo que está na frente. Ele só olha o controle
durante a transmissão.

- **Como fecha:** primeiro pede para o jogo fechar, como se você clicasse no X (o jogo pode salvar ou perguntar
  "tem certeza?"). Se o jogo não fechar em 5 s, segure o combo **de novo em até 15 s** para encerrá-lo. Existe a
  opção de encerrar sem pedir confirmação, desligada de fábrica.
- **O que nunca fecha:** o Windows, o Sunshine, este app, o launcher da lista da tela Geral (Playnite, por
  exemplo) e tudo que já estava aberto antes da transmissão. Depois que o jogo fecha, o combo fica parado por
  5 s, para não atingir o launcher que volta para a frente. Por isso **preencha a lista de programas do launcher**.
- **No Sunshine**, deixe a opção `gamepad` em `x360`: um controle virtual no estilo PlayStation não é visto.
- **Jogo como administrador:** o app não é administrador e o Windows não deixa ele fechar sozinho um programa que
  é. Com a permissão da seção abaixo, o combo fecha esses jogos também. Se o Playnite está com "Run as elevated" no
  Sunshine, os jogos dele ficam todos elevados; se o jogo não precisa disso, o melhor é desmarcar "Run as elevated"
  no app do Playnite, no Sunshine.
- Combo e tempo de segurar ficam no `settings.json` (seção `controller`); a tela para gravá-los vem depois.

### Fechar jogos de administrador (opcional)

Alguns jogos só abrem como administrador. Para o app conseguir fechá-los sem ele mesmo virar administrador, na
tela **Geral**, em "Jogos que rodam como administrador", clique em **Permitir fechar jogos de administrador** e
confirme o aviso do Windows (uma vez só).

- **O que isso faz:** copia o `SunshineHelper.Trigger.exe` para `C:\Program Files\SunshineHelper` e cria uma
  tarefa agendada chamada "SunshineHelper Close Elevated Game". Ela só roda quando o app pede, e só fecha um jogo
  que roda como administrador, é seu, tem janela e foi aberto pelo Sunshine.
- **Desfazer:** botão **Remover**, no mesmo lugar (outro aviso do Windows). Apaga a tarefa e a cópia.
- **Depois de atualizar o app:** se a tela disser "precisa atualizar", clique em **Atualizar**.
- **Conta sem administrador:** a função fica indisponível.
- **Com a permissão ligada,** o combo funciona igual para esses jogos: pede para fechar, e a segunda segurada
  encerra. Encerrar um jogo de administrador encerra só o jogo, não os programas que ele abriu. As proteções
  continuam valendo (o launcher, a lista protegida e o que já estava aberto antes da transmissão).
- **Sem a permissão,** o combo não fecha esses jogos e o log diz para usar o botão da tela Geral.
- **Limite conhecido:** a tarefa só aceita um jogo aberto por um Sunshine que roda como administrador. Por isso, para
  fechar um jogo de administrador, o app do Playnite (ou do launcher) precisa estar com **"Run as elevated"** no
  Sunshine. Com o Playnite sem administrador e o jogo com administrador, a tarefa recusa e o log diz "the process was
  not started by Sunshine". Isso será revisto em um PR próprio.
- **Se o log disser que a tarefa recusou,** o motivo vem junto. Por exemplo, "not started by Sunshine": o jogo não
  foi aberto pelo Sunshine (nem pelo Playnite que o Sunshine abriu).
- **Se aparecer um código:** `40` o aviso do Windows não deu administrador; `41` o app não achou o
  `SunshineHelper.Trigger.exe` ao lado dele; `42` não deu para copiar ou apagar em `Program Files` (feche o que
  estiver usando o arquivo e tente de novo); `43` o Agendador de Tarefas recusou; `44` não deu para ler a sua conta
  do Windows; `45` o aviso do Windows foi aprovado com **outra** conta de administrador (aprove com a sua);
  `46` a pasta em `Program Files` não é segura (é um atalho para outro lugar) e nada foi instalado; `30` erro
  inesperado.

## O que o app não cobre

- **Problemas do Sunshine com o monitor.** O app não liga, não acorda nem troca o monitor: ele só muda
  resolução, HDR e limite de FPS do **monitor principal** e devolve tudo no fim. Se o Sunshine precisa que o
  monitor fique ligado ou ativo durante a transmissão (ou deixa a tela em um estado estranho), isso é do Sunshine
  e do Windows, e o app não corrige.
- **Monitor sem HDR.** Se o monitor principal não suporta HDR, a função de HDR não funciona: o app se recusa a
  ligar o HDR com uma mensagem clara, avisa no Painel e no `app.log` e a transmissão continua **sem HDR**. Nesse
  caso, deixe o HDR do perfil como "Deixar como está".
- **Taxa de atualização:** o app não a muda (só resolução, HDR e limite de FPS).
- Mais de um monitor: o app age só sobre o monitor principal.

## Como saber se funcionou

- No **Painel**: "Transmissão ativa" durante a transmissão e "Nenhuma transmissão ativa" depois.
- O app guarda um registro em `%LOCALAPPDATA%\SunshineHelper` (`app.log`). Em **Geral** há o botão
  "Gerar relatório de diagnóstico" (dados sensíveis são ocultados; leia o arquivo antes de compartilhar).
- A tela **Log** mostra o mesmo registro dentro do app, e a tela **Dispositivos** lista os aparelhos que já
  conectaram e o perfil de cada um.
- Se o Sunshine for finalizado no meio da transmissão, o app restaura a tela sozinho em poucos segundos.

## Problemas

Abra uma issue no GitHub com o relatório de diagnóstico. **Leia o arquivo antes de enviar.**
