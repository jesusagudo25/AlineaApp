# AlineaApp · Fase 00

Base local: React/TypeScript/Vite → Nginx → ASP.NET Core 10 → PostgreSQL. UI en español conectada a `/health` y `/health/ready`. Sin login, SMS, datos financieros, IA ni despliegue público.

`AlineaApp.slnx` agrupa únicamente la API y sus tests. Para abrir el proyecto, usar esta raíz en el editor o la solución en un IDE compatible con .NET 10.

## Arranque local con Docker (Windows o Linux ARM64)

Requisitos: Docker Engine Linux y Compose con BuildKit. En Windows, Docker Desktop iniciado y contenedores Linux. No requiere SDK .NET, Node ni PostgreSQL en el host. Imágenes multiarquitectura; sin `platform: amd64` forzado.

Desde la raíz, PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File infra/init-env.ps1
docker compose config --quiet
docker compose up -d --build --wait
Invoke-RestMethod http://127.0.0.1:8080/health
Invoke-RestMethod http://127.0.0.1:8080/health/ready
```

El script crea `.env` ignorado con una contraseña aleatoria y no lo sobrescribe. `Bypass` solo se aplica a ese proceso; no cambia la política global. Alternativa: copiar `.env.example` a `.env` y reemplazar el marcador con un secreto aleatorio antes de levantar. Usar hex/base64url sin `;`, saltos ni `$` para no alterar la cadena Npgsql o interpolación Compose.

El contrato original recibido contiene un teléfono real: se conserva localmente e ignorado por Git. La copia `docs/contracts/FASE-00-FUNDACIONES-ALINEAAPP.public.md` conserva el alcance y reemplaza ese dato para compartirlo con seguridad.

Linux/Raspberry ARM64 **solo local, sin despliegue ni túnel**:

```sh
cp .env.example .env
# Editar .env: sustituir POSTGRES_PASSWORD con un secreto aleatorio local.
docker compose config --quiet
docker compose up -d --build --wait
curl --fail http://127.0.0.1:8080/health/ready
```

Abrir `http://127.0.0.1:8080`. El único puerto por defecto está ligado a loopback; PostgreSQL no publica puerto. Si ya existe `.env` conservarlo. No usar `docker compose config` sin `--quiet` en logs compartidos: la salida expandida contiene secretos. `docker compose stop` detiene sin eliminar datos; nunca usar `down -v` para resolver errores.

## Desarrollo nativo en Windows (opcional)

Requiere SDK .NET 10 LTS y Node 22.12+ de la línea 22 (inspección: Node 22.23.1). En esta entrega el SDK se usó dentro de Docker, no se instaló en Windows.

```powershell
# Solo PostgreSQL, accesible en loopback:
docker compose -f compose.yml -f infra/docker/compose.dev.yml up -d postgres
# Cargar .env en ESTA sesión sin imprimir valores:
Get-Content .env | Where-Object { $_ -match '^[A-Z_]+=' } | ForEach-Object {
  $taskName, $taskValue = $_ -split '=', 2
  [Environment]::SetEnvironmentVariable($taskName, $taskValue, 'Process')
}
$env:Database__ConnectionString = "Host=127.0.0.1;Database=$env:POSTGRES_DB;Username=$env:POSTGRES_USER;Password=$env:POSTGRES_PASSWORD;Timeout=3"
dotnet restore backend/Alinea.Api
dotnet run --project backend/Alinea.Api -- --migrate
dotnet run --project backend/Alinea.Api -- --urls http://127.0.0.1:5080
# Otra terminal:
cd frontend
npm ci
npm run dev
```

Vite usa `http://127.0.0.1:5173` y reenvía `/health` a 5080; por defecto no necesita CORS. `frontend/.env.example` documenta `VITE_API_BASE_URL` pública y `API_PROXY_TARGET` de desarrollo. No poner secretos en variables `VITE_*`. Para frontend separado configurar orígenes exactos con `Cors__AllowedOrigins__0`, nunca `*` ni bypass de autenticación.

## Migraciones y verificación

`Infrastructure/Migrations/` contiene `InitialConfiguration` y snapshot. `CONFIG_GENERAL` tiene PK `Clave`, `Valor` obligatorio; seed idempotente inserta `ACCESO_PROTEGIDO=YES` y `MONEDA_PREDETERMINADA=USD` sin sobrescribir. Compose local habilita migración al iniciar; ejecución nativa usa `--migrate`. No hay endpoint para editar estos valores.

```powershell
docker compose --profile tests run --build --rm backend-tests
cd frontend
npm ci
npm run build
npm test
npx playwright install chromium
npm run test:e2e
```

El perfil de tests crea PostgreSQL separado en tmpfs; no apunta al volumen de la app. Para ejecutar tests nativos, `ALINEA_TEST_CONNECTION` debe apuntar a una base **exclusiva de pruebas** (los tests modifican temporalmente configuración). No usar la base de la app ni una base compartida. Sin esa variable los tests de integración fallan explícitamente.

Smoke operativo con recuperación garantizada en `finally` (detiene temporalmente PostgreSQL local): `powershell -NoProfile -ExecutionPolicy Bypass -File infra/verify-local.ps1 -ExerciseOutage`. Sin el switch solo consulta salud. Usa el puerto 8080 predeterminado.

Con SDK nativo: `dotnet build backend/Alinea.Api` y `dotnet test tests/Alinea.Api.Tests`. Para generar migraciones: `dotnet tool restore`, `dotnet ef migrations add Nombre --project backend/Alinea.Api --output-dir Infrastructure/Migrations`; la factory de diseño no usa secretos ni conecta. Revisar SQL con `dotnet ef migrations script --project backend/Alinea.Api`; aplicar únicamente con la configuración real y el comando `--migrate` de la app. Prohibido usar `EnsureCreated` o borrar bases para evitar migraciones.

Validar build ARM64 sin publicar ni cargar en el stack local:

```sh
docker buildx build --platform linux/arm64 -t alinea-phase00-api:arm64 --load -f infra/docker/api.Dockerfile .
docker buildx build --platform linux/arm64 -t alinea-phase00-web:arm64 --load -f infra/docker/frontend.Dockerfile .
```

Las etiquetas ARM64 quedan separadas del stack amd64; no se publican. La ejecución final en Pi requiere validación separada. [Evidencias y versiones exactas](docs/VERIFICATION.md), [backup y restore](docs/BACKUP_RESTORE.md), [decisiones](docs/DECISIONS/0001-fundaciones.md), [arquitectura](docs/ARCHITECTURE.md).

## Problemas frecuentes

- Docker no conecta: iniciar Docker Desktop y comprobar `docker info`; Windows debe usar Linux containers. No instalar otro PostgreSQL para compensar un daemon detenido.
- Puerto ocupado: cambiar `WEB_PORT` en `.env`; en desarrollo comprobar 5432/5080/5173.
- Cambiar password en `.env` no cambia la contraseña de una base ya inicializada. Conservar el secreto original o rotarlo de forma explícita; nunca borrar el volumen.
- API no saludable: `docker compose ps`, `docker compose logs --tail 50 api`; comprobar BD y migración. No compartir dumps de configuración ni inspecciones de env. Readiness 503 puede indicar claves faltantes, valores inválidos o BD caída.
- UI no conecta: comprobar las dos rutas, proxy y base URL. La UI no presupone que una respuesta `/health` signifique que BD esté lista.
- La salida de procesos puede usar UTC; el negocio usa America/Panama. No ajustar el reloj ni configuración global del host.

Próxima fase: **FASE 01 — registro restringido y autenticación OTP por SMS**, con contrato y validaciones propios. Sin commits, pushes ni despliegues realizados por esta implementación.
