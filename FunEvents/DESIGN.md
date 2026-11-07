# FunEvents — Diseño inicial

## 1. Objetivo y alcance

Diseñar una plataforma de venta de entradas para FunEvents que atienda el portal propio, las oficinas de atención al cliente y los canales de colaboradores. La plataforma debe permitir que cada canal integre la compra en su propia experiencia sin perder el control central de catálogo, disponibilidad y emisión de entradas.

Este documento define la arquitectura objetivo y el alcance del prototipo técnico. **No incluye implementación todavía.** El prototipo de la Parte 2 cubrirá únicamente la reserva de entradas para un evento y usuario conocidos mediante un cliente de consola que consume una API web.

## 2. Requisitos y supuestos

### Requisitos funcionales

- Consultar eventos y sus sesiones/funciones.
- Consultar disponibilidad y reservar entradas desde distintos canales.
- Identificar el canal que origina cada operación.
- Permitir integraciones de colaboradores mediante APIs documentadas y credenciales propias.
- Evitar sobreventa cuando varios canales intentan reservar las últimas entradas a la vez.
- En el sistema completo: confirmar pagos, emitir entradas y gestionar cancelaciones y devoluciones.

### Supuestos para el prototipo

- El evento y el usuario ya existen y se identifican por código.
- La petición de reserva indica una sesión y una cantidad de entradas; el código de evento por sí solo no determina qué función se desea.
- El prototipo reserva entradas sin realizar cobro. La reserva queda temporalmente pendiente de confirmación y vence tras un plazo configurable.
- Habrá una base de datos relacional para persistir inventario y reservas. PostgreSQL es la opción propuesta.
- Un canal colaborador se identifica por una credencial/API key en el prototipo. En producción se recomienda OAuth 2.0 Client Credentials, con permisos por colaborador y rotación de secretos.

## 3. Arquitectura propuesta

### 3.1 Propuesta: adaptador desplegable por partner

```mermaid
flowchart LR
    WEB[FunEvents website] --> EDGE[Public API gateway]
    OFFICE[FunEvents offices] --> EDGE
    PA[Partner A] --> EDGE
    PB[Partner B] --> EDGE
    EDGE --> AA[Partner A adapter]
    EDGE --> AB[Partner B adapter]
    EDGE --> TICKETING[Ticketing core]
    AA -->|Internal API| TICKETING
    AB -->|Internal API, including payment status| TICKETING
    TICKETING --> INV[Inventory and holds]
    TICKETING --> ORD[Orders and reservations]
    TICKETING --> TIX[Ticket issuance]

    ORD --> PAY[Payment orchestration]
    PAY --> PSP[FunEvents payment providers]

    TICKETING --> DB[(Transactional store: domain data + outbox)]
    DB --> RELAY[Outbox publisher]
    RELAY --> BUS[Event bus]
    BUS --> ASYNC[Notifications, analytics, reconciliation]
```

La API canónica ofrece operaciones comunes de FunEvents. Cada partner tiene un adaptador como límite de integración y despliegue: valida la autenticación externa, aplica el contrato y modelo de datos del partner, y dirige su flujo de compra hacia las operaciones canónicas. Los adaptadores invocan FunEvents con identidades de servicio propias y permisos limitados; no transmiten credenciales externas al núcleo.

Los módulos de dominio centrales conservan las invariantes que deben ser iguales para todos los canales, especialmente disponibilidad, reservas idempotentes, estado de pedidos y emisión. Los adaptadores pueden escalar y evolucionar de forma independiente. Para un volumen bajo o para el prototipo, un adaptador podría ejecutarse inicialmente como módulo del mismo proceso; la frontera lógica debe permitir separarlo sin trasladar reglas de inventario o pedidos.

#### Motivos de selección

- **Capacidad de escalado independiente:** si un partner concentra picos de tráfico (por ejemplo, una preventa), se pueden añadir instancias y recursos a su adaptador sin escalar todos los conectores. Esto no elimina la necesidad de escalar y proteger el núcleo compartido de inventario, que sigue siendo un posible cuello de botella.
- **Límite claro de autenticación e integración:** cada partner puede usar su mecanismo de autenticación y su contrato, mientras FunEvents mantiene una identidad interna uniforme entre adaptadores y API canónica.
- **Evolución y despliegues independientes:** cambios en el esquema, autenticación o flujo de un partner pueden probarse y publicarse sin exigir una versión coordinada para los demás.
- **Aislamiento de recursos y fallos:** saturación, errores o dependencias externas defectuosas de un adaptador tienen un alcance menor. Deben existir límites de concurrencia, timeouts y circuit breakers para evitar que el problema alcance los servicios comunes.
- **Diferencias reales de flujo:** se esperan cambios de pasos, orden, handoffs de checkout y responsabilidades de pago, no solo traducción de campos o configuración visual. Cada adaptador puede implementar la orquestación externa correspondiente y llamar a las mismas operaciones centrales.

#### Tradeoffs y límites

- **Más coste operativo:** cada adaptador independiente añade despliegue, configuración, monitorización, alertas, parches de seguridad y soporte. Conviene reservar despliegues separados para partners cuyo volumen, aislamiento o ritmo de cambio lo justifique.
- **El núcleo sigue siendo compartido:** todos los canales compiten por inventario. La consistencia y prevención de sobreventa se resuelven en los servicios centrales y la base de datos, no duplicando lógica en adaptadores.
- **Riesgo de divergencia:** las reglas de reserva, precios y emisión no deben copiarse a cada adaptador; deben vivir en el núcleo canónico. Los adaptadores orquestan, autentican y traducen.
- **Superficie de integración mayor:** cada contrato debe versionarse, documentarse y observarse. Se requieren trazas correlacionadas desde el partner hasta la reserva.

### 3.2 Componentes y responsabilidades

Estos son límites lógicos; no implican que cada caja sea un despliegue independiente. El núcleo puede empezar como una aplicación modular. Los adaptadores de partners con requisitos de carga, aislamiento o ciclo de cambios propios sí pueden desplegarse por separado.

| Componente | Propósito y responsabilidades |
|---|---|
| API gateway | Entrada pública; termina TLS, enruta peticiones a APIs/adaptadores y aplica políticas comunes como límites de tráfico y trazabilidad. No contiene reglas de reservas ni pagos. |
| Adaptador por partner | Valida el esquema de autenticación propio del partner, traduce su contrato/datos y coordina su flujo de checkout. Llama al núcleo con identidad de servicio FunEvents. |
| Ticketing core | API y casos de uso canónicos para canales propios y partners; aplica reglas comunes y delega en módulos de dominio. |
| Catálogo | Eventos, recintos, sesiones, zonas y precios. |
| Inventario y reservas | Disponibilidad, holds con expiración y prevención de sobreventa. |
| Pedidos | Estado de la compra y vínculo entre reserva, comprador, canal, pago y entradas. |
| Orquestación de pagos | Inicia y consulta pagos cuando FunEvents cobra; normaliza estados de proveedores y coordina su efecto en el pedido. No procesa el dinero por sí sola. |
| Emisión de entradas | Genera y mantiene entradas/QR tras confirmarse el pedido; soporta entrega y validación en acceso. |
| Almacén transaccional | Persiste el estado actual de dominio. En la primera versión puede ser una base relacional; los módulos acceden mediante interfaces y límites definidos, no escribiendo libremente cualquier tabla. |
| Outbox y publicador | Registra eventos de integración en la misma transacción que el cambio de dominio y los publica de forma asíncrona. |
| Procesos en segundo plano | Expiran holds, publican outbox, envían notificaciones y ejecutan conciliaciones; deben ser idempotentes y observables. |

### 3.3 Flujos y patrones detallados

#### Orquestación de pagos y proveedores

Un **método de pago** es la forma elegida por el comprador (por ejemplo, tarjeta, cartera digital o método local). Un **proveedor** procesa ese método. La orquestación decide qué integración usar según el mercado, moneda, canal y métodos habilitados. Los nombres mostrados son ejemplos de proveedores posibles, no una selección de FunEvents; disponibilidad, cobertura y condiciones deben validarse por mercado antes de elegir. Como referencias iniciales, consultar los catálogos de métodos de [Adyen](https://docs.adyen.com/payment-methods/) y [Stripe](https://docs.stripe.com/payments/mobile/embedded).

```mermaid
flowchart LR
    BUYER[Comprador] --> CHECKOUT[Checkout del canal]
    CHECKOUT --> ORDER[Pedido y hold existentes]
    ORDER --> ORCH[Orquestación de pagos]
    ORCH --> ROUTE[Selección por canal, mercado, moneda y método]
    ROUTE --> ADAPTER[Adaptador de proveedor]
    ADAPTER --> ADYEN[Adyen, ejemplo]
    ADAPTER --> STRIPE[Stripe, ejemplo]
    ADAPTER --> OTHER[Otro proveedor o método local]
    ADYEN --> RESULT[Webhook / resultado de pago]
    STRIPE --> RESULT
    OTHER --> RESULT
    RESULT --> ORCH
    ORCH -->|Pago confirmado| ORDER
    ORCH -->|Fallo, rechazo o expiración| ORDER
    PARTNER[Partner que cobra] -->|Confirmación de pago autenticada| PA[Adaptador del partner]
    PA -->|Confirmar pedido asociado al hold| ORDER
```

Cuando FunEvents cobra, el proveedor informa el resultado a FunEvents y la orquestación actualiza el pedido de forma idempotente. En el modelo del partner, el partner cobra y notifica el resultado; FunEvents no debe marcar como pagado basándose en una redirección del navegador, sino en una notificación autenticada o consulta servidor a servidor.

#### Outbox transaccional

El outbox evita perder un evento entre la escritura de datos y su publicación. El registro del pedido/reserva y el mensaje outbox se confirman juntos; un publicador reintenta enviar los mensajes pendientes. Los consumidores deben tolerar duplicados.

```mermaid
sequenceDiagram
    participant API as Ticketing API
    participant DB as Base de datos
    participant RELAY as Publicador outbox
    participant BUS as Event bus
    participant CONSUMER as Consumidor

    API->>DB: Transacción: actualizar pedido + insertar outbox
    DB-->>API: Commit
    RELAY->>DB: Leer mensajes pendientes
    DB-->>RELAY: Mensajes outbox
    RELAY->>BUS: Publicar evento
    BUS-->>RELAY: Confirmación
    RELAY->>DB: Marcar mensaje como publicado
    BUS->>CONSUMER: Entregar evento (posibles duplicados)
```

#### Conciliación de pagos de partners

En el flujo «Caso B: el partner cobra al comprador» de la sección 4, el aviso de pago del partner permite confirmar la compra en tiempo real. Una conciliación periódica sirve como control de discrepancias y recuperación; no reemplaza el aviso de pago. Se comparan los pagos y reembolsos reportados por el partner con pedidos, holds y entradas de FunEvents. Las excepciones se reintentan o pasan a revisión operativa; nunca se emiten entradas solo porque una fila de conciliación no tenga discrepancias.

```mermaid
flowchart LR
    PARTNER[Partner B] -->|Webhook / API: estado de pago| ADAPTER[Adaptador B]
    ADAPTER -->|Notificación autenticada e idempotente| CORE[Pedidos FunEvents]
    CORE -->|Confirmar o rechazar fulfillment| TICKETS[Emisión de entradas]

    SCHEDULE[Programador] --> JOB[Trabajo de conciliación]
    JOB -->|Obtener informe/API de pagos, reembolsos y contracargos| PARTNER
    PARTNER -->|Movimientos y referencias| JOB
    JOB --> COMPARE[Comparar referencias y estados]
    CORE -->|Pedidos, reservas y tickets| COMPARE
    COMPARE -->|Coincidencia| DONE[Registrar conciliación]
    COMPARE -->|Diferencia| EXCEPTION[Cola de excepciones y alerta]
    EXCEPTION --> ACTION[Reintento, reembolso o revisión operativa]
```

La conciliación requiere referencias compartidas e idempotentes, como `partnerOrderId`, `reservationCode` y `paymentReference`, además de acuerdos sobre plazos, moneda, comisiones, reembolsos y contracargos.

### 3.4 Preocupaciones de diseño transversales

#### Dominio y persistencia

Catálogo, inventario, reservas/pedidos, pagos y entradas son capacidades de dominio; su separación lógica no obliga a separar procesos o bases desde el primer día. Los casos de uso coordinan operaciones. Las escrituras de inventario y reserva/pedido que requieren atomicidad se mantienen en una transacción de la base relacional inicial. Si se extraen servicios en el futuro, cada servicio será dueño de sus datos y otros servicios usarán APIs/eventos, no tablas ajenas.

El proveedor de pago es un sistema externo conectado mediante una interfaz y adaptadores. No forma parte del almacén de dominio ni de la transacción local de inventario.

#### Concurrencia y consistencia de inventario

El objetivo es que dos solicitudes simultáneas no puedan reservar las mismas últimas entradas. La base de datos debe decidir de forma atómica cuál solicitud obtiene el inventario; una comprobación previa en la aplicación no basta.

Para reservar `requestedQuantity`, se propone una actualización condicional dentro de una transacción:

```sql
UPDATE inventory
SET available = available - @requestedQuantity
WHERE session_code = @sessionCode
  AND available >= @requestedQuantity;
```

Si se actualiza una fila, la transacción crea la reserva pendiente y registra su clave de idempotencia. Si no se actualiza ninguna fila, no hay inventario suficiente y la API responde, por ejemplo, `409 Conflict`. Si falla la creación de la reserva, la transacción revierte también el descuento. Un proceso expira holds y devuelve inventario una sola vez.

La concurrencia optimista usa una versión de fila para detectar cambios concurrentes; resulta útil cuando los conflictos son poco frecuentes y se quiere evitar sobrescribir datos. Para descontar inventario, la actualización condicional suele ser más sencilla. Si las métricas muestran contención alta, se evaluarán bloqueos o serialización para el inventario afectado.

Una sala de espera virtual regula cuántos compradores entran al checkout; resulta útil ante picos previsibles o saturación observada. No reserva entradas: cada comprador admitido aún debe obtener un hold y puede encontrar el evento agotado. Las colas internas se reservan para trabajo asíncrono, como notificaciones y conciliación. No se retiene inventario para quienes esperan ni se mantienen transacciones abiertas durante llamadas externas.

#### Event sourcing

Event sourcing no es un requisito inicial. Guarda eventos de dominio como fuente de verdad y reconstruye el estado reproduciéndolos; no es lo mismo que publicar eventos de integración mediante outbox. Añade complejidad en versionado, proyecciones, consistencia eventual, consultas y operación.

Se recomienda empezar con estado relacional convencional, auditoría y outbox transaccional. Reconsiderar event sourcing solo si hay requisitos concretos de reconstrucción histórica completa, auditoría inmutable de alto detalle o múltiples proyecciones independientes; no se adopta para resolver concurrencia.

#### Límites de confianza y acceso

- El portal y las oficinas usan identidad de usuario; las oficinas registran también el agente que atendió la operación.
- Cada adaptador valida la autenticación específica del partner. Sus credenciales se gestionan y rotan de forma independiente y no identifican al comprador final.
- El adaptador se autentica ante la API interna con identidad de servicio FunEvents y permisos limitados al partner y operaciones permitidas.
- Cada operación registra `channelId`, origen y actor para auditoría, soporte y liquidación.
- El gateway aplica controles comunes de entrada; el adaptador aplica autenticación/validación específica del partner y el núcleo autoriza operaciones. Usar TLS, límites de tráfico, gestión de secretos y trazas sin datos sensibles.

## 4. Flujos principales

### Reserva (prototipo)

1. El cliente envía código de evento, código de usuario, sesión, cantidad, canal e identificador de idempotencia.
2. La API valida que evento, sesión y usuario existen y que el canal está autorizado.
3. En una transacción, el servicio comprueba y descuenta/bloquea disponibilidad de forma atómica.
4. Se crea una reserva `Pending` con fecha de expiración y se devuelve su código.
5. Las solicitudes repetidas con la misma clave de idempotencia devuelven el mismo resultado, sin crear otra reserva.
6. Un proceso de expiración libera inventario de reservas vencidas.

### Compra completa (fuera del prototipo)

La confirmación de pago debe ser idempotente. Tras el resultado del proveedor, el sistema confirma el pedido y emite entradas; si el pago falla o expira la reserva, libera inventario. Las llamadas al proveedor y las notificaciones no deben ejecutarse dentro de la transacción de inventario. Se recomienda patrón outbox para publicar eventos de forma fiable.

### Casos de uso de checkout con partners

Los siguientes casos muestran dos modelos de responsabilidad de pago. En ambos, FunEvents conserva la autoridad sobre el inventario, la reserva y la emisión de entradas. El partner se autentica en su adaptador; este llama a la API canónica con identidad de servicio FunEvents.

#### Caso A: FunEvents cobra al comprador

El partner solicita un hold temporal y redirige al comprador al checkout de FunEvents. FunEvents procesa el pago. Si el pago se confirma antes de vencer el hold, confirma el pedido y emite las entradas. Si falla o vence, cancela la reserva y devuelve inventario.

```mermaid
sequenceDiagram
    actor Buyer as Comprador
    participant Partner as Portal del partner
    participant Adapter as Adaptador del partner
    participant Core as API / servicios FunEvents
    participant PSP as Proveedor de pago FunEvents

    Buyer->>Partner: Selecciona evento y entradas
    Partner->>Adapter: Solicita hold
    Adapter->>Core: Crear reserva temporal (idempotente)
    Core-->>Adapter: reservationCode + expiresAt
    Adapter-->>Partner: URL de checkout + reservationCode
    Partner-->>Buyer: Redirige al checkout FunEvents
    Buyer->>Core: Completa checkout
    Core->>PSP: Autorizar/cobrar pago
    PSP-->>Core: Pago aprobado o rechazado
    alt Pago aprobado antes de expirar
        Core->>Core: Confirmar pedido y emitir entradas
        Core-->>Buyer: Confirmación y entradas
        Core-->>Partner: Notificación de resultado (opcional)
    else Pago rechazado o hold expirado
        Core->>Core: Cancelar reserva y liberar inventario
        Core-->>Buyer: Informar fallo/expiración
    end
```

#### Caso B: el partner cobra al comprador

El partner solicita primero un hold para reducir el riesgo de cobrar por entradas no disponibles. El partner procesa el pago en su propio checkout y comunica el resultado. FunEvents confirma el pedido y emite entradas solo cuando recibe una confirmación válida antes de que venza el hold. El partner liquida después con FunEvents según el acuerdo comercial; esa liquidación entre empresas es externa al flujo técnico de compra.

```mermaid
sequenceDiagram
    actor Buyer as Comprador
    participant Partner as Checkout del partner
    participant Adapter as Adaptador del partner
    participant Core as API y servicios FunEvents
    participant PSP as Proveedor de pago del partner

    Buyer->>Partner: Selecciona evento y entradas
    Partner->>Adapter: Solicita hold
    Adapter->>Core: Crear reserva temporal (idempotente)
    Core-->>Adapter: reservationCode y expiresAt
    Adapter-->>Partner: Hold confirmado
    Partner->>PSP: Cobrar al comprador
    PSP-->>Partner: Pago aprobado o rechazado
    alt Pago aprobado y hold vigente
        Partner->>Adapter: Confirmar pago (idempotencyKey)
        Adapter->>Core: Confirmar pedido
        Core->>Core: Confirmar pedido y emitir entradas
        Core-->>Adapter: Pedido confirmado y entradas
        Adapter-->>Partner: Resultado de fulfillment
        Partner-->>Buyer: Entregar entradas
    else Pago rechazado o hold expirado
        Partner->>Adapter: Notificar fallo o consultar reserva
        Adapter->>Core: Cancelar o consultar reservationCode
        Core->>Core: Liberar inventario si sigue pendiente
        Core-->>Adapter: No confirmado, no emitir entradas
        Adapter-->>Partner: Resultado de fulfillment
        Partner-->>Buyer: Informar fallo o reembolsar
    end
```

Este segundo modelo tiene un riesgo de coordinación: el pago del partner puede aprobarse y el hold expirar antes de que FunEvents reciba la confirmación. El contrato de integración debe definir tiempos límite, reintentos idempotentes, consulta del estado del pedido y el proceso de reembolso/compensación. La reserva no debe confirmarse solo porque el partner indique un cobro si el hold ya venció.

### Decisiones pendientes para la plataforma completa

Estas decisiones afectan a la arquitectura de producción, pero quedan fuera del alcance del prototipo de reserva:

1. ¿Qué medios y proveedores de pago ofrecerá FunEvents por mercado, y quién será el comerciante responsable en cada canal?
2. Para los partners que cobran directamente, ¿cómo se reportarán pagos, reembolsos y contracargos, y qué mecanismo de liquidación/reconciliación se requiere?
3. ¿Qué nivel de contención de inventario se espera durante los picos de venta y qué objetivos de latencia/disponibilidad se deben cumplir?
4. ¿Hay requisitos de auditoría o reconstrucción histórica que justifiquen event sourcing, más allá del historial de pedidos y una auditoría convencional?

## 5. Parte 2: Prototipo técnico

### Objetivo y alcance

El prototipo demuestra un recorrido vertical mínimo: un cliente de consola solicita, a través de una API ASP.NET Core, una reserva para un evento, sesión y usuario conocidos. La API valida los códigos, reserva inventario de forma atómica, persiste la operación y devuelve el estado y la expiración. El propósito es validar el contrato API, la persistencia y la consistencia de la reserva; no implementar la plataforma completa ni los flujos de pago y conciliación de partners.

El cliente no accede directamente a la base de datos. La reserva no realiza cobro y queda pendiente hasta confirmarse o expirar. PostgreSQL es la base propuesta para conservar el estado entre llamadas.

#### Arquitectura del prototipo

```mermaid
flowchart LR
    CONSOLE[Cliente de consola] -->|HTTP JSON| API[ASP.NET Core Minimal API]
    API --> USECASE[Servicio de reservas]
    USECASE --> VALIDATE[Validación de evento, sesión y usuario]
    USECASE --> INVENTORY[Control transaccional de inventario]
    USECASE --> RESERVATION[Creación y consulta de reservas]
    VALIDATE --> REPO[Interfaces de persistencia]
    INVENTORY --> REPO
    RESERVATION --> REPO
    REPO --> DB[(PostgreSQL)]
    API --> OPENAPI[OpenAPI / Swagger]
```

El cliente solo conoce la API HTTP. El servicio de reservas coordina validaciones y persistencia; el repositorio oculta el acceso a PostgreSQL. Inventario e inserción de reserva deben confirmarse en una sola transacción.

#### Flujo de reserva

```mermaid
sequenceDiagram
    actor User as Usuario
    participant Console as Cliente de consola
    participant API as Minimal API
    participant Service as Servicio de reservas
    participant DB as PostgreSQL

    User->>Console: Introduce evento, sesión, usuario y cantidad
    Console->>API: POST /api/v1/reservations con clave de idempotencia
    API->>Service: Validar y crear reserva
    Service->>DB: Comprobar códigos e idempotencia
    DB-->>Service: Resultado de validación
    alt Datos válidos e inventario disponible
        Service->>DB: Transacción: descontar inventario y guardar reserva
        DB-->>Service: Reserva creada y expiración
        Service-->>API: Reserva pendiente
        API-->>Console: 201 Created con código y expiración
        Console-->>User: Muestra confirmación
    else Código inválido, solicitud repetida o sin inventario
        Service-->>API: Error de validación, resultado idempotente o conflicto
        API-->>Console: 404, respuesta previa o 409 Conflict
        Console-->>User: Muestra el resultado
    end
```

### 5.1 API

Base propuesta: `/api/v1`.

| Método y ruta | Uso | Respuesta esperada |
|---|---|---|
| `GET /events/{eventCode}` | Validar/consultar el evento | Datos básicos del evento |
| `GET /events/{eventCode}/sessions` | Consultar sesiones disponibles | Sesiones, zonas/precios y disponibilidad resumida |
| `POST /reservations` | Crear una reserva | `201 Created`, reserva y expiración |
| `GET /reservations/{reservationCode}` | Consultar la reserva | Estado, evento, sesión, cantidad y expiración |
| `POST /reservations/{reservationCode}/cancel` | Cancelar y liberar inventario | Estado actualizado |

Petición de ejemplo:

```json
{
  "eventCode": "EVT-2026-001",
  "sessionCode": "SES-2026-001-A",
  "userCode": "USR-1042",
  "quantity": 2,
  "channelId": "fun-events-web",
  "idempotencyKey": "c6d1c491-4bca-476c-b450-f676ce01b61f"
}
```

El colaborador se determina preferiblemente por la credencial autenticada; `channelId` es un dato de contexto y debe coincidir con esa identidad, no ser una forma de suplantación.

Errores con `ProblemDetails` y códigos HTTP coherentes: `400` entrada inválida, `401/403` autenticación/autorización, `404` evento/sesión/usuario inexistente, `409` inventario insuficiente o conflicto de idempotencia, `410` reserva vencida y `500` fallo inesperado. La API tendrá OpenAPI/Swagger y versionado explícito.

### 5.2 Modelo de datos mínimo

- `Event(event_code, name, status)`
- `Session(session_code, event_code, starts_at, venue_id)`
- `Inventory(session_code, zone_code, capacity, available, price)`
- `User(user_code, display_name)` (datos mínimos o semilla de prueba en el prototipo)
- `Channel(channel_id, type, status)`
- `Reservation(reservation_code, user_code, session_code, channel_id, quantity, status, expires_at, idempotency_key, created_at)`

Restricciones e invariantes:

- `quantity > 0` y `available >= 0`.
- La actualización de disponibilidad y la inserción de reserva ocurren en una única transacción.
- Usar actualización condicional/`SELECT ... FOR UPDATE` o mecanismo equivalente para impedir carreras.
- Índice único para idempotencia dentro del ámbito definido (por canal/cliente autenticado).
- Los estados mínimos de reserva son `Pending`, `Confirmed`, `Cancelled` y `Expired`.

### 5.3 Cliente de consola

Aplicación .NET 8+ que recibe argumentos o solicita interactivamente `eventCode`, `sessionCode`, `userCode`, cantidad y canal. Llama a la API con `HttpClient`, muestra código/estado/expiración de la reserva y presenta errores HTTP de forma legible. La URL base y credenciales se configuran fuera del código (configuración/variables de entorno); no se incluyen secretos en el repositorio.

El cliente genera o acepta una clave de idempotencia para que un reintento de red no duplique la reserva. No accede directamente a PostgreSQL.

### 5.4 Tecnología y despliegue

- .NET 8 o superior, ASP.NET Core Minimal APIs.
- PostgreSQL para persistencia transaccional.
- EF Core o Npgsql con SQL explícito para la operación crítica de inventario; escoger una sola estrategia en la implementación.
- Docker Compose para ejecutar API y base de datos localmente. .NET Aspire es opcional para orquestación local, telemetría y descubrimiento de servicios; no es requisito del prototipo.
- OpenTelemetry para logs, métricas y trazas; health checks de vida y disponibilidad.
- En el prototipo, carga de datos de demostración reproducible mediante migración/seed.

### 5.5 Decisiones pendientes antes de implementar

1. ¿La reserva selecciona una sesión y zona concreta, o el sistema puede asignar automáticamente la mejor disponibilidad?
2. ¿La cantidad máxima por reserva y la duración del bloqueo deben ser fijas o configurables por evento?
3. ¿El prototipo debe incluir autenticación real desde el inicio o basta una API key de desarrollo claramente aislada?
4. ¿Se requiere persistir códigos de usuario/evento de prueba en PostgreSQL o cargar un conjunto semilla?
5. ¿El prototipo debe cubrir solo reserva/cancelación o simular también el pago y la emisión de entradas?
6. ¿Qué nivel de concurrencia se desea simular para comprobar el comportamiento de la reserva?

### 5.6 Criterios de aceptación (propuesta)

- Con códigos válidos y disponibilidad suficiente, la consola obtiene una reserva pendiente con expiración.
- Un evento, sesión o usuario desconocido produce un error claro sin modificar inventario.
- La cantidad inválida se rechaza.
- La disponibilidad nunca queda negativa ante reservas concurrentes.
- Repetir una solicitud con la misma clave de idempotencia no duplica la reserva.
- Cancelar o expirar una reserva pendiente libera su inventario una sola vez.
- La API expone documentación OpenAPI y puede ejecutarse localmente junto con PostgreSQL.
