# SunshineHelper

**Português** | [English](#english)

Aplicativo para Windows que, **sem modificar o Sunshine nem o Moonlight**, percebe quando uma transmissão começa, identifica qual aparelho conectou (por exemplo, TV ou celular) e aplica um perfil para ele: resolução, HDR e limite de FPS (via RTSS). Ao terminar, restaura o estado anterior. Um overlay acionado pelo controle permite trocar de perfil, ajustar resolução/FPS, listar e encerrar processos, trocar de janela e enviar atalhos de teclado.

> **Status:** em planejamento e estruturação. Ainda não há versão publicada.

## Como funciona (resumo)

- Um único Prep Command do Sunshine chama o `SunshineHelper.Trigger.exe`, que avisa o app (o app inicia com o Windows).
- O log do Sunshine serve de confirmação e plano B.
- Cada aparelho tem seu perfil; o estado anterior é salvo antes de qualquer mudança e restaurado ao fim, mesmo após falha.

## Requisitos previstos

- Windows 10 22H2 ou Windows 11 (piso a confirmar), x64.
- Sunshine e Moonlight instalados (não são distribuídos por este projeto).
- RTSS (RivaTuner Statistics Server) 7.3.7 ou superior, para o limite de FPS.
- Placas AMD, NVIDIA ou Intel; interface em português e inglês.

## Distribuição prevista

- Instalador `SunshineHelper-vX.Y.Z-setup.exe`, que baixa dependências quando necessário.
- Versão portátil `SunshineHelper-vX.Y.Z-portable-win-x64.zip`.
- Arquivo `SHA256SUMS.txt` para conferir os downloads.

## Contribuir

Veja [CONTRIBUTING.md](CONTRIBUTING.md). Licença: [MIT](LICENSE).

---

## English

Windows app that, **without modifying Sunshine or Moonlight**, detects when a stream starts, identifies which client connected (e.g. TV or phone) and applies a per-device profile: resolution, HDR and FPS cap (via RTSS). When the session ends it restores the previous state. A gamepad-driven overlay lets you switch profiles, adjust resolution/FPS, list and kill processes, switch windows and send keyboard shortcuts.

> **Status:** planning and scaffolding. No release yet.

- A single Sunshine Prep Command calls `SunshineHelper.Trigger.exe`, which notifies the app (the app starts with Windows). The Sunshine log is used as confirmation and fallback.
- Planned requirements: Windows 10 22H2 / Windows 11 x64, Sunshine and Moonlight (not bundled), RTSS 7.3.7+ for the FPS cap, AMD/NVIDIA/Intel GPUs. UI in Portuguese and English.
- Planned downloads: installer (downloads dependencies if needed) and a portable `.zip`, with `SHA256SUMS.txt`.

See [CONTRIBUTING.md](CONTRIBUTING.md). Licensed under [MIT](LICENSE).
