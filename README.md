# Scattered

![Scattered](assets/Scattered.svg)

> **Nota:** Scattered es una copia renombrada de [Distribt](https://github.com/ElectNewt/Distribt), proyecto creado por Ivan Abad ([NetMentor](https://www.netmentor.es)) y publicado bajo licencia MIT. El curso y los artículos enlazados en este documento pertenecen al autor original.
>
> **Esta rama (`features/flow1`)** contiene el código de la rama [`01-Introduccion`](https://github.com/ElectNewt/Distribt/tree/01-Introduccion) del proyecto original (commit `051e361`), renombrado de Distribt a Scattered. Es el punto de partida para replicar el curso paso a paso. Qué contiene, cómo ejecutarla y cómo avanzar al siguiente capítulo: **[docs/flow/README.md](docs/flow/README.md)**.

## Estado de esta rama

| | |
|---|---|
| Rama | `features/flow1` |
| Capítulo del curso | 1 · Introducción |
| Rama original | [`01-Introduccion`](https://github.com/ElectNewt/Distribt/tree/01-Introduccion) (`051e361`) |
| Qué hay | Esqueleto: 2 APIs *Minimal API* y 4 microservicios *Web API* con datos fijos, sobre un arranque común (`DefaultScatteredWebApplication`) |
| Qué falta | Comunicación entre servicios, persistencia e infraestructura |
| Siguiente capítulo | 2 · [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) con YARP (`02-patron-api-gateway`) |

## ¿Qué es Scattered?

Scattered es un proyecto Open Source creado para mostrar el funcionamiento y uso de los sistemas distribuidos con las implementaciones en .NET Core.

La aplicación va a ser "vendor free" lo que quiere decir que no va a estar enlazada directamente a ningún proveedor específico. Obviamente vamos a utilizar X o Y servicio (ya que no vamos a reinventar la rueda), pero lo haremos a través de abstracciones.

Es importante saber estos conceptos ya que cada día las empresas están migrando sus aplicaciones monolíticas a microservicios o incluso serverless, y para poder aplicar un correcto funcionamiento, debemos aprender sobre sistemas distribuidos.

## ¿Qué vamos a ver?

Vamos a ver un sistema distribuido con múltiples características, como pueden ser patrón consumer/publisher, sagas, service discovery, Eventual consistency, etc.

Esta es la arquitectura planteada en el capítulo 1, marcada con lo que ya existe en esta rama:

![ScatteredDiagram](assets/diagram.png)

- Las cajas con borde continuo existen en el código; las de borde discontinuo y todas las flechas son el objetivo, todavía no existen.
- Las etiquetas amarillas representan el código ya implementado; las grises indican en qué capítulo llega cada pieza.
- La fuente editable del diagrama es [`assets/architecture.svg`](assets/architecture.svg).

Puedes encontrar el proceso de creación del sistema en [el curso de YouTube del autor original](https://www.youtube.com/playlist?list=PLesmOrW3mp4jpSbdFMtVWINJZ7OLdSASS).

Alternativamente, si lo prefieres, puedes seguir los post con el código detallado y los razonamientos sobre por qué se ha elegido cada tecnología en la web del autor original, [NetMentor - Curso Distribt](https://www.netmentor.es/curso/sistemas-distribuidos).

## Progreso del curso

Cada capítulo se replica en su propia rama `features/flowN`. Marcado = ya replicado en Scattered.

- [x] 1 · [API Rest](https://www.netmentor.es/entrada/api-rest-csharp): esqueleto de microservicios creados con .NET **← esta rama**
- [ ] 2 · [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) con YARP
- [ ] 3–4 · [Producers/Consumers](https://www.netmentor.es/entrada/patron-productor-consumidor) para la comunicación asíncrona [con RabbitMQ](https://www.netmentor.es/entrada/rabbitmq-comunicacion-asincrona)
- [ ] 5 · [Gestión de credenciales](https://www.netmentor.es/entrada/gestion-credenciales-vault) con Vault
- [ ] 6 · [Service discovery / registry](https://www.netmentor.es/entrada/service-registry-discovery-consul) con Consul
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

## Descripción del repositorio

Todo el contenido se encuentra en este mismo repositorio para facilitar ver cómo funcionan las diferentes piezas.

Nota: técnicamente cada carpeta representa un dominio y podría ser su propio repositorio independiente, pero para facilitar el desarrollo y el seguimiento de los vídeos y posts está todo en un único repo.

```text
Scattered/
├── src/
│   ├── Api/
│   │   ├── Public/Scattered.API.Public       Minimal API: GET / → "Hello World!"
│   │   └── Private/Scattered.API.Private     Minimal API: GET / → "Hello World!"
│   ├── Services/
│   │   ├── Emails/Scattered.Services.Emails                POST /email
│   │   ├── Orders/Scattered.Services.Orders                GET /order/{id} · POST /order
│   │   ├── Products/Scattered.Services.Products            GET /product/{id} · POST /product
│   │   └── Subscriptions/Scattered.Services.Subscriptions  POST · DELETE /subscription
│   └── Shared/
│       ├── Scattered.Shared.Api      DefaultScatteredWebApplication: arranque común
│       └── Scattered.Shared.Setup    GlobalUsings.cs enlazado en cada proyecto
├── docs/flow/                Guía del flujo paso a paso
├── assets/                   Imágenes de este README
├── docker-compose.yaml       Solo RabbitMQ (aún sin uso)
└── Scattered.sln             Solución (.NET 6)
```

* **Api**: las futuras API Gateways. Hoy solo responden `Hello World!`; el enrutado con YARP llega en el capítulo 2.
* **Services**: los microservicios. Cada controlador devuelve datos fijos (`TODO`), sin base de datos.
* **Shared**: código común. `DefaultScatteredWebApplication` deja cada `Program.cs` en tres líneas:

```csharp
WebApplication app = DefaultScatteredWebApplication.Create();
app.MapGet("/", () => "Hello World!");
DefaultScatteredWebApplication.Run(app);
```

## ¿Cómo ejecutar la aplicación?

En este capítulo no hace falta Docker: los servicios no usan ninguna infraestructura.

```bash
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders
```

Cada servicio expone Swagger en `/swagger`. La lista completa de comandos, puertos y particularidades (.NET 6 sin soporte, el proyecto huérfano de Products…) está en [docs/flow/README.md](docs/flow/README.md#cómo-ejecutarla).

## Cómo evoluciona este README

Al replicar un capítulo nuevo en su rama `features/flowN`, actualiza:

1. La nota inicial y la tabla **Estado de esta rama** (rama, capítulo, commit de origen, qué hay, qué falta y el siguiente capítulo).
2. El **diagrama**: edita `assets/architecture.svg` (pasa a borde continuo y etiqueta amarilla lo que ya existe) y regenera `assets/diagram.png`:
   ```bash
   "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new \
     --hide-scrollbars --force-device-scale-factor=2 --window-size=1400,1090 \
     --screenshot=assets/diagram.png "file://$PWD/assets/architecture.svg"
   ```
3. La casilla del capítulo en **Progreso del curso**.
4. El árbol de **Descripción del repositorio** y **¿Cómo ejecutar la aplicación?** (a partir del capítulo 3 hace falta `docker-compose`).
5. [docs/flow/README.md](docs/flow/README.md) con el detalle del capítulo.

## Dale una estrella ⭐
Si te gusta el proyecto no dudes en darle una estrella al [proyecto original](https://github.com/ElectNewt/Distribt), hacer un fork junto a una PR o incluso apoyar económicamente al autor [donando un café](https://www.buymeacoffee.com/netmentor).
