# AlineaApp — Contrato de implementación FASE 00

**Estado:** listo para entregar a Codex  
**Fase:** 00 — Fundaciones técnicas y plataforma local  
**Producto:** AlineaApp  
**Objetivo del piloto:** una persona autorizada utiliza la aplicación, pero el diseño debe soportar múltiples usuarios y eventual registro público.

## 1. Rol y modo de trabajo

Actúa como un **ingeniero senior full-stack, arquitecto pragmático y revisor de seguridad**. Antes de tocar archivos, inspecciona el repositorio, herramientas y versiones disponibles. Si está vacío, crea una base limpia. Prioriza código sencillo, mantenible, probado y desplegable en Linux ARM64. No sobreingenierices.

**En esta fase NO implementes todavía el módulo completo de finanzas, IA, SMS real, notificaciones ni despliegue público.** La meta es un proyecto funcional local con frontend, backend, PostgreSQL, infraestructura, documentación y criterios de aceptación demostrables. Evita pantallas de muestra desconectadas de endpoints reales.

Toma este contrato como objetivo. Si una decisión técnica concreta no está definida, registra la opción elegida y su justificación en `docs/DECISIONS/` antes o junto a la implementación. No inventes requisitos de negocio.

## 2. Contexto funcional obligatorio

- Aplicación: **AlineaApp**, gestión de finanzas personales, futura apertura al público.
- Piloto: registro restringido a teléfonos en lista de permitidos. El primer teléfono autorizado será `[E164_CONFIGURADO_FUERA_DEL_REPOSITORIO]`, **configurado de forma segura fuera del repositorio** cuando se habilite registro real; no quemarlo en código, pruebas ni migraciones compartidas.
- `CONFIG_GENERAL` será una configuración persistente clave-valor administrada por la aplicación.
- Clave `ACCESO_PROTEGIDO`: `YES` = **registro restringido** a teléfonos permitidos; `NO` = registro público. En ambos estados es obligatoria la autenticación. No crear un segundo interruptor que pueda contradecir esta regla.
- Tabla futura `TELEFONOS_PERMITIDOS`: teléfonos en formato E.164, habilitación/revocación y auditoría básica.
- Identificador primario funcional de usuario: teléfono verificado por OTP SMS. Perfil adicional: nombre y correo opcional. El envío real y el flujo OTP son **Fase 01**, no Fase 00.
- Configuración monetaria: `MONEDA_PREDETERMINADA=USD` (código ISO 4217). Mostrar `$` y gestionar montos con `decimal`, nunca `float`/`double`.
- Inicio financiero planeado: fecha de corte y saldos iniciales por cuenta; no es necesario cargar 30 días previos. Programar ingresos y compromisos futuros sin duplicarlos como movimientos reales.
- Los bancos, cuentas, tarjetas, categorías, ingresos, gastos, transferencias y compromisos serán módulos incrementales. IA, reservas, envíos generales y automatizaciones vendrán después.
- El diseño debe ser multiusuario desde el principio, con aislamiento en servidor; **no crear aún un sistema complejo de organizaciones o permisos**.
- Idioma UI inicial: español; zona horaria de negocio: `America/Panama`; almacenar fechas de eventos en UTC y manejar fechas de calendario explícitamente.

## 3. Stack aprobado

- Frontend: **React + TypeScript + Vite**, TypeScript estricto, interfaz responsive mobile-first.
- Backend: **ASP.NET Core 10 LTS / C#**, API REST en un monolito modular pragmático.
- Persistencia: **PostgreSQL** + **EF Core / Npgsql** compatibles con .NET 10.
- Infraestructura: **Docker Compose** con imágenes compatibles con **linux/arm64**; desarrollo también posible desde Windows.
- Publicación futura: **Cloudflare Tunnel** en la Raspberry Pi 5, pero sin modificar el túnel ni exponer el servicio públicamente en esta fase.
- Autenticación futura: **ASP.NET Core Identity** (o justificación documentada de implementación equivalente), OTP SMS, sesiones web seguras con cookies `HttpOnly`, `Secure`, `SameSite` adecuado, protección CSRF cuando corresponda; no JWT en `localStorage`.
- No introducir Redis, MongoDB, microservicios, colas externas ni Clean Architecture ceremonial.
- Preferir paquetes de versión estable vigente y compatibles entre sí; registrar versiones concretas al crear el proyecto. No asumir que el equipo tiene herramientas instaladas: inspeccionar y reportar.

## 4. Estructura esperada

Proponer una estructura equivalente a:

```text
AlineaApp/
├── AGENTS.md
├── README.md
├── .env.example
├── .gitignore
├── docs/
│   ├── PRODUCT_VISION.md
│   ├── ARCHITECTURE.md
│   ├── DOMAIN_RULES.md
│   ├── DESIGN_SYSTEM.md
│   ├── ROADMAP.md
│   └── DECISIONS/
├── .agents/skills/
│   ├── auth-security/SKILL.md
│   ├── frontend-design/SKILL.md
│   ├── react-typescript/SKILL.md
│   ├── dotnet-backend/SKILL.md
│   ├── database-finance/SKILL.md
│   └── quality-review/SKILL.md
├── frontend/
├── backend/
├── infra/
│   └── docker/
└── tests/
```

Puedes ajustar nombres de carpetas cuando exista una razón técnica concreta. Evita múltiples proyectos .NET vacíos para simular arquitectura. Crea skills breves, accionables, con condiciones de uso, listas de comprobación y referencias a los documentos del repositorio. Verifica que Codex pueda descubrirlas en su entorno.

## 5. Alcance exacto de Fase 00

### 5.1 Repositorio y estándares

1. Inicializar estructura y `.gitignore` seguro (`.env`, secretos, certificados, `node_modules`, binarios, directorios de compilación y volúmenes de BD).
2. Crear `AGENTS.md` con reglas globales: analizar antes de modificar; mínimo cambio correcto; tests por riesgo; no credenciales; no UI genérica; no dependencias innecesarias; documentar validaciones y pendientes.
3. Crear documentación concisa que refleje este contrato, diagrama lógico de arquitectura y decisiones relevantes.
4. Incluir `README.md` operativo con requisitos, comandos, configuración y solución de problemas.

### 5.2 Backend real y verificable

1. Crear una API ASP.NET Core 10 con estructura por funcionalidades (feature folders o módulos pequeños), DI, configuración tipada y validación temprana de opciones importantes.
2. Exponer `GET /health` para salud básica. Si agregas readiness contra PostgreSQL, utilizar ruta separada y distinguir las dependencias.
3. Configurar EF Core/Npgsql y un `DbContext`, migraciones versionadas y al menos una entidad persistente de configuración `CONFIG_GENERAL` con `Clave` única y `Valor`. Su inicialización debe ser **idempotente**, no destructiva y sin secretos.
4. Valor inicial `MONEDA_PREDETERMINADA=USD` y `ACCESO_PROTEGIDO=YES`. Estos ajustes **no pueden modificarse desde endpoints públicos**. No exponer endpoint administrativo de escritura sin autenticación/autorización.
5. Tratar fallos con respuestas controladas y logs estructurados sin secretos; incluir validaciones de entrada y una política explícita de CORS si las URL de desarrollo lo requieren.
6. No implementar aún registro/inicio de sesión real ni falsos usuarios en producción. Un endpoint de prueba no debe simular un usuario autenticado.

### 5.3 Frontend real y sobrio

1. React/Vite con TS estricto; arquitectura por funcionalidades sin complejidad innecesaria.
2. Crear base de UI en español: layout responsive, estados loading/error/empty, página inicial indicando el estado de inicialización y consulta real a `GET /health` (y/o un estado público explícitamente seguro de la API). **No mostrar balances financieros inventados**.
3. Configurar API base URL sin secretos y un cliente HTTP centralizado.
4. Documentar tokens de diseño (tipografía, escala de espaciado, colores semánticos, focus, hover, active, disabled, invalid). No imponer estética de dashboard genérico de IA, tarjetas y degradados decorativos ni iconografía excesiva. Mantener diseño refinado, usable y mobile-first; identidad visual definitiva se validará después.
5. Añadir pruebas mínimas apropiadas para inicialización y estado de API, si la complejidad lo permite.

### 5.4 Docker y desarrollo local

1. `docker compose` con PostgreSQL, API y frontend (o frontend servido en web server ligero), redes internas, persistencia en volumen y healthchecks razonables.
2. PostgreSQL nunca publicado hacia Internet; para desarrollo, si se expone un puerto al host, ligar a `127.0.0.1` y documentarlo.
3. Configurar secretos mediante `.env` local / variables de entorno; `.env.example` solo contiene nombres y valores ficticios no sensibles.
4. No abrir puertos del router, no tocar configuración global existente del Raspberry Pi ni desplegar en Cloudflare en esta fase.
5. Verificar compatibilidad ARM64 de imágenes y ruta de build; si no puede verificarse por falta de entorno, reportarlo con comando exacto pendiente.
6. Preparar procedimiento de backup/restore de PostgreSQL documentado, sin afirmar que hay copias reales hasta probarlo. Diseñar comprobación y restauración posterior antes del despliegue.

### 5.5 Pruebas y seguridad

1. Ejecutar los tests que se hayan creado, build del backend, compilación/typecheck del frontend y validación de `docker compose config`.
2. Si Docker está disponible, levantar el stack y demostrar que frontend, API y PostgreSQL responden, que la migración funciona y que `CONFIG_GENERAL` contiene los valores iniciales correctos.
3. No introducir datos personales reales en seeds, tests, capturas ni logs.
4. No asumir que Cloudflare Tunnel equivale a autenticación; planificar defensa en profundidad.
5. No ejecutar migraciones destructivas ni borrar volúmenes existentes automáticamente.

## 6. Principios del futuro dominio financiero (NO construir todo hoy)

- Una cuenta tiene saldo de apertura y fecha de corte; el saldo se obtiene de movimientos posteriores a la apertura (o de un libro de movimientos consistente), sin duplicar saldo inicial.
- Ingresos, gastos y transferencias deben tener semánticas distintas; una transferencia no es ingreso ni gasto.
- Deudas y tarjetas de crédito no se confunden con efectivo disponible; el pago de una tarjeta no duplica gastos ya contabilizados.
- Compromisos programados son previsiones, no transacciones liquidadas. La confirmación posterior debe evitar duplicidad.
- Todas las consultas y escrituras financieras deben filtrar por usuario o espacio autorizado desde servidor.
- Usar columnas monetarias `numeric(18,2)` inicialmente o decisión equivalente justificada; `decimal` en C#.
- Los saldos deterministas son autoridad del sistema; la IA futura recomienda, no decide la contabilidad.

## 7. Criterios de aceptación de Fase 00

- [ ] El repositorio abre correctamente y sus carpetas tienen una razón de ser.
- [ ] El backend compila, tiene `/health`, conecta a PostgreSQL y aplica migraciones sin scripts manuales ocultos.
- [ ] `CONFIG_GENERAL` se inicializa con `ACCESO_PROTEGIDO=YES` y `MONEDA_PREDETERMINADA=USD` sin duplicados al reiniciar.
- [ ] No existe endpoint público para editar la configuración de seguridad.
- [ ] El frontend compila y presenta una interfaz responsive con consulta real a la API, estados de carga y error.
- [ ] `docker compose config` funciona; si existe Docker, el stack se levanta y se prueba de extremo a extremo.
- [ ] `.env.example`, `.gitignore`, `README.md`, `AGENTS.md` y los Skills existen y son coherentes.
- [ ] No hay contraseñas, OTP ni teléfonos reales incrustados en código o versionados.
- [ ] Las verificaciones ejecutadas y las limitaciones se reportan con precisión; no afirmar pruebas que no se realizaron.

## 8. Entrega obligatoria de Codex

Al terminar, responder con:

1. Resumen de arquitectura real y árbol breve de archivos relevantes.
2. Versiones exactas instaladas y comandos para levantarlo localmente en Windows y Docker/Linux ARM64.
3. Migraciones creadas, valores de configuración y cómo se verificaron.
4. Tabla `Prueba | Resultado | Evidencia` y lista precisa de comprobaciones pendientes.
5. Riesgos técnicos identificados y decisiones registradas.
6. Próxima fase recomendada: **FASE 01 — registro restringido y autenticación OTP por SMS**, sin implementarla anticipadamente.

**Regla de parada:** no hacer commits, pushes, deployments ni modificaciones de Cloudflare/Raspberry Pi sin instrucción explícita del usuario. No instalar servicios en el host si los contenedores bastan. Si detectas un bloqueo real, entrega el código y documenta exactamente el bloqueo en lugar de simular éxito.
