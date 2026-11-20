# Capítulo 17 · .NET Aspire (`features/flow16`)

**.NET Aspire** es la propuesta de Microsoft para desarrollar aplicaciones distribuidas en local: un proyecto **AppHost** describe en C# qué servicios forman la aplicación y los arranca juntos, un proyecto **ServiceDefaults** aplica a todos la misma configuración de observabilidad, health checks, resiliencia y descubrimiento de servicios, y un **dashboard** muestra en una sola web los recursos, sus logs, trazas y métricas. En el original, este capítulo es una rama aparte (`17-Aspire-version2023`, enero de 2024) que **nunca llegó a `main`**: prueba Aspire (entonces en *preview*) sobre los tres proyectos de Products, sube el sistema a **.NET 8** y recoge varias actualizaciones de 2023.

<!-- nav:start -->
[← Capítulo 16](flow15.md) · [Índice del flujo](README.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · [13](flow12.md) · [14](flow13.md) · [15](flow14.md) · [16](flow15.md) · **17**
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow16`)](#flujo-end-to-end-featuresflow16) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`17-Aspire-version2023`](https://github.com/ElectNewt/Distribt/tree/17-Aspire-version2023) (rama aparte; no está en `main`) |
| Commits de origen | `5f7a5cf` implement ConsulServiceDiscoveryTest (PR #33, 1-dic-2022) · `4334050` Bump MongoDB.Driver (5-mar-2023) · `2de9b3a` / `0cb6b1a` Bump Yarp.ReverseProxy (2-ago-2023) · `dffe01f` upgrade to net8 and make the services compatible with what it looks an exclusion range on my windows machine (23-dic-2023) · `d44834d` codigo con aspire (6-ene-2024), más 4 *merges* de pull requests |
| Diff respecto al capítulo 16 | 10 commits, 75 archivos, +1148 / −135 líneas |
| Artículo | [Introducción a .NET Aspire](https://www.netmentor.es/entrada/introduccion-net-aspire) |
| Cambios propios | AppHost migrado a **Aspire 13.6.5** (el formato *preview* ya no compila), `launchSettings` del AppHost con las variables actuales, paquetes a 8.0.0 para que la solución compile (el original no compila) y OpenTelemetry 1.8.1 en ServiceDefaults (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow15 -- src tools GlobalSettings.msbuild Scattered.sln`.

## Resumen

```text
dotnet run --project tools/local-development/Scattered.Tools.Aspire/Scattered.Tools.Aspire.AppHost

  AppHost (Program.cs)                        Aspire (dcp)                            Dashboard http://localhost:15175
  builder.AddProject<Products.Api.Read>  ──▶  arranca productsread   :60321 (proxy) ─┐   ├─ Recursos (estado, URL, variables)
  builder.AddProject<Products.Api.Write> ──▶  arranca productswrite  :60320 (proxy) ─┼─▶ ├─ Logs de consola y estructurados
  builder.AddProject<Products.Consumer>  ──▶  arranca productsconsumer :60322 (proxy)┘   ├─ Trazas
                                              OTEL_EXPORTER_OTLP_ENDPOINT=:16087 ────────▶└─ Métricas   (receptor OTLP :16087)
```

| Pieza | Qué hace |
|---|---|
| **AppHost** | Programa .NET que declara los recursos de la aplicación y los arranca. En el original: solo los 3 proyectos de Products |
| **ServiceDefaults** | `builder.AddServiceDefaults()`: OpenTelemetry (logs, trazas, métricas), health check `self`, resiliencia y *service discovery* en los `HttpClient` |
| **Dashboard** | Web con los recursos y su telemetría; recibe OTLP en `:16087` |
| **.NET 8** | `net7.0` → `net8.0` en todo el proyecto; C# 12 |
| **Puertos `60xxx`** | Los servicios pasan de `50xxx` a `60xxx` (Windows reservaba ese rango en la máquina del autor) |

## Qué cambió

```text
GlobalSettings.msbuild                       net7.0 → net8.0, LangVersion 10 → 12
docker-compose.yaml                          consul → hashicorp/consul, vault:latest → hashicorp/vault:latest (Scattered ya los usaba)
tools/local-devleopment/ → local-development/  (Scattered ya usaba el nombre correcto)
tools/consul/config.sh                       puertos de los microservicios 50xxx → 60xxx
tools/local-development/Scattered.Tools.Aspire/
├── Scattered.Tools.Aspire.sln               NUEVO: solución de ejemplo de Aspire (AppHost, ServiceDefaults, ApiService, Web)
├── Scattered.Tools.Aspire.AppHost           NUEVO: arranca Products.Api.Read, Products.Api.Write y Products.Consumer
├── Scattered.Tools.Aspire.ServiceDefaults   NUEVO: AddServiceDefaults (OpenTelemetry, health, resiliencia, discovery)
├── Scattered.Tools.Aspire.ApiService        NUEVO: plantilla de Aspire (weatherforecast), no forma parte de Scattered
└── Scattered.Tools.Aspire.Web               NUEVO: plantilla Blazor de Aspire, no forma parte de Scattered
Scattered.sln                                + AppHost, ServiceDefaults, Test.Shared.Discovery.Tests; tipos de proyecto SDK
src/
├── **/*.csproj                              net7.0 → net8.0; <NoWarn>AD0001</NoWarn> en APIs, Orders, Products y Setup
├── **/Properties/launchSettings.json        puertos 50xxx → 60xxx (y 5032 → 6032, 5073 → 6073)
├── Api/*/appsettings.json                   destinos de YARP → https://localhost:60220/ y :60320/
├── Api/*/…csproj                            Yarp.ReverseProxy 1.0.1 → 1.1.2 (Scattered ya lo tenía)
├── Shared/Scattered.Shared.Setup
│   ├── API/DefaultScatteredWebApplication.cs   + builder.AddServiceDefaults(); − AddTracing / AddMetrics
│   ├── Observability/OpenTelemetry.cs       ELIMINADO (capítulo 14)
│   └── Scattered.Shared.Setup.csproj        + ServiceDefaults; − paquetes OpenTelemetry
├── Shared/Scattered.Shared.EventSourcing    #pragma SYSLIB0050 en FormatterServices (obsoleto en .NET 8, TODO #39)
├── Shared/Shared.Databases/…MongoDb         MongoDB.Driver 2.15.0 → 2.19.0 (Scattered ya lo tenía)
├── Services/Products/*                      + referencia a ServiceDefaults (Read, Write, Consumer)
└── Tests/
    ├── Shared/Discovery/…Discovery.Tests    NUEVO: 4 tests de ConsulServiceDiscovery con Moq
    └── Services/Subscriptions/…ApiTests     UnitTest1 → SubscriptionControllerTest
```

### 1. El AppHost

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Scattered_Services_Products_Api_Read>("productsread");
builder.AddProject<Scattered_Services_Products_Api_Write>("productswrite");
builder.AddProject<Scattered_Services_Products_Consumer>("productsconsumer");

builder.Build().Run();
```

- Cada `AddProject<T>` referencia un `.csproj` (el AppHost los lista como `ProjectReference`); Aspire genera las clases `Projects.*`.
- Al arrancar, el *orquestador* de Aspire (`dcp`) lanza cada proyecto en un puerto interno y publica su puerto habitual (`60320`, `60321`, `60322`) como *proxy*.
- A cada proceso le inyecta `OTEL_EXPORTER_OTLP_ENDPOINT` (el receptor del dashboard) y `OTEL_SERVICE_NAME` (`productsread`…), que es lo que activa la telemetría de ServiceDefaults.
- **Solo están Products**: el resto de servicios, los gateways y la infraestructura (RabbitMQ, Vault, Consul, bases de datos…) se siguen arrancando como antes.

### 2. ServiceDefaults sustituye a la observabilidad del capítulo 14

```csharp
// DefaultScatteredWebApplication.Create
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();          // nuevo
…
// builder.Services.AddTracing(...)    eliminado
// builder.Services.AddMetrics(...)    eliminado
```

```csharp
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();          // logs + métricas (runtime, ASP.NET Core, Kestrel, HttpClient) + trazas (ASP.NET Core, gRPC, HttpClient)
    builder.AddDefaultHealthChecks();          // check "self" con la etiqueta "live"
    builder.Services.AddServiceDiscovery();    // Microsoft.Extensions.ServiceDiscovery (el de Aspire, no el de Consul)
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();   // reintentos, circuit breaker y timeouts en todos los HttpClient
        http.UseServiceDiscovery();
    });
    return builder;
}

private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
{
    var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
    if (useOtlpExporter) { … AddOtlpExporter() … }      // solo exporta si alguien (el AppHost) pone la variable
    return builder;
}
```

Como `AddServiceDefaults()` está en el arranque común, **los 10 servicios** reciben la instrumentación. Pero solo exportan si tienen `OTEL_EXPORTER_OTLP_ENDPOINT`, y esa variable solo la pone el AppHost a los 3 servicios de Products. Fuera de Aspire, la telemetría del capítulo 14 (collector → Zipkin y Prometheus) **deja de llegar** (paso 6 del flujo).

### 3. .NET 8 y los puertos `60xxx`

- `GlobalSettings.msbuild` y cada `.csproj` pasan a `net8.0`, y C# a la versión 12.
- `<NoWarn>AD0001</NoWarn>` silencia los avisos de analizadores que fallan con el SDK 8.
- `FormatterServices.GetUninitializedObject` está obsoleto en .NET 8 (`SYSLIB0050`); se desactiva el aviso con un `#pragma` y un `TODO #39`.
- **Todos los puertos de los microservicios cambian de `50xxx` a `60xxx`**, porque Windows reservaba ese rango en la máquina del autor: `launchSettings.json`, destinos de YARP en los gateways y registros de Consul.

### 4. Tests de Consul

`Scattered.Test.Shared.Discovery.Tests` (contribución externa, PR #33) prueba `ConsulServiceDiscovery` con un `IConsulClient` simulado: dirección con y sin puerto, uso de la caché y excepción cuando el servicio no existe. Son tests unitarios puros: **pasan los 4**.

### 5. El original converge con Scattered

Varios arreglos que Scattered hizo por su cuenta llegan aquí al original:

| Arreglo | Scattered lo hizo en | El original, en este capítulo |
|---|---|---|
| Imagen `hashicorp/vault` | [Capítulo 5](flow4.md#particularidades) | `vault:latest` → `hashicorp/vault:latest` |
| Imagen `hashicorp/consul` | [Capítulo 6](flow5.md#particularidades) | `consul` → `hashicorp/consul` |
| Carpeta `tools/local-development` | [Capítulo 5](flow4.md#particularidades) | Renombra `local-devleopment` |
| `Yarp.ReverseProxy` 1.1.2 | [Capítulo 2](flow2.md#particularidades) | Dependabot sube a 1.1.2 |
| `MongoDB.Driver` 2.19.0 | [Capítulo 8](flow7.md#particularidades) | Dependabot sube a 2.19.0 |

## Lo que no cambia

- **El comportamiento de negocio**: suscripciones, CQRS, Event Sourcing, API key y rate limiting funcionan igual con los puertos nuevos.
- **La infraestructura**: los 12 contenedores siguen haciendo falta. Aspire no gestiona ninguno en esta versión del AppHost.
- **Los health checks** del capítulo 13 (`/health` y `/health-ui`), a los que ServiceDefaults suma la comprobación `self`.

## Cómo ejecutarla

```bash
./tools/local-development/up.sh          # infraestructura y config de Vault/Consul (registra los puertos 60xxx)
dotnet build Scattered.sln

# Opción A · Products con Aspire (dashboard en http://localhost:15175, el enlace con token sale en la consola)
DOTNET_ROLL_FORWARD=Major dotnet run --project tools/local-development/Scattered.Tools.Aspire/Scattered.Tools.Aspire.AppHost

# Opción B · como hasta ahora, cada servicio por separado (sin telemetría)
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Products/Scattered.Services.Products.Api.Read
```

En ambos casos el resto de servicios (gateways, Orders, Subscriptions, Emails) se arranca por separado. Los puertos nuevos:

| Servicio | Antes | Ahora |
|---|---|---|
| Emails | `50120` | `60120` |
| Orders | `50220` | `60220` |
| Products.Api.Write · Read · Consumer | `50320` · `50321` · `50322` | `60320` · `60321` · `60322` |
| Subscriptions | `50420` | `60420` |
| Gateways (`7283`, `7022`) y consumers (`7073`, `7225`) | sin cambios (HTTP: `5032` → `6032`, `5073` → `6073`) | |

## Flujo end to end (`features/flow16`)

> **Rama:** `features/flow16`, equivalente a [`17-Aspire-version2023`](https://github.com/ElectNewt/Distribt/tree/17-Aspire-version2023) del original (`d44834d`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-20 con los 12 contenedores, compilado para `net8.0` y ejecutado con el runtime de .NET 10 (`DOTNET_ROLL_FORWARD=Major`).

| # | Acción | Resultado |
|---|---|---|
| 1 | Compilar **el original tal cual** | **3 errores** `NU1605`: `Microsoft.Extensions.Http` 6.0.0 y `Hosting.Abstractions` 6.0.0 frente a la 8.0.0 que exigen los paquetes de Aspire |
| 2 | Compilar con el AppHost original (Aspire 8 *preview*) | **`NETSDK1228`**: el *workload* de Aspire está retirado; los SDK actuales no lo admiten |
| 3 | Compilar Scattered (con los cambios propios) | 0 errores, sin avisos de vulnerabilidades |
| 4 | Tests | Consul **4/4** · Orders.BusinessLogic **4/4** · API de Subscriptions falla como desde el capítulo 8 (`PipeWriter.UnflushedBytes`) |
| 5 | Los 10 servicios por separado, puertos `60xxx` + Consul reconfigurado | 10/10 arrancan; suscripción `200`, crear producto `201` (URL `https://localhost:60321/product/17`), leerlo `200`, crear pedido por el gateway `201`, consultarlo `200` (`"Altavoz"`), rate limiting `200`; los consumers procesan |
| 6 | Zipkin tras ese tráfico | **0 trazas** de `api.public`: sin `OTEL_EXPORTER_OTLP_ENDPOINT`, nadie exporta |
| 7 | AppHost con el `launchSettings` original | **No arranca**: Aspire 13 exige `ASPIRE_ALLOW_UNSECURED_TRANSPORT` para un `applicationUrl` HTTP |
| 8 | AppHost con el `launchSettings` adaptado | *"Distributed application started"*; dashboard en `http://localhost:15175/login?t=…`; `dcp` publica `60320`–`60322` |
| 9 | Variables de un servicio lanzado por Aspire | `OTEL_SERVICE_NAME=productsread`, `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:16087`, `ASPNETCORE_URLS=https://localhost:26552;…` (puerto interno) |
| 10 | Crear y leer un producto a través de Aspire | `201`; la primera lectura da `stock 0, price 0` y la siguiente `stock 2, price 45`: la ventana de consistencia eventual del capítulo 11 |
| 11 | Conexiones de los 3 servicios con `:16087` | **12** cada uno: la telemetría llega al dashboard |
| 12 | `/health` de Products.Api.Read bajo Aspire | `Healthy` con `self` (de ServiceDefaults) + `mongodb`; `/alive` da `404` porque los servicios no llaman a `MapDefaultEndpoints()` |
| 13 | Parar el AppHost | Aspire detiene los 3 procesos de Products |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| Un comando arranca varios servicios con su telemetría | Pasos 8–11 | Es la promesa de Aspire en desarrollo local |
| Aspire **solo cubre Products** | Pasos 5 y 8 | El resto del sistema y toda la infraestructura siguen igual que antes |
| La observabilidad del capítulo 14 **desaparece fuera de Aspire** | Paso 6 | Zipkin, Prometheus y Grafana siguen corriendo, pero ya nadie les envía datos |
| El original **no compila** con un SDK actual | Pasos 1 y 2 | Rama experimental de 2024, con paquetes *preview* y conflictos de versiones |
| Aspire cambió mucho desde la *preview* | Pasos 2 y 7 | Formato del AppHost, variables de entorno, HTTPS obligatorio |

## Problemas heredados del original

### 1. La rama no compila

Además del *workload* retirado (paso 2), hay conflictos de versiones (`NU1605`, paso 1) que el original no resuelve: `Orders.BusinessLogic` y el test de API de Subscriptions referencian paquetes 6.0.0 mientras ServiceDefaults arrastra los 8.0.0.

### 2. Observabilidad a medias

- `AddTracing`/`AddMetrics` desaparecen y ServiceDefaults solo exporta con `OTEL_EXPORTER_OTLP_ENDPOINT`: fuera de Aspire no hay telemetría, pero el `docker-compose.yaml` sigue levantando el collector, Prometheus, Grafana y Zipkin.
- Los `AppName` de los `appsettings.json` ya no se usan.
- `MapDefaultEndpoints()` no se llama: `/alive` no existe.

### 3. Aspire parcial y con plantilla

- El AppHost solo arranca Products; el resto de servicios, los gateways y la infraestructura quedan fuera.
- `ApiService` y `Web` son la plantilla de Aspire (`weatherforecast`, contador Blazor), sin relación con Scattered.
- ServiceDefaults usa versiones *alpha*/*beta* de OpenTelemetry y una *preview* de `Microsoft.Extensions.ServiceDiscovery`.
- Hay **dos** *service discovery*: el de Consul (`IServiceDiscovery`, capítulo 6) y el de Aspire (`AddServiceDiscovery` en los `HttpClient`).

### 4. Otros detalles

- **Cambio de puertos por una configuración local** de Windows: afecta a todos los `launchSettings`, a YARP y a Consul.
- **`NoWarn AD0001`** y el `#pragma SYSLIB0050` silencian avisos en vez de resolverlos.
- **.NET 8** terminó su soporte el 10 de noviembre de 2026 (aviso `NETSDK1138`).
- Siguen los problemas de los capítulos anteriores.

## Particularidades

- **AppHost migrado a Aspire 13.6.5 (cambio propio).** El original usa el formato de Aspire 8 *preview* (`IsAspireHost` + paquete `Aspire.Hosting` 8.0.0-preview.2), que depende de un *workload* que los SDK actuales rechazan (`NETSDK1228`). Se cambia a `<Sdk Name="Aspire.AppHost.Sdk" Version="13.6.5" />` + `Aspire.Hosting.AppHost` 13.6.5. La 9.5.2, la última de la serie 9, tiene dos vulnerabilidades (GHSA-f7vh-h5g3-7jrh, alta; GHSA-4g86-54fm-h9h8, crítica en el dashboard) corregidas en la 13.6.5, que sigue admitiendo `net8.0`. `Program.cs` del AppHost no cambia.
- **`launchSettings.json` del AppHost (cambio propio).** `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL` (nombre de la *preview*) pasa a `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`, y se añaden `ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL` y `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, necesario para el perfil HTTP.
- **Paquetes a 8.0.0 (cambio propio).** `Microsoft.Extensions.Http` en `Orders.BusinessLogic` y `Microsoft.Extensions.Hosting.Abstractions` en el test de API de Subscriptions, para resolver los `NU1605` del original. `main` hace lo mismo con la versión de su framework.
- **OpenTelemetry 1.8.1 en ServiceDefaults (cambio propio).** `Instrumentation.AspNetCore` e `Instrumentation.Http` 1.6.0-beta.2 → 1.8.1, por la misma vulnerabilidad que en el [capítulo 14](flow13.md#particularidades). Con OpenTelemetry trasladado a Aspire, `Shared.Setup.csproj` queda idéntico al del original.
- **El `.csproj` de los gateways** se porta a mano (solo `NoWarn` y `net8.0`), porque la línea de YARP ya estaba en 1.1.2.
- **`main` no tiene Aspire.** Ni el `main` del original ni el de Scattered incluyen esta rama; el de Scattered sigue con la observabilidad del capítulo 14 (actualizada).
- **No se pudo ver el dashboard en una captura automática**: es una aplicación Blazor que necesita una conexión en tiempo real que Chrome *headless* no completa, y su API de telemetría pide autenticación. La llegada de la telemetría se comprobó por las conexiones OTLP.
- Siguen vigentes las particularidades de los capítulos anteriores.
