# Scattered

![Scattered](assets/Scattered.svg)

> **Nota:** Scattered es una copia renombrada de [Distribt](https://github.com/ElectNewt/Distribt), proyecto creado por Ivan Abad ([NetMentor](https://www.netmentor.es)) y publicado bajo licencia MIT. El curso y los artículos enlazados en este documento pertenecen al autor original.
>
> **Esta rama (`features/flow2`)** replica la rama [`02-patron-api-gateway`](https://github.com/ElectNewt/Distribt/tree/02-patron-api-gateway) del proyecto original (commit `fbd27a2`), renombrada de Distribt a Scattered. Cada capítulo del curso se replica en su propia rama `features/flowN`; la guía de cada uno, con qué cambió y cómo ejecutarlo, está en **[docs/flow/README.md](docs/flow/README.md)**.

## Estado de esta rama

| | |
|---|---|
| Rama | `features/flow2` (nace de `features/flow1`) |
| Capítulo del curso | 2 · [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) |
| Rama original | [`02-patron-api-gateway`](https://github.com/ElectNewt/Distribt/tree/02-patron-api-gateway) (`fbd27a2`) |
| Qué hay | 2 API Gateways con YARP que enrutan hacia Orders y Products, y 4 microservicios *Web API* con datos fijos en puertos fijos |
| Qué falta | Comunicación entre microservicios, mensajería, persistencia e infraestructura |
| Guía del capítulo | [docs/flow/flow2.md](docs/flow/flow2.md) |
| Siguiente capítulo | 3–4 · [Producers/Consumers](https://www.netmentor.es/entrada/patron-productor-consumidor) con RabbitMQ (`03-04-Producers-consumers-RabbitMQ`) |

## Qué cambió en este capítulo

Respecto a `features/flow1` ([capítulo 1](docs/flow/flow1.md)), las APIs pública y privada dejan de ser un `Hello World!` y pasan a ser **API Gateways**: el cliente ya no necesita conocer el puerto de cada microservicio, solo el del gateway.

| Cambio | Dónde | Para qué |
|---|---|---|
| Paquete `Yarp.ReverseProxy` | `.csproj` de los dos gateways | Reverse proxy de Microsoft |
| `AddReverseProxy().LoadFromConfig(...)` y `MapReverseProxy()` | `Program.cs` de los dos gateways | Usa por primera vez el punto de extensión de `DefaultScatteredWebApplication.Create` |
| Sección `ReverseProxy` (Routes + Clusters) | `appsettings.json` de los dos gateways | Público: `/order-ms/*` → Orders, `/product-ms/*` → Products. Privado: `/reports/*` → Orders |
| Puertos fijos `50X20` HTTPS / `50X10` HTTP | `launchSettings.json` de los 4 servicios | Los clusters necesitan direcciones estables (Emails 501x0, Orders 502x0, Products 503x0, Subscriptions 504x0) |
| `AddRouting(LowercaseUrls = true)` | `DefaultScatteredWebApplication` | URLs generadas en minúsculas (Swagger: `/order` en lugar de `/Order`) |

```text
Antes (capítulo 1)                      Ahora (capítulo 2)
cliente ──▶ :7077 Orders                cliente ──▶ :7283 /order-ms/order/{id}
cliente ──▶ :7185 Products                           └─ YARP ──▶ :50220 Orders /order/{id}
cliente ──▶ :7283 "Hello World!"        cliente ──▶ :7283 /product-ms/product/{id}
                                                     └─ YARP ──▶ :50320 Products /product/{id}
                                        tenant  ──▶ :7022 /reports/order/{id}
                                                     └─ YARP ──▶ :50220 Orders /order/{id}
```

Los controllers no cambian: el gateway solo cambia el camino. Subscriptions y Emails no se exponen (la suscripción llegará por mensajería en el capítulo 3–4). Única diferencia con el original: YARP 1.1.2 en lugar de 1.0.0, que tiene dos vulnerabilidades conocidas. Detalle de cada cambio, flujo end to end y casos límite en **[docs/flow/flow2.md](docs/flow/flow2.md)**.

## ¿Qué es Scattered?

Scattered es un proyecto Open Source creado para mostrar el funcionamiento y uso de los sistemas distribuidos con las implementaciones en .NET Core.

La aplicación va a ser "vendor free" lo que quiere decir que no va a estar enlazada directamente a ningún proveedor específico. Obviamente vamos a utilizar X o Y servicio (ya que no vamos a reinventar la rueda), pero lo haremos a través de abstracciones.

Es importante saber estos conceptos ya que cada día las empresas están migrando sus aplicaciones monolíticas a microservicios o incluso serverless, y para poder aplicar un correcto funcionamiento, debemos aprender sobre sistemas distribuidos.

## ¿Qué vamos a ver?

Vamos a ver un sistema distribuido con múltiples características, como pueden ser patrón consumer/publisher, sagas, service discovery, Eventual consistency, etc.

Esta es la arquitectura planteada en el capítulo 1, marcada con lo que ya existe en esta rama (`features/flow2`):

![ScatteredDiagram](assets/diagram.png)

- Las cajas con borde continuo existen en el código; las de borde discontinuo son el objetivo y todavía no existen.
- Las flechas continuas son conexiones que ya funcionan (las rutas de los gateways); las discontinuas, conexiones planificadas.
- Las etiquetas amarillas representan el código ya implementado; las grises indican en qué capítulo llega cada pieza.
- La fuente editable del diagrama es [`assets/architecture.svg`](assets/architecture.svg).

Puedes encontrar el proceso de creación del sistema en [el curso de YouTube del autor original](https://www.youtube.com/playlist?list=PLesmOrW3mp4jpSbdFMtVWINJZ7OLdSASS).

Alternativamente, si lo prefieres, puedes seguir los post con el código detallado y los razonamientos sobre por qué se ha elegido cada tecnología en la web del autor original, [NetMentor - Curso Distribt](https://www.netmentor.es/curso/sistemas-distribuidos).

## Progreso del curso

Cada capítulo se replica en su propia rama `features/flowN`. Marcado = ya replicado en Scattered.

- [x] 1 · [API Rest](https://www.netmentor.es/entrada/api-rest-csharp): esqueleto de microservicios creados con .NET · [guía](docs/flow/flow1.md)
- [x] 2 · [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) con YARP · [guía](docs/flow/flow2.md) **← esta rama**
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
│   │   ├── Public/Scattered.API.Public       Gateway YARP :7283  /order-ms/* · /product-ms/*
│   │   └── Private/Scattered.API.Private     Gateway YARP :7022  /reports/*
│   ├── Services/
│   │   ├── Emails/Scattered.Services.Emails                :50120  POST /email
│   │   ├── Orders/Scattered.Services.Orders                :50220  GET /order/{id} · POST /order
│   │   ├── Products/Scattered.Services.Products            :50320  GET /product/{id} · POST /product
│   │   └── Subscriptions/Scattered.Services.Subscriptions  :50420  POST · DELETE /subscription
│   └── Shared/
│       ├── Scattered.Shared.Api      DefaultScatteredWebApplication: arranque común
│       └── Scattered.Shared.Setup    GlobalUsings.cs enlazado en cada proyecto
├── docs/flow/                Guía del flujo paso a paso, un archivo por capítulo
├── assets/                   Imágenes de este README
├── docker-compose.yaml       Solo RabbitMQ (aún sin uso)
└── Scattered.sln             Solución (.NET 6)
```

* **Api**: los API Gateways. Reenvían las peticiones a los microservicios con YARP; las rutas están en su `appsettings.json`.
* **Services**: los microservicios. Cada controlador devuelve datos fijos (`TODO`), sin base de datos.
* **Shared**: código común. `DefaultScatteredWebApplication` centraliza el arranque, y su punto de extensión permite que cada proyecto añada lo suyo. Así queda el `Program.cs` de un gateway:

```csharp
WebApplication app = DefaultScatteredWebApplication.Create(webappBuilder =>
{
    webappBuilder.Services.AddReverseProxy()
        .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));
});

app.MapGet("/", () => "Hello World!");
app.MapReverseProxy();

DefaultScatteredWebApplication.Run(app);
```

## ¿Cómo ejecutar la aplicación?

En este capítulo no hace falta Docker: los servicios no usan ninguna infraestructura.

```bash
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders
curl -k https://localhost:7283/order-ms/order/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

Cada servicio expone Swagger en `/swagger`. La lista completa de comandos, el flujo end to end a través del gateway y las particularidades (.NET 6 sin soporte, el proyecto huérfano de Products, la versión de YARP…) están en [docs/flow/flow2.md](docs/flow/flow2.md#cómo-ejecutarla).

## Cómo evoluciona este README

Al replicar un capítulo nuevo en su rama `features/flowN`, actualiza:

1. La nota inicial, la tabla **Estado de esta rama** y la sección **Qué cambió en este capítulo** (se reescribe en cada capítulo; la de los anteriores queda en su guía de `docs/flow`).
2. El **diagrama**: edita `assets/architecture.svg` (pasa a borde continuo y etiqueta amarilla lo que ya existe) y regenera `assets/diagram.png`:
   ```bash
   "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new \
     --hide-scrollbars --force-device-scale-factor=2 --window-size=1400,1090 \
     --screenshot=assets/diagram.png "file://$PWD/assets/architecture.svg"
   ```
3. La casilla del capítulo en **Progreso del curso**.
4. El árbol de **Descripción del repositorio** y **¿Cómo ejecutar la aplicación?** (a partir del capítulo 3 hace falta `docker-compose`).
5. Crea `docs/flow/flowN.md` con el detalle del capítulo y enlázalo desde [docs/flow/README.md](docs/flow/README.md).

## Dale una estrella ⭐
Si te gusta el proyecto no dudes en darle una estrella al [proyecto original](https://github.com/ElectNewt/Distribt), hacer un fork junto a una PR o incluso apoyar económicamente al autor [donando un café](https://www.buymeacoffee.com/netmentor).
