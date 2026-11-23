# 0010. HDR é aplicado antes da resolução

- Status: aceita
- Data: 2026-11-23

## Contexto

O ADR 0002 define que os módulos são aplicados em ordem e revertidos na ordem inversa, mas não
fixa a ordem entre vídeo e HDR. Até aqui era vídeo (1), HDR (2), FPS (3), launcher (4).

No teste manual de 2026-11-23 (RX 9070 XT, tela 2560x1440 a 180 Hz, Windows 11), a resolução do
perfil nunca ficava aplicada. O log do app (`app.log`, PR 12c) mostrou:

- o módulo de vídeo trocou para 1920x1080 e a leitura imediata confirmou;
- o HDR ligou 0,3 s depois;
- menos de 1 s depois o Sunshine já via 2560x1440, e a releitura do app aos 3 s também.

Com as ferramentas de `tools/manual-tests`, fora de qualquer transmissão: aplicar 1920x1080 e
depois ligar o HDR devolve a tela a 2560x1440. Ao trocar o HDR a tela se reconecta (fica preta e
volta), e o Windows reaplica o modo **salvo** da tela. A troca de resolução do app é de propósito
só da sessão (`ChangeDisplaySettingsEx` sem gravar no registro), então é desfeita.

## Decisão

Nova ordem: **HDR (1), vídeo (2), FPS (3), launcher (4)**. A reversão continua na ordem inversa:
launcher, FPS, vídeo e por último HDR.

O módulo de HDR só retorna depois de confirmar o novo estado (ele relê por até 5 s), ou seja,
depois de a tela se reconectar. Então a resolução aplicada em seguida não é mais desfeita.

## Consequências

- A resolução do perfil passa a valer junto com HDR. A releitura de 3 s do módulo de vídeo
  continua no `app.log` para confirmar isso em outros PCs e drivers.
- Na reversão, o HDR é desligado depois de a resolução voltar. A reconexão reaplica o modo salvo,
  que é o próprio modo do usuário, então o resultado é o mesmo. Exceção conhecida: se antes da
  transmissão a tela estivesse num modo temporário (não salvo, feito por outro programa), a volta
  fica no modo salvo. É raro e não piora em relação a antes.
- Um snapshot gravado por uma versão anterior guarda a ordem antiga de cada módulo e é revertido
  nessa ordem. Isso continua correto, porque a ordem gravada é a que foi usada para aplicar.
- `ModuleOrderTests` (Infrastructure.Tests) protege a ordem contra uma troca acidental.
