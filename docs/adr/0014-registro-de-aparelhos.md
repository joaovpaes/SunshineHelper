# 0014. Registro dos aparelhos que conectaram

- Status: proposta (a aceitar depois do teste no PC do autor)
- Data: 2026-11-23

## Contexto

A tela Dispositivos precisa listar os aparelhos que já conectaram. Hoje o app só conhece o nome do cliente enquanto
a sessão está ativa (está no snapshot, que é apagado no fim). Para criar um perfil, o usuário precisa digitar à mão
o nome exato que o Moonlight informa, e um erro de digitação faz o perfil não casar.

## Decisão

- Um arquivo novo, `devices.json`, na mesma pasta do `settings.json` (`%LOCALAPPDATA%\SunshineHelper`). Não entra
  no `settings.json`: é histórico gerado pelo app, não configuração do usuário, e assim o `schemaVersion` das
  configurações não muda.
- Guarda **só**: nome do aparelho (como o Moonlight informa), primeira e última conexão, número de conexões e o
  nome do último perfil usado. **Nunca** IP, MAC, `uniqueid`, `rikey` nem outra chave do Sunshine.
- Regras sem I/O no Domain (`KnownDevices`): nome comparado sem diferenciar maiúsculas (igual ao casamento de
  perfis), mais recente primeiro, limite de 100 aparelhos e 200 caracteres por nome.
- `DeviceRegistry` (Application) lê e grava o arquivo pelo mesmo `ISettingsFileStore` das configurações (gravação
  atômica). Arquivo ausente, vazio ou inválido vira lista vazia, com aviso no `app.log`.
- O `TriggerChannelHandler` anota o aparelho **logo depois de escolher o perfil**, também quando nenhum perfil
  corresponde (é justamente o caso em que o usuário quer criar um). Falhas de leitura e gravação são só registradas
  no log e engolidas: **o registro nunca pode impedir nem atrasar a transmissão**. Não toca no orquestrador nem no
  snapshot.
- A tela mostra a lista (atualiza sozinha se um aparelho conectar com ela aberta) e tem "Esquecer aparelho", que só
  apaga o registro; os perfis não são mexidos.

## Consequências

- A lista começa vazia e vai enchendo a partir das próximas transmissões (não há como reconstruir o passado).
- Retomadas (`/resume`) detectadas só pelo leitor do log não contam como nova conexão.
- O botão "Criar perfil para este aparelho" fica para o PR seguinte.
