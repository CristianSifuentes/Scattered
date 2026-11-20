# Capítulo 1 · Introducción (`features/flow1`)

Punto de partida para replicar el curso de Distribt capítulo a capítulo dentro de Scattered. Este documento describe el estado del código **en la rama `features/flow1`**; las ramas posteriores lo conservan tal cual como referencia histórica.

<!-- nav:start (generado por tools/docs/progress.py desde docs/flow/chapters.json; no editar a mano) -->
[Índice del flujo](README.md) · [Siguiente: capítulo 2 →](flow2.md)

Progreso: **1** · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · 10 · 11 · 12 · 13 · 14 · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Qué contiene](#qué-contiene) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow1`)](#flujo-end-to-end-featuresflow1) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`01-Introduccion`](https://github.com/ElectNewt/Distribt/tree/01-Introduccion) |
| Commit de origen | `051e361` add abstraction on minimal api and webapi (26-dic-2021) |
| Cambios aplicados | Renombrado de `Distribt` a `Scattered` en carpetas, proyectos, namespaces y clases (`DefaultDistribtWebApplication` → `DefaultScatteredWebApplication`), y nota de créditos en el README |

Fuera del renombrado, el código es idéntico al original.

## Qué contiene

**En esta versión todo es esqueleto.** Los controladores devuelven datos fijos (`TODO`), no hay base de datos, y las APIs pública y privada solo dicen "Hello World!". Todavía no reenvían nada; eso llega en el capítulo 2 con YARP.

### Proyectos (9, todos en .NET 6)

| Proyecto | Qué hace hoy | URL local |
|---|---|---|
| `Scattered.API.Public` | Minimal API: `GET /` → `Hello World!` | https://localhost:7283 |
| `Scattered.API.Private` | Minimal API: `GET /` → `Hello World!` | https://localhost:7022 |
| `Scattered.Services.Emails` | `POST /email` devuelve siempre `true`, sin enviar nada | https://localhost:7273 |
| `Scattered.Services.Orders` | `GET /order/{id}` devuelve el mismo id; `POST /order` devuelve un GUID nuevo | https://localhost:7077 |
| `Scattered.Services.Products` | `GET /product/{id}` devuelve nombre y descripción `"TODO"`; `POST /product` devuelve siempre `true` | https://localhost:7185 |
| `Scattered.Services.Subscriptions` | `POST` y `DELETE /subscription` devuelven siempre `true` | https://localhost:7197 |
| `Scattered.Shared.Api` | `DefaultScatteredWebApplication`: arranque común (controllers, Swagger, HTTPS, autorización) | — |
| `Scattered.Shared.Setup` | `GlobalUsings.cs` compartido por todos los proyectos | — |
| `Scattered - Backup.Services.Products` | Proyecto huérfano heredado del original, fuera de la solución | — |

Cada servicio expone Swagger en `/swagger` (solo en entorno `Development`).

### Idea clave del capítulo

`DefaultScatteredWebApplication` centraliza la configuración que comparten todos los servicios. Cada `Program.cs` queda en tres líneas:

```csharp
WebApplication app = DefaultScatteredWebApplication.Create();
app.MapGet("/", () => "Hello World!");
DefaultScatteredWebApplication.Run(app);
```

`Create` acepta un `Action<WebApplicationBuilder>` opcional para que cada servicio añada lo suyo. Ese punto de extensión se usa por primera vez en el capítulo 2.

### Lo que todavía no existe

- Comunicación entre servicios: ni HTTP entre ellos ni mensajería.
- Persistencia: no hay bases de datos.
- Infraestructura: `docker-compose.yaml` solo define RabbitMQ, y ningún servicio lo usa todavía.
- Tests y `GlobalSettings.msbuild`: aparecen en capítulos posteriores.

## Cómo ejecutarla

Requisitos: SDK de .NET 10 y el certificado HTTPS de desarrollo de confianza (`dotnet dev-certs https --trust`, una sola vez).

```bash
dotnet build Scattered.sln
```

Compila con 0 errores y avisos `NETSDK1138`, que solo indican que .NET 6 ya no tiene soporte.

Para ejecutar los servicios sin instalar el runtime de .NET 6, usa `DOTNET_ROLL_FORWARD=Major`, que los hace correr sobre .NET 10:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Private/Scattered.API.Private
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Emails/Scattered.Services.Emails
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Products/Scattered.Services.Products/Scattered.Services.Products.csproj
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Subscriptions/Scattered.Services.Subscriptions
```

Para detenerlos todos: `pkill -f 'Scattered\.(API|Services)'`.

Verificado: los 6 servicios arrancan y responden en las URLs de la tabla anterior.

## Flujo end to end (`features/flow1`)

> **Rama:** `features/flow1`, equivalente a [`01-Introduccion`](https://github.com/ElectNewt/Distribt/tree/01-Introduccion) del original (`051e361`). Las respuestas y los casos límite de esta sección son los de esta rama; en los capítulos siguientes el flujo cambia (por ejemplo, en `features/flow2` las llamadas pasarán por el gateway con YARP).

Con los 6 servicios de esta rama en marcha se puede recorrer un flujo de negocio básico: suscribirse, crear un producto, hacer un pedido, consultarlo, enviar un email y darse de baja. Todas las llamadas funcionan, pero **todavía no hay interconexión**: en este capítulo cada servicio es una isla y ninguno llama a otro. Todo el "flujo" vive en el cliente (`curl`).

### Las llamadas

```bash
curl -k -X POST https://localhost:7197/subscription -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
curl -k -X POST https://localhost:7185/product -H 'Content-Type: application/json' \
  -d '{"productId":"11111111-1111-1111-1111-111111111111","productName":"Teclado","productDescription":"Mecánico"}'
OID=$(curl -sk -X POST https://localhost:7077/order -H 'Content-Type: application/json' \
  -d '{"orderId":"00000000-0000-0000-0000-000000000000"}' | tr -d '"')
curl -k https://localhost:7077/order/$OID
curl -k https://localhost:7185/product/11111111-1111-1111-1111-111111111111
curl -k -X POST https://localhost:7273/email -H 'Content-Type: application/json' \
  -d '{"from":"tienda@scattered.dev","to":"cliente@example.com","subject":"Pedido","body":"Gracias"}'
curl -k -X DELETE https://localhost:7197/subscription -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
```

| # | Llamada | Respuesta | Qué pasa realmente |
|---|---|---|---|
| 1 | `POST :7197/subscription` `{"email":…}` | `200 true` | No guarda nada |
| 2 | `POST :7185/product` | `200 true` | No guarda nada |
| 3 | `POST :7077/order` | `200 "11ec970e-…"` | Genera un `Guid.NewGuid()` nuevo |
| 4 | `GET :7077/order/11ec970e-…` | `200 {"orderId":"11ec970e-…"}` | Devuelve el mismo id que recibe; no busca nada |
| 5 | `GET :7185/product/1111…` | `200 {…"TODO","TODO"}` | Datos fijos |
| 6 | `POST :7273/email` | `200 true` | No envía nada |
| 7 | `DELETE :7197/subscription` | `200 true` | No borra nada |

El paso 4 parece "encontrar" el pedido del paso 3, pero cualquier GUID devolvería lo mismo porque no hay estado.

### Por dónde pasa una petición dentro de cada servicio

```text
curl ──HTTP──▶ Kestrel (:7077)
                │   DefaultScatteredWebApplication.Run(app)  ← src/Shared/Scattered.Shared.Api
                ├─ UseHttpsRedirection   http :5261 → 307 → https :7077
                ├─ UseAuthorization      (sin políticas todavía: deja pasar todo)
                └─ MapControllers
                     │  [Route("[controller]")]  → OrderController → /order
                     │  [ApiController]          → enlaza y valida el body JSON
                     ▼
                OrderController.GetOrder(Guid orderId)
                     └─ Task.FromResult(new OrderDto(orderId))  ← //TODO: logic
```

`Program.cs` solo llama a `Create()`, que registra los controllers y Swagger, y después a `Run()`, que monta el middleware de arriba. Ese arranque compartido es la "abstracción" de este capítulo, y es la razón de que los 4 servicios se comporten igual.

### Casos límite

- **Gateway:** `GET :7283/order/{id}` → **404**. La API pública solo conoce `GET /` (`Hello World!`). El enrutado hacia los servicios llega en el capítulo 2 con YARP.
- **Validación de `[ApiController]`:** un email sin `to`/`subject`/`body` → **400** con `errors`. Un id que no es GUID en `/product/{id}` → **400**. El framework lo hace antes de que se ejecute tu código.
- **HTTP a HTTPS:** HTTP plano devuelve **307** al puerto HTTPS, por `UseHttpsRedirection`.
- **Nombres de ruta:** Swagger muestra la ruta como `/Order` (viene de `[controller]`). `Name = "addorder"` en `[HttpPost]` es solo el nombre del endpoint (`operationId`), no forma parte de la URL.

## Particularidades

- **.NET 6 sin soporte.** El código se mantiene en `net6.0` para ser fiel al curso. La migración a .NET 10 ya está hecha en `main` y se puede repetir al final del flujo.
- **`Scattered.sln` y no `.slnx`.** El formato `.slnx` llegó al original en el último commit de `main` (`6be727b`).
- **Products necesita la ruta al `.csproj`.** Su carpeta contiene también `Scattered - Backup.Services.Products.csproj`, así que `dotnet run --project <carpeta>` falla con *"contiene más de un archivo de proyecto"*. Se mantiene por fidelidad al original; `main` lo eliminó.
- **Puertos aleatorios.** Los puertos de `launchSettings.json` son los que generó Visual Studio. El capítulo 2 los cambia por un esquema fijo (`50X20` HTTPS / `50X10` HTTP).
- **Carpetas vacías al cambiar desde `main`.** Al pasar a esta rama quedan en disco carpetas de proyectos de `main` que solo contienen `bin/` y `obj/` (ignorados por Git). Se limpian con `git clean -ndX` (vista previa) y `git clean -fdX`.
- **Sin historial común con Distribt.** Scattered empezó con un `git init` propio, así que Git no relaciona sus commits con los del original. Para comparar se usa el contenido (ver [Cómo avanzar al siguiente capítulo](README.md#cómo-avanzar-al-siguiente-capítulo)).
