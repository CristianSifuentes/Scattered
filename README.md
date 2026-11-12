# Scattered

![Scattered](assets/scattered-logo.svg)

> **Nota:** Scattered es una copia renombrada de [Distribt](https://github.com/ElectNewt/Distribt), proyecto creado por Ivan Abad ([NetMentor](https://www.netmentor.es)) y publicado bajo licencia MIT. El curso y los artículos enlazados en este documento pertenecen al autor original.
>
> **Propósito de esta copia:** este repositorio existe con fines de aprendizaje. Su objetivo es replicar paso a paso el flujo del curso e ir agregando particularidades personales derivadas de ese aprendizaje, por lo que el código puede diferir del proyecto original.

## ¿Qué es Scattered? 

Scattered es un proyecto Open Source creado para mostrar el funcionamiento y uso de los sistemas distribuidos con las implementaciones en .NET Core.


La aplicación va a ser "vendor free" lo que quiere decir que no va a estar enlazada directamente a ningún proveedor específico. Obviamente vamos a utilizar X o Y servicio (ya que no vamos a reinventar la rueda), pero lo haremos a través de abstracciones.  


Es importante saber estos conceptos ya que cada día las empresas están migrando sus aplicaciones monolíticas a microservicios o incluso serverless, y para poder aplicar un correcto funcionamiento, debemos aprender sobre sistemas distribuidos. 

## ¿Qué vamos a ver? 
Vamos a ver un sistema distribuido con múltiples características, como pueden ser patrón consumer/publiser, sagas, service discovery, Eventual consistency, etc.

La arquitectura que vamos a ver es la siguiente:

![ScatteredDiagram](assets/architecture.svg)
- Nota: las etiquetas amarillas representan el código ya implementado.

Puedes encontrar el proceso de creación del sistema en [el curso de YouTube del autor original](https://www.youtube.com/playlist?list=PLesmOrW3mp4jpSbdFMtVWINJZ7OLdSASS).

Alternativamente, si lo prefieres, puedes seguir los post con el código detallado y los razonamientos sobre por qué se ha elegido cada tecnología en la web del autor original, [NetMentor - Curso Distribt](https://www.netmentor.es/curso/sistemas-distribuidos)

Si estás siguiendo el curso vídeo a vídeo tienes diferentes branches en el repositorio original con el código de cada vídeo (de forma incremental) aquí:  https://github.com/ElectNewt/Distribt/branches/all 

## Infraestructura
Hasta ahora hemos visto las siguientes funcionalidades:


* [API Rest](https://www.netmentor.es/entrada/api-rest-csharp) microservicios creados con .NET
* [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) con YARP.
* [Producers/Consumers](https://www.netmentor.es/entrada/patron-productor-consumidor) para la comunicación asíncrona [implementado con RabbitMQ](https://www.netmentor.es/entrada/rabbitmq-comunicacion-asincrona).
* [Acceso y almacenamiento seuguro](https://www.netmentor.es/entrada/gestion-credenciales-vault) de la informción secreta con Vault.
* [Registro de servicios](https://www.netmentor.es/entrada/service-registry-discovery-consul) con Consul (service discovery / Service registry).
* [Sistema de logs](https://www.netmentor.es/entrada/servicio-logs-graylog) con GrayLog y SeriLog.
* [CQRS](https://www.netmentor.es/entrada/patron-cqrs-explicado-10-minutos) para separar lecturas de escrituras.
* [Event Sourcing](https://www.netmentor.es/entrada/event-sourcing-explicado-facil) junto a MongoDb para almacenar los eventos.
* [Eventual Consistency](https://www.netmentor.es/entrada/consistencia-eventual-microservicios) usando comunicación asíncrona para sincronizar los diferentes microservicios.
* [Unificación de las API](https://www.netmentor.es/entrada/unificar-respuestas-api) para tener una respuesta consistente con [Rop](https://www.netmentor.es/entrada/railway-oriented-programming).
* [Patrón SAGA](https://www.netmentor.es/entrada/patron-saga) para las transacciones distribuidas.
* [Health checks](https://www.netmentor.es/entrada/health-checks-asp-net) Para la visibilidad de la salud de nuestros microservicios.
* [Observabilidad](https://www.netmentor.es/entrada/monitorizacion-sistemas-distribuidos) De nuestro sistema e infraestructura (metricas y traces) con OpenTelemetry, Prometheus, Grafana y Zipkin
* [API Key](https://www.netmentor.es/entrada/explicacion-api-key) Para las llamadas desde clientes.
* [Rate Limiting](https://www.netmentor.es/entrada/rate-limiting) Para limitar el uso de recursos.
* [Aspire](https://www.netmentor.es/entrada/introduccion-net-aspire) Para ejecución local de las applicaiones. nota, está solo en un [branch](https://github.com/ElectNewt/Distribt/tree/17-Aspire-version2023) ya que aspire sigue en preview.



## Documentación [En progreso]
Puedes encontrar documentación de cada projecto dentro de `Shared` en la carpeta `docs`.
* Shared.Communication - [Enlace](docs/communication/Readme.md)
* Shared.Discovery - [Enlace](docs/discovery/Readme.md)
* Shared.Secrets - [Enlace](docs/secrets/Readme.md)


## Descripción del repositorio

Todo el contenido se encuentra en este mismo repositorio esto es así para una mayor facilidad a la hora de ver cómo funcionan las diferentes herramientas.

Puedes encontrar el código dentro de la carpeta `src`. Donde encontrarás múltiples carpetas.

Nota: Técnicamente cada carpeta representa un dominio y puede ser su propio repositorio independiente, pero para una mayor facilidad del desarrollo y del seguimiento en los vídeos y posts está todo en un único repo.

* Api: capa de abstracción de una API Gateway.
* Services: Carpeta que contiene los microservicios del sistema.
* Shared: código común de las abstracciones.


## Arquitectura de carpetas y proyectos

Esta sección muestra de forma gráfica cómo está organizado el repositorio, qué objetivo tiene cada carpeta y cómo se conectan los proyectos entre sí, tanto en compilación (referencias) como en ejecución (HTTP y mensajería).

### 1. Vista general del repositorio

```text
Scattered/
├── src/                        Código fuente (.NET 10)
│   ├── Api/                    Puerta de entrada al sistema: los API Gateways
│   ├── Services/               Microservicios, una carpeta por dominio de negocio
│   ├── Shared/                 Bloques reutilizables: abstracciones sobre la infraestructura
│   └── Tests/                  Tests unitarios y de API
├── tools/                      Configuración de la infraestructura local y scripts
├── docs/                       Documentación de los proyectos de Shared
├── assets/                     Imágenes de este README
├── docker-compose.yaml         Infraestructura: RabbitMQ, Consul, Vault, MongoDB, MySQL, Graylog...
├── GlobalSettings.msbuild      Propiedades comunes a todos los proyectos (framework, versión, autor)
└── Scattered.slnx              Solución con todos los proyectos
```

Las cuatro carpetas de `src` forman capas. Las de arriba dependen de las de abajo, nunca al revés:

```mermaid
flowchart TD
    Cliente(["Cliente / Tenant"])

    subgraph SRC["src"]
        direction TB
        API["<b>Api</b><br/>API Gateways<br/>enrutan y protegen la entrada"]
        SERVICES["<b>Services</b><br/>Microservicios<br/>lógica de negocio por dominio"]
        SHARED["<b>Shared</b><br/>Building blocks<br/>abstracciones de infraestructura"]
        TESTS["<b>Tests</b><br/>verifican Services y Shared"]
    end

    INFRA[("<b>Infraestructura</b><br/>docker-compose.yaml + tools")]

    Cliente -->|"HTTP"| API
    API -->|"HTTP o mensajes"| SERVICES
    API -->|"usa"| SHARED
    SERVICES -->|"usa"| SHARED
    SHARED -->|"se conecta a"| INFRA
    TESTS -.->|"prueba"| SERVICES
    TESTS -.->|"prueba"| SHARED
```

La idea central del proyecto es que los microservicios **no conocen la tecnología concreta**: hablan con interfaces de `Shared` (publicar un mensaje, leer un secreto, descubrir un servicio) y es `Shared` quien sabe que detrás hay RabbitMQ, Vault o Consul. Eso es lo que el README llama ser *vendor free*.

### 2. `src/Api`: los API Gateways

Un API Gateway es el único punto de entrada para los clientes: recibe la petición y la reenvía al microservicio que corresponde, de modo que el cliente no necesita saber cuántos servicios hay ni dónde están. Aquí se implementan con [YARP](https://github.com/microsoft/reverse-proxy) (reverse proxy), configurado en el `appsettings.json` de cada proyecto.

```text
src/Api/
├── Public/Scattered.API.Public      Gateway para clientes públicos
│                                      /order-ms/*    → API de Orders
│                                      /product-ms/*  → API de Products (escritura)
│                                      POST /subscribe → publica un mensaje (no llama por HTTP)
└── Private/Scattered.API.Private    Gateway para tenants (clientes con contrato)
                                       exige cabecera apiKey (API Key)
                                       aplica límite de peticiones (Rate Limiting)
                                       /reports/*     → API de Orders
```

### 3. `src/Services`: los microservicios

Cada subcarpeta es un **dominio** (bounded context) y podría vivir en su propio repositorio. Dentro, un dominio se divide en varios proyectos según su responsabilidad:

| Tipo de proyecto | Objetivo | Se ejecuta |
|---|---|---|
| **API** (`Scattered.Services.X` o `.Api.Read` / `.Api.Write`) | Expone los endpoints HTTP del dominio | Sí |
| **Consumer** (`.Consumer`) | Proceso que escucha colas de RabbitMQ y ejecuta un *handler* por cada mensaje | Sí |
| **BusinessLogic** (`.BusinessLogic`) | Casos de uso y acceso a datos, compartidos por la API y el Consumer | No (librería) |
| **Dtos** (`.Dtos` / `.Dto`) | Contratos: los objetos que viajan por HTTP o por mensajes. Es lo único que otro dominio puede referenciar | No (librería) |

```text
src/Services/
├── Subscriptions/                                 Dominio: suscripciones al boletín
│   ├── Scattered.Services.Subscriptions             API: POST/DELETE /subscription; publica un mensaje de integración
│   │   └── Controllers/
│   ├── Scattered.Services.Subscriptions.Consumer    Consumer: procesa el alta de la suscripción
│   │   ├── Controllers/                               endpoint para reiniciar el consumidor
│   │   └── Handler/                                   SubscriptionHandler, UnSubscriptionHandler
│   └── Scattered.Services.Subscriptions.Dtos        Contratos: SubscriptionDto, UnSubscriptionDto
│
├── Emails/                                        Dominio: envío de correos
│   └── Scattered.Services.Emails                    API: POST /email (el envío real está pendiente)
│       └── Controllers/
│
├── Products/                                      Dominio: catálogo de productos (aplica CQRS)
│   ├── Scattered.Services.Products.Api.Write        API de escritura: crea y modifica productos en MySQL
│   │   └── Controllers/
│   ├── Scattered.Services.Products.Api.Read         API de lectura: devuelve productos desde MongoDB
│   ├── Scattered.Services.Products.Consumer         Consumer: copia cada cambio al modelo de lectura
│   │   ├── Controllers/
│   │   └── Handlers/                                  ProductCreatedHandler, ProductUpdatedHandler
│   ├── Scattered.Services.Products.BusinessLogic    Librería compartida por los tres anteriores
│   │   ├── UseCases/                                  CreateProductDetails, UpdateProductDetails
│   │   └── DataAccess/                                ProductsWriteStore (MySQL), ProductsReadStore (MongoDB)
│   └── Scattered.Services.Products.Dtos             Contratos: peticiones, respuestas y eventos de producto
│
└── Orders/                                        Dominio: pedidos (aplica Event Sourcing)
    ├── Scattered.Services.Orders                    API: crea, consulta y cambia el estado de un pedido
    │   ├── Controllers/
    │   ├── Services/                                  Un caso de uso por clase: CreateOrderService, GetOrderService...
    │   ├── Aggregates/                                OrderDetails: el agregado que se reconstruye desde sus eventos
    │   ├── Events/                                    OrderCreated, OrderPaid, OrderDispatched, OrderCompleted
    │   └── Data/                                      OrderRepository: guarda y carga el agregado del event store
    ├── Scattered.Services.Orders.Consumer           Consumer: reacciona a eventos de pedidos y de productos
    │   ├── Controllers/
    │   └── Handler/                                   OrderCreatedHandler, ProductModifierHandler
    ├── Scattered.Services.Orders.BusinessLogic      Librería compartida por la API y el Consumer
    │   ├── Services/External/                         ProductNameService: obtiene el nombre de un producto
    │   ├── Data/External/                             ProductRepository: copia local de nombres de producto
    │   └── HealthChecks/                              ProductsHealthCheck: comprueba la API de Products
    └── Scattered.Services.Orders.Dto                Contratos: OrderRequest, OrderResponse
```

### 4. `src/Shared`: los bloques reutilizables

Cada proyecto de `Shared` resuelve una necesidad transversal y esconde la tecnología que la implementa.

```text
src/Shared/
├── Scattered.Shared.Setup                   Punto de entrada común: arma una aplicación web con todo lo demás
│   ├── API/                                   DefaultScatteredWebApplication, health checks
│   │   ├── Key/                                 Middleware de API Key
│   │   └── RateLimiting/                        Política de límite de peticiones
│   ├── Services/                              Registro de service bus, secretos, discovery y event sourcing
│   ├── Databases/                             Registro de MySQL y MongoDB
│   └── Observability/                         Trazas y métricas con OpenTelemetry
├── Shared.Communication/
│   ├── Scattered.Shared.Communication         Abstracción de mensajería: mensajes, publicadores, consumidores, handlers
│   │   ├── Messages/                            IntegrationMessage, DomainMessage, Metadata
│   │   ├── Publisher/                           Publicadores de dominio y de integración
│   │   └── Consumer/                            Host del consumidor, gestor y registro de handlers
│   └── Scattered.Shared.Communication.RabbitMQ  Implementación de lo anterior con RabbitMQ
│       ├── Publisher/
│       └── Consumer/
├── Shared.Databases/
│   ├── Scattered.Shared.Databases.MySql       Registro de DbContext de EF Core sobre MySQL y su health check
│   └── Scattered.Shared.Databases.MongoDb     Conexión a MongoDB (credenciales desde Vault) y su health check
├── Scattered.Shared.EventSourcing           Agregados, repositorio de agregados y event store sobre MongoDB
│   └── EventStores/
├── Scattered.Shared.Discovery               Localiza servicios por nombre consultando Consul (con caché)
├── Scattered.Shared.Secrets                 Lee credenciales desde Vault
├── Scattered.Shared.Logging                 Logs estructurados con Serilog hacia consola y Graylog
│   └── Loggers/
└── Scattered.Shared.Serialization           Serialización de mensajes a JSON
```

| Proyecto | Necesidad que resuelve | Patrón o concepto | Tecnología detrás |
|---|---|---|---|
| `Shared.Setup` | Que todos los servicios arranquen igual, con una sola llamada | Composición / *cross-cutting concerns* | ASP.NET Core |
| `Shared.Communication` | Comunicación asíncrona entre servicios | Productor/Consumidor, mensajes de dominio y de integración | — (solo interfaces) |
| `Shared.Communication.RabbitMQ` | Implementar esa comunicación | Service bus, *dead letter queue* | RabbitMQ |
| `Shared.Databases.MySql` | Base de datos relacional | Write store | EF Core + MySQL |
| `Shared.Databases.MongoDb` | Base de datos documental | Read store, event store | MongoDB |
| `Shared.EventSourcing` | Guardar el estado como una secuencia de eventos | Event Sourcing, Aggregate | MongoDB |
| `Shared.Discovery` | Saber dónde está cada servicio sin fijar direcciones | Service Registry / Service Discovery | Consul |
| `Shared.Secrets` | No tener credenciales en el código ni en la configuración | Secret store | Vault |
| `Shared.Logging` | Centralizar los logs de todos los servicios | Logging centralizado | Serilog + Graylog |
| `Shared.Serialization` | Formato común de los mensajes | — | Newtonsoft.Json |
| `Shared.Setup/Observability` | Seguir una petición a través de varios servicios y medirlos | Trazas distribuidas y métricas | OpenTelemetry → Zipkin, Prometheus, Grafana |
| `Shared.Setup/API` | Saber si un servicio y sus dependencias están vivos | Health checks | AspNetCore.HealthChecks |

### 5. `src/Tests`, `tools` y `docs`

```text
src/Tests/
├── Services/Orders/...BusinessLogicTests        Tests unitarios de ProductNameService
├── Services/Subscriptions/...ApiTests           Test de la API de Subscriptions (actualmente omitido)
└── Shared/Discovery/...Discovery.Tests          Tests unitarios de ConsulServiceDiscovery

tools/
├── local-development/     up.sh (infraestructura) y run-services.sh (servicios)
├── rabbitmq/              Exchanges, colas y enlaces que se cargan al arrancar RabbitMQ
├── consul/                Registro de la infraestructura y de los servicios en Consul
├── vault/                 Alta de los secretos (RabbitMQ, MongoDB, MySQL)
├── mysql/                 Script de creación de la base de datos de escritura
├── mongodb/               Script de inicialización de MongoDB
└── telemetry/             Configuración del colector de OpenTelemetry, Prometheus y Grafana

docs/
├── communication/         Cómo funciona Shared.Communication
├── discovery/             Cómo funciona Shared.Discovery
└── secrets/               Cómo funciona Shared.Secrets
```

### 6. Dependencias entre proyectos (compilación)

Estas son las referencias reales entre proyectos (`ProjectReference`). Todos los servicios entran a `Shared` por un único proyecto, `Shared.Setup`. Entre dominios solo se referencian los proyectos de contratos (`Dtos`), nunca la lógica.

```mermaid
flowchart LR
    subgraph API["Api"]
        PUB["API.Public"]
        PRI["API.Private"]
    end

    subgraph SUBS["Services / Subscriptions"]
        S_API["Subscriptions"]
        S_CON["Subscriptions.Consumer"]
        S_DTO["Subscriptions.Dtos"]
    end

    subgraph EMAILS["Services / Emails"]
        E_API["Emails"]
    end

    subgraph PRODUCTS["Services / Products"]
        P_W["Products.Api.Write"]
        P_R["Products.Api.Read"]
        P_CON["Products.Consumer"]
        P_BL["Products.BusinessLogic"]
        P_DTO["Products.Dtos"]
    end

    subgraph ORDERS["Services / Orders"]
        O_API["Orders"]
        O_CON["Orders.Consumer"]
        O_BL["Orders.BusinessLogic"]
        O_DTO["Orders.Dto"]
    end

    SETUP(["Shared.Setup"])

    PUB --> SETUP
    PUB --> S_DTO
    PRI --> SETUP

    S_API --> S_DTO
    S_CON --> S_DTO
    S_API --> SETUP
    S_CON --> SETUP

    E_API --> SETUP

    P_W --> P_BL
    P_R --> P_BL
    P_CON --> P_BL
    P_BL --> P_DTO
    P_BL --> SETUP

    O_API --> O_BL
    O_CON --> O_BL
    O_API --> O_DTO
    O_CON --> O_DTO
    O_API --> SETUP
    O_CON --> SETUP
    O_BL --> SETUP
    O_BL -->|"contrato de otro dominio"| P_DTO
    O_DTO --> P_DTO
```

Y así se relacionan los proyectos dentro de `Shared`:

```mermaid
flowchart TD
    SETUP(["Shared.Setup"])

    COM_R["Shared.Communication.RabbitMQ"]
    COM["Shared.Communication"]
    ES["Shared.EventSourcing"]
    MONGO["Shared.Databases.MongoDb"]
    MYSQL["Shared.Databases.MySql"]
    DISC["Shared.Discovery"]
    SEC["Shared.Secrets"]
    LOG["Shared.Logging"]
    SER["Shared.Serialization"]

    SETUP --> COM_R
    SETUP --> ES
    SETUP --> MYSQL
    SETUP --> DISC
    SETUP --> SEC
    SETUP --> LOG
    SETUP --> SER

    COM_R --> COM
    COM_R --> SER
    ES --> MONGO
    MONGO --> DISC
    MONGO --> SEC
    LOG --> DISC
```

### 7. Comunicación entre servicios (ejecución)

En ejecución hay dos formas de comunicación:

- **Síncrona (HTTP):** quien llama espera la respuesta. Se usa en la entrada por los gateways y cuando Orders necesita un dato de Products.
- **Asíncrona (mensajes por RabbitMQ):** quien publica no espera; uno o varios consumidores procesan el mensaje después. Es lo que permite que los servicios no dependan unos de otros para responder.

Los mensajes son de dos tipos: **de dominio** (los escucha el propio dominio que los generó) y **de integración** (los escuchan otros dominios).

**Llamadas síncronas (HTTP) y bases de datos.** Cada dominio es dueño de sus datos: ningún servicio lee la base de datos de otro.

```mermaid
flowchart LR
    Cliente(["Cliente"])
    Tenant(["Tenant"])

    PUB["API.Public"]
    PRI["API.Private<br/>API Key + Rate Limiting"]

    O_API["Orders (API)"]
    P_W["Products.Api.Write"]
    P_R["Products.Api.Read"]

    M_EVT[("MongoDB<br/>event store de pedidos")]
    MYSQL[("MySQL<br/>write store de productos")]
    M_READ[("MongoDB<br/>read store de productos")]

    Cliente --> PUB
    Tenant --> PRI
    PUB -->|"/order-ms"| O_API
    PUB -->|"/product-ms"| P_W
    PRI -->|"/reports"| O_API
    O_API -->|"datos del producto"| P_R
    O_API --> M_EVT
    P_W --> MYSQL
    P_R --> M_READ
```

**Mensajes (RabbitMQ).** Quien publica envía el mensaje a un *exchange* con una clave de enrutado; el exchange lo copia a las colas enlazadas con esa clave, y cada cola lo guarda hasta que su consumidor lo procesa. En el diagrama los hexágonos son exchanges, los paralelogramos son colas y la etiqueta de cada flecha es la clave de enrutado.

```mermaid
flowchart LR
    PUB["API.Public<br/>POST /subscribe"]
    S_API["Subscriptions (API)"]
    P_W["Products.Api.Write"]
    P_CON["Products.Consumer"]
    O_API["Orders (API)"]

    X_PUB{{"api.public.exchange"}}
    X_SUB{{"subscription.exchange"}}
    X_PRO{{"products.exchange"}}
    X_ORD{{"order.exchange"}}

    Q_SUB[/"subscription-queue"/]
    Q_PD[/"product-domain-queue"/]
    Q_P[/"product-queue<br/>(sin consumidor)"/]
    Q_OD[/"order-domain-queue"/]
    Q_O[/"order-queue"/]

    S_CON["Subscriptions.Consumer"]
    P_CON2["Products.Consumer"]
    O_CON["Orders.Consumer"]

    M_READ[("MongoDB<br/>read store de productos")]
    M_NAMES[("MongoDB<br/>nombres de producto")]

    PUB -->|"subscription"| X_PUB
    X_PUB -->|"subscription"| X_SUB
    S_API --> X_SUB
    X_SUB -->|"subscription"| Q_SUB
    Q_SUB --> S_CON

    P_W -->|"internal"| X_PRO
    P_CON -->|"external"| X_PRO
    X_PRO -->|"internal"| Q_PD
    X_PRO -->|"external"| Q_P
    X_PRO -->|"external"| X_ORD
    Q_PD --> P_CON2
    P_CON2 --> M_READ

    O_API -->|"order"| X_ORD
    X_ORD -->|"order"| Q_OD
    X_ORD -->|"external"| Q_O
    Q_OD --> O_CON
    Q_O --> O_CON
    O_CON --> M_NAMES
```

`Products.Consumer` aparece dos veces porque hace las dos cosas: consume el mensaje de dominio (clave `internal`), actualiza el modelo de lectura y publica después un mensaje de integración (clave `external`) para los demás dominios. Ese mensaje llega a Orders porque `products.exchange` está enlazado con `order.exchange`.

Cada cola tiene además una cola `*.dead-letter` asociada, a la que van los mensajes que no se pudieron procesar. `product-queue` todavía no tiene consumidor.

Todos los servicios, además, consultan **Consul** para localizar la infraestructura, leen sus credenciales de **Vault** y envían logs a **Graylog** y telemetría al colector de **OpenTelemetry**; no se dibuja para mantener los diagramas legibles.

#### El patrón productor-consumidor, a gran escala

Toda la mensajería anterior es una aplicación del patrón **productor-consumidor**: separar a quien genera un dato de quien lo procesa, poniendo una cola en medio. La imagen muestra el sistema completo leído de izquierda a derecha; cada círculo es un componente y cada línea es el camino real de un mensaje.

![Patrón productor-consumidor en Scattered](assets/producer-consumer.svg)

**Cómo leer la imagen.** Cada columna es un papel del patrón y cada franja horizontal es un dominio. Las líneas azules son mensajes de dominio (no salen de su franja) y las rosas son mensajes de integración (son las únicas que cruzan de una franja a otra). Los puntos que recorren las líneas indican el sentido del mensaje.

| Pieza del patrón | En Scattered | Qué hace |
|---|---|---|
| **Productor** | `API.Public`, `Subscriptions`, `Products.Api.Write`, `Products.Consumer`, `Orders` | Publica el mensaje en un exchange y sigue con su trabajo, sin esperar a nadie. |
| **Cola (búfer)** | Las colas de RabbitMQ: `subscription-queue`, `product-domain-queue`, `order-domain-queue`, `order-queue` | Guarda cada mensaje hasta que un consumidor lo procesa. |
| **Consumidor** | `Subscriptions.Consumer`, `Products.Consumer`, `Orders.Consumer` | Toma los mensajes de su cola a su ritmo y ejecuta el handler correspondiente. |

RabbitMQ añade una pieza que el patrón básico no tiene: el **exchange**. El productor no escribe directamente en la cola, sino en un exchange que decide, según la clave de enrutado, a qué colas copiar el mensaje. Eso permite que un mismo mensaje llegue a varios consumidores sin que el productor lo sepa.

**Qué se gana con ello**, visto en este proyecto:

- **Desacoplamiento.** `Products.Api.Write` no sabe que existe `Orders`. Publica que un producto cambió y termina; añadir un nuevo interesado es enlazar otra cola al exchange, sin tocar al productor.
- **Control de velocidades.** Si `Orders.Consumer` está caído o va lento, los mensajes esperan en `order-queue` y se procesan cuando vuelve. La petición HTTP original ya respondió hace tiempo.
- **Escalabilidad.** Se pueden arrancar varias instancias de un mismo consumidor sobre la misma cola y RabbitMQ reparte los mensajes entre ellas.

Dos detalles que la imagen deja a la vista:

- `Products.Consumer` aparece en las dos columnas porque es **consumidor y productor a la vez**: consume el mensaje de dominio, actualiza el modelo de lectura y publica el de integración.
- El punto rojo bajo cada cola es su cola `*.dead-letter`: ahí acaban los mensajes que el consumidor no pudo procesar, para que un fallo no bloquee al resto.

### 8. Dos flujos explicados paso a paso

**Crear un producto: CQRS y consistencia eventual.** CQRS separa las escrituras de las lecturas en modelos (y aquí también bases de datos) distintos. La escritura se confirma en MySQL y el modelo de lectura en MongoDB se actualiza después mediante un mensaje; durante ese instante la lectura aún no ve el producto, y eso es la *consistencia eventual*.

```mermaid
sequenceDiagram
    actor C as Cliente
    participant W as Products.Api.Write
    participant DB as MySQL (write store)
    participant R as RabbitMQ
    participant PC as Products.Consumer
    participant M as MongoDB (read store)
    participant OC as Orders.Consumer
    participant RD as Products.Api.Read

    C->>W: POST /product
    W->>DB: guarda el producto
    W-)R: ProductCreated (mensaje de dominio)
    W-->>C: 201 + URL de lectura
    R-)PC: product-domain-queue
    PC->>M: crea la vista del producto
    PC-)R: ProductUpdated (mensaje de integración)
    R-)OC: order-queue
    OC->>OC: guarda el nombre del producto en su propia base
    C->>RD: GET /product/{id}
    RD->>M: lee la vista
    RD-->>C: producto
```

**Crear un pedido: Event Sourcing.** En lugar de guardar el estado actual del pedido, se guardan los eventos que le han ocurrido (`OrderCreated`, `OrderPaid`...). El estado se obtiene volviendo a aplicar esos eventos sobre el agregado `OrderDetails`.

```mermaid
sequenceDiagram
    actor C as Cliente
    participant O as Orders (API)
    participant A as OrderDetails (agregado)
    participant ES as MongoDB (event store)
    participant P as Products.Api.Read
    participant R as RabbitMQ
    participant OC as Orders.Consumer

    C->>O: POST /order/create
    O->>A: Apply(OrderCreated)
    O->>ES: guarda el evento
    O->>P: GET /product/{id} (nombre, con caché)
    O-)R: OrderResponse (mensaje de dominio)
    O-->>C: 201 + orderId
    R-)OC: order-domain-queue
    Note over OC: OrderCreatedHandler
    C->>O: GET /order/{id}
    O->>ES: carga los eventos del pedido
    O->>A: los vuelve a aplicar
    O-->>C: estado actual del pedido
```

### 9. Glosario: dónde está cada patrón en el código

| Patrón o término | Qué es | Dónde verlo |
|---|---|---|
| **Microservicio** | Servicio pequeño, desplegable por separado, dueño de sus datos | Cada dominio de `src/Services` |
| **API Gateway** | Punto de entrada único que enruta hacia los servicios | `src/Api` (YARP) |
| **API Key** | Clave que identifica al cliente que llama | `Shared.Setup/API/Key`, usada en `API.Private` |
| **Rate Limiting** | Límite de peticiones por cliente y ventana de tiempo | `Shared.Setup/API/RateLimiting`, usada en `API.Private` |
| **Productor/Consumidor** | Un servicio publica mensajes y otro los procesa más tarde | `Shared.Communication`, proyectos `.Consumer` |
| **Mensaje de dominio / de integración** | Evento interno del dominio / evento para otros dominios | `Shared.Communication/Messages` |
| **Dead letter queue** | Cola donde acaban los mensajes que fallan | `tools/rabbitmq/definitions.json` |
| **CQRS** | Separar el modelo de escritura del de lectura | `Products.Api.Write`, `Products.Api.Read`, `Products.BusinessLogic/DataAccess` |
| **Consistencia eventual** | Los datos convergen tras un breve retraso, no al instante | `Products.Consumer/Handlers` |
| **Event Sourcing** | Persistir eventos en lugar del estado | `Shared.EventSourcing`, `Orders/Aggregates`, `Orders/Events` |
| **Aggregate** | Objeto de dominio que agrupa estado y reglas, y aplica eventos | `Orders/Aggregates/OrderDetails.cs` |
| **Service Discovery / Registry** | Localizar servicios por nombre en tiempo de ejecución | `Shared.Discovery`, `tools/consul` |
| **Secret store** | Almacén centralizado de credenciales | `Shared.Secrets`, `tools/vault` |
| **Health check** | Endpoint que informa del estado del servicio y sus dependencias | `Shared.Setup/API`, `/health` y `/health-ui` en cada servicio |
| **Observabilidad** | Logs, métricas y trazas para entender el sistema en ejecución | `Shared.Logging`, `Shared.Setup/Observability`, `tools/telemetry` |
| **Railway Oriented Programming** | Encadenar pasos que pueden fallar devolviendo un `Result` | `Orders/Services/CreateOrderService.cs` |
| **Vendor free** | El negocio depende de interfaces, no de un proveedor | Toda la carpeta `src/Shared` |

Pendiente en el código respecto al diagrama de arquitectura del inicio: el patrón SAGA, la caché distribuida real (ahora es en memoria), el envío de correos, la baja de suscripciones y la base de datos de Subscriptions.

## ¿Cómo ejecutar la aplicación? 

Para ejecutar la aplicación correctamente debes tener [Docker](https://www.netmentor.es/curso/docker) instalado en tu máquina y entender cómo funciona docker-compose.

El repositorio incluye un fichero `docker-compose.yaml` que ya contiene toda la configuración necesaria para que una vez ejecutes la solución, esta funcione sin problemas.

Eventualmente lo veremos toda la configuración en kubernetes (con [project tye](https://github.com/dotnet/tye)).

Para poder ejecutar la aplicación correctamente en local, necestiaras ejecutar tanto el fichero `docker-compose` como la configuración para los diferentes servicios utilizados. 

Para ahorrar tiempo hay un fichero `bash` que ejectua todo `./tools/local-development/up.sh`

Y luego simplemente ejecuta cada aplicacion de forma individual o en un compound para poder testearlas.

En esta copia hay además un segundo fichero que compila la solución y arranca todos los servicios a la vez (necesitas el SDK de .NET 10):

```bash
./tools/local-development/up.sh            # infraestructura
./tools/local-development/run-services.sh  # servicios
```

Los logs de cada servicio quedan en `tools/local-development/logs` y con `Ctrl+C` se detienen todos.

## Cambios respecto al proyecto original

Esta sección documenta todo lo que se modificó en esta copia para que la aplicación compile y levante hoy en local. El proyecto original apuntaba a .NET 8 y a versiones de paquetes e imágenes de la época de .NET 6; varias de ellas ya no existen o cambiaron de comportamiento.

Entorno en el que se verificó: macOS (Apple Silicon, arm64), SDK de .NET 10.0.403 y Docker 29.9.2.

### Resumen por commit

| Commit | Contenido |
|---|---|
| `f894488` | Arreglos de la infraestructura local (Docker, OpenTelemetry Collector, Consul, permisos de scripts) |
| `1391701` | Arreglos de arranque del gateway privado y de la API de Subscriptions |
| `5da9951` | Fase 1: migración de .NET 8 a .NET 10 |
| `f535c42` | Fase 2: eliminación de dependencias con vulnerabilidades conocidas |
| `378faa5` | Fase 3: actualización de paquetes de bajo esfuerzo |
| `b9fbe77` | Script para compilar y arrancar todos los servicios |

### 1. Infraestructura local

| Archivo | Problema | Cambio |
|---|---|---|
| `docker-compose.yaml` | La imagen `bitnami/prometheus` ya no se publica en Docker Hub y abortaba todo el `docker-compose up` | Se usa la imagen oficial `prom/prometheus` |
| `tools/telemetry/otel-collector-config.yaml` | El colector actual eliminó el exportador `logging` y se caía al arrancar | Se sustituye por el exportador `debug` en los tres pipelines |
| `tools/telemetry/otel-collector-config.yaml` | El receptor OTLP ya no escucha por defecto en todas las interfaces | Se fija `endpoint: 0.0.0.0:4317` en el receptor gRPC |
| `tools/consul/config.sh` | La CLI de Consul actual rechaza direcciones con esquema (`http://...`), por lo que fallaban 6 de los 11 registros (Vault y todas las APIs) | Esos servicios se registran por la API HTTP de Consul (`PUT /v1/agent/service/register`) mediante la función `register_with_scheme` |
| `tools/local-development/up.sh`, `tools/vault/config.sh`, `tools/consul/config.sh` | No tenían permiso de ejecución | Se marcan como ejecutables |

### 2. Arreglos de arranque

| Archivo | Problema | Cambio |
|---|---|---|
| `src/Api/Private/Scattered.API.Private/Program.cs` | El gateway privado llamaba a `UseRateLimiter()` sin registrar sus servicios y se caía al iniciar | Se añade `AddRateLimiter` |
| `src/Services/Subscriptions/Scattered.Services.Subscriptions/Properties/launchSettings.json` | Faltaba la variable `VAULT-TOKEN` que sí tienen los demás proyectos; `/health` devolvía 500 | Se añade `VAULT-TOKEN` al perfil de arranque |

### 3. Fase 1: migración a .NET 10

.NET 8 llegó al final de su soporte, así que la solución se migró a .NET 10 (LTS).

- `TargetFramework` pasa de `net8.0` a `net10.0` en `GlobalSettings.msbuild` y en los 20 `.csproj` que lo declaraban.
- Se elimina el `LangVersion` fijado en `12.0` de `GlobalSettings.msbuild`; se usa la versión de C# que trae el SDK.
- `.github/workflows/build&Test.yml` compila con `dotnet-version: 10.0.x`.
- Se eliminan dos proyectos huérfanos que no formaban parte de la solución: `Scattered - Backup.Services.Products.csproj` (net6.0) y `src/Shared/Scattered.Shared.Api` (net7.0). El primero impedía ejecutar `dotnet run --project` sobre la carpeta de `Products.Api.Write`.

No hizo falta cambiar código C# para esta fase.

### 4. Fase 2: dependencias con vulnerabilidades conocidas

Al compilar para .NET 10 el SDK reporta las vulnerabilidades de los paquetes transitivos. Había 16 paquetes afectados (uno crítico); tras esta fase `dotnet list package --vulnerable --include-transitive` no devuelve ninguno.

| Paquete | Antes | Ahora | Motivo |
|---|---|---|---|
| `Microsoft.AspNetCore.Mvc` | 2.2.0 | eliminado | Arrastraba `System.Drawing.Common` (crítica). Se sustituye por `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` en `Scattered.Shared.Communication` |
| `AspNetCore.HealthChecks.UI`, `.UI.Client`, `.UI.InMemory.Storage` | 6.0.4 | 9.0.0 | Arrastraban `KubernetesClient`, `AutoMapper`, SQLite y JWT vulnerables |
| `AspNetCore.HealthChecks.MySql` | 6.0.2 | 9.0.0 | Arrastraba `MySqlConnector` vulnerable |
| `AspNetCore.HealthChecks.MongoDb` | 6.0.2 | 9.0.0 | Requerido por el nuevo driver de MongoDB |
| `MongoDB.Driver` | 2.19.0 | 3.13.0 | Arrastraba `Snappier` y `SharpCompress` vulnerables |
| `Microsoft.EntityFrameworkCore` | 6.0.3 | 10.0.12 | Arrastraba `Microsoft.Extensions.Caching.Memory` vulnerable |
| `MySql.EntityFrameworkCore` | 6.0.0 | 10.0.12 | Debe coincidir con la versión de EF Core |
| `OpenTelemetry.Exporter.Console`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.3.0 | 1.21.0 | `OpenTelemetry.Api` vulnerable |
| `OpenTelemetry.Extensions.Hosting` | 1.0.0-rc9.4 | 1.21.0 | Ídem |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.8.1 | 1.21.0 | Ídem |
| `Consul` | 1.6.10.3 | 1.8.0 | Arrastraba `Newtonsoft.Json` 10.0.3 vulnerable |
| `Microsoft.Extensions.*` (Caching.Memory, Configuration.Abstractions, DependencyInjection, DependencyInjection.Abstractions, Hosting.Abstractions, Http, Options.ConfigurationExtensions) | 2.2.0 / 3.1.3 / 6.0.0 | 10.0.12 | Evitar degradaciones de versión (NU1605) con los paquetes anteriores |
| `Microsoft.NET.Test.Sdk` | 16.11.0 / 17.4.0 | 18.12.0 | Arrastraba `Newtonsoft.Json` antiguo |
| `Microsoft.AspNetCore.Mvc.Testing` | 6.0.2 | 10.0.12 | Alineado con .NET 10 |

Paquetes que se fijan explícitamente aunque el código no los usa de forma directa:

| Paquete | Versión | Motivo |
|---|---|---|
| `MySqlConnector` | 2.6.2 | La versión que trae `HealthChecks.MySql` 9.0.0 sigue siendo vulnerable (GHSA-473q-m89c-ghf8) |
| `KubernetesClient` | 17.0.14 | La versión que trae `HealthChecks.UI` 9.0.0 sigue siendo vulnerable (GHSA-w7r3-mgwf-4mqq) |
| `IdentityModel` | 5.2.0 | `HealthChecks.UI` lo necesita y el `KubernetesClient` fijado ya no lo incluye; sin él el recolector de health checks falla |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.12 | `HealthChecks.UI.InMemory.Storage` trae el proveedor de EF Core 8, incompatible con EF Core 10; sin él los servicios no arrancan |

Cambios de código que exigieron las nuevas versiones:

| Archivo | Cambio |
|---|---|
| `src/Shared/Scattered.Shared.Setup/Observability/OpenTelemetry.cs` | `AddOpenTelemetryTracing` y `AddOpenTelemetryMetrics` ya no existen; se usa `AddOpenTelemetry().WithTracing(...)` y `.WithMetrics(...)` |
| `src/Shared/Shared.Databases/Scattered.Shared.Databases.MongoDb/MongoDbDependencyInjection.cs` | Nueva firma de `AddMongoDb` en el health check (recibe una factoría de `IMongoClient`) |
| Ídem | Se registra `GuidSerializer(GuidRepresentation.CSharpLegacy)`: el driver 3.x exige indicar la representación de `Guid`, y esta es la que usaba el driver 2.x por defecto |
| Ídem | Se registra un `ObjectSerializer` que permite los tipos `Scattered.*`. Sin él, guardar eventos en el event store fallaba con error 500 al crear un pedido (este fallo ya existía antes de la actualización) |
| `src/Shared/Shared.Communication/Scattered.Shared.Communication.RabbitMQ/RabbitMQDependencyInjection.cs` | Se elimina un `using Microsoft.AspNetCore.JsonPatch.Adapters` sin uso, que dependía del paquete MVC 2.2 |
| `src/Shared/Scattered.Shared.Discovery/ConsulServiceDiscovery.cs`, `DiscoveryDependencyInjection.cs` | Ajustes de nulabilidad por las anotaciones de los paquetes nuevos |
| `src/Tests/Shared/Discovery/.../ConsulServiceDiscoveryTest.cs` | `Consul` 1.8.0 añade una sobrecarga de `Catalog.Service`; los mocks usan `default(CancellationToken)` para evitar la ambigüedad |

### 5. Fase 3: actualización de paquetes de bajo esfuerzo

| Paquete | Antes | Ahora |
|---|---|---|
| `Yarp.ReverseProxy` | 1.1.2 | 2.3.0 |
| `Serilog` | 2.10.0 | 4.4.0 |
| `Serilog.AspNetCore` | 5.0.0 | 10.0.0 |
| `Serilog.Sinks.Console` | 4.0.1 | 6.1.1 |
| `Serilog.Sinks.Graylog` | 2.3.0 | 3.1.1 |
| `Swashbuckle.AspNetCore` | 6.4.0 | 10.3.1 |
| `VaultSharp` | 1.7.0.4 | 1.17.5.1 |
| `Scrutor` | 4.1.0 | 7.0.0 |
| `Newtonsoft.Json` | 13.0.1 | 13.0.4 |
| `xunit` | 2.4.1 / 2.4.2 | 2.9.3 |
| `xunit.runner.visualstudio` | 2.4.3 / 2.4.5 | 4.0.1 |
| `Moq` | 4.17.2 / 4.18.2 | 4.21.0 |
| `coverlet.collector` | 3.1.0 | 10.1.0 |

Cambios de código asociados:

| Archivo | Cambio |
|---|---|
| `src/Shared/Scattered.Shared.Secrets/Extensions/ObjectExtensions.cs` | `VaultSharp` devuelve ahora los valores de los secretos como `JsonElement`; se convierten al tipo de la propiedad. Sin este cambio 6 de los 10 servicios no arrancaban |
| `src/Shared/Scattered.Shared.Logging/Loggers/LoggerConfigurationExtensions.cs` | Cambió la firma de `WriteTo.Graylog`; el nivel mínimo se pasa como argumento con nombre (`minimumLogEventLevel`) |
| `src/Tests/Shared/Discovery/.../ConsulServiceDiscoveryTest.cs` | El analizador de xunit detectó un `Assert.ThrowsAsync` sin `await`; el test pasa a ser `async Task` |

### 6. Script de arranque

Se añade `tools/local-development/run-services.sh`, que compila la solución y arranca los 10 servicios (ver [¿Cómo ejecutar la aplicación?](#cómo-ejecutar-la-aplicación)).

### Requisito adicional: certificado HTTPS de desarrollo

Los servicios se llaman entre sí por HTTPS. Si el certificado de desarrollo de .NET no es de confianza, Orders aparece `Unhealthy`, `GET /order/{id}` falla y los gateways devuelven 502. Se resuelve una sola vez con:

```bash
dotnet dev-certs https --trust
```

### Estado verificado

Con los cambios anteriores:

- La solución compila con 0 errores y 0 advertencias, y los tests pasan (8 correctos, 1 omitido).
- Los 12 contenedores y los 10 servicios levantan y responden `Healthy` en `/health`.
- Se probaron contra los servicios: crear y leer un producto, crear y consultar un pedido, crear una suscripción, el reenvío del gateway público, y la API Key y el rate limiting del gateway privado.

### Pendiente y limitaciones conocidas

- `GET /order/getorderstatus/{id}` no está implementado en el código original (lanza `NotImplementedException`).
- Graylog no trae ninguna entrada configurada: para recibir logs hay que crear una de tipo GELF UDP en el puerto 12201 desde `http://localhost:9000`. El envío de logs con el sink actualizado no se ha verificado.
- La imagen `graylog/graylog:4.2.5` es solo amd64; en Apple Silicon corre emulada.
- El colector de OpenTelemetry avisa de que un componente de la configuración saldrá de su imagen en la versión 0.167; como `docker-compose.yaml` usa `latest`, puede requerir un ajuste futuro.
- `RabbitMQ.Client` sigue en 6.2.2 y `AspNetCore.HealthChecks.Rabbitmq` en 6.0.2: la versión 7 del cliente tiene una API totalmente asíncrona y exige reescribir el publicador y los consumidores.
- `Netmentor.ROP.ApiExtensions` sigue en 1.0.10.
- `IdentityModel` 5.2.0 está marcado como obsoleto en NuGet; es necesario mientras se use `HealthChecks.UI` 9.

## Dale una estrella ⭐
Si te gusta el proyecto no dudes en darle una estrella, hacer un fork junto a una PR o incluso apoyar económicamente al autor original [donando un café](https://www.buymeacoffee.com/netmentor).
