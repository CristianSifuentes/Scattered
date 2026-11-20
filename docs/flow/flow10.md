# Capítulo 11 · Respuestas de API unificadas con ROP (`features/flow10`)

Hasta ahora cada endpoint respondía a su manera: un `Guid` suelto, un `true`, un objeto, un `CreatedResult` sin cuerpo o una traza de excepción. En este capítulo las APIs adoptan un **formato común** para todas las respuestas, `ResultDto<T>` (`value`, `errors`, `success`), y la lógica pasa a encadenarse con **Railway Oriented Programming** (ROP): cada paso devuelve un `Result<T>` que va por la vía del éxito o por la del error, y el controller lo convierte en la respuesta HTTP adecuada. La rama trae además lo que dejó pendiente la consistencia eventual: **Orders conoce los nombres de los productos** gracias a los mensajes de integración de Products, y un producto creado ya llega a la vista de lectura.

<!-- nav:start -->
[← Capítulo 10](flow9.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 12 →](flow11.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · **11** · [12](flow11.md) · [13](flow12.md) · [14](flow13.md) · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow10`)](#flujo-end-to-end-featuresflow10) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`11-unificar-respuestas-api`](https://github.com/ElectNewt/Distribt/tree/11-unificar-respuestas-api) |
| Commits de origen | `5fcfd78` add eventual consistency · `f3c6d98` #25 add unit test for the orders microservice (1-may-2022) · `3354b67` Respuestas consistentes de la API · `32f8502` #24 include graylog in consul · `461eeb5` #20 move away from the code the database connection · `1f86a05` add create product handler (13/14-may-2022) · `92de34f` bugfix: cache not being replaced (19-may-2022) |
| Diff respecto al capítulo 10 | 7 commits, 48 archivos, +852 / −177 líneas |
| Artículos | [Unificar respuestas de API](https://www.netmentor.es/entrada/unificar-respuestas-api) · [Railway Oriented Programming](https://www.netmentor.es/entrada/railway-oriented-programming) |
| Cambios propios | `tools/consul/config.sh` (Graylog y Products por HTTPS, con la API HTTP), `ObjectSerializer` trasladado a `Shared.Databases`, `MongoDB.Driver` 2.19.0 y `up.sh` en su ruta correcta (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow9 -- src tools docker-compose.yaml Scattered.sln LICENSE`.

## Resumen

```text
                      Formato común de respuesta (Netmentor.ROP.ApiExtensions)
   200/201/202:  {"value": {...},  "errors": [],                                   "success": true}
   404:          {"value": null,   "errors": [{"message":"Order … not found"}],     "success": false}

                      Consistencia eventual entre Products y Orders
Products.Api.Write ──ProductCreated/ProductUpdated──▶ product-domain-queue ──▶ Products.Consumer
                                                                                 ├─▶ MongoDB Products (vista de lectura)
                                                                                 └─▶ ProductUpdated (integración, "external")
                                                    products.exchange ──▶ order.exchange ──▶ order-queue
                                                                                 ▼
                                                     Orders.Consumer · ProductModifierHandler
                                                       └─▶ MongoDB ProductName (copia local de los nombres) + caché en memoria
Orders (GET / create) ──nombre de producto──▶ caché → ProductName → (si no está) HTTP a Products.Api.Read
```

Tres ideas en una rama:

1. **Respuestas unificadas.** `Result<T>` en la lógica y `ResultDto<T>` en la API, con el código HTTP decidido por el resultado (404 si no existe, 201 al crear…).
2. **Cada servicio con su copia de los datos ajenos.** Orders no consulta a Products en cada petición: guarda los nombres que le llegan por mensajes (`ProductName`) y solo llama por HTTP si no los tiene. Es consistencia eventual entre servicios.
3. **Infraestructura fuera del código.** La dirección de Graylog sale de Consul, y la conexión a MongoDB (base de datos incluida) de la configuración y de `Shared.Databases`.

## Qué cambió

```text
LICENSE                                      + cláusula: prohibido usarlo en software que dañe a personas
Scattered.sln                                + Orders.BusinessLogic, Tests.Services.Orders.BusinessLogicTests
tools/consul/config.sh                       + Graylog (localhost:12201); ProductsApiWrite/Read pasan a https
tools/local-development/up.sh                sleep 10 → 15
tools/mongodb/mongo-init.js                  + colección ProductName
tools/rabbitmq/definitions.json              + order-domain-queue; order.exchange: "order" → order-domain-queue, "external" → order-queue;
                                               products.exchange → order.exchange ("external")
src/
├── Shared/
│   ├── Scattered.Shared.Setup               + Netmentor.ROP.ApiExtensions 1.0.10; GlobalUsings: ROP, ROP.APIExtensions
│   │   ├── API/DefaultScatteredWebApplication.cs   ConfigureSerilog(discovery): Graylog desde Consul
│   │   ├── Databases/MongoDb.cs             solo delega en Shared.Databases (+ configuración)
│   │   └── Extensions/LinqExtensions.cs     NUEVO: SelectAsync con concurrencia limitada
│   ├── Shared.Databases/…MongoDb            + MongoDbConnectionProvider (movido desde Setup), DatabaseConfiguration,
│   │                                          MongoEventStoreConfiguration (antes EventStoreOptions)
│   ├── Scattered.Shared.Logging             ConfigureSerilog recibe IServiceDiscovery y pide "Graylog"
│   ├── Scattered.Shared.EventSourcing       usa MongoEventStoreConfiguration; TODOs #27 y #29
│   └── Scattered.Shared.Discovery           + DiscoveryServices.Graylog
├── Services/Orders/
│   ├── Scattered.Services.Orders.BusinessLogic   NUEVO: ProductNameService (caché → Mongo → HTTP), ProductRepository
│   ├── Scattered.Services.Orders            Result<T> en Create/Get; controller con ResultDto y ProducesResponseType;
│   │                                        nombres reales de producto; Database:MongoDb en appsettings
│   ├── Scattered.Services.Orders.Consumer   + ProductModifierHandler (integración); consume order-domain-queue y order-queue
│   └── Scattered.Services.Orders.Dto        + CreateOrderResponse(OrderId, Location)
├── Services/Products/
│   ├── …Api.Read                            − ProductController comentado; Database:MongoDb
│   ├── …Api.Write                           respuestas con ResultDto (201 con la URL de lectura, 200 al actualizar)
│   ├── …BusinessLogic/ProductsReadStore     upsert que crea si no existe; UpdateProductStock / UpdateProductPrice; Price como double
│   └── …Consumer                            + ProductCreatedHandler: ¡la creación ya llega a la vista de lectura!
├── Services/Subscriptions/…Controllers      respuestas con ResultDto
└── Tests/Services/Orders/…BusinessLogicTests   NUEVO: 4 tests de ProductNameService con Moq
```

### 1. Respuestas unificadas con ROP

El paquete [`Netmentor.ROP.ApiExtensions`](https://www.nuget.org/packages/Netmentor.ROP.ApiExtensions) (del propio autor) aporta `Result<T>`, los combinadores (`Bind`, `Then`, `Map`, `Async`…) y la conversión a respuesta HTTP:

```csharp
[HttpGet("{orderId}")]
[ProducesResponseType(typeof(ResultDto<OrderResponse>), (int)HttpStatusCode.OK)]
[ProducesResponseType(typeof(ResultDto<OrderResponse>), (int)HttpStatusCode.NotFound)]
public async Task<IActionResult> GetOrder(Guid orderId)
    => await _getOrderService.Execute(orderId)
        .UseSuccessHttpStatusCode(HttpStatusCode.OK)
        .ToActionResult();
```

```csharp
// GetOrderService: el "no existe" deja de ser una excepción y pasa a ser un resultado
OrderDetails? orderDetails = await _orderRepository.GetById(orderId, cancellationToken);
if (orderDetails == null)
    return Result.NotFound<OrderDetails>($"Order {orderId} not found");      // → 404 con el error en "errors"
```

```csharp
// CreateOrderService: el camino feliz como una cadena de pasos
return await CreateOrder(createOrder)
    .Async()
    .Bind(x => SaveOrder(x, cancellationToken))                               // guardar eventos
    .Then(x => MapToOrderResponse(x, cancellationToken)                       // nombres de producto
        .Bind(or => PublishDomainEvent(or, cancellationToken)))               // publicar
    .Map(x => new CreateOrderResponse(x.Id, $"order/getorderstatus/{x.Id}")); // → 201
```

| Endpoint | Antes | Ahora |
|---|---|---|
| `POST /order/create` | `202` + `"guid"` | `201` + `{"value":{"orderId","location"},…}` |
| `GET /order/{id}` | `200` con el pedido · `500` si no existe | `200` con `ResultDto` · **`404`** con el error si no existe |
| `PUT /order/markaspaid` | `200` vacío | `202` con `ResultDto` (ver problemas) |
| `POST /product` (Write) | `201` sin cuerpo, `Location` en cabecera | `201` + `{"value":{"url":…}}` |
| `PUT /product/updateproductdetails/{id}` | `200` vacío | `200` + `{"value":true}` |
| `POST /subscription` | `200` `true` | `ResultDto<bool>` |

`ProducesResponseType` documenta en Swagger el formato y los códigos posibles.

### 2. Consistencia eventual entre Products y Orders

Orders necesita el nombre de cada producto para mostrar un pedido. En vez de preguntar a Products cada vez, mantiene su propia copia:

| Pieza | Dónde | Qué hace |
|---|---|---|
| `ProductCreatedHandler` | Products.Consumer | **Nuevo.** Crea el documento en la vista de lectura (detalles, stock, precio) y publica `ProductUpdated` de integración |
| `ProductUpdatedHandler` | Products.Consumer | Actualiza la vista (ahora crea el documento si no existe) y publica `ProductUpdated` de integración |
| Enlace `products.exchange` → `order.exchange` (`external`) | RabbitMQ | Los mensajes de integración de Products llegan también a `order-queue` |
| `ProductModifierHandler` | Orders.Consumer | Consume `order-queue` y guarda el nombre en `ProductName` + caché |
| `ProductNameService` | Orders.BusinessLogic | Al leer: caché en memoria → colección `ProductName` → si no está, `GET` a `ProductsApiRead` (dirección de Consul, ahora `https://localhost:50321`) y lo guarda |

Orders.Consumer pasa a tener **dos consumidores**: el de dominio (`order-domain-queue`, sus propios pedidos) y el de integración (`order-queue`, eventos de otros servicios). Por eso `order.exchange` separa las claves `order` y `external`.

### 3. Graylog y MongoDB fuera del código

- **Graylog desde Consul.** `ConfigureSerilog` recibe `IServiceDiscovery` y pide `Graylog` al arrancar; `Logging:Graylog:Host/Port` desaparecen de Orders. Se llama en `DefaultScatteredWebApplication`, así que **todos** los servicios lo piden, aunque no envíen logs.
- **MongoDB.** `MongoDbConnectionProvider` se mueve de `Setup` a `Shared.Databases.MongoDb` (cerrando el `#20`), y el nombre de la base sale de `Database:MongoDb:DatabaseName` en lugar de estar escrito en el código.

### 4. Tests unitarios

`Scattered.Tests.Services.Orders.BusinessLogicTests` prueba `ProductNameService` con Moq (caché, base de datos, llamada HTTP simulada con un `HttpMessageHandler` falso). Son tests unitarios de verdad: no arrancan la API ni necesitan Consul. **Pasan los 4** en este equipo.

### 5. Otros

- **`LICENSE`**: el original añade al MIT una cláusula que prohíbe usar el software en sistemas que hagan daño a personas (*"gambling, cryptocurrencies, guns…"*). Se replica tal cual.
- **`SelectAsync`** (`Setup/Extensions/LinqExtensions.cs`): `Select` asíncrono con un semáforo, para pedir varios nombres de producto en paralelo.
- **`92de34f` bugfix: cache not being replaced**: `SetProductName` borra la clave de la caché antes de escribirla.
- **`ProductsReadStore.Price`** pasa de `decimal` a `double` (el capítulo 8 lo guardaba como texto).

## Lo que no cambia

- **Infraestructura**: no hay contenedores nuevos.
- **Event Sourcing de Orders**, Vault, la fuga de conexiones y el receptor que no hace *nack*.
- **La API de lectura de Products** sigue siendo una *minimal API* sin `ResultDto` (*"TODO: result struct gives an error on minimal api?"*) y devuelve 500 para un id inexistente.

## Cómo ejecutarla

```bash
./tools/local-development/up.sh          # incluye el registro de Graylog en Consul
docker restart rabbitmq                  # si ya estaba corriendo: carga order-domain-queue y los enlaces nuevos
dotnet build Scattered.sln
dotnet test src/Tests/Services/Orders/Scattered.Tests.Services.Orders.BusinessLogicTests
# Arrancar Products (Write, Read, Consumer), Orders, Orders.Consumer y API Public (ver capítulos 8 y 9)
```

```bash
# Crear un producto → 201 con ResultDto; aparece en la lectura y en ProductName de Orders
curl -k -X POST https://localhost:50320/product -H 'Content-Type: application/json' \
  -d '{"details":{"name":"Monitor 27","description":"IPS 144Hz"},"stock":7,"price":219.99}'
# Crear un pedido con ese producto → 201; consultarlo → nombres reales
curl -k -X POST https://localhost:7283/order-ms/order/create -H 'Content-Type: application/json' -d '{
  "deliveryDetails":{"street":"Calle Mayor 1","city":"Madrid","country":"España"},
  "paymentInformation":{"cardNumber":"4111111111111111","expireDate":"12/28","security":"123"},
  "products":[{"productId":1,"quantity":2},{"productId":12,"quantity":1}]}'
curl -k https://localhost:7283/order-ms/order/<orderId>
curl -k https://localhost:7283/order-ms/order/00000000-0000-0000-0000-000000000001   # 404 con ResultDto
```

Si RabbitMQ conserva enlaces de capítulos anteriores, elimina `order.exchange → order-queue` con clave `order` (ver [Particularidades](#particularidades)).

## Flujo end to end (`features/flow10`)

> **Rama:** `features/flow10`, equivalente a [`11-unificar-respuestas-api`](https://github.com/ElectNewt/Distribt/tree/11-unificar-respuestas-api) del original (`92de34f`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-20 con los 8 contenedores y los 10 procesos arrancados.

### Qué ocurre en cada parte

| # | Acción | Respuesta | Qué pasó por dentro |
|---|---|---|---|
| 1 | `dotnet test` de Orders.BusinessLogicTests | **4/4 correctos** | Tests unitarios de `ProductNameService` con Moq |
| 2 | `POST :50320/product` (Monitor 27) | **`201`** `{"value":{"url":"https://localhost:50321/product/12"},"errors":[],"success":true}` | MySQL → `ProductCreated` → Products.Consumer: documento con `Stock: 7`, `Price: 219.99` → `ProductUpdated` de integración → Orders.Consumer: `ProductName {Id:12, Name:"Monitor 27"}` |
| 3 | `GET :50321/product/12` | `200` | **La creación por fin converge** a la vista de lectura (en el capítulo 8 daba 500) |
| 4 | `PUT …/updateproductdetails/2` | `200` `{"value":true,…}` | La vista se actualiza, pero **`Stock` 3 → 0 y `Price` 120 → 0**: el upsert los pone a cero. El nombre llega a `ProductName` de Orders unos segundos después |
| 5 | Mensaje envenenado del producto 10 (capítulo 8) | — | Al arrancar el consumer nuevo, el upsert crea el documento y el mensaje por fin se confirma, con `Stock: 0, Price: 0` |
| 6 | `GET :50321/product/999` | **`500`** `NullReferenceException` | La API de lectura sigue sin `ResultDto` |
| 7 | `POST /order-ms/order/create` con productos 1, 2 y 12 | **`201`** `{"value":{"orderId":"f2bfe0ee-…","location":"order/getorderstatus/…"}}` | Event store + nombres + `Order f2bfe0ee-… created` en Orders.Consumer |
| 8 | `GET /order-ms/order/{id}` | `200` con `"name":"Producto 1"`, `"Segundo producto (cap. 11)"`, `"Monitor 27"` | Nombres reales, ya no `"fakeNAme"` |
| 9 | `PUT markaspaid` | **`202`** con `"value":{"result":true,"id":1,"status":5,"isCompleted":true,…}` | Se serializa el `Task<bool>` en vez de `true` |
| 10 | `PUT markasdispatched` | `200` **vacío** | El método devuelve `Task`: el `ResultDto` se descarta |
| 11 | `GET` de un pedido inexistente | **`404`** `{"value":null,"errors":[{"message":"Order … not found"}],"success":false}` | El objetivo del capítulo |
| 12 | `PUT markaspaid` de un pedido inexistente | **`500`** `NullReferenceException` | `OrderPaidService` no usa `Result` |
| 13 | `POST create` con el producto 11 (creado en el cap. 8, nunca llegó a la vista) | **`201`** | El pedido se guarda, pero **no se publica** (Orders.Consumer no lo recibe) y `GET` de ese pedido da **`500`** (`HttpRequestException`: la API de lectura responde 500 al pedir el nombre) |
| 14 | `POST :50420/subscription` | `500` `please specify the VAULT-TOKEN env_var` | Sigue sin token en su `launchSettings` (heredado del cap. 8) |
| 15 | Graylog | 1 error recibido en `localhost:12201` | Dirección obtenida de Consul |
| 16 | Dar de baja `Graylog` en Consul + arrancar Emails | **Emails muere al arrancar**: `seems like the service your are trying to access (Graylog) does not exist` | Todos los servicios piden Graylog a Consul, aunque no envíen logs |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| El formato `ResultDto` y el **404** con mensaje | Pasos 2, 7 y 11 | El cliente siempre sabe dónde está el dato (`value`) y dónde los errores (`errors`) |
| La conversión a ROP es **parcial** | Pasos 6, 9, 10, 12 y 14 | Lectura de Products, pagar/enviar y Subscriptions siguen devolviendo 500 o respuestas raras |
| Un producto creado **ya converge** a la lectura | Pasos 2–3 | Lo que faltaba en el capítulo 8 |
| Orders tiene su **propia copia** de los nombres | Pasos 2, 4 y 8 | Llega por mensajes; Orders no depende de Products para responder (salvo si no tiene el nombre) |
| Actualizar un producto **pone su stock y precio a cero** | Paso 4 | El upsert de la vista sobrescribe `Stock` y `Price` con 0 |
| Un dato perdido antes no se recupera | Paso 13 | El producto 11 nunca entró en la vista; un pedido que lo use queda guardado pero sin publicar y no se puede consultar |
| `201` aunque falle la publicación | Paso 13 | El fallo dentro del `.Then(...)` no llega a la respuesta |
| Graylog es otra dependencia de arranque | Paso 16 | Igual que Vault y Consul en el capítulo 6 |

## Problemas heredados del original

### 1. ROP a medias

- `markaspaid` devuelve `_orderPaidService.Execute(...).Success().Async()`: `.Success()` se aplica al `Task<bool>`, no a su resultado, y el cliente recibe el `Task` serializado (`isCompleted`, `asyncState`…).
- `markasdispatched` y `markasdelivered` siguen declarados como `async Task`: construyen el `IActionResult` y lo descartan, así que responden 200 sin cuerpo. `markasdelivered` sigue llamando al servicio de *dispatched*.
- `OrderPaidService`, `OrderDispatchedService` y `OrderDeliveredService` no usan `Result`: un id inexistente sigue dando 500.
- Los `ProducesResponseType` de pagar/enviar anuncian `202 ResultDto<bool>`, que no es lo que devuelven.
- La API de lectura de Products no usa `ResultDto` (*"TODO: result struct gives an error on minimal api?"*) y sigue con el 500 para ids inexistentes.

### 2. Crear un pedido puede quedar a medias

`CreateOrderService` guarda los eventos y, después, en un `.Then(...)`, busca los nombres y publica el mensaje de dominio. Si la búsqueda del nombre falla (paso 13), el pedido queda guardado, no se publica y la respuesta es igualmente **201**. Además, `GetProduct` lanza `ArgumentException` (*"TODO: replace exception"*) en vez de devolver un `Result`.

### 3. El upsert de la vista de Products borra stock y precio

`UpsertProductViewDetails` asigna `Stock = 0` y `Price = 0` siempre (*"//default"*). Para `ProductCreatedHandler` no importa, porque luego llama a `UpdateProductStock`/`UpdateProductPrice`, pero `ProductUpdatedHandler` solo llama al upsert: **cualquier cambio de nombre o descripción deja el producto sin stock y a precio 0** en la vista de lectura.

### 4. Otros detalles

- **Graylog obligatorio al arrancar**: `ConfigureSerilog` llama a `GetDiscoveryData(Graylog).Result` en todos los servicios, incluso con `Logging:Graylog:Enabled` desactivado o ausente. Y otro `BuildServiceProvider()` en `DefaultScatteredWebApplication`.
- **`AddDistributedMemoryCache`** como "caché distribuida": cada proceso tiene la suya (*"in prod I recommend redis"*).
- **`ProductsApiRead` pasa a `https`** en Consul; Orders confía en el certificado de desarrollo de ASP.NET Core.
- **`UpdateProductStock` / `UpdateProductPrice`** hacen `FirstOrDefault` sin comprobar `null`.
- **`product-queue`** sigue acumulando mensajes de integración que nadie consume (19 en esta ejecución); los de Orders ahora llegan por `order.exchange`.
- **`config.sh` del original** registra Graylog con `--address` (doble guion).
- **TODOs nuevos**: `#26` (abstraer las llamadas HTTP entre servicios), `#27` (¿debe `GetByIdAsync` devolver `null`?), `#29` (uso de `IMongoDatabase`).
- Siguen los problemas de capítulos anteriores (`VAULT-TOKEN` en Subscriptions, fuga de conexiones, mensajes envenenados).

## Particularidades

- **`tools/consul/config.sh` (cambio propio).** Se añaden los mismos registros que el original (`Graylog → localhost:12201`, `ProductsApiWrite`/`Read` con `https`), con la API HTTP como desde el [capítulo 6](flow5.md#particularidades).
- **`ObjectSerializer` en `Shared.Databases` (cambio propio).** El original mueve la configuración de MongoDB de `Setup` a `Shared.Databases.MongoDb`; el registro del `ObjectSerializer` que Scattered necesita desde el [capítulo 9](flow8.md#particularidades) se mueve con ella, igual que en el `main` de Scattered.
- **`MongoDB.Driver` 2.19.0** en `Shared.Databases.MongoDb.csproj`, que el original deja en 2.15.0 (ver [capítulo 8](flow7.md#particularidades)).
- **`up.sh`**: el cambio del original (`sleep 15`) va en `tools/local-development/up.sh`; el original sigue usando la carpeta con errata.
- **Enlace antiguo en RabbitMQ.** RabbitMQ aplica `definitions.json` sumando, sin borrar lo que ya existía. El broker conservaba `order.exchange → order-queue` con clave `order` del capítulo 7, con el que cada pedido habría llegado también a la cola de integración de Orders. Se eliminó con la API de gestión (`DELETE /api/bindings/%2F/e/order.exchange/q/order-queue/order`) para que el broker coincida con el `definitions.json` del capítulo. Con un contenedor nuevo no ocurre.
- **Datos heredados.** El producto 11 (creado por el gateway en el capítulo 8) no está en la vista de lectura y ya no llegará; el 10 se sincronizó en este capítulo con stock y precio a 0.
- Siguen vigentes las particularidades de los capítulos anteriores.
