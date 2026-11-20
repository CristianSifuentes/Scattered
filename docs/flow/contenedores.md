# Contenedores de Docker por capítulo

Qué contenedores levanta `docker-compose.yaml` en cada capítulo, qué corre dentro de cada uno y para qué lo usa el sistema. Este documento se actualiza al replicar cada capítulo; su versión en PDF está en [`docs/pdf/contenedores.pdf`](../pdf/contenedores.pdf).

[← Índice del flujo](README.md)

## Contenido

[Estado actual](#estado-actual) · [Línea de tiempo](#línea-de-tiempo) · [Qué nace en cada capítulo](#qué-nace-en-cada-capítulo) · [Ficha de cada contenedor](#ficha-de-cada-contenedor) · [Comandos útiles](#comandos-útiles) · [Cómo actualizar este documento](#cómo-actualizar-este-documento) · [Historial](#historial)

## Estado actual

Foto de `docker ps -a` tomada el **2026-11-19** con `features/flow6` en la copia de trabajo.

| Contenedor | Imagen | Estado | Creado desde | Qué corre dentro ahora |
|---|---|---|---|---|
| `rabbitmq` | `rabbitmq:3.9-management-alpine` (3.9.29) | **Corriendo** | `features/flow3` (18-nov) | `rabbitmq-server` → `beam.smp` (la máquina virtual de Erlang con el broker y el plugin de gestión) + `epmd`. Usuarios: `ScatteredAdmin` y los `token-…` temporales que crea Vault para el gateway |
| `vault` | `hashicorp/vault:latest` (2.2.1) | **Corriendo** | `features/flow4` (18-nov) | `dumb-init` → `docker-entrypoint.sh` → `vault server -dev -dev-root-token-id=vault-scattered-token`. Motores `secret/` (KV con `secret/rabbitmq`) y `rabbitmq/` (rol `scattered-role`), cargados con `tools/vault/config.sh` |
| `consul` | `hashicorp/consul:latest` (2.1.0) | **Corriendo** | `features/flow5` (19-nov) | `dumb-init` → `docker-entrypoint.sh` → `consul agent -data-dir=/consul/data -config-dir=/consul/config -dev -client 0.0.0.0`. Servicios registrados con `tools/consul/config.sh`: `RabbitMQ`, `SecretManager`, `EmailsApi`, `OrdersApi`, `ProductsApi`, `SubscriptionsApi` |
| `graylog` | `graylog/graylog:4.2.5` (amd64, emulado) | **Corriendo** (healthy) | `features/flow6` (19-nov) | `tini` → `wait-for-it elasticsearch:9200` → `docker-entrypoint.sh` → servidor Graylog (Java). Un input *GELF UDP* en `12201`, creado a mano |
| `mongo_graylog` | `mongo:4.2` (4.2.24) | **Corriendo** | `main` (11-nov), reutilizado | `mongod`: configuración de Graylog (usuarios, inputs) |
| `elasticserach_graylog` | `elasticsearch-oss:7.10.2` | **Corriendo** | `main` (11-nov), reutilizado | Elasticsearch: índice `graylog_0` con los logs |
| `MySql` | `mysql:8.0` | Detenido | `main` (11-nov) | — |
| `MongoDb` | `mongo:latest` | Detenido | `main` (11-nov) | — |
| `open_telemetry_collector` | `otel/opentelemetry-collector:latest` | Detenido | `main` (11-nov) | — |
| `prometheus` | `prom/prometheus` | Detenido | `main` (11-nov) | — |
| `grafana` | `grafana/grafana` | Detenido | `main` (11-nov) | — |
| `zipkin-traces` | `openzipkin/zipkin:latest` | Detenido | `main` (11-nov) | — |

- Los 6 detenidos se crearon con el `docker-compose.yaml` de `main` y salieron con código `255`, que en Docker Desktop suele significar que se reinició Docker o la máquina, no que fallaran.
- Todos comparten el proyecto de compose `scattered` y nombres fijos (`container_name`), así que **cada rama reutiliza el mismo contenedor**. `rabbitmq` (3.8.34 en `main`), `vault` (con dependencia de Consul en `main`) y `consul` se recrearon con la configuración de las ramas `flowN`. Ojo con `consul`: al recrearlo, compose **conserva su volumen anónimo `/consul/data`** y con él los servicios registrados por otra rama; para empezar limpio, `docker rm -fv consul` antes de `docker compose up -d consul`. Al cambiar de rama: `docker compose up -d --force-recreate`.
- `features/flow6` necesita `rabbitmq`, `vault`, `consul`, `graylog`, `mongo_graylog` y `elasticserach_graylog`. Los otros 6 no existen todavía en su `docker-compose.yaml`.
- `mongo_graylog` y `elasticserach_graylog` tienen la misma configuración en `main` y en `features/flow6`, así que `docker compose up -d` los reutiliza con sus datos; `graylog` se recrea.

## Línea de tiempo

Qué contenedores define el `docker-compose.yaml` de cada rama del original. ● = definido y usado · ○ = definido pero el código aún no lo usa · ✔ = capítulo ya replicado en Scattered.

| Contenedor | 1 ✔ | 2 ✔ | 3–4 ✔ | 5 ✔ | 6 ✔ | 7 ✔ | 8 | 9–12 | 13 | 14–16 | main |
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

### Capítulo 5 · Gestión de credenciales · `features/flow4` ✔

- **Nace `vault`** en modo desarrollo (`vault server -dev`, en memoria, token raíz `vault-scattered-token`). Los servicios dejan de tener las credenciales de RabbitMQ en `appsettings.json` y se las piden a Vault: el consumer lee el secreto KV `secret/rabbitmq` al arrancar y el gateway pide al motor `rabbitmq` un usuario temporal (`token-…`) en su primera publicación.
- `tools/vault/config.sh` carga el secreto y configura el motor; `tools/local-development/up.sh` levanta los contenedores y lo ejecuta. Hay que repetirlo **cada vez que se recrea `vault`**, porque en modo dev empieza vacío.
- Vault llega a RabbitMQ por la red de Docker Compose (`http://rabbitmq:15672`) con el usuario `ScatteredAdmin` para crear y borrar los usuarios temporales.
- Ajuste propio de Scattered: la imagen `vault:latest` del original ya no existe en Docker Hub; se usa `hashicorp/vault:latest` (ver [capítulo 5](flow4.md#particularidades)).

### Capítulo 6 · Service discovery · `features/flow5` ✔

- **Nace `consul`** en modo desarrollo (`consul agent -dev`, un nodo que es servidor y agente). Los servicios dejan de tener en `appsettings.json` la dirección de Vault y el host de RabbitMQ: solo conocen `Discovery:Address = http://localhost:8500` y preguntan por nombre (`SecretManager`, `RabbitMQ`).
- **Todos los procesos le preguntan al arrancar** (lo hace el arranque común), así que sin Consul no arranca ninguno. Una vez arrancados, guardan la respuesta y siguen funcionando aunque Consul caiga.
- `tools/consul/config.sh` registra 6 servicios a mano: `RabbitMQ`, `SecretManager` y los 4 microservicios, que todavía no consulta nadie (YARP sigue con sus direcciones fijas). `up.sh` lo ejecuta después del de Vault.
- Al contrario que Vault, **conserva el registro al reiniciarse**: el agente lo guarda en `/consul/data`.
- `vault` pasa a depender de `consul` en `depends_on`.
- Ajustes propios de Scattered: la imagen `consul` del original ya no existe en Docker Hub (se usa `hashicorp/consul:latest`), y `config.sh` registra por la API HTTP porque la CLI actual rechaza las direcciones con `http://` del original (ver [capítulo 6](flow5.md#particularidades)).

### Capítulo 7 · Logs en la nube · `features/flow6` ✔

- **Nacen `graylog`, `mongo_graylog` y `elasticserach_graylog`**: los tres forman la plataforma de logs. Graylog espera a Elasticsearch (`wait-for-it`) antes de arrancar y se encuentra con sus dos dependencias por los nombres de servicio de compose (`mongo`, `elasticsearch`).
- Los servicios usan Serilog y pueden enviar logs por **GELF/UDP** a `localhost:12201`. En esta rama solo Orders lo configura, y solo para errores.
- **Graylog arranca sin inputs**: hay que crear uno *GELF UDP* en el puerto `12201` desde la web (*System → Inputs*) o por la API (ver [capítulo 7](flow6.md#cómo-ejecutarla)). Se guarda en `mongo_graylog`.
- `rabbitmq` no cambia de imagen, pero `definitions.json` suma `order.exchange` y `order-queue`: hay que reiniciarlo para que las cree.
- En Apple Silicon, `graylog/graylog:4.2.5` corre emulado (solo hay imagen amd64). En la ejecución del capítulo, Docker Desktop no publicó el `12201/udp` hasta reiniciar el contenedor (ver [capítulo 7](flow6.md#particularidades)).

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
| Imagen | `hashicorp/vault:latest` (el original usa `vault:latest`, retirada de Docker Hub) |
| Proceso | `vault server -dev`: un solo nodo, almacenamiento en memoria y ya desbloqueado |
| Puertos | `8200` API y web |
| Acceso | http://localhost:8200 · token `vault-scattered-token` |
| Para qué | Guarda las credenciales. En el capítulo 5, solo las de RabbitMQ: un secreto KV (`secret/rabbitmq`, para el consumer) y un motor que crea usuarios temporales (`rabbitmq/creds/scattered-role`, para el gateway). Desde el capítulo 8, también las de MySQL y MongoDB. En modo dev **se pierde todo al reiniciar**: hay que volver a ejecutar `tools/vault/config.sh` |

### `consul` · service discovery

| | |
|---|---|
| Nace | Capítulo 6 |
| Imagen | `hashicorp/consul:latest` (el original usa `consul`, retirada de Docker Hub) |
| Proceso | `consul agent -dev -client 0.0.0.0`: servidor y agente en un nodo; guarda su estado en el volumen `/consul/data` |
| Puertos | `8500` API y web (lo usan los servicios) · `8600` DNS (TCP/UDP) · `8400` publicado pero sin uso (RPC de versiones antiguas) |
| Acceso | http://localhost:8500 |
| Para qué | Registro de servicios. En el capítulo 6, los servicios resuelven por nombre dónde están Vault (`SecretManager`) y RabbitMQ; los microservicios están registrados con `tools/consul/config.sh` pero todavía no los consulta nadie. En capítulos posteriores los gateways y servicios lo usan para llamarse entre sí y se registran solos |

### `graylog` · interfaz de logs

| | |
|---|---|
| Nace | Capítulo 7 |
| Imagen | `graylog/graylog:4.2.5`, solo amd64 (emulada en Apple Silicon) |
| Proceso | `tini` → `wait-for-it elasticsearch:9200` → servidor Graylog (Java). Espera a que Elasticsearch responda antes de arrancar |
| Puertos | `9000` web y API · `12201` GELF (TCP/UDP) · `1514` syslog (TCP/UDP) |
| Acceso | http://localhost:9000 · `admin` / `admin` |
| Para qué | Recibe los logs que envían los servicios con Serilog (GELF), los procesa y permite buscarlos. Depende de los dos siguientes. **Sin un input creado, descarta todo lo que le llega** |

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
curl -s -u admin:admin -H 'Accept: application/json' localhost:9000/api/system/inputs  # inputs de Graylog
curl -s localhost:8500/v1/catalog/services                           # servicios registrados en Consul
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
| 2026-11-19 | `features/flow6` | Capítulo 7: nacen `graylog` (recreado), `mongo_graylog` y `elasticserach_graylog` (reutilizados de `main`); `rabbitmq` reiniciado para cargar `order-queue`. 6 corriendo, 6 de `main` detenidos |
| 2026-11-19 | `features/flow5` | Capítulo 6: nace `consul` (`hashicorp/consul`), corriendo junto a `rabbitmq` y `vault`; se recreó sin el volumen heredado de `main`. 9 contenedores de `main` detenidos |
| 2026-11-18 | `features/flow4` | Capítulo 5: nace `vault` (`hashicorp/vault`), corriendo junto a `rabbitmq`; 10 contenedores de `main` detenidos |
| 2026-11-18 | `features/flow3` | Documento creado. Estado: `rabbitmq` corriendo (flow3); 11 contenedores de `main` detenidos. Capítulos 1–4 replicados |
