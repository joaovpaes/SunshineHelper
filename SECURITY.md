# Security Policy / Política de segurança

🇧🇷 **[Leia em português](#português)**

## Supported versions

SunshineHelper is in development and has no release yet. Until the first release, only the latest commit on
`main` is supported. After that, only the latest release will be.

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Use GitHub's private reporting instead: open the **Security** tab of this repository, choose
**Report a vulnerability**, and describe what you found. Only the maintainer can see the report.

Please include what you did, what you expected and what happened, the app version, and your Windows version.
Do **not** attach raw Sunshine logs (see below); the app's *diagnostic report* already redacts them.

This is a hobby project maintained by one person, so there is no guaranteed response time. Reports are read
and answered as soon as possible, and a confirmed problem is fixed in a pull request before it is disclosed.

## What is in scope

SunshineHelper runs on your PC with your user's rights and changes system settings, so these are the areas
where a vulnerability matters most:

- **The local channel** (named pipe `SunshineHelper.v1`) that the Sunshine Prep Command uses to start and end
  a session. Its access is limited to the interactive user, `SYSTEM` and Administrators, and messages are
  size-limited. Anything that lets another account send commands through it, or crash the app, is in scope.
- **Closing processes** (the launcher module). The app only closes process names the user wrote in the
  settings, and only ones that were opened during the session. Anything that makes it close something else
  is in scope.
- **Files the app writes**: the session snapshot and `settings.json` in `%LOCALAPPDATA%\SunshineHelper`.
  Anything that lets one user tamper with another's, or that makes a restore apply attacker-chosen values,
  is in scope.
- **The RTSS profile permission** (ADR 0012). To set an FPS cap without running as administrator, the app
  can ask Windows (UAC, once) to run `icacls` and give your account read and write on RTSS's
  `Profiles\Global` file only. Anything that makes that grant reach another file or folder, or run
  something else elevated, is in scope. Known and accepted: any program running as you can then edit
  RTSS's global profile.
- **Closing games that run as administrator** (ADR 0017). Off until the user clicks "Allow" on the General
  screen (UAC, once): the auxiliary `SunshineHelper.Trigger.exe` is copied to `%ProgramFiles%\SunshineHelper`
  and a scheduled task "SunshineHelper Close Elevated Game" runs it with the user's highest privileges, on
  demand only. In that elevated mode it closes or ends one process, and only one that is elevated, belongs to
  the same user and session, has a visible window and descends from a `sunshine.exe` running as `SYSTEM` or
  elevated. It writes nothing and reads no settings. Anything that makes it act on another process, run
  other code elevated, or that lets the installed copy be replaced without administrator, is in scope. Known
  and accepted: any program running as you can start the task and close such a game, and can change the
  auxiliary next to the app before you click "Allow" or "Update" (as with any program you approve in UAC;
  code signing is on the backlog).
- **The diagnostic report**, which must never leak IPs, MAC addresses, Sunshine keys or the Windows user name.

Out of scope: vulnerabilities in Sunshine, Moonlight, RTSS or Windows themselves (report those to their own
projects), and anything that needs an attacker who already has administrator rights on the PC.

## Sunshine logs contain secrets

Sunshine logs include `rikey`, `uniqueid`, IP addresses and MAC addresses. Never paste them raw into an issue,
a pull request or a test fixture. Replace those values with markers first (`<REDACTED>`, `192.0.2.x`), or use
the app's diagnostic report, which does it for you. Still read the file before sharing it.

---

## Português

### Versões com suporte

O SunshineHelper está em desenvolvimento e ainda não tem versão publicada. Até a primeira versão, só o último
commit do `main` tem suporte. Depois, só a versão mais recente.

### Como relatar uma vulnerabilidade

**Não abra uma issue pública para um problema de segurança.**

Use o relato privado do GitHub: na aba **Security** deste repositório, escolha **Report a vulnerability** e
descreva o que encontrou. Só o mantenedor vê o relato.

Informe o que você fez, o que esperava e o que aconteceu, a versão do app e a versão do Windows. **Não anexe**
logs do Sunshine sem tratar (veja abaixo); o *relatório de diagnóstico* do app já os redige.

É um projeto pessoal mantido por uma pessoa, então não há prazo garantido. Os relatos são lidos e respondidos
o quanto antes, e um problema confirmado é corrigido em um pull request antes de ser divulgado.

### O que está no escopo

O SunshineHelper roda no seu PC, com os direitos do seu usuário, e muda configurações do sistema. Por isso
importam principalmente:

- **O canal local** (named pipe `SunshineHelper.v1`) que o Prep Command do Sunshine usa para iniciar e
  encerrar a sessão. O acesso é limitado ao usuário interativo, a `SYSTEM` e a Administradores, e as mensagens
  têm tamanho máximo. Qualquer forma de outra conta mandar comandos por ele, ou derrubar o app, está no escopo.
- **Fechamento de processos** (módulo do launcher). O app só fecha nomes que o usuário escreveu nas
  configurações, e só processos que abriram durante a sessão. Qualquer forma de fechar outra coisa está no
  escopo.
- **Arquivos que o app grava**: o snapshot da sessão e o `settings.json` em
  `%LOCALAPPDATA%\SunshineHelper`. Qualquer forma de um usuário alterar os de outro, ou de fazer a restauração
  aplicar valores escolhidos por um atacante, está no escopo.
- **A permissão no perfil do RTSS** (ADR 0012). Para limitar o FPS sem rodar como administrador, o app
  pode pedir ao Windows (UAC, uma vez) que rode o `icacls` e dê à sua conta leitura e escrita **só** no
  arquivo `Profiles\Global` do RTSS. Qualquer forma de essa permissão chegar a outro arquivo ou pasta,
  ou de rodar outra coisa como administrador, está no escopo. Conhecido e aceito: qualquer programa
  rodando como você passa a poder alterar o perfil global do RTSS.
- **Fechar jogos que rodam como administrador** (ADR 0017). Desligado até o usuário clicar em "Permitir" na
  tela Geral (UAC, uma vez): o auxiliar `SunshineHelper.Trigger.exe` é copiado para
  `%ProgramFiles%\SunshineHelper` e uma tarefa agendada "SunshineHelper Close Elevated Game" o roda com os
  privilégios mais altos do usuário, só sob demanda. Nesse modo elevado ele fecha ou encerra um processo, e só
  um que esteja elevado, seja do mesmo usuário e sessão, tenha janela visível e descenda de um `sunshine.exe`
  rodando como `SYSTEM` ou elevado. Não grava nada nem lê configurações. Qualquer forma de fazê-lo agir em
  outro processo, rodar outro código como administrador, ou de trocar a cópia instalada sem administrador,
  está no escopo. Conhecido e aceito: qualquer programa rodando como você pode disparar a tarefa e fechar um
  jogo assim, e pode trocar o auxiliar ao lado do app antes de você clicar em "Permitir" ou "Atualizar" (como
  qualquer programa que você aprova no UAC; a assinatura de código está no backlog).
- **O relatório de diagnóstico**, que nunca pode vazar IPs, MACs, chaves do Sunshine nem o nome de usuário do
  Windows.

Fora do escopo: vulnerabilidades do Sunshine, do Moonlight, do RTSS ou do próprio Windows (relate aos
respectivos projetos) e qualquer coisa que exija um atacante que já tenha direitos de administrador no PC.

### Logs do Sunshine têm segredos

Os logs do Sunshine incluem `rikey`, `uniqueid`, endereços IP e MAC. Nunca cole esses logs crus em issue, pull
request ou fixture de teste. Troque esses valores por marcadores antes (`<REDACTED>`, `192.0.2.x`) ou use o
relatório de diagnóstico do app, que faz isso. Mesmo assim, leia o arquivo antes de compartilhar.
