# Capítulo 6 · Service discovery con Consul (`features/flow5`)

Hasta el capítulo 5 cada servicio sabía **dónde** estaba lo que necesitaba porque estaba escrito en su `appsettings.json`: `Bus:RabbitMQ:Hostname = localhost`, `SecretManager:VaultUrl = http://localhost:8200`. En este capítulo esas direcciones salen de la configuración y pasan a un **registro de servicios**, HashiCorp **Consul**. Cada proceso solo conoce la dirección de Consul y le pregunta por nombre: "¿dónde está `SecretManager`?", "¿dónde está `RabbitMQ`?". Si mañana Vault cambia de máquina o de puerto, se actualiza el registro y ningún servicio se recompila ni se reconfigura.

<!-- nav:start -->
[← Capítulo 5](flow4.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 7 →](flow6.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · **6** · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · 11 · 12 · 13 · 14 · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow5`)](#flujo-end-to-end-featuresflow5) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`06-Service-discovery-registry`](https://github.com/ElectNewt/Distribt/tree/06-Service-discovery-registry) |
| Commit de origen | `f596ee4` add service registry / discovery with consul (7-feb-2022) |
| Diff respecto al capítulo 5 | 1 commit, 31 archivos, +267 / −64 líneas |
| Artículo | [Service registry / discovery con Consul](https://www.netmentor.es/entrada/service-registry-discovery-consul) |
| Cambios propios | Imagen `hashicorp/consul`, `tools/consul/config.sh` registra por la API HTTP (la CLI actual rechaza el original) y corrige los puertos de Orders y Products (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff -M features/flow4 -- src tools docker-compose.yaml Scattered.sln`.

## Resumen

```text
                      ┌────────────── Consul (:8500, modo dev) ──────────────┐
                      │ RabbitMQ          → localhost                         │
                      │ SecretManager     → http://localhost:8200             │
                      │ EmailsApi · OrdersApi · ProductsApi · SubscriptionsApi│  (registrados, aún sin uso)
                      └──────▲──────────────────────▲────────────────────────┘
          1. ¿SecretManager? │ (al arrancar, todos)  │ 2. ¿RabbitMQ? (con las credenciales)
                             │                       │
        Los 7 procesos ──────┘   API Public · Subscriptions.Consumer
                                       │                      │
                                       ▼                      ▼
                              Vault (http://localhost:8200)   RabbitMQ (localhost:5672)
```

- **Todos los procesos** preguntan a Consul por `SecretManager` **al arrancar**, porque lo hace el arranque común. Con esa respuesta construyen la URL de Vault.
- **Los que usan el bus** (API Public y Subscriptions.Consumer) preguntan además por `RabbitMQ` en el mismo momento en que piden las credenciales a Vault: el consumer al arrancar y el gateway en su primera publicación.
- Los microservicios (`EmailsApi`, `OrdersApi`…) **se registran pero nadie los consulta todavía**: los gateways siguen con las direcciones de YARP escritas en `appsettings.json`.

El flujo de mensajes y las credenciales son los mismos del capítulo 5. Lo que cambia es **de dónde sale la dirección** de Vault y de RabbitMQ.

## Qué cambió

```text
docker-compose.yaml                          + servicio consul (agente en modo dev); vault depende de consul
tools/consul/config.sh                       NUEVO: registra 6 servicios en Consul
tools/local-development/up.sh                + ejecuta tools/consul/config.sh
Scattered.sln                                + Scattered.Shared.Discovery  − Scattered.Shared.Api (el .csproj sigue en disco)
src/
├── Api/Public/…/appsettings.json            − SecretManager:VaultUrl  + Discovery:Address
├── Api/Public/…/Program.cs                  − una línea en blanco
├── Api/Private/…/appsettings.json           + Discovery:Address
├── Api/Private/…/Program.cs                 − los dos using sin uso del capítulo 5
├── Services/Emails|Orders|Products|Subscriptions/…/appsettings.json   + Discovery:Address
├── Services/Subscriptions/…Consumer/appsettings.json                 − SecretManager:VaultUrl  + Discovery:Address
├── Services/Orders/…/OrderController.cs     − un using (ya llega por GlobalUsings)
└── Shared/
    ├── Scattered.Shared.Discovery           NUEVO: IServiceDiscovery + ConsulServiceDiscovery (paquete Consul)
    ├── Scattered.Shared.Api                 queda vacío: DefaultScatteredWebApplication se muda a Setup/API
    ├── Scattered.Shared.Setup
    │   ├── API/DefaultScatteredWebApplication.cs   MOVIDO; + AddServiceDiscovery() + AddSecretManager()
    │   ├── Services/ServiceBus.cs                  MOVIDO; + GetRabbitMQHostName() desde Consul
    │   ├── Services/SecretManager.cs               MOVIDO; ahora sí se usa: URL de Vault desde Consul
    │   ├── Services/ServiceDiscovery.cs            NUEVO: AddServiceDiscovery()
    │   └── GlobalUsings.cs                         + Setup.Services, Setup.API, Shared.Discovery
    ├── Scattered.Shared.Secrets             VaultSettings.UpdateUrl() + AddVaultService(…, discoveredUrl)
    └── Shared.Communication/…RabbitMQ       RabbitMQSettings.SetHostName/SetCredentials + AddRabbitMQ(…, hostNameFactory, …)
```

### 1. Infraestructura: Consul en modo desarrollo

**`docker-compose.yaml`**

```yaml
  consul:
    container_name: consul
    image: hashicorp/consul:latest     # el original usa consul, ya retirada
    ports:
      - 8500:8500 # this is the UI/API calls
      - 8400:8400
      - 8600:8600
      - 8600:8600/udp
  vault:
    …
    depends_on:
      - rabbitmq
      - consul
```

Sin `command`, la imagen arranca `consul agent -dev -client 0.0.0.0`: un único nodo que hace de servidor y de agente, escuchando en todas las interfaces para que se pueda llegar desde el host.

| Puerto | Para qué |
|---|---|
| `8500` | API HTTP y web. Es el único que usa Scattered (`Discovery:Address`) |
| `8600` TCP/UDP | DNS: `dig @127.0.0.1 -p 8600 secretmanager.service.consul SRV` devuelve `8200 http://localhost.`. Consul también sirve para resolver servicios por DNS, aunque Scattered no lo usa |
| `8400` | Puerto RPC de las versiones antiguas de Consul. Consul 2.1 no escucha en él; se mantiene por fidelidad |

**`tools/consul/config.sh`** (nuevo) registra seis servicios: dos de infraestructura, que el código ya consulta, y los cuatro microservicios, que todavía no consulta nadie.

| Nombre en Consul | Dirección | Puerto | Quién lo consulta |
|---|---|---|---|
| `RabbitMQ` | `localhost` | — | `ServiceBus.GetRabbitMQHostName()` |
| `SecretManager` | `http://localhost` | `8200` | `SecretManager.GetVaultUrl()` |
| `EmailsApi` | `http://localhost` | `50120` | nadie todavía |
| `OrdersApi` | `http://localhost` | `50220` | nadie todavía |
| `ProductsApi` | `http://localhost` | `50320` | nadie todavía |
| `SubscriptionsApi` | `http://localhost` | `50420` | nadie todavía |

Dos detalles importantes:

- **La dirección lleva el esquema** (`http://`) excepto la de RabbitMQ. El código concatena dirección y puerto tal cual, así que `SecretManager` resuelve a `http://localhost:8200`, que es una URL válida para VaultSharp, y `RabbitMQ` resuelve a `localhost`, que es lo que espera `ConnectionFactory.HostName`.
- **Es un registro manual, hecho desde fuera.** Los servicios no se registran a sí mismos al arrancar ni hay *health checks*: Consul sabe de ellos solo porque el script los dio de alta.

El original lo hace con la CLI (`consul services register -name=… -address=http://localhost -port=8200`); en Scattered se hace con la API HTTP del agente (ver [Particularidades](#particularidades)):

```bash
register() {
    docker exec consul curl -sf -X PUT http://localhost:8500/v1/agent/service/register -d "$1" \
        && echo "Registered service: …"
}
register '{"Name":"RabbitMQ","Address":"localhost"}'
register '{"Name":"SecretManager","Address":"http://localhost","Port":8200}'
…
```

**`tools/local-development/up.sh`** suma `./tools/consul/config.sh` después del de Vault.

### 2. `Scattered.Shared.Discovery`: el cliente de Consul

Proyecto nuevo con el paquete `Consul` 1.6.10.3 y `Microsoft.Extensions.Caching.Memory` 2.2.0.

| Archivo | Qué hace |
|---|---|
| `ConsulServiceDiscovery.cs` | `IServiceDiscovery.GetFullAddress(nombre)`: pregunta al catálogo de Consul y devuelve `ServiceAddress[:ServicePort]`. Guarda el resultado en una `MemoryCache` sin caducidad |
| `DiscoveryDependencyInjection.cs` | `AddDiscovery()`: registra `IConsulClient` apuntando a `Discovery:Address` e `IServiceDiscovery` como *singleton* |
| `DiscoveryServices.cs` | Constantes con los nombres: `RabbitMQ`, `SecretManager` y, en `Microservices`, `EmailsApi`, `OrdersAPi`, `ProductsAPi`, `SubscriptionsAPi` |

```csharp
public async Task<string> GetFullAddress(string serviceKey, CancellationToken cancellationToken = default)
{
    if (_cache.TryGetValue(serviceKey, out string serviceAddress))
        return serviceAddress;                                    // 2.ª vez y siguientes: memoria

    var services = await _client.Catalog.Service(serviceKey, cancellationToken);   // GET /v1/catalog/service/{name}
    if (services.Response != null && services.Response.Any())
    {
        var service = services.Response.First();                  // si hay varias instancias, siempre la primera
        // "http://localhost" + ":8200"  (el puerto solo si no es 0)
        …
        AddToCache(serviceKey, serviceAddressString);
        return serviceAddressString;
    }
    throw new ArgumentException($"seems like the service your are trying to access ({serviceKey}) does not exist ");
}
```

- **Catálogo, no *health*.** Usa `/v1/catalog/service`, que devuelve todas las instancias registradas estén sanas o no. Con `/v1/health/service?passing` solo devolvería las sanas, pero aquí no hay *health checks* que lo permitan.
- **Sin balanceo.** Con varias instancias registradas siempre se queda con la primera.
- **Caché sin caducidad.** Una vez resuelta, la dirección no se vuelve a pedir durante la vida del objeto.

### 3. El arranque común pasa a `Shared.Setup`

`DefaultScatteredWebApplication` se mueve de `Scattered.Shared.Api` a `Scattered.Shared.Setup/API/` (namespace `Scattered.Shared.Setup.API`) y cambia sus dos últimas líneas:

```diff
 builder.Services.AddSerializer();
-builder.Services.AddVaultService(builder.Configuration);
+
+builder.Services.AddServiceDiscovery(builder.Configuration);   // IConsulClient + IServiceDiscovery
+builder.Services.AddSecretManager(builder.Configuration);      // pregunta YA a Consul por SecretManager
```

El motivo de la mudanza es la dependencia: para resolver la URL de Vault, el arranque común necesita Discovery y Secrets a la vez, y `Setup` es el proyecto que ya junta todo. `Scattered.Shared.Api` se queda sin código y sale del `.sln`, pero su `.csproj` sigue en disco porque `Setup` lo referencia: a través de él llegan `Microsoft.AspNetCore.App` y Swashbuckle.

`ServiceBus.cs` y `SecretManager.cs` también se mueven, a `Setup/Services/` (namespace `Scattered.Shared.Setup.Services`), junto al nuevo `ServiceDiscovery.cs`. `GlobalUsings.cs` deja de importar `Scattered.Shared.Api` y suma `Setup.Services`, `Setup.API` y `Shared.Discovery`.

### 4. La URL de Vault sale de Consul

En el capítulo 5, `SecretManager.AddSecretManager()` existía pero no se usaba. Ahora es el punto de entrada:

```csharp
public static void AddSecretManager(this IServiceCollection serviceCollection, IConfiguration configuration)
{
    //TODO: create an awaiter project instead of .result everywhere in the config
    string discoveredUrl = GetVaultUrl(serviceCollection.BuildServiceProvider()).Result;   // ← Consul, en Create()
    serviceCollection.AddVaultService(configuration, discoveredUrl);
}
```

Y `Shared.Secrets` acepta esa URL:

```diff
-public static void AddVaultService(this IServiceCollection serviceCollection, IConfiguration configuration)
+public static void AddVaultService(this IServiceCollection serviceCollection, IConfiguration configuration, string? discoveredUrl = null)
 {
     serviceCollection.Configure<VaultSettings>(configuration.GetSection("SecretManager"));
+    serviceCollection.PostConfigure<VaultSettings>(settings =>
+    {
+        if(!string.IsNullOrWhiteSpace(discoveredUrl))
+            settings.UpdateUrl(discoveredUrl);
+    });
```

```diff
 public record VaultSettings
 {
-    public string? VaultUrl { get; init; }
+    public string? VaultUrl { get; private set; }
     public string? TokenApi { get; init; }
+
+    public void UpdateUrl(string url) => VaultUrl = url;
 }
```

A diferencia de RabbitMQ, la consulta **no es perezosa**: se hace con `.Result` dentro de `Create()`, antes de `builder.Build()`. Por eso **los 7 procesos necesitan Consul para arrancar**, incluidos Emails, Products u Orders, que no usan Vault para nada (paso 12 del [flujo](#flujo-end-to-end-featuresflow5)).

`AddVaultService` sigue funcionando sin Consul (el parámetro es opcional), pero como `VaultUrl` pasa a tener `private set`, el *binder* de configuración ya no la rellena desde `SecretManager:VaultUrl`. Fuera de `Setup`, la URL solo puede llegar con `UpdateUrl`.

### 5. El host de RabbitMQ sale de Consul

`AddRabbitMQ` recibe una segunda función, para el host:

```csharp
public static void AddRabbitMQ(this IServiceCollection serviceCollection,
    Func<IServiceProvider, Task<RabbitMQCredentials>> rabbitMqCredentialsFactory,
    Func<IServiceProvider, Task<string>> rabbitMqHostName,
    IConfiguration configuration)
{
    serviceCollection.AddRabbitMQ(configuration);
    serviceCollection.PostConfigure<RabbitMQSettings>(x =>
    {
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
        x.SetCredentials(rabbitMqCredentialsFactory.Invoke(serviceProvider).Result);   // Vault (cap. 5)
        x.SetHostName(rabbitMqHostName.Invoke(serviceProvider).Result);                // Consul (cap. 6)
    });
}
```

`ServiceBus` le pasa siempre la misma, para los cuatro tipos de conexión:

```csharp
private static async Task<string> GetRabbitMQHostName(IServiceProvider serviceProvider)
{
    var serviceDiscovery = serviceProvider.GetService<IServiceDiscovery>();
    return await serviceDiscovery?.GetFullAddress(DiscoveryServices.RabbitMQ)!;
}
```

`RabbitMQSettings` cambia `Hostname` y `Credentials` a `private set` con métodos `SetHostName` y `SetCredentials`. Aquí sí la consulta es perezosa: ocurre en el `PostConfigure`, la primera vez que alguien pide `IOptions<RabbitMQSettings>`.

| Proceso | Pregunta por `SecretManager` | Pregunta por `RabbitMQ` |
|---|---|---|
| API Public | Al arrancar | En la **primera publicación** (junto a las credenciales dinámicas) |
| Subscriptions.Consumer | Al arrancar | **Al arrancar** (el `HostedService` conecta enseguida) |
| API Private, Emails, Orders, Products, Subscriptions | Al arrancar | Nunca |

### 6. Configuración de los servicios

Los 7 `appsettings.json` suman la dirección de Consul:

```json
  "Discovery": {
    "Address": "http://localhost:8500"
  },
```

En **API Public** y **Subscriptions.Consumer** sustituye a la sección `SecretManager:VaultUrl`, que desaparece. `Bus:RabbitMQ:Hostname` sigue en ambos `appsettings.json`, pero ya **no se lee**: con `private set` el *binder* la ignora y el valor siempre llega de Consul (paso 8 del flujo lo demuestra).

### 7. Otros

- **API Private** quita los dos `using` sin uso que había sumado en el capítulo 5.
- **`OrderController`** quita `using …Publisher.Integration`, que ya llega por `GlobalUsings.cs`. Orders sigue sin registrar el publicador, así que sigue devolviendo 500.

## Lo que no cambia

- **Las rutas de YARP.** Los gateways siguen con `https://localhost:50220/` y `https://localhost:50320/` escritos en `appsettings.json`. Consul todavía no decide a dónde va una petición HTTP (paso 7).
- **Vault y sus credenciales**: mismo secreto KV para el consumer, mismo usuario dinámico para el gateway, mismos problemas (no se renuevan, modo dev).
- **La topología de RabbitMQ** y el flujo `POST /subscribe` → cola → consumer.
- **Orders sigue devolviendo 500** y **el consumer sigue abriendo una conexión por segundo** (problemas heredados del [capítulo 3–4](flow3.md#problemas-heredados-del-original)).

## Cómo ejecutarla

Ahora hacen falta RabbitMQ, Vault **y** Consul, y configurar Vault y Consul:

```bash
./tools/local-development/up.sh          # docker-compose up -d + tools/vault/config.sh + tools/consul/config.sh
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Subscriptions/Scattered.Services.Subscriptions.Consumer
```

`tools/consul/config.sh` ya no usa `docker exec -it` y funciona desde cualquier sitio. `tools/vault/config.sh` sigue necesitando una terminal interactiva; desde un script, usa `sed 's/docker exec -it /docker exec /' tools/vault/config.sh | bash` (ver [capítulo 5](flow4.md#3-configsh-usa-docker-exec--it)).

Products tiene dos `.csproj` en su carpeta (problema del [capítulo 1](flow1.md#particularidades)), así que hay que indicar cuál: `dotnet run --project src/Services/Products/Scattered.Services.Products/Scattered.Services.Products.csproj`.

| Qué | Dónde |
|---|---|
| UI de Consul | http://localhost:8500 → *Services*: los 6 registrados más `consul` |
| Catálogo por API | `curl localhost:8500/v1/catalog/services` · `curl localhost:8500/v1/catalog/service/SecretManager` |
| Por DNS | `dig @127.0.0.1 -p 8600 secretmanager.service.consul SRV` |
| UI de Vault | http://localhost:8200 (token `vault-scattered-token`) |
| UI de RabbitMQ | http://localhost:15672 (`ScatteredAdmin` / `ScatteredPass`) |

Para detener todo: `pkill -f 'Scattered\.(API|Services)'` y `docker compose stop`.

## Flujo end to end (`features/flow5`)

> **Rama:** `features/flow5`, equivalente a [`06-Service-discovery-registry`](https://github.com/ElectNewt/Distribt/tree/06-Service-discovery-registry) del original (`f596ee4`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-19 con RabbitMQ, Vault y Consul en Docker y los 7 procesos arrancados.

### Las llamadas

```bash
# 1. Registrar los servicios en Consul
./tools/consul/config.sh
curl -s localhost:8500/v1/catalog/service/SecretManager

# 2–6. Flujo normal: arrancar los 7 procesos y repetir las llamadas del capítulo 5
curl -k https://localhost:7283/
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"ana@example.com"}'
curl -k https://localhost:7283/product-ms/product/3fa85f64-5717-4562-b3fc-2c963f66afa6
curl -k https://localhost:7283/order-ms/order/3fa85f64-5717-4562-b3fc-2c963f66afa6
curl -k -X POST https://localhost:50120/email -H 'Content-Type: application/json' \
  -d '{"from":"tienda@example.com","to":"a@b.c","subject":"Hola","body":"Bienvenido"}'

# 7. Cambiar en Consul el puerto de OrdersApi: ¿lo nota el gateway?
docker exec consul curl -s -X PUT localhost:8500/v1/agent/service/register \
  -d '{"Name":"OrdersApi","Address":"http://localhost","Port":59999}'
curl -k https://localhost:7283/order-ms/order/3fa85f64-5717-4562-b3fc-2c963f66afa6

# 8–10. Cambiar en Consul RabbitMQ / SecretManager, o darlo de baja, y reiniciar el consumer
docker exec consul curl -s -X PUT localhost:8500/v1/agent/service/register -d '{"Name":"RabbitMQ","Address":"rabbitmq-inexistente"}'
docker exec consul curl -s -X PUT localhost:8500/v1/agent/service/register -d '{"Name":"SecretManager","Address":"http://localhost","Port":8201}'
docker exec consul curl -s -X PUT localhost:8500/v1/agent/service/deregister/RabbitMQ

# 11–13. Consul parado: procesos ya arrancados y procesos nuevos; Consul de vuelta
docker stop consul
docker start consul
```

### Qué ocurre en cada parte

| # | Acción | Respuesta | Gateway (API Public) | Consumer | Consul | Vault / RabbitMQ |
|---|---|---|---|---|---|---|
| 1 | `config.sh` | 6 × `Registered service` | — | — | `RabbitMQ → localhost`, `SecretManager → http://localhost:8200` y los 4 microservicios | — |
| 2 | Arrancar los 7 procesos | 7/7 escuchando | Pide `SecretManager` a Consul al arrancar | Pide `SecretManager` y `RabbitMQ` al arrancar; lee el KV de Vault y conecta | Responde a cada proceso (`SecretManager`) y al consumer (`RabbitMQ`) | El consumer conecta como `ScatteredAdmin` |
| 3 | `GET /` en los dos gateways | `200` `Hello World!` | — | — | — | — |
| 4 | `POST /subscribe` × 2 | `200` (415 ms y 37 ms) | 1.ª publicación: `RabbitMQ` a Consul + credenciales dinámicas a Vault. La 2.ª lo reutiliza todo | `Email cliente@… / ana@… successfully subscribed.` | — | Nuevo usuario `token-d6b6561f-…` |
| 5 | Products, Emails, Subscriptions (directos o por gateway) | `200` | Igual que en el capítulo 5 | — | — | — |
| 6 | Orders por los dos gateways | **`500`** | `IIntegrationMessagePublisher` sin registrar (heredado) | — | — | — |
| 7 | `OrdersApi` → puerto `59999` en Consul + `GET /order-ms/…` | `500` (el mismo error de Orders) | **Sigue llamando a `:50220`**: YARP no consulta Consul | — | Registro cambiado | — |
| 8 | `RabbitMQ` → `rabbitmq-inexistente` + reiniciar consumer + `/subscribe` | `200` | Publica con su host en caché | **Arranca y escucha en `:7073`, pero no consume y no escribe ningún error** | — | La cola acumula el mensaje: `1` mensaje, `0` consumidores |
| 9 | `SecretManager` → puerto `8201` + reiniciar consumer | — | — | **Muere al arrancar**: `Connection refused (localhost:8201)` | — | Vault sigue en `:8200`, pero el consumer va a donde dice Consul |
| 10 | Dar de baja `RabbitMQ` + reiniciar consumer | — | — | **Muere al arrancar**: `ArgumentException: seems like the service your are trying to access (RabbitMQ) does not exist` | Sin `RabbitMQ` | — |
| 11 | Restaurar los registros + reiniciar consumer | — | — | Arranca, consume y procesa el mensaje que esperaba del paso 8 | Restaurado | `subscription-queue`: `0` mensajes |
| 12 | `docker stop consul` + `/subscribe` y Products | `200` · `200` | Sigue funcionando: Vault y RabbitMQ ya estaban resueltos | Procesa `sin-consul@…` | Caído | — |
| 13 | Reiniciar Emails con Consul parado | — | — | — | Caído | **Emails muere al arrancar**: `Connection refused (localhost:8500)`, aunque no usa ni Vault ni RabbitMQ |
| 14 | `docker start consul` | — | — | — | **Conserva los 6 registros**: el agente los guarda en `/consul/data` | Emails vuelve a arrancar |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| Las direcciones de Vault y RabbitMQ ya no están en `appsettings.json` | Pasos 8 y 9 | Cambiar un registro en Consul cambia a dónde se conecta un proceso, sin tocar su configuración |
| `Bus:RabbitMQ:Hostname` sigue en `appsettings.json` pero no se usa | Paso 8 | El valor de Consul gana siempre; la configuración que queda en el archivo engaña |
| **Consul es imprescindible para arrancar**, para todos | Pasos 9, 10 y 13 | Hasta Emails, que no usa ni Vault ni RabbitMQ, muere sin Consul porque la consulta está en el arranque común |
| Una vez arrancado, un proceso **no vuelve a preguntar** | Pasos 7, 8 y 12 | Las direcciones se resuelven una vez y se quedan en memoria: sobrevive a una caída de Consul, pero no se entera de un cambio |
| Un host de RabbitMQ incorrecto deja el consumer **vivo pero inútil** | Paso 8 | Es la "muerte silenciosa" del capítulo 3–4: no hay error en el log y el puerto `:7073` responde |
| Los microservicios están registrados pero **YARP no usa Consul** | Paso 7 | El *service discovery* llega a la infraestructura, no al tráfico HTTP entre gateway y servicios |
| Consul conserva su registro entre reinicios | Paso 14 | Al revés que Vault en modo dev: no hace falta repetir `config.sh` tras `docker restart consul`, pero sí tras recrear el contenedor sin su volumen |

## Problemas heredados del original

### 1. Consul es un punto único de fallo al arrancar

`AddSecretManager()` llama a Consul con `.Result` dentro de `DefaultScatteredWebApplication.Create()`, así que ningún proceso arranca si Consul no responde, aunque no use Vault. No hay reintentos ni valor por defecto, y la excepción es un `AggregateException` envuelto en `Unhandled exception`.

### 2. Las direcciones se resuelven una sola vez

La `MemoryCache` no caduca y el resultado acaba guardado dentro de `IOptions<…>`, que tampoco se refresca. Si un servicio cambia de dirección hay que reiniciar a sus clientes, que es justo lo que el *service discovery* pretende evitar. Es el mismo patrón que las credenciales dinámicas del [capítulo 5](flow4.md#1-las-credenciales-dinámicas-nunca-se-renuevan).

### 3. Registro manual y sin *health checks*

Los servicios no se registran a sí mismos, no hay comprobaciones de salud y se consulta el catálogo en vez de `/v1/health/service`. Consul devuelve una dirección aunque el servicio esté caído, y con varias instancias se usa siempre la primera.

### 4. `config.sh` del original no funciona con Consul actual

`consul services register -address=http://localhost -port=8200` falla con *"Invalid Service address when using CLI flags. Use -port flag instead: address should not contain port"*: la CLI actual interpreta `http://localhost` como `host:puerto`. Solo se registraría `RabbitMQ`, y el script terminaría con código `0`. Además usa `docker exec -it`, con el mismo problema fuera de una terminal que el de Vault. Scattered lo reescribe con la API HTTP (ver [Particularidades](#particularidades)).

### 5. Otros detalles

- **Puertos de Orders y Products cruzados** en el `config.sh` original: `ProductsApi → 50220` (que es Orders) y `OrdersApi → 50320` (que es Products). No tiene efecto todavía porque nadie consulta esos nombres. Corregido en Scattered.
- **Mayúsculas inconsistentes** en `DiscoveryServices.Microservices`: `OrdersAPi`, `ProductsAPi`, `SubscriptionsAPi`, frente a `OrdersApi` en el registro. Funciona porque Consul compara nombres sin distinguir mayúsculas; se mantiene como en el original.
- **Más `BuildServiceProvider()` y `.Result`.** Ahora hay uno en `AddSecretManager()` y otro en cada `PostConfigure` de RabbitMQ. Cada uno crea su propio `ConsulServiceDiscovery` con su propia caché, así que la caché casi nunca se reutiliza. El propio código lo reconoce: `//TODO: create an awaiter project instead of .result everywhere in the config`.
- **`Scattered.Shared.Api` queda vacío**, fuera del `.sln` pero referenciado por `Setup` (de él vienen Swashbuckle y el *framework* de ASP.NET Core) y con una referencia nueva a `Discovery` que no usa.
- **Paquetes de 2022 con vulnerabilidades transitivas.** `Consul` 1.6.10.3 arrastra `Newtonsoft.Json` 10.0.3, `System.Net.Http` 4.3.0 y `System.Text.RegularExpressions` 4.3.0, todos con avisos de gravedad alta (`dotnet list package --vulnerable --include-transitive`). No rompen la compilación porque en `net6.0` NuGet solo audita las dependencias directas.
- Siguen los problemas de los capítulos [3–4](flow3.md#problemas-heredados-del-original) y [5](flow4.md#problemas-heredados-del-original).

## Particularidades

- **Imagen `hashicorp/consul` (cambio propio).** El original usa `consul`, la imagen *oficial* de Docker Hub que HashiCorp dejó de publicar, igual que `vault`. `hashicorp/consul:latest` es la misma aplicación y la que usa `main`. Hoy es Consul 2.1.0; en febrero de 2022 era la 1.11. Arranca igual (`agent -dev -client 0.0.0.0`).
- **`tools/consul/config.sh` con la API HTTP (cambio propio).** Ver [problema 4](#4-configsh-del-original-no-funciona-con-consul-actual). Registra lo mismo, con el mismo nombre, dirección y puerto (salvo la corrección de Orders y Products), y sin `-it`. Es idempotente: repetirlo actualiza los registros en vez de duplicarlos.
- **El contenedor `consul` se comparte con `main`.** `docker compose up -d` lo **recrea** con la configuración de esta rama, pero conserva su volumen anónimo `/consul/data`, y con él los registros de `main` (`Graylog`, `MongoDb`, `ProductsApiRead`, `OrdersApi` en `60220`…). Para empezar limpio: `docker rm -fv consul && docker compose up -d consul && ./tools/consul/config.sh`.
- **Vault no se recrea.** Añadir `consul` a su `depends_on` no cambia la configuración del contenedor `vault`, así que `docker compose up -d` lo deja corriendo con sus secretos.
- Siguen vigentes las particularidades de los capítulos [1](flow1.md#particularidades), [2](flow2.md#particularidades), [3–4](flow3.md#particularidades) y [5](flow4.md#particularidades).
