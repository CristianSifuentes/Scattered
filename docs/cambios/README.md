# Cambios respecto al proyecto original

Este documento recoge todo lo que se modificó en esta copia para que la aplicación compile y levante hoy en local. El proyecto original apuntaba a .NET 8 y a versiones de paquetes e imágenes de la época de .NET 6; varias de ellas ya no existen o cambiaron de comportamiento.

[← Volver al README principal](../../README.md)

## Contenido

[Resumen](#resumen) · [1. Infraestructura](#1-infraestructura-local) · [2. Arranque](#2-arreglos-de-arranque) · [3. Fase 1: .NET 10](#3-fase-1-migración-a-net-10) · [4. Fase 2: vulnerabilidades](#4-fase-2-dependencias-con-vulnerabilidades-conocidas) · [5. Fase 3: paquetes](#5-fase-3-actualización-de-paquetes-de-bajo-esfuerzo) · [6. Script](#6-script-de-arranque) · [Certificado HTTPS](#requisito-adicional-certificado-https-de-desarrollo) · [Estado verificado](#estado-verificado) · [Pendiente](#pendiente-y-limitaciones-conocidas)

## Resumen

Entorno en el que se verificó: macOS (Apple Silicon, arm64), SDK de .NET 10.0.403 y Docker 29.9.2.

Los cambios se hicieron en seis pasos, cada uno en su propio commit:

<div style="overflow-x: auto;">

| # | Paso | Commit | Qué resuelve |
|---|---|---|---|
| 1 | [Infraestructura local](#1-infraestructura-local) | `f894488` | `docker-compose up` abortaba y la configuración de Consul fallaba a medias |
| 2 | [Arreglos de arranque](#2-arreglos-de-arranque) | `1391701` | El gateway privado se caía al iniciar y Subscriptions devolvía 500 en `/health` |
| 3 | [Fase 1: .NET 10](#3-fase-1-migración-a-net-10) | `5da9951` | .NET 8 está fuera de soporte |
| 4 | [Fase 2: vulnerabilidades](#4-fase-2-dependencias-con-vulnerabilidades-conocidas) | `f535c42` | 16 paquetes con vulnerabilidades conocidas (uno crítico) |
| 5 | [Fase 3: paquetes](#5-fase-3-actualización-de-paquetes-de-bajo-esfuerzo) | `378faa5` | Paquetes desactualizados que se podían subir con poco esfuerzo |
| 6 | [Script de arranque](#6-script-de-arranque) | `b9fbe77` | Arrancar los 10 servicios con un solo comando |

</div>

Los pasos 1 y 2 corrigen problemas que impedían levantar el proyecto tal como estaba. Los pasos 3 a 5 son la migración a .NET 10, dividida en fases para poder verificar cada una por separado.

## 1. Infraestructura local

Commit `f894488`. Afecta a `docker-compose.yaml` y a los scripts de `tools/`.

<div style="overflow-x: auto;">

| Archivo | Problema | Cambio |
|---|---|---|
| `docker-compose.yaml` | La imagen `bitnami/prometheus` ya no se publica en Docker Hub y abortaba todo el `docker-compose up` | Se usa la imagen oficial `prom/prometheus` |
| `tools/telemetry/otel-collector-config.yaml` | El colector actual eliminó el exportador `logging` y se caía al arrancar | Se sustituye por el exportador `debug` en los tres pipelines |
| `tools/telemetry/otel-collector-config.yaml` | El receptor OTLP ya no escucha por defecto en todas las interfaces | Se fija `endpoint: 0.0.0.0:4317` en el receptor gRPC |
| `tools/consul/config.sh` | La CLI de Consul actual rechaza direcciones con esquema (`http://...`), por lo que fallaban 6 de los 11 registros (Vault y todas las APIs) | Esos servicios se registran por la API HTTP de Consul (`PUT /v1/agent/service/register`) mediante la función `register_with_scheme` |
| `tools/local-development/up.sh`, `tools/vault/config.sh`, `tools/consul/config.sh` | No tenían permiso de ejecución | Se marcan como ejecutables |

</div>

## 2. Arreglos de arranque

Commit `1391701`. Dos servicios no arrancaban bien con el código original.

<div style="overflow-x: auto;">

| Archivo | Problema | Cambio |
|---|---|---|
| `src/Api/Private/Scattered.API.Private/Program.cs` | El gateway privado llamaba a `UseRateLimiter()` sin registrar sus servicios y se caía al iniciar | Se añade `AddRateLimiter` |
| `src/Services/Subscriptions/Scattered.Services.Subscriptions/Properties/launchSettings.json` | Faltaba la variable `VAULT-TOKEN` que sí tienen los demás proyectos; `/health` devolvía 500 | Se añade `VAULT-TOKEN` al perfil de arranque |

</div>

## 3. Fase 1: migración a .NET 10

Commit `5da9951`. .NET 8 llegó al final de su soporte, así que la solución se migró a .NET 10 (LTS). No hizo falta cambiar código C# para esta fase.

<div style="overflow-x: auto;">

| Archivo | Cambio |
|---|---|
| `GlobalSettings.msbuild` y los 20 `.csproj` que lo declaraban | `TargetFramework` pasa de `net8.0` a `net10.0` |
| `GlobalSettings.msbuild` | Se elimina el `LangVersion` fijado en `12.0`; se usa la versión de C# que trae el SDK |
| `.github/workflows/build&Test.yml` | Compila con `dotnet-version: 10.0.x` |
| `Scattered - Backup.Services.Products.csproj` (net6.0) | Eliminado. Era un proyecto huérfano, fuera de la solución, que impedía ejecutar `dotnet run --project` sobre la carpeta de `Products.Api.Write` |
| `src/Shared/Scattered.Shared.Api` (net7.0) | Eliminado. Proyecto huérfano, fuera de la solución |

</div>

## 4. Fase 2: dependencias con vulnerabilidades conocidas

Commit `f535c42`. Al compilar para .NET 10 el SDK reporta las vulnerabilidades de los paquetes transitivos. Había 16 paquetes afectados (uno crítico); tras esta fase `dotnet list package --vulnerable --include-transitive` no devuelve ninguno.

### 4.1 Paquetes actualizados

**Health checks**

<div style="overflow-x: auto;">

| Paquete | Antes | Ahora | Motivo |
|---|---|---|---|
| `AspNetCore.HealthChecks.UI`, `.UI.Client`, `.UI.InMemory.Storage` | 6.0.4 | 9.0.0 | Arrastraban `KubernetesClient`, `AutoMapper`, SQLite y JWT vulnerables |
| `AspNetCore.HealthChecks.MySql` | 6.0.2 | 9.0.0 | Arrastraba `MySqlConnector` vulnerable |
| `AspNetCore.HealthChecks.MongoDb` | 6.0.2 | 9.0.0 | Requerido por el nuevo driver de MongoDB |

</div>

**Bases de datos**

<div style="overflow-x: auto;">

| Paquete | Antes | Ahora | Motivo |
|---|---|---|---|
| `MongoDB.Driver` | 2.19.0 | 3.13.0 | Arrastraba `Snappier` y `SharpCompress` vulnerables |
| `Microsoft.EntityFrameworkCore` | 6.0.3 | 10.0.12 | Arrastraba `Microsoft.Extensions.Caching.Memory` vulnerable |
| `MySql.EntityFrameworkCore` | 6.0.0 | 10.0.12 | Debe coincidir con la versión de EF Core |

</div>

**Observabilidad y descubrimiento de servicios**

<div style="overflow-x: auto;">

| Paquete | Antes | Ahora | Motivo |
|---|---|---|---|
| `OpenTelemetry.Exporter.Console`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.3.0 | 1.21.0 | `OpenTelemetry.Api` vulnerable |
| `OpenTelemetry.Extensions.Hosting` | 1.0.0-rc9.4 | 1.21.0 | Ídem |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.8.1 | 1.21.0 | Ídem |
| `Consul` | 1.6.10.3 | 1.8.0 | Arrastraba `Newtonsoft.Json` 10.0.3 vulnerable |

</div>

**Plataforma (ASP.NET Core y Microsoft.Extensions)**

<div style="overflow-x: auto;">

| Paquete | Antes | Ahora | Motivo |
|---|---|---|---|
| `Microsoft.AspNetCore.Mvc` | 2.2.0 | eliminado | Arrastraba `System.Drawing.Common` (crítica). Se sustituye por `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` en `Scattered.Shared.Communication` |
| `Microsoft.Extensions.*` (Caching.Memory, Configuration.Abstractions, DependencyInjection, DependencyInjection.Abstractions, Hosting.Abstractions, Http, Options.ConfigurationExtensions) | 2.2.0 / 3.1.3 / 6.0.0 | 10.0.12 | Evitar degradaciones de versión (NU1605) con los paquetes anteriores |

</div>

**Tests**

<div style="overflow-x: auto;">

| Paquete | Antes | Ahora | Motivo |
|---|---|---|---|
| `Microsoft.NET.Test.Sdk` | 16.11.0 / 17.4.0 | 18.12.0 | Arrastraba `Newtonsoft.Json` antiguo |
| `Microsoft.AspNetCore.Mvc.Testing` | 6.0.2 | 10.0.12 | Alineado con .NET 10 |

</div>

### 4.2 Paquetes fijados explícitamente

Estos paquetes se declaran aunque el código no los usa de forma directa. Sin ellos queda una vulnerabilidad transitiva o algo deja de funcionar.

<div style="overflow-x: auto;">

| Paquete | Versión | Motivo |
|---|---|---|
| `MySqlConnector` | 2.6.2 | La versión que trae `HealthChecks.MySql` 9.0.0 sigue siendo vulnerable (GHSA-473q-m89c-ghf8) |
| `KubernetesClient` | 17.0.14 | La versión que trae `HealthChecks.UI` 9.0.0 sigue siendo vulnerable (GHSA-w7r3-mgwf-4mqq) |
| `IdentityModel` | 5.2.0 | `HealthChecks.UI` lo necesita y el `KubernetesClient` fijado ya no lo incluye; sin él el recolector de health checks falla |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.12 | `HealthChecks.UI.InMemory.Storage` trae el proveedor de EF Core 8, incompatible con EF Core 10; sin él los servicios no arrancan |

</div>

### 4.3 Cambios de código

Cambios que exigieron las nuevas versiones.

<div style="overflow-x: auto;">

| Archivo | Cambio |
|---|---|
| `src/Shared/Scattered.Shared.Setup/Observability/OpenTelemetry.cs` | `AddOpenTelemetryTracing` y `AddOpenTelemetryMetrics` ya no existen; se usa `AddOpenTelemetry().WithTracing(...)` y `.WithMetrics(...)` |
| `src/Shared/Shared.Databases/Scattered.Shared.Databases.MongoDb/MongoDbDependencyInjection.cs` | Nueva firma de `AddMongoDb` en el health check (recibe una factoría de `IMongoClient`) |
| Ídem | Se registra `GuidSerializer(GuidRepresentation.CSharpLegacy)`: el driver 3.x exige indicar la representación de `Guid`, y esta es la que usaba el driver 2.x por defecto |
| Ídem | Se registra un `ObjectSerializer` que permite los tipos `Scattered.*`. Sin él, guardar eventos en el event store fallaba con error 500 al crear un pedido (este fallo ya existía antes de la actualización) |
| `src/Shared/Shared.Communication/Scattered.Shared.Communication.RabbitMQ/RabbitMQDependencyInjection.cs` | Se elimina un `using Microsoft.AspNetCore.JsonPatch.Adapters` sin uso, que dependía del paquete MVC 2.2 |
| `src/Shared/Scattered.Shared.Discovery/ConsulServiceDiscovery.cs`, `DiscoveryDependencyInjection.cs` | Ajustes de nulabilidad por las anotaciones de los paquetes nuevos |
| `src/Tests/Shared/Discovery/.../ConsulServiceDiscoveryTest.cs` | `Consul` 1.8.0 añade una sobrecarga de `Catalog.Service`; los mocks usan `default(CancellationToken)` para evitar la ambigüedad |

</div>

## 5. Fase 3: actualización de paquetes de bajo esfuerzo

Commit `378faa5`. Paquetes sin vulnerabilidades conocidas pero desactualizados, que se podían subir con cambios pequeños. Los que exigen reescribir código quedaron fuera (ver [Pendiente](#pendiente-y-limitaciones-conocidas)).

### 5.1 Paquetes actualizados

<div style="overflow-x: auto;">

| Área | Paquete | Antes | Ahora |
|---|---|---|---|
| Gateways | `Yarp.ReverseProxy` | 1.1.2 | 2.3.0 |
| Logging | `Serilog` | 2.10.0 | 4.4.0 |
| Logging | `Serilog.AspNetCore` | 5.0.0 | 10.0.0 |
| Logging | `Serilog.Sinks.Console` | 4.0.1 | 6.1.1 |
| Logging | `Serilog.Sinks.Graylog` | 2.3.0 | 3.1.1 |
| API | `Swashbuckle.AspNetCore` | 6.4.0 | 10.3.1 |
| Secretos | `VaultSharp` | 1.7.0.4 | 1.17.5.1 |
| Inyección de dependencias | `Scrutor` | 4.1.0 | 7.0.0 |
| Serialización | `Newtonsoft.Json` | 13.0.1 | 13.0.4 |
| Tests | `xunit` | 2.4.1 / 2.4.2 | 2.9.3 |
| Tests | `xunit.runner.visualstudio` | 2.4.3 / 2.4.5 | 4.0.1 |
| Tests | `Moq` | 4.17.2 / 4.18.2 | 4.21.0 |
| Tests | `coverlet.collector` | 3.1.0 | 10.1.0 |

</div>

### 5.2 Cambios de código

<div style="overflow-x: auto;">

| Archivo | Cambio |
|---|---|
| `src/Shared/Scattered.Shared.Secrets/Extensions/ObjectExtensions.cs` | `VaultSharp` devuelve ahora los valores de los secretos como `JsonElement`; se convierten al tipo de la propiedad. Sin este cambio 6 de los 10 servicios no arrancaban |
| `src/Shared/Scattered.Shared.Logging/Loggers/LoggerConfigurationExtensions.cs` | Cambió la firma de `WriteTo.Graylog`; el nivel mínimo se pasa como argumento con nombre (`minimumLogEventLevel`) |
| `src/Tests/Shared/Discovery/.../ConsulServiceDiscoveryTest.cs` | El analizador de xunit detectó un `Assert.ThrowsAsync` sin `await`; el test pasa a ser `async Task` |

</div>

## 6. Script de arranque

Commit `b9fbe77`. Se añade `tools/local-development/run-services.sh`, que compila la solución y arranca los 10 servicios. Los logs de cada servicio quedan en `tools/local-development/logs` y con `Ctrl+C` se detienen todos.

```bash
./tools/local-development/up.sh            # infraestructura
./tools/local-development/run-services.sh  # servicios
```

Más detalle en [¿Cómo ejecutar la aplicación?](../../README.md#cómo-ejecutar-la-aplicación) del README principal.

## Requisito adicional: certificado HTTPS de desarrollo

Los servicios se llaman entre sí por HTTPS. Si el certificado de desarrollo de .NET no es de confianza, Orders aparece `Unhealthy`, `GET /order/{id}` falla y los gateways devuelven 502. Se resuelve una sola vez con:

```bash
dotnet dev-certs https --trust
```

## Estado verificado

Con los cambios anteriores:

- La solución compila con 0 errores y 0 advertencias, y los tests pasan (8 correctos, 1 omitido).
- Los 12 contenedores y los 10 servicios levantan y responden `Healthy` en `/health`.
- Se probaron contra los servicios: crear y leer un producto, crear y consultar un pedido, crear una suscripción, el reenvío del gateway público, y la API Key y el rate limiting del gateway privado.

## Pendiente y limitaciones conocidas

**Código original**

- `GET /order/getorderstatus/{id}` no está implementado en el código original (lanza `NotImplementedException`).

**Infraestructura**

- Graylog no trae ninguna entrada configurada: para recibir logs hay que crear una de tipo GELF UDP en el puerto 12201 desde `http://localhost:9000`. El envío de logs con el sink actualizado no se ha verificado.
- La imagen `graylog/graylog:4.2.5` es solo amd64; en Apple Silicon corre emulada.
- El colector de OpenTelemetry avisa de que un componente de la configuración saldrá de su imagen en la versión 0.167; como `docker-compose.yaml` usa `latest`, puede requerir un ajuste futuro.

**Paquetes sin actualizar**

- `RabbitMQ.Client` sigue en 6.2.2 y `AspNetCore.HealthChecks.Rabbitmq` en 6.0.2: la versión 7 del cliente tiene una API totalmente asíncrona y exige reescribir el publicador y los consumidores.
- `Netmentor.ROP.ApiExtensions` sigue en 1.0.10.
- `IdentityModel` 5.2.0 está marcado como obsoleto en NuGet; es necesario mientras se use `HealthChecks.UI` 9.
