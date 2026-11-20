# Capítulo 13 · Health checks (`features/flow12`)

Un servicio puede estar arrancado y escuchando en su puerto, y aun así no poder hacer su trabajo porque su base de datos o RabbitMQ no responden. Un **health check** es un endpoint que lo dice: comprueba las dependencias del servicio y devuelve `Healthy`, `Degraded` o `Unhealthy`. Los orquestadores (Kubernetes, Azure Container Apps), los balanceadores y los sistemas de monitorización lo consultan para decidir si mandarle tráfico, reiniciarlo o avisar. En este capítulo todos los servicios exponen `/health` con el estado de cada dependencia y una pequeña web en `/health-ui`.

<!-- nav:start -->
[← Capítulo 12](flow11.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 14 →](flow13.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · **13** · [14](flow13.md) · [15](flow14.md) · [16](flow15.md) · [17](flow16.md)
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow12`)](#flujo-end-to-end-featuresflow12) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`13-Health-Checks`](https://github.com/ElectNewt/Distribt/tree/13-Health-Checks) |
| Commits de origen | `95e1739` health check with json resopnse · `4016113` add UI for health checks · `b3c0734` #30 default basic ui information (7-jun-2022) · `e206a9a` add health checks information (8-jun-2022) · `c46a5bb` #31 fix the kafka health check (9-jun-2022) |
| Diff respecto al capítulo 12 | 5 commits, 24 archivos, +188 / −56 líneas |
| Artículo | [Health checks en ASP.NET Core](https://www.netmentor.es/entrada/health-checks-asp-net) |
| Cambios propios | `MongoDB.Driver` 2.19.0 y el `ObjectSerializer` en `MongoDbDependencyInjection.cs`. **Se retira** el usuario de RabbitMQ que Scattered añadía a `definitions.json` desde el capítulo 3–4, porque el `rabbitmq.conf` de este capítulo ya lo crea (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow11 -- src tools docker-compose.yaml`.

## Resumen

```text
GET https://localhost:50220/health   (Orders)
{
  "status": "Healthy",                     ← peor estado de todas las comprobaciones
  "totalDuration": "00:00:00.0148159",
  "entries": {
    "DomainPublisher":     { "status": "Healthy" },   ← RabbitMQ (AddServiceBusDomainPublisher)
    "mongodb":             { "status": "Healthy" },   ← event store
    "productStore":        { "status": "Healthy" },   ← colección ProductName
    "ProductsHealthCheck": { "status": "Healthy" }    ← GET /health de Products.Api.Read (comprobación propia)
  }
}

https://localhost:50220/health-ui   → web de AspNetCore.HealthChecks.UI que consulta /health periódicamente
```

| Servicio | Comprobaciones |
|---|---|
| API Public | `IntegrationPublisher` (RabbitMQ) |
| API Private, Emails | ninguna (`Healthy` si el proceso responde) |
| Orders | `DomainPublisher`, `mongodb`, `productStore`, `ProductsHealthCheck` |
| Orders.Consumer | `productStore`, `DomainConsumer`, `IntegrationConsumer` |
| Products.Api.Read | `mongodb` |
| Products.Api.Write | `mysql`, `DomainPublisher` |
| Products.Consumer | `mongodb`, `IntegrationPublisher`, `DomainConsumer` |
| Subscriptions | `IntegrationPublisher` (pero `/health` da 500, ver [problemas](#problemas-heredados-del-original)) |
| Subscriptions.Consumer | `IntegrationConsumer` |

Las comprobaciones **se registran solas**: cada vez que un servicio llama a `AddServiceBus…`, `AddMySql` o `AddScatteredMongoDbConnectionProvider`, se añade la comprobación de esa dependencia.

## Qué cambió

```text
docker-compose.yaml                          rabbitmq: imagen 3.9 → 3.8, sin variables de entorno, monta rabbitmq.conf
tools/rabbitmq/rabbitmq.config               ELIMINADO (formato Erlang)
tools/rabbitmq/rabbitmq.conf                 NUEVO (formato ini): usuario por defecto + management.load_definitions
tools/rabbitmq/definitions.json              se corrige la coma sobrante (JSON válido por fin)
src/
├── Shared/
│   ├── Scattered.Shared.Setup
│   │   ├── API/DefaultScatteredWebApplication.cs   AddHealthChecks + HealthChecksUI (memoria); /health (JSON) y /health-ui
│   │   ├── API/HealthCheckHelper.cs         NUEVO: configura la UI para consultar "self" en /health
│   │   ├── Databases/MongoDb.cs · MySql.cs  registran la comprobación de su base
│   │   └── Services/ServiceBus.cs           cada conexión de RabbitMQ con nombre: IntegrationPublisher, DomainConsumer…
│   ├── Shared.Communication/…RabbitMQ       AddRabbitMQ(…, name) + comprobación AspNetCore.HealthChecks.Rabbitmq
│   ├── Shared.Databases/…MongoDb            AddMongoHealthCheck(name) + GetMongoConnectionString()
│   └── Shared.Databases/…MySql              AddMysqlHealthCheck()
└── Services/Orders/
    ├── …BusinessLogic/HealthChecks/ProductsHealthCheck.cs   NUEVO: comprobación propia (Products.Api.Read /health)
    ├── …BusinessLogic                       la copia de nombres registra su MongoDB como "productStore"
    └── Scattered.Services.Orders/Program.cs + AddCheck<ProductsHealthCheck>
```

### 1. `/health` y `/health-ui` en el arranque común

```csharp
// Create()
builder.Configuration.AddConfiguration(HealthCheckHelper.BuildBasicHealthCheck());   // UI → "self" en /health
builder.Services.AddHealthChecks();
builder.Services.AddHealthChecksUI().AddInMemoryStorage();

// Run()
webApp.MapHealthChecks("/health");
webApp.UseHealthChecks("/health", new HealthCheckOptions()
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse      // JSON con cada entrada
});
webApp.UseHealthChecksUI(config => config.UIPath = "/health-ui");
```

- La respuesta es **200** si todo está `Healthy` (o `Degraded`) y **503** si algo está `Unhealthy`, que es lo que miran los balanceadores.
- `UIResponseWriter` cambia el texto plano por defecto (`Healthy`) por un JSON con cada comprobación, su duración y, si falla, la descripción o la excepción.
- `/health-ui` es [AspNetCore.HealthChecks.UI](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks): una web que consulta `/health` periódicamente (aquí solo a sí misma, `self`) y guarda el historial en memoria.

### 2. Comprobaciones de las dependencias

| Dependencia | Paquete | Cómo comprueba |
|---|---|---|
| RabbitMQ | `AspNetCore.HealthChecks.Rabbitmq` 6.0.2 | Una conexión AMQP con las credenciales y el host del servicio (Vault + Consul) |
| MongoDB | `AspNetCore.HealthChecks.MongoDb` 6.0.2 | Lista bases de datos con la cadena de conexión |
| MySQL | `AspNetCore.HealthChecks.MySql` 6.0.2 | Abre una conexión y ejecuta una consulta simple |
| Products (desde Orders) | propia (`ProductsHealthCheck`) | `GET {ProductsApiRead}/health` (dirección de Consul) |

```csharp
public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
{
    HttpClient client = _httpClientFactory.CreateClient();
    client.BaseAddress = new Uri(await _discovery.GetFullAddress(DiscoveryServices.Microservices.ProductsApi.ApiRead, ct));
    HttpResponseMessage responseMessage = await client.GetAsync("health", ct);
    return responseMessage.IsSuccessStatusCode
        ? HealthCheckResult.Healthy("Product service is healthy")
        : HealthCheckResult.Degraded("Product service is down");     // Orders puede seguir funcionando con su copia
}
```

Que Products caído sea **`Degraded`** y no `Unhealthy` es una decisión de diseño: Orders tiene su propia copia de los nombres (capítulo 11), así que puede seguir respondiendo.

Para registrar cada comprobación con su cadena de conexión, `AddMongoHealthCheck` y `AddMysqlHealthCheck` construyen un contenedor temporal (`BuildServiceProvider()`) y piden la cadena a Consul y Vault **en el arranque**.

### 3. RabbitMQ 3.8 con `rabbitmq.conf`

```ini
# tools/rabbitmq/rabbitmq.conf  (sustituye a rabbitmq.config, en formato Erlang)
loopback_users.guest = false
default_vhost = /
default_user = ScatteredAdmin
default_pass = ScatteredPass
management.load_definitions = /etc/rabbitmq/definitions.json
```

- El `docker-compose.yaml` baja la imagen de `rabbitmq:3.9-management-alpine` a **`3.8-management-alpine`** y quita las variables `RABBITMQ_DEFAULT_USER/PASS`: el usuario sale de `rabbitmq.conf`.
- Con `management.load_definitions`, RabbitMQ **crea el usuario por defecto antes** de aplicar las definiciones (*"Will seed default virtual host and user"*). Es lo que fallaba con el `rabbitmq.config` del capítulo 3–4, donde las definiciones impedían crear el usuario.
- El commit `c46a5bb` (*#31 fix the kafka health check*, aunque no hay Kafka) corrige la coma sobrante de `definitions.json` que el original arrastraba desde el capítulo 8.

## Lo que no cambia

- **El comportamiento de negocio**: suscripciones, CQRS de Products, Event Sourcing y nombres de producto en Orders funcionan igual (comprobado en el paso 9 del flujo).
- **Los problemas de ROP** del capítulo 11 y la fuga de conexiones.

## Cómo ejecutarla

```bash
docker compose up -d rabbitmq            # recrea el contenedor con la imagen 3.8 y rabbitmq.conf
./tools/local-development/up.sh
dotnet build Scattered.sln
# Arranca los servicios como en los capítulos 8–11
curl -k https://localhost:50220/health   # Orders: JSON con sus 4 comprobaciones
open https://localhost:50220/health-ui   # la web de health checks
```

Al cambiar de imagen, `rabbitmq` se recrea **vacío**: se pierden los mensajes pendientes y los enlaces o usuarios añadidos fuera de `definitions.json` (los usuarios `token-…` de Vault, enlaces antiguos de capítulos anteriores). Las colas y enlaces del capítulo se vuelven a cargar desde `definitions.json`.

| Servicio | `/health` | `/health-ui` |
|---|---|---|
| Orders | https://localhost:50220/health | https://localhost:50220/health-ui |
| Products.Api.Write / Read / Consumer | `:50320` · `:50321` · `:50322` | ídem |
| API Public | https://localhost:7283/health | ídem |

## Flujo end to end (`features/flow12`)

> **Rama:** `features/flow12`, equivalente a [`13-Health-Checks`](https://github.com/ElectNewt/Distribt/tree/13-Health-Checks) del original (`c46a5bb`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-20 con los 8 contenedores (RabbitMQ recreado en 3.8.34) y los 10 procesos arrancados.

| # | Acción | Respuesta | Qué se ve |
|---|---|---|---|
| 1 | `docker compose up -d rabbitmq` | `Recreate` → RabbitMQ **3.8.34** | `Created user 'ScatteredAdmin'` desde `rabbitmq.conf`; 10 colas y todos los enlaces de `definitions.json`, sin el enlace antiguo del capítulo 7 |
| 2 | `GET /health` en los 10 servicios | 9 × **`200 Healthy`** · Subscriptions **`500`** | Cada servicio con sus comprobaciones (tabla del [resumen](#resumen)). Subscriptions: `please specify the VAULT-TOKEN env_var` |
| 3 | Parar MySQL + `GET :50320/health` | **`503 Unhealthy`** | `mysql=Unhealthy (Unable to connect to any of the specified MySQL hosts.)`, `DomainPublisher=Healthy` |
| 4 | Parar Products.Api.Read + `GET :50220/health` | **`503 Unhealthy`** | `ProductsHealthCheck=Unhealthy (Connection refused (localhost:50321))`: la excepción gana a la intención de devolver `Degraded` |
| 5 | Parar RabbitMQ + `/health` de API Public y Orders | **`503`** | `IntegrationPublisher` / `DomainPublisher` = `Unhealthy (Already closed: The AMQP operation was interrupted…)` |
| 6 | Arrancar RabbitMQ | `200 Healthy` | Las comprobaciones se recuperan solas |
| 7 | `GET :50220/health-ui` | `200` | La UI ve `self` con 4 entradas (`/healthchecks-api`) |
| 8 | Respuesta de `/health` | JSON de `UIResponseWriter` | Aunque también se llama a `MapHealthChecks("/health")`, responde el middleware `UseHealthChecks`, que va antes en la canalización |
| 9 | Regresión: suscripción, crear producto 14, leerlo, crear y consultar pedido | `200` · `201` · `200` · `201` · `200` | Todo igual que en el capítulo 12 |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| Cada servicio declara **sus** dependencias | Paso 2 | El estado global dice *qué* falla, no solo que algo falla |
| 503 cuando algo está `Unhealthy` | Pasos 3–5 | Un balanceador o un orquestador puede dejar de mandarle tráfico |
| `Degraded` para dependencias opcionales… en teoría | Paso 4 | La comprobación de Products quería degradar, pero una excepción la marca `Unhealthy` |
| Un `/health` que lanza excepción no informa | Paso 2 | Subscriptions da 500 con traza en vez de un `Unhealthy` con el motivo |
| RabbitMQ 3.8 + `rabbitmq.conf` crea el usuario | Paso 1 | Ya no hace falta el arreglo de Scattered del capítulo 3–4 |

## Problemas heredados del original

### 1. `/health` de Subscriptions da 500

La comprobación de RabbitMQ necesita las credenciales de Vault y Subscriptions sigue sin `VAULT-TOKEN` (desde el capítulo 8). La excepción no se convierte en `Unhealthy`: el endpoint responde 500 con la traza, y la UI no puede mostrar nada útil.

### 2. `ProductsHealthCheck` nunca devuelve `Degraded` cuando Products está caído

Solo devuelve `Degraded` si Products responde con un código de error. Si no responde (conexión rechazada, *timeout*), `GetAsync` lanza una excepción y el servicio de health checks la convierte en `Unhealthy` (el `failureStatus` por defecto). Orders aparece como no disponible aunque podría funcionar con su copia de nombres.

### 3. Las comprobaciones se preparan en el arranque

- `AddMongoHealthCheck` y `AddMysqlHealthCheck` hacen `BuildServiceProvider()` y piden la cadena a Consul y Vault en `Create()`: más dependencias de arranque.
- Las cadenas de conexión quedan fijadas: si Vault rota la contraseña, la comprobación sigue usando la antigua.

### 4. Otros detalles

- **`/health` registrado dos veces** (`MapHealthChecks` y `UseHealthChecks`); solo se usa el segundo.
- **Sin separar *liveness* y *readiness***: un único `/health` mezcla "el proceso vive" con "sus dependencias responden"; un orquestador reiniciaría el servicio por un fallo de MySQL.
- **La UI solo se vigila a sí misma** (`self`): no hay un panel que reúna la salud de todos los servicios.
- **RabbitMQ baja a 3.8**, una versión sin soporte; la 3.9 también lo está hoy (*"This release series has reached end of life"*).
- **`using Microsoft.AspNetCore.JsonPatch.Adapters`** sin uso en `RabbitMQDependencyInjection.cs`, y el mensaje del commit `c46a5bb` habla de Kafka, que el proyecto no usa.
- Siguen los problemas de los capítulos anteriores.

## Particularidades

- **Se retira el usuario de `definitions.json` (cambio propio que se deshace).** Desde el [capítulo 3–4](flow3.md#particularidades), Scattered declaraba `ScatteredAdmin` y sus permisos en `definitions.json` porque RabbitMQ no creaba el usuario por defecto al cargar definiciones. El `rabbitmq.conf` de este capítulo lo resuelve (`Created user 'ScatteredAdmin'` en el arranque), así que `definitions.json` vuelve a ser idéntico al del original (`"users": []`, `"permissions": []`). Comprobado recreando el contenedor: usuario, permisos `.*` y las 10 colas.
- **`MongoDB.Driver` 2.19.0 y `ObjectSerializer`** se mantienen en `Shared.Databases.MongoDb` (ver capítulos [8](flow7.md#particularidades) y [9](flow8.md#particularidades)); el `.csproj` y `MongoDbDependencyInjection.cs` se portan a mano desde el original con esos dos cambios.
- **RabbitMQ recreado.** El cambio de imagen recrea el contenedor; en esta ejecución eso eliminó también los usuarios `token-…` de capítulos anteriores y el enlace que el [capítulo 11](flow10.md#particularidades) borró a mano.
- Siguen vigentes las particularidades de los capítulos anteriores.
