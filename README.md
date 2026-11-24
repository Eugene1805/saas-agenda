# saas-agenda

Microservicio **Agenda** de **Vital** (Plataforma SaaS Gestión Hospitalaria), implementado en **.NET 10** con **Arquitectura Hexagonal (Puertos y Adaptadores)**, persistencia en **PostgreSQL**, validación **JWT Bearer** emitida por `saas-identity`, bloqueo estricto de traslapes horarios en **GMT-6** (sin sobrecupo), documentación **OpenAPI / Scalar** y pruebas unitarias (**xUnit**) + integración HTTP (**Postman / Newman**).

## Arquitectura Hexagonal

```text
src/
  Saas.Agenda.Domain/          # Entidades (Appointment, ClinicSchedule, SpecialistSchedule, SpecialistScheduleException), políticas CU-18 y horarios GMT-6, Result<T>
  Saas.Agenda.Application/     # Puertos (IAppointmentRepository, IScheduleRepository, ITenantAccessStatePort, IAgendaDirectoryPort, IAgendaAccessControlPort) y Casos de Uso
  Saas.Agenda.Infrastructure/  # Adaptadores salientes: EF Core + Migraciones PostgreSQL multitenant (GiST antiempalme + horarios) y resolución CNPG por clínica
  Saas.Agenda.Api/             # Adaptadores entrantes: Minimal APIs HTTP (:8080), manejo 503 de BD por tenant, JWT Bearer, ProblemDetails y OpenAPI/Scalar
tests/
  Saas.Agenda.UnitTests/       # Pruebas unitarias xUnit de dominio, horarios GMT-6, políticas anti-huérfanas, casos de uso, métricas CU-16 y resolución CNPG (60 pruebas)
  Saas.Agenda.IntegrationTests/# Pruebas multi-PostgreSQL 17 con Testcontainers / BDs reales, GiST aislado por clínica y KPI plataforma
  postman/                     # Colección (26 requests secuenciales) y plantilla de entorno para Postman / Newman (`Vital.Agenda.postman_collection.json`)
```

Regla de dependencias: `Api -> Application -> Domain` y `Infrastructure -> Application -> Domain`. El núcleo (`Domain` + `Application`) no depende de frameworks HTTP, Entity Framework ni PostgreSQL.

## Aislamiento por clínica (ADR-0002 Alternativa B)

Cada clínica tiene su propia instancia PostgreSQL 17 (`vital_agenda_<clinic>`) con esquema privado `agenda` (incluyendo `btree_gist` y exclusión antiempalme aislada por instancia) sin credenciales embebidas en el repositorio:

- **Kubernetes / CloudNativePG:** `TenantConnectionResolver` resuelve secretos montados en `TenantDatabases:SecretsDirectory` (`<tenantId>/uri`, `<tenantId>/{host,port,dbname,username,password,sslmode}` o `<tenantId>.connection`).
- **Docker Compose / Pruebas:** se configura `TenantDatabases__Connections__<tenantId>`. La métrica global `GET /v1/metrics/platform/activity` (`SUPER_ADMIN`) agrega conteos iterando únicamente los tenants configurados y fallando cerrado con `503` si una BD requerida es inaccesible.

## Arranque rápido con Docker

```powershell
cd saas-agenda
Copy-Item .env.example .env
# Define contraseñas locales y JWT_SIGNING_KEY (misma clave que saas-identity) en .env
docker compose up -d --build
```

- API HTTP Agenda: `http://127.0.0.1:8083` (usuario no root `1654`)
- Health checks: `http://127.0.0.1:8083/health`, `http://127.0.0.1:8083/health/live` (liveness) y `http://127.0.0.1:8083/health/ready` (readiness con todas las BDs configuradas)
- Documento OpenAPI: `http://127.0.0.1:8083/openapi/v1.json`
- Documentación interactiva Scalar: `http://127.0.0.1:8083/scalar/v1`
- Instancias PostgreSQL 17 locales (loopback `127.0.0.1`, esquema `agenda`):
  - **Clínica Norte (`vital-agenda-norte-db`):** `127.0.0.1:5434` (`vital_agenda_norte`)
  - **Clínica Valle (`vital-agenda-valle-db`):** `127.0.0.1:5441` (`vital_agenda_valle`)
  - **Clínica Aurora (`vital-agenda-aurora-db`):** `127.0.0.1:5442` (`vital_agenda_aurora`)
- Variables clave en `.env` / Docker:
  - `Jwt__Issuer=saas-identity`, `Jwt__Audience=vital-saas`, `JWT_SIGNING_KEY`: alineados con `saas-identity` para validar tokens JWT reales sin llamada en cada request.
  - `PLATFORM_MODE`, `DIRECTORY_MODE`, `ACCESS_CONTROL_MODE`: en `Local` para Incremento 1 y 2 (preparados para `Grpc` en Incremento 3).

## Endpoints implementados (Incremento 2: 14 rutas OpenAPI)

- `GET /health`
- `GET /health/live` (sonda Kubernetes liveness `process-only`)
- `GET /health/ready` (sonda Kubernetes readiness con verificación PostgreSQL)
- `GET /v1/appointments` (filtros: `specialistId`, `patientId`, `status`, `fromDateGmt6`, `toDateGmt6`; alcance por rol `CU-10` / `CU-18`)
- `GET /v1/appointments/{appointmentId}` (detalle con bitácora histórica cronológica)
- `POST /v1/appointments` (`PROGRAMADA` inicial, valida fecha futura, horario efectivo, sin sobrecupo; bloqueado a `ESPECIALISTA` y `SUPER_ADMIN`)
- `PUT /v1/appointments/{appointmentId}/reschedule` (bloquea fechas pasadas y estados terminales, valida jornada laboral y traslapes excluyendo la propia cita)
- `PATCH /v1/appointments/{appointmentId}/status` (máquina cerrada: `PROGRAMADA`, `CONFIRMADA`, `EN_CURSO`, `COMPLETADA`, `CANCELADA`, `NO_ASISTIO`; nota opcional)
- `GET /v1/schedules/clinic` y `PUT /v1/schedules/clinic` (horario general base de la clínica con protección `409` para especialistas que heredan)
- `GET /v1/schedules/specialists/{specialistId}` y `PUT /v1/schedules/specialists/{specialistId}` (configuración propia del especialista o respaldo clínico, editable por `ADMIN_CLINICA` y el propio `ESPECIALISTA`)
- `POST /v1/schedules/specialists/{specialistId}/exceptions` y `DELETE /v1/schedules/specialists/{specialistId}/exceptions/{exceptionId}` (días inhábiles y guardias especiales con protección `409` anti-huérfanas)
- `GET /v1/schedules/specialists/{specialistId}/availability` (bloques laborales, ocupados y huecos libres en `GMT-6`)
- `GET /v1/metrics/clinic/activity` (KPI `AGENDA_ACTIVITY` de `CU-16` para `ADMIN_CLINICA`)
- `GET /v1/metrics/platform/activity` (KPI global `APPOINTMENT_ACTIVITY` de `CU-16` para `SUPER_ADMIN` sin PII)

## Pruebas automatizadas

### 1. Pruebas unitarias xUnit dentro de contenedor .NET 10

```powershell
docker build --target test -t vital-agenda-test .
```

### 2. Pruebas de integración multi-PostgreSQL (.NET 10)

```powershell
dotnet test tests/Saas.Agenda.IntegrationTests/Saas.Agenda.IntegrationTests.csproj -c Release
```

### 3. Pruebas Postman con Newman contra el stack levantado

```powershell
docker run --rm --network saas-agenda_default `
  -v "${PWD}/tests/postman:/etc/newman:ro" `
  postman/newman:alpine run /etc/newman/Vital.Agenda.postman_collection.json `
  --env-var "baseUrl=http://agenda-api:8080" `
  --env-var "jwtSigningKey=TU_CLAVE_JWT_DE_ENV"
```

## CI y publicación de imagen en GHCR

El workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) replica el estándar de `saas-platform` y `saas-infra`:

- **PR y push a `develop`/`main`:**
  - `build-and-test` (`contents: read`): `dotnet restore`, `build`, pruebas unitarias xUnit y pruebas multi-PostgreSQL `Saas.Agenda.IntegrationTests` (Testcontainers) en `.NET 10`.
  - `docker-build` (`contents: read`): construye la imagen con etiquetas OCI, levanta 2 instancias `postgres:17-alpine` con secretos efímeros y `saas-agenda` (`--cap-drop=ALL`, `--security-opt=no-new-privileges:true`, usuario no root `$APP_UID`), valida `/health/live`, `/health/ready`, `/health`, `/openapi/v1.json`, corre Postman/Newman (`26/26`) y verifica aislamiento SQL (`VALLE_APPOINTMENTS=0`).
- **Push a `main` (`publish-ghcr`, `packages: write`):**
  - Publica la misma imagen validada en `ghcr.io/eugene1805/saas-agenda:<SHA>` (sin sobrescribir `latest`) registrando su digest inmutable `@sha256:...` en `GITHUB_STEP_SUMMARY` para su posterior promoción en `saas-infra`.

Para ver decisiones tomadas, dudas abiertas y roadmap por incrementos, consulta `../SEGUIMIENTO_AGENDA_BACKEND.md`.
