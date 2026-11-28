# 0017. Ajudante elevado para fechar jogos que rodam como administrador

- Status: proposta
- Data: 2026-11-27
- Complementa: 0016 (seção 6, "Administrador: limite aceito e explicado")

## Contexto

O PR 74 (ADR 0016) foi testado no PC do autor em 2026-11-27:

- com um jogo comum (Mortal Shell 2, "Elevado = Não"), o combo fechou o jogo em 1,6 s e o Playnite voltou;
- com `SuperOdysseyRecomp.exe` ("Elevado = Sim", mesmo aberto por um Playnite comum), o Windows recusou o pedido de
  fechar (`PostMessage` com acesso negado). Alguns desses jogos *recomp* exigem administrador de verdade.

O Windows não deixa um programa comum mandar mensagens para a janela de um programa elevado nem encerrá-lo (UIPI e a
lista de acesso do processo). É uma proteção proposital: não há como contornar sem ter o privilégio. O autor pediu que o
app cubra esses jogos.

O que não muda: o app continua **sem administrador** (ADRs 0008 e 0012). A interface, o leitor do log, o canal do pipe
e o resto do app não ganham privilégio nenhum.

## Decisão

### 1. Um ajudante pequeno, elevado só enquanto faz uma coisa

Um executável novo, `SunshineHelper.CloseHelper.exe`, que **só sabe fazer duas coisas** com **um** processo:

- `close`: pedir para fechar as janelas dele (`WM_CLOSE`, o mesmo que clicar no X);
- `kill`: encerrar **só esse processo** (sem a árvore; o app pede um por um, como já faz hoje com a lista de protegidos).

Ele roda elevado por uma **tarefa agendada** do Windows criada uma vez, com UAC, e sai em seguida. Não fica aberto, não
escuta nada, não abre janela.

### 2. Como o app chama o ajudante

- A tarefa `\SunshineHelper\CloseElevatedGame` é registrada com: conta = o próprio usuário, "executar só com o usuário
  conectado" (sem senha guardada), **"executar com privilégios mais altos"**, oculta, sem gatilho (só sob demanda),
  limite de 30 s, e "não iniciar outra instância" se uma já estiver rodando.
- A ação é fixa: `"<pasta protegida>\SunshineHelper.CloseHelper.exe" task $(Arg0) $(Arg1) $(Arg2)`, com a pasta de
  início também fixa. O app dispara a tarefa pelo Agendador de Tarefas (COM, `IRegisteredTask.RunEx`), passando só
  três valores: a operação (`close` ou `kill`), o PID e o horário de início do processo.
- O ajudante **não confia em nada disso**: os valores podem vir de qualquer programa do usuário, não só do app (ver
  "Riscos"). Ele confere tudo de novo, sozinho, no Windows.
- O app sabe o resultado olhando o próprio processo (sumiu ou não, como já faz hoje) e lê o código de saída da tarefa
  (`LastTaskResult`) só para escrever o motivo no `app.log`.

### 3. O que o ajudante confere antes de agir (todas as regras; uma falha basta para recusar)

O ajudante não lê o `settings.json` nem nenhum arquivo do usuário: tudo vem do próprio Windows ou está fixo no código
dele.

1. **Argumentos exatos:** `task`, depois `close` ou `kill`, depois dois números inteiros. Qualquer outra coisa (a mais, a
   menos, texto, aspas) é recusada. Não há outro modo acessível pela tarefa.
2. **Mesmo processo:** o PID existe e o horário de início é **exatamente** o informado (o Windows reaproveita PIDs).
3. **O processo está elevado.** Se não estiver, o ajudante recusa: o app consegue fechá-lo sozinho e o ajudante não é
   necessário. Isso mantém o poder dele restrito ao buraco que ele cobre.
4. **Mesmo usuário e mesma sessão do Windows:** o dono do processo é o usuário da tarefa, na sessão interativa dele.
   Processos do `SYSTEM`, de serviços ou de outro usuário são recusados.
5. **Foi aberto pelo Sunshine:** subindo pelos pais do processo, com cada pai mais antigo que o filho (para não ser
   enganado por PID reaproveitado), chega-se a um `sunshine.exe` em execução. Tudo o que já estava aberto antes, ou foi
   aberto pelo usuário fora da transmissão (antivírus, ferramentas de administrador, o próprio Playnite aberto à mão),
   fica de fora sem depender de lista nenhuma.
6. **Tem janela visível** de primeiro nível. Programas de fundo e ferramentas sem janela ficam de fora.
7. **Não está na lista fixa de nomes proibidos** (a mesma do ADR 0016: Windows, Sunshine, este app,
   `ApplicationFrameHost`), compilada no ajudante.
8. **Não é um processo crítico do Windows** (`IsProcessCritical`).

As proteções do usuário (lista do launcher, lista de protegidos, "começou antes da sessão", alvo único, confirmação e
pausa) continuam sendo feitas pelo app, **antes** de chamar o ajudante, como no ADR 0016. O ajudante não as repete
porque dependeriam do `settings.json`, que qualquer programa do usuário pode alterar; ler um arquivo do usuário num
processo elevado também aumentaria o que pode dar errado lá dentro.

### 4. O que o ajudante nunca faz

- **Não escreve em disco.** Nada de log no `%LOCALAPPDATA%`: um processo elevado gravando numa pasta que o usuário
  controla é a porta clássica para um atalho (link simbólico) redirecionar a gravação para um arquivo do sistema. O
  resultado sai só pelo código de saída.
- **Não lê configuração**, variável de ambiente para montar caminho, nem carrega DLL de pasta do usuário. É um `.exe`
  único em NativeAOT, como o auxiliar de gatilho (ADR 0003).
- **Não encerra árvores**, não inicia processos e não aceita caminho, nome ou comando como argumento.

### 5. Onde ele mora, como se instala e como se remove

- **Pasta protegida:** `%ProgramFiles%\SunshineHelper\CloseHelper\`, onde só administradores gravam. Se o ajudante
  ficasse na pasta do zip (que o usuário pode alterar), qualquer programa poderia trocar o `.exe` e ganhar
  administrador sem UAC. Esta é a regra mais importante do desenho.
- **Instalar:** na tela Geral, na seção "Fechar o jogo", um botão **"Permitir fechar jogos de administrador"**. Ele pede
  UAC **uma vez**; o passo elevado copia o ajudante para a pasta protegida e registra a tarefa. Feito pelo próprio
  ajudante num modo `install`, que **só existe quando aberto pelo UAC** e não pela tarefa (a tarefa sempre passa `task`
  primeiro, regra 1).
- **Remover:** botão **"Remover"** no mesmo lugar (UAC de novo): apaga a tarefa e a pasta. Manualmente:
  `schtasks /delete /tn "\SunshineHelper\CloseElevatedGame" /f` e apagar a pasta.
- **Versão:** a tela compara a versão do ajudante instalado com a do pacote. Se o app for atualizado e o ajudante
  mudar, aparece **"Atualizar"** (UAC de novo). Ajudante antigo continua seguro: só faz menos.
- Quando houver instalador, ele (que já roda como administrador) faz o mesmo, e o botão fica para quem usa o zip.
- Conta sem administrador: "privilégios mais altos" não eleva nada. A tela avisa que a função não está disponível.

### 6. O que muda no app (fluxo do ADR 0016)

- Pedir para fechar: se o Windows recusar (`Denied`) **e** o ajudante estiver instalado, o app chama `close` pelo
  ajudante e segue igual (espera o teto, confirmação pendente, pausa).
- Encerrar (segunda segurada ou "encerrar sem pedir confirmação"): o app monta a lista como hoje (o jogo e os filhos,
  sem os protegidos) e pede `kill` de um em um pelo ajudante para os que estiverem elevados.
- Sem ajudante instalado: o aviso do log passa a dizer "o jogo roda como administrador; na tela Geral, use 'Permitir
  fechar jogos de administrador'".
- A função "fechar o jogo" continua desligada de fábrica, e o ajudante é uma segunda opção, também desligada (não
  instalada) de fábrica.

## Riscos aceitos

- **Qualquer programa rodando como o usuário pode disparar a tarefa.** O Windows não deixa restringir isso só ao app. O
  pior que esse programa consegue é fechar ou encerrar um programa **elevado, com janela, do próprio usuário, aberto
  pelo Sunshine**. Na prática, um jogo ou o launcher durante a transmissão: perda de progresso, sem ganho de acesso.
  Não abre caminho para rodar código elevado nem para atingir o Windows, serviços, outros usuários ou o que o usuário
  abriu por conta própria.
- **Quem já é administrador** pode fazer tudo isso sem o ajudante; fica fora do escopo, como no `SECURITY.md`.
- **A cópia na instalação:** no instante do UAC, o ajudante é copiado da pasta do zip. Um programa malicioso que já
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
- **D2a:** o projeto do ajudante (NativeAOT, sem referência a Application nem Infrastructure), com as regras da seção 3
  numa classe pura testada no CI com processos falsos; o workflow do pacote passa a incluir o `.exe`.
- **D2b:** instalar/remover/atualizar (UAC), chamada pelo Agendador de Tarefas, ligação no `CloseGameService`, botões na
  tela Geral, `SECURITY.md` e roteiro em `docs/testes-manuais.md`.
- Os dois com Opus · esforço alto e **revisão de segurança por um agente separado** antes de mesclar.

## Alternativas consideradas

- **App inteiro como administrador:** qualquer erro da interface, do leitor do log ou do pipe passaria a ter poder total
  no PC. Contraria os ADRs 0008 e 0012. Rejeitada.
- **Serviço do Windows rodando como `SYSTEM`:** mais poder do que o necessário (SYSTEM fica acima do administrador), fica
  sempre rodando e escutando, e precisa de instalador. Rejeitada.
- **Pedir UAC a cada fechamento:** a janela do UAC aparece na área segura do Windows, ruim de responder com o controle
  pela transmissão. Rejeitada.
- **Ajudante na pasta do app (zip):** troca do `.exe` = administrador sem UAC. Rejeitada (seção 5).
- **Ajudante que encerra a árvore inteira ou recebe nome do processo:** dá mais poder a quem dispara a tarefa sem
  necessidade. Rejeitada.
- **Rodar o jogo sem administrador** (desmarcar na aba Compatibilidade, ou `RunAsInvoker`): resolve os jogos que só
  foram marcados assim, e continua sendo a primeira orientação do guia, mas não os que exigem administrador.
- **Não tratar (só o aviso):** era a decisão do ADR 0016; o autor pediu que o app cubra esses jogos.

## Consequências

- Jogos de administrador abertos pelo Sunshine passam a fechar pelo combo, depois de um UAC único.
- O app continua sem administrador; o único código elevado é um `.exe` pequeno, sem estado, numa pasta protegida.
- `SECURITY.md` ganha o ajudante no escopo, com o risco aceito acima.
- O ADR 0016 (seção 6) passa a apontar para este.
