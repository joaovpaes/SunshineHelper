# Fixtures de log do Sunshine

Trechos de `sunshine.log` (nível Debug), versão 2026.914.233613, já **redigidos**: IPs viraram `192.0.2.x`, `uniqueid`, `rikey` e `uuid` viraram `<REDACTED>`, usuário do Windows virou `<USER>`. Só foram mantidas as linhas relevantes para detecção (rotas HTTPS, parâmetros de `/launch`, comandos Do/Undo, conexão e desconexão do cliente, resolução, bitrate, cor).

| Arquivo | Cenário |
| --- | --- |
| `tv-hdr-session.log` | TV (Xbox): `/launch` com `hdrMode`, 3840x2160x60, HDR, ~78 Mbps; termina com `/cancel` (sem `CLIENT DISCONNECTED`) |
| `phone-disconnect-resume-cancel.log` | Celular: SDR, ~31 Mbps; Desconectar simples, `/resume`, Desconectar de novo e `/cancel` |

Antes de adicionar um log novo, redija os segredos e confira com uma busca por `192.168`, `rikey` e `uniqueid` (e pelo nome de usuário do Windows) antes de commitar.
