# SunshineHelper

[![CI](https://github.com/joaovpaes/SunshineHelper/actions/workflows/ci.yml/badge.svg)](https://github.com/joaovpaes/SunshineHelper/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)

**Per-client display profiles for [Sunshine](https://github.com/LizardByte/Sunshine) game streaming.** Start a stream from your TV, phone, tablet, Steam Deck or another PC, and the host PC switches to the resolution, HDR setting and FPS cap that device needs — then puts everything back the way you had it when the session ends.

A companion to Sunshine and [Moonlight](https://moonlight-stream.org), not a replacement for either. Nothing in them is patched, bundled or reconfigured behind your back.

> **Status:** in development. Test versions (pre-releases) are on the [Releases page](https://github.com/joaovpaes/SunshineHelper/releases); they have **no installer and no code signing yet** (Windows shows a SmartScreen warning). The step-by-step setup is in [docs/preview-como-usar.md](docs/preview-como-usar.md) (Portuguese; it also ships inside the zip as `LEIA-ME.md`).

🇧🇷 **[Leia em português](#português)**

---

## The problem

Your monitor and the device you stream to rarely want the same thing. A 4K TV wants 3840×2160 with HDR on. A phone is happier with 1080p and an FPS cap that keeps the battery alive. Sitting back down at the PC afterwards, you want neither.

Changing the resolution, toggling HDR and setting an FPS cap by hand before every session gets old fast — and forgetting to undo it leaves your desk setup wrong until you notice, which is usually halfway through something else.

## What it does

- **Notices when a stream starts**, through a single Sunshine Prep Command that calls a small helper. No polling, no guessing.
- **Identifies which device connected**, by the client name Sunshine reports.
- **Applies that device's profile:** screen resolution, HDR on or off, and an FPS cap through [RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/). Each setting can follow what the client asked for, be pinned to a fixed value, or be left alone.
- **Puts everything back when the session ends** — and also after a crash, or after the PC reboots mid-session. The previous state is written to disk before anything is changed, so there is always a way back.
- **Gamepad overlay** *(planned, not built yet)* to switch profiles mid-session, adjust resolution and FPS, list and close processes, switch windows and send keyboard shortcuts, without reaching for a keyboard.

## What it does not do

- It does not modify, patch, fork or bundle Sunshine or Moonlight.
- It does not change your Sunshine configuration without asking.
- It does not change your monitor's refresh rate. Ever.
- It does not stream anything itself — Sunshine still does all the streaming.

## Setup you have to do

The app does not set these up for you:

1. **Sunshine log level:** set the minimum log level to **Debug** in Sunshine (Configuration > General) and restart Sunshine. The app reads the log to notice disconnects; the Dashboard warns if the level is too low.
2. **Sunshine log path:** in the app's **General** screen, point to your `sunshine.log`.
3. **Prep Commands:** in Sunshine, under your app's **Command Preparations**, add `SunshineHelper.Trigger.exe start` (Do) and `SunshineHelper.Trigger.exe end` (Undo).
4. **The app must be open before the stream starts** (the helper gives up after 2 s). Turn on *Start with Windows* in the **General** screen to have it start minimized in the tray.
5. **FPS cap:** RTSS must be running, and the Profiles screen offers a one-time **Repair** button (Windows asks for confirmation once) so the app can write RTSS's profile file.
6. **Resolutions above your monitor's (Super Resolution):** the app only asks Windows for the resolution, so the GPU driver has to offer it. On AMD, turn on **Virtual Super Resolution (VSR)** in AMD Software: Adrenalin Edition; on NVIDIA, enable **DSR** factors in the NVIDIA Control Panel. If the driver does not offer the resolution, the app logs the refusal and the stream goes on without it.

## Known limits

- **Sunshine's own monitor problems are not covered.** The app does not turn on, wake up or switch a monitor: it only changes resolution, HDR and FPS cap on the **primary** monitor and restores them. If Sunshine needs the monitor to stay on or active during a stream, that is up to Sunshine and Windows.
- **A monitor without HDR cannot use the HDR option.** The app refuses to turn HDR on, says so in the Dashboard and in `app.log`, and the stream goes on without HDR. Leave the profile's HDR as "leave as is" for that monitor.
- Refresh rate is never changed, and only the primary monitor is handled.
- Test versions have no installer and no code signing, and were tested only on Windows 11 with Sunshine for Windows, RTSS 7.3.x and Playnite.

## Works with

Any client [Moonlight](https://moonlight-stream.org) runs on: Android and Android TV, iOS and tvOS, Windows, macOS and Linux, Steam Deck, smart TVs, Raspberry Pi and others. Profiles are per client and set up by you, so the app is not tied to a fixed list of device types. The TV and phone in these docs are simply the devices used in the author's own testing.

## How it works

1. A single Sunshine Prep Command runs `SunshineHelper.Trigger.exe`, which tells the app a session is starting. The helper always exits successfully, so a problem on this side can never stop your stream.
2. The app matches the connecting client to a profile, writes down everything it is about to change, and applies the profile.
3. The Sunshine log is read as a second source, which is what catches a disconnect or a resumed session — Sunshine runs no command for either.
4. When the session ends, the saved state is restored. If the app or the PC died first, it is restored at the next startup instead.

The design decisions behind this live in [`docs/adr/`](docs/adr/).

## Requirements

- Windows 10 22H2 or Windows 11, x64.
- [Sunshine](https://github.com/LizardByte/Sunshine) on the host PC and [Moonlight](https://moonlight-stream.org) on the device you stream from. Neither is distributed with this project.
- [RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/) 7.3.7 or newer, for the FPS cap.
- An AMD, NVIDIA or Intel GPU.

The installer (planned) will check for Sunshine before it installs anything. If it is not there, the installation stops and points you to Sunshine's own download page, and a dedicated tutorial covers how to configure Sunshine to work with SunshineHelper.

## Already working (test versions)

Profiles per device, resolution, HDR and FPS cap with restore, a Devices screen (devices that have connected), a Log screen, start with Windows, interface in English and Portuguese, and closing the launcher at the end of a session ([Playnite](https://playnite.link) first, because it is what the author uses; other launchers are a goal for later).

## Planned

- Installer (`SunshineHelper-vX.Y.Z-setup.exe`) and code signing, with `SHA256SUMS.txt`.
- Gamepad overlay.
- Update notice and, later, automatic update.

## Related projects

- [Sunshine](https://github.com/LizardByte/Sunshine) — the self-hosted game stream host this is built around.
- [Moonlight](https://moonlight-stream.org) — the client side.
- [RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/) — RivaTuner Statistics Server, used for the FPS cap.

## Contributing

Issues and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Licensed under the [MIT License](LICENSE).

---

## Português

**Perfis de tela por aparelho para transmissões com o [Sunshine](https://github.com/LizardByte/Sunshine).** Você começa a transmitir para a TV, o celular, o tablet, o Steam Deck ou outro PC, e o PC de origem muda para a resolução, o HDR e o limite de FPS daquele aparelho — e devolve tudo como estava quando a sessão termina.

É um complemento ao Sunshine e ao [Moonlight](https://moonlight-stream.org), não um substituto para nenhum dos dois. Nada neles é alterado, embutido ou reconfigurado sem você saber.

> **Situação:** em desenvolvimento. Há versões de testes (pré-lançamentos) na [página de Releases](https://github.com/joaovpaes/SunshineHelper/releases); elas **ainda não têm instalador nem assinatura de código** (o Windows mostra o aviso do SmartScreen). O passo a passo está em [docs/preview-como-usar.md](docs/preview-como-usar.md), que também vai dentro do zip como `LEIA-ME.md`.

### O problema

Seu monitor e o aparelho para onde você transmite quase nunca querem a mesma coisa. Uma TV 4K quer 3840×2160 com HDR ligado. Um celular se dá melhor com 1080p e um limite de FPS que poupe a bateria. E quando você volta a sentar no PC, não quer nenhum dos dois.

Mudar a resolução, ligar e desligar o HDR e ajustar o limite de FPS na mão antes de cada sessão cansa rápido — e esquecer de desfazer deixa o PC errado até você perceber, normalmente no meio de outra coisa.

### O que ele faz

- **Percebe quando a transmissão começa**, por um único Prep Command do Sunshine que chama um auxiliar pequeno. Sem ficar vigiando, sem adivinhação.
- **Identifica qual aparelho conectou**, pelo nome do cliente que o Sunshine informa.
- **Aplica o perfil daquele aparelho:** resolução, HDR ligado ou desligado e limite de FPS pelo [RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/). Cada item pode seguir o que o cliente pediu, ficar num valor fixo, ou não ser mexido.
- **Devolve tudo ao fim da sessão** — e também depois de uma queda do app ou de um reinício do PC no meio. O estado anterior vai para o disco antes de qualquer mudança, então sempre existe caminho de volta.
- **Overlay pelo controle** *(planejado, ainda não feito)* para trocar de perfil no meio da sessão, ajustar resolução e FPS, listar e fechar processos, trocar de janela e enviar atalhos de teclado, sem precisar de teclado.

### O que ele não faz

- Não altera, corrige, bifurca nem distribui o Sunshine ou o Moonlight.
- Não muda a sua configuração do Sunshine sem pedir.
- Não muda a taxa de atualização do monitor. Nunca.
- Não transmite nada por conta própria — quem transmite continua sendo o Sunshine.

### O que você precisa configurar

O app não faz estas configurações por você:

1. **Nível de log do Sunshine:** coloque o nível mínimo em **Debug** no Sunshine (Configuration > General) e reinicie o Sunshine. O app lê o log para perceber desconexões; o Painel avisa se o nível estiver baixo.
2. **Caminho do log do Sunshine:** na tela **Geral** do app, aponte o seu `sunshine.log`.
3. **Prep Commands:** no Sunshine, em **Command Preparations** do seu app, adicione `SunshineHelper.Trigger.exe start` (Do) e `SunshineHelper.Trigger.exe end` (Undo).
4. **O app precisa estar aberto antes da transmissão** (o auxiliar desiste em 2 s). Ligue **Iniciar com o Windows** na tela **Geral** para ele abrir minimizado na bandeja.
5. **Limite de FPS:** o RTSS precisa estar aberto, e a tela Perfis tem um botão **Reparar** (o Windows pede confirmação uma vez) para o app poder gravar o arquivo de perfil do RTSS.
6. **Resolução maior que a do monitor (Super Resolução):** o app só pede a resolução ao Windows, então o driver da placa precisa oferecê-la. Na AMD, ligue o **Virtual Super Resolution (VSR)** no AMD Software: Adrenalin Edition; na NVIDIA, ative os fatores do **DSR** no Painel de Controle da NVIDIA. Se o driver não oferecer a resolução, o app registra a recusa e a transmissão segue sem ela.

### Limites conhecidos

- **Problemas do próprio Sunshine com o monitor não são cobertos.** O app não liga, não acorda nem troca o monitor: ele só muda resolução, HDR e limite de FPS do monitor **principal** e devolve tudo. Se o Sunshine precisa que o monitor fique ligado ou ativo durante a transmissão, isso é do Sunshine e do Windows.
- **Monitor sem HDR não usa a opção de HDR.** O app se recusa a ligar o HDR, avisa no Painel e no `app.log`, e a transmissão segue sem HDR. Deixe o HDR do perfil em "Deixar como está" nesse monitor.
- A taxa de atualização nunca é mudada, e só o monitor principal é tratado.
- As versões de testes não têm instalador nem assinatura de código, e foram testadas só em Windows 11 com o Sunshine para Windows, RTSS 7.3.x e Playnite.

### Funciona com

Qualquer cliente em que o [Moonlight](https://moonlight-stream.org) rode: Android e Android TV, iOS e tvOS, Windows, macOS e Linux, Steam Deck, TVs inteligentes, Raspberry Pi e outros. Os perfis são por cliente e configurados por você, então o app não fica preso a uma lista fixa de tipos de aparelho. A TV e o celular que aparecem nesta documentação são só os aparelhos usados nos testes do autor.

### Como funciona

1. Um único Prep Command do Sunshine roda o `SunshineHelper.Trigger.exe`, que avisa o app que uma sessão está começando. O auxiliar sempre sai com sucesso, então um problema deste lado nunca impede a sua transmissão.
2. O app encontra o perfil do cliente que conectou, anota tudo que vai mudar e aplica o perfil.
3. O log do Sunshine é lido como segunda fonte, e é o que pega uma desconexão ou uma sessão retomada — o Sunshine não roda comando nenhum nesses dois casos.
4. No fim da sessão o estado salvo é restaurado. Se o app ou o PC caiu antes, a restauração acontece na próxima inicialização.

As decisões de projeto estão em [`docs/adr/`](docs/adr/).

### Requisitos

- Windows 10 22H2 ou Windows 11, x64.
- [Sunshine](https://github.com/LizardByte/Sunshine) no PC de origem e [Moonlight](https://moonlight-stream.org) no aparelho de onde você transmite. Nenhum dos dois é distribuído por este projeto.
- [RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/) 7.3.7 ou superior, para o limite de FPS.
- Placa de vídeo AMD, NVIDIA ou Intel.

O instalador (planejado) vai verificar se o Sunshine está presente antes de instalar qualquer coisa. Se não achar, a instalação é interrompida e aponta para a página oficial do Sunshine, e há um tutorial próprio de como configurar o Sunshine para funcionar com o SunshineHelper.

### Já funciona (versões de testes)

Perfis por aparelho, resolução, HDR e limite de FPS com restauração, tela Dispositivos (aparelhos que já conectaram), tela Log, iniciar com o Windows, interface em português e inglês e fechamento do launcher ao fim da sessão (o [Playnite](https://playnite.link) vem primeiro, por ser o que o autor usa; outros launchers são objetivo futuro).

### Planejado

- Instalador (`SunshineHelper-vX.Y.Z-setup.exe`) e assinatura de código, com `SHA256SUMS.txt`.
- Overlay pelo controle.
- Aviso de versão nova e, depois, atualização automática.

### Contribuir

Issues e pull requests são bem-vindos — veja o [CONTRIBUTING.md](CONTRIBUTING.md). Licença [MIT](LICENSE).
