# 0001. Stack e gatilho de sessão

- Status: aceita
- Data: 2026-11-22

## Contexto

O SunshineHelper precisa detectar o início de uma transmissão Sunshine e qual cliente conectou, sem modificar Sunshine nem Moonlight, e restaurar o ambiente de forma confiável.

## Decisão

- C# 14 em .NET 10 LTS, WPF com MVVM, arquitetura em camadas (Domain, Application, Infrastructure, UI).
- Gatilho: um único Prep Command do Sunshine chama `SunshineHelper.Trigger.exe`, que abre o app se estiver fechado, envia os dados da sessão e espera a confirmação. Se o tempo esgotar, o auxiliar termina com sucesso para nunca bloquear a transmissão. O log do Sunshine segue como confirmação e plano B.
- O app inicia com o Windows (tarefa agendada elevada).
- Aplicação de perfis com Capture/Apply/Revert por módulo, snapshot atômico e restauração após falha (`--restore`).

## Consequências

O comportamento exato das variáveis de ambiente do Sunshine e dos tempos de barreira é confirmado na Fase 0 antes do código do núcleo.
