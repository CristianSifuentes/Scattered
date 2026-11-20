# Capítulo 14 · Observabilidad con OpenTelemetry, Prometheus, Grafana y Zipkin (`features/flow13`)

Los logs (capítulo 7) dicen *qué* pasó en cada servicio, y los health checks (capítulo 13) si un servicio puede trabajar. Faltan dos preguntas: **¿por dónde pasó una petición y cuánto tardó en cada servicio?** (trazas) y **¿cuántas peticiones, errores y mensajes hay, y cómo evolucionan?** (métricas). En este capítulo los servicios se instrumentan con **OpenTelemetry**, el estándar abierto de observabilidad, y envían trazas y métricas a un **collector**, que las reparte: trazas a **Zipkin**, métricas a **Prometheus**, y **Grafana** dibuja los paneles. RabbitMQ publica también sus métricas.

<!-- nav:start -->
[← Capítulo 13](flow12.md) · [Índice del flujo](README.md) · Siguiente: capítulo 15 (pendiente)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · [13](flow12.md) · **14** · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow13`)](#flujo-end-to-end-featuresflow13) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`14-monitorizacion-sistemas-distribuidos`](https://github.com/ElectNewt/Distribt/tree/14-monitorizacion-sistemas-distribuidos) |
| Commits de origen | `3ce28c8` small fix on rabbitmq (14-jun-2022) · `a8edf35` add basic opentelemetry (23-jun-2022) · `216f100` add rabbitmq telemetry · `fd5f88b` update readme with the blog link (24-jun-2022) · `14e02ef` #32 update opentelemetry to hold the url in consul instead (26-jun-2022) |
| Diff respecto al capítulo 13 | 5 commits, 23 archivos, +6032 / −11 líneas (5841 son el dashboard de Grafana) |
| Artículo | [Monitorización de sistemas distribuidos](https://www.netmentor.es/entrada/monitorizacion-sistemas-distribuidos) |
| Cambios propios | `prom/prometheus` en vez de `bitnami/prometheus`, exportador `debug` y `endpoint: 0.0.0.0:4317` en el collector, `OpenTelemetry.Instrumentation.AspNetCore` 1.8.1 (vulnerabilidad) y `tools/consul/config.sh` con la API HTTP (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow12 -- src tools docker-compose.yaml`.

## Resumen

```text
                         OTLP gRPC :4317 (dirección de Consul: OpenTelemetryCollector)
 10 servicios .NET ──trazas + métricas──▶ open_telemetry_collector
 (OpenTelemetry,                            ├─ trazas   ─▶ zipkin-traces (:9411)  ← recorrido de cada petición
  AppName = nombre)                         ├─ métricas ─▶ :8889 (formato Prometheus)
                                            └─ debug    ─▶ consola del collector
                                                             ▲
 rabbitmq (:15692, plugin rabbitmq_prometheus) ──────────────┤
                                                       prometheus (:9090) ── lee cada 10 s ──▶ grafana (:3000)
                                                                                                └─ dashboard RabbitMQ-Overview
```

| Señal | Qué responde | Dónde se ve |
|---|---|---|
| **Trazas** | Por qué servicios pasó una petición, en qué orden y cuánto tardó cada tramo | Zipkin, http://localhost:9411 |
| **Métricas** | Cuántas peticiones por servicio, ruta y código; duración (histograma); estado de las colas | Prometheus, http://localhost:9090 · Grafana, http://localhost:3000 |
| **Logs** | Qué pasó (capítulo 7) | Graylog, http://localhost:9000 |

## Qué cambió

```text
docker-compose.yaml                          + opentelemetry-collector, prometheus, grafana, zipkin
                                             rabbitmq: imagen 3.8.34 fija + enabled_plugins montado
tools/telemetry/otel-collector-config.yaml   NUEVO: receptor OTLP gRPC; trazas → zipkin, métricas → prometheus, logs → archivo
tools/telemetry/prometheus.yaml              NUEVO: lee collector :8889 y :8888 y rabbitmq :15692 cada 10 s
tools/telemetry/grafana_datasources.yaml     NUEVO: Prometheus como origen de datos de Grafana
tools/telemetry/rabbitmq-overview_rev11.json NUEVO: dashboard de Grafana para RabbitMQ (se importa a mano)
tools/rabbitmq/enabled_plugins               NUEVO: + rabbitmq_prometheus (métricas en :15692)
tools/rabbitmq/rabbitmq.conf                 loopback_users.guest → loopback_users.ScatteredAdmin
tools/consul/config.sh                       + OpenTelemetryCollector → localhost:4317
src/
├── Shared/Scattered.Shared.Setup
│   ├── Observability/OpenTelemetry.cs       NUEVO: AddTracing, AddMetrics (+ AddLogging sin uso)
│   ├── API/DefaultScatteredWebApplication.cs + AddTracing + AddMetrics en el arranque común
│   └── Scattered.Shared.Setup.csproj        + OpenTelemetry (Exporter.Console/OTLP, Extensions.Hosting, Instrumentation.AspNetCore)
├── Shared/Scattered.Shared.Discovery        + DiscoveryServices.OpenTelemetry = "OpenTelemetryCollector"
└── */appsettings.json (los 10 servicios)    + "AppName": nombre del servicio en trazas y métricas
```

### 1. Instrumentación de los servicios

```csharp
public static void AddTracing(this IServiceCollection serviceCollection, IConfiguration configuration)
{
    serviceCollection.AddOpenTelemetryTracing(builder => builder
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(configuration["AppName"]))   // nombre del servicio
        .AddAspNetCoreInstrumentation()                                                             // un span por petición HTTP
        .AddOtlpExporter(exporter =>
        {
            string url = GetOpenTelemetryCollectorUrl(serviceCollection.BuildServiceProvider()).Result;   // Consul
            exporter.Endpoint = new Uri(url);                                                      // http://localhost:4317
        }));
}
// AddMetrics: lo mismo con AddOpenTelemetryMetrics (duración y número de peticiones HTTP)
```

- **Todos los servicios** quedan instrumentados al llamarse desde `DefaultScatteredWebApplication`.
- **`AppName`** en cada `appsettings.json` da nombre al servicio en Zipkin y en las métricas: `Api.Public`, `Orders.API`, `Products.API.Read`… (y `API.Privae`, con errata, en el gateway privado).
- **La dirección del collector sale de Consul** (`OpenTelemetryCollector`, commit `#32`), como la de Graylog o Vault, y se guarda en un campo estático.
- **Solo se instrumenta ASP.NET Core** (peticiones entrantes). Aun así, las trazas cruzan servicios: `HttpClient` y YARP propagan la cabecera `traceparent`, y el servicio de destino continúa la misma traza.
- `AddLogging` (logs por OpenTelemetry) existe pero no se usa: *"Not used in distribt, added here because of the blogpost"*.

### 2. El collector

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317          # (cambio propio, ver Particularidades)
exporters:
  prometheus: { endpoint: "0.0.0.0:8889" }
  debug:                                # (el original usaba "logging")
  zipkin: { endpoint: "http://zipkin:9411/api/v2/spans", format: proto }
  file: { path: /etc/output/logs.json }
processors:
  batch:
service:
  pipelines:
    traces:  { receivers: [otlp], processors: [batch], exporters: [debug, zipkin] }
    metrics: { receivers: [otlp], processors: [batch], exporters: [debug, prometheus] }
    logs:    { receivers: [otlp], processors: [],      exporters: [debug, file] }
```

El collector desacopla los servicios de los sistemas de destino: los servicios solo hablan OTLP con el collector, y cambiar Zipkin por Jaeger o Prometheus por otro sistema es un cambio de configuración aquí, sin tocar código.

### 3. Prometheus, Grafana y Zipkin

| Contenedor | Imagen | Puerto | Configuración |
|---|---|---|---|
| `open_telemetry_collector` | `otel/opentelemetry-collector:latest` | `4317` OTLP gRPC · `8889` métricas para Prometheus · `8888` métricas propias | `otel-collector-config.yaml`; los logs van a `tools/telemetry/logs/` (ignorado en git) |
| `prometheus` | `prom/prometheus` (original: `bitnami/prometheus`) | `9090` | `prometheus.yaml`: *scrape* cada 10 s de `opentelemetry-collector:8889`, `:8888` y `rabbitmq:15692` |
| `grafana` | `grafana/grafana` | `3000` (`admin` / `admin`) | `grafana_datasources.yaml`: Prometheus como origen de datos |
| `zipkin-traces` | `openzipkin/zipkin:latest` | `9411` | En memoria: las trazas se pierden al reiniciar |

### 4. Métricas de RabbitMQ

`tools/rabbitmq/enabled_plugins` activa `rabbitmq_prometheus`, que expone las métricas del broker en el puerto `15692` (dentro de la red de compose; no se publica al host). `rabbitmq-overview_rev11.json` es el dashboard oficial de RabbitMQ para Grafana, listo para importar. El compose fija la imagen en `3.8.34`.

## Lo que no cambia

- **El comportamiento de negocio**, los health checks y los logs en Graylog.
- **Los mensajes de RabbitMQ no se trazan**: la traza de una petición termina al publicar; el consumer no la continúa.

## Cómo ejecutarla

```bash
docker compose up -d                     # 12 contenedores; recrea rabbitmq (imagen 3.8.34 + enabled_plugins)
sed 's/docker exec -it /docker exec /' tools/vault/config.sh | bash   # si Vault se reinició
./tools/consul/config.sh                 # registra OpenTelemetryCollector
dotnet build Scattered.sln
# Arranca los servicios como en los capítulos anteriores y genera tráfico (crear producto, crear pedido…)
```

| Qué | Dónde |
|---|---|
| Trazas | http://localhost:9411 → *Run Query*; filtra por `serviceName` (`api.public`, `orders.api`…) |
| Métricas (consultas) | http://localhost:9090 → por ejemplo `sum by (exported_job) (http_server_request_duration_seconds_count)` |
| Paneles | http://localhost:3000 (`admin` / `admin`) → *Dashboards → Import* → sube `tools/telemetry/rabbitmq-overview_rev11.json` y elige `prometheus` |
| Estado de los *scrapes* | http://localhost:9090/targets |

Consultas útiles en Prometheus:

```promql
sum by (exported_job) (http_server_request_duration_seconds_count)                                 # peticiones por servicio
sum by (exported_job, http_route, http_response_status_code) (http_server_request_duration_seconds_count{http_route!="/health"})
histogram_quantile(0.95, sum by (le, exported_job) (rate(http_server_request_duration_seconds_bucket[5m])))   # p95
rabbitmq_queue_messages_ready                                                                       # mensajes pendientes (total)
```

## Flujo end to end (`features/flow13`)

> **Rama:** `features/flow13`, equivalente a [`14-monitorizacion-sistemas-distribuidos`](https://github.com/ElectNewt/Distribt/tree/14-monitorizacion-sistemas-distribuidos) del original (`14e02ef`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-20 con los **12 contenedores** corriendo por primera vez a la vez y los 10 procesos arrancados.

| # | Acción | Resultado |
|---|---|---|
| 1 | `docker compose up -d` | `rabbitmq` recreado (3.8.34 + plugins); `open_telemetry_collector`, `prometheus`, `grafana` y `zipkin-traces` reutilizados de `main`. Collector **0.165.0**: *"Everything is ready"* |
| 2 | Plugins de RabbitMQ | `rabbitmq_prometheus`, `rabbitmq_management`, `rabbitmq_amqp1_0`, `rabbitmq_stomp`… activos |
| 3 | Targets de Prometheus | `opentelemetry-collector:8889` **up** · `rabbitmq:15692` **up** · `opentelemetry-collector:8888` **down** (ver problemas) |
| 4 | Tráfico: suscripción, crear producto, crear pedido, 5 × consultar pedido y producto, pedido inexistente | `200` · `201` · `201` · `200` · `404` |
| 5 | Zipkin → servicios | Los **10**: `api.privae`, `api.public`, `email.api`, `orders.api`, `orders.consumer`, `products.api.read`, `products.api.write`, `products.consumer`, `subscription.api`, `subscription.consumer` |
| 6 | Traza de `POST /order-ms/order/create` | **1 traza, 3 servicios**: `api.public` → `orders.api` → `products.api.read` (al pedir el nombre del producto), 276 ms en total |
| 7 | Trazas de `GET /order-ms/order/{id}` | 2 servicios (`api.public` → `orders.api`), 7–12 ms |
| 8 | Trazas de los consumers | Solo `get /health`: el procesamiento de mensajes **no aparece** |
| 9 | Prometheus: peticiones por servicio y ruta | `Orders.API Order/{orderId} 200 → 5`, `Orders.API Order/{orderId} 404 → 1`, `Products.API.Read product/{productId} 500 → 4` (las lecturas mientras el producto nuevo aún no estaba en la vista)… |
| 10 | Prometheus: RabbitMQ | 107 métricas `rabbitmq_*`; `rabbitmq_queue_messages_ready` agregado, sin desglose por cola |
| 11 | Grafana | Origen de datos `prometheus` provisionado; dashboard **RabbitMQ-Overview** (48 paneles) importado; consulta `sum(rabbitmq_queues)` = 10 |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| Las trazas **cruzan servicios** sin instrumentar `HttpClient` | Paso 6 | `traceparent` viaja en las cabeceras: se ve el tiempo de cada tramo (gateway, Orders, Products) |
| RabbitMQ **corta** la traza | Paso 8 | El trabajo asíncrono (consumers) queda fuera; no se puede seguir un pedido hasta Orders.Consumer |
| Los health checks **ensucian** las trazas | Pasos 5 y 9 | La UI consulta `/health` cada pocos segundos: la mayoría de trazas y métricas son `get /health` |
| Las métricas llevan **ruta y código** | Paso 9 | Se ven los 404 y los 500 por endpoint sin mirar logs |
| `job` → `exported_job` | Paso 9 | Prometheus renombra la etiqueta `job` del collector (el nombre del servicio) porque choca con la suya (`collect-metrics`) |
| El collector desacopla servicios y destinos | Configuración | Cambiar Zipkin por otro sistema no toca el código |

## Problemas heredados del original

### 1. Ruido de `/health` en trazas y métricas

`/health-ui` (capítulo 13) consulta `/health` periódicamente, y `AddAspNetCoreInstrumentation()` traza y mide todas las peticiones. La mayoría de lo que llega a Zipkin y Prometheus son health checks. Lo habitual es filtrarlos (`options.Filter = ctx => ctx.Request.Path != "/health"`).

### 2. Sin trazas a través de RabbitMQ

No hay instrumentación de mensajería: ni el publicador añade el contexto de traza al mensaje ni el consumer lo recupera. La parte asíncrona del sistema, justo la más difícil de depurar, no se ve en Zipkin.

### 3. Otros detalles

- **`opentelemetry-collector:8888` siempre *down*** en Prometheus: el collector actual solo publica sus métricas internas en `localhost` dentro del contenedor.
- **Métricas de colas agregadas**: `rabbitmq_prometheus` no desglosa por cola si no se activa `prometheus.return_per_object_metrics`.
- **Otra dependencia de arranque**: la URL del collector se pide a Consul con `.Result` y otro `BuildServiceProvider()`; se guarda en un campo `static` compartido por trazas y métricas.
- **El dashboard de Grafana no se provisiona**: hay que importarlo a mano. Zipkin guarda las trazas en memoria.
- **Versiones mezcladas de OpenTelemetry**: `Exporter.*` 1.3.0, `Extensions.Hosting` 1.0.0-rc9.4 (preliminar) e `Instrumentation.AspNetCore` 1.8.1.
- **`AppName: "API.Privae"`** (errata) y `AddLogging` sin uso.
- **El pipeline de logs del collector** (a `tools/telemetry/logs/logs.json`) no recibe nada: los servicios siguen enviando sus logs a Graylog.
- Siguen los problemas de los capítulos anteriores.

## Particularidades

- **`prom/prometheus` (cambio propio).** `bitnami/prometheus` ya no se publica en Docker Hub y hacía fallar `docker compose up`. Se usa la imagen oficial, como en `main` (documentado en `docs/cambios` de `main`).
- **Collector actual (cambio propio).** Con `otel/opentelemetry-collector:latest` (hoy 0.165.0), la configuración del original no arranca: el exportador `logging` ya no existe (se sustituye por `debug`) y el receptor OTLP solo escucha en `localhost` si no se indica `endpoint: 0.0.0.0:4317`. Es la misma configuración que `main`. El collector avisa de que un componente que se usa saldrá de la distribución *core* en la v0.167.0: con `latest`, la configuración puede volver a romperse.
- **`OpenTelemetry.Instrumentation.AspNetCore` 1.0.0-rc9.4 → 1.8.1 (cambio propio).** La versión original tiene una vulnerabilidad ([GHSA-vh2m-22xx-q94f](https://github.com/advisories/GHSA-vh2m-22xx-q94f), CVE-2024-32028: registra parámetros sensibles de la *query*) y `TreatWarningsAsErrors` rompe la compilación. La 1.8.1 es la primera corregida y la que usa el `main` del original. Funciona junto a los demás paquetes de 2022 (comprobado en ejecución).
- **`tools/consul/config.sh`** suma `OpenTelemetryCollector → localhost:4317` con la API HTTP.
- **Contenedores de `main` reutilizados.** `open_telemetry_collector`, `prometheus`, `grafana` y `zipkin-traces` ya existían con la misma configuración; Prometheus conserva el histórico de `main` (por eso aparecen nombres de métricas antes de que esta rama envíe datos). El dashboard de RabbitMQ quedó importado en ese Grafana.
- **Docker se había detenido** entre capítulos (el equipo se reinició): al volver, Vault estaba vacío y hubo que repetir su `config.sh`.
- Siguen vigentes las particularidades de los capítulos anteriores.
