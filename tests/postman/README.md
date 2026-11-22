# Pruebas Postman y Newman (`saas-agenda`)

## Importar en Postman Desktop / VS Code

1. Levanta el servicio y PostgreSQL desde `saas-agenda/`:

```powershell
docker compose up -d --build
```

2. Importa `Vital.Agenda.postman_collection.json` y el entorno `Vital.Agenda.local.postman_environment.json`.
3. Ejecuta el **Collection Runner** completo (27 requests secuenciales). El script pre-request genera automáticamente los tokens JWT HS256 firmados con la misma clave dev que `saas-identity`.

## Ejecutar con Newman en Docker (sin instalar Node/Newman global)

```powershell
docker run --rm --network saas-agenda_default `
  -v "${PWD}:/etc/newman" `
  postman/newman:alpine run /etc/newman/Vital.Agenda.postman_collection.json `
  --env-var "baseUrl=http://agenda-api:8080"
```

Si ejecutas el comando desde `saas-agenda/` en lugar de `tests/postman/`, monta `${PWD}/tests/postman:/etc/newman`.
