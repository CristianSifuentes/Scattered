# Capítulo 5 · Gestión de credenciales con Vault (`features/flow4`)

En el capítulo 3–4 el usuario y la contraseña de RabbitMQ estaban escritos en el `appsettings.json` de cada servicio. En este capítulo salen del código: los servicios se las piden a **HashiCorp Vault** al arrancar, y solo necesitan saber dónde está Vault y un token para hablar con él. Vault guarda los secretos de dos formas, y el capítulo usa las dos: un **secreto estático** (clave/valor) para el consumer y **credenciales dinámicas**, un usuario de RabbitMQ creado a demanda con caducidad, para el gateway.

<!-- nav:start -->
[← Capítulo 3–4](flow3.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 6 →](flow5.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · **5** · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · 12 · 13 · 14 · 15 · 16 · 17
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow4`)](#flujo-end-to-end-featuresflow4) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`05-Gestion-credenciales`](https://github.com/ElectNewt/Distribt/tree/05-Gestion-credenciales) |
| Commit de origen | `972c150` add vault service (31-ene-2022) |
| Diff respecto al capítulo 3–4 | 1 commit, 31 archivos, +251 / −33 líneas |
| Artículo | [Gestión de credenciales con Vault](https://www.netmentor.es/entrada/gestion-credenciales-vault) |
| Cambios propios | Imagen `hashicorp/vault`, carpeta `tools/local-development` bien escrita y scripts ejecutables (ver [Particularidades](#particularidades)) |

Para ver el diff completo en Scattered: `git diff features/flow3 -- src tools docker-compose.yaml Scattered.sln`.

## Resumen

```text
                         ┌──────────── Vault (:8200, modo dev) ────────────┐
                         │ secret/rabbitmq      KV: ScatteredAdmin / …       │
                         │ rabbitmq/ engine     crea usuarios bajo demanda   │
                         │   └─ roles/scattered-role  (write + read)        │
                         └────────▲───────────────────────────▲────────────┘
            VAULT-TOKEN + GetRabbitMQCredentials   VAULT-TOKEN + Get<RabbitMQCredentials>("rabbitmq")
                 (credenciales dinámicas)                (secreto estático)
                                  │                           │
Cliente ──POST /subscribe──▶ API Public (:7283)        Subscriptions.Consumer (:7073)
                                  │ usuario token-xxxx         │ usuario ScatteredAdmin
                                  ▼                           ▲
                         ┌──────────── RabbitMQ (:5672) ───────┴─────────┐
                         │ api.public.exchange → … → subscription-queue  │
                         └───────────────────────────────────────────────┘
```

El flujo de mensajes es el mismo del capítulo 3–4. Lo que cambia es **de dónde salen las credenciales** con las que cada proceso se conecta a RabbitMQ.

## Qué cambió

```text
docker-compose.yaml                         + servicio vault (modo dev, token raíz fijo)
tools/vault/config.sh                       NUEVO: carga el secreto KV y configura el motor rabbitmq
tools/local-development/up.sh               NUEVO: docker-compose up + config.sh
Scattered.sln                               + Scattered.Shared.Secrets
src/
├── Api/Public/Scattered.API.Public
│   ├── appsettings.json                    − Username/Password  + SecretManager:VaultUrl
│   ├── Properties/launchSettings.json      + VAULT-TOKEN
│   └── Program.cs                          − un using (pasa a GlobalUsings)
├── Api/Private/…/Program.cs                + dos using sin uso
├── Services/Subscriptions/…Consumer
│   ├── appsettings.json                    − Username/Password  + SecretManager:VaultUrl
│   ├── Properties/launchSettings.json      + VAULT-TOKEN
│   ├── .csproj                             + enlaza GlobalUsings.cs de Setup
│   └── Program.cs, Handler/*, Controllers/* − using que pasan a GlobalUsings
└── Shared/
    ├── Scattered.Shared.Secrets            NUEVO: ISecretManager + VaultSecretManager (VaultSharp)
    ├── Scattered.Shared.Api                + AddVaultService() en el arranque común
    ├── Scattered.Shared.Setup              ServiceBus.cs pide credenciales a Vault; + SecretManager.cs (sin uso)
    └── Shared.Communication/…RabbitMQ      RabbitMQSettings.Credentials + AddRabbitMQ(factory, …)
```

### 1. Infraestructura: Vault en modo desarrollo

**`docker-compose.yaml`**

```yaml
  vault:
    image: hashicorp/vault:latest          # el original usa vault:latest, ya retirada
    container_name: vault
    ports:
      - 8200:8200
    environment:
      VAULT_ADDR: "http://127.0.0.1:8200"
      VAULT_TOKEN: "vault-scattered-token"            # para usar la CLI dentro del contenedor
      VAULT_DEV_ROOT_TOKEN_ID: "vault-scattered-token" # token raíz del modo dev
    cap_add:
      - IPC_LOCK
    depends_on:
      - rabbitmq
```

Sin `command`, la imagen arranca `vault server -dev`: un solo nodo, **almacenamiento en memoria**, ya desbloqueado y con el token raíz que se indica. Es cómodo para desarrollo, pero todo lo que se guarde se pierde al reiniciar el contenedor (ver paso 11 del [flujo](#flujo-end-to-end-featuresflow4)). `IPC_LOCK` permite a Vault bloquear memoria para que los secretos no acaben en swap.

**`tools/vault/config.sh`** (nuevo) prepara Vault en dos partes, una por cada forma de guardar secretos:

```bash
# 1. Secreto estático (motor KV v2, montado por defecto en secret/)
docker exec -it vault vault kv put secret/rabbitmq username=ScatteredAdmin password=ScatteredPass

# 2. Credenciales dinámicas (motor rabbitmq)
docker exec -it vault vault secrets enable rabbitmq
docker exec -it vault vault write rabbitmq/config/connection \
    connection_uri="http://rabbitmq:15672" username="ScatteredAdmin" password="ScatteredPass"
docker exec -it vault vault write rabbitmq/roles/scattered-role \
    vhosts='{"/":{"write": ".*", "read": ".*"}}'
```

En la parte 2, Vault usa la **API de gestión** de RabbitMQ (`rabbitmq:15672`, por la red de Docker Compose) con el usuario administrador para crear y borrar usuarios. El rol `scattered-role` define qué permisos tendrá cada usuario creado: `write` y `read` sobre todo el vhost `/`, sin `configure`. Basta para publicar en exchanges que ya existen, pero no para declarar exchanges ni colas.

**`tools/local-development/up.sh`** (nuevo) junta los dos pasos: `docker-compose up -d`, espera 10 segundos y ejecuta `config.sh`.

### 2. `Scattered.Shared.Secrets`: el cliente de Vault

Proyecto nuevo con `VaultSharp` 1.7.0.4 y `Microsoft.Extensions.Options.ConfigurationExtensions` 6.0.0.

| Archivo | Qué hace |
|---|---|
| `VaultSettings.cs` | `record` con `VaultUrl` (de `appsettings`) y `TokenApi` |
| `VaultDependencyInjection.cs` | `AddVaultService()`: enlaza la sección `SecretManager` y registra `ISecretManager` → `VaultSecretManager` como *scoped* |
| `VaultSecretManager.cs` | `ISecretManager` con dos métodos, uno por motor (abajo) |
| `Extensions/ObjectExtensions.cs` | `ToObject<T>()`: convierte el diccionario del secreto en un objeto por reflexión, propiedad a propiedad |

```csharp
public interface ISecretManager
{
    Task<T> Get<T>(string path) where T : new();                                     // KV v2: secret/{path}
    Task<UsernamePasswordCredentials> GetRabbitMQCredentials(string roleName);        // motor rabbitmq
}
```

- **El token no está en `appsettings`.** `VaultSecretManager` lo lee de la variable de entorno `VAULT-TOKEN` al construirse; si no existe lanza `NotImplementedException("please specify the VAULT-TOKEN env_var")`.
- **Un `VaultClient` nuevo por llamada**, autenticado con ese token (`TokenAuthMethodInfo`).
- `Get<T>` lee `secret/data/{path}` y lo convierte con `ToObject<T>()`. Las claves del secreto deben llamarse **exactamente** como las propiedades de `T`; por eso `RabbitMQCredentials` tiene `username` y `password` en minúsculas.

### 3. `Shared.Communication.RabbitMQ`: credenciales que llegan de fuera

**`RabbitMQSettings.cs`**

```diff
-public record RabbitMQSettings
+public class RabbitMQSettings
 {
     public string Hostname { get; init; } = null!;
-    public string Username { get; init; } = null!;
-    public string Password { get; init; } = null!;
+    public RabbitMQCredentials? Credentials { get; set; }
     public PublisherSettings? Publisher { get; init; }
     public ConsumerSettings? Consumer { get; init; }
 }
+
+public record RabbitMQCredentials
+{
+    public string username { get; init; } = null!;
+    public string password { get; init; } = null!;
+}
```

Pasa de `record` a `class` y `Credentials` tiene `set`, porque ahora se rellena **después** de leer la configuración.

**`RabbitMQDependencyInjection.cs`**: nueva sobrecarga de `AddRabbitMQ` que recibe una función que obtiene las credenciales:

```csharp
public static void AddRabbitMQ(this IServiceCollection serviceCollection,
    Func<IServiceProvider, Task<RabbitMQCredentials>> rabbitMqCredentialsFactory, IConfiguration configuration)
{
    serviceCollection.AddRabbitMQ(configuration);
    serviceCollection.PostConfigure<RabbitMQSettings>(x => x.Credentials
        = rabbitMqCredentialsFactory.Invoke(serviceCollection.BuildServiceProvider()).Result);
}
```

`PostConfigure` se ejecuta la **primera vez** que alguien pide `IOptions<RabbitMQSettings>`, y el resultado queda en memoria durante toda la vida del proceso. Para llamar a Vault construye un segundo contenedor de dependencias (`BuildServiceProvider()`) y espera la tarea con `.Result`. La sobrecarga antigua, que lee usuario y contraseña de la configuración, sigue existiendo con el comentario *"not recommended"*.

`RabbitMQMessagePublisher` y `RabbitMQMessageConsumer` pasan a usar `_settings.Credentials!.username/password`.

### 4. `ServiceBus.cs`: cada tipo de conexión elige su motor de Vault

```csharp
// Gateway (publicador de integración): credenciales dinámicas del motor rabbitmq
serviceCollection.AddRabbitMQ(GetRabbitMqSecretCredentialsfromRabbitMQEngine, configuration);
    // → secretManager.GetRabbitMQCredentials("scattered-role")

// Consumer de integración y publicador/consumer de dominio: secreto estático KV
serviceCollection.AddRabbitMQ(GetRabbitMqSecretCredentials, configuration);
    // → secretManager.Get<RabbitMQCredentials>("rabbitmq")
```

El comentario del original lo explica: KV es la opción por defecto, y el motor rabbitmq se usa en el gateway *"to show the usage of different engines on Vault"*.

| Proceso | Motor | Usuario en RabbitMQ | Cuándo lo pide |
|---|---|---|---|
| API Public (publica) | `rabbitmq` (dinámico) | `token-<uuid>`, creado por Vault, caduca a las 768 h | En la **primera publicación** |
| Subscriptions.Consumer | `secret` (KV) | `ScatteredAdmin` | Al **arrancar** (el `HostedService` lo necesita enseguida) |

### 5. Arranque común y `GlobalUsings`

- **`DefaultScatteredWebApplication`** añade `builder.Services.AddVaultService(builder.Configuration)`, así que **los 7 procesos** registran el cliente de Vault. Solo lo usan los que se conectan a RabbitMQ; los demás no lo resuelven nunca y no necesitan token.
- **`GlobalUsings.cs`** de Setup suma `Shared.Secrets` y los namespaces de `Shared.Communication` (`Consumer.Handler`, `Messages`, `Publisher.Integration`, `Publisher.Domain`). El consumer pasa a enlazarlo en su `.csproj`, y por eso desaparecen `using` de `Program.cs`, de los handlers y del controller.
- **`SecretManager.cs`** (nuevo, en Setup): `AddSecretManager()`. Su propio comentario dice que **no se usa**, porque el registro ya está en `DefaultScatteredWebApplication`.

### 6. Configuración de los servicios

En **API Public** y **Subscriptions.Consumer**:

```diff
   "Bus": {
     "RabbitMQ": {
       "Hostname": "localhost",
-      "Username": "ScatteredAdmin",
-      "Password": "ScatteredPass",
       …
     }
   },
+  "SecretManager": {
+    "VaultUrl" : "http://localhost:8200"
+  },
```

`launchSettings.json` (perfil HTTPS de cada uno) añade `"VAULT-TOKEN": "vault-scattered-token"`. Las credenciales salen de `appsettings`, pero el token raíz de Vault queda en el repositorio; el comentario del `docker-compose.yaml` avisa de que en producción ese mecanismo estaría prohibido.

### 7. Otros

- **API Private** suma dos `using` (`Shared.Communication.RabbitMQ`, `Shared.Secrets`) que no usa: no publica nada.
- **`Scattered.sln`** incorpora `Scattered.Shared.Secrets`.

## Lo que no cambia

- **La topología de RabbitMQ** (`definitions.json`), el flujo `POST /subscribe` → cola → consumer y los handlers.
- **Rutas de YARP, puertos, Products, Emails y la API de Subscriptions.**
- **Orders sigue devolviendo 500**, desde los dos gateways (problema heredado del [capítulo 3–4](flow3.md#1-orders-devuelve-500-en-todos-sus-endpoints)).
- **El consumer sigue abriendo una conexión por segundo** (ver [capítulo 3–4](flow3.md#2-el-consumer-abre-una-conexión-nueva-por-segundo)).

## Cómo ejecutarla

Ahora hacen falta RabbitMQ **y** Vault, y configurar Vault cada vez que arranca su contenedor:

```bash
./tools/local-development/up.sh          # docker-compose up -d + tools/vault/config.sh
dotnet build Scattered.sln
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Api/Public/Scattered.API.Public
DOTNET_ROLL_FORWARD=Major dotnet run --project src/Services/Subscriptions/Scattered.Services.Subscriptions.Consumer
```

`dotnet run` aplica el perfil de `launchSettings.json`, que ya trae `VAULT-TOKEN`. Si se arranca de otra forma (por ejemplo, `--no-launch-profile`), hay que pasarlo con `env 'VAULT-TOKEN=vault-scattered-token' …`, porque el guion impide hacer `export VAULT-TOKEN=…` en bash o zsh.

`config.sh` usa `docker exec -it` y necesita una terminal interactiva (ver [Problemas heredados](#problemas-heredados-del-original)). Desde un script o una CI, ejecuta los mismos comandos sin `-it`:

```bash
sed 's/docker exec -it /docker exec /' tools/vault/config.sh | bash
```

| Qué | Dónde |
|---|---|
| UI de Vault | http://localhost:8200 (método *Token*, `vault-scattered-token`) |
| UI de RabbitMQ | http://localhost:15672 (`ScatteredAdmin` / `ScatteredPass`); en *Admin → Users* aparecen los usuarios `token-…` que crea Vault |
| Secreto KV | `docker exec vault vault kv get secret/rabbitmq` |
| Credenciales emitidas | `docker exec vault vault list sys/leases/lookup/rabbitmq/creds/scattered-role` |

Para detener todo: `pkill -f 'Scattered\.(API|Services)'` y `docker compose stop`.

## Flujo end to end (`features/flow4`)

> **Rama:** `features/flow4`, equivalente a [`05-Gestion-credenciales`](https://github.com/ElectNewt/Distribt/tree/05-Gestion-credenciales) del original (`972c150`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-18 con RabbitMQ y Vault en Docker y los 7 procesos arrancados con logs detallados (como en el [capítulo 2](flow2.md#arrancar-con-logs-detallados)).

### Las llamadas

```bash
# 1. Configurar Vault (sin -it fuera de una terminal interactiva)
sed 's/docker exec -it /docker exec /' tools/vault/config.sh | bash

# 2–4. Arrancar servicios, suscribirse dos veces y ver qué usuario usa el gateway
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"cliente@example.com"}'
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"ana@example.com"}'
docker exec rabbitmq rabbitmqctl list_users
docker exec rabbitmq rabbitmqctl list_permissions

# 5–6. Revocar las credenciales dinámicas y volver a publicar
docker exec vault vault list sys/leases/lookup/rabbitmq/creds/scattered-role
docker exec vault vault lease revoke rabbitmq/creds/scattered-role/<lease-id>
curl -k -X POST https://localhost:7283/subscribe -H 'Content-Type: application/json' -d '{"email":"tras-revocar@example.com"}'

# 7. Reiniciar el gateway y volver a publicar
# 8. Gateway sin VAULT-TOKEN
env DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7299 \
  dotnet run --no-build --no-launch-profile --project src/Api/Public/Scattered.API.Public
curl -k -X POST https://localhost:7299/subscribe -H 'Content-Type: application/json' -d '{"email":"x@example.com"}'

# 9–12. Vault parado, consumer reiniciado sin Vault, Vault reiniciado (vacío) y reconfigurado
docker stop vault
docker start vault
```

### Qué ocurre en cada parte

| # | Acción | Respuesta | Gateway (API Public) | Consumer | Vault | RabbitMQ |
|---|---|---|---|---|---|---|
| 1 | `config.sh` (sin `-it`) | 4 × `Success!` | — | — | KV `secret/rabbitmq` + motor `rabbitmq` + rol `scattered-role` | — |
| 2 | Arrancar los 7 procesos | — | Arranca sin pedir nada a Vault | Pide `secret/rabbitmq` **al arrancar** y conecta como `ScatteredAdmin` | Lee el KV | 10 consumidores en la cola (la fuga del cap. 3–4 sigue) |
| 3 | `POST :7283/subscribe` | `200` (150 ms) | **Primera publicación:** pide credenciales al motor `rabbitmq` | `Email cliente@… successfully subscribed.` | Crea el lease (TTL 768 h, renovable) | Aparece el usuario **`token-ca417db8-…`**: `write .*`, `read .*`, sin `configure` |
| 4 | Segundo `/subscribe` | `200` | **Reutiliza** las credenciales en memoria | Procesa | Nada nuevo | Sigue habiendo un solo usuario `token-…` |
| 5 | `vault lease revoke …` | `All revocation operations queued` | — | — | Revoca el lease | **Borra** el usuario `token-ca417db8-…` |
| 6 | `/subscribe` tras revocar | **`500`** | `BrokerUnreachableException` → `ACCESS_REFUSED - Login was refused` | — | — | Rechaza el login |
| 7 | Reiniciar el gateway + `/subscribe` | `200` | Pide credenciales nuevas | Procesa | Nuevo lease | Usuario nuevo **`token-5aa9cf7d-…`** |
| 8 | Gateway sin `VAULT-TOKEN` (`:7299`) | `GET /` `200`; `/subscribe` **`500`** | Arranca bien; falla al publicar: `NotImplementedException: please specify the VAULT-TOKEN env_var` | — | — | — |
| 9 | Vault parado + `/subscribe` | `200` | Usa las credenciales que ya tenía | Procesa | Caído | El usuario `token-…` sigue existiendo |
| 10 | Reiniciar el consumer con Vault parado | — | — | **El proceso muere**: `Hosting failed to start … Connection refused (localhost:8200)` | Caído | 0 consumidores |
| 11 | Arrancar Vault de nuevo + arrancar el consumer | — | — | **Muere otra vez**: `VaultApiException: {"errors":[]}` | **Vacío**: `No value found at secret/data/rabbitmq`, sin motor `rabbitmq` (modo dev en memoria) | — |
| 12 | `config.sh` + arrancar el consumer + `/subscribe` | `200` | Sigue con sus credenciales | Arranca y procesa `final@…` | Configurado de nuevo | El usuario `token-5aa9cf7d-…` queda **huérfano**: Vault perdió su lease y ya no lo borrará |
| 13 | Orders, Products, gateway privado | `500` · `200` · `500` / `200` | Igual que en el capítulo 3–4 | — | — | — |
| 14 | `./tools/vault/config.sh` sin terminal | exit `0` | — | — | **No se configura nada**: 4 × `cannot attach stdin to a TTY-enabled container` | — |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| Ya no hay contraseñas en `appsettings.json` | Pasos 2–3 | Solo queda la URL de Vault; el token llega por variable de entorno |
| Cada proceso usa un motor distinto | Pasos 2–3 | Consumer: secreto estático KV. Gateway: usuario dinámico creado por Vault |
| Las credenciales dinámicas son un **usuario real** de RabbitMQ con permisos mínimos | Paso 3 (`list_users`, `list_permissions`) | Si se filtran, caducan y se pueden revocar sin tocar al administrador |
| Las credenciales se piden **una vez** y se quedan en memoria | Pasos 4 y 9 | Con Vault caído el gateway sigue publicando, pero nunca renueva ni refresca |
| Revocar el lease rompe el gateway hasta reiniciarlo | Pasos 5–7 | Lo mismo ocurrirá cuando caduque el lease (768 h, unos 32 días) |
| El momento en que se piden depende del proceso | Pasos 2, 3 y 10 | Consumer: al arrancar, y si falla, muere. Gateway: al publicar, y si falla, devuelve 500 |
| Vault en modo dev **olvida todo** al reiniciarse | Paso 11 | Hay que volver a ejecutar `config.sh` cada vez que se recrea el contenedor |
| Quedan usuarios huérfanos en RabbitMQ | Paso 12 | Vault solo borra los usuarios cuyo lease conoce |
| `config.sh` falla en silencio fuera de una terminal | Paso 14 | Termina con código `0` sin haber configurado nada |

## Problemas heredados del original

### 1. Las credenciales dinámicas nunca se renuevan

`PostConfigure` pide las credenciales una sola vez y `IOptions<RabbitMQSettings>` las guarda para siempre. Ni se renueva el lease ni se vuelven a pedir si RabbitMQ rechaza el login. Si el lease se revoca (paso 5) o caduca a las 768 h, **todas** las publicaciones del gateway devuelven `500` (`ACCESS_REFUSED`) hasta reiniciar el proceso. Es justo el caso que las credenciales dinámicas quieren aprovechar, así que el ejemplo enseña el motor pero no su ciclo de vida.

### 2. Vault en modo dev y `config.sh` manual

El contenedor arranca vacío cada vez. Si Vault se recrea y no se ejecuta `config.sh`, el consumer no puede arrancar (paso 11) y el gateway falla en su primera publicación. Además, los usuarios dinámicos emitidos antes del reinicio quedan huérfanos en RabbitMQ.

### 3. `config.sh` usa `docker exec -it`

Fuera de una terminal interactiva (un script lanzado por otra herramienta o una CI), cada `docker exec -it` falla con *"cannot attach stdin to a TTY-enabled container"*. El script no comprueba errores y termina con código `0`. `main` sigue igual.

### 4. Otros detalles

- **`BuildServiceProvider()` dentro de `PostConfigure`** crea un segundo contenedor de dependencias, con sus propias instancias *singleton*, y bloquea con `.Result` una llamada asíncrona.
- **`VAULT-TOKEN` lleva guion.** No se puede definir con `export` en bash ni en zsh; solo funciona vía `launchSettings.json` (perfil HTTPS) o `env 'VAULT-TOKEN=…'`.
- **El token raíz está en el repositorio** (`docker-compose.yaml` y `launchSettings.json`). El propio comentario lo marca como algo exclusivo de desarrollo.
- **`NotImplementedException`** para un token ausente. La excepción adecuada sería `InvalidOperationException` o un fallo al arrancar.
- **`ToObject<T>()` falla con un `NullReferenceException`** si el secreto KV tiene una clave que no coincide con una propiedad de `T` (por ejemplo, `Username` con mayúscula).
- **`SecretManager.AddSecretManager()` no se usa**, y API Private suma dos `using` sin uso.
- Siguen los problemas del [capítulo 3–4](flow3.md#problemas-heredados-del-original): Orders roto, fuga de conexiones del consumer y consumer que no se recupera si RabbitMQ no estaba al arrancar.

## Particularidades

- **Imagen `hashicorp/vault` (cambio propio).** El original usa `vault:latest`, la imagen *oficial* de Docker Hub que HashiCorp dejó de publicar en 2023 y ya no se puede descargar. `hashicorp/vault:latest` es la misma aplicación y la que usa `main`. En modo dev se comporta igual (`vault server -dev`, token raíz por variable de entorno).
- **`tools/local-development/up.sh` (cambio propio).** El original crea el script en `tools/local-devleopment/` (con errata), mientras que su README indica ejecutar `./tools/local-development/up.sh`. Se usa el nombre correcto, que es el que el original adopta después (commit `dffe01f`) y el de `main`.
- **Scripts ejecutables (cambio propio).** En el original `up.sh` y `config.sh` se crean sin permiso de ejecución (`100644`), así que en macOS y Linux `up.sh` no puede llamar a `./tools/vault/config.sh`. Aquí tienen `+x`, como en `main`.
- **Contenedor `vault` compartido con `main`.** `main` define también `container_name: vault`, con dependencia de Consul. Al cambiar de rama, `docker compose up -d --force-recreate vault` lo recrea con la configuración de esta.
- **Versión de Vault.** `latest` es hoy Vault 2.2.1; en enero de 2022 la versión vigente era la 1.9. Los comandos usados (`kv put`, `secrets enable rabbitmq`, `write rabbitmq/roles/…`) funcionan igual. La UI muestra además un motor `agent-registry/` que no existía entonces.
- Siguen vigentes las particularidades de los capítulos [1](flow1.md#particularidades), [2](flow2.md#particularidades) y [3–4](flow3.md#particularidades).
