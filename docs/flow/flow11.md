# Capítulo 12 · Patrón SAGA (`features/flow11`)

Una **SAGA** coordina una operación que abarca varios servicios, cada uno con su propia base de datos, sin una transacción común. En lugar de un `COMMIT` global, la operación se divide en pasos locales; si uno falla, se ejecutan **compensaciones** que deshacen los pasos ya hechos. En el original, este capítulo **no añade código de SAGA**: el commit `1c53dbf` *add saga to readme* solo enlaza el [artículo](https://www.netmentor.es/entrada/patron-saga), que explica el patrón en teoría. Lo que sí cambia en el código son dos mejoras de mantenimiento: un `GlobalSettings.msbuild` común a las librerías de `Shared` y un `GetByIdOrDefaultAsync` que cierra el TODO `#27` del event sourcing.

<!-- nav:start -->
[← Capítulo 11](flow10.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 13 →](flow12.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · **12** · [13](flow12.md) · 14 · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Qué es una SAGA](#qué-es-una-saga) · [Dónde haría falta en Scattered](#dónde-haría-falta-en-scattered) · [Qué cambió en el código](#qué-cambió-en-el-código) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end](#flujo-end-to-end-featuresflow11) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`12-Saga`](https://github.com/ElectNewt/Distribt/tree/12-Saga) |
| Commits de origen | `1c53dbf` add saga to readme · `5bf00fb` #27 create a return with nullable in the aggregate · `0e5e34a` group the key information of every package into GlobalSettings (6-jun-2022) |
| Diff respecto al capítulo 11 | 3 commits, 16 archivos, +75 / −77 líneas |
| Artículo | [Patrón SAGA](https://www.netmentor.es/entrada/patron-saga) |
| Cambios propios | Metadatos de `GlobalSettings.msbuild` con los de Scattered, `MongoDB.Driver` 2.19.0 y se omite el cambio de espaciado de la línea de YARP 1.0.1 (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow10 -- src GlobalSettings.msbuild`.

## Qué es una SAGA

En un monolito, "crear pedido, cobrar y reservar stock" sería una transacción: o se hace todo o nada. Con microservicios, cada paso vive en un servicio y una base distintos, y no hay transacción que los abarque (el *two-phase commit* acopla los servicios y no escala). La SAGA lo resuelve así:

| | |
|---|---|
| **Pasos locales** | Cada servicio hace su parte con su propia transacción y avisa del resultado (normalmente con un evento) |
| **Compensaciones** | Por cada paso hay una acción que lo deshace: liberar el stock reservado, devolver el cobro, cancelar el pedido |
| **Consistencia** | Eventual: durante la SAGA el sistema puede estar a medias (pedido creado pero sin cobrar) |

Hay dos formas de coordinarla:

| | Coreografía | Orquestación |
|---|---|---|
| Quién decide el siguiente paso | Cada servicio reacciona a los eventos de los demás | Un orquestador central (un servicio o una máquina de estados) da las órdenes |
| Ventajas | Sin punto central; encaja con los eventos que Scattered ya publica | El flujo está en un solo sitio; es más fácil de seguir y de cambiar |
| Inconvenientes | El flujo queda repartido y es difícil de ver; riesgo de dependencias circulares | El orquestador es otra pieza que mantener y un posible cuello de botella |

```text
Coreografía (pedido)                                        Compensación si falla el cobro
Orders: OrderCreated ──▶ Products: reservar stock ──▶ StockReserved
                         Payments: cobrar ──▶ PaymentFailed ──▶ Products: liberar stock
                                                            └──▶ Orders: OrderCancelled
```

## Dónde haría falta en Scattered

El código ya tiene operaciones que cruzan varios servicios sin ninguna coordinación:

| Operación | Pasos | Qué pasa hoy si un paso falla |
|---|---|---|
| Crear un producto (`CreateProductDetails`, cap. 8) | 1. Insertar en MySQL · 2. `IStockApi.AddStockToProduct` · 3. `IWarehouseApi.ModifySalesPrice` · 4. Publicar `ProductCreated` | `IStockApi` e `IWarehouseApi` son falsos (siempre `true`). Con servicios reales, un fallo en el paso 2 o 3 dejaría el producto en MySQL sin stock ni precio. El propio código lo anuncia: *"Note: we will see sagas in the future"* |
| Crear un pedido (`CreateOrderService`, cap. 11) | 1. Guardar `OrderCreated` · 2. Pedir los nombres a Products · 3. Publicar el mensaje de dominio | Si el paso 2 falla, el pedido queda guardado, sin publicar y respondiendo 201 (paso 13 del [capítulo 11](flow10.md#flujo-end-to-end-featuresflow10)) |
| Ciclo de vida del pedido (cap. 9) | Created → Paid → Dispatched → Completed | No hay cobro, reserva de stock ni compensaciones: los estados se cambian a mano con `PUT` y sin validar |

Las piezas para una SAGA por coreografía ya existen: mensajes de dominio e integración por RabbitMQ, consumers con handlers y un event store en Orders. Lo que falta son los servicios que participarían (stock, pagos), los eventos de fallo y las compensaciones. `main` tampoco lo implementa.

## Qué cambió en el código

### 1. `GlobalSettings.msbuild`

Un archivo nuevo en la raíz con la configuración común, que las **10 librerías de `src/Shared`** importan al final de su `.csproj`:

```xml
<Project>
  <PropertyGroup>
    <Authors>Ivan Abad</Authors>  <Company>…</Company>  <Product>…</Product>  <Version>1.0.0</Version>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>  <RepositoryUrl>…</RepositoryUrl>
  </PropertyGroup>
  <PropertyGroup>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <LangVersion>10.0</LangVersion>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  …
</Project>
```

```diff
 <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
-        <TargetFramework>net6.0</TargetFramework>
-        <ImplicitUsings>enable</ImplicitUsings>
-        <Nullable>enable</Nullable>
-        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
         <RootNamespace>Scattered.Shared.Discovery</RootNamespace>
     </PropertyGroup>
     …
+    <Import Project="../../../GlobalSettings.msbuild" />
 </Project>
```

- Cambiar el *framework* o activar un aviso en todas las librerías pasa a ser un cambio en un solo archivo (es lo que hará el capítulo 16 para subir a .NET 7).
- Los metadatos (`Authors`, `Version`, `RepositoryUrl`…) son los de un paquete NuGet: las librerías de `Shared` quedan listas para publicarse como paquetes.
- `AppendTargetFrameworkToOutputPath=false` cambia la carpeta de salida: los `.dll` de `Shared` van a `bin/Debug/` en vez de `bin/Debug/net6.0/`.
- Los proyectos de servicios, APIs y tests **no** lo importan; siguen con su configuración propia.
- `Shared.Databases.MySql` cambia de paso la referencia a la DLL de Windows por `PackageReference` (Scattered ya lo tenía desde el capítulo 8).

### 2. `GetByIdOrDefaultAsync` (#27)

En el capítulo 9, `GetByIdAsync` devolvía `null!` cuando no había eventos: el tipo decía "nunca es nulo" y el valor sí lo era. Ahora hay dos métodos con contratos claros:

```csharp
public async Task<TAggregate> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    => await GetByIdOrDefaultAsync(id, cancellationToken) ??
       throw new ApplicationException("seems that the aggregate cannot be found, please use GetByIdOrDefaultAsync");

public async Task<TAggregate?> GetByIdOrDefaultAsync(Guid id, CancellationToken cancellationToken = default)
{
    var events = (await _eventStore.GetEventsForAggregate(AggregateName, id, cancellationToken)).ToList();
    if (!events.Any())
        return null;
    …
}
```

`OrderRepository` expone `GetByIdOrDefault`, y `GetOrderService` lo usa para devolver el 404. Los servicios de pagar/enviar/entregar siguen con `GetById`: un id inexistente pasa de `NullReferenceException` a `ApplicationException`, con un mensaje claro, pero sigue siendo un 500.

## Lo que no cambia

- **Infraestructura, mensajes, Products, Orders y Subscriptions**: el comportamiento es el del capítulo 11.
- **Los problemas de ROP del capítulo 11** (`markaspaid` serializa un `Task`, `markasdispatched` vacío, stock y precio a 0 al actualizar un producto).

## Cómo ejecutarla

Igual que el [capítulo 11](flow10.md#cómo-ejecutarla). Para comprobar el cambio de carpeta de salida:

```bash
dotnet build Scattered.sln
ls src/Shared/Scattered.Shared.Discovery/bin/Debug/        # Scattered.Shared.Discovery.dll directamente aquí
```

Si la carpeta `bin/Debug/net6.0/` de compilaciones anteriores sigue existiendo, contiene `.dll` antiguos que ya no se usan; se puede borrar con `dotnet clean` o a mano.

## Flujo end to end (`features/flow11`)

> **Rama:** `features/flow11`, equivalente a [`12-Saga`](https://github.com/ElectNewt/Distribt/tree/12-Saga) del original (`0e5e34a`).

Ejecutado el 2026-11-20 con los 8 contenedores y los 10 procesos arrancados. El objetivo es comprobar que la refactorización no rompe nada y ver el cambio del `#27`.

| # | Acción | Respuesta | Comentario |
|---|---|---|---|
| 1 | `dotnet build` + tests de Orders | 0 errores · **4/4 tests** | Los `.dll` de `Shared` salen en `bin/Debug/` |
| 2 | `POST /subscribe` | `200` | `Email saga@example.com successfully subscribed.` |
| 3 | `POST :50320/product` (Auriculares) | `201` `{"value":{"url":"…/product/13"},…}` (4,8 s, primera petición) | — |
| 4 | `GET :50321/product/13` a los 3 s | **`500`** | Todavía no estaba en la vista de lectura: el consumer recién arrancado tardó más (consistencia eventual, ver [capítulo 10](flow9.md)) |
| 5 | `GET :50321/product/13` unos segundos después | `200` `stock: 4`, `price: 59.9` | Convergió; también llegó a `ProductName` de Orders |
| 6 | `POST /order-ms/order/create` con el producto 13 | `201` + `orderId` | `Order 4911603c-… created` en Orders.Consumer |
| 7 | `GET` del pedido | `200`, producto `"Auriculares"` | — |
| 8 | `PUT markaspaid` | `202` con el `Task` serializado | Igual que en el capítulo 11 |
| 9 | `GET` de un pedido inexistente | **`404`** `"Order … not found"` | Ahora vía `GetByIdOrDefault` |
| 10 | `PUT markaspaid` de un pedido inexistente | **`500`** `ApplicationException: seems that the aggregate cannot be found, please use GetByIdOrDefaultAsync` | Antes era `NullReferenceException` |

## Problemas heredados del original

- **Ninguna SAGA implementada.** El capítulo es teórico: las operaciones que cruzan servicios siguen sin coordinación ni compensaciones (ver [Dónde haría falta](#dónde-haría-falta-en-scattered)).
- **`GlobalSettings.msbuild` solo en `Shared`**: servicios, APIs y tests repiten su `TargetFramework`, `Nullable`… Un `Directory.Build.props` en la raíz se aplicaría a todos sin tocar cada `.csproj`.
- **Propiedades duplicadas**: en `GlobalSettings.msbuild`, `Description` aparece dos veces en cada bloque de configuración (y pisa la del bloque general).
- **`ApplicationException`** como error de dominio; pagar/enviar/entregar no usan `GetByIdOrDefault` ni `Result`.
- Siguen los problemas de los capítulos anteriores.

## Particularidades

- **Metadatos de `GlobalSettings.msbuild` (cambio propio).** El renombrado automático Distribt → Scattered producía `RepositoryUrl https://github.com/ElectNewt/Scattered`, que no existe. Se usan los mismos valores que el `main` de Scattered: `Company` y `Product` de CristianSifuentes y `RepositoryUrl https://github.com/CristianSifuentes/Scattered`, manteniendo a Ivan Abad como autor y el copyright de NetMentor.
- **`MongoDB.Driver` 2.19.0** en `Shared.Databases.MongoDb.csproj` (el original sigue en 2.15.0, ver [capítulo 8](flow7.md#particularidades)).
- **API Private**: el original solo cambia espacios en la línea de `Yarp.ReverseProxy` 1.0.1; en Scattered esa línea es la 1.1.2 y no se toca.
- Siguen vigentes las particularidades de los capítulos anteriores.
