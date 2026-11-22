# Fase 02A — adaptador Brevo SMS

Fecha: 2026-11-22, America/Panama. Alcance autorizado en la conversación: integrar Brevo con la autenticación existente, probar HTTP simulado y documentar activación. Sin SMS reales, acceso a la cuenta Brevo, cambios de diseño/finanzas, migraciones, commits, push ni despliegues.

Contexto comunicado por el propietario, **no consultado mediante API**: clave denominada `AlineaApp-OTP` con permisos SMS Read/write, Sender ID `AlineaApp` solicitado para Panamá y pendiente, saldo de créditos cero. No se solicitó, leyó ni guardó esa credencial.

## Implementación y decisión

`Features/Auth/BrevoSmsSender.cs` implementa `ISmsSender` con cliente nombrado de `IHttpClientFactory`, sin SDK adicional ni paquetes nuevos. Usa el endpoint oficial actual **POST `https://api.brevo.com/v3/transactionalSMS/send`**, header `api-key`, teléfono internacional, Sender ID configurable, contenido OTP corto en español sin caracteres Unicode, `type=transactional` y `unicodeEnabled=false`. No crea contactos, campañas, webhooks ni envíos de prueba.

Decisión de proveedor: Brevo seleccionado expresamente por el propietario. No se presupone coste cero: antes de activar deberá confirmar tarifa vigente para Panamá, créditos suficientes y aprobación del remitente. No se compran créditos ni contrata ningún servicio en esta fase.

Solo un **201 con messageId numérico positivo** significa aceptación. No demuestra entrega al teléfono: el endpoint es asíncrono. HTTP inesperado, redirección, 400/402 por créditos, permisos 401/403, 429, 5xx, JSON inválido, aceptación incompleta, respuesta excesiva o timeout devuelven fallo. Se reconoce el código oficial `not_enough_credits` sin mostrar el mensaje del proveedor. Sin reintentos automáticos: una respuesta incierta podría corresponder a un SMS ya aceptado y repetirlo duplicaría mensajes/cargos.

Timeout total de tres segundos, configurable entre uno y cinco; cubre petición y lectura del cuerpo. Cuerpo limitado a 4096 bytes. Cancelación del cliente se propaga y la transacción existente se revierte. No se siguen redirecciones ni se almacenan cookies HTTP. Logging automático del cliente eliminado y headers redactados; solo se registran categoría/estado HTTP, nunca API Key, destino, OTP, cuerpo del proveedor, messageId o mensajes de excepción.

Se mantiene el servicio OTP existente: un fallo deja `Eligible=false`; la verificación exige elegibilidad incluso si se presenta el código correcto. Se auditan también los fallos que devuelven false, además de excepciones. No se cambian duración/consumo OTP, CSRF, cuotas, cooldown, sesiones, autorización ni lista de permitidos. `DevelopmentMailbox` sigue prohibido en Production.

## Configuración externa

| Configuración ASP.NET / variable directa | Variable usada por Compose | Valor o uso |
|---|---|---|
| `Sms__Provider` | `SMS_PROVIDER` | `Disabled` por defecto; `Brevo` para seleccionar el adaptador |
| `Sms__Brevo__ApiKey` | `BREVO_API_KEY` | Clave privada cargada desde secreto externo; sin valor en Git |
| `Sms__Brevo__SenderId` | `BREVO_SMS_SENDER_ID` | `AlineaApp` cuando esté aprobado para Panamá |
| `Sms__Brevo__SendingEnabled` | `BREVO_SMS_SENDING_ENABLED` | `false` por defecto; bloqueo adicional de envío |
| `Sms__Brevo__TimeoutSeconds` | `BREVO_SMS_TIMEOUT_SECONDS` | `3` por defecto; rango 1–5 |

Seleccionar Brevo con SendingEnabled=false no realiza ninguna petición al proveedor. `/api/auth/status` indica `unavailable` y solicitar OTP devuelve 503 antes de entrar al servicio OTP. Con ambos activados indica `sms`: es configuración habilitada, **no una comprobación de saldo, aprobación o entrega**. Ante fallo de envío, se conserva la respuesta pública uniforme 202 del flujo para no revelar elegibilidad y el desafío no puede verificarse.

La configuración activa exige clave sin caracteres de control y Sender ID alfanumérico de 3–11 caracteres o numérico de 12–15. Errores de opciones son genéricos y no muestran valores. Una configuración bloqueada admite clave/remitente vacíos. `.env.example` mantiene `SMS_PROVIDER=Disabled` y envío false. **La configuración local real no se modifica.** Las claves OTP, conexión y demás secretos existentes se conservan.

Cuando exista aprobación del Sender ID, créditos y autorización expresa para SMS real: cargar la clave mediante variables del proceso, un gestor de secretos o archivo local ignorado, establecer proveedor/remitente y solo entonces habilitar el bloqueo. No imprimir `.env`, `docker compose config` expandido, headers o trazas de cuerpos. No introducir la clave en `VITE_*`. En Compose las variables de la tabla se trasladan a la API; en ejecución nativa usar los nombres ASP.NET. Esta documentación no ejecuta ni autoriza activación o despliegue.

## Verificación específica

SDK .NET 10.0.404 disponible en Windows; Docker Engine no responde. Sin iniciar Docker, servicios o bases de datos.

```powershell
dotnet restore tests/Alinea.Api.Tests/Alinea.Api.Tests.csproj --locked-mode
dotnet test tests/Alinea.Api.Tests/Alinea.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~BrevoSms" --logger "console;verbosity=minimal"
```

Las pruebas sustituyen el transporte por `HttpMessageHandler` en memoria antes de resolver el proveedor. Ningún test usa credenciales reales, destinatarios reales ni sockets hacia Brevo. Cubren contrato de petición, aceptación, HTTP/créditos/permisos/rate limit, fallo de red, timeout de cabeceras y lectura, cancelación, bloqueo de envío, opciones, protección de logs y selección DI. El test HTTP del endpoint con bloqueo usa una conexión deliberadamente inaccesible para demostrar que responde 503 sin depender de PostgreSQL. No se ejecuta la suite completa.

Resultado ejecutado: **29/29 PASS, 0 fallos, 0 omitidos**. Restore con lock y compilación correctos, sin warnings ni errores. Los seis casos PostgreSQL siguientes compilan pero no se contabilizan como ejecutados. `git diff --check` termina 0. Ninguna migración ejecutada y ninguna modificación en archivos de frontend.

También se añaden seis casos de integración en `AuthTests.Brevo_failure_blocks_verification_even_with_the_correct_generated_otp`, usando HTTP simulado y PostgreSQL **real de pruebas**. Demuestran que un fallo Brevo impide verificar el código correcto. Su ejecución queda pendiente mientras Docker/PostgreSQL de pruebas no estén disponibles; no se reemplaza persistencia por InMemory ni se conecta a la base del piloto. Cuando haya una base exclusiva de pruebas y `ALINEA_TEST_CONNECTION` configurada, ejecutar únicamente:

```powershell
dotnet test tests/Alinea.Api.Tests/Alinea.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Brevo_failure_blocks_verification_even_with_the_correct_generated_otp"
```

## Pendientes para activar

- Aprobación efectiva del Sender ID para Panamá y créditos suficientes, confirmados por el propietario.
- Cargar el secreto externamente y autorización expresa para un primer envío real; probar registro/login con el teléfono autorizado sin publicar datos personales.
- Ejecutar los seis casos PostgreSQL indicados cuando el entorno de pruebas esté disponible.
- Medir entrega/latencia y cargos reales en esa prueba autorizada. No hay evidencias de entrega real en Fase 02A.

Referencias oficiales consultadas: [endpoint SMS transaccional](https://developers.brevo.com/reference/send-async-transactional-sms), [guía de envío](https://developers.brevo.com/docs/transactional-sms-endpoints), [códigos de error](https://developers.brevo.com/docs/how-it-works), [registro de Sender ID](https://help.brevo.com/hc/en-us/articles/28255350696466-Register-a-Sender-ID-to-send-SMS-messages).
