# Capítulo 9 · Event Sourcing en Orders (`features/flow8`)

Hasta ahora, guardar un dato significaba sobrescribir su estado: el producto 3 *tiene* este nombre. Con **Event Sourcing** no se guarda el estado, sino **la lista de cosas que han pasado**: el pedido se creó, se pagó, se envió. El estado actual se calcula reproduciendo esos eventos en orden, y nunca se borra ni se modifica ninguno. En este capítulo, Orders deja de inventarse los pedidos y pasa a guardarlos así, en una colección de MongoDB que hace de **event store**, con una librería genérica nueva, `Shared.EventSourcing`.

<!-- nav:start -->
[← Capítulo 8](flow7.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 10 →](flow9.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · **9** · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · [13](flow12.md) · [14](flow13.md) · [15](flow14.md) · [16](flow15.md) · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow8`)](#flujo-end-to-end-featuresflow8) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`09-Event-sourcing`](https://github.com/ElectNewt/Distribt/tree/09-Event-sourcing) |
| Commits de origen | `a00be45` update yarp to avoid denaial of service vulnerability (23-abr-2022) · `66a0e62` Add Event Sourcing (25-abr-2022) |
| Diff respecto al capítulo 8 | 2 commits, 40 archivos, +1025 / −49 líneas |
| Artículo | [Event Sourcing explicado fácil](https://www.netmentor.es/entrada/event-sourcing-explicado-facil) |
| Rama del capítulo 10 | [`10-Consistencia-eventual`](https://github.com/ElectNewt/Distribt/tree/10-Consistencia-eventual) apunta **al mismo commit** (`66a0e62`): el artículo de consistencia eventual explica este mismo código |
| Cambios propios | `ObjectSerializer` con tipos permitidos (necesario con `MongoDB.Driver` 2.19); se omite la subida de YARP a 1.0.1 porque Scattered ya usa la 1.1.2 (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow7 -- src tools docker-compose.yaml Scattered.sln`.

## Resumen

```text
Cliente ──POST /order-ms/order/create──▶ Orders (:50220)
                                            │ 1. new OrderDetails(id).Apply(OrderCreated)      ← agregado en memoria
                                            │ 2. OrderRepository.Save → EventStore → MongoDB   ← se guardan los eventos NUEVOS
                                            │ 3. DomainMessage<OrderResponse> → order-queue → Orders.Consumer: "Order {id} created"
                                            ▼
                     MongoDB · scattered.EventsOrders   (una fila por evento, nunca se modifica)
                     ┌───────────┬─────────────────┬──────────────────┬───────────────┐
                     │ Version 1 │ OrderCreated    │ Delivery, Payment, Products       │
                     │ Version 2 │ OrderPaid       │                                    │
                     │ Version 3 │ OrderDispatched │                                    │
                     └───────────┴─────────────────┴──────────────────┴───────────────┘
                                            ▲
Cliente ──GET /order-ms/order/{id}──▶ Orders: lee los eventos del pedido, los aplica en orden → Status = Dispatched
Cliente ──PUT /order-ms/order/markaspaid?orderId=…──▶ Orders: reconstruye, aplica OrderPaid, guarda solo el evento nuevo
```

- **El estado no se guarda en ningún sitio**: `GET` lo reconstruye cada vez reproduciendo los eventos.
- **Nada se sobrescribe**: pagar añade un documento `OrderPaid`; el `OrderCreated` sigue ahí. Se puede saber en qué estado estaba un pedido en cualquier momento.
- **Concurrencia optimista**: cada evento lleva una versión y la combinación `(tipo, id, versión)` es única. Si dos peticiones intentan escribir la misma versión, una falla.

## Qué cambió

```text
Scattered.sln                                + Scattered.Shared.EventSourcing
tools/mongodb/mongo-init.js                  + colección EventsOrders
src/
├── Shared/
│   ├── Scattered.Shared.EventSourcing       NUEVO: Aggregate, AggregateChange, AggregateRepository<T>, IApply<T>,
│   │                                               EventStore + MongoEventStoreManager, DynamicExtensions (reflexión)
│   ├── Shared.Databases/…MongoDb            + EventStoreOptions (DatabaseName, CollectionName); DI.Abstractions como paquete
│   └── Scattered.Shared.Setup               + Services/EventSourcing.cs (AddEventSourcing); referencia a EventSourcing
│       └── Databases/MongoDb.cs             (propio) ObjectSerializer con los tipos Scattered.* permitidos
└── Services/Orders/
    ├── Scattered.Services.Orders
    │   ├── Aggregates/OrderDetails.cs       NUEVO: el agregado; Apply(OrderCreated|OrderPaid|OrderDispatched|OrderCompleted)
    │   ├── Aggregates/MongoMapping.cs       NUEVO: BsonClassMap de los eventos
    │   ├── Events/OrderEvents.cs            NUEVO: OrderCreated, OrderPaid, OrderDispatched, OrderCompleted, OrderStatus
    │   ├── Data/OrderRepository.cs          NUEVO: AggregateRepository<OrderDetails>
    │   ├── Services/*.cs                    NUEVO: Create, Get, Paid, Dispatched, Delivered
    │   ├── Controllers/OrderController.cs   POST create · GET {id} · PUT markaspaid / markasdispatched / markasdelivered
    │   ├── Program.cs                       + MongoDB, event sourcing, repositorio y servicios
    │   └── appsettings.json                 + EventSourcing:DatabaseName/CollectionName
    ├── Scattered.Services.Orders.Dto        CreateOrderRequest, DeliveryDetails, PaymentInformation, ProductQuantity, OrderResponse
    │                                        (− OrderDto, CreateOrderDto, OrderAddress, PersonalDetails)
    └── Scattered.Services.Orders.Consumer   el handler pasa a DomainMessage<OrderResponse>
```

### 1. `Shared.EventSourcing`: la librería genérica

| Pieza | Qué hace |
|---|---|
| `Aggregate` | Clase base. Guarda `Id`, `Version` y la lista de cambios. `ApplyChange(evento)` sube la versión y anota el evento como **nuevo**; `LoadFromHistory(eventos)` los vuelve a aplicar marcándolos como **ya guardados** |
| `IApply<TEvento>` | Contrato que implementa el agregado por cada evento: `void Apply(TEvento ev)` cambia el estado y llama a `ApplyChange` |
| `AggregateChange` | Un evento con su metadato: contenido, id, tipo, `TransactionId` (`{id}:{versión}`), versión y si es nuevo |
| `AggregateRepository<T>` | `GetByIdAsync`: lee los eventos, crea el agregado **sin llamar a su constructor** (`FormatterServices.GetUninitializedObject`) y lo reconstruye. `SaveAsync`: guarda solo los eventos nuevos |
| `MongoEventStoreManager` | El event store en MongoDB: un documento `AggregateChangeDto` por evento, índice único `(AggregateType, AggregateId, AggregateVersion)` y control de versión antes de insertar |
| `DynamicExtensions` | `AsDynamic()`: envoltorio por reflexión (copiado de Hexa.Core) para llamar a `Apply(evento)` sin conocer el tipo en tiempo de compilación |

```csharp
// Aggregate.LoadFromHistory: reconstruir el estado
ReadingFromHistory = true;
foreach (var e in history)
    this.AsDynamic()!.Apply(e.Content);      // llama a OrderDetails.Apply(OrderCreated), Apply(OrderPaid)…
ReadingFromHistory = false;
Version = history.Last().Version;
```

```csharp
// MongoEventStoreManager.SaveEvents: concurrencia optimista
var latestAggregateVersion = (última versión guardada de este agregado)?.AggregateVersion;
if (latestAggregateVersion.HasValue && latestAggregateVersion >= expectedVersion)
    throw new DBConcurrencyException("Concurrency exception");
await collection.InsertManyAsync(eventos, new InsertManyOptions { IsOrdered = true });
```

Un documento del event store tal como queda en MongoDB:

```js
{
  Content: { _t: 'OrderCreated',
             Delivery: { Street: 'Calle Mayor 1', City: 'Madrid', Country: 'España' },
             PaymentInformation: { CardNumber: '4111111111111111', ExpireDate: '12/28', Security: '123' },
             Products: [ { ProductId: 1, Quantity: 2 }, { ProductId: 3, Quantity: 1 } ] },
  AggregateId: Binary(…, 3),          // Guid en formato legacy (subtipo 3)
  AggregateType: 'OrderDetails',
  TransactionId: 'bbe077c6-…:1',
  AggregateVersion: 1
}
```

`Content` es de tipo `object`; el campo `_t` (discriminador) le dice al driver qué clase reconstruir al leer. Por eso `MongoMapping.RegisterClasses()` registra los eventos con `BsonClassMap` al arrancar.

### 2. Orders: el agregado y sus eventos

```csharp
public class OrderDetails : Aggregate, IApply<OrderCreated>, IApply<OrderPaid>, IApply<OrderDispatched>, IApply<OrderCompleted>
{
    public DeliveryDetails Delivery { get; private set; }
    public PaymentInformation PaymentInformation { get; private set; }
    public List<ProductQuantity> Products { get; private set; }
    public OrderStatus Status { get; private set; }

    public void Apply(OrderCreated ev)    { Delivery = ev.Delivery; …; Status = OrderStatus.Created;    ApplyChange(ev); }
    public void Apply(OrderPaid ev)       { Status = OrderStatus.Paid;       ApplyChange(ev); }
    public void Apply(OrderDispatched ev) { Status = OrderStatus.Dispatched; ApplyChange(ev); }
    public void Apply(OrderCompleted ev)  { Status = OrderStatus.Completed;  ApplyChange(ev); }
}
```

| Endpoint | Servicio | Qué hace |
|---|---|---|
| `POST /order/create` | `CreateOrderService` | Nuevo agregado + `OrderCreated`, guarda y publica `DomainMessage<OrderResponse>` en `order.exchange`. Responde **202** con `Location: getorderstatus/{id}` |
| `GET /order/{id}` | `GetOrderService` | Reconstruye el agregado y lo devuelve como `OrderResponse` (los nombres de producto son `"fakeNAme"`: *"it will be completed in the next episodes"*) |
| `PUT /order/markaspaid?orderId=` | `OrderPaidService` | Reconstruye, aplica `OrderPaid`, guarda |
| `PUT /order/markasdispatched?orderId=` | `OrderDispatchedService` | Ídem con `OrderDispatched` |
| `PUT /order/markasdelivered?orderId=` | — | Debería usar `OrderDeliveredService` (`OrderCompleted`), pero llama a `_orderDispatchedService` (ver [problemas](#problemas-heredados-del-original)) |
| `GET /order/getorderstatus/{id}` | — | Sigue con `NotImplementedException` |

Los servicios siguen siempre el mismo patrón, que es el corazón de Event Sourcing: **cargar** (reproducir eventos) → **decidir** (aplicar un evento nuevo) → **guardar** (solo lo nuevo).

### 3. DTOs nuevos

`CreateOrderRequest(DeliveryDetails, PaymentInformation, List<ProductQuantity>)` sustituye a `CreateOrderDto`, y `OrderResponse(OrderId, orderStatus, DeliveryDetails, PaymentInformation, List<ProductQuantityName>)` a `OrderDto`. `OrderAddress` y `PersonalDetails` desaparecen. `Orders.Consumer` pasa a manejar `DomainMessage<OrderResponse>`.

### 4. Otros

- **`tools/mongodb/mongo-init.js`** crea la colección `EventsOrders`, en la misma base `scattered` que la vista de Products (*"on normal circumstances … this will be in a separated database"*).
- **`EventStoreOptions`** vive en `Shared.Databases.MongoDb`, con el comentario *"nullables are a pain in the ass for the configuration files"*.
- **`Shared.Databases.MongoDb.csproj`** cambia la referencia a la DLL de Windows por `PackageReference` (el arreglo que Scattered ya había adelantado en el capítulo 8).
- **YARP 1.0.0 → 1.0.1** en los dos gateways del original (vulnerabilidad de denegación de servicio). En Scattered no aplica: ya están en 1.1.2 desde el [capítulo 2](flow2.md#particularidades).

## Lo que no cambia

- **Infraestructura**: no hay contenedores nuevos; el event store vive en el `MongoDb` del capítulo 8.
- **Products (CQRS), suscripciones, Vault, Consul y Graylog.** Orders deja de escribir el `LogError` de ejemplo, así que en esta rama ningún servicio envía logs a Graylog en el flujo normal.
- **Rutas de YARP**: `/order-ms/*` sigue llevando a Orders; lo que cambia son las rutas dentro de Orders (`/order/create` en vez de `POST /order`).

## Cómo ejecutarla

```bash
./tools/local-development/up.sh
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders.Consumer
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
```

```bash
GW=https://localhost:7283/order-ms/order
# Crear → 202 con el id
curl -k -X POST $GW/create -H 'Content-Type: application/json' -d '{
  "deliveryDetails":{"street":"Calle Mayor 1","city":"Madrid","country":"España"},
  "paymentInformation":{"cardNumber":"4111111111111111","expireDate":"12/28","security":"123"},
  "products":[{"productId":1,"quantity":2},{"productId":3,"quantity":1}]}'
ID=…   # el Guid devuelto
curl -k $GW/$ID                                    # orderStatus: Created
curl -k -X PUT "$GW/markaspaid?orderId=$ID"        # → Paid
curl -k -X PUT "$GW/markasdispatched?orderId=$ID"  # → Dispatched
curl -k $GW/$ID
```

Para ver los eventos de un pedido, filtra por `TransactionId`, que lleva el id en texto. `AggregateId` se guarda como `Guid` *legacy* (subtipo 3) y `UUID('…')` no lo encuentra:

```bash
docker exec MongoDb mongosh --quiet -u scatteredUser -p scatteredPassword --eval \
  "db.getSiblingDB('scattered').EventsOrders.find({TransactionId: /^$ID/}).sort({AggregateVersion:1})"
```

## Flujo end to end (`features/flow8`)

> **Rama:** `features/flow8`, equivalente a [`09-Event-sourcing`](https://github.com/ElectNewt/Distribt/tree/09-Event-sourcing) del original (`66a0e62`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-19 con los 8 contenedores y los 10 procesos arrancados. `EventsOrders` ya existía en el `MongoDb` reutilizado de `main`, con 6 eventos de pruebas anteriores que no interfieren (cada pedido tiene su id).

### Qué ocurre en cada parte

| # | Acción | Respuesta | Orders | Event store (`EventsOrders`) | RabbitMQ / Orders.Consumer |
|---|---|---|---|---|---|
| 1 | `POST /order-ms/order/create` (sin el arreglo del `ObjectSerializer`) | **`500`** | `BsonSerializationException: Type …OrderCreated is not configured as an allowed type for this instance of ObjectSerializer` | Nada | Nada (falla antes de publicar) |
| 2 | Mismo `POST` con el arreglo | **`202`** `"bbe077c6-…"` (2,5 s: primera conexión a Consul, Vault y MongoDB) | Crea el agregado y aplica `OrderCreated` | **v1** `OrderCreated` con dirección, pago y productos | `Order bbe077c6-… created` |
| 3 | `GET /order-ms/order/{id}` | `200` `orderStatus: "Created"`, productos con `name: "fakeNAme"` | Reproduce 1 evento | — | — |
| 4 | `PUT markaspaid` + `GET` | `200` · `"Paid"` | Reconstruye (v1), aplica `OrderPaid` | **v2** `OrderPaid` | — |
| 5 | `PUT markasdispatched` | `200` | Reconstruye (v1–v2), aplica `OrderDispatched` | **v3** `OrderDispatched` | — |
| 6 | `PUT markasdelivered` + `GET` | `200` · **`"Dispatched"`** | El controller llama al servicio de *dispatched* | **v4** `OrderDispatched` (otra vez), no `OrderCompleted` | — |
| 7 | `PUT markaspaid` sobre el pedido ya enviado + `GET` | `200` · **`"Paid"`** | No valida transiciones: el pedido "vuelve atrás" | **v5** `OrderPaid` | — |
| 8 | `GET` y `PUT markaspaid` de un id inexistente | **`500`** `NullReferenceException` · **`500`** | `GetById` devuelve `null` y se usa sin comprobar | Nada | — |
| 9 | 6 × `PUT markaspaid` en paralelo | 6 × `200` | Las peticiones no llegan a solaparse | v6 a v11 | — |
| 10 | Reiniciar Orders + `GET` | `200` · `"Paid"` | Sin estado en memoria: lo reconstruye de los 11 eventos | — | — |
| 11 | 30 × `PUT markaspaid` en paralelo (`xargs -P 30`) | **10 × `200`**, **20 × `500`** | 15 × `DBConcurrencyException`, 5 × `E11000 duplicate key` | Sube de v11 a v21: **ninguna versión repetida** | — |
| 12 | `POST /order-ms/order` (ruta antigua) · `GET getorderstatus/{id}` | `404` · `500` | La creación ahora es `/create`; el estado sigue sin implementar | — | — |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| El estado se **reconstruye** cada vez | Pasos 3, 4, 7 y 10 | No hay tabla de pedidos: `GET` reproduce los eventos. Tras reiniciar, el pedido sigue ahí |
| Los eventos **nunca se modifican** | Pasos 4–7 | Cada cambio añade un documento; se puede reconstruir la historia completa y auditarla |
| La **concurrencia optimista** funciona en dos niveles | Pasos 9 y 11 | Primero la comprobación de versión (`DBConcurrencyException`); si dos peticiones la pasan a la vez, el índice único (`E11000`) frena a la segunda |
| El agregado **no tiene reglas de negocio** | Pasos 6 y 7 | Se puede pagar un pedido ya enviado; "entregar" registra un segundo envío |
| La tarjeta y el CVV se guardan **en claro**, para siempre | Paso 2 | En un event store no se borra nada: un dato sensible guardado una vez queda en todos los históricos |
| `MongoDB.Driver` 2.19 exige **lista blanca** de tipos | Paso 1 | La versión parcheada no serializa objetos arbitrarios en campos `object`, justo lo que hace el event store |

## Problemas heredados del original

### 1. `markasdelivered` llama al servicio equivocado

`OrderDelivered` usa `_orderDispatchedService` en lugar de `IOrderDeliveredService`, que está registrado pero no se inyecta en el controller. Un pedido nunca llega a `Completed`; "entregar" registra otro `OrderDispatched`. Además, `OrderCompleted` no está en `MongoMapping.RegisterClasses()`.

### 2. Sin validaciones ni reglas en el agregado

- `Apply(OrderPaid)` no comprueba el estado actual: un pedido enviado puede volver a `Paid`, y se puede pagar cuantas veces se quiera.
- Un id inexistente da `NullReferenceException` (500) en `GET`, `markaspaid`, `markasdispatched` y `markasdelivered`: `AggregateRepository.GetByIdAsync` devuelve `null!`.
- `CreateOrderService` lo reconoce en un comentario: *"On a real scenario: validate orders, validate fraud check"*.

### 3. Datos de pago en el event store

`PaymentInformation` (número de tarjeta, caducidad y código de seguridad) forma parte de `OrderCreated` y queda guardado en claro, sin posibilidad de borrarlo sin romper la historia del agregado. También se publica en el mensaje de dominio y se devuelve en cada `GET`.

### 4. Otros detalles

- **`AggregateMappers.ToAggregateChange`** guarda como tipo del evento `change.GetType()`, que es `AggregateChangeDto`, no el tipo real del evento. No tiene efecto porque al reconstruir se usa el contenido.
- **`CreateIndex` se ejecuta en cada `SaveEvents`.** MongoDB lo ignora si ya existe, pero es una llamada extra en cada escritura.
- **Una lectura extra en cada escritura** para comprobar la versión, y otro `new MongoClient(...)` por instancia de `MongoEventStoreManager` (*transient*), con el `//TODO: #20`.
- **Registro manual de eventos** en `MongoMapping` (`//#22 find a way to register the classes automatically`).
- **`ProductInformation`** (`Events/ProductEvents.cs`) no se usa; los nombres de producto son `"fakename"` / `"fakeNAme"`.
- **`Orders.Dto` sigue referenciando `Products.Dtos`**, aunque ya no usa ningún tipo de allí.
- **El `Location` del 202** sigue apuntando a `getorderstatus`, que lanza `NotImplementedException`.
- Siguen los problemas de los capítulos anteriores (CQRS de Products a medias, mensajes envenenados, fuga de conexiones…).

## Particularidades

- **`ObjectSerializer` con tipos permitidos (cambio propio, necesario).** En el capítulo 8 Scattered subió `MongoDB.Driver` a 2.19.0 por la vulnerabilidad CVE-2022-48282. La corrección de esa vulnerabilidad es justamente que el `ObjectSerializer` solo (de)serializa en campos `object` una **lista blanca** de tipos, y el event store guarda cada evento en `Content : object`. Sin configurarlo, crear un pedido da 500 (paso 1). Se añade en `AddScatteredMongoDbConnectionProvider` (`Setup/Databases/MongoDb.cs`), igual que lo hace el `main` de Scattered:
  ```csharp
  BsonSerializer.TryRegisterSerializer(new ObjectSerializer(type =>
      ObjectSerializer.DefaultAllowedTypes(type) || type.FullName?.StartsWith("Scattered.") == true));
  ```
  El original no lo necesitaba en 2022 porque usaba la 2.15.0. Su `main` subió a la 2.19.0 en 2023 (`4334050`, *Bump MongoDB.Driver*) sin añadir esta configuración.
- **YARP 1.0.1 no se aplica.** El original sube los gateways de 1.0.0 a 1.0.1; Scattered ya está en 1.1.2 desde el capítulo 2 por el mismo motivo.
- **`Shared.Databases.MongoDb.csproj`** queda con la misma forma que el del original (un único `ItemGroup` con `DependencyInjection.Abstractions` y `MongoDB.Driver`), pero con la versión 2.19.0 del driver.
- **Guids *legacy*.** El driver 2.x guarda los `Guid` como binario subtipo 3 (`CSharpLegacy`); en `mongosh`, `UUID('…')` (subtipo 4) no los encuentra. Para consultar, usa `TransactionId`.
- **`EventsOrders` heredada de `main`.** El `MongoDb` reutilizado ya tenía la colección (el `mongo-init.js` de `main` la crea) con 6 eventos de pruebas anteriores. No interfieren, porque cada pedido tiene su propio id.
- **El capítulo 10 es el mismo commit.** `10-Consistencia-eventual` apunta a `66a0e62`, igual que `09-Event-sourcing`: no hay código nuevo que replicar en `features/flow9`.
- Siguen vigentes las particularidades de los capítulos anteriores.
