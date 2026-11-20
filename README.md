# Scattered

![Scattered](assets/scattered-logo.svg)

> **Nota:** Scattered es una copia renombrada de [Distribt](https://github.com/ElectNewt/Distribt), proyecto creado por Ivan Abad ([NetMentor](https://www.netmentor.es)) y publicado bajo licencia MIT. El curso y los artículos enlazados en este documento pertenecen al autor original.
>
> **Esta rama (`features/flow6`)** replica la rama [`07-Logs-en-la-nube`](https://github.com/ElectNewt/Distribt/tree/07-Logs-en-la-nube) del proyecto original (commit `53d0830`), renombrada de Distribt a Scattered. Cada capítulo del curso se replica en su propia rama `features/flowN`; la guía de cada uno, con qué cambió y cómo ejecutarlo, está en **[docs/flow/README.md](docs/flow/README.md)**.

## Estado de esta rama

| | |
|---|---|
| Rama | `features/flow6` (nace de `features/flow5`) |
| Capítulo del curso | 7 · [Sistema de logs](https://www.netmentor.es/entrada/servicio-logs-graylog) con Graylog y Serilog |
| Rama original | [`07-Logs-en-la-nube`](https://github.com/ElectNewt/Distribt/tree/07-Logs-en-la-nube) (`53d0830`) |
| Qué hay | 2 API Gateways con YARP; suscripciones por RabbitMQ (integración) y **pedidos por RabbitMQ (dominio)** con su propio consumer. Credenciales en Vault, direcciones en Consul y **logs con Serilog hacia Graylog** |
| Qué falta | Persistencia (CQRS, cap. 8), health checks y observabilidad. Solo Orders envía logs; el resto de servicios aún no los configura |
| Infraestructura | RabbitMQ, Vault, Consul y Graylog (+ MongoDB y Elasticsearch) en Docker: `./tools/local-development/up.sh` |
| Guía del capítulo | [docs/flow/flow6.md](docs/flow/flow6.md) |
| Siguiente capítulo | 8 · [CQRS](https://www.netmentor.es/entrada/patron-cqrs-explicado-10-minutos) para separar lecturas de escrituras (`08-CQRS`) |

## Qué cambió en este capítulo

Respecto a `features/flow5` ([capítulo 6](docs/flow/flow5.md)) llegan **los logs centralizados**: el arranque común sustituye el logger de .NET por **Serilog**, que puede escribir en consola y enviar por GELF/UDP a **Graylog**, donde se buscan los logs de todos los procesos desde una web. La rama también **arregla Orders**, que deja de devolver 500 y estrena los **mensajes de dominio**.

| Cambio | Dónde | Para qué |
|---|---|---|
| Contenedores `graylog`, `mongo_graylog` y `elasticserach_graylog` | `docker-compose.yaml` | Recibir (`:12201` GELF), guardar (Elasticsearch) y buscar (`:9000`) logs |
| `Scattered.Shared.Logging` (nuevo) | `src/Shared/` | `ConfigureSerilog()`: sinks de consola y Graylog activables desde `Logging:Console` / `Logging:Graylog` |
| `UseSerilog` en el arranque común | `DefaultScatteredWebApplication` | Los 8 procesos usan Serilog. Los que no lo configuran dejan de escribir en consola |
| `OrderController` reescrito + `AddServiceBusDomainPublisher` | `Scattered.Services.Orders` | `POST` publica `DomainMessage<OrderDto>` y responde **202**; `GET` escribe un `LogError` de ejemplo |
| `Scattered.Services.Orders.Consumer` (nuevo, `:7225`) | `src/Services/Orders/` | Consume `order-queue` con `OrderCreatedHandler` |
| `order.exchange` → `order-queue` | `tools/rabbitmq/definitions.json` | Topología para los mensajes de dominio de Orders |
| `Orders.Dto` y `Products.Dto` (nuevos) | `src/Services/` | `OrderDto`, `CreateOrderDto`, `OrderAddress`, `PersonalDetails`, `ProductDto` |
| `IDomainMessageHandler<T>` corregido | `Shared.Communication` | Manejaba `IntegrationMessage<T>` por error; sin el arreglo, el consumer de dominio no encontraría su handler |

```text
Antes (capítulo 6)                               Ahora (capítulo 7)
Logs: ILogger de .NET → consola de cada proceso  Logs: Serilog → consola (si Logging:Console) + Graylog (si Logging:Graylog)
                                                        Orders ──GELF/UDP──▶ Graylog (:9000) ──▶ Elasticsearch
Orders: GET/POST → 500 (falta publisher)         Orders: POST → 202 ──DomainMessage──▶ order.exchange → order-queue
                                                                                         └─▶ Orders.Consumer (:7225)
```

Lo que conviene saber al probarlo: Graylog **arranca sin input**, así que hasta crear el *GELF UDP* en *System → Inputs* (o con el `curl` de la guía) los logs se descartan; solo Orders tiene configurado Serilog, y solo para errores, de modo que el resto de procesos **no escribe nada en consola**; y `graylog/graylog:4.2.5` corre emulado en Apple Silicon. El diff del original aplica limpio: no hay cambios propios en el código. Detalle de cada cambio y el flujo end to end de 14 pasos en **[docs/flow/flow6.md](docs/flow/flow6.md)**.

## ¿Qué es Scattered?

Scattered es un proyecto Open Source creado para mostrar el funcionamiento y uso de los sistemas distribuidos con las implementaciones en .NET Core.

La aplicación va a ser "vendor free" lo que quiere decir que no va a estar enlazada directamente a ningún proveedor específico. Obviamente vamos a utilizar X o Y servicio (ya que no vamos a reinventar la rueda), pero lo haremos a través de abstracciones.

Es importante saber estos conceptos ya que cada día las empresas están migrando sus aplicaciones monolíticas a microservicios o incluso serverless, y para poder aplicar un correcto funcionamiento, debemos aprender sobre sistemas distribuidos.

## ¿Qué vamos a ver?

Vamos a ver un sistema distribuido con múltiples características, como pueden ser patrón consumer/publisher, sagas, service discovery, Eventual consistency, etc.

Esta es la arquitectura planteada en el capítulo 1, marcada con lo que ya existe en esta rama (`features/flow6`):

![ScatteredDiagram](assets/diagram.png)

- Las cajas con borde continuo existen en el código; las de borde discontinuo son el objetivo y todavía no existen.
- Las flechas continuas son conexiones que ya funcionan: grises para HTTP (rutas de los gateways), ámbar para mensajes por RabbitMQ, violetas para la lectura de secretos en Vault, verdes para las consultas a Consul y rosas para el envío de logs a Graylog. Las discontinuas son conexiones planificadas.
- Las etiquetas amarillas representan el código ya implementado; las grises indican en qué capítulo llega cada pieza.
- La fuente editable del diagrama es [`assets/architecture.svg`](assets/architecture.svg).

Puedes encontrar el proceso de creación del sistema en [el curso de YouTube del autor original](https://www.youtube.com/playlist?list=PLesmOrW3mp4jpSbdFMtVWINJZ7OLdSASS).

Alternativamente, si lo prefieres, puedes seguir los post con el código detallado y los razonamientos sobre por qué se ha elegido cada tecnología en la web del autor original, [NetMentor - Curso Distribt](https://www.netmentor.es/curso/sistemas-distribuidos).

## Progreso del curso

<!-- progress:start (generado por tools/docs/progress.py desde docs/flow/chapters.json; no editar a mano) -->
Cada capítulo se replica en su propia rama `features/flowN` y tiene su guía en `docs/flow`. **7 de 17 capítulos** replicados.

- [x] 1 · [API Rest](https://www.netmentor.es/entrada/api-rest-csharp): esqueleto de microservicios creados con .NET · [guía](docs/flow/flow1.md)
- [x] 2 · [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) con YARP · [guía](docs/flow/flow2.md)
- [x] 3–4 · [Producers/Consumers](https://www.netmentor.es/entrada/patron-productor-consumidor) para la comunicación asíncrona [con RabbitMQ](https://www.netmentor.es/entrada/rabbitmq-comunicacion-asincrona) · [guía](docs/flow/flow3.md)
- [x] 5 · [Gestión de credenciales](https://www.netmentor.es/entrada/gestion-credenciales-vault) con Vault · [guía](docs/flow/flow4.md)
- [x] 6 · [Service discovery / registry](https://www.netmentor.es/entrada/service-registry-discovery-consul) con Consul · [guía](docs/flow/flow5.md)
- [x] 7 · [Sistema de logs](https://www.netmentor.es/entrada/servicio-logs-graylog) con Graylog y Serilog · [guía](docs/flow/flow6.md) **← esta rama**
- [ ] 8 · [CQRS](https://www.netmentor.es/entrada/patron-cqrs-explicado-10-minutos) para separar lecturas de escrituras
- [ ] 9 · [Event Sourcing](https://www.netmentor.es/entrada/event-sourcing-explicado-facil) con MongoDB
- [ ] 10 · [Consistencia eventual](https://www.netmentor.es/entrada/consistencia-eventual-microservicios) entre microservicios
- [ ] 11 · [Unificación de respuestas de API](https://www.netmentor.es/entrada/unificar-respuestas-api) con [ROP](https://www.netmentor.es/entrada/railway-oriented-programming)
- [ ] 12 · [Patrón SAGA](https://www.netmentor.es/entrada/patron-saga) para transacciones distribuidas
- [ ] 13 · [Health checks](https://www.netmentor.es/entrada/health-checks-asp-net)
- [ ] 14 · [Observabilidad](https://www.netmentor.es/entrada/monitorizacion-sistemas-distribuidos) con OpenTelemetry, Prometheus, Grafana y Zipkin
- [ ] 15 · [API Key](https://www.netmentor.es/entrada/explicacion-api-key) para las llamadas desde clientes
- [ ] 16 · [Rate Limiting](https://www.netmentor.es/entrada/rate-limiting)
- [ ] 17 · [.NET Aspire](https://www.netmentor.es/entrada/introduccion-net-aspire) (rama aparte en el original)
<!-- progress:end -->

## Descripción del repositorio

Todo el contenido se encuentra en este mismo repositorio para facilitar ver cómo funcionan las diferentes piezas.

Nota: técnicamente cada carpeta representa un dominio y podría ser su propio repositorio independiente, pero para facilitar el desarrollo y el seguimiento de los vídeos y posts está todo en un único repo.

```text
Scattered/
├── src/
│   ├── Api/
│   │   ├── Public/Scattered.API.Public       Gateway YARP :7283  /order-ms/* · /product-ms/* + POST /subscribe (publica)
│   │   └── Private/Scattered.API.Private     Gateway YARP :7022  /reports/*
│   ├── Services/
│   │   ├── Emails/Scattered.Services.Emails                :50120  POST /email
│   │   ├── Orders/
│   │   │   ├── Scattered.Services.Orders                   :50220  GET /order/{id} · POST /order (202, publica dominio) · logs a Graylog
│   │   │   ├── Scattered.Services.Orders.Consumer          :7225   consume order-queue
│   │   │   └── Scattered.Services.Orders.Dto                       OrderDto · CreateOrderDto · OrderAddress · PersonalDetails
│   │   ├── Products/
│   │   │   ├── Scattered.Services.Products                 :50320  GET /product/{id} · POST /product
│   │   │   └── Scattered.Services.Products.Dto                     ProductDto
│   │   └── Subscriptions/
│   │       ├── Scattered.Services.Subscriptions            :50420  POST · DELETE /subscription
│   │       ├── Scattered.Services.Subscriptions.Consumer   :7073   consume subscription-queue
│   │       └── Scattered.Services.Subscriptions.Dtos               SubscriptionDto · UnSubscriptionDto
│   └── Shared/
│       ├── Scattered.Shared.Setup           API/DefaultScatteredWebApplication (arranque común) + Services/ (ServiceBus, SecretManager, ServiceDiscovery) + GlobalUsings.cs
│       ├── Scattered.Shared.Discovery       IServiceDiscovery / ConsulServiceDiscovery (paquete Consul)
│       ├── Scattered.Shared.Logging         ConfigureSerilog(): sinks de consola y Graylog según configuración
│       ├── Scattered.Shared.Secrets         ISecretManager / VaultSecretManager (VaultSharp)
│       ├── Scattered.Shared.Serialization   ISerializer (Newtonsoft / System.Text.Json)
│       ├── Scattered.Shared.Api             Sin código: solo el .csproj (ASP.NET Core + Swashbuckle) que referencia Setup
│       └── Shared.Communication/
│           ├── Scattered.Shared.Communication            Mensajes, publisher, consumer y handlers (sin broker)
│           └── Scattered.Shared.Communication.RabbitMQ   Implementación con RabbitMQ.Client
├── tools/
│   ├── rabbitmq/             rabbitmq.config + definitions.json (usuario y topología: suscripciones y pedidos)
│   ├── vault/config.sh       Secreto KV, motor rabbitmq y rol scattered-role
│   ├── consul/config.sh      Registro de RabbitMQ, SecretManager y los 4 microservicios
│   └── local-development/    up.sh: docker-compose up + los dos config.sh
├── docs/
│   ├── flow/                 Guía del flujo paso a paso, un archivo por capítulo
│   └── communication/        Documentación de Shared.Communication
├── assets/                   Imágenes de este README
├── docker-compose.yaml       RabbitMQ 3.9 (:5672, UI :15672) + Vault (:8200) + Consul (:8500) + Graylog (:9000, GELF :12201) con MongoDB y Elasticsearch
└── Scattered.sln             Solución (.NET 6)
```

* **Api**: los API Gateways. Reenvían peticiones HTTP con YARP y, en el caso del público, también **publican mensajes** (`POST /subscribe`).
* **Services**: los microservicios. Subscriptions y Orders se dividen en API, *consumer* y DTOs: el consumer es un proceso aparte que solo lee de la cola (de integración en Subscriptions, de dominio en Orders).
* **Shared**: código común. Los secretos y el registro de servicios también pasan por abstracciones, `ISecretManager` e `IServiceDiscovery`; solo `Shared.Secrets` sabe que detrás está Vault y solo `Shared.Discovery` que detrás está Consul. La comunicación asíncrona sigue la idea *vendor free*: los servicios solo usan `IIntegrationMessagePublisher` y `IIntegrationMessageHandler<T>`; que detrás esté RabbitMQ solo lo sabe `Shared.Communication.RabbitMQ`. Así queda el consumer:

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

DefaultScatteredWebApplication.Run(app);
```

## Documentación

* Flujo paso a paso por capítulo - [docs/flow](docs/flow/README.md)
* Shared.Communication - [docs/communication/Readme.md](docs/communication/Readme.md)

## ¿Cómo ejecutar la aplicación?

Hace falta [Docker](https://www.netmentor.es/curso/docker) para RabbitMQ, Vault, Consul y Graylog. `up.sh` levanta los contenedores, configura Vault y registra los servicios en Consul:

```bash
./tools/local-development/up.sh
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders.Consumer
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
curl -k -X POST https://localhost:7283/order-ms/order -H 'Content-Type: application/json' \
  -d '{"orderAddress":{"street":"Calle Mayor 1","postalCode":"28001"},"personalDetails":{"name":"Ana","surname":"García"},"products":[]}'
curl -k https://localhost:7283/order-ms/order/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

El `POST` responde `202` y Orders.Consumer escribe `Order {id} created`; el `GET` escribe un error de ejemplo en la consola de Orders y lo envía a Graylog. Para verlo en http://localhost:9000 (`admin` / `admin`), crea antes **una vez** el input *System → Inputs → GELF UDP* en el puerto `12201` (o con el `curl` de la [guía](docs/flow/flow6.md#cómo-ejecutarla)). Si RabbitMQ ya estaba corriendo, reinícialo (`docker restart rabbitmq`) para que cree `order-queue`. El resto de servicios no escribe nada en consola: solo Orders configura Serilog. Las UIs de Consul (:8500), Vault (:8200, token `vault-scattered-token`) y RabbitMQ (:15672, `ScatteredAdmin` / `ScatteredPass`) siguen igual. La lista completa de comandos, el flujo end to end y los problemas conocidos están en [docs/flow/flow6.md](docs/flow/flow6.md#cómo-ejecutarla).

## Cómo evoluciona este README

Al replicar un capítulo nuevo en su rama `features/flowN`, actualiza:

1. La nota inicial, la tabla **Estado de esta rama** y la sección **Qué cambió en este capítulo** (se reescribe en cada capítulo; la de los anteriores queda en su guía de `docs/flow`).
2. El **diagrama**: edita `assets/architecture.svg` (pasa a borde continuo y etiqueta amarilla lo que ya existe) y regenera `assets/diagram.png`:
   ```bash
   "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new \
     --hide-scrollbars --force-device-scale-factor=2 --window-size=1400,1090 \
     --screenshot=assets/diagram.png "file://$PWD/assets/architecture.svg"
   ```
3. **Progreso del curso** no se edita a mano: lo genera `tools/docs/progress.py` desde `docs/flow/chapters.json`, y los hooks de `.githooks/` lo ejecutan en cada commit y merge (actívalos una vez con `git config core.hooksPath .githooks`; ver [docs/flow](docs/flow/README.md#4-documentar-y-cerrar-el-capítulo)).
4. El árbol de **Descripción del repositorio** y **¿Cómo ejecutar la aplicación?** (a partir del capítulo 3 hace falta `docker-compose`).
5. Crea `docs/flow/flowN.md` con el detalle del capítulo y enlázalo desde [docs/flow/README.md](docs/flow/README.md).

## Dale una estrella ⭐
Si te gusta el proyecto no dudes en darle una estrella al [proyecto original](https://github.com/ElectNewt/Distribt), hacer un fork junto a una PR o incluso apoyar económicamente al autor [donando un café](https://www.buymeacoffee.com/netmentor).
