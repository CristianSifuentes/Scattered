# Capítulo 10 · Consistencia eventual (`features/flow9`)

Este capítulo **no trae código nuevo**: en el original, la rama `10-Consistencia-eventual` apunta exactamente al mismo commit que `09-Event-sourcing` (`66a0e62`). El artículo del curso explica un concepto que el sistema ya practica desde el capítulo 3–4 y que CQRS (capítulo 8) y Event Sourcing (capítulo 9) hacen evidente: **la consistencia eventual**. Esta guía lo explica con mediciones reales sobre el código de `features/flow8`, que es idéntico al de esta rama.

<!-- nav:start -->
[← Capítulo 9](flow8.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 11 →](flow10.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · **10** · [11](flow10.md) · [12](flow11.md) · [13](flow12.md) · [14](flow13.md) · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Qué es la consistencia eventual](#qué-es-la-consistencia-eventual) · [Dónde aparece en Scattered](#dónde-aparece-en-scattered) · [Mediciones](#mediciones-featuresflow9) · [Lo que el código no resuelve todavía](#lo-que-el-código-no-resuelve-todavía) · [Cómo reproducirlo](#cómo-reproducirlo) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`10-Consistencia-eventual`](https://github.com/ElectNewt/Distribt/tree/10-Consistencia-eventual) |
| Commit de origen | `66a0e62` Add Event Sourcing (25-abr-2022), **el mismo que `09-Event-sourcing`** |
| Diff respecto al capítulo 9 | Ninguno: 0 archivos |
| Artículo | [Consistencia eventual en microservicios](https://www.netmentor.es/entrada/consistencia-eventual-microservicios) |
| Cambios en Scattered | Solo documentación: esta guía, el README, el diagrama y los PDF. El código de `src/` y `tools/` es el de `features/flow8` |

Para comprobarlo: `git diff features/flow8 features/flow9 -- src tools docker-compose.yaml Scattered.sln` no muestra nada.

## Qué es la consistencia eventual

En un sistema con una sola base de datos, una escritura confirmada se ve inmediatamente en cualquier lectura: es **consistencia fuerte**. Cuando los datos están repartidos entre servicios y bases que se comunican por mensajes, eso deja de ser cierto: durante un instante, una parte del sistema ya sabe algo que otra todavía no. **Consistencia eventual** significa que, si no llegan más cambios, todas las partes acaban viendo lo mismo, pero no al mismo tiempo.

No es un fallo, es una decisión. El [teorema CAP](https://es.wikipedia.org/wiki/Teorema_CAP) dice que un sistema distribuido, ante un fallo de red, debe elegir entre estar disponible o ser consistente. Scattered elige la **disponibilidad**: la escritura responde aunque el resto del sistema esté caído, y los datos se ponen al día después.

| | Consistencia fuerte | Consistencia eventual |
|---|---|---|
| Lectura justo después de escribir | Siempre ve el dato nuevo | Puede ver el dato antiguo durante un tiempo |
| Si un componente está caído | La escritura falla o espera | La escritura funciona; el componente se pone al día al volver |
| Cómo se consigue | Transacción en una sola base | Mensajes entre servicios (colas, eventos) |
| Coste | Acoplamiento y menos disponibilidad | Hay que diseñar para el "todavía no" |

## Dónde aparece en Scattered

| Flujo | Escritura (lo que confirma la respuesta) | Lo que llega después | Ventana de inconsistencia |
|---|---|---|---|
| Suscripción (cap. 3–4) | `POST /subscribe` → **200** cuando RabbitMQ tiene el mensaje | Subscriptions.Consumer procesa la suscripción | Desde el 200 hasta que el consumer la procesa |
| Pedidos (cap. 7 y 9) | `POST /order/create` → **202**, con el pedido ya guardado en el *event store* | Orders.Consumer recibe `OrderResponse` (`Order … created`) | El pedido existe y se puede consultar; los avisos van por detrás |
| Products con CQRS (cap. 8) | `PUT …/updateproductdetails/{id}` → **200** con MySQL actualizado | Products.Consumer actualiza MongoDB y publica la integración | **La API de lectura devuelve el dato antiguo** hasta que el consumer termina |

El código lo dice de varias formas:

- **`202 Accepted`** en Orders: "lo he recibido, se procesará", no "está hecho".
- **`Location`** en las respuestas de creación: dónde consultar el resultado cuando esté listo.
- **Dos bases para Products**: el modelo de lectura solo se actualiza a través de `product-domain-queue`.
- **El event store como fuente de verdad** en Orders: el estado del pedido es consistente al instante dentro de Orders (se reconstruye de sus eventos), y lo eventual son sus efectos en otros procesos.

## Mediciones (`features/flow9`)

> Ejecutado el 2026-11-19 con los 8 contenedores y los 10 procesos recién arrancados. El código es el de `features/flow8` ([capítulo 9](flow8.md)).

### 1. ¿Cuánto tarda la lectura de Products en ver una escritura?

Un script hace `PUT` del producto 2 con un nombre nuevo y, sin esperar, consulta `GET :50321/product/2` en bucle hasta que aparece el cambio. Diez rondas:

| Ronda | `PUT` responde en | La lectura ve el cambio a los | Lecturas con el dato antiguo |
|---|---|---|---|
| 1 | 126 ms | 1023 ms | 31 |
| 2 | 119 ms | 1993 ms | 71 |
| 3 | 47 ms | 3003 ms | 132 |
| 4 | 68 ms | 3008 ms | 116 |
| 5 | 55 ms | 3002 ms | 106 |
| 6 | 52 ms | 3019 ms | 111 |
| 7 | 65 ms | 1090 ms | 40 |
| 8 | 39 ms | 57 ms | 0 |
| 9 | 32 ms | 61 ms | 0 |
| 10 | 87 ms | 108 ms | 0 |

- La escritura responde en **~60 ms** (mediana). La lectura tarda **~1,5 s** en reflejarla (mediana), y hasta **3 s**.
- En **7 de 10** rondas, un cliente que lee justo después de escribir **ve el dato antiguo** (hasta 132 lecturas seguidas).
- Los tiempos caen en escalones de ~1, ~2 o ~3 segundos, o casi inmediatos. No está comprobada la causa; coincide con que el consumer crea un consumidor nuevo en cada vuelta de su bucle (la fuga del capítulo 3–4), y RabbitMQ reparte los mensajes entre todos ellos.

### 2. Products con el consumer parado

| Momento | MySQL (escritura) | API de lectura (MongoDB) | `product-domain-queue` |
|---|---|---|---|
| `PUT` con Products.Consumer parado → **200** en 146 ms | `Segundo producto (sin consumer)` | `Segundo producto r10` (antiguo) | El mensaje espera |
| 10 s después | nuevo | antiguo | espera |
| 30 s después | nuevo | antiguo | espera |
| Arrancar Products.Consumer | nuevo | **nuevo, 2,9 s después** (incluye el arranque) | vacía (salvo el mensaje envenenado del capítulo 8) |

La escritura **no depende** del consumer: el sistema sigue disponible y converge cuando el consumer vuelve. Mientras tanto, **dos APIs del mismo servicio dan respuestas distintas** para el mismo producto.

### 3. Orders y Subscriptions con sus consumers parados

| Paso | Resultado |
|---|---|
| `POST /order-ms/order/create` | **202** `20385b32-…` |
| `GET /order-ms/order/{id}` inmediato | `orderStatus: "Created"`: el pedido ya existe, porque el event store es la fuente de verdad |
| `POST /subscribe` | **200** |
| Colas | `order-queue`: 1 mensaje · `subscription-queue`: 1 mensaje |
| Arrancar los dos consumers | Ambos mensajes procesados **1,1 s** después: `Order 20385b32-… created` y `Email esperando@… successfully subscribed.` |

Aquí el cliente no nota la inconsistencia: lo que lee (`GET`) ya es correcto. Lo eventual son los efectos en otros procesos.

### 4. La fuga de conexiones empeora la ventana

Antes de estas mediciones, con los procesos llevando unos 30 minutos arrancados, RabbitMQ tenía **1346 conexiones** abiertas y un `PUT` tardaba **2,6 s** (un `GET`, 1,9 s). Tras reiniciar los procesos bajaron a 63 conexiones y el `PUT` a ~60 ms. La fuga heredada del capítulo 3–4 no solo consume recursos: alarga todo, también la convergencia.

## Lo que el código no resuelve todavía

La consistencia eventual solo es aceptable si el sistema **acaba** convergiendo y el cliente sabe que puede ver datos antiguos. En esta rama:

| Problema | Consecuencia | Dónde se vio |
|---|---|---|
| No hay handler de `ProductCreated` y el de `ProductUpdated` falla si el documento no existe | Un producto creado **nunca** converge a la vista de lectura | [Capítulo 8](flow7.md#1-la-cqrs-no-cubre-la-creación) |
| El receptor no hace *nack* ni manda a la *dead-letter* | Un mensaje que falla se queda atascado y se reintenta para siempre | [Capítulo 8](flow7.md#2-mensajes-envenenados-sin-salida); sigue en `product-domain-queue` |
| `getorderstatus/{id}` lanza `NotImplementedException` | El `Location` del 202 no sirve para consultar el progreso | [Capítulo 9](flow8.md#4-otros-detalles) |
| No hay versión ni fecha en las respuestas de lectura | El cliente no puede saber si lo que lee está al día | Mediciones 1 y 2 |
| Publicar y guardar no son atómicos | Si Orders guarda el evento pero falla al publicar, el pedido existe y nadie se entera (sin *outbox*) | Código de `CreateOrderService` |

El curso aborda parte de esto más adelante: respuestas unificadas (capítulo 11) y el patrón SAGA para operaciones que abarcan varios servicios (capítulo 12).

## Cómo reproducirlo

Con todo arrancado (ver [capítulo 9](flow8.md#cómo-ejecutarla) y [capítulo 8](flow7.md#cómo-ejecutarla)):

```bash
# Ventana de Products: escribir y leer enseguida
curl -k -X PUT https://localhost:50320/product/updateproductdetails/2 -H 'Content-Type: application/json' \
  -d '{"name":"Nombre nuevo","description":"x"}' && \
for i in $(seq 1 20); do curl -sk https://localhost:50321/product/2 | grep -o '"name":"[^"]*"'; sleep 0.2; done

# Products con el consumer parado: la lectura no cambia hasta arrancarlo
pkill -f Scattered.Services.Products.Consumer
curl -k -X PUT https://localhost:50320/product/updateproductdetails/2 -H 'Content-Type: application/json' \
  -d '{"name":"Sin consumer","description":"x"}'
curl -k https://localhost:50321/product/2                          # nombre antiguo
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Products/Scattered.Services.Products.Consumer &
sleep 5; curl -k https://localhost:50321/product/2                 # nombre nuevo
```

Las colas y sus mensajes pendientes se ven en http://localhost:15672 → *Queues*.

## Particularidades

- **Rama solo de documentación.** `features/flow9` nace de `features/flow8` y no cambia nada en `src/`, `tools/`, `docker-compose.yaml` ni `Scattered.sln`, igual que el original. Existe para mantener una rama por capítulo del curso.
- **Las cifras dependen de la máquina** (Mac con Apple Silicon, Docker Desktop, Graylog emulado) y de cuánto tiempo lleven arrancados los consumers, por la fuga de conexiones.
- **El producto 2 de las mediciones** se usa porque ya existe en MongoDB; con uno creado por la API la lectura no converge nunca.
- Siguen vigentes las particularidades de los capítulos anteriores.
