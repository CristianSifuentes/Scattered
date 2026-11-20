# Capítulo 16 · Rate limiting y paso a .NET 7 (`features/flow15`)

Una API key (capítulo 15) dice **quién** llama; el **rate limiting** decide **cuántas veces** puede hacerlo en un periodo. Sirve para proteger el sistema de abusos o de un cliente con un error que entra en bucle, y para repartir la capacidad entre clientes. .NET 7 trae un limitador integrado (`System.Threading.RateLimiting` y el middleware de ASP.NET Core), así que el capítulo empieza por **subir todo el proyecto a .NET 7** y después añade un ejemplo en el gateway privado: un endpoint que admite 2 llamadas por hora y por API key.

<!-- nav:start -->
[← Capítulo 15](flow14.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 17 →](flow16.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · [13](flow12.md) · [14](flow13.md) · [15](flow14.md) · **16** · [17](flow16.md)
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow15`)](#flujo-end-to-end-featuresflow15) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`16-rate-limiting`](https://github.com/ElectNewt/Distribt/tree/16-rate-limiting) |
| Commits de origen | `79ec7a9` Update Distribt to .NET7 (14-nov-2022) · `4fd8e17` Add rate limiting (24-nov-2022) |
| Diff respecto al capítulo 15 | 2 commits, 26 archivos, +64 / −36 líneas |
| Artículo | [Rate limiting en ASP.NET Core](https://www.netmentor.es/entrada/rate-limiting) |
| Cambios propios | `AddRateLimiter(_ => { })` en el gateway privado, necesario con el runtime de .NET 8 o superior; `OpenTelemetry.Instrumentation.AspNetCore` sigue en 1.8.1 (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow14 -- src GlobalSettings.msbuild`.

## Resumen

```text
Cliente ──GET /rate-limiting-test──▶ API Private (:7022)
          apiKey: b92b0bdf-…          │ 1. ApiKeyMiddleware      (capítulo 15: ¿clave válida?)
                                      │ 2. UseRateLimiter         ScatteredRateLimiterPolicy
                                      │      partición = valor de la cabecera apiKey
                                      │      ventana fija: 2 permisos cada 60 minutos
                                      ├─ llamadas 1 y 2 → 200 "Hello World!"
                                      └─ llamada 3 en adelante → 429 "Lots of calls, please try later"
```

| Parte | Qué hace |
|---|---|
| .NET 7 | `TargetFramework` `net6.0` → `net7.0` en `GlobalSettings.msbuild` y en los 18 `.csproj` que no lo importan |
| Rate limiting | Política propia (`ScatteredRateLimiterPolicy`) aplicada a un endpoint de ejemplo del gateway privado |

## Qué cambió

```text
GlobalSettings.msbuild                       net6.0 → net7.0 (librerías de Shared)
src/**/*.csproj (servicios, APIs, tests)     net6.0 → net7.0
src/Shared/Scattered.Shared.Setup
├── Scattered.Shared.Setup.csproj            − referencia a Scattered.Shared.Api · + Swashbuckle.AspNetCore 6.4.0
├── API/RateLimiting/ScatteredRateLimiterPolicy.cs   NUEVO: IRateLimiterPolicy<string>, ventana fija por apiKey
└── API/HealthCheckHelper.cs                 Dictionary<string, string?> (anotaciones de nulabilidad de .NET 7)
src/Api/Private/Scattered.API.Private/Program.cs     + UseRateLimiter + GET /rate-limiting-test con RequireRateLimiting
src/Services/Orders/…/ProductNameService.cs  string? value (GetStringAsync puede devolver null)
src/Services/*/…Consumer.csproj              − Swashbuckle 6.1.5 (llega por Setup)
```

### 1. Paso a .NET 7

- Las 10 librerías de `Shared` cambian de versión con **una línea** en `GlobalSettings.msbuild` (capítulo 12). Los servicios, APIs y tests no lo importan y cambian uno a uno.
- Swashbuckle sube de 6.1.5 a 6.4.0 y se centraliza en `Shared.Setup`; los consumers dejan de referenciarlo.
- `Shared.Setup` deja de referenciar `Scattered.Shared.Api`, que queda sin uso (ya estaba fuera del `.sln` desde el capítulo 6).
- .NET 7 anota mejor la nulabilidad: `IDistributedCache.GetStringAsync` devuelve `string?`, y con `TreatWarningsAsErrors` hay que reflejarlo en el código (`ProductNameService`, `HealthCheckHelper`).

### 2. La política de rate limiting

```csharp
public class ScatteredRateLimiterPolicy : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers["apiKey"].ToString(),   // un contador por API key
            partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,                                              // 2 peticiones…
                Window = TimeSpan.FromMinutes(60),                            // …por hora
            });

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } =
        (context, _) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.WriteAsync("Lots of calls, please try later");
            return new ValueTask();
        };
}
```

```csharp
// API Private · Program.cs
app.UseApiTokenMiddleware();          // capítulo 15
app.UseRateLimiter();
app.MapGet("/rate-limiting-test", () => "Hello World!")
   .RequireRateLimiting(new ScatteredRateLimiterPolicy());
```

| Algoritmo de .NET 7 | Cómo cuenta |
|---|---|
| **Ventana fija** (el usado) | N permisos por intervalo; el contador se reinicia al acabar la ventana |
| Ventana deslizante | La ventana se divide en segmentos y se va desplazando, sin saltos bruscos |
| *Token bucket* | Un cubo que se rellena a ritmo constante; permite ráfagas |
| Concurrencia | Limita las peticiones simultáneas, no las del periodo |

Particionar por la API key significa que **cada cliente tiene su propio contador**. Con una sola clave válida (capítulo 15), en la práctica es un único contador compartido.

## Lo que no cambia

- **Todo el comportamiento de negocio**, la infraestructura (12 contenedores) y la observabilidad.
- **El resto de rutas del gateway privado no tienen límite**: `/`, `/reports/*`.

## Cómo ejecutarla

```bash
dotnet build Scattered.sln                # compila para net7.0 (avisa NETSDK1138: .NET 7 sin soporte)
# Arranca los servicios como en los capítulos anteriores (DOTNET_ROLL_FORWARD=Major si solo tienes runtimes más nuevos)
KEY=b92b0bdf-da95-42a8-a2b1-780ca461aaf3
for i in 1 2 3; do curl -k -H "apiKey: $KEY" https://localhost:7022/rate-limiting-test; echo; done
# Hello World! · Hello World! · Lots of calls, please try later (429)
```

El contador vive en memoria del proceso: se reinicia al reiniciar el gateway privado.

## Flujo end to end (`features/flow15`)

> **Rama:** `features/flow15`, equivalente a [`16-rate-limiting`](https://github.com/ElectNewt/Distribt/tree/16-rate-limiting) del original (`4fd8e17`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-20 con los 12 contenedores y los 10 procesos, compilados para `net7.0` y ejecutados con el runtime de .NET 10 (`DOTNET_ROLL_FORWARD=Major`).

| # | Acción | Resultado |
|---|---|---|
| 1 | `dotnet build` | 0 errores; todos los proyectos en `net7.0` (18 `.csproj` + las 10 librerías de `Shared` vía `GlobalSettings.msbuild`) |
| 2 | Tests de Orders.BusinessLogic | **4/4** correctos |
| 3 | Test de API de Subscriptions | **Falla** como desde el capítulo 8: `PipeWriter.UnflushedBytes` (TestServer 6.0.2 con el runtime de .NET 10) |
| 4 | Arrancar el gateway privado **sin** el cambio propio | **No arranca**: `Unable to find the required services. Please add all the required services by calling 'IServiceCollection.AddRateLimiter'` |
| 5 | Con `AddRateLimiter(_ => { })`: 4 × `GET /rate-limiting-test` con la clave | `200` · `200` · **`429`** `Lots of calls, please try later` · **`429`** |
| 6 | `GET /reports/order/{id}` y 3 × `GET /` con la misma clave | `200` en todas: solo el endpoint de prueba está limitado |
| 7 | `GET /rate-limiting-test` sin clave | `500` `The API Key is missing`: la API key se comprueba antes que el límite |
| 8 | Regresión: suscripción, crear producto 16, leerlo, crear y consultar pedido, `/health` de Orders | `200` · `201` · `200` · `201` · `200` (`"Hub USB"`) · `200`; trazas en Zipkin |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| El límite se aplica **por endpoint** | Pasos 5 y 6 | `RequireRateLimiting` activa la política solo donde se pide |
| `429 Too Many Requests` | Paso 5 | Es el código estándar: el cliente puede distinguir "espera" de "error" |
| Sin cabecera `Retry-After` | Paso 5 | El cliente no sabe cuánto esperar (en este caso, hasta una hora) |
| El orden de middlewares importa | Paso 7 | Primero la clave (quién) y después el límite (cuánto) |

## Problemas heredados del original

### 1. `UseRateLimiter` sin `AddRateLimiter`

El original solo llama a `app.UseRateLimiter()`. Con el runtime de .NET 7 funcionaba; desde ASP.NET Core 8, el middleware comprueba que sus servicios estén registrados y la aplicación no arranca (paso 4). Scattered añade `AddRateLimiter(_ => { })`, igual que su `main`.

### 2. Ejemplo mínimo

- **Un solo endpoint de prueba** limitado; las rutas reales del gateway (`/reports/*`) no tienen límite.
- **Sin `Retry-After`** ni cabeceras de cuota restante.
- **`OnRejected` no espera** a `WriteAsync` (devuelve un `ValueTask` vacío mientras la escritura sigue en curso).
- **La partición usa el valor de la cabecera** tal cual: con una sola API key válida, todos los clientes comparten el contador.
- **El contador está en memoria**: con varias instancias del gateway, cada una cuenta por su lado.

### 3. Otros detalles

- **.NET 7 tampoco tiene soporte** (terminó en mayo de 2024): el aviso `NETSDK1138` pasa de `net6.0` a `net7.0`.
- **`Scattered.Shared.Api`** queda en disco sin ninguna referencia.
- Siguen los problemas de los capítulos anteriores.

## Particularidades

- **`AddRateLimiter(_ => { })` (cambio propio).** Ver [problema 1](#1-useratelimiter-sin-addratelimiter). Es la misma línea que tiene el `main` de Scattered; el `main` del original no la tiene.
- **Runtime.** Los proyectos compilan para `net7.0`, pero solo está instalado el runtime de .NET 10, así que se ejecutan con `DOTNET_ROLL_FORWARD=Major`. Es la causa de los pasos 3 y 4.
- **`Shared.Setup.csproj` portado a mano.** El original quita la referencia a `Shared.Api` y añade Swashbuckle 6.4.0; Scattered hace lo mismo conservando `OpenTelemetry.Instrumentation.AspNetCore` 1.8.1 (ver [capítulo 14](flow13.md#particularidades)).
- Siguen vigentes las particularidades de los capítulos anteriores.
