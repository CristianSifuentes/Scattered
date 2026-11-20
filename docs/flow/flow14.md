# Capítulo 15 · API Key en el gateway privado (`features/flow14`)

El gateway privado (`:7022`, *"Private api for Tenants"*) está pensado para clientes concretos, no para cualquiera. Una **API key** es la forma más simple de identificarlos: un valor secreto que el cliente envía en cada petición, en una cabecera, y que el servidor compara con el que tiene configurado. En este capítulo un middleware exige la cabecera `apiKey` en todas las peticiones al gateway privado, salvo en `/health`. La rama original añade además documentación de `Shared.Discovery` y `Shared.Secrets`, y un intento de pasar a .NET 7 que se revierte el mismo día.

<!-- nav:start -->
[← Capítulo 14](flow13.md) · [Índice del flujo](README.md) · [Siguiente: capítulo 16 →](flow15.md)

Progreso: [1](flow1.md) · [2](flow2.md) · [3–4](flow3.md) · [5](flow4.md) · [6](flow5.md) · [7](flow6.md) · [8](flow7.md) · [9](flow8.md) · [10](flow9.md) · [11](flow10.md) · [12](flow11.md) · [13](flow12.md) · [14](flow13.md) · **15** · [16](flow15.md) · [17](flow16.md)
<!-- nav:end -->

## Contenido

[Origen](#origen) · [Resumen](#resumen) · [Qué cambió](#qué-cambió) · [Lo que no cambia](#lo-que-no-cambia) · [Cómo ejecutarla](#cómo-ejecutarla) · [Flujo end to end (`features/flow14`)](#flujo-end-to-end-featuresflow14) · [Problemas heredados del original](#problemas-heredados-del-original) · [Particularidades](#particularidades)

## Origen

| | |
|---|---|
| Proyecto original | [ElectNewt/Distribt](https://github.com/ElectNewt/Distribt) |
| Rama de origen | [`15-Api-key`](https://github.com/ElectNewt/Distribt/tree/15-Api-key) |
| Commits de origen | `3485abc` add information about the branches (9-jul-2022) · `fd6f4f1` #19 add documentation for service discovery · `a3ed963` #15 add documentation for secrets (1-ago-2022) · `467c99c` Api Key example (25-oct-2022) · `068a024` Update Distribt to .NET7 + `21bbc57` Revert "Update Distribt to .NET7" (14-nov-2022) |
| Diff respecto al capítulo 14 | 6 commits, 8 archivos, +168 / −3 líneas (el paso a .NET 7 y su revert se anulan) |
| Artículo | [Qué es una API key](https://www.netmentor.es/entrada/explicacion-api-key) |
| Cambios propios | Ninguno en el código: el diff del original aplica limpio |

Para ver el diff completo en Scattered: `git diff -M features/flow13 -- src docs/discovery docs/secrets`.

## Resumen

```text
Cliente ──GET /reports/order/{id}──────────────────────────▶ API Private (:7022) ──▶ ApiKeyMiddleware
          cabecera  apiKey: b92b0bdf-da95-42a8-a2b1-780ca461aaf3                      │
                                                                                      ├─ coincide  ──▶ YARP ──▶ Orders (:50220)
                                                                                      └─ falta/no coincide ──▶ UnauthorizedAccessException (500)
          /health → sin comprobar (lo consultan los balanceadores)
```

| Petición al gateway privado | Respuesta |
|---|---|
| Con `apiKey` correcta | La de siempre (`200`) |
| Sin `apiKey` o con una incorrecta | **`500`** `UnauthorizedAccessException: The API Key is missing` |
| `/health` | `200`, sin clave |

## Qué cambió

```text
docs/discovery/Readme.md                     NUEVO: cómo usar Scattered.Shared.Discovery (AddDiscovery, IServiceDiscovery, Consul)
docs/secrets/Readme.md                       NUEVO: cómo usar Scattered.Shared.Secrets (VAULT-TOKEN, ISecretManager, Vault)
src/Shared/Scattered.Shared.Setup/API/Key/
├── ApiKeyConfiguration.cs                   NUEVO: ClientId, Value
├── ApiKeyDependencyInjection.cs             NUEVO: AddApiToken(configuration) + UseApiTokenMiddleware()
└── ApiKeyMiddleware.cs                      NUEVO: compara la cabecera "apiKey" con la configuración
src/Api/Private/Scattered.API.Private
├── Program.cs                               + AddApiToken + UseApiTokenMiddleware
└── appsettings.json                         + "ApiKey": { "clientId": "1", "value": "b92b0bdf-…" }
```

### 1. El middleware

```csharp
public async Task Invoke(HttpContext context, IOptions<ApiKeyConfiguration> apiToken)
{
    if (context.Request.Headers.TryGetValue("apiKey", out StringValues apiKey))
    {
        if (apiKey == apiToken.Value.Value)
            await _next(context);                 // clave correcta: la petición sigue
        else
            ReturnApKeyNotfound();
    }
    else
        ReturnApKeyNotfound();

    void ReturnApKeyNotfound() => throw new UnauthorizedAccessException("The API Key is missing");
}
```

```csharp
public static void UseApiTokenMiddleware(this WebApplication webApp)
{
    //Do not act on /health or /health-ui
    webApp.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"),
        appBuilder => appBuilder.UseMiddleware<ApiKeyMiddleware>());
}
```

- **Solo el gateway privado** lo usa (`Program.cs` de API Private). El público, que recibe tráfico de cualquier cliente, no pide clave.
- **Una sola clave**, en `appsettings.json` (`ApiKey:value`). `ClientId` se lee pero no se usa: no hay forma de saber qué cliente llama ni de tener varios.
- `UseWhen` aplica el middleware a todo menos a `/health`, para que balanceadores y orquestadores puedan comprobar la salud sin clave.
- El middleware se registra en `Program.cs` después de `MapReverseProxy()`, pero eso no lo deja fuera: con *minimal hosting*, los endpoints mapeados se ejecutan al final de la canalización, después de todos los middlewares.

### 2. Documentación de `Shared`

`docs/discovery/Readme.md` y `docs/secrets/Readme.md` explican cómo usar las dos librerías en otro proyecto: configuración (`Discovery:Address`, `VAULT-TOKEN`), registro (`AddDiscovery`, `AddSecretManager`), interfaces (`IServiceDiscovery.GetFullAddress` / `GetDiscoveryData`, `ISecretManager.Get<T>`) y cómo dar de alta datos en Consul y Vault. Se suman a `docs/communication`, que existe desde el capítulo 3–4.

### 3. .NET 7 de ida y vuelta

El commit `068a024` *Update Distribt to .NET7* y su revert `21bbc57` llegan el mismo día: en esta rama el código sigue en `net6.0`. El paso definitivo llega en el capítulo 16.

## Lo que no cambia

- **El gateway público, los servicios y la infraestructura.**
- **Los servicios detrás del gateway privado no están protegidos**: Orders sigue respondiendo en `:50220` a cualquiera (paso 11 del flujo).

## Cómo ejecutarla

Como el [capítulo 14](flow13.md#cómo-ejecutarla). Para llamar al gateway privado:

```bash
KEY=b92b0bdf-da95-42a8-a2b1-780ca461aaf3                         # ApiKey:value en src/Api/Private/…/appsettings.json
curl -k -H "apiKey: $KEY" https://localhost:7022/reports/order/<orderId>
curl -k https://localhost:7022/reports/order/<orderId>           # 500: The API Key is missing
curl -k https://localhost:7022/health                            # 200 sin clave
```

## Flujo end to end (`features/flow14`)

> **Rama:** `features/flow14`, equivalente a [`15-Api-key`](https://github.com/ElectNewt/Distribt/tree/15-Api-key) del original (`21bbc57`). Las respuestas y los casos límite de esta sección son los de esta rama.

Ejecutado el 2026-11-20 con los 12 contenedores y los 10 procesos arrancados.

| # | Petición | Respuesta | Comentario |
|---|---|---|---|
| 1 | `GET :7022/` sin clave | **`500`** `UnauthorizedAccessException: The API Key is missing` | Hasta el *Hello World* está protegido |
| 2 | `GET :7022/` con `apiKey` | `200` `Hello World!` | — |
| 3 | `GET :7022/reports/order/{id}` sin clave | **`500`** | — |
| 4 | Ídem con una clave incorrecta | **`500`** con el mismo mensaje, *"is missing"* | No distingue "falta" de "no es válida" |
| 5 | Ídem con la clave correcta | `200` con el pedido (`ResultDto`) | YARP reenvía a Orders |
| 6 | Ídem con la cabecera `ApiKey` (otra capitalización) | `200` | Los nombres de cabecera HTTP no distinguen mayúsculas |
| 7 | `GET :7022/health` sin clave | `200 Healthy` | Excluido, como pretende el código |
| 8 | `GET :7022/health-ui` sin clave | **`500`** | `StartsWithSegments("/health")` compara segmentos completos: `/health-ui` no empieza por el segmento `/health` |
| 9 | `GET :7022/swagger/index.html` sin clave | **`500`** | Swagger también queda detrás de la clave |
| 10 | `GET :7283/order-ms/order/{id}` (gateway público) | `200` | El público no pide clave |
| 11 | `GET :50220/order/{id}` (Orders directo) | `200` | La clave protege el gateway, no el servicio |
| 12 | `GET http://localhost:5032/` (gateway privado por HTTP) sin clave | **`500`** | El middleware actúa antes que la redirección a HTTPS |

### Observaciones de la ejecución

| Observación | Dónde se vio | Por qué importa |
|---|---|---|
| La clave **funciona como filtro** | Pasos 1–5 | Sin ella no se llega a Orders por el gateway privado |
| Responde **500**, no **401** | Pasos 1, 3, 4 | El cliente no puede distinguir "no autorizado" de "el servidor ha fallado"; además aparece como error en métricas y trazas |
| `/health-ui` queda bloqueada | Paso 8 | El comentario del código dice lo contrario |
| La protección es **solo perimetral** | Paso 11 | Quien llegue a la red interna llama a los servicios sin clave |

## Problemas heredados del original

### 1. 500 en lugar de 401

El middleware lanza `UnauthorizedAccessException` en vez de escribir una respuesta `401 Unauthorized`. En desarrollo, la página de excepciones devuelve la traza completa; en producción sería un 500 genérico. Además, el mensaje es siempre *"The API Key is missing"*, también cuando la clave existe pero no coincide.

### 2. La clave, en texto plano y única

- Está en `appsettings.json`, en el repositorio, después de varios capítulos sacando secretos a Vault (capítulo 5).
- Hay una sola clave para todos los clientes: no se puede revocar a uno sin cambiarla para todos, y `ClientId` no se usa.
- La comparación es una igualdad normal de cadenas, no una comparación en tiempo constante.

### 3. Otros detalles

- **`/health-ui` y Swagger** quedan detrás de la clave (ver pasos 8 y 9).
- **Los servicios internos no se protegen**; la clave solo tiene sentido si la red impide llegar a ellos directamente.
- **Errata en `docs/secrets/Readme.md`**: el método es `Get<T>` (escrito `GeT<T>`), y el ejemplo usa la ruta `rabbitmq/config/connection`, que es la del motor de RabbitMQ, no la del secreto KV.
- Siguen los problemas de los capítulos anteriores.

## Particularidades

- **Sin cambios propios en el código.** El diff del original, renombrado, aplica limpio sobre `features/flow13`.
- **Documentación de `Shared`**: `docs/discovery` y `docs/secrets` se renombran a Scattered como el resto del código; el README los enlaza en la sección *Documentación*.
- **El intento de .NET 7** (`068a024` + revert `21bbc57`) no deja rastro en el diff.
- Siguen vigentes las particularidades de los capítulos anteriores.
