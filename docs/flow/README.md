# Flujo paso a paso

Scattered replica el curso de Distribt capítulo a capítulo para entender cómo evoluciona un sistema distribuido desde un esqueleto hasta la versión final (`main`). Cada capítulo vive en su propia rama `features/flowN` y tiene aquí su guía: qué cambió respecto al anterior, cómo ejecutarlo y un flujo end to end para verlo funcionar.

[← Volver al README principal](../../README.md)

## Contenido

[Capítulos replicados](#capítulos-replicados) · [Ramas de Scattered](#ramas-de-scattered) · [Ramas del curso](#ramas-del-curso) · [Cómo avanzar al siguiente capítulo](#cómo-avanzar-al-siguiente-capítulo)

## Capítulos replicados

<!-- chapters:start (generado por tools/docs/progress.py desde docs/flow/chapters.json; no editar a mano) -->
| # | Guía | Rama de Scattered | Rama original | Qué cambia |
|---|---|---|---|---|
| 1 | [Introducción](flow1.md) | `features/flow1` | [`01-Introduccion`](https://github.com/ElectNewt/Distribt/tree/01-Introduccion) | Esqueleto de APIs y microservicios con un arranque común |
| 2 | [Patrón API Gateway](flow2.md) | `features/flow2` | [`02-patron-api-gateway`](https://github.com/ElectNewt/Distribt/tree/02-patron-api-gateway) | Las APIs pública y privada enrutan hacia Orders y Products con YARP |
| 3–4 | [Producers/Consumers con RabbitMQ](flow3.md) | `features/flow3` | [`03-04-Producers-consumers-RabbitMQ`](https://github.com/ElectNewt/Distribt/tree/03-04-Producers-consumers-RabbitMQ) | Comunicación asíncrona: el gateway publica las suscripciones y un consumer las procesa |
| 5 | [Gestión de credenciales](flow4.md) | `features/flow4` | [`05-Gestion-credenciales`](https://github.com/ElectNewt/Distribt/tree/05-Gestion-credenciales) | Las credenciales de RabbitMQ salen de Vault: secreto KV para el consumer y usuario dinámico para el gateway |
| 6 | [Service discovery](flow5.md) | `features/flow5` | [`06-Service-discovery-registry`](https://github.com/ElectNewt/Distribt/tree/06-Service-discovery-registry) | Las direcciones de Vault y RabbitMQ salen de Consul; los microservicios se registran pero los gateways aún no lo usan |
| 7 | [Logs en la nube](flow6.md) | `features/flow6` | [`07-Logs-en-la-nube`](https://github.com/ElectNewt/Distribt/tree/07-Logs-en-la-nube) | Serilog y Graylog para centralizar logs (solo Orders los envía); Orders se arregla y estrena mensajes de dominio con su consumer |
| 8 | [CQRS](flow7.md) | `features/flow7` | [`08-CQRS`](https://github.com/ElectNewt/Distribt/tree/08-CQRS) | Products se divide en API de escritura (MySQL), API de lectura (MongoDB) y un consumer que sincroniza; handlers con Scrutor y primer test de API |
| 9 | [Event Sourcing](flow8.md) | `features/flow8` | [`09-Event-sourcing`](https://github.com/ElectNewt/Distribt/tree/09-Event-sourcing) | Orders guarda sus pedidos como eventos en MongoDB (event store) y reconstruye el estado al leer; nueva librería Shared.EventSourcing |
| 10 | [Consistencia eventual](flow9.md) | `features/flow9` | [`10-Consistencia-eventual`](https://github.com/ElectNewt/Distribt/tree/10-Consistencia-eventual) | Sin código nuevo (mismo commit que el 9): guía con mediciones de la consistencia eventual en Products, Orders y Subscriptions |

Siguiente: capítulo 11 · Respuestas de API unificadas (rama `features/flow10`, original [`11-unificar-respuestas-api`](https://github.com/ElectNewt/Distribt/tree/11-unificar-respuestas-api)).
<!-- chapters:end -->

Documentos transversales, que se actualizan con cada capítulo:

- [Contenedores de Docker por capítulo](contenedores.md): qué contenedor nace en cada capítulo, qué corre dentro y su estado actual.
- PDF de cada guía y de los contenedores en [`docs/pdf/`](../pdf/): `flowN.pdf` (qué cambió y qué no, archivo por archivo) y `contenedores.pdf`.

## Ramas de Scattered

| Rama | Para qué |
|---|---|
| `features/flowN` | Réplica de un capítulo. Nace de `features/flow(N-1)` |
| `features/integration-flow` | Va acumulando cada capítulo terminado (se mergea `features/flowN` al cerrarlo) |
| `main` | Versión final, equivalente al `main` del original migrado a .NET 10 |

## Ramas del curso

Ramas del proyecto original en orden. Cada una corresponde a un vídeo o artículo del curso.

| # | Rama original | Commit | Fecha | Qué añade |
|---|---|---|---|---|
| 1 | `01-Introduccion` | `051e361` | 2021-12-26 | Esqueleto de APIs y microservicios |
| 2 | `02-patron-api-gateway` | `fbd27a2` | 2022-01-07 | API Gateway con YARP |
| 3–4 | `03-04-Producers-consumers-RabbitMQ` | `0da6850` | 2022-01-19 | Productor/consumidor con RabbitMQ |
| 5 | `05-Gestion-credenciales` | `972c150` | 2022-01-31 | Secretos con Vault |
| 6 | `06-Service-discovery-registry` | `f596ee4` | 2022-02-07 | Service discovery con Consul |
| 7 | `07-Logs-en-la-nube` | `53d0830` | 2022-02-27 | Logs con Serilog y Graylog |
| 8 | `08-CQRS` | `53d17af` | 2022-03-19 | CQRS en Products |
| 9 | `09-Event-sourcing` | `66a0e62` | 2022-04-25 | Event Sourcing con MongoDB |
| 10 | `10-Consistencia-eventual` | `66a0e62` | 2022-04-25 | Mismo commit que la 09 |
| 11 | `11-unificar-respuestas-api` | `92de34f` | 2022-05-19 | Respuestas de API unificadas con ROP |
| 12 | `12-Saga` | `0e5e34a` | 2022-06-06 | Patrón SAGA |
| 13 | `13-Health-Checks` | `c46a5bb` | 2022-06-09 | Health checks |
| 14 | `14-monitorizacion-sistemas-distribuidos` | `14e02ef` | 2022-06-26 | OpenTelemetry, Prometheus, Grafana y Zipkin |
| 15 | `15-Api-key` | `21bbc57` | 2022-11-14 | API Key |
| 16 | `16-rate-limiting` | `4fd8e17` | 2022-11-24 | Rate limiting (y paso a .NET 7) |
| 17 | `17-Aspire-version2023` | `d44834d` | 2024-01-06 | .NET Aspire (rama aparte, no llegó a `main`) |
| — | `main` | `6be727b` | 2026-09-26 | Versión final en .NET 8 y `.slnx`; punto de partida de Scattered `main` |

El original tiene además ramas de experimentos (`ai-test/*`, `codex/*`, `copilot/*`, `GraphQL-Example`, etc.) que no forman parte del curso.

## Cómo avanzar al siguiente capítulo

### 1. Ver qué cambió en el original entre dos ramas

Desde una copia de Distribt (por ejemplo `~/Documents/workspace/NET/DistribtOrigin/Distribt`):

```bash
git fetch origin
git log --oneline origin/10-Consistencia-eventual..origin/11-unificar-respuestas-api
git diff --stat  origin/10-Consistencia-eventual origin/11-unificar-respuestas-api
git diff         origin/10-Consistencia-eventual origin/11-unificar-respuestas-api
```

### 2. Aplicar ese cambio en Scattered

Crea la rama del capítulo a partir de la anterior y aplica el diff del original. El diff nombra todo `Distribt`, así que hay que adaptarlo, y se excluyen el `README.md` y `assets/` porque en Scattered se escriben a mano:

```bash
cd ~/Documents/workspace/NET/Scattered
git switch -c features/flow10 features/flow9

cd ~/Documents/workspace/NET/DistribtOrigin/Distribt
git diff origin/10-Consistencia-eventual origin/11-unificar-respuestas-api -- . ':(exclude)README.md' ':(exclude)assets' \
  | sed 's/Distribt/Scattered/g; s/distribt/scattered/g' \
  > /tmp/11.patch

cd ~/Documents/workspace/NET/Scattered
git apply --check /tmp/11.patch   # comprobar que aplica limpio
git apply /tmp/11.patch
```

Si el parche no aplica limpio (porque ya se hizo algún ajuste propio), aplícalo con `git apply --3way` o a mano.

### 3. Verificar antes de pasar al siguiente

- `dotnet build Scattered.sln` sin errores. Todos los proyectos tienen `TreatWarningsAsErrors`, así que un paquete de 2022 con vulnerabilidades conocidas (aviso `NU1903`) rompe la compilación: sube a la versión parcheada más cercana y anótalo en la guía del capítulo (ver el caso de YARP en el [capítulo 2](flow2.md#particularidades)).
- Los servicios arrancan y las funcionalidades nuevas del capítulo responden: escribe su flujo end to end.
- A partir del capítulo 3 hace falta la infraestructura de `docker-compose.yaml` (RabbitMQ, y después Vault, Consul, Graylog…). Las imágenes de 2022 pueden comportarse distinto hoy (ver el usuario de RabbitMQ en el [capítulo 3–4](flow3.md#particularidades)); `docs/cambios/README.md` en `main` documenta los arreglos que hicieron falta para la versión final.
- El código del original a veces queda a medias entre capítulos. Si algo falla, compruébalo también en la rama original antes de corregirlo, y documéntalo como *problema heredado* (ver [capítulo 3–4](flow3.md#problemas-heredados-del-original)).

### 4. Documentar y cerrar el capítulo

1. Crea `docs/flow/flowN.md` con un bloque vacío `<!-- nav:start -->` / `<!-- nav:end -->` bajo la introducción y ejecuta `python3 tools/docs/progress.py`. Al existir la guía, el capítulo cuenta como replicado y el script actualiza:
   - la lista **Progreso del curso** del README principal (casilla, enlace a la guía, contador y `← esta rama` según la rama actual);
   - la tabla [Capítulos replicados](#capítulos-replicados) y la línea *Siguiente*;
   - la navegación (anterior · índice · siguiente) y la línea *Progreso* de cada guía, de modo que el enlace *Siguiente* de la guía anterior pasa de "(pendiente)" a enlazar la nueva.

   Los títulos, enlaces a artículos y resúmenes salen de [`chapters.json`](chapters.json); edítalos allí. `python3 tools/docs/progress.py --check` falla si algo está desactualizado.

   No hace falta ejecutarlo a mano si los hooks de `.githooks/` están activos (una vez por clon: `git config core.hooksPath .githooks`):
   - `pre-commit` regenera los bloques y añade al commit los archivos que cambien;
   - `post-merge` hace lo mismo tras un merge (el marcador `← esta rama` desaparece en `features/integration-flow`) y lo guarda en un commit aparte, *Update course progress after merge*.

   En GitHub, el workflow `.github/workflows/docs-progress.yml` ejecuta `--check` en cada push a `features/**` y en cada PR.
2. Actualiza el README principal según su sección *Cómo evoluciona este README*.
3. Si el capítulo toca `docker-compose.yaml`, actualiza [contenedores.md](contenedores.md#cómo-actualizar-este-documento). Regenera los PDF de `docs/pdf/`.
4. Haz commit en `features/flowN` y mergea en `features/integration-flow`.

### 5. Comparar con la versión final

Para ver cuánto falta para llegar a `main` de Scattered:

```bash
git diff -M --stat features/flow9 main
```

`-M` empareja archivos renombrados en lugar de mostrarlos como borrados y nuevos.
