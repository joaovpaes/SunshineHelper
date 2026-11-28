# 0017. Fechar jogos que rodam como administrador: modo elevado do auxiliar

- Status: proposta
- Data: 2026-11-27 (revista no mesmo dia: sem programa novo, a pedido do autor)
- Complementa: 0016 (seção 6, "Administrador: limite aceito e explicado")

## Contexto

O PR 74 (ADR 0016) foi testado no PC do autor em 2026-11-27:

- com um jogo comum (Mortal Shell 2, "Elevado = Não"), o combo fechou o jogo em 1,6 s e o Playnite voltou;
- com `SuperOdysseyRecomp.exe` ("Elevado = Sim", mesmo aberto por um Playnite comum), o Windows recusou o pedido de
  fechar (`PostMessage` com acesso negado). Alguns desses jogos *recomp* exigem administrador de verdade.

O Windows não deixa um programa comum mandar mensagens para a janela de um programa elevado nem encerrá-lo (UIPI e a
lista de acesso do processo). É uma proteção proposital, sem atalho: só quem tem o privilégio consegue. O autor pediu
que o app cubra esses jogos **sem um programa separado**; aceitou mudar regra de negócio se preciso.

Fatos que pesaram na escolha:

- Qualquer coisa que rode como administrador **sem UAC** precisa estar numa pasta onde só administradores gravam
  (`Program Files`). Se estiver numa pasta do usuário (a do zip), qualquer programa troca o arquivo e ganha
  administrador sem o usuário ver.
- O app (`SunshineHelper.exe`) é .NET comum com WPF. Um processo assim, elevado, **obedece variáveis de ambiente do
  usuário que mandam carregar DLLs** (`DOTNET_STARTUP_HOOKS`, `CORECLR_ENABLE_PROFILING` com `CORECLR_PROFILER_PATH`,
  `DOTNET_GCName`...), e qualquer programa do usuário pode criá-las. Além disso, o pacote de arquivo único extrai as
  DLLs nativas do WPF para a pasta temporária do usuário (`IncludeNativeLibrariesForSelfExtract`). Rodar **esse**
  `.exe` elevado por uma tarefa abriria um caminho silencioso para administrador.
- O auxiliar de gatilho (`SunshineHelper.Trigger.exe`, ADR 0003) já faz parte do pacote, é **NativeAOT** (código de
  máquina, sem carregador de .NET, sem *startup hooks*, sem *profiler*, sem DLL extraída) e é pequeno.

## Decisão

### 1. Nenhum programa novo: o auxiliar ganha um modo "fechar elevado"

O `SunshineHelper.Trigger.exe` passa a ter um terceiro modo, além de `start` e `end`: `elevated-close`. Nesse modo ele
**só sabe fazer duas coisas** com **um** processo:

- `close`: pedir para fechar as janelas dele (`WM_CLOSE`, o mesmo que clicar no X);
- `kill`: encerrar **só esse processo** (sem a árvore; o app pede um por um, como já faz com a lista de protegidos).

O usuário continua vendo um programa só (o SunshineHelper), com os mesmos dois arquivos de hoje. O app em si continua
**sem administrador** (ADRs 0008 e 0012 seguem valendo). O modo `elevated-close` não fala com o pipe, não lê
configuração e sai em seguida.

### 2. Como o app chama o modo elevado

- A tarefa `SunshineHelper Close Elevated Game`, na pasta raiz do Agendador de Tarefas (uma tarefa na raiz cujo
  principal é o usuário pode ser iniciada por ele sem elevação), é registrada com: conta = o próprio usuário, "executar só com o usuário
  conectado" (sem senha guardada), **"executar com privilégios mais altos"**, oculta, sem gatilho (só sob demanda),
  limite de 30 s, e "não iniciar outra instância" se uma já estiver rodando.
- A ação é fixa: `"<pasta protegida>\SunshineHelper.Trigger.exe" elevated-close $(Arg0) $(Arg1) $(Arg2)`, com a pasta
  de início também fixa. O app dispara a tarefa pelo Agendador de Tarefas (COM, `IRegisteredTask.RunEx`), passando só
  três valores: a operação (`close` ou `kill`), o PID e o horário de início do processo.
- O modo elevado **não confia em nada disso**: os valores podem vir de qualquer programa do usuário, não só do app (ver
  "Riscos"). Ele confere tudo de novo, sozinho, no Windows.
- O app sabe o resultado olhando o próprio processo (sumiu ou não, como já faz hoje) e lê o código de saída da tarefa
  (`LastTaskResult`) só para escrever o motivo no `app.log`.
- Os modos `start` e `end` continuam iguais e sempre saindo com 0 (ADR 0002). O `elevated-close` usa códigos de saída
  próprios (motivo da recusa), porque quem o chama é a tarefa, não o Sunshine.

### 3. O que o modo elevado confere antes de agir (todas as regras; uma falha basta para recusar)

Ele não lê o `settings.json` nem nenhum arquivo do usuário: tudo vem do próprio Windows ou está fixo no código.

1. **Argumentos exatos:** `elevated-close`, depois `close` ou `kill`, depois dois números inteiros. Qualquer outra coisa
   (a mais, a menos, texto, aspas) é recusada. A tarefa sempre põe `elevated-close` primeiro, então ela não alcança
   nenhum outro modo.
2. **Mesmo processo:** o PID existe e o horário de início é **exatamente** o informado (o Windows reaproveita PIDs).
3. **O processo está elevado.** Se não estiver, recusa: o app consegue fechá-lo sozinho. Isso mantém o poder do modo
   elevado restrito ao buraco que ele cobre.
4. **Mesmo usuário e mesma sessão do Windows:** o dono do processo é o usuário da tarefa, na sessão interativa dele.
   Processos do `SYSTEM`, de serviços ou de outro usuário são recusados.
5. **Foi aberto pelo Sunshine:** subindo pelos pais do processo, com cada pai **estritamente** mais antigo que o filho
   (para não ser enganado por PID reaproveitado), chega-se a um `sunshine.exe` em execução **que roda como SYSTEM ou
   elevado** (o serviço do Sunshine o abre como SYSTEM). Uma cópia de `sunshine.exe` largada e aberta por um programa
   comum não conta (revisão de segurança do D2a). Tudo o que já estava aberto antes, ou foi
   aberto pelo usuário fora da transmissão (antivírus, ferramentas de administrador, o Playnite aberto à mão), fica de
   fora sem depender de lista nenhuma.
6. **Tem janela visível** de primeiro nível. Programas de fundo e ferramentas sem janela ficam de fora.
7. **Não está na lista fixa de nomes proibidos** (a mesma do ADR 0016: Windows, Sunshine, este app,
   `ApplicationFrameHost`), compilada no auxiliar, mais consoles e instaladores elevados que o próprio Sunshine pode
   rodar (`cmd`, `powershell`, `pwsh`, `WindowsTerminal`, `OpenConsole`, `msiexec`).
8. **Não é um processo crítico do Windows** (`IsProcessCritical`).

Consequências aceitas dessas regras (todas recusam, o que é o lado seguro): um filho do jogo **sem janela** não é
encerrado pelo modo elevado; e um jogo cujo launcher **já fechou** perde o caminho até o Sunshine e também é recusado.

As proteções do usuário (lista do launcher, lista de protegidos, "começou antes da sessão", alvo único, confirmação e
pausa) continuam sendo feitas pelo app, **antes** de chamar a tarefa, como no ADR 0016. O modo elevado não as repete
porque dependeriam do `settings.json`, que qualquer programa do usuário pode alterar; ler um arquivo do usuário num
processo elevado também aumentaria o que pode dar errado lá dentro.

### 4. O que o modo elevado nunca faz

- **Não escreve em disco.** Nada de log no `%LOCALAPPDATA%`: um processo elevado gravando numa pasta que o usuário
  controla é a porta clássica para um atalho (link simbólico) redirecionar a gravação para um arquivo do sistema. O
  resultado sai só pelo código de saída.
- **Não lê configuração**, não monta caminho a partir de variável de ambiente e não carrega DLL de pasta do usuário. As
  DLLs do Windows só vêm da System32, e o CI roda o auxiliar publicado com as variáveis que mandariam o .NET carregar
  outro GC, um *startup hook* ou um *profiler*, para provar que o NativeAOT as ignora.
- **Não encerra árvores**, não inicia processos e não aceita caminho, nome ou comando como argumento.

### 5. Instalar no PC: o auxiliar vai para uma pasta protegida

- **Pasta protegida:** `%ProgramFiles%\SunshineHelper\`, onde só administradores gravam. A tarefa só aponta para a
  cópia de lá. Esta é a regra mais importante do desenho.
- **Instalar:** na tela Geral, seção "Fechar o jogo", um botão **"Permitir fechar jogos de administrador"**. Ele pede UAC
  **uma vez**; o passo elevado copia o `SunshineHelper.Trigger.exe` do pacote para a pasta protegida e registra a
  tarefa. Quem faz isso é o próprio auxiliar no modo `install`, que **só é alcançado pelo UAC** (a tarefa sempre passa
  `elevated-close` primeiro, regra 1).
- **O Sunshine pode usar a mesma cópia.** Depois de instalar, a tela mostra o caminho novo do auxiliar para o Do/Undo
  Command do Sunshine. Não é obrigatório (a cópia da pasta do zip continua funcionando para `start` e `end`), mas evita
  ter duas cópias com versões diferentes.
- **Remover:** botão **"Remover"** no mesmo lugar (UAC de novo): apaga a tarefa e a cópia. Manualmente:
  `schtasks /delete /tn "SunshineHelper Close Elevated Game" /f` e apagar a pasta.
- **Versão:** a tela compara a versão do auxiliar instalado com a do pacote. Se o app for atualizado e o auxiliar
  mudar, aparece **"Atualizar"** (UAC de novo). Uma cópia antiga continua segura: só faz menos.
- Este é o primeiro passo do instalador que está no backlog; quando ele existir (já rodando como administrador), faz o
  mesmo, e o botão fica para quem usa o zip.
- Conta sem administrador: "privilégios mais altos" não eleva nada. A tela avisa que a função não está disponível.

### 6. O que muda no app (fluxo do ADR 0016)

- Pedir para fechar: se o Windows recusar (`Denied`) **e** a tarefa estiver instalada, o app pede `close` pela tarefa e
  segue igual (espera o teto, confirmação pendente, pausa).
- Encerrar (segunda segurada ou "encerrar sem pedir confirmação"): o app monta a lista como hoje (o jogo e os filhos,
  sem os protegidos) e pede `kill` de um em um pela tarefa para os que estiverem elevados.
- Sem a tarefa instalada: o aviso do log passa a dizer "o jogo roda como administrador; na tela Geral, use 'Permitir
  fechar jogos de administrador'".
- **Como o app acha o jogo (D2c):** o Windows não deixa o app abrir um processo elevado, então o .NET não lê a hora de
  início. O app lê PID, nome e hora de início da lista de processos do Windows (`NtQuerySystemInformation`), que não
  abre nenhum processo, e confere as proteções dele (launcher, lista protegida, aberto antes da transmissão) como
  para qualquer jogo. Um jogo elevado na frente vai direto pela tarefa; um jogo comum cujo pedido o Windows recusar
  também. Esperar o jogo sumir usa a mesma lista (mesmo PID e mesma hora de início).
- **Encerrar pela tarefa encerra só o jogo:** a tarefa age num processo com janela, e o app não consegue abrir os
  filhos de um processo elevado. Filhos sem janela ficam (na prática, somem com o jogo ou não atrapalham).
- **Resposta da tarefa:** o app dispara com `RunEx` e lê `LastTaskResult` quando a tarefa termina (até 12 s). O
  número vira o motivo no `app.log` ("the process was not started by Sunshine", etc.). Sem resposta a tempo, o app
  segue olhando o próprio processo. Os códigos ficam duplicados no app (`ElevatedCloseReason`) e no auxiliar; um
  teste mantém as duas listas iguais.
- A função "fechar o jogo" continua desligada de fábrica, e a permissão de administrador também (não instalada).

## Riscos aceitos

- **Qualquer programa rodando como o usuário pode disparar a tarefa.** O Windows não deixa restringir isso só ao app. O
  pior que esse programa consegue é fechar ou encerrar um programa **elevado, com janela, do próprio usuário, aberto
  pelo Sunshine**. Na prática, um jogo ou o launcher durante a transmissão: perda de progresso, sem ganho de acesso.
  Não abre caminho para rodar código elevado nem para atingir o Windows, serviços, outros usuários ou o que o usuário
  abriu por conta própria.
- **Quem já é administrador** pode fazer tudo isso sem a tarefa; fica fora do escopo, como no `SECURITY.md`.
- **A cópia na instalação:** no instante do UAC, o auxiliar é copiado da pasta do zip. Um programa malicioso que já
  esteja no PC poderia trocar o arquivo antes; é o mesmo risco de qualquer instalador que o usuário aprova, e o UAC
  mostra o nome do programa. A assinatura de código (backlog) reduz isso.

## Suposição a confirmar no PC antes do código

A regra 5 supõe que o jogo descende do `sunshine.exe` (Sunshine abre o Playnite, que abre o jogo). Se o Sunshine abrir
o Playnite por um atalho ou URL, o pai pode ser outro (por exemplo o `explorer`). Conferência: com uma transmissão e o
jogo elevado abertos, rodar no PowerShell (comum, sem administrador):

```powershell
$p = Get-CimInstance Win32_Process -Filter "Name='SuperOdysseyRecomp.exe'"
for ($i = 0; $p -and $i -lt 10; $i++) { "{0} ({1})" -f $p.Name, $p.ProcessId; $p = Get-CimInstance Win32_Process -Filter "ProcessId=$($p.ParentProcessId)" }
```

Se a lista chegar a `sunshine.exe`, a regra fica como está. Se não, este ADR é revisto antes do código (a reserva seria
"descende de um processo da lista do launcher", mais fraca porque a lista vem do `settings.json`).

## Dividido em PRs

- **Este PR:** só o ADR, para aprovação.
- **D2a:** o modo `elevated-close` no auxiliar, com as regras da seção 3 numa parte pura testada no CI com processos
  falsos (o auxiliar continua sem referência a Application nem Infrastructure).
- **D2b:** instalar/remover/atualizar pelo UAC e a situação na tela Geral, `SECURITY.md`, guia e roteiro em
  `docs/testes-manuais.md`. Pela revisão de segurança: a pasta instalada é recusada se for um link e recebe
  permissões explícitas (só administradores gravam); uma tarefa de mesmo nome criada antes por outro programa é
  apagada antes de criar a nova; e o app manda o SID da conta que clicou, para a instalação recusar se o UAC for
  aprovado por outra conta de administrador.
- **D2c:** a chamada pelo Agendador de Tarefas e a ligação no `CloseGameService`, com o jogo elevado identificado
  pela lista de processos do Windows (seção 6).
- Todos com Opus · esforço alto e **revisão de segurança por um agente separado** antes de mesclar.

## Alternativas consideradas

- **O próprio `SunshineHelper.exe` num modo elevado** (primeira forma do U1, escolhido pelo autor): é .NET comum com
  WPF; elevado, ele obedece variáveis de ambiente do usuário que carregam DLLs e extrai DLLs nativas na pasta temporária
  do usuário. Seria um caminho silencioso para administrador. Trocado pelo auxiliar NativeAOT, que já está no pacote.
- **Programa novo só para isso** (primeira versão deste ADR): funcionaria igual ao modo no auxiliar, mas o autor não quer
  outro programa, e o auxiliar já tem tudo o que é preciso (NativeAOT, pequeno, sem tela).
- **App inteiro como administrador (U2):** qualquer erro da interface, do leitor do log ou do pipe passaria a ter poder
  total no PC, e tudo o que o app abrisse (pasta do log, navegador) abriria elevado. Contraria os ADRs 0008 e 0012.
  Rejeitada pelo autor.
- **Serviço do Windows rodando como `SYSTEM`:** mais poder do que o necessário, sempre rodando e escutando, e precisa de
  instalador. Rejeitada.
- **Pedir UAC a cada fechamento:** a janela do UAC aparece na área segura do Windows, ruim de responder com o controle
  pela transmissão. Rejeitada.
- **Modo elevado que encerra a árvore inteira ou recebe nome do processo:** dá mais poder a quem dispara a tarefa sem
  necessidade. Rejeitada.
- **Rodar o jogo sem administrador** (desmarcar na aba Compatibilidade, ou `RunAsInvoker`): resolve os jogos que só
  foram marcados assim, e continua sendo a primeira orientação do guia, mas não os que exigem administrador.
- **Não tratar (só o aviso):** era a decisão do ADR 0016; o autor pediu que o app cubra esses jogos.

## Consequências

- Jogos de administrador abertos pelo Sunshine passam a fechar pelo combo, depois de um UAC único.
- O app continua sem administrador e com os mesmos dois arquivos. O único código que roda elevado é o modo
  `elevated-close` do auxiliar, sem estado, numa pasta protegida.
- O auxiliar deixa de ter um único papel (o gatilho do Sunshine); os modos ficam separados no código e nos testes.
- `SECURITY.md` ganha o modo elevado no escopo, com o risco aceito acima.
- O ADR 0016 (seção 6) passa a apontar para este.
