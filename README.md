# SunshineHelper

[![CI](https://github.com/joaovpaes/SunshineHelper/actions/workflows/ci.yml/badge.svg)](https://github.com/joaovpaes/SunshineHelper/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)

**Per-client display profiles for [Sunshine](https://github.com/LizardByte/Sunshine) game streaming.** Start a stream from your TV, phone, tablet, Steam Deck or another PC, and the host PC switches to the resolution, HDR setting and FPS cap that device needs — then puts everything back the way you had it when the session ends.

A companion to Sunshine and [Moonlight](https://moonlight-stream.org), not a replacement for either. Nothing in them is patched, bundled or reconfigured behind your back.

> **Status:** in development, no release yet. The core is being built in the open; see [docs/](docs/).

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
- **Gamepad overlay** to switch profiles mid-session, adjust resolution and FPS, list and close processes, switch windows and send keyboard shortcuts, without reaching for a keyboard.

## What it does not do

- It does not modify, patch, fork or bundle Sunshine or Moonlight.
- It does not change your Sunshine configuration without asking.
- It does not change your monitor's refresh rate. Ever.
- It does not stream anything itself — Sunshine still does all the streaming.

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

The installer checks for Sunshine before it installs anything. If it is not there, the installation stops and points you to Sunshine's own download page, and a dedicated tutorial covers how to configure Sunshine to work with SunshineHelper.

## Planned

- Installer (`SunshineHelper-vX.Y.Z-setup.exe`) and a portable `.zip`, both with `SHA256SUMS.txt`.
- Interface in English and Portuguese.
- Closing the launcher at the end of a session. [Playnite](https://playnite.link) is supported first, because it is what the author uses; other launchers are a goal for later.

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

> **Situação:** em desenvolvimento, ainda sem versão publicada.

### O problema

Seu monitor e o aparelho para onde você transmite quase nunca querem a mesma coisa. Uma TV 4K quer 3840×2160 com HDR ligado. Um celular se dá melhor com 1080p e um limite de FPS que poupe a bateria. E quando você volta a sentar no PC, não quer nenhum dos dois.

Mudar a resolução, ligar e desligar o HDR e ajustar o limite de FPS na mão antes de cada sessão cansa rápido — e esquecer de desfazer deixa o PC errado até você perceber, normalmente no meio de outra coisa.

### O que ele faz

- **Percebe quando a transmissão começa**, por um único Prep Command do Sunshine que chama um auxiliar pequeno. Sem ficar vigiando, sem adivinhação.
- **Identifica qual aparelho conectou**, pelo nome do cliente que o Sunshine informa.
- **Aplica o perfil daquele aparelho:** resolução, HDR ligado ou desligado e limite de FPS pelo [RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/). Cada item pode seguir o que o cliente pediu, ficar num valor fixo, ou não ser mexido.
- **Devolve tudo ao fim da sessão** — e também depois de uma queda do app ou de um reinício do PC no meio. O estado anterior vai para o disco antes de qualquer mudança, então sempre existe caminho de volta.
- **Overlay pelo controle** para trocar de perfil no meio da sessão, ajustar resolução e FPS, listar e fechar processos, trocar de janela e enviar atalhos de teclado, sem precisar de teclado.

### O que ele não faz

- Não altera, corrige, bifurca nem distribui o Sunshine ou o Moonlight.
- Não muda a sua configuração do Sunshine sem pedir.
- Não muda a taxa de atualização do monitor. Nunca.
- Não transmite nada por conta própria — quem transmite continua sendo o Sunshine.

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

O instalador verifica se o Sunshine está presente antes de instalar qualquer coisa. Se não achar, a instalação é interrompida e aponta para a página oficial do Sunshine, e há um tutorial próprio de como configurar o Sunshine para funcionar com o SunshineHelper.

### Planejado

- Instalador (`SunshineHelper-vX.Y.Z-setup.exe`) e versão portátil em `.zip`, os dois com `SHA256SUMS.txt`.
- Interface em português e inglês.
- Fechamento do launcher ao fim da sessão. O [Playnite](https://playnite.link) vem primeiro, por ser o que o autor usa; outros launchers são objetivo futuro.

### Contribuir

Issues e pull requests são bem-vindos — veja o [CONTRIBUTING.md](CONTRIBUTING.md). Licença [MIT](LICENSE).
