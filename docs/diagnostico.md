# Relatório de diagnóstico

Quando algo não funciona e a causa não é óbvia, o app gera **um arquivo de texto** com tudo o que ajuda a
entender o problema. Esse arquivo pode ser lido por uma pessoa, pelo Claude ou por uma ferramenta externa
que só analisa o código-fonte. Serve para diagnosticar **antes** de gastar muito tempo e muitos tokens
tentando consertar às cegas.

## Como gerar

1. Abra o app, tela **Geral**, e role até **Relatório de diagnóstico**.
2. Escreva o problema com suas palavras: o que você fez, o que esperava e o que aconteceu.
3. Clique em **Gerar relatório de diagnóstico** e escolha onde salvar (a área de trabalho já vem sugerida).
4. **Leia o arquivo** antes de compartilhar.

Nada é enviado pela internet. O app só grava o arquivo que você escolheu.

## O que o arquivo tem

| Parte | De onde vem | Tamanho |
|---|---|---|
| Versão do app e do Windows, data | o próprio app | 3 linhas |
| Descrição do problema | o que você digitou | livre |
| Problemas de inicialização | o app (por exemplo, `settings.json` ignorado) | poucas linhas |
| `settings.json` | `%LOCALAPPDATA%\SunshineHelper\settings.json` | até 256 KB |
| Log do app | `%LOCALAPPDATA%\SunshineHelper\logs\app.log` | últimas 400 linhas |
| Log do Sunshine | o caminho configurado na tela Geral | últimas 1500 linhas |

Se uma parte não existir (log do Sunshine não configurado, arquivo apagado), o relatório diz isso na própria
seção e segue com o resto.

## Privacidade

O texto inteiro passa pelo `LogRedactor` no fim, incluindo o que você digitou:

- endereços IPv4 viram `<ip>`;
- endereços MAC viram `<mac>`;
- `rikey`, `uniqueid` e `rikeyid` do Sunshine viram `<redacted>`;
- o nome da conta do Windows em `C:\Users\<nome>\...` vira `<user>`.

**O que não é redigido:** o nome do aparelho (por exemplo "Celular"), nomes de perfis, nomes de programas e
qualquer coisa que você escreva por extenso na descrição (como um endereço de e-mail). Por isso, leia o
arquivo antes de compartilhar. O log do Sunshine em nível Debug é longo e pode conter detalhes do seu
PC; o relatório só leva o fim dele.

O `sunshine.log` completo **nunca** entra no repositório. Se um trecho virar arquivo de teste, ele é
redigido antes e revisado.

## Modelo de pedido para a ferramenta externa

A ferramenta externa lê só o código-fonte baixado (não o histórico do git). Cole o texto abaixo e anexe
o relatório e a pasta do código:

```text
Você vai diagnosticar um problema do SunshineHelper (app WPF, C# 14, .NET 10, Windows).
Você tem: o código-fonte desta versão (pasta anexada) e o relatório de diagnóstico (arquivo anexado).
Você NÃO tem o histórico do git e NÃO pode executar o app.

Leia primeiro: CLAUDE.md, docs/fase1-plano.md e docs/adr/.

Tarefa:
1. Resuma o problema em uma frase, com base na descrição do relatório.
2. Ligue o que o app.log mostra (linhas "orchestrator", "display", "hdr", "channel", "monitor") ao código
   responsável. Cite arquivo e método.
3. Liste até 3 causas prováveis, da mais para a menos provável, e para cada uma diga que linha do relatório
   a sustenta e que linha a enfraqueceria.
4. Proponha a menor correção para a causa mais provável (arquivos e o que muda), sem refatorar o resto.
5. Diga o que falta saber e que teste manual no PC do autor responderia isso.

Regras: não invente comportamento do Sunshine ou do Moonlight; se não está no relatório nem no código,
diga que não sabe. Responda em português, com frases curtas.
```

Depois, cole a resposta na conversa com o Claude, que confere contra o código e abre o PR pequeno.

## Quando o Claude deve recomendar este relatório

Está no `CLAUDE.md`: quando um problema não se resolve em duas tentativas, ou o `app.log` não explica a
causa, o Claude recomenda gerar o relatório e usar a ferramenta externa antes de tentar uma terceira correção.
