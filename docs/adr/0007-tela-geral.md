# 0007. Tela Geral: idioma e caminho do log (PR 9c)

- Status: aceita
- Data: 2026-11-23

## Contexto

O 9a só seguia o idioma do Windows e o 9b deixou o leitor do log sem como ser ligado, porque
nenhum lugar da interface permitia definir `SunshineLogPath`. O 9c fecha essas duas pontas.

## Decisão

### Dois campos, nenhum com valor fixo

- `AppSettings.Language` (`pt-BR`, `en` ou nulo = seguir o Windows). Campo **opcional**, como
  `RevertOnDisconnect` no PR 8: arquivos antigos carregam sem mudança e o `schemaVersion`
  continua 1. Um valor desconhecido escrito à mão não é erro de validação (isso apagaria
  todas as configurações por causa de um detalhe de aparência); o `LanguageResolver` já cai
  no idioma do Windows ou em inglês.
- `AppSettings.SunshineLogPath`, que já existia. Continua **sem padrão**: o usuário escolhe
  (digitando, colando, ou "Procurar..."). Nada do PC do autor entra no código.

### Regra do caminho (`LogPathRule`, Domain, testada)

Vazio é válido e **desliga** o leitor. Aspas e espaços nas pontas são removidos ("Copiar como
caminho" do Explorer coloca aspas). Precisa ser um caminho completo: caminho relativo
dependeria da pasta de onde o app foi aberto. Pasta (termina em `\`) e caracteres
inválidos são recusados. **Não exige que o arquivo exista**: o leitor já espera o arquivo
aparecer (ADR 0004), e o Sunshine pode ainda não ter rodado.

### Salvar e aplicar sem reabrir

`AppHost.UpdateSettingsAsync` recebe a mudança, valida o resultado, **grava primeiro** e só
então publica em `CurrentSettingsHolder`. Se a gravação falha, nada muda em memória e a
tela mostra o erro (o app não fecha). Quando o caminho do log mudou, o leitor antigo é
parado e outro é iniciado no novo caminho, na mesma operação (que é serializada, então duas
edições seguidas não se atropelam). O idioma troca ao vivo pelo mesmo mecanismo do 9a.

### O backup diário já cobre o caso "arquivo quebrado"

Se o `settings.json` estava quebrado na abertura, o app roda com padrões; salvar uma
mudança nessa situação grava um arquivo novo por cima. `SettingsService.SaveAsync` copia o
arquivo anterior para um backup (uma vez por dia UTC) antes de gravar, então o conteúdo
quebrado não se perde.

## Fora deste PR

- **Aviso de nível de log insuficiente** (ADR 0002/0004). Para avisar certo é preciso saber
  como o log do Sunshine fica quando o nível é baixo demais, e as fixtures da Fase 0 não
  cobrem isso. Fica como item próprio, a decidir com um log de verdade em mãos, em vez de
  adivinhar uma heurística que daria alarme falso.
- **Tema manual** (a descrição da tela fala em tema): por ora o tema segue o Windows.
- Estilo dos controles de entrada (caixa de texto, lista) segue o padrão do WPF; o Sinal
  ainda não tem esses componentes.

## Consequências

- `LogPathRule` e o campo novo têm testes no CI; a tela em si só se confere à mão
  (`docs/testes-manuais.md`, seção "PR 9c").
- Depois do 9c o app é usável de ponta a ponta para quem editar o `settings.json` à mão; a
  tela de perfis (onde o usuário cria e edita perfis) ainda não existe.
