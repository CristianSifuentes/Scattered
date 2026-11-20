# Capítulos 3–4 · Producers/Consumers con RabbitMQ (`features/flow3`)

En el capítulo 2 toda la comunicación era **síncrona**: el cliente llama al gateway, el gateway llama a un servicio y espera la respuesta. En estos capítulos aparece la comunicación **asíncrona**: el gateway publica un mensaje en RabbitMQ y responde enseguida; un proceso distinto (el *consumer*) lo recoge cuando puede y lo procesa. Productor y consumidor ya no necesitan estar vivos a la vez.

<!-- nav:start (generado por tools/docs/progress.py desde docs/flow/chapters.json; no editar a mano) -->
[← Capítulo 2](flow2.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 5 →](flow4.md)

Progreso: [1](flow1.md) · [2](flow2.md) · **3–4** · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · [13](flow12.md) · [14](flow13.md) · [15](flow14.md) · [16](flow15.md) · [17](flow16.md)
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow3`)](#flujo-end-to-end-featuresflow3) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`03-04-Producers-consumers-RabbitMQ`](https://github.com/ElectNewt/Distribt/tree/03-04-Producers-consumers-RabbitMQ) |
| Commits de origen | `407155f` Add producers consumers with RabbitMQ (13-ene-2022) · `871499e` fix typos · `86a48b8` improve RabbitMQ configuration and library (19-ene-2022) · `0da6850` update readme |
| Diff respecto al capítulo 2 | 4 commits, 57 archivos, +1366 / −12 líneas |
| Artículos | [Patrón productor-consumidor](https://www.netmentor.es/entrada/patron-productor-consumidor) · [RabbitMQ](https://www.netmentor.es/entrada/rabbitmq-comunicacion-asincrona) |
| Cambios propios | Usuario de RabbitMQ declarado en `tools/rabbitmq/definitions.json` (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff features/flow2 features/flow3 -- src tools docker-compose.yaml`.

## Resumen

```text
Cliente ──POST /subscribe──▶ API Public (:7283)
                              │  IIntegrationMessagePublisher.Publish(dto, routingKey: "subscription")
                              ▼
                    ┌──────────── RabbitMQ (:5672) ────────────┐
                    │ api.public.exchange (direct)              │
                    │   └─ key "subscription" ─▶ subscription.exchange (topic)
                    │                              └─ key "subscription" ─▶ subscription-queue
                    └───────────────────────────────────────────┘
                              │  BasicConsume (ack manual)
                              ▼
               Subscriptions.Consumer (:7073)
                 ConsumerHostedService → RabbitMQMessageConsumer → RabbitMQMessageReceiver
                   → HandleMessage → MessageHandlerRegistry → SubscriptionHandler.Handle()
                   → Console: "Email … successfully subscribed."  → BasicAck
```

El cliente recibe `200` en cuanto el mensaje está en RabbitMQ, **no** cuando se procesa.

## Qué cambió

```text
docker-compose.yaml                              RabbitMQ 3.9 con usuario, config y topología
tools/rabbitmq/                                  NUEVO: rabbitmq.config + definitions.json
docs/communication/Readme.md                     NUEVO: documentación de Shared.Communication (del original)
Scattered.sln                                    + 5 proyectos
src/
├── Api/Public/Scattered.API.Public
│   ├── Program.cs                               + publisher + endpoint POST /subscribe
│   ├── appsettings.json                         + sección Bus:RabbitMQ (exchange de publicación)
│   └── .csproj                                  + referencia a Subscriptions.Dtos
├── Services/
│   ├── Orders/…/Controllers/OrderController.cs  GetOrder publica un mensaje (⚠ roto, ver abajo)
│   └── Subscriptions/
│       ├── Scattered.Services.Subscriptions.Dtos       NUEVO: contrato SubscriptionDto / UnSubscriptionDto
│       └── Scattered.Services.Subscriptions.Consumer   NUEVO: proceso que consume la cola
└── Shared/
    ├── Scattered.Shared.Serialization           NUEVO: ISerializer (Newtonsoft + System.Text.Json)
    ├── Shared.Communication/
    │   ├── Scattered.Shared.Communication          NUEVO: abstracciones (mensajes, publisher, consumer, handlers)
    │   └── Scattered.Shared.Communication.RabbitMQ NUEVO: implementación con RabbitMQ.Client
    ├── Scattered.Shared.Setup                   + ServiceBus.cs, referencias, GlobalUsings
    └── Scattered.Shared.Api                     + AddSerializer() en el arranque común
```

### 1. Infraestructura: RabbitMQ con topología predefinida

**`docker-compose.yaml`**

```diff
-    image: rabbitmq:3-management
+    image: rabbitmq:3.9-management-alpine
     container_name: rabbitmq
+    environment:
+        - RABBITMQ_DEFAULT_USER=ScatteredAdmin
+        - RABBITMQ_DEFAULT_PASS=ScatteredPass
     ports:
         - 5672:5672      # AMQP: lo usan las aplicaciones
         - 15672:15672    # UI de administración
+    volumes:
+          - ./tools/rabbitmq/rabbitmq.config:/etc/rabbitmq/rabbitmq.config
+          - ./tools/rabbitmq/definitions.json:/etc/rabbitmq/definitions.json
```

- Imagen fijada a 3.9 en lugar de `3-management` (que cambiaba con cada versión).
- **`tools/rabbitmq/rabbitmq.config`** (formato Erlang): `loopback_users = []` permite conectarse con usuarios no locales, y `load_definitions` carga `definitions.json` al arrancar.
- **`tools/rabbitmq/definitions.json`**: la topología se crea sola al arrancar el contenedor, en vez de crearla a mano o desde el código.

| Tipo | Nombre | Detalle |
|---|---|---|
| Exchange | `api.public.exchange` | `direct`. Donde publica el gateway público |
| Exchange | `subscription.exchange` | `topic`. Exchange del dominio Subscriptions |
| Exchange | `dead-letter.exchange` | `direct`. Destino de los mensajes rechazados |
| Binding | `api.public.exchange` → `subscription.exchange` | Exchange a exchange, routing key `subscription` |
| Binding | `subscription.exchange` → `subscription-queue` | Routing key `subscription` |
| Binding | `dead-letter.exchange` → `subscription-queue.dead-letter` | Routing key `subscription` |
| Cola | `subscription-queue` | Durable. La consume Subscriptions.Consumer |
| Cola | `subscription-queue.dead-letter` | Durable. Recibe lo que se rechace de la anterior |
| Policy | `DLX` | Patrón `.*`: todas las colas mandan sus rechazos a `dead-letter.exchange` |

El gateway no conoce la cola de Subscriptions: publica en **su** exchange (`api.public.exchange`) y es RabbitMQ quien enruta. Si mañana otro servicio quiere enterarse de las suscripciones, basta con añadir un binding, sin tocar el gateway.

### 2. `Scattered.Shared.Communication`: la abstracción (sin RabbitMQ)

Proyecto nuevo que define *qué* es publicar y consumir, sin saber *con qué*. Es lo que hace a Scattered *vendor free*.

| Archivo | Qué define |
|---|---|
| `Messages/IMessage.cs` | `MessageIdentifier` (único por mensaje) y `Name` (nombre legible, para logs) |
| `Messages/IntegrationMessage.cs` | `IntegrationMessage` y `IntegrationMessage<T>` con `Content` (el DTO) y `Metadata`. Mensajes **entre** servicios |
| `Messages/DomainMessage.cs` | `DomainMessage` / `DomainMessage<T>`. Mensajes **dentro** de un servicio (se usarán con CQRS) |
| `Messages/Metadata.cs` | `CorrelationId` y `CreatedUtc` |
| `Publisher/IExternalMessagePublisher.cs` | Contrato que implementa cada broker: `Publish` y `PublishMany` |
| `Publisher/Integration/DefaultIntegrationMessagePublisher.cs` | `IIntegrationMessagePublisher`: recibe un objeto cualquiera, genera `Metadata` y lo envuelve |
| `Publisher/Integration/IntegrationMessageMapper.cs` | Envuelve `T` en `IntegrationMessage<T>` por reflexión (`Name = typeof(T).Name`, `MessageIdentifier = Guid`) |
| `Publisher/Domain/*` | Lo mismo para `DomainMessage` |
| `Consumer/IMessageConsumer.cs` | Contrato del consumidor: `StartAsync` |
| `Consumer/Host/ConsumerHostedService.cs` | `IHostedService` que arranca el consumidor con la aplicación |
| `Consumer/Host/ConsumerController.cs` | Controller base con `PUT start` para reiniciar el consumidor |
| `Consumer/Manager/ConsumerManager.cs` | Guarda el `CancellationTokenSource` para parar o reiniciar |
| `Consumer/Handler/IMessageHandler.cs` | `IIntegrationMessageHandler<T>` / `IDomainMessageHandler<T>`: lo que implementa cada caso de uso |
| `Consumer/Handler/MessageHandlerRegistry.cs` | Busca (y cachea) qué handlers aceptan un tipo de mensaje |
| `Consumer/Handler/HandleMessage.cs` | Invoca por reflexión el `Handle` del handler que corresponde |
| `CommunicationDependencyInjection.cs` | `AddPublisher<T>()` y `AddConsumer<T>()` |

Un servicio que quiera publicar solo ve `IIntegrationMessagePublisher.Publish(objeto)`. No sabe que existe RabbitMQ.

### 3. `Scattered.Shared.Communication.RabbitMQ`: la implementación

| Archivo | Qué hace |
|---|---|
| `RabbitMQSettings.cs` | Mapea `Bus:RabbitMQ` de `appsettings.json`: host, usuario, contraseña, `Publisher` (exchanges) y `Consumer` (colas) |
| `RabbitMQDependencyInjection.cs` | `AddRabbitMQ`, `AddRabbitMQPublisher<T>`, `AddRabbitMqConsumer<T>`, `AddConsumerHandlers` |
| `Publisher/RabbitMQMessagePublisher.cs` | Abre conexión y canal **por cada publicación**, marca el mensaje como persistente, pone en `properties.Type` el nombre del tipo **sin versión** (límite de 255 caracteres y evita romper al cambiar de versión) y publica en el exchange de integración o de dominio según `T` |
| `Consumer/RabbitMQMessageConsumer.cs` | Abre conexión y canal y se registra en la cola con `BasicConsume(queue, autoAck: false, receiver)` |
| `Consumer/RabbitMQMessageReceiver.cs` | Por cada mensaje: `Type.GetType(properties.Type)`, deserializa, llama a `IHandleMessage` y, si todo va bien, `BasicAck` |

Dependencia: `RabbitMQ.Client` 6.2.2.

### 4. `Scattered.Shared.Serialization`

`ISerializer` con dos motores: **Newtonsoft.Json** (con `TypeNameHandling.Auto`) para los bytes que viajan por RabbitMQ, y **System.Text.Json** para strings. Se registra en el arranque común:

```diff
 builder.Services.AddRouting(x => x.LowercaseUrls = true);
+builder.Services.AddSerializer();
```

Así, todos los proyectos que usan `DefaultScatteredWebApplication` tienen `ISerializer` disponible.

### 5. `Scattered.Shared.Setup/ServiceBus.cs`: la fachada

Lo único que los servicios llaman. Esconde que la implementación es RabbitMQ:

```csharp
services.AddServiceBusIntegrationPublisher(configuration); // publicar IntegrationMessage
services.AddServiceBusIntegrationConsumer(configuration);  // consumir IntegrationMessage
services.AddServiceBusDomainPublisher(configuration);      // ídem para DomainMessage
services.AddServiceBusDomainConsumer(configuration);
services.AddHandlers(handlers);                            // registrar los handlers
```

Además `Shared.Setup` pasa a referenciar `Serialization` y `Communication.RabbitMQ`, y `GlobalUsings.cs` añade `Scattered.Shared.Setup` y `Scattered.Shared.Serialization`.

### 6. El productor: `POST /subscribe` en el gateway público

```diff
 WebApplication app = DefaultScatteredWebApplication.Create(webappBuilder =>
 {
     webappBuilder.Services.AddReverseProxy()
         .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));
+    webappBuilder.Services.AddServiceBusIntegrationPublisher(webappBuilder.Configuration);
 });

 app.MapGet("/", () => "Hello World!");
+
+app.MapPost("/subscribe", async (SubscriptionDto subscriptionDto) =>
+{
+    IIntegrationMessagePublisher publisher = app.Services.GetRequiredService<IIntegrationMessagePublisher>();
+    await publisher.Publish(subscriptionDto, routingKey: "subscription");
+});
+
 app.MapReverseProxy();
```

```json
"Bus": {
  "RabbitMQ": {
    "Hostname": "localhost",
    "Username": "ScatteredAdmin",
    "Password": "ScatteredPass",
    "Publisher": { "IntegrationExchange": "api.public.exchange" }
  }
}
```

- La suscripción **no** pasa por YARP hacia el microservicio Subscriptions: el gateway la convierte en un mensaje. Por eso en el capítulo 2 Subscriptions no tenía ruta.
- El gateway referencia `Subscriptions.Dtos` para conocer el contrato `SubscriptionDto(string Email)`.

### 7. El consumidor: `Scattered.Services.Subscriptions.Consumer`

Proyecto web nuevo (`:7073` HTTPS / `:5073` HTTP) cuyo trabajo real ocurre en segundo plano.

```csharp
WebApplication app = DefaultScatteredWebApplication.Create(x =>
{
    x.Services.AddHandlers(new List<IMessageHandler>()
    {
        new SubscriptionHandler(),
        new UnSubscriptionHandler()
    });
    x.Services.AddServiceBusIntegrationConsumer(x.Configuration);
});
```

| Archivo | Qué hace |
|---|---|
| `Handler/SubscriptionHandler.cs` | `IIntegrationMessageHandler<SubscriptionDto>`: escribe `Email … successfully subscribed.` |
| `Handler/UnSubscriptionHandler.cs` | `IIntegrationMessageHandler<UnSubscriptionDto>`: escribe `the email … has unsubscribed.` (nadie publica este mensaje todavía) |
| `Controllers/IntegrationConsumerController.cs` | Hereda de `ConsumerController<IntegrationMessage>`: expone `PUT /integrationconsumer/start` |
| `appsettings.json` | `Bus:RabbitMQ:Consumer:IntegrationQueue = subscription-queue` |

Los handlers se crean con `new` y se pasan como lista: no reciben dependencias por inyección (el original lo marca como pendiente, `#8`).

### 8. `Scattered.Services.Subscriptions.Dtos`

Proyecto de contratos compartido entre productor (gateway) y consumidor:

```csharp
public record SubscriptionDto(string Email);
public record UnSubscriptionDto(string Email);
```

### 9. Orders publica al consultar un pedido

```diff
+    public OrderController(IIntegrationMessagePublisher integrationMessagePublisher) { … }
+
     [HttpGet("{orderId}")]
-    public Task<OrderDto> GetOrder(Guid orderId)
+    public async Task<OrderDto> GetOrder(Guid orderId)
     {
-        return Task.FromResult(new OrderDto(orderId));
+        OrderDto orderDto = new OrderDto(orderId);
+        await _integrationMessagePublisher.Publish(orderDto);
+        return orderDto;
     }
```

Es un ejemplo a medias del original: `Program.cs` de Orders **no** registra el publisher ni tiene sección `Bus`, así que el controller entero falla (ver [Problemas heredados](#problemas-heredados-del-original)).

### 10. Solución

`Scattered.sln` incorpora 5 proyectos: `Subscriptions.Consumer`, `Subscriptions.Dtos`, `Shared.Serialization`, `Shared.Communication` y `Shared.Communication.RabbitMQ`.

## Lo que no cambia

- **Rutas de YARP y puertos** del capítulo 2. El gateway sigue enrutando `/order-ms`, `/product-ms` y `/reports`.
- **Products, Emails y el microservicio Subscriptions (API)**: mismo código. La suscripción ya no llega a la API de Subscriptions sino a su *consumer*.
- **Persistencia**: el handler solo escribe en consola; no se guarda la suscripción.

## Cómo ejecutarla

Ahora hace falta Docker para RabbitMQ:

```bash
docker compose up -d rabbitmq
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Subscriptions/Scattered.Services.Subscriptions.Consumer
```

Los demás servicios se arrancan igual que en el [capítulo 2](flow2.md#cómo-ejecutarla). Para detenerlos: `pkill -f 'Scattered\.(API|Services)'`; para RabbitMQ: `docker compose stop rabbitmq`.

| Qué | Dónde |
|---|---|
| UI de RabbitMQ | http://localhost:15672 (usuario `ScatteredAdmin`, contraseña `ScatteredPass`) |
| AMQP | `localhost:5672` |
| Subscriptions.Consumer | https://localhost:7073/swagger |

En la UI, *Queues → subscription-queue* muestra los mensajes pendientes y los consumidores conectados; *Get messages* permite ver un mensaje sin consumirlo.

## Flujo end to end (`features/flow3`)

> **Rama:** `features/flow3`, equivalente a [`03-04-Producers-consumers-RabbitMQ`](https://github.com/ElectNewt/Distribt/tree/03-04-Producers-consumers-RabbitMQ) del original (`0da6850`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-18 con los 7 procesos arrancados (logs detallados como en el [capítulo 2](flow2.md#arrancar-con-logs-detallados), añadiendo `Scattered.Services.Subscriptions.Consumer`) y RabbitMQ en Docker.

### Las llamadas

```bash
# 1. Suscribirse: el gateway publica, el consumer procesa
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'

# 2–3. Con el consumer parado, los mensajes esperan en la cola; al arrancarlo se procesan
pkill -f 'Scattered\.Services\.Subscriptions\.Consumer'
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"ana@example.com"}'
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"luis@example.com"}'
curl -s -u ScatteredAdmin:ScatteredPass -H 'content-type: application/json' -X POST \
  http://localhost:15672/api/queues/%2F/subscription-queue/get -d '{"count":1,"ackmode":"ack_requeue_true","encoding":"auto"}'
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Subscriptions/Scattered.Services.Subscriptions.Consumer

# 4. Baja: publicar a mano un UnSubscriptionDto (properties.type = IntegrationMessage`1[[...UnSubscriptionDto...]])
#    desde la UI (Exchanges → subscription.exchange → Publish message) o con POST /api/exchanges/%2F/subscription.exchange/publish

# 5–6. Orders, roto en este capítulo
curl -k https://localhost:7283/order-ms/order/3fa85f64-5717-4562-b3fc-2c963f66afa6
curl -k -X POST https://localhost:7283/order-ms/order -H 'Content-Type: application/json' -d '{"orderId":"00000000-0000-0000-0000-000000000000"}'

# 7–8. Products (el id es un Guid)
curl -k https://localhost:7283/product-ms/product/3fa85f64-5717-4562-b3fc-2c963f66afa6
curl -k -X POST https://localhost:7283/product-ms/product -H 'Content-Type: application/json' \
  -d '{"id":"3fa85f64-5717-4562-b3fc-2c963f66afa6","productName":"Teclado","productDescription":"Mecánico"}'

# 9–10. Gateway privado
curl -k https://localhost:7022/reports/order/3fa85f64-5717-4562-b3fc-2c963f66afa6
curl -k -X POST https://localhost:7022/subscribe -H 'Content-Type: application/json' -d '{"email":"x@example.com"}'

# 11–12. Servicios llamados directamente (no están en el gateway)
curl -k -X POST https://localhost:50120/email -H 'Content-Type: application/json' -d '{"to":"a@b.c"}'
curl -k -X POST https://localhost:50420/subscription -H 'Content-Type: application/json' -d '{"email":"a@b.c"}'

# 13–14. Otros casos
curl -k -X PUT https://localhost:7073/integrationconsumer/start
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"correo":1}'

# 15–16. RabbitMQ caído y de vuelta
docker stop rabbitmq
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"perdido@example.com"}'
docker start rabbitmq
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"vuelta@example.com"}'
```

### Qué ocurre en cada parte

| # | Llamada | Respuesta | Gateway | Servicio destino | RabbitMQ (`subscription-queue`) | Consumer |
|---|---|---|---|---|---|---|
| 1 | `POST :7283/subscribe` `{"email":"cliente@…"}` | `200` vacío (102 ms) | Endpoint propio `HTTP: POST /subscribe`, **sin YARP**; publica | — | Entra y sale el mensaje | `Email cliente@example.com successfully subscribed.` |
| 2a | Consumer parado + `/subscribe` (ana) | `200` (11 ms) | Publica igual: no sabe que no hay consumer | — | **1 mensaje**, 0 consumidores | Parado |
| 2b | `/subscribe` (luis) | `200` (8 ms) | Publica | — | **2 mensajes**, 0 consumidores | Parado |
| 2c | Ver un mensaje sin consumirlo (API de gestión, `ack_requeue_true`) | El mensaje (ver [El mensaje por dentro](#el-mensaje-por-dentro)) | — | — | Sigue con 2 | — |
| 3 | Arrancar el consumer | — | — | — | Vuelve a 0 | `Email ana@…` y `Email luis@… successfully subscribed.`, **antes** del `Now listening` de Kestrel |
| 4 | `UnSubscriptionDto` publicado a mano en `subscription.exchange` | `{"routed":true}` | — | — | Entra y sale | `the email ana@example.com has unsubscribed.` |
| 5 | `GET :7283/order-ms/order/{id}` | **`500`** | `OrderRoute` → proxy a `:50220/order/{id}`, recibe 500 | **Orders:** coincide `GetOrder` pero `Unable to resolve service for type 'IIntegrationMessagePublisher'` | — | — |
| 6 | `POST :7283/order-ms/order` | **`500`** | Proxy a `:50220/order`, recibe 500 | **Orders:** mismo error en `AddOrder` | — | — |
| 7 | `GET :7283/product-ms/product/{guid}` | `200` `{"productId":…,"productName":"TODO",…}` | `ProductRoute` → proxy a `:50320` | **Products:** datos fijos. Con `/product/1` → `400` (el id es `Guid`) | — | — |
| 8 | `POST :7283/product-ms/product` | `200` `true` | Proxy a `:50320/product` | **Products:** devuelve `true` | — | — |
| 9 | `GET :7022/reports/order/{id}` | **`500`** | **Privado:** `ReportsRoute` → `:50220` | **Orders:** mismo error que el paso 5 | — | — |
| 10 | `POST :7022/subscribe` | `404` | **Privado:** "reached the end of the middleware pipeline" | — | — | — |
| 11 | `POST :50120/email` (directo) | `400` (faltan `from`, `subject`, `body`) | No pasa por gateway | **Emails:** validación del modelo | — | — |
| 12 | `POST :50420/subscription` (directo) | `200` `true` | No pasa por gateway | **API de Subscriptions:** responde fijo; **no publica ni consume** nada | — | — |
| 13 | `PUT :7073/integrationconsumer/start` | `200` | — | — | — | Acción ejecutada, sin efecto visible |
| 14 | `POST :7283/subscribe` `{"correo":1}` | `200` | Publica `SubscriptionDto(Email: null)` | — | Entra y sale | `Email  successfully subscribed.` (vacío) |
| 15 | RabbitMQ parado + `/subscribe` | **`500`** (24 ms) | `BrokerUnreachableException: None of the specified endpoints were reachable` | — | Caído | — |
| 16 | RabbitMQ de vuelta + `/subscribe` | `200` | Publica | — | Entra y sale | `Email vuelta@example.com successfully subscribed.` (se reconectó solo) |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| El gateway público hace dos trabajos: **proxy** (`/order-ms`, `/product-ms`) y **productor** (`/subscribe`) | Pasos 1, 5, 7: líneas `Yarp…Proxying to` solo en las rutas de proxy | `/subscribe` no llama a ningún servicio: responde en ~10 ms sin esperar a nadie |
| `200` en `/subscribe` = "RabbitMQ lo tiene", no "procesado" | Pasos 2–3: `200` con el consumer apagado | El cliente no sabe si el consumer lo procesó ni cuándo |
| Los mensajes sobreviven a un consumer caído | Pasos 2–3: la cola llega a 2 y se vacía al arrancar | Desacoplamiento temporal entre productor y consumidor |
| El consumer empieza a consumir antes de que Kestrel escuche | Paso 3: los `Email … subscribed` salen antes del `Now listening` | El `ConsumerHostedService` arranca antes que el servidor HTTP |
| El handler se elige por `properties.type` | Paso 4: misma cola, otro handler | Un consumer, una cola, varios tipos de mensaje |
| Orders da `500` desde los **dos** gateways | Pasos 5, 6, 9 | Falta registrar el publisher en Orders (heredado) |
| Products necesita un `Guid` en la URL | Paso 7: `/product/1` → `400` | El `400` lo genera Products y el gateway lo reenvía tal cual |
| La API de Subscriptions y el consumer no se relacionan | Paso 12 | La API sigue con datos fijos; el trabajo real lo hace el consumer |
| Sin validación en `/subscribe` | Paso 14 | Se procesa una suscripción con email vacío |
| Con RabbitMQ caído la suscripción **se pierde** | Paso 15: `500` | No hay reintentos ni *outbox*; RabbitMQ es una dependencia del gateway |
| Gateway y consumer se reconectan solos | Paso 16 | Reconexión automática de `RabbitMQ.Client` |
| **Fuga de consumidores** | `list_queues`: 9 al arrancar → 21 → 52 → **77** en ~2 minutos | Debería haber 1: el bucle de `ConsumerHostedService` abre uno nuevo cada segundo (ver [Problemas heredados](#problemas-heredados-del-original)) |

### El mensaje por dentro

Visto desde la UI de RabbitMQ (*Get messages*, sin consumirlo):

```text
exchange        = api.public.exchange
routing_key     = subscription
properties.type = Scattered.Shared.Communication.Messages.IntegrationMessage`1[[Scattered.Services.Subscriptions.Dtos.SubscriptionDto, Scattered.Services.Subscriptions.Dtos]], Scattered.Shared.Communication
delivery_mode   = 2   (persistente: sobrevive a un reinicio de RabbitMQ)
payload         = {"Content":{"Email":"ana@example.com"},
                   "Metadata":{"CorrelationId":"060c68a7-…","CreatedUtc":"2026-11-18T21:00:34.96Z"},
                   "MessageIdentifier":"89e2f2c4-…","Name":"SubscriptionDto"}
```

El consumer usa `properties.type` para saber a qué tipo deserializar (`Type.GetType`), y el tipo (`IntegrationMessage<SubscriptionDto>`) para elegir el handler. Por eso el paso 4 funciona: basta con que `properties.type` diga `UnSubscriptionDto`.

### Qué enseña este flujo

- **Desacoplamiento temporal.** En los pasos 2–3 el gateway respondió `200` con el consumer apagado; los mensajes esperaron y se procesaron después. Con HTTP (capítulo 2), con el servicio caído, el cliente recibía `502`.
- **`200` no significa "procesado".** Significa "RabbitMQ lo tiene". El cliente no se entera de lo que pase después.
- **El productor no sabe quién consume.** El gateway publica en `api.public.exchange`; la ruta hasta `subscription-queue` la deciden los bindings de `definitions.json`.
- **RabbitMQ pasa a ser una dependencia del gateway.** Con RabbitMQ caído (paso 10) `/subscribe` devuelve `500` y la suscripción se pierde: no hay reintentos ni *outbox*.
- **El dispatch es por tipo.** Un mismo consumer atiende varios mensajes (suscripción y baja) desde la misma cola; `HandleMessage` elige el handler por el tipo genérico.

## Problemas heredados del original

Se replican tal cual para ser fieles al curso. Ninguno impide seguir el capítulo, pero conviene conocerlos.

### 1. Orders devuelve 500 en todos sus endpoints

`OrderController` pide `IIntegrationMessagePublisher` en el constructor, pero Orders no llama a `AddServiceBusIntegrationPublisher` ni tiene sección `Bus`. Como la inyección ocurre al construir el controller, fallan **los dos** endpoints, también `POST /order` (que no publica nada) y `/reports` del gateway privado:

```text
System.InvalidOperationException: Unable to resolve service for type
'Scattered.Shared.Communication.Publisher.Integration.IIntegrationMessagePublisher'
while attempting to activate 'Scattered.Services.Orders.Controllers.OrderController'.
```

En el original sigue así al menos hasta el capítulo 6. Arreglarlo exige registrar el publisher en Orders **y** un exchange donde publicar, que `definitions.json` todavía no tiene.

### 2. El consumer abre una conexión nueva por segundo

`ConsumerHostedService` repite `await _messageConsumer.StartAsync()` en un `while`. Pero `RabbitMQMessageConsumer.Consume` solo abre conexión, llama a `BasicConsume` y **retorna enseguida**, así que el bucle no espera y vuelve a abrir otra conexión:

| Tiempo | Conexiones | Consumidores en `subscription-queue` |
|---|---|---|
| Arranque | 21 | 20 |
| +5 s | 27 | 26 |
| +10 s | 32 | 31 |
| +15 s | 37 | 36 |

Funciona porque RabbitMQ reparte cada mensaje a uno de esos consumidores, todos del mismo proceso, pero las conexiones crecen sin límite mientras el proceso vive. El código lo marca con `#5` y `#6`. Sigue igual en el `main` del original y en el de Scattered.

### 3. Si RabbitMQ no está al arrancar, el consumer muere en silencio

Si `CreateConnection` lanza una excepción que no es `OperationCanceledException`, el bucle termina y nadie lo registra (comentario `#3` del código). Verificado:

1. Consumer arrancado con RabbitMQ parado: `Now listening`, sin errores en el log.
2. RabbitMQ arriba otra vez: `consumers = 0`.
3. `POST /subscribe` → el mensaje se queda en la cola (`msgs = 1`).
4. `PUT /integrationconsumer/start` → `200`, pero sigue `consumers = 0`: el reinicio solo cambia un token, el bucle ya no existe.
5. Solo reiniciando el **proceso** se procesa el mensaje pendiente. El mensaje no se pierde.

Si RabbitMQ se cae **después** de arrancar, las conexiones que ya existían se recuperan solas (*automatic recovery* de `RabbitMQ.Client`) y el consumer sigue funcionando, aunque el bucle haya muerto.

### 4. Otros detalles

- **Sin validación en `/subscribe`.** Un body sin `email` se publica y se procesa con el email vacío (paso 9).
- **La dead-letter queue no se usa todavía.** El receiver nunca hace `BasicNack`/`BasicReject`: si un handler lanza una excepción, el mensaje queda sin confirmar y vuelve a la cola al cerrarse el canal; no llega a `subscription-queue.dead-letter`.
- **`IDomainMessageHandler<T>` hereda de `IMessageHandler<IntegrationMessage<T>>`** en lugar de `DomainMessage<T>`. No afecta todavía (no hay mensajes de dominio); el original lo corrige en el capítulo 6–7 (*"fix a bug on domainhandlers"*).
- **Una conexión por publicación.** `RabbitMQMessagePublisher` abre y cierra conexión y canal en cada `Publish`. Es correcto pero costoso; lo habitual es reutilizar la conexión.

## Particularidades

- **Usuario declarado en `definitions.json` (cambio propio).** Con la imagen `rabbitmq:3.9-management-alpine` actual, cuando hay definiciones que cargar RabbitMQ no crea el usuario por defecto (*"Will not seed default virtual host and user: have definitions to load"*), y como el `definitions.json` original tenía `"users": []`, el broker arrancaba **sin usuarios**: ni la UI ni las aplicaciones podían conectarse (`Login failed`). Se añade `ScatteredAdmin` (hash SHA-256 con sal, `administrator`) y sus permisos sobre `/`. Las variables `RABBITMQ_DEFAULT_*` se mantienen por fidelidad, aunque no tienen efecto. El original lo resolvió más tarde (junio de 2022) pasando a `rabbitmq.conf` con `default_user` y a la imagen 3.8.34, que es lo que usa `main`.
- **RabbitMQ 3.9 está fuera de soporte.** Su log lo avisa (*"This release series has reached end of life"*). Se mantiene por fidelidad al capítulo.
- **El contenedor `rabbitmq` es el mismo que el de `main`.** Ambos usan `container_name: rabbitmq` con configuraciones distintas. Al cambiar de rama, `docker compose up -d --force-recreate rabbitmq` lo recrea con la configuración de la rama actual. El aviso *"Found orphan containers (graylog, vault, …)"* viene de los contenedores de `main` y se puede ignorar.
- **Paquetes de 2022 con vulnerabilidades transitivas.** `dotnet list package --vulnerable --include-transitive` señala `System.Text.Json` 6.0.0, `System.Net.Http` 4.3.0, `Newtonsoft.Json` 11.0.2 (arrastrado por `Microsoft.AspNetCore.Mvc` 2.2.0 en `Shared.Communication`) y `Swashbuckle.AspNetCore.SwaggerUI` 6.1.5, entre otros. No rompen la compilación porque en `net6.0` NuGet solo audita las dependencias directas. `main` los actualiza; aquí se mantienen por fidelidad.
- **El consumer no sigue el esquema de puertos.** Usa `7073` / `5073` en lugar de `50X20` / `50X10`, igual que el original.
- **Logs detallados en zsh.** Los nombres con puntos (`Logging__LogLevel__Microsoft.AspNetCore`) no se pueden asignar en zsh directamente; se pasan con `env`, como en el [capítulo 2](flow2.md#arrancar-con-logs-detallados).
- Siguen vigentes las particularidades de los capítulos [1](flow1.md#particularidades) y [2](flow2.md#particularidades).
