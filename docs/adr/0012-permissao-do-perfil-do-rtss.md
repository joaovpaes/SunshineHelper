# 0012. Permissão de escrita só no perfil global do RTSS (o app não é elevado)

- Status: aceita (15b-1; a tela com o botão "Reparar" vem no 15b-2)
- Data: 2026-11-23
- Substitui: 0002 quanto à elevação do app (a linha "elevado, inicia no logon por tarefa agendada")

## Contexto

O limite de FPS é aplicado gravando a propriedade `FramerateLimit` do perfil global do RTSS
(`SaveProfile`), que fica em `<pasta do RTSS>\Profiles\Global`, dentro de Program Files. Sem
administrador o Windows nega essa gravação. O ADR 0002 previa o app inteiro elevado por tarefa
agendada, mas desde a decisão D1 (`docs/fase1-plano.md`) o app roda **sem** administrador, e hoje o
módulo de FPS falha e vira aviso (ADR 0011).

O teste do 15a, na máquina do autor (2026-11-23), mostrou que mudar o limite só em memória
(`SetProfileProperty` + `UpdateProfiles`, sem `SaveProfile`) não serve: o RTSS aceita e relê o valor,
mas o jogo continua sem limite, e o arquivo não muda. O RTSS só aplica o que está gravado.

## Decisão

1. **O app continua sem administrador.** Só uma ação pontual é elevada.
2. **Permissão mínima:** a conta do usuário (pelo SID) recebe leitura e escrita, `(R,W)`, **só no
   arquivo** `Profiles\Global`. Nada na pasta do RTSS e nada herdado.
3. **Quem concede:** o `icacls.exe` do próprio Windows (`%SystemRoot%\System32`), iniciado com
   `runas` (janela do UAC, uma vez). Nenhum código nosso roda elevado. A linha de comando só é
   montada se o caminho for absoluto, terminar em `Profiles\Global` e não tiver aspas, e se o SID
   tiver só `S`, dígitos e hífens (`RtssProfileAccess.BuildIcaclsArguments`).
4. **Detecção:** o módulo confere se consegue abrir esse arquivo para escrita (sem gravar nada)
   antes de aplicar ou reverter. Sem o arquivo (RTSS que nunca salvou o perfil), volta ao teste
   antigo de criar um arquivo temporário na pasta. Se o RTSS for reinstalado e a permissão sumir, a
   conferência volta a falhar e o app avisa de novo.
5. **Onde o usuário concede:** no 15b-1, pela opção 7 da ferramenta `tools/manual-tests/rtss-module`
   (para validar a decisão). No 15b-2, por um botão "Reparar" na tela Perfis, que aparece quando um
   perfil mexe no FPS e falta a permissão. Quando houver instalador, ele (que já roda como
   administrador) concede o mesmo, e o botão fica para o caso de reinstalação do RTSS.

## Alternativas consideradas

- **App inteiro elevado por tarefa agendada (ADR 0002):** a interface toda com privilégio alto por
  causa de um arquivo. Rejeitada.
- **Limite só em memória:** testado no 15a, o RTSS não aplica. Rejeitada.
- **Permissão de escrita na pasta do RTSS:** resolveria até um RTSS que grava em arquivo temporário e
  renomeia, mas permitiria trocar o `RTSSHooks64.dll`, que o RTSS injeta nos jogos: qualquer programa
  do usuário viraria código rodando em outros processos, inclusive elevados. Rejeitada.
- **Permissão `(M)` (modificar) no arquivo:** inclui apagar o arquivo, que não é necessário.

## Consequências

- O FPS funciona sem administrador depois de um único UAC.
- Risco residual: qualquer programa rodando como o usuário pode alterar o perfil global do RTSS
  (limite de FPS, OSD e outras opções do RTSS). Não dá acesso a outros arquivos nem a execução de
  código elevado. Fica registrado no `SECURITY.md` como parte do escopo "arquivos que o app grava".
- **Suposição confirmada no PC do autor (2026-11-23, PR 15b-1):** o RTSS regrava `Profiles\Global` no mesmo lugar.
  Se ele
  gravar um arquivo novo e renomear, a permissão só no arquivo não basta; aí esta decisão é revista
  (sem passar a dar permissão na pasta, pelo motivo acima).
