# Capítulo 7 · Logs centralizados con Serilog y Graylog (`features/flow6`)

Con 8 procesos repartidos, buscar un error mirando la consola de cada uno deja de ser viable. En este capítulo los servicios empiezan a usar **Serilog** y pueden enviar sus logs a **Graylog**, una plataforma que los recibe de todos los procesos, los guarda en Elasticsearch y permite buscarlos desde una sola web. De paso, la rama arregla **Orders**: deja de devolver 500, publica un **mensaje de dominio** al crear un pedido y estrena su propio consumer.

<!-- nav:start -->
[← Capítulo 6](flow5.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 8 →](flow7.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · **7** · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · 13 · 14 · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow6`)](#flujo-end-to-end-featuresflow6) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`07-Logs-en-la-nube`](https://github.com/ElectNewt/Distribt/tree/07-Logs-en-la-nube) |
| Commits de origen | `f841b78` add orders consumer and example on create order and fix a bug on domainhandlers · `e9fa683` cleanump config · `53d0830` add log configuration (27-feb-2022) |
| Diff respecto al capítulo 6 | 3 commits, 36 archivos, +408 / −38 líneas |
| Artículo | [Sistema de logs con Graylog y Serilog](https://www.netmentor.es/entrada/servicio-logs-graylog) |
| Cambios propios | Ninguno en el código: el diff del original aplica limpio. Solo documentación (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow5 -- src tools docker-compose.yaml Scattered.sln`.

## Resumen

```text
                                 ┌──────────────── Graylog (:9000 web · :12201 GELF) ────────────────┐
                                 │ graylog ──▶ elasticserach_graylog (logs) · mongo_graylog (config)  │
                                 └──────────────────────────▲────────────────────────────────────────┘
                                                            │ GELF por UDP (solo Error, solo Orders)
Cliente ──POST /order-ms/order──▶ API Public ──YARP──▶ Orders (:50220) ──[ERR]──▶ consola
                                                            │ DomainMessage<OrderDto>, routing key "order"
                                                            ▼
                                 RabbitMQ: order.exchange ──▶ order-queue ──▶ Orders.Consumer (:7225)
                                                                               "Order {id} created"
```

Dos historias en una rama:

1. **Logs.** `DefaultScatteredWebApplication` sustituye el logger de .NET por Serilog, que escribe en los destinos (*sinks*) que diga la configuración: consola y/o Graylog. En esta rama **solo Orders** tiene esa configuración, y solo para errores.
2. **Orders y los mensajes de dominio.** Hasta ahora todo el tráfico de RabbitMQ era de **integración** (entre servicios: el gateway publica, Subscriptions consume). Orders estrena el otro tipo, los mensajes de **dominio**: los publica y los consume el propio Orders, en dos procesos distintos (API y consumer).

## Qué cambió

```text
docker-compose.yaml                          + mongo (mongo_graylog), elasticsearch (elasticserach_graylog), graylog
tools/rabbitmq/definitions.json              + order.exchange (topic) → order-queue (+ order-queue.dead-letter)
Scattered.sln                                + Orders.Consumer, Orders.Dto, Products.Dto, Shared.Logging
src/
├── Shared/
│   ├── Scattered.Shared.Logging             NUEVO: ConfigureSerilog() + sinks Console y Graylog según configuración
│   ├── Scattered.Shared.Setup               DefaultScatteredWebApplication usa Serilog; + referencia a Logging
│   └── Shared.Communication/…/IMessageHandler.cs   ARREGLO: IDomainMessageHandler<T> manejaba IntegrationMessage<T>
├── Services/Orders/
│   ├── Scattered.Services.Orders            OrderController reescrito: GET con log de ejemplo, POST publica dominio (202)
│   │   ├── Program.cs                       + AddServiceBusDomainPublisher()
│   │   └── appsettings.json                 + Logging:Console/Graylog (Error) + Bus:RabbitMQ:Publisher:DomainExchange
│   ├── Scattered.Services.Orders.Consumer   NUEVO (:7225): consume order-queue con OrderCreatedHandler
│   └── Scattered.Services.Orders.Dto        NUEVO: OrderDto, CreateOrderDto, OrderAddress, PersonalDetails
├── Services/Products/Scattered.Services.Products.Dto   NUEVO: ProductDto(productId, quantity)
├── Api/Public/…/appsettings.json            − Bus:RabbitMQ:Hostname (ya llegaba de Consul)
├── Services/Subscriptions/…Consumer/appsettings.json   − Bus:RabbitMQ:Hostname
└── */Properties/launchSettings.json         + VAULT-TOKEN en API Private, Emails, Orders y Products
```

### 1. Infraestructura: Graylog y sus dos dependencias

**`docker-compose.yaml`** suma tres contenedores:

| Servicio (contenedor) | Imagen | Puertos | Para qué |
|---|---|---|---|
| `mongo` (`mongo_graylog`) | `mongo:4.2` | ninguno publicado | Configuración de Graylog: usuarios, *inputs*, *streams*. No guarda logs |
| `elasticsearch` (`elasticserach_graylog`) | `elasticsearch-oss:7.10.2` | ninguno publicado | Guarda e indexa los logs. Heap de 512 MB, límite de 1 GB |
| `graylog` | `graylog/graylog:4.2.5` | `9000` web/API · `12201` GELF TCP/UDP · `1514` syslog TCP/UDP | Recibe, procesa y permite buscar los logs |

```yaml
  graylog:
    image: graylog/graylog:4.2.5
    environment:
      - GRAYLOG_PASSWORD_SECRET=thispassshouldbeatleast16characters
      - GRAYLOG_ROOT_PASSWORD_SHA2=8c6976e5…    # sha256("admin")
      - GRAYLOG_HTTP_EXTERNAL_URI=http://localhost:9000/
    entrypoint: /usr/bin/tini -- wait-for-it elasticsearch:9200 --  /docker-entrypoint.sh
```

- El usuario de la web es **`admin` / `admin`**: la variable guarda el SHA-256 de la contraseña, no la contraseña.
- `wait-for-it elasticsearch:9200` hace que Graylog espere a que Elasticsearch responda antes de arrancar. Graylog y Elasticsearch se encuentran por la red de compose, por el **nombre del servicio** (`mongo`, `elasticsearch`); de ahí el comentario *"mongo should be called mongo"*.
- **Graylog arranca sin ningún *input*.** Los puertos `12201`/`1514` están publicados, pero hasta que no se crea un input en la web (*System → Inputs → GELF UDP*), lo que llega se descarta. El original no lo automatiza; lo explica el artículo.

**`tools/rabbitmq/definitions.json`**: nuevo `order.exchange` (tipo `topic`) enlazado a `order-queue` con la clave `order`, y la cola `order-queue.dead-letter`. La política `DLX` existente (patrón `.*`) también aplica a `order-queue`, pero falta el enlace de `dead-letter.exchange` a `order-queue.dead-letter`, así que un mensaje rechazado se perdería.

### 2. `Scattered.Shared.Logging`: Serilog configurable

Proyecto nuevo con `Serilog` 2.10.0, `Serilog.AspNetCore` 5.0.0, `Serilog.Sinks.Console` 4.0.1 y `Serilog.Sinks.Graylog` 2.3.0.

```csharp
public static IHostBuilder ConfigureSerilog(this IHostBuilder builder)
    => builder.UseSerilog((context, loggerConfiguration)
        => ConfigureSerilogLogger(loggerConfiguration, context.Configuration));

// Lee Logging:Graylog y Logging:Console y añade cada sink solo si Enabled = true
return loggerConfiguration
        .AddConsoleLogger(consoleLogger)      // WriteTo.Console(MinimumLevel)
        .AddGraylogLogger(graylogLogger);     // WriteTo.Graylog(Host, Port, TransportType.Udp, MinimumLevel)
```

| Clase | Propiedades (de `appsettings`) |
|---|---|
| `ConsoleLoggerConfiguration` | `Enabled` (por defecto `false`), `MinimumLevel` |
| `GraylogLoggerConfiguration` | `Enabled` (por defecto `false`), `Host`, `Port`, `MinimumLevel` |

El envío a Graylog usa **GELF sobre UDP**: el servicio lanza el paquete y no espera respuesta. Es rápido y nunca bloquea una petición, pero si Graylog no está o no tiene input, el log se pierde sin aviso.

### 3. El arranque común usa Serilog

```diff
 WebApplicationBuilder builder = WebApplication.CreateBuilder();
-
+builder.Host.ConfigureSerilog();
 …
 builder.Services.AddServiceDiscovery(builder.Configuration);
 builder.Services.AddSecretManager(builder.Configuration);
+builder.Services.AddLogging(logger => logger.AddSerilog());
```

`UseSerilog` **reemplaza** al logger de .NET en los 8 procesos. La consecuencia, que se ve al arrancar: los servicios sin la sección `Logging:Console` **dejan de escribir nada en consola**, ni siquiera *"Now listening on…"*, porque Serilog no tiene ningún sink activo. La sección `Logging:LogLevel` de los demás `appsettings.json` ya no tiene efecto. Lo único que sigue apareciendo son los `Console.WriteLine` de los handlers (`Email … successfully subscribed.`, `Order … created`), que no pasan por el logger.

### 4. Orders: mensajes de dominio y su consumer

**`OrderController`** se reescribe:

| Endpoint | Antes (cap. 3–6) | Ahora |
|---|---|---|
| `GET /order/{id}` | Publicaba un mensaje de integración que nadie había registrado → 500 | Escribe un `LogError` de ejemplo y devuelve un `OrderDto` inventado (`"stree1"`, `"name"`…) |
| `POST /order` | Devolvía un `Guid` nuevo | `CreateOrder(CreateOrderDto)`: publica `DomainMessage<OrderDto>` en `order.exchange` con clave `order` y responde **202 Accepted** con `Location: getorderstatus/{id}` |
| `GET /order/getorderstatus/{id}` | — | `NotImplementedException` → 500 |

```csharp
[HttpPost(Name = "createorder")]
public async Task<ActionResult<Guid>> CreateOrder(CreateOrderDto createOrder)
{
    OrderDto orderDto = new OrderDto(Guid.NewGuid(), createOrder.orderAddress, createOrder.PersonalDetails,
        createOrder.Products);
    await _domainMessagePublisher.Publish(orderDto, routingKey: "order");
    return new AcceptedResult($"getorderstatus/{orderDto.orderId}", orderDto.orderId);
}
```

`202 Accepted` es la respuesta honesta para una operación asíncrona: "lo he recibido, se procesará". El `Location` indica dónde consultar el estado, aunque ese endpoint aún no existe.

**`Program.cs`** de Orders registra `AddServiceBusDomainPublisher()`, que usa las credenciales **estáticas** de Vault (secreto KV) y el host de RabbitMQ de Consul. Con eso desaparece el 500 heredado del capítulo 3–4.

**`Scattered.Services.Orders.Consumer`** (nuevo, `:7225`) es el gemelo de `Subscriptions.Consumer` para dominio:

```csharp
WebApplication app = DefaultScatteredWebApplication.Create(x =>
{
    x.Services.AddHandlers(new List<IMessageHandler>() { new OrderCreatedHandler() });
    x.Services.AddServiceBusDomainConsumer(x.Configuration);   // lee Bus:RabbitMQ:Consumer:DomainQueue = order-queue
});
```

`OrderCreatedHandler : IDomainMessageHandler<OrderDto>` escribe `Order {id} created`. Expone también `IntegrationConsumerController` (heredado de la plantilla), aunque este proceso solo consume dominio.

**Arreglo en `IMessageHandler.cs`**: `IDomainMessageHandler<TMessage>` heredaba de `IMessageHandler<IntegrationMessage<TMessage>>`. Un handler de dominio declaraba manejar mensajes de integración, así que el registro de handlers nunca lo emparejaba con un `DomainMessage<T>`. Era un error latente porque, hasta este capítulo, nadie consumía dominio.

| | Integración | Dominio |
|---|---|---|
| Para qué | Avisar a **otros** servicios | Comunicar partes del **mismo** servicio |
| Ejemplo | Gateway → Subscriptions | Orders API → Orders.Consumer |
| Exchange / cola | `api.public.exchange` → `subscription.exchange` → `subscription-queue` | `order.exchange` → `order-queue` |
| Tipo de mensaje | `IntegrationMessage<T>` | `DomainMessage<T>` |
| Credenciales de RabbitMQ | Gateway: usuario dinámico de Vault | Secreto estático de Vault (KV) |

### 5. DTOs compartidos

- **`Scattered.Services.Orders.Dto`**: `OrderDto(orderId, orderAddress, PersonalDetails, Products)`, `CreateOrderDto` (lo mismo sin id), `OrderAddress(street, postalCode)`, `PersonalDetails(name, surname)`. Sustituye al `record OrderDto(Guid orderId)` que vivía dentro del controller con un `//TODO: move`.
- **`Scattered.Services.Products.Dto`**: `ProductDto(productId, quantity)`, referenciado por `Orders.Dto`. Es la primera dependencia entre proyectos de dos dominios distintos.

### 6. Configuración

- **Orders** (`appsettings.json`): `Logging:Console` y `Logging:Graylog` activos con `MinimumLevel: Error` (Graylog en `localhost:12201`) y `Bus:RabbitMQ:Publisher:DomainExchange: order.exchange`.
- **Orders.Consumer**: `Bus:RabbitMQ:Consumer:DomainQueue: order-queue` y `Discovery:Address`. Trae `Hostname: localhost`, que se ignora (llega de Consul).
- **API Public y Subscriptions.Consumer** quitan `Bus:RabbitMQ:Hostname`, que desde el capítulo 6 ya no se leía (*cleanup config*).
- **`VAULT-TOKEN`** se añade al perfil de API Private, Emails, Orders (también al de IIS Express) y Products. Orders y Orders.Consumer lo necesitan para leer el secreto KV; los demás no lo usan.

## Lo que no cambia

- **Vault, Consul y el flujo de suscripciones** (`POST /subscribe` → `subscription-queue` → Subscriptions.Consumer).
- **Las rutas de YARP**: Orders sigue llegando por `/order-ms/*` y `/reports/*` con su dirección fija.
- **Products, Emails y la API de Subscriptions**.
- **La fuga de conexiones** de los consumers: ahora son dos, así que crece al doble de ritmo.

## Cómo ejecutarla

Ahora hacen falta RabbitMQ, Vault, Consul y los tres contenedores de Graylog:

```bash
./tools/local-development/up.sh          # docker-compose up -d + config de Vault y Consul
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders.Consumer
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
```

Graylog tarda en arrancar (espera a Elasticsearch). Cuando la web responda en http://localhost:9000 (`admin` / `admin`), **crea el input** una vez: *System → Inputs → GELF UDP → Launch new input*, marca *Global* y deja el puerto `12201`. O por la API:

```bash
curl -u admin:admin -H 'Content-Type: application/json' -H 'X-Requested-By: cli' \
  -X POST localhost:9000/api/system/inputs \
  -d '{"title":"GELF UDP","type":"org.graylog2.inputs.gelf.udp.GELFUDPInput","global":true,
       "configuration":{"bind_address":"0.0.0.0","port":12201,"recv_buffer_size":262144}}'
```

El input se guarda en `mongo_graylog`, así que sobrevive a reinicios de `graylog`. Si `definitions.json` cambió y RabbitMQ ya estaba corriendo, reinícialo (`docker restart rabbitmq`) para que cree `order.exchange` y `order-queue`.

| Qué | Dónde |
|---|---|
| Web de Graylog | http://localhost:9000 (`admin` / `admin`) → *Search* |
| Mensajes recibidos por el input | *System → Inputs → GELF UDP* (contador *Network IO*) |
| Cola de pedidos | http://localhost:15672 → *Queues* → `order-queue` |
| Crear un pedido | `curl -k -X POST https://localhost:7283/order-ms/order -H 'Content-Type: application/json' -d '{"orderAddress":{"street":"Calle Mayor 1","postalCode":"28001"},"personalDetails":{"name":"Ana","surname":"García"},"products":[{"productId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","quantity":2}]}'` |

Para detener todo: `pkill -f 'Scattered\.(API|Services)'` y `docker compose stop`.

## Flujo end to end (`features/flow6`)

> **Rama:** `features/flow6`, equivalente a [`07-Logs-en-la-nube`](https://github.com/ElectNewt/Distribt/tree/07-Logs-en-la-nube) del original (`53d0830`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-19 con los 6 contenedores (RabbitMQ, Vault, Consul y los tres de Graylog) y los 8 procesos arrancados.

### Las llamadas

```bash
G=3fa85f64-5717-4562-b3fc-2c963f66afa6

# 1. Arrancar los 8 procesos y mirar sus consolas
# 2–6. Sin input en Graylog: suscripción, Orders (GET, privado, POST, estado) y Products
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
curl -k https://localhost:7283/order-ms/order/$G
curl -k https://localhost:7022/reports/order/$G
curl -k -X POST https://localhost:7283/order-ms/order -H 'Content-Type: application/json' -d '{"orderAddress":{…},"personalDetails":{…},"products":[…]}'
curl -k https://localhost:7283/order-ms/order/getorderstatus/$G

# 7–8. Crear el input GELF UDP y repetir
curl -u admin:admin … -X POST localhost:9000/api/system/inputs -d '{…GELFUDPInput…}'
curl 'localhost:9000/api/search/universal/relative?query=*&range=3600' -u admin:admin -H 'Accept: application/json'

# 9–12. Graylog parado, Orders.Consumer parado, seguir el Location del 202, cuerpo vacío
docker stop graylog
pkill -f Scattered.Services.Orders.Consumer
curl -k -X POST https://localhost:7283/order-ms/order -H 'Content-Type: application/json' -d '{}'
```

### Qué ocurre en cada parte

| # | Acción | Respuesta | Orders (API) | Orders.Consumer | Graylog | RabbitMQ |
|---|---|---|---|---|---|---|
| 1 | Arrancar los 8 procesos | 8/8 escuchando | **Consola vacía**: Serilog sin sink de consola por debajo de Error | Consola vacía | Sano, **0 inputs** | `order-queue` creada (tras reiniciar RabbitMQ) |
| 2 | `POST /subscribe` | `200` | — | — | — | Subscriptions.Consumer: `Email cliente@… successfully subscribed.` |
| 3 | `GET /order-ms/order/{id}` y `GET :7022/reports/order/{id}` | **`200`** con un pedido inventado (`"stree1"`) | `[ERR] esto es un mensaje de ejemplo con el order …` en consola, una vez por llamada | — | Recibe el paquete UDP y lo **descarta**: no hay input | — |
| 4 | `POST /order-ms/order` | **`202`** `"d92d6e02-…"`, `Location: getorderstatus/d92d6e02-…` | Publica `DomainMessage<OrderDto>` | `Order d92d6e02-… created` | — | `order.exchange` → `order-queue` → consumido |
| 5 | `GET /order-ms/order/getorderstatus/{id}` | **`500`** `NotImplementedException` | `[ERR] An unhandled exception has occurred…` + traza | — | Descartado | — |
| 6 | `GET /product-ms/product/{id}` | `200` | — | — | — | — |
| 7 | Crear input GELF UDP (API) | `{"id":"6aff…"}` | — | — | Input `RUNNING`… pero recibe **0** mensajes desde el host (ver [Particularidades](#particularidades)) | — |
| 8 | `docker restart graylog` + repetir 3 y 5 | `200` · `500` | Igual | — | **Llegan 2 mensajes**, `level 3` (Error), con campos `SourceContext`, `RequestPath`, `ExceptionType`, `StackTrace`, `RequestId`… | — |
| 9 | `docker stop graylog` + `GET order` | `200` (63 ms) | Log en consola | — | Caído: el log se **pierde sin aviso** | — |
| 10 | Parar Orders.Consumer + 2 × `POST order` | `202` · `202` | Publica | Parado | — | Los 2 pedidos **esperan** en `order-queue` |
| 11 | Arrancar Orders.Consumer | — | — | `Order 8d6598c2-… created` y `Order ff74e7b2-… created` | — | Cola vacía |
| 12 | Seguir el `Location` del 202 desde el gateway | **`404`** | — | — | — | `getorderstatus/{id}` relativo a `/order-ms/order` resuelve a `/order-ms/getorderstatus/{id}` |
| 13 | `POST /order-ms/order` con `{}` | `400` | Validación automática: `Products`, `orderAddress`, `PersonalDetails` requeridos | — | — | — |
| 14 | Conexiones tras unos minutos | — | — | — | — | **264** conexiones `ScatteredAdmin`: ahora hay dos consumers con la fuga |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| **Orders ya funciona** | Pasos 3, 4 y 8 | Desaparece el 500 heredado desde el capítulo 3–4 |
| Primer mensaje de **dominio** de punta a punta | Pasos 4, 10 y 11 | Mismo patrón que las suscripciones, pero dentro de un servicio: API publica, consumer procesa, la cola amortigua |
| **Serilog silencia las consolas** | Paso 1 | Sin `Logging:Console`, un proceso no escribe nada, ni el puerto en el que escucha. Solo Orders tiene la sección |
| Graylog **no recibe nada sin input** | Pasos 3 y 7 | El puerto está abierto, pero los paquetes se descartan. Hay que crear el input a mano una vez |
| Los logs llegan **estructurados** | Paso 8 | Además del texto, Graylog recibe `SourceContext` (clase), `RequestPath`, `ExceptionType`, `StackTrace`… y se puede filtrar por cualquiera |
| Ningún campo identifica al **servicio** | Paso 8 | `source` es el nombre de la máquina; si todos los servicios enviaran logs, solo `SourceContext` (el namespace) permitiría distinguirlos |
| UDP: rápido y sin garantías | Paso 9 | Graylog caído no afecta a la petición, pero los logs de ese rato se pierden |
| El `Location` del 202 no sirve | Paso 12 | Es relativo y sin el segmento `order/`, y además el endpoint de estado no está implementado |

## Problemas heredados del original

### 1. Solo Orders configura el logging

Los otros 7 procesos no tienen `Logging:Console` ni `Logging:Graylog`, y Serilog sustituye al logger de .NET en todos. Resultado: **no escriben nada** ni en consola ni en Graylog, incluidos los errores no controlados. Antes del capítulo, al menos la consola mostraba los avisos de ASP.NET Core.

### 2. El input de Graylog es manual

Graylog arranca sin inputs y el repositorio no tiene un `config.sh` que lo cree, como sí lo tienen Vault y Consul. Hasta crearlo, todo lo que envía Orders se pierde en silencio. Una vez creado se guarda en `mongo_graylog` y sobrevive a los reinicios, pero no a recrear ese contenedor.

### 3. Logs con interpolación de cadenas

`_logger.LogError($"esto es un mensaje de ejemplo con el order {orderId}")` construye el texto antes de llamar al logger. Serilog recibe un mensaje plano, así que en Graylog no existe un campo `orderId` por el que filtrar. Con una plantilla (`LogError("… {OrderId}", orderId)`) sí existiría. Además es un `LogError` para algo que no es un error, puesto ahí solo para probar el envío.

### 4. Orders a medias

- `GET /order/{id}` devuelve datos inventados.
- `GET /order/getorderstatus/{id}` lanza `NotImplementedException`.
- El `Location` del 202 es relativo y le falta el segmento `order/`: desde el gateway resuelve a una ruta que da 404.
- `order-queue.dead-letter` existe, pero `dead-letter.exchange` no tiene enlace hacia ella con la clave `order`.

### 5. Otros detalles

- **`VAULT-TOKEN` en servicios que no lo usan** (API Private, Emails, Products).
- **`Hostname: localhost` en Orders.Consumer**, que se ignora desde el capítulo 6, justo en el commit que limpia esa clave de los demás.
- **`Products.Dto` sin `TreatWarningsAsErrors`**, a diferencia del resto de proyectos.
- **`Orders.Consumer` expone `IntegrationConsumerController`**, aunque solo consume dominio.
- Siguen los problemas de los capítulos [3–4](flow3.md#problemas-heredados-del-original), [5](flow4.md#problemas-heredados-del-original) y [6](flow5.md#problemas-heredados-del-original). Orders deja de estar roto, pero la fuga de conexiones se duplica.

## Particularidades

- **Sin cambios propios en el código.** El diff del original, renombrado, aplica limpio sobre `features/flow5`. Los paquetes nuevos (Serilog y sus sinks) no tienen vulnerabilidades conocidas.
- **`graylog/graylog:4.2.5` solo existe para amd64.** En un Mac con Apple Silicon corre emulado (Docker avisa: *"The requested image's platform (linux/amd64) does not match the detected host platform"*). Funciona, pero tarda más en arrancar. `mongo:4.2` y `elasticsearch-oss:7.10.2` tienen versión arm64.
- **UDP 12201 desde el host.** En esta ejecución, tras crear el contenedor `graylog` con `docker compose up -d`, Docker Desktop publicó el `12201/tcp` y el `1514/udp`, pero **no el `12201/udp`**: los paquetes enviados desde el Mac no llegaban, mientras que desde la red de compose sí. Un `docker restart graylog` lo resolvió. Para comprobarlo: `lsof -nP -iUDP:12201` debe mostrar `com.docker.backend`.
- **Contenedores compartidos con `main`.** `mongo_graylog` y `elasticserach_graylog` ya existían (creados con el compose de `main`) y, al tener la misma configuración, se reutilizan con sus datos. `graylog` se recrea porque `main` lo define distinto.
- Siguen vigentes las particularidades de los capítulos [1](flow1.md#particularidades), [2](flow2.md#particularidades), [3–4](flow3.md#particularidades), [5](flow4.md#particularidades) y [6](flow5.md#particularidades).
