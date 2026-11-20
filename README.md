# Scattered

![Scattered](assets/scattered-logo.svg)

> **Nota:** Scattered es una copia renombrada de [Distribt](https://github.com/ElectNewt/Distribt), proyecto creado por Ivan Abad ([NetMentor](https://www.netmentor.es)) y publicado bajo licencia MIT. El curso y los artículos enlazados en este documento pertenecen al autor original.
>
> **Esta rama (`features/flow5`)** replica la rama [`06-Service-discovery-registry`](https://github.com/ElectNewt/Distribt/tree/06-Service-discovery-registry) del proyecto original (commit `f596ee4`), renombrada de Distribt a Scattered. Cada capítulo del curso se replica en su propia rama `features/flowN`; la guía de cada uno, con qué cambió y cómo ejecutarlo, está en **[docs/flow/README.md](docs/flow/README.md)**.

## Estado de esta rama

| | |
|---|---|
| Rama | `features/flow5` (nace de `features/flow4`) |
| Capítulo del curso | 6 · [Service discovery / registry](https://www.netmentor.es/entrada/service-registry-discovery-consul) con Consul |
| Rama original | [`06-Service-discovery-registry`](https://github.com/ElectNewt/Distribt/tree/06-Service-discovery-registry) (`f596ee4`) |
| Qué hay | 2 API Gateways con YARP; el público publica suscripciones en RabbitMQ y un consumer las procesa. Las credenciales de RabbitMQ salen de Vault, y ahora **las direcciones de Vault y RabbitMQ salen de Consul** |
| Qué falta | Persistencia, logs centralizados y el resto de infraestructura. Los gateways todavía no usan Consul para enrutar. Orders sigue roto (problema heredado) |
| Infraestructura | RabbitMQ 3.9, Vault (modo dev) y Consul (modo dev) en Docker: `./tools/local-development/up.sh` |
| Guía del capítulo | [docs/flow/flow5.md](docs/flow/flow5.md) |
| Siguiente capítulo | 7 · [Sistema de logs](https://www.netmentor.es/entrada/servicio-logs-graylog) con Graylog y Serilog (`07-Logs-en-la-nube`) |

## Qué cambió en este capítulo

Respecto a `features/flow4` ([capítulo 5](docs/flow/flow4.md)) las **direcciones salen de la configuración**. Antes cada servicio tenía escrito dónde estaba Vault (`SecretManager:VaultUrl`) y dónde estaba RabbitMQ (`Bus:RabbitMQ:Hostname`); ahora solo sabe dónde está **Consul** (`Discovery:Address`) y le pregunta por nombre. El flujo de mensajes y las credenciales no cambian.

| Cambio | Dónde | Para qué |
|---|---|---|
| Contenedor `consul` (agente en modo dev) | `docker-compose.yaml` | Registro de servicios en `:8500` (API y web) y `:8600` (DNS) |
| `config.sh` (nuevo) y `up.sh` | `tools/consul/`, `tools/local-development/` | Registrar `RabbitMQ`, `SecretManager` y los 4 microservicios; `up.sh` lo ejecuta tras el de Vault |
| `Scattered.Shared.Discovery` (nuevo) | `src/Shared/` | `IServiceDiscovery` / `ConsulServiceDiscovery`: nombre → `dirección[:puerto]`, con caché en memoria |
| Arranque común movido a `Setup/API` | `DefaultScatteredWebApplication` | Registra Discovery y **pregunta a Consul por Vault al arrancar** (`AddSecretManager`); `Shared.Api` queda vacío |
| `ServiceBus` y `SecretManager` a `Setup/Services` | `Scattered.Shared.Setup` | El host de RabbitMQ y la URL de Vault se resuelven con Consul |
| `SetHostName` / `UpdateUrl` + `PostConfigure` | `Shared.Communication.RabbitMQ`, `Shared.Secrets` | Las direcciones se rellenan después de leer la configuración, como las credenciales del capítulo 5 |
| − `SecretManager:VaultUrl`, + `Discovery:Address` | Los 7 `appsettings.json` | Solo queda la dirección de Consul |

```text
Antes (capítulo 5)                               Ahora (capítulo 6)
appsettings.json                                 appsettings.json: Discovery:Address = http://localhost:8500
  SecretManager:VaultUrl = http://localhost:8200          │
  Bus:RabbitMQ:Hostname  = localhost                      ▼
        │                                        Consul (:8500)
        ▼                                          ├─ SecretManager → http://localhost:8200 ──▶ Vault
Vault (:8200) · RabbitMQ (:5672)                   ├─ RabbitMQ      → localhost             ──▶ RabbitMQ
                                                   └─ EmailsApi, OrdersApi… (registrados, aún sin uso)
```

Lo que conviene saber al probarlo: **Consul pasa a ser imprescindible para arrancar** cualquier proceso, incluso los que no usan Vault ni RabbitMQ; cada proceso resuelve las direcciones una sola vez y no se entera de cambios posteriores; y los gateways siguen enrutando con las direcciones de `appsettings.json`, no con Consul. Diferencias con el original: la imagen pasa de `consul` (retirada de Docker Hub) a `hashicorp/consul`, y `tools/consul/config.sh` registra por la API HTTP porque la CLI actual rechaza las direcciones con `http://` del original (de paso se corrigen los puertos cruzados de Orders y Products). Detalle de cada cambio y el flujo end to end de 14 pasos en **[docs/flow/flow5.md](docs/flow/flow5.md)**.

## ¿Qué es Scattered?

Scattered es un proyecto Open Source creado para mostrar el funcionamiento y uso de los sistemas distribuidos con las implementaciones en .NET Core.

La aplicación va a ser "vendor free" lo que quiere decir que no va a estar enlazada directamente a ningún proveedor específico. Obviamente vamos a utilizar X o Y servicio (ya que no vamos a reinventar la rueda), pero lo haremos a través de abstracciones.

Es importante saber estos conceptos ya que cada día las empresas están migrando sus aplicaciones monolíticas a microservicios o incluso serverless, y para poder aplicar un correcto funcionamiento, debemos aprender sobre sistemas distribuidos.

## ¿Qué vamos a ver?

Vamos a ver un sistema distribuido con múltiples características, como pueden ser patrón consumer/publisher, sagas, service discovery, Eventual consistency, etc.

Esta es la arquitectura planteada en el capítulo 1, marcada con lo que ya existe en esta rama (`features/flow5`):

![ScatteredDiagram](assets/diagram.png)

- Las cajas con borde continuo existen en el código; las de borde discontinuo son el objetivo y todavía no existen.
- Las flechas continuas son conexiones que ya funcionan: grises para HTTP (rutas de los gateways), ámbar para mensajes por RabbitMQ, violetas para la lectura de secretos en Vault y verdes para las consultas a Consul. Las discontinuas son conexiones planificadas.
- Las etiquetas amarillas representan el código ya implementado; las grises indican en qué capítulo llega cada pieza.
- La fuente editable del diagrama es [`assets/architecture.svg`](assets/architecture.svg).

Puedes encontrar el proceso de creación del sistema en [el curso de YouTube del autor original](https://www.youtube.com/playlist?list=PLesmOrW3mp4jpSbdFMtVWINJZ7OLdSASS).

Alternativamente, si lo prefieres, puedes seguir los post con el código detallado y los razonamientos sobre por qué se ha elegido cada tecnología en la web del autor original, [NetMentor - Curso Distribt](https://www.netmentor.es/curso/sistemas-distribuidos).

## Progreso del curso

<!-- progress:start (generado por tools/docs/progress.py desde docs/flow/chapters.json; no editar a mano) -->
Cada capítulo se replica en su propia rama `features/flowN` y tiene su guía en `docs/flow`. **6 de 17 capítulos** replicados.

- [x] 1 · [API Rest](https://www.netmentor.es/entrada/api-rest-csharp): esqueleto de microservicios creados con .NET · [guía](docs/flow/flow1.md)
- [x] 2 · [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) con YARP · [guía](docs/flow/flow2.md)
- [x] 3–4 · [Producers/Consumers](https://www.netmentor.es/entrada/patron-productor-consumidor) para la comunicación asíncrona [con RabbitMQ](https://www.netmentor.es/entrada/rabbitmq-comunicacion-asincrona) · [guía](docs/flow/flow3.md)
- [x] 5 · [Gestión de credenciales](https://www.netmentor.es/entrada/gestion-credenciales-vault) con Vault · [guía](docs/flow/flow4.md)
- [x] 6 · [Service discovery / registry](https://www.netmentor.es/entrada/service-registry-discovery-consul) con Consul · [guía](docs/flow/flow5.md)
- [ ] 7 · [Sistema de logs](https://www.netmentor.es/entrada/servicio-logs-graylog) con Graylog y Serilog
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
│   │   ├── Orders/Scattered.Services.Orders                :50220  GET /order/{id} · POST /order (⚠ 500)
│   │   ├── Products/Scattered.Services.Products            :50320  GET /product/{id} · POST /product
│   │   └── Subscriptions/
│   │       ├── Scattered.Services.Subscriptions            :50420  POST · DELETE /subscription
│   │       ├── Scattered.Services.Subscriptions.Consumer   :7073   consume subscription-queue
│   │       └── Scattered.Services.Subscriptions.Dtos               SubscriptionDto · UnSubscriptionDto
│   └── Shared/
│       ├── Scattered.Shared.Setup           API/DefaultScatteredWebApplication (arranque común) + Services/ (ServiceBus, SecretManager, ServiceDiscovery) + GlobalUsings.cs
│       ├── Scattered.Shared.Discovery       IServiceDiscovery / ConsulServiceDiscovery (paquete Consul)
│       ├── Scattered.Shared.Secrets         ISecretManager / VaultSecretManager (VaultSharp)
│       ├── Scattered.Shared.Serialization   ISerializer (Newtonsoft / System.Text.Json)
│       ├── Scattered.Shared.Api             Sin código: solo el .csproj (ASP.NET Core + Swashbuckle) que referencia Setup
│       └── Shared.Communication/
│           ├── Scattered.Shared.Communication            Mensajes, publisher, consumer y handlers (sin broker)
│           └── Scattered.Shared.Communication.RabbitMQ   Implementación con RabbitMQ.Client
├── tools/
│   ├── rabbitmq/             rabbitmq.config + definitions.json (usuario y topología)
│   ├── vault/config.sh       Secreto KV, motor rabbitmq y rol scattered-role
│   ├── consul/config.sh      Registro de RabbitMQ, SecretManager y los 4 microservicios
│   └── local-development/    up.sh: docker-compose up + los dos config.sh
├── docs/
│   ├── flow/                 Guía del flujo paso a paso, un archivo por capítulo
│   └── communication/        Documentación de Shared.Communication
├── assets/                   Imágenes de este README
├── docker-compose.yaml       RabbitMQ 3.9 (AMQP :5672, UI :15672) + Vault (:8200) + Consul (:8500), los dos en modo dev
└── Scattered.sln             Solución (.NET 6)
```

* **Api**: los API Gateways. Reenvían peticiones HTTP con YARP y, en el caso del público, también **publican mensajes** (`POST /subscribe`).
* **Services**: los microservicios. Subscriptions se divide en API, *consumer* y DTOs: el consumer es un proceso aparte que solo lee de la cola.
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

Hace falta [Docker](https://www.netmentor.es/curso/docker) para RabbitMQ, Vault y Consul. `up.sh` levanta los contenedores y después configura Vault (que arranca vacío cada vez) y registra los servicios en Consul:

```bash
./tools/local-development/up.sh
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Subscriptions/Scattered.Services.Subscriptions.Consumer
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
```

El consumer escribe `Email cliente@example.com successfully subscribed.` en su consola. Si Consul no está levantado, **ningún** servicio arranca. Las UIs: Consul en http://localhost:8500 (los servicios registrados), Vault en http://localhost:8200 (token `vault-scattered-token`) y RabbitMQ en http://localhost:15672 (`ScatteredAdmin` / `ScatteredPass`). `tools/vault/config.sh` necesita una terminal interactiva: desde un script, usa `sed 's/docker exec -it /docker exec /' tools/vault/config.sh | bash`. Si el contenedor `consul` viene de `main`, conserva sus registros: empieza limpio con `docker rm -fv consul && docker compose up -d consul && ./tools/consul/config.sh`. La lista completa de comandos, el flujo end to end y los problemas conocidos están en [docs/flow/flow5.md](docs/flow/flow5.md#cómo-ejecutarla).

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
