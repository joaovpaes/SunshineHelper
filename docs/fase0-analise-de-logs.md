# Fase 0: análise dos logs do Sunshine

Fonte: logs de 21 e 22/11/2026 (Sunshine 2026.914.233613, Debug) e os quatro `.bat` atuais. Os trechos redigidos estão em `tests/fixtures/sunshine/`.

## O que os logs confirmam

| Fato | Evidência |
| --- | --- |
| Os Prep Commands rodam em sequência, só depois da reenumeração de encoders, e o cliente só conecta depois do último | `/launch` → Do 1 em +1,2 s → Do 2 em +11,5 s → app em +24,7 s → `CLIENT CONNECTED` em +24,9 s (TV) e +25,6 s (celular) |
| Quase todo o tempo de espera vem dos `timeout` fixos dos `.bat` | `Display_ON` leva ~10,3 s (8 s de `timeout`); `RTSS_ON` leva ~13,1 s (13 s de `timeout`); o trabalho real é de poucos décimos de segundo a ~2 s |
| O desfazer também é sequencial e longo | `/cancel` → `RTSS_OFF` (+0,2 s, 13,3 s) → `Display_OFF` (10,0 s); total ~23,5 s |
| `/resume` não executa os Prep Commands | celular: `/resume` em 09:09:48 reconectou em 1,3 s sem nenhum `Executing Do Cmd` |
| Desconectar simples não dispara o Undo | celular: `CLIENT DISCONNECTED` + `Session ended`, sem `Executing Undo Cmd`; o Undo só roda no `/cancel` |
| "Desconectar e fechar" gera `/cancel` e depois o Undo | TV: `/cancel` → `Session ended` (+0,09 s) → `Executing Undo Cmd` (+0,23 s) |
| A resolução 4K funciona no monitor de 1440p | `Desktop resolution [3840x2160]` após `Display_ON` (resolução virtual) com o painel a 180 Hz |
| O log não traz o nome do cliente | `/launch` mostra só `mode`, `hdrMode`, `appid`, `sops`, áudio e `uuid` da sessão; o certificado é sempre "NVIDIA GameStream Client"; o `uniqueid` é o mesmo nos dois aparelhos |

## Como distinguir TV de celular (só pelo log)

| Sinal | TV (Xbox) | Celular |
| --- | --- | --- |
| `mode` no `/launch` | 3840x2160x60 | 3840x2160x60 |
| `hdrMode` no `/launch` | 1 | ausente |
| Cor | HDR (Rec. 2020 + PQ), 59,94 fps | SDR (Rec. 709), 60 fps |
| Bitrate | ~78 Mbps | ~31 Mbps |
| IP do controle | 192.0.2.20 (nos logs originais, rede local) | 192.0.2.30 |

Conclusões:
1. Resolução e FPS pedidos são iguais; só o HDR e o bitrate diferem. Uma TV que peça SDR seria confundida com o celular.
2. IP serve, mas muda sem reserva de DHCP. Para o app, o aparelho vira uma regra configurável: nome do cliente (se existir), IP ou faixa de IPs, e características do pedido (HDR, resolução, FPS, bitrate), com um assistente "Detectar este aparelho" que aprende na primeira conexão.
3. A documentação oficial do Sunshine lista só `SUNSHINE_CLIENT_WIDTH`, `SUNSHINE_CLIENT_HEIGHT`, `SUNSHINE_CLIENT_FPS` e `SUNSHINE_CLIENT_HDR` para os comandos. Nome do cliente **não** consta: tratar como inexistente até o teste 1 abaixo.

## Testes na máquina do usuário (a fazer)

1. **Variáveis de ambiente:** um `.bat` temporário como Prep Command que grave `set SUNSHINE` e `set` completo em arquivo, conectando pela TV e pelo celular. Decide se existe nome ou ID de cliente.
2. **Tempo real de cada passo** sem `timeout`: troca de resolução, HDR ligado e desligado, reinício do RTSS até estar pronto (poll do processo e da janela), restauração.
3. **Barreira:** com o auxiliar esperando 5, 15 e 30 s, ver até quanto o Moonlight da TV e do celular aguardam antes de desistir.
4. **Monitor capturado:** confirmar qual monitor o Sunshine captura (`output_name` vazio = principal) e o identificador estável do AOC Q27G4 para a API de vídeo.
5. **HDR por API:** o `HDRSwitch.exe -m 0` usa o monitor 0; validar a chamada nativa equivalente (DisplayConfig) e o comportamento com HDR do Windows ligado e cliente SDR (o celular hoje recebe SDR com o HDR do Windows ligado).
6. **RTSS:** copiar o perfil `Global` e reiniciar o RTSS basta? Medir o tempo até o limite valer e checar licença e link oficial para o instalador.
7. **Piso do Windows e dependências nativas:** confirmar Windows 10 22H2 como mínimo e se alguma biblioteca nativa exige o VC++ Redistributable.

## Decisões que esta análise sustenta

- O trabalho real do `.bat` cabe em poucos segundos; o app deve substituir esperas fixas por condições (processo pronto, resolução aplicada), reduzindo a espera de ~25 s para o tempo medido no teste 2.
- Manter o gatilho único (Prep Command chamando o auxiliar), pois só ele bloqueia o início do stream até o app terminar de aplicar; o log confirma e serve de plano B.
- `/resume` sem Do Cmd obriga o app a observar o log (ou o auxiliar de retomada) para reaplicar o perfil quando o ambiente foi restaurado.
- A identificação do aparelho precisa ser configurável e não pode depender só de IP.
