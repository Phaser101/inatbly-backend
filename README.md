# Intably Backend

ASP.NET Core backend for defining, launching, and auditing reusable operational
processes.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (`MSSQLLocalDB`)

## Run locally

```powershell
dotnet restore
dotnet tool restore
dotnet dev-certs https --trust
dotnet ef database update `
  --project src/Intably.Infrastructure `
  --startup-project src/Intably.Api
dotnet run --project src/Intably.Api
```

The development API exposes:

- Health check: `/health`
- OpenAPI document: `/openapi/v1.json`

## API access

Backend authentication uses the explicit `BackendTrust:Mode` setting.

Development runs in `DevelopmentHeaders` mode. Protected endpoints require the
local subscription key:

```text
Ocp-Apim-Subscription-Key: intably-local-development-key
```

Requests also require `X-Intably-Tenant-Id`, `X-Intably-User-Id`,
`X-Intably-User-Name`, and `X-Intably-User-Email`. The first valid development
request creates the user's local Intably record only when
`UserProvisioning:AutoProvisionAuthenticatedUsers` is explicitly enabled.
Development-header provisioning remains disabled outside the Development
environment even if that setting is enabled.

Base and production configuration use `TrustedGateway` mode. In that mode, APIM
must send its separate secret in `X-Intably-Gateway-Key`; the backend ignores
`Ocp-Apim-Subscription-Key` for trust and accepts the `X-Intably-*` identity
only after the gateway key is validated. A verified identity is provisioned as
an active user with no functional roles or application permissions. The
configured first administrator is the sole automatic permission exception.
Set `BackendTrust:GatewayKey` through the deployment secret store, using the
same value as the APIM `intably-gateway-key` named value:

```powershell
dotnet user-secrets set "BackendTrust:GatewayKey" "<gateway-key>" `
  --project src/Intably.Api
```

The committed trusted-gateway key is intentionally empty, so missing, unknown,
or incompletely configured trust modes fail during startup. Keep direct backend
ingress restricted to APIM and never expose or log the gateway key.

## First administrator

Set the first administrator's immutable Entra tenant and object IDs before their
first sign-in. Keep the committed defaults empty and store real values in user
secrets or the deployment secret store:

```powershell
dotnet user-secrets set "FirstAdmin:EntraTenantId" "<tenant-id>" `
  --project src/Intably.Api
dotnet user-secrets set "FirstAdmin:EntraObjectId" "<object-id>" `
  --project src/Intably.Api
```

On that active user's first successful login, Intably grants
`MANAGE_PERMISSIONS`. Repeated logins do not create duplicate grants. The match
uses only Entra tenant and object IDs; email and display name are not used.
Leaving either value empty disables the bootstrap.

## Internal-tool access model

Entra authentication establishes identity; Intably application permissions
authorize access to this internal tool. Except for the configured first
administrator bootstrap, a user's first verified login provisions an active
Intably record with zero application permissions. An administrator with
`MANAGE_PERMISSIONS` must then assign supported direct grants through the
frontend's **Administration → Permissions** page.

Effective access includes the implications documented under **Application
permissions** below, while grant APIs and the administration UI retain the
original direct grants for audit and revocation. Functional roles are separate:
they determine process-step eligibility rather than access to application
areas. The API prevents revoking or deactivating the final active
`MANAGE_PERMISSIONS` administrator.

Users do not require direct access assigned to the frontend Entra application
when that application's redirect URI and multitenant settings are already
correct and the tenant administrator uses dynamic consent. This is the intended
access model only; it does not assert that Azure registration, consent, APIM, or
deployment configuration is complete. Verify each environment independently.

## Template services

Template reads require `VIEW_TEMPLATES`; writes require `MANAGE_TEMPLATES`.
Authorized lists return the complete dataset for frontend filtering:

- `IN_001` — `GET /api/users/me`
- `IN_002` — `GET /api/templates` — `VIEW_TEMPLATES`
- `IN_003` — `GET /api/templates/{ptrg}` — `VIEW_TEMPLATES`
- `IN_004` — `GET /api/templates/{ptrg}/published` — `VIEW_TEMPLATES`
- `IN_005` — `POST /api/templates` — `MANAGE_TEMPLATES`
- `IN_006` — `PUT /api/templates/{ptrg}` — `MANAGE_TEMPLATES`
- `IN_007` — `POST /api/templates/{ptrg}/publish` — `MANAGE_TEMPLATES`
- `IN_008` — `POST /api/templates/{ptrg}/duplicate` — `MANAGE_TEMPLATES`
- `IN_009` — `DELETE /api/templates/{ptrg}` — `MANAGE_TEMPLATES`

Deleting archives a template so existing process history remains valid.

## My Work service

My Work requires `VIEW_MY_WORK` and returns the current active user's assigned
open steps, eligible unassigned open steps, and steps they completed in the
last 14 days.

- `IN_010` — `GET /api/my-work` — `VIEW_MY_WORK`

## Process services

Process reads and step mutations require `VIEW_PROCESSES`. Starting a process
requires `START_PROCESSES`. Existing owner, assignee, functional-role, and
`MANAGE_PROCESSES` business rules still apply after the permission gate.

- `IN_011` — `GET /api/processes` — `VIEW_PROCESSES`
- `IN_012` — `POST /api/processes` — `START_PROCESSES`
- `IN_013` — `GET /api/processes/{pirg}` — `VIEW_PROCESSES`
- `IN_014` — `PATCH /api/processes/{pirg}/steps/{psrg}/status` —
  `VIEW_PROCESSES`
- `IN_015` — `PATCH /api/processes/{pirg}/steps/{psrg}/assignment` —
  `VIEW_PROCESSES`
- `IN_016` — `GET /api/processes/{pirg}/steps/{psrg}/eligible-assignees` —
  `VIEW_PROCESSES`
- `IN_017` — `POST /api/processes/{pirg}/close` — `VIEW_PROCESSES`
- `IN_018` — `GET /api/processes/{pirg}/timeline` — `VIEW_PROCESSES`
- `IN_019` — `GET /api/processes/{pirg}/export` — `VIEW_PROCESSES`

## Application permissions

Application permission contract values are `VIEW_MY_WORK`, `VIEW_PROCESSES`,
`START_PROCESSES`, `VIEW_TEMPLATES`, `MANAGE_PERMISSIONS`, `MANAGE_ROLES`,
`MANAGE_TEMPLATES`, and `MANAGE_PROCESSES`.

The current-user payload returns effective permissions. Implications are
centralized as follows:

- `MANAGE_TEMPLATES` implies `VIEW_TEMPLATES`.
- `START_PROCESSES` implies `VIEW_TEMPLATES` and `VIEW_PROCESSES`.
- `MANAGE_PROCESSES` implies `START_PROCESSES`, `VIEW_PROCESSES`, and
  `VIEW_TEMPLATES`.

Permission-grant APIs continue to return direct grants only, so implied access
does not create or obscure audit records.

## Administration services

Administration endpoints use the centralized permission policies. Existing
lookup responses remain compatible and return complete datasets for frontend
filtering.

- `IN_021` — `GET /api/functional-roles` — authenticated API access
- `IN_023` — `GET /api/users` — authenticated API access
- `IN_024` — `POST /api/functional-roles` — `MANAGE_ROLES`
- `IN_025` — `PUT /api/functional-roles/{frrg}` — `MANAGE_ROLES`
- `IN_026` — `DELETE /api/functional-roles/{frrg}` — `MANAGE_ROLES`
- `IN_027` — `PUT /api/users/{grg}/functional-roles` — `MANAGE_ROLES`
- `IN_028` — `PATCH /api/users/{grg}/active` — `MANAGE_ROLES`
- `IN_029` — `GET /api/permission-grants` — `MANAGE_PERMISSIONS`
- `IN_030` — `POST /api/permission-grants` — `MANAGE_PERMISSIONS`
- `IN_031` — `DELETE /api/permission-grants/{pgrg}` —
  `MANAGE_PERMISSIONS`

Deleting a functional role archives it. Replacing a user's role memberships is
atomic. Permission grant responses include the grant `pgrg`, target `grg`,
permission, granting actor `grg` and name, and UTC grant timestamp. The API
rejects duplicate, missing, and conflicting mutations and will not revoke or
deactivate the final active `MANAGE_PERMISSIONS` administrator.

## Solution structure

- `src/Intably.Api` — HTTP pipeline and endpoints
- `src/Intably.Application` — application use cases and contracts
- `src/Intably.Domain` — process rules and domain entities
- `src/Intably.Infrastructure` — EF Core and external integrations
- `tests/Intably.UnitTests` — isolated business-rule tests
- `tests/Intably.IntegrationTests` — API and persistence integration tests
- `infra` — Azure Bicep, APIM policy, and production deployment guide

## Production deployment

See `infra/README.md` for the parameterized Azure architecture, APIM ingress
choices, secure parameters, managed-identity option, GitHub OIDC variables,
migration strategy, deployment commands, and verification steps. The workflows
under `.github/workflows` build and test on Windows because integration tests
currently require LocalDB; production deployment is manual and protected by the
GitHub `production` environment.

## Configuration

Development uses the `Intably` LocalDB database configured in
`src/Intably.Api/appsettings.Development.json` and explicitly selects
`DevelopmentHeaders`. `src/Intably.Api/appsettings.json` defaults to
`TrustedGateway` with no committed secret.

Do not commit credentials or production connection strings. Use .NET user
secrets locally and Azure managed identity with Key Vault in production.
