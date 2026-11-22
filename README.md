# SunshineHelper

**Português** | [English](#english)

Aplicativo para Windows que, **sem modificar o Sunshine nem o Moonlight**, percebe quando uma transmissão começa, identifica qual aparelho conectou e aplica um perfil para ele: resolução, HDR e limite de FPS (via RTSS). Ao terminar, restaura o estado anterior. Um overlay acionado pelo controle permite trocar de perfil, ajustar resolução/FPS, listar e encerrar processos, trocar de janela e enviar atalhos de teclado.

Funciona com **qualquer aparelho que o Moonlight suporte** (TV, celular, tablet, Steam Deck, outro PC, etc.) — os perfis são por cliente, configuráveis pelo usuário, e não ficam limitados a um conjunto fixo de tipos de aparelho. Os exemplos neste repositório (TV e celular) vêm só da configuração de testes do autor.

Hoje o fechamento de launcher é feito para o **Playnite** (o launcher usado pelo autor nos testes da Fase 0); dar suporte a outros launchers é um objetivo futuro do projeto.

> **Status:** em planejamento e estruturação. Ainda não há versão publicada.

## Como funciona (resumo)

- Um único Prep Command do Sunshine chama o `SunshineHelper.Trigger.exe`, que avisa o app (o app inicia com o Windows).
- O log do Sunshine serve de confirmação e plano B.
- Cada aparelho tem seu perfil, identificado pelo nome do cliente que o Sunshine informa (com outras pistas como plano B); o estado anterior é salvo antes de qualquer mudança e restaurado ao fim, mesmo após falha.

## Requisitos previstos

- Windows 10 22H2 ou Windows 11 (piso a confirmar), x64.
- Sunshine e Moonlight instalados (não são distribuídos por este projeto). Qualquer aparelho suportado pelo Moonlight pode ter seu próprio perfil.
- RTSS (RivaTuner Statistics Server) 7.3.7 ou superior, para o limite de FPS.
- Placas AMD, NVIDIA ou Intel; interface em português e inglês.
- Fechamento de launcher: hoje só Playnite; outros launchers estão nos planos futuros.

## Distribuição prevista

- Instalador `SunshineHelper-vX.Y.Z-setup.exe`, que baixa dependências quando necessário.
- Versão portátil `SunshineHelper-vX.Y.Z-portable-win-x64.zip`.
- Arquivo `SHA256SUMS.txt` para conferir os downloads.

## Contribuir

Veja [CONTRIBUTING.md](CONTRIBUTING.md). Licença: [MIT](LICENSE).

---

## English

Windows app that, **without modifying Sunshine or Moonlight**, detects when a stream starts, identifies which client connected and applies a per-device profile: resolution, HDR and FPS cap (via RTSS). When the session ends it restores the previous state. A gamepad-driven overlay lets you switch profiles, adjust resolution/FPS, list and kill processes, switch windows and send keyboard shortcuts.

Works with **any device Moonlight supports** (TV, phone, tablet, Steam Deck, another PC, etc.) — profiles are per client and user-configurable, not limited to a fixed set of device types. The examples in this repo (TV and phone) come only from the author's own test setup.

Launcher closing currently targets **Playnite** (the launcher used in the author's Phase 0 tests); supporting other launchers is a planned future goal.

> **Status:** planning and scaffolding. No release yet.

- A single Sunshine Prep Command calls `SunshineHelper.Trigger.exe`, which notifies the app (the app starts with Windows). The Sunshine log is used as confirmation and fallback.
- Each device gets its own profile, matched by the client name Sunshine reports (with other signals as a fallback).
- Planned requirements: Windows 10 22H2 / Windows 11 x64, Sunshine and Moonlight (not bundled, any Moonlight-supported device), RTSS 7.3.7+ for the FPS cap, AMD/NVIDIA/Intel GPUs. UI in Portuguese and English. Launcher closing: Playnite only for now, other launchers planned.
- Planned downloads: installer (downloads dependencies if needed) and a portable `.zip`, with `SHA256SUMS.txt`.

See [CONTRIBUTING.md](CONTRIBUTING.md). Licensed under [MIT](LICENSE).
