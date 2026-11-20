# Resumen de los 17 capítulos

Qué cambia y qué no cambia en cada capítulo del curso, de un vistazo. El detalle archivo por archivo, el flujo end to end y los problemas heredados de cada uno están en su guía (`flowN.md`).

[← Índice del flujo](README.md) · [README principal](../../README.md)

## Contenido

[De un vistazo](#de-un-vistazo) · [1](#capítulo-1--introducción) · [2](#capítulo-2--patrón-api-gateway) · [3–4](#capítulos-34--producersconsumers-con-rabbitmq) · [5](#capítulo-5--gestión-de-credenciales-con-vault) · [6](#capítulo-6--service-discovery-con-consul) · [7](#capítulo-7--logs-centralizados-con-serilog-y-graylog) · [8](#capítulo-8--cqrs-en-products) · [9](#capítulo-9--event-sourcing-en-orders) · [10](#capítulo-10--consistencia-eventual) · [11](#capítulo-11--respuestas-de-api-unificadas-con-rop) · [12](#capítulo-12--patrón-saga) · [13](#capítulo-13--health-checks) · [14](#capítulo-14--observabilidad) · [15](#capítulo-15--api-key) · [16](#capítulo-16--rate-limiting-y-net-7) · [17](#capítulo-17--net-aspire-y-net-8) · [La evolución en una tabla](#la-evolución-en-una-tabla)

## De un vistazo

```text
 1  Esqueleto ─▶ 2  YARP ─▶ 3–4  RabbitMQ ─▶ 5  Vault ─▶ 6  Consul ─▶ 7  Graylog
                                                                         │
 ┌───────────────────────────────────────────────────────────────────────┘
 ▼
 8  CQRS (Products) ─▶ 9  Event Sourcing (Orders) ─▶ 10  Consistencia eventual (docs)
                                                                         │
 ┌───────────────────────────────────────────────────────────────────────┘
 ▼
11  ROP ─▶ 12  SAGA (teoría) ─▶ 13  Health checks ─▶ 14  OpenTelemetry
                                                                         │
 ┌───────────────────────────────────────────────────────────────────────┘
 ▼
15  API key ─▶ 16  Rate limiting + .NET 7 ─▶ 17  Aspire + .NET 8
```

| # | Guía | Rama | Qué cambia en una línea |
|---|---|---|---|
| 1 | [Introducción](flow1.md) | `features/flow1` | Esqueleto: 4 microservicios y 2 gateways con datos fijos y un arranque común |
| 2 | [Patrón API Gateway](flow2.md) | `features/flow2` | Los gateways enrutan con YARP hacia Orders y Products |
| 3–4 | [Producers/Consumers](flow3.md) | `features/flow3` | Primera comunicación asíncrona: suscripciones por RabbitMQ |
| 5 | [Gestión de credenciales](flow4.md) | `features/flow4` | Las credenciales de RabbitMQ salen de Vault |
| 6 | [Service discovery](flow5.md) | `features/flow5` | Las direcciones de Vault y RabbitMQ salen de Consul |
| 7 | [Logs en la nube](flow6.md) | `features/flow6` | Serilog + Graylog; Orders publica mensajes de dominio |
| 8 | [CQRS](flow7.md) | `features/flow7` | Products se parte en escritura (MySQL), lectura (MongoDB) y consumer |
| 9 | [Event Sourcing](flow8.md) | `features/flow8` | Orders guarda sus pedidos como eventos en MongoDB |
| 10 | [Consistencia eventual](flow9.md) | `features/flow9` | Sin código: mediciones de la consistencia eventual |
| 11 | [Respuestas unificadas](flow10.md) | `features/flow10` | `ResultDto` con ROP; Orders guarda nombres de producto |
| 12 | [Patrón SAGA](flow11.md) | `features/flow11` | SAGA en teoría; `GlobalSettings.msbuild` común |
| 13 | [Health checks](flow12.md) | `features/flow12` | `/health` y `/health-ui` en todos los servicios |
| 14 | [Observabilidad](flow13.md) | `features/flow13` | OpenTelemetry → collector → Zipkin, Prometheus y Grafana |
| 15 | [API Key](flow14.md) | `features/flow14` | El gateway privado exige la cabecera `apiKey` |
| 16 | [Rate limiting](flow15.md) | `features/flow15` | .NET 7 y 2 llamadas por hora y por API key (429) |
| 17 | [.NET Aspire](flow16.md) | `features/flow16` | .NET 8, puertos 60xxx y AppHost de Aspire para Products |

---

## Capítulo 1 · Introducción

[Guía completa](flow1.md) · `features/flow1` · original `01-Introduccion`

**Qué cambia** (punto de partida)

- 9 proyectos en .NET 6: API pública y privada (`GET /` → `Hello World!`), Emails, Orders, Products y Subscriptions con controladores que devuelven datos fijos.
- `Scattered.Shared.Api` con `DefaultScatteredWebApplication`: controllers, Swagger, HTTPS y autorización en un arranque común; cada `Program.cs` queda en tres líneas.
- `Scattered.Shared.Setup` con un `GlobalUsings.cs` compartido.

**Lo que todavía no existe**

- Comunicación entre servicios, persistencia y tests.
- Infraestructura: `docker-compose.yaml` define RabbitMQ, pero nadie lo usa.

## Capítulo 2 · Patrón API Gateway

[Guía completa](flow2.md) · `features/flow2` · original `02-patron-api-gateway`

**Qué cambia**

- YARP (`Yarp.ReverseProxy`) en los dos gateways, usando por primera vez el punto de extensión de `Create`.
- Rutas en `appsettings.json`: el público enruta `/order-ms` y `/product-ms`; el privado, `/reports`.
- Puertos fijos `50X20` / `50X10` en los `launchSettings.json` de los servicios.
- URLs en minúsculas en el arranque común.

**Lo que no cambia**

- El código de los microservicios: siguen devolviendo datos fijos.
- Subscriptions y Emails no se exponen por el gateway.
- Los servicios no se llaman entre sí.

## Capítulos 3–4 · Producers/Consumers con RabbitMQ

[Guía completa](flow3.md) · `features/flow3` · original `03-04-Producers-consumers-RabbitMQ`

**Qué cambia**

- RabbitMQ 3.9 con usuario propio y topología predefinida (`tools/rabbitmq/`: exchanges, colas y dead-letter).
- Librerías nuevas: `Shared.Serialization`, `Shared.Communication` (abstracciones) y `Shared.Communication.RabbitMQ`.
- El gateway público publica la suscripción (`POST /subscribe`) y un proceso nuevo, `Subscriptions.Consumer`, la consume.
- `Subscriptions.Dtos` con el contrato del mensaje.

**Lo que no cambia**

- Rutas de YARP y puertos del capítulo 2.
- Products, Emails y la API de Subscriptions.
- Persistencia: el handler solo escribe en consola.

## Capítulo 5 · Gestión de credenciales con Vault

[Guía completa](flow4.md) · `features/flow4` · original `05-Gestion-credenciales`

**Qué cambia**

- Vault en modo dev en `docker-compose.yaml`, configurado por `tools/vault/config.sh`; `tools/local-development/up.sh` lo arranca todo.
- `Shared.Secrets` (`ISecretManager` con VaultSharp).
- Las credenciales de RabbitMQ desaparecen de `appsettings.json`: el consumer las lee de un secreto KV y el gateway pide un usuario dinámico.

**Lo que no cambia**

- La topología de RabbitMQ y el flujo `POST /subscribe` → cola → consumer.
- Rutas de YARP, puertos, Products, Emails y la API de Subscriptions.
- Orders sigue devolviendo 500 y el consumer sigue abriendo una conexión por segundo (heredado del 3–4).

## Capítulo 6 · Service discovery con Consul

[Guía completa](flow5.md) · `features/flow5` · original `06-Service-discovery-registry`

**Qué cambia**

- Consul en modo dev; `tools/consul/config.sh` registra 6 servicios.
- `Shared.Discovery` (`IServiceDiscovery` + `ConsulServiceDiscovery`).
- Las direcciones de Vault y RabbitMQ salen de Consul; `appsettings.json` solo guarda `Discovery:Address`.
- `DefaultScatteredWebApplication` se muda de `Shared.Api` a `Shared.Setup/API`.

**Lo que no cambia**

- Las rutas de YARP: los gateways siguen con las direcciones escritas en `appsettings.json`.
- Vault y sus credenciales.
- La topología de RabbitMQ, el 500 de Orders y la fuga de conexiones del consumer.

## Capítulo 7 · Logs centralizados con Serilog y Graylog

[Guía completa](flow6.md) · `features/flow6` · original `07-Logs-en-la-nube`

**Qué cambia**

- Graylog con MongoDB y Elasticsearch (3 contenedores nuevos).
- `Shared.Logging`: Serilog configurable con sinks de consola y Graylog. Solo Orders envía logs.
- Orders se arregla: `POST` publica un mensaje de dominio (202) y nace `Orders.Consumer` con `order.exchange` → `order-queue`.
- Proyectos `Orders.Dto` y `Products.Dto`; arreglo de `IDomainMessageHandler<T>`.

**Lo que no cambia**

- Vault, Consul y el flujo de suscripciones.
- Las rutas de YARP.
- Products, Emails y la API de Subscriptions.
- La fuga de conexiones, que ahora crece al doble porque hay dos consumers.

## Capítulo 8 · CQRS en Products

[Guía completa](flow7.md) · `features/flow7` · original `08-CQRS`

**Qué cambia**

- MySQL (`:3307`) y MongoDB (`:27017`) con datos iniciales; credenciales en Vault y direcciones en Consul.
- Products se divide en `Api.Write` (MySQL, EF Core), `Api.Read` (minimal API sobre MongoDB), `Consumer` (sincroniza la lectura) y `BusinessLogic`.
- `Shared.Databases.MySql` y `Shared.Databases.MongoDb`.
- Handlers registrados por ensamblado con Scrutor; primer test de API (Subscriptions con `WebApplicationFactory`).

**Lo que no cambia**

- Vault, Consul y Graylog: solo suman entradas.
- Las rutas de YARP: `/product-ms/*` apunta ahora a la API de escritura, así que por el gateway se crea pero no se lee.
- Orders y el flujo de suscripciones.
- El receptor de RabbitMQ: no hace *nack* si el handler falla.

## Capítulo 9 · Event Sourcing en Orders

[Guía completa](flow8.md) · `features/flow8` · original `09-Event-sourcing`

**Qué cambia**

- `Shared.EventSourcing`: `Aggregate`, `AggregateRepository<T>` y un event store en MongoDB con concurrencia optimista.
- Orders guarda sus pedidos como eventos (`OrderCreated`, `OrderPaid`, `OrderDispatched`, `OrderCompleted`) y reconstruye el estado al leer.
- Rutas nuevas en Orders: `POST create`, `GET {id}`, `PUT markaspaid / markasdispatched / markasdelivered`.
- Colección `EventsOrders` en MongoDB.

**Lo que no cambia**

- La infraestructura: el event store vive en el MongoDB del capítulo 8.
- Products (CQRS), suscripciones, Vault, Consul y Graylog.
- Las rutas de YARP hacia Orders.

## Capítulo 10 · Consistencia eventual

[Guía completa](flow9.md) · `features/flow9` · original `10-Consistencia-eventual`

**Qué cambia**

- Nada en el código: es el mismo commit que el capítulo 9.
- La guía mide la consistencia eventual en Products, Orders y Subscriptions.

**Lo que el código no resuelve todavía**

- Un producto creado nunca llega a la vista de lectura (no hay handler de `ProductCreated`).
- Sin *nack* ni dead-letter, un mensaje que falla se reintenta para siempre.
- Las lecturas no llevan versión ni fecha.
- Guardar y publicar no son atómicos (no hay *outbox*).

## Capítulo 11 · Respuestas de API unificadas con ROP

[Guía completa](flow10.md) · `features/flow10` · original `11-unificar-respuestas-api`

**Qué cambia**

- `Netmentor.ROP.ApiExtensions`: `Result<T>` y respuestas `ResultDto` en Orders, Products (escritura) y Subscriptions.
- `Orders.BusinessLogic` con `ProductNameService` (caché → MongoDB → HTTP): Orders guarda los nombres de producto que le llegan por integración.
- Products por fin crea en la vista de lectura (`ProductCreatedHandler`).
- La dirección de Graylog sale de Consul; colas `order-domain-queue` y enlace `products.exchange` → `order.exchange`.
- 4 tests de `ProductNameService` con Moq.

**Lo que no cambia**

- La infraestructura: no hay contenedores nuevos.
- El Event Sourcing de Orders, Vault, la fuga de conexiones y el receptor sin *nack*.
- La API de lectura de Products sigue sin `ResultDto` y devuelve 500 para un id inexistente.

## Capítulo 12 · Patrón SAGA

[Guía completa](flow11.md) · `features/flow11` · original `12-Saga`

**Qué cambia**

- SAGA solo en teoría: el original no trae código del patrón.
- `GlobalSettings.msbuild`: configuración común (framework, nulabilidad, `TreatWarningsAsErrors`, metadatos NuGet) para las 10 librerías de `Shared`.
- `GetByIdOrDefaultAsync` en el event sourcing, para no devolver `null!`.

**Lo que no cambia**

- La infraestructura, los mensajes, Products, Orders y Subscriptions.
- Los problemas de ROP del capítulo 11.

## Capítulo 13 · Health checks

[Guía completa](flow12.md) · `features/flow12` · original `13-Health-Checks`

**Qué cambia**

- `/health` (JSON) y `/health-ui` en todos los servicios desde el arranque común.
- Comprobaciones de RabbitMQ, MySQL y MongoDB, y una propia en Orders que consulta el `/health` de Products.
- RabbitMQ baja a 3.8 y cambia `rabbitmq.config` (Erlang) por `rabbitmq.conf` (ini); `definitions.json` pasa a ser JSON válido.

**Lo que no cambia**

- El comportamiento de negocio: suscripciones, CQRS, Event Sourcing y nombres de producto.
- Los problemas de ROP y la fuga de conexiones.

## Capítulo 14 · Observabilidad

[Guía completa](flow13.md) · `features/flow13` · original `14-monitorizacion-sistemas-distribuidos`

**Qué cambia**

- 4 contenedores nuevos: collector de OpenTelemetry, Prometheus, Grafana y Zipkin.
- `AddTracing` y `AddMetrics` en el arranque común; la dirección del collector sale de Consul y cada servicio tiene su `AppName`.
- RabbitMQ expone métricas (`rabbitmq_prometheus`) y hay un dashboard de Grafana para él.

**Lo que no cambia**

- El comportamiento de negocio, los health checks y los logs en Graylog.
- Los mensajes de RabbitMQ no se trazan: la traza termina al publicar.

## Capítulo 15 · API Key

[Guía completa](flow14.md) · `features/flow14` · original `15-Api-key`

**Qué cambia**

- `ApiKeyMiddleware`: el gateway privado exige la cabecera `apiKey`, salvo en `/health`.
- Una sola clave en `appsettings.json`.
- Documentación de `Shared.Discovery` y `Shared.Secrets` en `docs/`.

**Lo que no cambia**

- El gateway público, los servicios y la infraestructura.
- Los servicios detrás del gateway privado siguen sin protección: Orders responde a cualquiera en su puerto.
- El código sigue en .NET 6 (el paso a .NET 7 se revierte el mismo día).

## Capítulo 16 · Rate limiting y .NET 7

[Guía completa](flow15.md) · `features/flow15` · original `16-rate-limiting`

**Qué cambia**

- Todo pasa a .NET 7: una línea en `GlobalSettings.msbuild` para `Shared` y uno a uno en el resto de `.csproj`.
- `ScatteredRateLimiterPolicy`: ventana fija de 2 llamadas por hora y por API key en `GET /rate-limiting-test` del gateway privado (429 al superarla).
- Swashbuckle 6.4.0 centralizado en `Shared.Setup`; ajustes de nulabilidad.

**Lo que no cambia**

- El comportamiento de negocio, la infraestructura (12 contenedores) y la observabilidad.
- El resto de rutas del gateway privado no tienen límite.

## Capítulo 17 · .NET Aspire y .NET 8

[Guía completa](flow16.md) · `features/flow16` · original `17-Aspire-version2023` (rama aparte, no llegó a `main`)

**Qué cambia**

- Todo pasa a .NET 8 y los puertos de los microservicios, de 50xxx a 60xxx (`launchSettings`, YARP y Consul).
- AppHost de Aspire que arranca los 3 servicios de Products, con su dashboard.
- `ServiceDefaults` (OpenTelemetry, health, resiliencia, discovery) sustituye a `AddTracing`/`AddMetrics` del capítulo 14.
- 4 tests de `ConsulServiceDiscovery` con Moq.

**Lo que no cambia**

- El comportamiento de negocio: suscripciones, CQRS, Event Sourcing, API key y rate limiting.
- La infraestructura: los 12 contenedores siguen haciendo falta y Aspire no gestiona ninguno.
- Los health checks del capítulo 13, a los que ServiceDefaults suma `self`.

---

## La evolución en una tabla

| # | .NET | Contenedores nuevos | Librerías de `Shared` nuevas | Servicios nuevos |
|---|---|---|---|---|
| 1 | 6 | (RabbitMQ sin usar) | `Shared.Api`, `Shared.Setup` | Emails, Orders, Products, Subscriptions, 2 gateways |
| 2 | 6 | — | — | — |
| 3–4 | 6 | RabbitMQ (configurado) | `Serialization`, `Communication`, `Communication.RabbitMQ` | Subscriptions.Consumer |
| 5 | 6 | Vault | `Secrets` | — |
| 6 | 6 | Consul | `Discovery` | — |
| 7 | 6 | Graylog, MongoDB y Elasticsearch de Graylog | `Logging` | Orders.Consumer |
| 8 | 6 | MySQL, MongoDB | `Databases.MySql`, `Databases.MongoDb` | Products.Api.Read, Products.Consumer |
| 9 | 6 | — | `EventSourcing` | — |
| 10 | 6 | — | — | — |
| 11 | 6 | — | — | — (`Orders.BusinessLogic`) |
| 12 | 6 | — | — (`GlobalSettings.msbuild`) | — |
| 13 | 6 | — | — | — |
| 14 | 6 | Collector OTel, Prometheus, Grafana, Zipkin | — | — |
| 15 | 6 | — | — | — |
| 16 | 7 | — | — | — |
| 17 | 8 | — | — (Aspire `ServiceDefaults`) | Aspire AppHost |

Siguen pendientes al terminar el curso, según las guías: los mensajes que fallan sin *nack* ni dead-letter, el *outbox* y una SAGA con código. Para ver qué separa el último capítulo de la versión final: `git diff -M --stat features/flow16 main`.
