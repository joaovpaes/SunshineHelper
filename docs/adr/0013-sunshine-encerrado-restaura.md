# 0013. Restaurar sozinho quando o Sunshine encerra no meio da sessão

- Status: aceita (testada no PC do autor em 2026-11-23, PR 51)
- Data: 2026-11-23

## Contexto

O ambiente só volta quando o Sunshine roda o Undo Command (ou quando o leitor do log vê o fim da sessão). Se o
Sunshine for finalizado, travar ou reiniciar no meio de uma transmissão, o Undo nunca roda: a tela fica em
1920x1080 com HDR, o limite de FPS fica gravado no RTSS e o launcher fica fechado até a próxima transmissão (que
restaura a sessão presa antes, ADR 0011) ou até o app ser reaberto (verificação de inicialização, ADR 0002). Isso
foi visto no teste do PR 13b, finalizando o `sunshinesvc.exe` pelo Gerenciador de Tarefas.

No Windows, o serviço `sunshinesvc.exe` inicia o `sunshine.exe`, que roda como SISTEMA (conferido no PC do autor
em 2026-11-23). O SunshineHelper não é elevado (ADR 0012) e não deve abrir um processo do sistema.

## Decisão

- Um vigia (`SunshineProcessWatchdog`, Application) roda a cada 2 s pela vida inteira do app, ligado ou não o
  caminho do log.
- Sem snapshot no disco, ele só confere se o arquivo existe. Com snapshot, lê o id da sessão e pede ao Windows os
  PIDs dos processos `sunshine.exe` (`SunshineProcessProbe`, Infrastructure, só lê a lista de processos; não abre
  o processo, não precisa de administrador).
- Na primeira verificação de cada sessão, anota os PIDs vistos. Se nenhum desses PIDs aparecer em **2 verificações
  seguidas** (cerca de 4 s), pede o fim da sessão com o motivo `sunshine-exited` (restauração completa, como o
  `/cancel`, inclusive o launcher).
- Compara por **PID**, não por nome: um Sunshine que reiniciou sozinho tem outro PID, e a transmissão que ele
  servia acabou do mesmo jeito.
- O pedido passa pela mesma fila do canal (`TriggerChannelHandler.EndSessionIfCurrentAsync`), então nunca roda ao
  mesmo tempo que um início ou fim de sessão. Quando chega a vez, só restaura se o snapshot ainda for **da mesma
  sessão**; se uma sessão nova já tomou o lugar (o início restaura a presa, ADR 0011), não faz nada.
- **Travas:** se nenhum `sunshine.exe` estiver rodando quando a sessão é vista (um fork com outro nome, por exemplo),
  o vigia não restaura nada naquela sessão e escreve um aviso no `app.log` uma vez. Uma tentativa por sessão: se a
  restauração falhar, o snapshot fica como `RevertFailed`, o Painel mostra "Atenção" e o vigia não repete. Um erro
  numa verificação vai para o `app.log` e o vigia segue.

## Alternativas consideradas

- **Vigiar pelo nome** (restaurar quando não houver `sunshine.exe`): um reinício rápido do Sunshine passaria
  despercebido e a sessão morta ficaria aplicada. Rejeitada.
- **Esperar o processo terminar** (`WaitForExit` ou handle do processo): exige abrir um processo do SISTEMA, que um
  usuário comum normalmente não pode; seria preciso elevar o app. Rejeitada pelo ADR 0012.
- **Vigiar o serviço `SunshineService`** pelo gerenciador de serviços: não cobre um `sunshine.exe` que cai e é
  reiniciado pelo próprio serviço, nem o Sunshine rodando fora do serviço. Rejeitada.
- **Só pelo log do Sunshine:** quando o processo morre, ele não escreve o fim da sessão. Não resolve.
- **Uma opção na tela para desligar:** sem caso de uso hoje; exigiria mudar o `settings.json`. Fica para depois,
  se alguém pedir.

## Consequências

- Se o Sunshine encerrar no meio da transmissão, o ambiente volta sozinho em cerca de 4 a 6 s, sem reabrir nada.
- Custo: fora de transmissão, uma consulta de existência de arquivo a cada 2 s; durante a transmissão, mais uma
  leitura da lista de processos (poucos milissegundos). Nada toca o jogo nem o Sunshine.
- O nome `sunshine` fica fixo no código (é o executável do próprio Sunshine, não algo do PC do autor). Um fork com
  outro nome só perde a restauração automática; o resto funciona como antes.
- O motivo `sunshine-exited` é só interno; o auxiliar de gatilho e o protocolo do pipe não mudam.
