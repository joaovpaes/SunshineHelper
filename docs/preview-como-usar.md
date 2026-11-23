# SunshineHelper (versão de testes): como usar

Esta é uma **versão de testes** (pré-lançamento). Ela já faz o trabalho principal: quando uma transmissão do
Sunshine começa, aplica o perfil do aparelho (resolução, HDR, limite de FPS pelo RTSS) e, quando a transmissão
acaba, devolve tudo como estava. Mas ainda **não tem instalador** e **não é assinada**.

## O que vem no zip

- `SunshineHelper.exe`: o app (fica na bandeja do Windows). Não precisa instalar o .NET.
- `SunshineHelper.Trigger.exe`: o auxiliar que o Sunshine chama no começo e no fim da transmissão.
- `LICENSE` e este `LEIA-ME.md`.

Confira o zip com o `SHA256SUMS.txt` da página da versão (no PowerShell: `Get-FileHash .\arquivo.zip`; o valor
tem de ser igual ao do arquivo).

## Limites desta versão (leia antes)

- **Sem assinatura de código:** o Windows mostra "O Windows protegeu o computador" (SmartScreen). Clique em
  "Mais informações" e "Executar assim mesmo", se você confia na origem.
- **Sem instalador:** descompacte numa pasta sua (por exemplo `C:\Programas\SunshineHelper`) e abra o
  `SunshineHelper.exe`.
- **O app precisa estar aberto antes da transmissão.** Se estiver fechado, o auxiliar desiste em 2 s e a
  transmissão começa sem perfil. Para abrir junto com o Windows, ponha um atalho na pasta de inicialização
  (`Win+R`, `shell:startup`).
- Testado até agora só em Windows 11 com o Sunshine para Windows, RTSS 7.3.x e Playnite.

## Configurar (uma vez)

1. Abra o `SunshineHelper.exe`. Na tela **Geral**, informe o caminho do `sunshine.log` do Sunshine (botão
   "Procurar"). No Sunshine, o nível de log deve ser **Debug** (o app avisa no Painel se estiver baixo).
2. Na tela **Perfis**, crie um perfil (nome do aparelho como aparece no Moonlight, resolução, HDR, limite de FPS).
   Um perfil marcado como padrão vale quando nenhum nome bate.
3. No Sunshine (página web, **Applications**, o seu app, **Command Preparations**), adicione:
   - **Do:** `"C:\caminho\da\pasta\SunshineHelper.Trigger.exe" start`
   - **Undo:** `"C:\caminho\da\pasta\SunshineHelper.Trigger.exe" end`

   O auxiliar lê sozinho o nome e as configurações do aparelho que o Sunshine envia. Funciona com ou sem
   "Run as elevated".
   **Opcional, para o caso de esquecer o app fechado:** acrescente, **antes** do `Trigger start`, um comando
   **Do** que abre o app sem travar o Sunshine (que espera cada comando terminar):
   `cmd /c start "" "C:\caminho\da\pasta\SunshineHelper.exe"` (deixe "Run as elevated" **desmarcado**, para o app
   abrir na sua conta). O app abre em alguns segundos e o auxiliar só espera 2 s, então, se o app estava fechado,
   a primeira transmissão pode começar sem perfil. Para dar tempo, ponha entre os dois um comando de espera:
   `cmd /c ping -n 6 127.0.0.1 >nul` (custa uns 5 s em toda transmissão). Ele não abre uma segunda cópia se o app
   já estiver aberto. Na **Undo** não é preciso nada para o app.
4. **Limite de FPS (RTSS):** se o perfil limita o FPS e o app ainda não pode gravar o perfil do RTSS, a tela
   Perfis mostra um aviso com o botão **Reparar**. O Windows pede confirmação uma vez e libera só esse arquivo.
   O RTSS precisa estar aberto para o limite valer.

## Como saber se funcionou

- No **Painel**: "Transmissão ativa" durante a transmissão e "Nenhuma transmissão ativa" depois.
- O app guarda um registro em `%LOCALAPPDATA%\SunshineHelper` (`app.log`). Em **Geral** há o botão
  "Gerar relatório de diagnóstico" (dados sensíveis são ocultados; leia o arquivo antes de compartilhar).
- Se o Sunshine for finalizado no meio da transmissão, o app restaura a tela sozinho em poucos segundos.

## Problemas

Abra uma issue no GitHub com o relatório de diagnóstico. **Leia o arquivo antes de enviar.**
