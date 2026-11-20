# Capítulo 2 · Patrón API Gateway (`features/flow2`)

En el capítulo 1 cada microservicio era una isla y el cliente tenía que conocer el puerto de cada uno. En este capítulo las APIs pública y privada se convierten en **API Gateways** con [YARP](https://github.com/microsoft/reverse-proxy): el cliente solo habla con el gateway y este reenvía la petición al microservicio que corresponde.

<!-- nav:start (generado por tools/docs/progress.py desde docs/flow/chapters.json; no editar a mano) -->
[← Capítulo 1](flow1.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 3–4 →](flow3.md)

Progreso: [1](flow1.md) · **2** · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · 8 · 9 · 10 · 11 · 12 · 13 · 14 · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow2`)](#flujo-end-to-end-featuresflow2) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`02-patron-api-gateway`](https://github.com/ElectNewt/Distribt/tree/02-patron-api-gateway) |
| Commit de origen | `fbd27a2` Add api gateway pattern (7-ene-2022) |
| Diff respecto al capítulo 1 | 1 commit, 12 archivos, +93 / −12 líneas |
| Artículo | [Patrón API Gateway](https://www.netmentor.es/entrada/patron-api-gateway) |
| Cambios propios | `Yarp.ReverseProxy` 1.0.0 → **1.1.2** por vulnerabilidades (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff features/flow1 features/flow2 -- src`.

## Qué cambió

```text
src/
├── Api/Public/Scattered.API.Public
│   ├── Scattered.API.Public.csproj      + paquete Yarp.ReverseProxy
│   ├── Program.cs                       + AddReverseProxy() y MapReverseProxy()
│   └── appsettings.json                 + rutas /order-ms y /product-ms
├── Api/Private/Scattered.API.Private
│   ├── Scattered.API.Private.csproj     + paquete Yarp.ReverseProxy
│   ├── Program.cs                       + AddReverseProxy() y MapReverseProxy()
│   └── appsettings.json                 + ruta /reports
├── Services/*/Properties/launchSettings.json   puertos fijos 50X20 / 50X10
└── Shared/Scattered.Shared.Api/DefaultScatteredWebApplication.cs   + URLs en minúsculas
```

### 1. Los gateways usan por primera vez el punto de extensión de `Create`

En el capítulo 1, `DefaultScatteredWebApplication.Create` ya aceptaba un `Action<WebApplicationBuilder>` opcional, pero nadie lo usaba. Ahora los dos gateways lo usan para registrar YARP sin tocar el código compartido:

```diff
-WebApplication app = DefaultScatteredWebApplication.Create();
+WebApplication app = DefaultScatteredWebApplication.Create(webappBuilder =>
+{
+    webappBuilder.Services.AddReverseProxy()
+        .LoadFromConfig(webappBuilder.Configuration.GetSection("ReverseProxy"));
+});

 app.MapGet("/", () => "Hello World!");
+app.MapReverseProxy();

 DefaultScatteredWebApplication.Run(app);
```

- `AddReverseProxy().LoadFromConfig(...)` registra el proxy y lee las rutas de la sección `ReverseProxy` de `appsettings.json`. Cambiar o añadir rutas no requiere recompilar.
- `MapReverseProxy()` añade el proxy como un endpoint más. `GET /` sigue respondiendo `Hello World!` porque ninguna ruta del proxy coincide con `/`.

### 2. Las rutas viven en `appsettings.json`

La configuración de YARP tiene dos partes:

- **Routes:** qué peticiones acepta el gateway (`Match.Path`) y a qué cluster las manda.
- **Clusters:** a qué direcciones reales reenvía.

```json
"ReverseProxy": {
  "Routes": {
    "OrderRoute": {
      "ClusterId": "OrderCluster",
      "Match": { "Path": "order-ms/{**catch-all}" },
      "Transforms": [ { "PathPattern": "{**catch-all}" } ]
    }
  },
  "Clusters": {
    "OrderCluster": {
      "Destinations": {
        "OrderCluster/destination1": { "Address": "https://localhost:50220/" }
      }
    }
  }
}
```

`{**catch-all}` captura todo lo que va detrás del prefijo, y el transform `PathPattern` reescribe la ruta para quitar el prefijo antes de reenviar:

```text
GET https://localhost:7283/order-ms/order/123
          └─ Match "order-ms/{**catch-all}"   catch-all = "order/123"
          └─ PathPattern "{**catch-all}"      → /order/123
          └─ OrderCluster                     → GET https://localhost:50220/order/123
```

| Gateway | Ruta | Cluster | Destino |
|---|---|---|---|
| Público `:7283` | `/order-ms/{**catch-all}` | `OrderCluster` | Orders `https://localhost:50220` |
| Público `:7283` | `/product-ms/{**catch-all}` | `ProductCluster` | Products `https://localhost:50320` |
| Privado `:7022` | `/reports/{**catch-all}` | `OrderCluster` | Orders `https://localhost:50220` |

El privado es la "API para tenants" del diagrama. Por ahora solo expone `reports`, que también apunta a Orders; la API Key y el rate limiting llegan en los capítulos 15 y 16.

### 3. Puertos fijos para los microservicios

Los clusters necesitan direcciones estables, así que los puertos aleatorios que generó Visual Studio se sustituyen por un esquema: **`50X20` HTTPS / `50X10` HTTP**, donde `X` identifica al servicio.

| Servicio | X | Antes (capítulo 1) | Ahora |
|---|---|---|---|
| Emails | 1 | `7273` / `5180` | `50120` / `50110` |
| Orders | 2 | `7077` / `5261` | `50220` / `50210` |
| Products | 3 | `7185` / `5048` | `50320` / `50310` |
| Subscriptions | 4 | `7197` / `5142` | `50420` / `50410` |
| API Public | — | `7283` / `5187` | sin cambios |
| API Private | — | `7022` / `5032` | sin cambios |

Las direcciones de los clusters están escritas a mano en `appsettings.json`: si un servicio cambia de puerto, el gateway deja de encontrarlo. El capítulo 6 resuelve esto con service discovery (Consul).

### 4. URLs en minúsculas en el arranque común

```diff
 builder.Services.AddSwaggerGen();
+builder.Services.AddRouting(x => x.LowercaseUrls = true);
```

Afecta a las URLs que **genera** ASP.NET Core: Swagger muestra ahora `/order/{orderId}` en lugar de `/Order/{orderId}`. No cambia cómo se **resuelven**: el enrutado ya no distinguía mayúsculas, así que `/order-ms/Order/123` sigue funcionando. Al estar en `DefaultScatteredWebApplication`, se aplica a los 6 proyectos a la vez.

## Lo que no cambia

- **El código de los microservicios.** Los controllers siguen devolviendo datos fijos; el gateway solo cambia *cómo se llega* a ellos.
- **Subscriptions y Emails no se exponen por el gateway.** La suscripción llegará en el capítulo 3–4 como mensaje por RabbitMQ (no como llamada HTTP), y Emails es interno: lo usarán otros servicios.
- **No hay comunicación entre microservicios.** El gateway llama a un servicio; los servicios siguen sin llamarse entre sí.

## Cómo ejecutarla

Igual que el capítulo 1 (ver [flow1.md](flow1.md#cómo-ejecutarla)): .NET 10 SDK, certificado de desarrollo de confianza y `DOTNET_ROLL_FORWARD=Major`.

```bash
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Private/Scattered.API.Private
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Orders/Scattered.Services.Orders
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Products/Scattered.Services.Products/Scattered.Services.Products.csproj
```

Para este flujo bastan los dos gateways, Orders y Products. Emails y Subscriptions arrancan igual pero no son alcanzables desde el gateway. Para detenerlos todos: `pkill -f 'Scattered\.(API|Services)'`.

El gateway se conecta a los servicios por HTTPS, así que el certificado de desarrollo tiene que ser de confianza (`dotnet dev-certs https --trust`).

## Flujo end to end (`features/flow2`)

> **Rama:** `features/flow2`, equivalente a [`02-patron-api-gateway`](https://github.com/ElectNewt/Distribt/tree/02-patron-api-gateway) del original (`fbd27a2`). Las respuestas y los casos límite de esta sección son los de esta rama.

Es el mismo flujo del [capítulo 1](flow1.md#flujo-end-to-end-featuresflow1), pero ahora **el cliente solo conoce el puerto del gateway** (`7283` público, `7022` privado), no el de cada microservicio. Las respuestas son las mismas porque los controllers no cambiaron; lo nuevo es el camino.

### Arrancar con logs detallados

Para ver qué hace cada pieza (qué ruta de YARP coincide, a dónde reenvía y qué action ejecuta el microservicio) se sube el nivel de log de ASP.NET Core y de YARP solo para esta ejecución, con variables de entorno y sin tocar código. Los nombres llevan puntos, así que se pasan con `env`:

```bash
for p in src/Api/Public/Scattered.API.Public src/Api/Private/Scattered.API.Private \
         src/Services/Emails/Scattered.Services.Emails src/Services/Orders/Scattered.Services.Orders \
         src/Services/Products/Scattered.Services.Products/Scattered.Services.Products.csproj \
         src/Services/Subscriptions/Scattered.Services.Subscriptions; do
  n=$(basename ${p%.csproj})
  nohup env DOTNET_ROLL_FORWARD=Major \
    'Logging__LogLevel__Microsoft.AspNetCore=Information' 'Logging__LogLevel__Yarp=Information' \
    Logging__Console__FormatterName=simple Logging__Console__FormatterOptions__SingleLine=true \
    dotnet run --no-build --project $p > /tmp/$n.log 2>&1 &
done
```

Después de cada llamada, `tail /tmp/Scattered.API.Public.log` y `tail /tmp/Scattered.Services.Orders.log` muestran el lado del gateway y el del microservicio. Para detenerlos: `pkill -f 'Scattered\.(API|Services)'`.

### Las llamadas

```bash
# Gateway público
curl -k https://localhost:7283/
curl -k -X POST https://localhost:7283/product-ms/product -H 'Content-Type: application/json' \
  -d '{"productId":"11111111-1111-1111-1111-111111111111","productName":"Teclado","productDescription":"Mecánico"}'
curl -k https://localhost:7283/product-ms/product/11111111-1111-1111-1111-111111111111
OID=$(curl -sk -X POST https://localhost:7283/order-ms/order -H 'Content-Type: application/json' \
  -d '{"orderId":"00000000-0000-0000-0000-000000000000"}' | tr -d '"')
curl -k https://localhost:7283/order-ms/order/$OID

# Gateway privado (tenants)
curl -k https://localhost:7022/reports/order/$OID

# Subscriptions y Emails no tienen ruta: solo se llega directo
curl -k -X POST https://localhost:7283/subscription-ms/subscription -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
curl -k -X POST https://localhost:50420/subscription -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
curl -k -X POST https://localhost:50120/email -H 'Content-Type: application/json' \
  -d '{"from":"tienda@scattered.dev","to":"cliente@example.com","subject":"Pedido","body":"Gracias"}'
curl -k -X DELETE https://localhost:50420/subscription -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'

# Errores a través del gateway
curl -k https://localhost:7283/order-ms/order/no-es-guid
curl -k -X POST https://localhost:7283/product-ms/product -H 'Content-Type: application/json' -d '{"productName":"x"}'
curl -k https://localhost:7283/order-ms/no-existe
curl -s -o /dev/null -w '%{http_code} → %{redirect_url}\n' http://localhost:5187/order-ms/order/$OID

# Con Products parado
pkill -f 'Scattered\.Services\.Products'
curl -k https://localhost:7283/product-ms/product/11111111-1111-1111-1111-111111111111
```

### Qué ocurre en cada parte

| # | Llamada | Respuesta | Gateway | Microservicio |
|---|---|---|---|---|
| 1 | `GET :7283/` | `200 Hello World!` | Ejecuta `HTTP: GET /`; **no hace proxy** | — |
| 2 | `POST :7283/product-ms/product` | `200 true` | `ProductRoute` → `Proxying to https://localhost:50320/product` | Products: `ProductController.GetProduct(ProductDto)` |
| 3 | `GET :7283/product-ms/product/1111…` | `200 {…"TODO","TODO"}` | `ProductRoute` → `…:50320/product/1111…` | Products: `GetProduct(Guid)` |
| 4 | `POST :7283/order-ms/order` | `200 "7cf48a99-…"` | `OrderRoute` → `…:50220/order` | Orders: `AddOrder` |
| 5 | `GET :7283/order-ms/order/7cf48a99-…` | `200 {"orderId":…}` | `OrderRoute` → `…:50220/order/7cf48a99-…` | Orders: `GetOrder` |
| 6 | `GET :7022/reports/order/7cf48a99-…` | `200 {"orderId":…}` | Gateway **privado**, `ReportsRoute` → `…:50220/order/…` | Orders: `GetOrder` |
| 7 | `POST :7283/subscription-ms/subscription` | `404` | Ninguna ruta coincide; **no reenvía nada** | No llega |
| 8 | `POST :50420/subscription` (directo) | `200 true` | — | Subscriptions: `Subscribe` |
| 9 | `POST :50120/email` (directo) | `200 true` | — | Emails: `Send` |
| 10 | `DELETE :50420/subscription` (directo) | `200 true` | — | Subscriptions: `Unsubscribe` |
| 11 | `GET :7283/order-ms/order/no-es-guid` | `400` | Reenvía; recibe `response 400` | Orders: `BadRequestObjectResult` (validación) |
| 12 | `POST :7283/product-ms/product` con body incompleto | `400` | Reenvía; recibe `response 400` | Products: `ValidationProblemDetails` |
| 13 | `GET :7283/order-ms/no-existe` | `404` | **Sí** coincide `OrderRoute` y reenvía a `/no-existe` | Orders: *"reached the end of the middleware pipeline"* → 404 |
| 14 | `GET http://:5187/order-ms/order/…` | `307` → https `:7283` | Redirige **antes** del proxy | No llega |
| 15 | Cabeceras directo vs gateway | Idénticas | Transparente: mismo `content-type` y `server: Kestrel` | — |
| 16 | Products parado, `GET :7283/product-ms/product/…` | `502` | `Connection refused (localhost:50320)` | Caído |

### Qué enseña este flujo

- **El gateway no valida, solo enruta.** Los 400 de los pasos 11 y 12 los genera el `[ApiController]` del microservicio, y el gateway los devuelve tal cual.
- **Dos 404 distintos.** En el paso 7 el 404 lo da el **gateway** (ninguna ruta coincide) y la petición no llega a ningún servicio. En el 13 el gateway **sí** reenvía y el 404 lo da el **microservicio**. El cliente no ve la diferencia; los logs sí.
- **El prefijo desaparece.** El cliente pide `/order-ms/order/{id}` y Orders recibe `/order/{id}`: es el transform `PathPattern`, visible en la línea `Proxying to …`.
- **Gateway → servicio va por HTTP/2 sobre HTTPS** (`HTTP/2 RequestVersionOrLower`). Por eso el certificado de desarrollo tiene que ser de confianza.
- **Dos gateways, un mismo servicio.** El público y el privado llegan al mismo Orders por rutas distintas (pasos 5 y 6); Orders no sabe quién lo llamó.
- **Subscriptions y Emails solo funcionan directo** (pasos 8–10). Sigue sin haber comunicación entre servicios; eso empieza en el capítulo 3–4.
- **Peculiaridad heredada:** en Products la action del `POST` también se llama `GetProduct` (sobrecarga), así que el log muestra `GetProduct(ProductDto)` para una creación.

### Por dónde pasa una petición ahora

```text
curl ──HTTPS──▶ API Public (:7283)
                 │   DefaultScatteredWebApplication.Run(app)
                 ├─ UseHttpsRedirection
                 ├─ UseAuthorization
                 ├─ MapGet("/")             ← no coincide
                 └─ MapReverseProxy()       ← "order-ms/{**catch-all}" coincide
                      │  Transform PathPattern: /order-ms/order/{id} → /order/{id}
                      │  OrderCluster → https://localhost:50220
                      ▼
                Orders (:50220)  ── mismo pipeline que en el capítulo 1 ──▶
                OrderController.GetOrder(Guid orderId)
```

### Casos límite

- **Servicios sin ruta:** `POST :7283/subscription-ms/subscription` y `POST :7283/email-ms/email` → **404**. El gateway solo reenvía lo que está en `Routes`.
- **Cada gateway tiene sus rutas:** `GET :7283/reports/…` → **404** en el público, y `GET :7022/order-ms/…` → **404** en el privado.
- **Destino caído:** con Products parado, `GET :7283/product-ms/product/…` → **502 Bad Gateway**. El gateway está vivo pero no llega al servicio; antes, sin gateway, el cliente recibía un error de conexión.
- **Mayúsculas:** `GET :7283/order-ms/Order/…` → **200**. El enrutado no distingue mayúsculas; `LowercaseUrls` solo afecta a las URLs generadas (Swagger de Orders: `/order/{orderId}`, `/order`).
- **HTTP a HTTPS en el gateway:** `http://localhost:5187/order-ms/order/…` → **307** a `https://localhost:7283/…`, antes de llegar al proxy.

## Particularidades

- **YARP 1.1.2 en lugar de 1.0.0.** El original usa `Yarp.ReverseProxy` 1.0.0, que hoy tiene dos vulnerabilidades de denegación de servicio de gravedad alta: [CVE-2022-26924](https://github.com/advisories/GHSA-8xc6-g8xw-h2c4) (corregida en 1.0.1) y [CVE-2023-33141](https://github.com/advisories/GHSA-jrjw-qgr2-wfcg) (corregida en 1.1.2). Como todos los proyectos tienen `TreatWarningsAsErrors`, el aviso `NU1903` rompe la compilación. Se usa 1.1.2: la versión más cercana que corrige ambas, de la misma línea 1.x y compatible con .NET 6. La API usada (`AddReverseProxy`, `LoadFromConfig`, `MapReverseProxy`) es la misma.
- **El original deja espacios en blanco al final de línea** en el `appsettings.json` del gateway privado; `git apply` lo avisa, pero se conserva por fidelidad.
- **Error de Kestrel inofensivo en los logs.** El gateway público registra de vez en cuando `fail: Unexpected exception in TimingPipeFlusher.FlushAsync … Bad address`. El cliente siempre recibe la respuesta correcta (120 de 120 llamadas con 200). Solo pasa por HTTP/2 (2 de 40) y nunca por HTTP/1.1 (0 de 40): es Kestrel intentando escribir el cierre de la conexión TLS cuando `curl` ya la cerró. No viene del código ni de YARP; lo más probable es que sea efecto de ejecutar código .NET 6 sobre el runtime .NET 10 (`DOTNET_ROLL_FORWARD=Major`) en macOS.
- **Direcciones escritas a mano.** Los clusters apuntan a `https://localhost:50X20/`; si un servicio no arranca en su puerto, el gateway devuelve 502.
- Siguen vigentes las particularidades del [capítulo 1](flow1.md#particularidades) (.NET 6 sin soporte, `.csproj` huérfano de Products, `Scattered.sln`).
