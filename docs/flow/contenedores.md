# Contenedores de Docker por capítulo

Qué contenedores levanta `docker-compose.yaml` en cada capítulo, qué corre dentro de cada uno y para qué lo usa el sistema. Este documento se actualiza al replicar cada capítulo; su versión en PDF está en [`docs/pdf/contenedores.pdf`](../pdf/contenedores.pdf).

[← Índice del flujo](README.md)

## Contenido

[Estado actual](#estado-actual) · [Línea de tiempo](#línea-de-tiempo) · [Qué nace en cada capítulo](#qué-nace-en-cada-capítulo) · [Ficha de cada contenedor](#ficha-de-cada-contenedor) · [Comandos útiles](#comandos-útiles) · [Cómo actualizar este documento](#cómo-actualizar-este-documento) · [Historial](#historial)

## Estado actual

Foto de `docker ps -a` tomada el **2026-11-18** con `features/flow3` en la copia de trabajo.

| Contenedor | Imagen | Estado | Creado desde | Qué corre dentro ahora |
|---|---|---|---|---|
| `rabbitmq` | `rabbitmq:3.9-management-alpine` (3.9.29) | **Corriendo** | `features/flow3` (18-nov) | `rabbitmq-server` → `beam.smp` (la máquina virtual de Erlang con el broker y el plugin de gestión) + `epmd` (resolución de nombres de nodos Erlang). Colas: `subscription-queue` y `subscription-queue.dead-letter`, ambas con 0 mensajes |
| `vault` | `hashicorp/vault:latest` | Detenido | `main` (11-nov) | — |
| `consul` | `hashicorp/consul` | Detenido | `main` (11-nov) | — |
| `graylog` | `graylog/graylog:4.2.5` | Detenido | `main` (11-nov) | — |
| `mongo_graylog` | `mongo:4.2` | Detenido | `main` (11-nov) | — |
| `elasticserach_graylog` | `elasticsearch-oss:7.10.2` | Detenido | `main` (11-nov) | — |
| `MySql` | `mysql:8.0` | Detenido | `main` (11-nov) | — |
| `MongoDb` | `mongo:latest` | Detenido | `main` (11-nov) | — |
| `open_telemetry_collector` | `otel/opentelemetry-collector:latest` | Detenido | `main` (11-nov) | — |
| `prometheus` | `prom/prometheus` | Detenido | `main` (11-nov) | — |
| `grafana` | `grafana/grafana` | Detenido | `main` (11-nov) | — |
| `zipkin-traces` | `openzipkin/zipkin:latest` | Detenido | `main` (11-nov) | — |

- Los 11 detenidos se crearon con el `docker-compose.yaml` de `main` y salieron con código `255`, que en Docker Desktop suele significar que se reinició Docker o la máquina, no que fallaran.
- Todos comparten el proyecto de compose `scattered` y nombres fijos (`container_name`), así que **cada rama reutiliza el mismo contenedor**. El `rabbitmq` de `main` (3.8.34) fue sustituido por el de `features/flow3` (3.9). Al cambiar de rama: `docker compose up -d --force-recreate`.
- `features/flow3` solo necesita `rabbitmq`. Los otros 11 no existen todavía en su `docker-compose.yaml`.

## Línea de tiempo

Qué contenedores define el `docker-compose.yaml` de cada rama del original. ● = definido y usado · ○ = definido pero el código aún no lo usa · ✔ = capítulo ya replicado en Scattered.

| Contenedor | 1 ✔ | 2 ✔ | 3–4 ✔ | 5 | 6 | 7 | 8 | 9–12 | 13 | 14–16 | main |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `rabbitmq` | ○ 3 | ○ 3 | ● 3.9 | ● 3.9 | ● 3.9 | ● 3.9 | ● 3.9 | ● 3.9 | ● 3.8 | ● 3.8.34 | ● 3.8.34 |
| `vault` | | | | ● | ● | ● | ● | ● | ● | ● | ● |
| `consul` | | | | | ● | ● | ● | ● | ● | ● | ● |
| `graylog` | | | | | | ● | ● | ● | ● | ● | ● |
| `mongo_graylog` | | | | | | ● | ● | ● | ● | ● | ● |
| `elasticserach_graylog` | | | | | | ● | ● | ● | ● | ● | ● |
| `MySql` | | | | | | | ● | ● | ● | ● | ● |
| `MongoDb` | | | | | | | ● | ● | ● | ● | ● |
| `open_telemetry_collector` | | | | | | | | | | ● | ● |
| `prometheus` | | | | | | | | | | ● | ● |
| `grafana` | | | | | | | | | | ● | ● |
| `zipkin-traces` | | | | | | | | | | ● | ● |
| **Total** | **1** | **1** | **1** | **2** | **3** | **6** | **8** | **8** | **8** | **12** | **12** |

En la columna de RabbitMQ figura la versión de la imagen, que cambia a lo largo del curso. Los capítulos 9 a 12 y 14 a 16 no añaden contenedores, así que van agrupados.

## Qué nace en cada capítulo

### Capítulos 1 y 2 · `features/flow1`, `features/flow2` ✔

- **Nace `rabbitmq`** (`rabbitmq:3-management`), pero **ningún proyecto lo usa**: el `docker-compose.yaml` del original ya lo traía desde el esqueleto, preparado para el capítulo 3.
- No hace falta levantarlo para ejecutar estos capítulos.

### Capítulos 3–4 · `features/flow3` ✔

- **`rabbitmq` empieza a usarse** y cambia a `rabbitmq:3.9-management-alpine`, con usuario propio y dos archivos montados: `tools/rabbitmq/rabbitmq.config` y `tools/rabbitmq/definitions.json` (exchanges, colas, bindings y la política de dead letter).
- API Public publica en `api.public.exchange`; `Subscriptions.Consumer` lee de `subscription-queue`.
- Ajuste propio de Scattered: el usuario `ScatteredAdmin` se declara en `definitions.json` porque la imagen actual no lo crea cuando hay definiciones que cargar (ver [capítulo 3–4](flow3.md#particularidades)).

### Capítulo 5 · Gestión de credenciales (pendiente)

- **Nace `vault`**: los servicios dejan de tener las credenciales de RabbitMQ en `appsettings.json` y se las piden a Vault al arrancar.

### Capítulo 6 · Service discovery (pendiente)

- **Nace `consul`**: cada servicio se registra al arrancar y los gateways y servicios preguntan a Consul dónde está cada uno, en lugar de usar puertos escritos a mano.

### Capítulo 7 · Logs en la nube (pendiente)

- **Nacen `graylog`, `mongo_graylog` y `elasticserach_graylog`**: los tres forman la plataforma de logs. Los servicios envían sus logs con Serilog a Graylog.

### Capítulo 8 · CQRS (pendiente)

- **Nacen `MySql` y `MongoDb`**: Products separa el modelo de escritura (MySQL) del de lectura (MongoDB), sincronizados con mensajes de dominio por RabbitMQ.
- En los capítulos 9 a 12 no nacen contenedores nuevos, pero `MongoDb` pasa a guardar también el *event store* de Orders (Event Sourcing, capítulo 9).

### Capítulo 13 · Health checks (pendiente)

- No nace ningún contenedor. RabbitMQ baja a la imagen 3.8 y los servicios exponen `/health` comprobando RabbitMQ, MySQL, MongoDB, Consul y Vault.

### Capítulo 14 · Monitorización (pendiente)

- **Nacen `open_telemetry_collector`, `prometheus`, `grafana` y `zipkin-traces`**: métricas y trazas distribuidas. RabbitMQ fija la versión 3.8.34.
- En el original Prometheus usaba `bitnami/prometheus`; `main` de Scattered usa `prom/prometheus`.

### Capítulos 15–16 y `main`

- No nacen contenedores. `main` es el estado final con los 12.

## Ficha de cada contenedor

### `rabbitmq` · bus de mensajes

| | |
|---|---|
| Nace / se usa | Capítulo 1 / capítulo 3–4 |
| Imagen | `rabbitmq:3.9-management-alpine` en `features/flow3`; `3.8.34-management-alpine` en `main` |
| Proceso | `docker-entrypoint.sh rabbitmq-server` → `beam.smp` (Erlang) + `epmd` |
| Puertos | `5672` AMQP (lo usan los servicios) · `15672` web de gestión |
| Acceso | http://localhost:15672 · `ScatteredAdmin` / `ScatteredPass` |
| Configuración | `tools/rabbitmq/rabbitmq.config` y `definitions.json` montados en `/etc/rabbitmq/` |
| Para qué | Lleva los mensajes entre servicios: integración (entre microservicios) y dominio (dentro de uno). En `main` también expone métricas en `15692` para Prometheus |

### `vault` · secretos

| | |
|---|---|
| Nace | Capítulo 5 |
| Proceso | `vault server -dev`: un solo nodo, almacenamiento en memoria y ya desbloqueado |
| Puertos | `8200` API y web |
| Acceso | http://localhost:8200 · token `vault-scattered-token` |
| Para qué | Guarda las credenciales de RabbitMQ, MySQL y MongoDB. En modo dev **se pierde todo al reiniciar**: hay que volver a cargar los secretos (`tools/vault/config.sh` en `main`) |

### `consul` · service discovery

| | |
|---|---|
| Nace | Capítulo 6 |
| Proceso | `consul agent -dev -client 0.0.0.0`: servidor y agente en un nodo |
| Puertos | `8500` API y web · `8400` · `8600` DNS (TCP/UDP) |
| Acceso | http://localhost:8500 |
| Para qué | Registro de servicios: cada microservicio se da de alta al arrancar y los demás resuelven su dirección por nombre. También lo usan para localizar RabbitMQ, Vault, etc. |

### `graylog` · interfaz de logs

| | |
|---|---|
| Nace | Capítulo 7 |
| Proceso | `tini` → `wait-for-it elasticsearch:9200` → servidor Graylog (Java). Espera a que Elasticsearch responda antes de arrancar |
| Puertos | `9000` web y API · `12201` GELF (TCP/UDP) · `1514` syslog (TCP/UDP) |
| Acceso | http://localhost:9000 · `admin` / `admin` |
| Para qué | Recibe los logs que envían los servicios con Serilog (GELF), los procesa y permite buscarlos. Depende de los dos siguientes |

### `mongo_graylog` · metadatos de Graylog

| | |
|---|---|
| Nace | Capítulo 7 |
| Proceso | `mongod` 4.2 |
| Puertos | ninguno publicado (solo red interna de compose) |
| Para qué | Configuración interna de Graylog: usuarios, streams, inputs. **No guarda logs**. Es distinto de `MongoDb` |

### `elasticserach_graylog` · almacenamiento de logs

| | |
|---|---|
| Nace | Capítulo 7 |
| Proceso | `eswrapper` → Elasticsearch OSS 7.10.2 (Java, heap de 512 MB, límite de 1 GB) |
| Puertos | ninguno publicado (9200/9300 internos) |
| Para qué | Indexa y guarda los logs para que Graylog pueda buscarlos. El nombre lleva la errata del original |

### `MySql` · base de datos relacional

| | |
|---|---|
| Nace | Capítulo 8 |
| Proceso | `mysqld` 8.0; al crearse ejecuta `tools/mysql/init.sql` |
| Puertos | `3307` → 3306 (3307 porque el autor tenía ocupado el 3306) |
| Acceso | base `scattered` · `scatteredUser` / `scatteredPassword` · root `scatteredRootPassword` |
| Para qué | Modelo de escritura de Products (CQRS) y datos relacionales del resto de servicios |

### `MongoDb` · base de datos documental

| | |
|---|---|
| Nace | Capítulo 8 |
| Proceso | `mongod` (última versión); al crearse ejecuta `tools/mongodb/mongo-init.js` (usuario, colección `Products` con 3 productos y `EventsOrders`) |
| Puertos | `27017` |
| Acceso | base `scattered` · `scatteredUser` / `scatteredPassword` |
| Para qué | Modelo de lectura de Products (CQRS) y, desde el capítulo 9, *event store* de Orders |

### `open_telemetry_collector` · recolector de telemetría

| | |
|---|---|
| Nace | Capítulo 14 |
| Proceso | `/otelcol --config=/etc/otel-collector-config.yaml` |
| Puertos | `4317` OTLP gRPC (entrada) · `8889` métricas para Prometheus · `8888` métricas propias |
| Para qué | Punto único al que los servicios envían telemetría con OpenTelemetry. Reparte: trazas → Zipkin, métricas → endpoint de Prometheus, logs → archivo `tools/telemetry/logs/logs.json` |

### `prometheus` · métricas

| | |
|---|---|
| Nace | Capítulo 14 |
| Proceso | `/bin/prometheus --config.file=/etc/prometheus/prometheus.yml` |
| Puertos | `9090` |
| Acceso | http://localhost:9090 |
| Para qué | Cada 10 s lee las métricas del collector (`8888`, `8889`) y de RabbitMQ (`15692`) y las guarda como series temporales |

### `grafana` · dashboards

| | |
|---|---|
| Nace | Capítulo 14 |
| Proceso | `/run.sh` → servidor Grafana, con Prometheus ya configurado como origen de datos |
| Puertos | `3000` |
| Acceso | http://localhost:3000 · `admin` / `admin` |
| Para qué | Paneles sobre las métricas de Prometheus (en `main` hay un dashboard de RabbitMQ en `tools/telemetry`) |

### `zipkin-traces` · trazas distribuidas

| | |
|---|---|
| Nace | Capítulo 14 |
| Proceso | `start-zipkin` → servidor Zipkin (Java), almacenamiento en memoria |
| Puertos | `9411` |
| Acceso | http://localhost:9411 |
| Para qué | Muestra el recorrido completo de una petición entre gateway, servicios y RabbitMQ, con lo que tarda cada tramo. Se pierde al reiniciar |

## Comandos útiles

```bash
docker ps -a --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}'   # qué hay y en qué estado
docker compose up -d rabbitmq                                        # levantar lo que pide la rama actual
docker compose up -d --force-recreate                                # recrear tras cambiar de rama
docker compose stop                                                  # parar sin borrar
docker top rabbitmq                                                  # procesos dentro de un contenedor
docker logs -f rabbitmq                                              # logs del contenedor
docker exec rabbitmq rabbitmqctl list_queues name messages consumers # colas y mensajes
```

## Cómo actualizar este documento

Al replicar un capítulo que toca `docker-compose.yaml`:

1. Marca el capítulo con ✔ en la [línea de tiempo](#línea-de-tiempo) y en [Qué nace en cada capítulo](#qué-nace-en-cada-capítulo), con su rama `features/flowN`.
2. Añade la ficha de cada contenedor nuevo, o corrige la existente si cambia la imagen, los puertos o la configuración.
3. Rehaz la foto de [Estado actual](#estado-actual) con `docker ps -a` y `docker top`.
4. Añade una línea al [historial](#historial) y regenera `docs/pdf/contenedores.pdf`.

## Historial

| Fecha | Rama | Cambio |
|---|---|---|
| 2026-11-18 | `features/flow3` | Documento creado. Estado: `rabbitmq` corriendo (flow3); 11 contenedores de `main` detenidos. Capítulos 1–4 replicados |
