# Arquitetura — StreamerBot.UnifiedHub

Este documento reúne decisões de arquitetura que não são óbvias olhando só o
código, e que já foram redescobertas/discutidas mais de uma vez. O objetivo é
não perder esse contexto entre sessões de desenvolvimento.

## Visão geral

DLL C# que roda hospedada dentro do processo do Streamer.bot para dar suporte
a ferramentas de live (Spotify hoje, Twitch e YouTube planejados). Multi-target
`net8.0;net48` — o `net48` é obrigatório porque o compilador Roslyn interno do
Streamer.bot resolve contra assemblies da era .NET Framework.

Princípio geral do projeto: **generalizar só quando dois ou mais casos
concretos justificarem a forma** — sem abstração prematura.

## Por que não usamos RazorLight

O `RazorLight` foi removido do projeto inteiro. Ele escaneia todas as
assemblies do `AppDomain` para montar as referências de compilação, e quebra
ao encontrar uma assembly dinâmica sem `Location` — que é exatamente o que o
Streamer.bot gera ao compilar o código C# inline do usuário em memória. É uma
limitação conhecida e não configurável do RazorLight nesse cenário.

**Substituto:** sistema próprio de duas camadas em `Core/Services/Html/`:

- `HtmlPageShell` — renderiza a casca completa da página (doctype, head,
  estilos, container, header, banners de erro/sucesso) via
  `HtmlPageShell.Render(options, bodyHtml)` com `PageShellOptions`.
- `HtmlComponents` — helpers estáticos para fragmentos reutilizáveis de UI
  (`SectionTitle`, `FormGroup`, `ToggleSwitch`, `SubmitButton`, `CancelLink`,
  `TwoColumnGrid`).

Elementos específicos de cada página (lista de playlists, iframe de preview,
JS de postMessage) permanecem como HTML manual dentro do corpo, por design —
não vale a pena abstrair algo usado uma única vez.

## Polyfills para `net48`

O namespace `Core/Compatibility` guarda extension methods para APIs que não
existem no `net48` (ex: overloads de `string.Contains`/`string.Replace` com
`StringComparison`). Qualquer arquivo que use esses overloads precisa de:

```csharp
using StreamerBot.UnifiedHub.Core.Compatibility;
```

Se aparecer um erro `CS1501` dizendo que o overload não existe, é quase
sempre esse `using` faltando (o compilador do Streamer.bot só reclama disso
quando compila sob `net48`).

## Conflito de porta com o Streamer.bot

O WebSocket nativo do Streamer.bot ocupa a porta **8080**. Todos os
servidores HTTP/WebSocket da UnifiedHub precisam usar portas diferentes.
Hoje: overlay usa **8081** por padrão.

## Onde a DLL precisa ficar

O `.dll` compilado precisa ir **diretamente na pasta do executável do
Streamer.bot**. A opção "Additional References" dentro do Streamer.bot só
afeta os metadados de compilação do Roslyn — não afeta a resolução de
assemblies em tempo de execução do CLR. Colocar a DLL só nas "Additional
References" e não na pasta do executável resulta em erro de tipo não
encontrado em runtime, mesmo compilando sem erros.

## Por que `InitSpotify` e não `Init`

O método de inicialização da integração se chama `InitSpotify` (não `Init`)
porque `Init` colide com o método reservado da própria plataforma
(`CPHInlineBase.Init()`). O mesmo padrão deve ser seguido nas integrações
futuras (`InitTwitch`, `InitYoutube`, etc.) para evitar a mesma colisão.

## JavaScript dentro do overlay (OBS/CEF)

O Chromium embutido no OBS Browser Source é antigo e **não suporta nullish
coalescing (`??`)** — gera `SyntaxError` em tempo de execução no navegador
embutido, silenciosamente quebrando o overlay. Use `||` no lugar em todo
JavaScript que vai rodar dentro do Browser Source (ex:
`Integrations/Overlay/Assets/chat-overlay.html`).

## Padrão `onSettingsSaved`

Callbacks assíncronos que reiniciam um servidor HTTP/WebSocket após salvar
configurações (ex: `ChatOverlayHub.OnSettingsSavedAsync`) devem dar um
`await Task.Delay(500)` **antes** de derrubar o servidor. Isso garante que a
resposta HTTP do POST do formulário seja entregue ao navegador antes da
conexão cair — sem o delay, o navegador pode ver a requisição falhar mesmo
com o save tendo funcionado.

## Limitação: `BotName` não controla o remetente da mensagem

`CPH.SendMessage` do Streamer.bot **sempre** envia a mensagem sob a conta
real do bot conectado — a configuração `BotLabel`/`BotName` da integração não
tem nenhum efeito sobre a identidade de quem envia. O workaround atual é
prefixar o texto da mensagem com o label (ex: `[Spotify] mensagem`), visível
em `ChatMessageDispatcher.Raise`. Não é uma limitação da DLL — é uma
limitação da própria API do Streamer.bot.

## Responsabilidades: o que é da DLL, o que é do Streamer.bot

Comandos de chat e checagem de permissões (quem pode rodar `!skip`, cooldown,
etc.) são responsabilidade do **Streamer.bot** (configurado nos triggers). A
DLL só cuida de:

- chamadas às APIs externas (Spotify, futuramente Twitch/YouTube);
- gerenciamento de estado (fila, votos de skip, faixa atual);
- montagem e disparo de mensagens de resposta no chat.

Isso já rendeu uma rejeição explícita no passado: um pedido de adicionar
`CommandText` em `MessageDefinition` foi recusado porque misturaria a
responsabilidade de "definir o texto do comando" (Streamer.bot) com a de
"definir o template da mensagem de resposta" (DLL).

## Padrão de integração (para replicar em Twitch/YouTube)

Cada integração de chat segue a mesma estrutura de pastas e classes:

```
Integrations/{Nome}/
  Extensions/   → {Nome}AppConfigExtensions (Get/Set config no AppConfig)
  Hubs/         → {Nome}Hub (fachada estática pro Streamer.bot chamar)
  Models/       → {Nome}Config : ChatIntegrationConfig, view models, DTOs
  Services/     → {Nome}Manager, {Nome}OAuthHandler, {Nome}OAuthStrategy,
                   {Nome}HtmlTemplates
```

Peças genéricas em `Core/` que toda integração de chat deve reaproveitar:

- `ChatIntegrationConfig` (base de config)
- `ChatMessageDispatcher` + `ChatMessageFormatter` (montar/disparar mensagem)
- `ChatIntegrationConfigMapper` (aplicar `ExtraSettings` do OAuth na config)
- `HubExecutionHelper` (padrão try/catch → `HubResult` amigável)
- `HtmlPageShell` + `HtmlComponents` (telas de configuração web)
- `OAuthHandler` / `OAuthFlowHandler` / `SettingsOnlyFlowHandler` (fluxo OAuth)

## Pendências conhecidas

- Badges de chat (MOD/VIP/SUB/BR) hoje são chips coloridos custom baseados em
  argumentos booleanos vindos do trigger do Streamer.bot. Integração completa
  com a API de badges da Twitch (imagens reais) ainda não foi feita.
