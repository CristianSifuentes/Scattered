# Capítulo 8 · CQRS en Products (`features/flow7`)

**CQRS** (*Command Query Responsibility Segregation*) separa las operaciones que **cambian** datos (comandos) de las que **los leen** (consultas), y les da a cada una su propio modelo, su propia API y, aquí, su propia base de datos. En este capítulo, Products se parte en tres procesos: una **API de escritura** que guarda en **MySQL**, una **API de lectura** que consulta **MongoDB** y un **consumer** que mantiene MongoDB al día a partir de los mensajes de dominio que publica la escritura. La rama trae además dos mejoras previas: los handlers de RabbitMQ se registran solos con inyección de dependencias (Scrutor) y aparecen los primeros tests de API.

<!-- nav:start -->
[← Capítulo 7](flow6.md) · [Índice del flujo](README.md) · Siguiente: capítulo 9 (pendiente)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · **8** · 9 · 10 · 11 · 12 · 13 · 14 · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow7`)](#flujo-end-to-end-featuresflow7) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`08-CQRS`](https://github.com/ElectNewt/Distribt/tree/08-CQRS) |
| Commits de origen | `13993e8` #8 Improve handlers in rabbitMQ · `e52f188` add issue with the api tests (5-mar-2022) · `53d17af` add cqrs en products (19-mar-2022) |
| Diff respecto al capítulo 7 | 3 commits, 68 archivos, +1095 / −100 líneas |
| Artículo | [CQRS explicado en 10 minutos](https://www.netmentor.es/entrada/patron-cqrs-explicado-10-minutos) |
| Cambios propios | `MongoDB.Driver` 2.15.0 → 2.19.0 (vulnerabilidad), referencia a `DependencyInjection.Abstractions` como paquete en vez de una DLL de Windows, y `tools/consul/config.sh` adaptado (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow6 -- src tools docker-compose.yaml Scattered.sln`.

## Resumen

```text
                       COMANDOS (escritura)                                  CONSULTAS (lectura)
Cliente ──POST /product──▶ Products.Api.Write (:50320)              Cliente ──GET /product/{id}──▶ Products.Api.Read (:50321)
        ──PUT  /product/updateproductdetails/{id}──▶ │                                                       │
                                                     ▼                                                       ▼
                                          MySQL :3307 (tabla Products)                           MongoDB :27017 (colección Products)
                                          Id · Name · Description                                Id · Details · Stock · Price
                                                     │                                                       ▲
                                    DomainMessage ProductCreated / ProductUpdated                            │ upsert
                                    products.exchange ──"internal"──▶ product-domain-queue ──▶ Products.Consumer (:50322)
                                                                                                             │ IntegrationMessage ProductUpdated
                                                       products.exchange ──"external"──▶ product-queue ◀─────┘ (nadie la consume aún)
```

- **Dos modelos distintos.** MySQL guarda lo mínimo para escribir (`Id`, `Name`, `Description`); MongoDB guarda la vista lista para leer (`Details`, `Stock`, `Price`), sin *joins*.
- **Consistencia eventual.** La escritura responde en cuanto MySQL confirma; MongoDB se actualiza un instante después, cuando el consumer procesa el mensaje. Si el consumer está parado, la lectura muestra datos antiguos.
- **Las direcciones y credenciales** de MySQL y MongoDB salen de Consul y Vault, como las de RabbitMQ.

## Qué cambió

```text
docker-compose.yaml                          + MongoDb (mongo:latest :27017) + MySql (mysql:8.0 :3307); graylog sin restart: always
tools/mysql/init.sql                         NUEVO: tabla Products + 3 productos
tools/mongodb/mongo-init.js                  NUEVO: usuario, colección Products + los mismos 3 productos con Stock y Price
tools/vault/config.sh                        + secretos KV secret/mongodb y secret/mysql
tools/consul/config.sh                       + MySql, MongoDb, ProductsApiWrite (:50320), ProductsApiRead (:50321)
tools/rabbitmq/definitions.json              + products.exchange → product-domain-queue ("internal") y product-queue ("external")
Scattered.sln                                + 7 proyectos (Products ×5, Shared.Databases ×2) + 1 de tests
src/
├── Services/Products/
│   ├── Scattered.Services.Products          ELIMINADO: pasa a ser Products.Api.Write
│   ├── Scattered.Services.Products.Api.Write   :50320  POST /product (201) · PUT /product/updateproductdetails/{id}
│   ├── Scattered.Services.Products.Api.Read    :50321  GET /product/{id} (minimal API sobre MongoDB)
│   ├── Scattered.Services.Products.Consumer    :50322  product-domain-queue → MongoDB + mensaje de integración
│   ├── Scattered.Services.Products.BusinessLogic        casos de uso + ProductsWriteStore (EF Core) + ProductsReadStore (Mongo)
│   └── Scattered.Services.Products.Dtos     (antes .Dto) CreateProductRequest, ProductDetails, FullProductResponse, ProductUpdated, ProductCreated
├── Shared/
│   ├── Shared.Databases/…MySql              NUEVO: AddMySqlDbContext<T>() con EF Core + MySql.EntityFrameworkCore
│   ├── Shared.Databases/…MongoDb            NUEVO: IMongoDbConnectionProvider
│   ├── Scattered.Shared.Setup
│   │   ├── Databases/MySql.cs · MongoDb.cs  NUEVO: cadenas de conexión con Consul (host:puerto) + Vault (usuario)
│   │   ├── Services/ServiceBus.cs           AddHandlers(lista) → AddHandlersInAssembly<T>() con Scrutor; el gateway pasa a credenciales KV
│   │   ├── API/DefaultScatteredWebApplication.cs   Create(args, …)
│   │   └── GlobalUsings.cs                  + usings de ASP.NET Core, DI, logging y Setup.Databases
│   └── Scattered.Shared.Discovery           + GetDiscoveryData() → DiscoveryData(Server, Port); caché de DiscoveryData
├── Services/Subscriptions/
│   ├── …Subscriptions                       POST /subscription publica de verdad; public partial class Program (para tests)
│   └── …Subscriptions.Consumer              handlers por ensamblado + IDependenciaTest de ejemplo
├── Services/Orders/…                        DTOs usan FullProductResponse; consumer con AddHandlersInAssembly
├── Api/*, Services/Emails                   Create(args, …)
└── Tests/Services/Subscriptions/…ApiTests   NUEVO: test de API con WebApplicationFactory y un publisher falso
```

### 1. Infraestructura: MySQL y MongoDB

| Servicio (contenedor) | Imagen | Puerto | Usuario | Inicialización |
|---|---|---|---|---|
| `mysql` (`MySql`) | `mysql:8.0` | **`3307`** → 3306 | `scatteredUser` / `scatteredPassword` (root: `scatteredRootPassword`) | `tools/mysql/init.sql`: tabla `Products(Id, Name, Description)` + 3 filas |
| `mongodb` (`MongoDb`) | `mongo:latest` | `27017` | `scatteredUser` / `scatteredPassword` (root y usuario de `scattered`) | `tools/mongodb/mongo-init.js`: colección `Products` + 3 documentos con `Stock` y `Price` |

- El puerto de MySQL es `3307` porque, según el comentario del original, el autor tenía el 3306 ocupado.
- Los scripts de `docker-entrypoint-initdb.d` **solo se ejecutan la primera vez**, con el volumen de datos vacío. Si el contenedor ya existía, se conservan sus datos.
- `tools/vault/config.sh` guarda las credenciales en `secret/mysql` y `secret/mongodb`; `tools/consul/config.sh` registra `MySql → localhost:3307` y `MongoDb → localhost:27017`. **Ningún servicio tiene la cadena de conexión en `appsettings.json`.**

### 2. `Shared.Databases` y `Setup/Databases`: conexiones que se construyen solas

```csharp
// Setup/Databases/MySql.cs
public static IServiceCollection AddMySql<T>(this IServiceCollection serviceCollection, string databaseName)
    where T : DbContext
    => serviceCollection.AddMySqlDbContext<T>(serviceProvider => GetConnectionString(serviceProvider, databaseName));

private static async Task<string> GetConnectionString(IServiceProvider serviceProvider, string databaseName)
{
    DiscoveryData mysqlData = await serviceDiscovery.GetDiscoveryData(DiscoveryServices.MySql);      // Consul → localhost:3307
    MySqlCredentials credentials = await secretManager.Get<MySqlCredentials>("mysql");               // Vault  → usuario/contraseña
    return $"Server={mysqlData.Server};Port={mysqlData.Port};Database={databaseName};Uid=…;password=…;";
}
```

`MongoDbConnectionProvider` hace lo mismo para MongoDB (`mongodb://usuario:contraseña@localhost:27017`) y guarda el `MongoUrl` la primera vez. Para ello `IServiceDiscovery` estrena `GetDiscoveryData()`, que devuelve servidor y puerto por separado (`DiscoveryData`) en lugar de la cadena `host:puerto`.

`AddMySqlDbContext` resuelve la cadena **cada vez que se crea el `DbContext`**, es decir, en cada petición (con `.Result`). Gracias a la caché de `ConsulServiceDiscovery` solo la primera llega a Consul, pero el secreto se pide a Vault en todas.

### 3. Products en tres procesos

**`Products.Api.Write`** (`:50320`, antes `Scattered.Services.Products`) — los comandos:

| Endpoint | Caso de uso | Qué hace |
|---|---|---|
| `POST /product` | `CreateProductDetails` | Inserta en MySQL, llama a `IStockApi` e `IWarehouseApi` (falsos: devuelven `true`), publica `ProductCreated` con clave `internal` y responde **201** con `Location` = URL de la API de lectura, que pide a Consul (`ProductsApiRead`) |
| `PUT /product/updateproductdetails/{id}` | `UpdateProductDetails` | Actualiza nombre y descripción en MySQL y publica `ProductUpdated` con clave `internal` |

`ProductsWriteStore` es a la vez el `DbContext` de EF Core y el repositorio. El comentario de `IStockApi`/`IWarehouseApi` adelanta el problema que resolverá el capítulo 12: *"we will see sagas in the future"*.

**`Products.Api.Read`** (`:50321`) — las consultas. Una *minimal API* con un único endpoint:

```csharp
app.MapGet("product/{productId}", async (int productId, IProductsReadStore readStore)
    => await readStore.GetFullProduct(productId));
```

Lee el documento de MongoDB por `Id` y lo devuelve como `FullProductResponse(Id, Details, Stock, Price)`. El `ProductController` equivalente está en el proyecto, pero comentado entero.

**`Products.Consumer`** (`:50322`) — el que une los dos lados. Consume `product-domain-queue` con `ProductUpdatedHandler`:

```csharp
public async Task Handle(DomainMessage<ProductUpdated> message, CancellationToken cancelToken = default)
{
    await _readStore.UpsertProductViewDetails(message.Content.ProductId, message.Content.Details, cancelToken);   // MongoDB
    await _integrationMessagePublisher.Publish(new ProductUpdated(…), routingKey: "external", …);                // otros servicios
}
```

Primero actualiza la vista de lectura y después avisa al resto del sistema con un mensaje de **integración** (`product-queue`), que todavía no consume nadie. Es el patrón completo: **dominio** dentro de Products, **integración** hacia fuera.

| | Modelo de escritura (MySQL) | Modelo de lectura (MongoDB) |
|---|---|---|
| Proceso | `Products.Api.Write` | `Products.Api.Read` |
| Forma | Tabla normalizada: `Id`, `Name`, `Description` | Documento listo para devolver: `Id`, `Details{Name, Description}`, `Stock`, `Price` |
| Acceso | EF Core (`ProductsWriteStore : DbContext`) | `MongoDB.Driver` (`ProductsReadStore`) |
| Se actualiza | Directamente, en la petición | Por `Products.Consumer`, a partir de `ProductUpdated` |

### 4. Handlers registrados con inyección de dependencias (#8)

Hasta ahora los handlers se creaban a mano (`new SubscriptionHandler()`), así que no podían recibir dependencias. `AddHandlersInAssembly<T>()` usa **Scrutor** para registrar automáticamente todas las clases de ese ensamblado que implementan `IMessageHandler`:

```csharp
serviceCollection.Scan(scan => scan.FromAssemblyOf<T>()
    .AddClasses(classes => classes.AssignableTo<IMessageHandler>())
    .AsImplementedInterfaces()
    .WithTransientLifetime());

ServiceProvider sp = serviceCollection.BuildServiceProvider();      // contenedor temporal
var listHandlers = sp.GetServices<IMessageHandler>();               // instancias creadas aquí, una sola vez
serviceCollection.AddConsumerHandlers(listHandlers);
```

Así `ProductUpdatedHandler` recibe `IProductsReadStore` e `IIntegrationMessagePublisher`, y `SubscriptionHandler` recibe una `IDependenciaTest` de ejemplo (su mensaje pasa a ser *"… successfully subscribed. y la dependencia es 1"*). Los handlers se registran como *transient*, pero en la práctica se construyen **una sola vez**, en un contenedor temporal, y se reutilizan para todos los mensajes.

### 5. Primer test de API

`Scattered.Tests.Services.Subscriptions.ApiTests` levanta la API de Subscriptions en memoria con `WebApplicationFactory<Program>`, sustituye `IIntegrationMessagePublisher` por un `FakeIntegrationPublisher` y comprueba que `POST /subscription` publica el DTO recibido. Para que funcione:

- `Program.cs` de Subscriptions declara `public partial class Program { }`, porque las *top-level statements* generan una clase `Program` interna.
- `DefaultScatteredWebApplication.Create` recibe `args` y llama a `WebApplication.CreateBuilder(args)`: es el *"issue with the api tests"* del segundo commit. Todos los `Program.cs` pasan a `Create(args, …)`.

`SubscriptionController` deja de ser un `return true` y publica de verdad en `subscription.exchange`; el `record SubscriptionDto` duplicado del controller desaparece en favor del de `Subscriptions.Dtos`.

### 6. Otros cambios

- **El gateway deja de usar credenciales dinámicas.** `AddServiceBusIntegrationPublisher` pasa de `GetRabbitMqSecretCredentialsfromRabbitMQEngine` al secreto KV. Vault ya no crea usuarios `token-…` en RabbitMQ; el motor `rabbitmq` sigue configurado pero sin uso.
- **`DiscoveryServices`** suma `MySql` y `MongoDb`, y sustituye `Products` por `ProductsApi.ApiRead` / `ApiWrite`.
- **`OrderDto` y `CreateOrderDto`** usan `List<FullProductResponse>` en vez del antiguo `ProductDto(productId, quantity)`, que desaparece.
- **`GlobalUsings.cs`** suma `Microsoft.AspNetCore.Builder`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, `System.Threading.Tasks` y `Setup.Databases`.
- **`graylog`** pierde `restart: always`.

## Lo que no cambia

- **Vault, Consul y Graylog** funcionan igual; solo suman entradas.
- **Las rutas de YARP.** `/product-ms/*` sigue apuntando a `https://localhost:50320/`, que ahora es la API de **escritura**: por el gateway se puede crear, pero no leer (paso 6).
- **Orders y el flujo de suscripciones por el gateway.**
- **El receptor de RabbitMQ**: confirma el mensaje si el handler termina y no hace nada si falla. Con CQRS esto se nota (pasos 3–5).

## Cómo ejecutarla

```bash
./tools/local-development/up.sh          # contenedores + Vault (incluye secret/mysql y secret/mongodb) + Consul
dotnet build Scattered.sln
P=src/Services/Products
DOTNET_ROLL_FORWARD=Major dotnet run --project $P/Scattered.Services.Products.Api.Write/Scattered.Services.Products.Api.Write.csproj
DOTNET_ROLL_FORWARD=Major dotnet run --project $P/Scattered.Services.Products.Api.Read
DOTNET_ROLL_FORWARD=Major dotnet run --project $P/Scattered.Services.Products.Consumer
```

`Api.Write` necesita la ruta del `.csproj`: la carpeta conserva el `Scattered - Backup.Services.Products.csproj` heredado del capítulo 1. Si RabbitMQ ya estaba corriendo, reinícialo (`docker restart rabbitmq`) para que cree `products.exchange` y sus colas.

```bash
# Leer (MongoDB)
curl -k https://localhost:50321/product/1
# Crear (MySQL) → 201 con Location en la API de lectura
curl -k -X POST https://localhost:50320/product -H 'Content-Type: application/json' \
  -d '{"details":{"name":"Teclado mecánico","description":"Switches rojos"},"stock":5,"price":49.9}'
# Actualizar (MySQL → mensaje → MongoDB). Usa un producto que ya exista en MongoDB (1, 2 o 3)
curl -k -X PUT https://localhost:50320/product/updateproductdetails/2 -H 'Content-Type: application/json' \
  -d '{"name":"Segundo producto (editado)","description":"Actualizado por CQRS"}'
curl -k https://localhost:50321/product/2
```

| Qué | Dónde |
|---|---|
| MySQL | `docker exec -it MySql mysql -uscatteredUser -pscatteredPassword scattered -e 'select * from Products'` |
| MongoDB | `docker exec -it MongoDb mongosh -u scatteredUser -p scatteredPassword --eval 'db.getSiblingDB("scattered").Products.find()'` |
| Colas de Products | http://localhost:15672 → *Queues* → `product-domain-queue`, `product-queue` |
| Test de API | `dotnet test src/Tests/Services/Subscriptions/Scattered.Tests.Services.Subscriptions.ApiTests` (necesita Consul; ver [Particularidades](#particularidades)) |

Para detener todo: `pkill -f 'Scattered\.(API|Services)'` y `docker compose stop`.

## Flujo end to end (`features/flow7`)

> **Rama:** `features/flow7`, equivalente a [`08-CQRS`](https://github.com/ElectNewt/Distribt/tree/08-CQRS) del original (`53d17af`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-19 con 8 contenedores (RabbitMQ, Vault, Consul, Graylog ×3, MySQL y MongoDB) y los **10 procesos** arrancados. `MySql` y `MongoDb` se reutilizaron de `main` (misma configuración), así que ya tenían productos de pruebas anteriores (ids 4 a 9) además de los 3 de la semilla.

### Las llamadas

```bash
# 1. Leer un producto de la semilla
curl -k https://localhost:50321/product/1
# 2–3. Crear un producto y leerlo
curl -k -X POST https://localhost:50320/product -H 'Content-Type: application/json' -d '{"details":{"name":"Teclado mecánico","description":"Switches rojos"},"stock":5,"price":49.9}'
curl -k https://localhost:50321/product/10
# 4–5. Actualizar el producto recién creado y leerlo
curl -k -X PUT https://localhost:50320/product/updateproductdetails/10 -H 'Content-Type: application/json' -d '{"name":"Teclado mecánico v2","description":"Switches marrones"}'
curl -k https://localhost:50321/product/10
# 6. Actualizar un producto que sí está en MongoDB y leerlo
curl -k -X PUT https://localhost:50320/product/updateproductdetails/2 -H 'Content-Type: application/json' -d '{"name":"Segundo producto (editado)","description":"Actualizado por CQRS"}'
curl -k https://localhost:50321/product/2
# 7. Reiniciar Products.Consumer
# 8–9. Products por el gateway; producto inexistente
curl -k https://localhost:7283/product-ms/product/1
curl -k -X POST https://localhost:7283/product-ms/product -H 'Content-Type: application/json' -d '{…}'
curl -k https://localhost:50321/product/999
# 10–12. Suscripción por el gateway, por la API de Subscriptions (sin y con VAULT-TOKEN), y Orders
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
curl -k -X POST https://localhost:50420/subscription -H 'Content-Type: application/json' -d '{"email":"directo@example.com"}'
curl -k -X POST https://localhost:7283/order-ms/order -H 'Content-Type: application/json' -d '{…}'
# 13. Test de API
dotnet test src/Tests/Services/Subscriptions/Scattered.Tests.Services.Subscriptions.ApiTests
```

### Qué ocurre en cada parte

| # | Acción | Respuesta | MySQL (escritura) | RabbitMQ | Products.Consumer | MongoDB (lectura) |
|---|---|---|---|---|---|---|
| 1 | `GET :50321/product/1` | `200` `{"id":1,"details":{…},"stock":32,"price":10}` (1,06 s: primera conexión a Consul, Vault y Mongo) | — | — | — | Lee el documento 1 |
| 2 | `POST :50320/product` | **`201`**, `Location: http://localhost:50321/product/10` | Inserta la fila 10 | `ProductCreated` → `product-domain-queue` | Lo recibe, **no hay handler** para `ProductCreated` → lo confirma y lo **descarta** | Nada |
| 3 | `GET :50321/product/10` | **`500`** `NullReferenceException` | — | — | — | No existe el 10. El `Location` del paso 2 apunta a un recurso que no se puede leer |
| 4 | `PUT …/updateproductdetails/10` | `200` | Actualiza la fila 10 | `ProductUpdated` → `product-domain-queue` | `UpsertProductViewDetails` hace `entity.Id ??= id` sobre un `null` → excepción. **No confirma el mensaje**: queda `unacked` | Nada |
| 5 | `GET :50321/product/10` | **`500`** | Fila 10 con `v2` | 1 mensaje `unacked` | — | Sigue sin el 10 |
| 6 | `PUT …/updateproductdetails/2` + `GET :50321/product/2` | `200` · `200` con los datos nuevos | Actualiza la fila 2 | `ProductUpdated` (`internal`) y después `ProductUpdated` de integración (`external`) → `product-queue` | Upsert correcto y publica la integración | Documento 2 actualizado. `Price` pasa de número a texto (`'120'`) |
| 7 | Reiniciar Products.Consumer | — | — | El mensaje del 10 vuelve a `ready` y se reentrega | **Falla otra vez** → otra vez `unacked`. Nunca llega a `product-domain-queue.dead-letter` | — |
| 8 | `GET :7283/product-ms/product/1` · `POST :7283/product-ms/product` | **`404`** · `201` | Inserta | — | — | El gateway apunta a la API de **escritura**: crear funciona, leer no |
| 9 | `GET :50321/product/999` | **`500`** `NullReferenceException` | — | — | — | Un producto inexistente no da 404 |
| 10 | `POST :7283/subscribe` | `200` | — | Igual que antes, pero **con `ScatteredAdmin`**: no se crean usuarios `token-…` (6 → 6) | — | Subscriptions.Consumer: `… successfully subscribed. y la dependencia es 1` |
| 11 | `POST :50420/subscription` (API de Subscriptions) | **`500`** sin token (`please specify the VAULT-TOKEN env_var`); `200 true` con `VAULT-TOKEN` | — | Con token: llega a `subscription.exchange` **sin clave de enrutado** → ninguna cola lo recibe | — | El consumer no procesa nada |
| 12 | `POST /order-ms/order` + `GET` | `202` · `200` | — | `order-queue` | — | Orders.Consumer: `Order 86f2a60e-… created` |
| 13 | `dotnet test` de Subscriptions | **Falla**: la API responde 500 (`PipeWriter.UnflushedBytes`, ver [Particularidades](#particularidades)). Con Consul parado falla antes: `Connection refused (localhost:8500)` | — | — | — | — |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| Escritura y lectura en **bases distintas** | Pasos 1, 2 y 6 | MySQL tiene `Id/Name/Description`; MongoDB tiene la vista completa con `Stock` y `Price` |
| La sincronización funciona para **productos que ya existen** en la vista | Paso 6 | Es el camino feliz: MySQL → `ProductUpdated` → consumer → MongoDB → mensaje de integración |
| **Un producto nuevo nunca llega a la lectura** | Pasos 2–5 | No hay handler de `ProductCreated`, y el de `ProductUpdated` falla si el documento no existe. La CQRS queda a medias |
| Un handler que falla deja el mensaje **atascado** | Pasos 4, 5 y 7 | Sin *nack* ni reintentos con límite, el mensaje nunca va a la *dead-letter* y vuelve a fallar en cada reinicio |
| La API de lectura devuelve **500** en vez de 404 | Pasos 3 y 9 | `SingleOrDefault` + `ToFullProductResponse()` sobre `null` |
| El gateway sigue con la ruta antigua de Products | Paso 8 | `/product-ms/*` va a escritura; la lectura (`:50321`) no está en YARP |
| El gateway ya no usa credenciales dinámicas | Paso 10 | Vault solo sirve secretos estáticos (RabbitMQ, MySQL, MongoDB) |
| La API de Subscriptions publica **a ninguna parte** | Paso 11 | Sin clave de enrutado, `subscription.exchange` no tiene enlace que coincida; además le falta `VAULT-TOKEN` |
| Los servicios de Products **no escriben nada en consola** | Todos | Ninguno configura `Logging:Console`; el consumer falla sin dejar rastro |

## Problemas heredados del original

### 1. La CQRS no cubre la creación

`CreateProductDetails` publica `ProductCreated`, pero ningún handler lo consume: el receptor lo confirma y se pierde. Y `ProductUpdatedHandler` hace `SingleOrDefault` + `entity.Id ??= id`, que lanza `NullReferenceException` si el documento no existe, en vez de crearlo (el `IsUpsert = true` posterior nunca se alcanza). Un producto creado por la API **no aparece nunca** en la de lectura, aunque se actualice después. El `Location` del 201 apunta a un recurso que responde 500.

### 2. Mensajes envenenados sin salida

`RabbitMQMessageReceiver` solo hace `BasicAck` si el handler termina; si lanza una excepción no hace `BasicNack` ni `BasicReject`. El mensaje queda sin confirmar mientras la conexión siga abierta y vuelve a la cola al cerrarse, para fallar de nuevo. La `product-domain-queue.dead-letter` existe y la política DLX aplica, pero nunca recibe nada. Y como el consumer no escribe logs, el fallo es invisible.

### 3. Lectura frágil y rutas sin actualizar

- `GET /product/{id}` devuelve 500 para un id inexistente.
- `Price` se guarda como texto en MongoDB tras un upsert (`decimal` se serializa como cadena por defecto).
- `/product-ms/*` del gateway sigue apuntando a `:50320` (escritura). La API de lectura no está publicada en ningún gateway.

### 4. Subscriptions a medias

- `SubscriptionController` publica sin clave de enrutado y nadie lo recibe.
- Su `launchSettings.json` no tiene `VAULT-TOKEN`, que ahora necesita.
- `IDependenciaTest` / `DependenciaTest` son código de ejemplo dentro del handler real.

### 5. El test de API no es un test aislado

`WebApplicationFactory` arranca el `Program` completo, que llama a Consul en `Create()`. Sin Consul el test falla con `Connection refused`. Además, el `FakeIntegrationPublisher` se registra en `CreateHost` sobre el `IHostBuilder`, y la clase de test se llama `UnitTest1`.

### 6. Otros detalles

- **`definitions.json` no es JSON válido**: hay una coma sobrante tras `products.exchange`. RabbitMQ 3.9 lo acepta, pero cualquier herramienta estricta lo rechaza. El original lo arrastra hasta el capítulo 13 (`c46a5bb`).
- **Más `BuildServiceProvider()` y `.Result`**: en `AddHandlersInAssembly`, en `AddMySqlDbContext` (en cada petición) y en `MongoDbConnectionProvider`.
- **`AddMongoDbConnectionProvider<T>()`** de `Shared.Databases.MongoDb` lanza `NotImplementedException("#20")`; la implementación real vive en `Setup`.
- **`IStockApi` / `IWarehouseApi`** son interfaces con implementación por defecto y una clase vacía, `ProductsDependencyFakeType`, para el contenedor.
- **`Products.Api.Read`** deja un `ProductController` comentado entero, y **`Products.Api.Write`** hereda el `Scattered - Backup.Services.Products.csproj` del capítulo 1.
- **`product-queue`** acumula mensajes de integración que nadie consume.
- **Vulnerabilidades transitivas**: `MySql.EntityFrameworkCore` 6.0.0 arrastra `System.Drawing.Common` 4.7.0 (crítica) y `Microsoft.Extensions.Caching.Memory` 6.0.1; `MongoDB.Driver` arrastra `SharpCompress` 0.30.1 y `Snappier` 1.0.0. No rompen la compilación (en `net6.0` NuGet solo audita las dependencias directas).
- Siguen los problemas de los capítulos anteriores. La fuga de conexiones crece con un consumer más: más de 460 conexiones en unos minutos.

## Particularidades

- **`MongoDB.Driver` 2.15.0 → 2.19.0 (cambio propio).** La 2.15.0 tiene una vulnerabilidad alta ([GHSA-7j9m-j397-g4wx](https://github.com/advisories/GHSA-7j9m-j397-g4wx), CVE-2022-48282, deserialización de datos no confiables) y, con `TreatWarningsAsErrors`, el aviso `NU1903` rompe la compilación. La 2.19.0 es la primera versión corregida y la que usa el `main` del original. Funciona con el MongoDB 9 de `mongo:latest`.
- **`DependencyInjection.Abstractions` como paquete (cambio propio).** Los dos `.csproj` de `Shared.Databases` referencian la DLL por ruta: `..\..\..\..\..\..\Program Files\dotnet\shared\Microsoft.AspNetCore.App\6.0.3\…`, que solo existe en el Windows del autor. En macOS, `Shared.Databases.MongoDb` no compila (`IServiceCollection` no existe) y `MySql` da el aviso `MSB3245`. Se sustituye por `PackageReference Microsoft.Extensions.DependencyInjection.Abstractions 6.0.0`, que es como lo corrige el propio original en el capítulo 9 (`66a0e62`).
- **`tools/consul/config.sh` adaptado (cambio propio).** El script de Scattered usa la API HTTP desde el [capítulo 6](flow5.md#particularidades); se le añaden los mismos registros que el original (`MySql`, `MongoDb`, `ProductsApiWrite`, `ProductsApiRead`). El original corrige en este commit los puertos cruzados de Orders y Products, que Scattered ya tenía bien. `ProductsApi` queda registrado de capítulos anteriores en un Consul que no se recrea.
- **El test de API no pasa en este equipo.** Solo está instalado el runtime de .NET 10 y los proyectos `net6.0` se ejecutan con `DOTNET_ROLL_FORWARD=Major`. ASP.NET Core 10 escribe el JSON con `PipeWriter.UnflushedBytes`, que el `TestServer` de `Microsoft.AspNetCore.Mvc.Testing` 6.0.2 no implementa, y la API responde 500 (*"The PipeWriter 'ResponseBodyPipeWriter' does not implement PipeWriter.UnflushedBytes"*). Con el runtime de .NET 6 instalado debería pasar. El código del test se mantiene como en el original.
- **`MySql` y `MongoDb` compartidos con `main`.** Tienen la misma configuración (imagen, usuario, base, scripts montados), así que `docker compose up -d` los reutiliza con sus datos. Por eso no se volvieron a ejecutar `init.sql` ni `mongo-init.js`, y había productos de pruebas anteriores. Para partir de la semilla: `docker compose rm -sfv mysql mongodb && docker compose up -d mysql mongodb` (borra sus datos).
- **`definitions.json` inválido pero aceptado.** Se mantiene la coma sobrante del original porque RabbitMQ 3.9 lo carga sin error (8 colas, `products.exchange` y sus dos enlaces).
- Siguen vigentes las particularidades de los capítulos anteriores.
