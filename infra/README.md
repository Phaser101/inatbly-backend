# Production deployment

`main.bicep` is a parameterized, single-region production foundation. It creates:

- Linux Azure App Service and a system-assigned managed identity
- Azure SQL logical server/database with a private endpoint
- Key Vault with RBAC and a private endpoint
- Application Insights backed by Log Analytics
- App Service VNet integration and optional private backend ingress

No APIM instance is created. The existing APIM API, product, named values, and
`infra/apim/inbound-policy.xml` remain the public API boundary.

## APIM ingress choice

App Service access is always default-deny. Pick one supported model:

1. **Public backend with dedicated APIM outbound CIDRs** (recommended for an
   existing external APIM instance): keep `backendPublicNetworkAccess=true`,
   pass every APIM gateway outbound address in `apimOutboundIpCidrs`, and leave
   `allowApiManagementServiceTag=false`.
2. **Public backend with the `ApiManagement` service tag**: use only when the
   APIM tier does not provide usable stable outbound CIDRs. This permits traffic
   from the wider service tag, not only this APIM instance; the independent
   gateway key remains required. Confirm the service tag works for the chosen
   APIM tier and region before production.
3. **Private backend**: set `backendPublicNetworkAccess=false`. This creates an
   App Service private endpoint and DNS zone. It works only when the APIM tier
   supports VNet connectivity to that VNet and resolves
   `privatelink.azurewebsites.net`. Consumption and other tiers without the
   required VNet path cannot use this option.

Do not deploy public mode with an empty CIDR list unless the service-tag rule is
enabled: the backend will correctly reject all application traffic. Never add a
catch-all allow rule. After deployment, set the APIM
`intably-backend-base-url` and the secret `intably-gateway-key` named values as
described in `apim/README.md`.

The SCM deployment endpoint is independently authenticated and is not an API
bypass. Its network rule is intentionally separate so GitHub-hosted deployment
runners can upload packages. Organizations requiring private SCM should use a
self-hosted runner with VNet reachability and restrict the SCM endpoint too.

## Deploy infrastructure

Requirements: Azure CLI with Bicep support and permission to create resource
groups, role assignments, private endpoints, SQL, Key Vault, and App Service.

```powershell
az login
az account set --subscription "<subscription-id>"
az group create --name "<resource-group>" --location "<azure-region>"

$sqlPassword = Read-Host "SQL administrator password" -AsSecureString
$sqlPasswordText = [Net.NetworkCredential]::new("", $sqlPassword).Password
$gatewayBytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($gatewayBytes)
$gatewayKey = [Convert]::ToBase64String($gatewayBytes)

az deployment group create `
  --resource-group "<resource-group>" `
  --template-file infra/main.bicep `
  --parameters `
    workloadName="intably" `
    environmentName="prod" `
    sqlAdministratorLogin="<non-default-admin-name>" `
    sqlAdministratorPassword="$sqlPasswordText" `
    backendGatewayKey="$gatewayKey" `
    allowedCorsOrigins='["https://<frontend-host>"]' `
    apimOutboundIpCidrs='["<apim-outbound-ip>/32"]'

Remove-Variable sqlPassword,sqlPasswordText,gatewayKey,gatewayBytes
```

The two secret values are secure Bicep parameters and are stored as Key Vault
secrets. Do not put them in a committed parameter file, command transcript, or
GitHub variable. Rotate the SQL administrator after bootstrap and rotate the
gateway key in Key Vault and APIM together.

The default `linuxFxVersion` is `DOTNETCORE|10.0`. App Service runtime
availability can vary by region/platform rollout; override that parameter only
with a runtime compatible with the application's `net10.0` target.

## Database authentication and migrations

The secure, low-complexity default stores the SQL-authenticated connection
string in Key Vault. The app identity reads both required secrets through Key
Vault RBAC; SQL and Key Vault data-plane traffic stays on private endpoints.

For managed-identity SQL authentication:

1. Deploy once with the default.
2. From a SQL administration path with VNet/private-endpoint reachability,
   create a contained user for the emitted `managedIdentityPrincipalId` and
   grant the permissions needed by the application and EF migrations.
3. Redeploy with `useManagedIdentitySqlAuthentication=true`.
4. Rotate/disable the runtime SQL login after verification.

The CI artifact contains a Linux EF Core migration bundle and
`migrate-and-start.sh`. App Service runs the idempotent bundle before starting
the API. A failed migration stops startup rather than running mismatched code.
Because this simple model gives the runtime identity schema-change rights,
larger environments should separate migration and runtime identities using a
VNet-connected deployment runner.

## Backend GitHub configuration

Create a protected GitHub environment named `production` and require reviewers.
Add these **environment variables**:

- `AZURE_CLIENT_ID` — client ID of the federated deployment application
- `AZURE_TENANT_ID` — Entra tenant ID
- `AZURE_SUBSCRIPTION_ID` — Azure subscription ID
- `AZURE_RESOURCE_GROUP` — deployed resource group
- `AZURE_WEBAPP_NAME` — `appServiceName` deployment output

Configure workload identity federation from that application to the repository
and `production` environment. No Azure client secret is required. Grant only
the permissions needed to deploy to the target App Service.

Run **Backend CI** for restore/build/tests/artifact creation. Production deploys
are manual:

```text
GitHub → Actions → Deploy backend to production → Run workflow
```

The deploy job uses the protected environment, rebuilds and tests on
`windows-latest` (required by the current LocalDB integration tests), downloads
the resulting ZIP artifact, signs in with OIDC, and deploys it.

## Verification

Before provisioning:

```powershell
az bicep build --file infra/main.bicep
dotnet restore
dotnet build Intably.sln --configuration Release
dotnet test Intably.sln --configuration Release
```

After provisioning, verify `/health` through APIM, then verify that the direct
App Service hostname returns `403` (public restricted mode) or is unreachable
(private mode). Test missing/invalid APIM subscription keys, bearer tokens, and
gateway keys independently.
