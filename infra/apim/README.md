# APIM identity contract

`inbound-policy.xml` is the production gateway boundary for Intably. It
validates the user's Entra access token, removes caller-supplied identity
headers, and sends identity to the backend only from validated claims.

The policy does not replace APIM subscriptions. Keep the API attached to an
APIM product with **Subscription required** enabled and keep the subscription
key header name as `Ocp-Apim-Subscription-Key`. The policy deliberately leaves
that browser-to-APIM header unchanged. The backend's `TrustedGateway` mode does
not use it as backend trust; it authenticates APIM with the separate gateway
key.

## One-time Entra setup

Perform these steps in the directory that owns Intably.

1. Register a separate **Intably API** application.
   - Supported account types: **Accounts in any organizational directory
     (Any Microsoft Entra ID tenant - Multitenant)**.
   - Do not select an option that includes personal Microsoft accounts.
   - Under **Expose an API**, keep or set the Application ID URI (normally
     `api://<api-application-client-id>`).
   - Add a delegated scope named `access_as_user`.
   - In the manifest, set `api.requestedAccessTokenVersion` to `2`. This makes
     v2 tokens the normal contract even when callers come from another tenant.
   - Record the API application (client) ID and Application ID URI; neither is
     a secret.
2. Configure the **Intably frontend** application as organizational
   multi-tenant as well. Keep its existing SPA redirect URIs exact (scheme,
   host, path, and trailing slash).
3. In the frontend registration, add delegated API permission
   `Intably API / access_as_user`. Grant admin consent in the home directory.
   Do not grant application permissions; this contract represents a user.
4. Record the frontend application (client) ID. Do not use its object ID or a
   service-principal object ID in APIM.
5. For each customer directory, have an administrator grant tenant-wide
   consent using a tenant-specific URL:

   ```text
   https://login.microsoftonline.com/<customer-tenant-id>/adminconsent?client_id=<frontend-client-id>&redirect_uri=<url-encoded-frontend-redirect-uri>&state=<anti-forgery-value>
   ```

   PowerShell template:

   ```powershell
   $tenantId = "<customer-tenant-guid>"
   $clientId = "<frontend-application-client-id>"
   $redirectUri = [uri]::EscapeDataString("https://app.example.com/auth/callback")
   $state = [uri]::EscapeDataString("<fresh-random-state>")
   "https://login.microsoftonline.com/$tenantId/adminconsent?client_id=$clientId&redirect_uri=$redirectUri&state=$state"
   ```

   The redirect URI must already be registered on the frontend application.
   Generate and verify `state` in the onboarding flow; the static placeholder
   above is documentation only. Consent creates enterprise applications
   (service principals) in that customer directory.

## APIM named values

Create the following named values before importing the policy. References use
the exact names shown.

- `intably-api-audience-v2`: the API application client ID GUID. This is the
  normal `aud` for a v2 access token.
- `intably-api-audience-v1`: the API Application ID URI, normally
  `api://<api-application-client-id>`. This permits a correctly issued legacy
  v1 access token while the policy applies the v1 `appid` client check. Confirm
  the value against a non-production token from the actual registration.
- `intably-frontend-client-id`: the frontend application client ID GUID. APIM
  matches `azp` on v2 tokens and `appid` on v1 tokens.
- `intably-allowed-tenant-ids`: comma-separated customer and home-directory
  tenant GUIDs. Add a tenant only after onboarding and admin consent. Do not
  put `organizations`, `common`, domains, or the Microsoft consumer tenant ID
  in this value.
- `intably-gateway-key`: a cryptographically random value of at least 32
  bytes. Mark it **Secret** and preferably make it a Key Vault reference. The
  policy writes it to `X-Intably-Gateway-Key` after deleting any caller
  value.
- `intably-backend-base-url`: the backend's HTTPS origin, with no path suffix
  unless the deployment requires one. Prefer a private endpoint or internal
  load-balancer address.

Example secret generation (do not commit the output):

```powershell
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)
```

Use separate named values and secrets per environment. No value in this
repository is a production credential.

## Apply the policy

Import `inbound-policy.xml` at the Intably **API scope**. Keep `<base />` so
service/global controls remain inherited. Associate the API with the intended
subscription-required product and test with both:

1. a valid product subscription key plus a valid user access token; and
2. independently invalid/missing subscription keys and bearer tokens.

APIM policy expressions are compiled only when APIM saves the policy. A local
XML parser can validate well-formedness, but final validation must be done by
uploading to a non-production APIM instance and exercising a request.

## Cross-directory and multi-tenant caveats

- `organizations` excludes personal Microsoft accounts. The policy also
  rejects the well-known consumer tenant
  `9188040d-6c67-4c5b-b112-36a304b66dad` and requires `tid` to be explicitly
  allowlisted.
- Use a tenant-specific authority during customer onboarding. A service
  principal and consent in the API owner's directory do not grant access in a
  customer directory.
- A guest user receives `tid` and `oid` in the resource tenant's context.
  Object IDs are tenant-local, so Intably identity remains the pair
  (`tid`, `oid`), never email.
- Email, UPN, and display name are mutable presentation data. The policy
  forwards them for the current backend contract but authorization must remain
  based on immutable tenant/object IDs.
- Publisher verification, tenant user-assignment policy, Conditional Access,
  and cross-tenant access settings can block consent or token issuance. Resolve
  those controls in each directory; do not weaken token validation.
- The API audience must describe the API, not Microsoft Graph and not the
  frontend. ID tokens must never be sent to this API.

## Network boundary

The gateway key is meaningful only if clients cannot bypass APIM. Restrict
the backend to APIM egress by using a private endpoint/VNet integration where
available. Otherwise, use the narrowest supported combination of access
restrictions and APIM service tags/IP addresses. Disable unrestricted public
ingress and direct custom-domain/DNS paths to the backend.

Store the same gateway key as the backend's `BackendTrust:GatewayKey` deployment
secret. Rotate it through Key Vault/APIM named values, support a controlled
overlap if zero downtime is required, and never log the header. The backend
must run in `TrustedGateway` mode outside local development.

## Reserved headers

APIM deletes and reconstructs these headers on every accepted request:

- `X-Intably-Tenant-Id`
- `X-Intably-User-Id`
- `X-Intably-User-Name`
- `X-Intably-User-Email`
- `X-Intably-Gateway-Key`

Treat the complete `X-Intably-*` namespace as gateway-reserved. When adding a
new header to that namespace, add an explicit delete/rebuild entry to the APIM
policy in the same change; APIM `set-header` does not support wildcard header
deletion.
