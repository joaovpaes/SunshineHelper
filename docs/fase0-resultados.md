# Fase 0: resultados dos testes na máquina do autor

Testes executados em 22/11/2026, Windows 11 Pro 26H2 (26300.9974), Sunshine 2026.914.233613, RTSS 7.3.7, monitor único AOC Q27G4 (2560x1440 @ 180 Hz). Scripts em `tools/phase0/`.

## Resumo

| Teste | Resultado | Efeito no projeto |
| --- | --- | --- |
| 1. Variáveis do Prep Command | **`SUNSHINE_CLIENT_NAME` existe** | Identificação do aparelho resolvida; não depende de IP |
| 2. Tempo real de cada passo | Trabalho real ≈ 2,5 s contra 23,4 s dos `.bat` | Esperas por condição no lugar de `timeout` fixo |
| 3. Barreira (espera de 45 s) | Celular desiste aos ~57 s; TV aguarda 70 s | Orçamento de tempo da barreira definido |
| 4. Monitor capturado | Um monitor, `DISPLAY1`, `device_id` estável | Identificação do monitor pela API |
| Fechamento do Playnite | Mudou de comportamento; causa ainda indefinida | Precisa de um teste A/B |

## Teste 1: o Sunshine entrega o nome do cliente (o achado principal)

A documentação oficial lista apenas largura, altura, FPS e HDR. **Esta versão entrega bem mais**, incluindo o nome que o aparelho tem no Sunshine:

| Variável | Celular | TV (Xbox) |
| --- | --- | --- |
| `SUNSHINE_CLIENT_NAME` | `Celular` | `XBOX` |
| `SUNSHINE_CLIENT_WIDTH` × `HEIGHT` | 3840 × 2160 | 3840 × 2160 |
| `SUNSHINE_CLIENT_FPS` | 60 | 60 |
| `SUNSHINE_CLIENT_HDR` | `false` | `true` |
| `SUNSHINE_CLIENT_GCMAP` | 0 | 1 |
| `SUNSHINE_CLIENT_ENABLE_SOPS` | `true` | `false` |
| `SUNSHINE_CLIENT_AUDIO_CONFIGURATION` | 2.0 | 5.1 |
| `SUNSHINE_CLIENT_HOST_AUDIO` | `false` | `false` |
| `SUNSHINE_APP_ID` / `SUNSHINE_APP_NAME` | 1942687974 / Playnite | iguais |

Consequências:

1. **O perfil passa a ser por nome de cliente.** O auxiliar de gatilho lê `SUNSHINE_CLIENT_NAME` e manda ao app; o app casa com o perfil. Sem heurística de IP, sem depender do log.
2. **O HDR passa a seguir o pedido do cliente.** Hoje o `Display_ON.bat` liga HDR sempre, inclusive para o celular, que pede `HDR=false`. O app usa a variável e só liga quando o cliente quer.
3. **Resolução e FPS vêm prontos** (`WIDTH`, `HEIGHT`, `FPS`), e o perfil pode escolher entre seguir o cliente ou forçar valores próprios.
4. **Plano B continua necessário.** Forks e versões antigas podem não ter `SUNSHINE_CLIENT_NAME`. A ordem de identificação fica: nome do cliente → regra do perfil (HDR, resolução, FPS, áudio) → IP → perguntar na primeira conexão.
5. O áudio 5.1 contra 2.0 distingue os aparelhos mesmo sem o nome, e confirma a decisão de deixar troca de áudio fora da v1.

## Teste 2: tempo real de cada passo

Três rodadas, valores consistentes:

| Passo | Chamada | Observação |
| --- | --- | --- |
| Ligar HDR (`HDRSwitch.exe`) | 2284, 2401, 2385 ms | é o passo caro; troca de modo no driver |
| Desligar HDR | 2253, 2256, 2241 ms | |
| Resolução (`QRes.exe`) | 74 a 88 ms | confirmada em 143 a 153 ms na volta para 1440p |
| RTSS (copiar perfil, matar, reiniciar) | 10 a 47 ms | processo no ar em 16 a 61 ms |

**O trabalho real soma cerca de 2,5 s.** Os `.bat` levam 23,4 s (10,3 s + 13,1 s), ou seja, mais de 20 s são `timeout` fixo. O mesmo vale para o desfazer.

Duas ressalvas honestas:

- **A verificação do 4K falhou nas três rodadas** (`ok=False` após 30 s). O `QRes /S` continuou relatando 2560x1440. Como o Sunshine registra `Desktop resolution [3840x2160]` durante a transmissão, a hipótese mais provável é que a leitura do script seja ruim, ou que o 4K exija uma taxa de atualização específica fora da transmissão. Isso **não invalida** os tempos de chamada, mas precisa do teste rápido descrito no fim.
- **"RTSS no ar" não é o mesmo que "limite aplicado".** Medir o limite valendo exige um jogo rodando; fica para a Fase 1, com o app medindo o próprio efeito.

## Teste 3: até quando o Moonlight espera

Com a barreira de 45 s somada aos `.bat`, o tempo total entre o pedido e o início do app foi de **70 s** nos dois aparelhos.

| Aparelho | Resultado |
| --- | --- |
| TV (Xbox) | Conectou normalmente aos 70 s, sem erro |
| Celular | Desistiu aos ~57 s (`Falha ao iniciar Playnite (error 0)`), tentou de novo e falhou no handshake RTSP (`error 104`), e só na terceira tentativa conectou |

Linha do tempo do celular: pedido às 11:02:05, erro às 11:03:02 (57 s), preparo terminou às 11:03:15, `/resume` às 11:03:23 falhou, `/resume` às 11:03:35 conectou.

**Orçamento da barreira: menos de 55 s, contados do pedido, para não irritar o cliente mais impaciente.** O Moonlight do celular se recupera sozinho, mas às custas de duas telas de erro.

Regras que isso fixa para o auxiliar de gatilho:
- Alvo de aplicação do perfil: **menos de 10 s** (viável, visto o teste 2).
- Barreira com limite configurável, **padrão 20 s**; ao estourar, o auxiliar sai com sucesso e o app termina de aplicar em segundo plano. Nunca bloquear a transmissão.
- O app registra quanto levou em cada sessão e avisa se passar do alvo.

## Teste 4: monitor capturado

Um único monitor, pelo próprio log do Sunshine:

| Campo | Valor |
| --- | --- |
| `device_id` | `{fc520864-8976-50ea-bd20-365ee8af9840}` |
| `display_name` | `\\.\DISPLAY1` |
| `friendly_name` / EDID | `Q27G4`, AOC, product `B403` |
| Estado em repouso | 2560x1440, 180 Hz, HDR desligado, primário |

O `device_id` é o identificador estável a usar na API de vídeo (o mesmo que o Sunshine usa). O `display_name` muda de ordem entre reinícios e não serve.

## O fechamento do Playnite

Nas quatro sessões gravadas o Sunshine se comportou de três formas diferentes ao encerrar:

| Hora | Playnite aberto às | No `/cancel` |
| --- | --- | --- |
| 10:55 (celular) | 10:55:23 | `App exited gracefully within 5 seconds of launch. Treating the app as a detached command.` — o Sunshine perde o rastro e não fecha |
| 11:00 (TV) | 10:59:43 | `App did not fully exit within the timeout. Terminating the app's remaining processes.` — matou à força |
| 11:03 (celular, com barreira) | 11:03:15 | `All app processes have successfully exited.` |
| 11:06 (TV, com barreira) | 11:05:48 | `All app processes have successfully exited.` |

As duas sessões limpas são justamente as que tinham a barreira, **mas também são as duas últimas**. Duas explicações cabem nos dados:

- **A barreira ajudou:** o Playnite abre 45 s depois do RTSS reiniciar, e não durante a inicialização dele.
- **O Playnite estava "quente":** a primeira abertura depois de um tempo é lenta, e as seguintes não.

Com quatro sessões não dá para separar as duas. O `Exit Timeout` de 5 s configurado no Sunshine é curto para uma abertura fria, e isso explica o caso das 11:00 sem precisar da barreira.

**Teste A/B para decidir:** com o Playnite já aberto e fechado uma vez (quente), remover a barreira e conectar duas vezes. Se fechar limpo sem a barreira, a causa era o aquecimento, e a solução é o app fechar o Playnite por conta própria (pedido educado, espera por condição, e só então encerrar) em vez de depender do `Exit Timeout`.

## Teste rápido pendente (resolução 4K)

No PowerShell, na pasta `Automacoes`, **fora de transmissão**:

```
.\QRes.exe /S
.\QRes.exe /x 3840 /y 2160
.\QRes.exe /S
.\QRes.exe /x 2560 /y 1440
```

A saída dos dois `/S` diz se o 4K foi aplicado e como o Windows relata o modo. Isso define se a API de vídeo do app precisa informar a taxa de atualização junto com a resolução.

## O que muda no plano

1. Detecção pelo nome do cliente, com as outras pistas como plano B. O risco de confundir TV e celular cai muito.
2. A barreira do auxiliar ganha número: alvo abaixo de 10 s, limite padrão 20 s, teto absoluto 55 s.
3. HDR é o passo caro (2,3 s por troca). Vale testar na Fase 1 se a API do Windows aplica HDR e resolução numa só chamada, o que economizaria cerca de 1 s.
4. O HDR passa a seguir `SUNSHINE_CLIENT_HDR` em vez de ligar sempre.
5. O fechamento do Playnite vira responsabilidade do app, não do `Exit Timeout` do Sunshine.
