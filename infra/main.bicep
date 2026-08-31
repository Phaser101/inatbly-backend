targetScope = 'resourceGroup'

@description('Short lowercase workload name used in globally unique resource names.')
@minLength(3)
@maxLength(18)
param workloadName string = 'intably'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Deployment environment name.')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'prod'

@description('Linux App Service runtime. Keep configurable because regional platform support can lag ARM schema support.')
param linuxFxVersion string = 'DOTNETCORE|10.0'

@description('App Service plan SKU.')
param appServicePlanSku object = {
  name: 'B1'
  tier: 'Basic'
  capacity: 1
}

@description('Azure SQL database SKU.')
param sqlDatabaseSku object = {
  name: 'Basic'
  tier: 'Basic'
  capacity: 5
}

@description('SQL administrator login. With the secure default it bootstraps the app; useManagedIdentitySqlAuthentication removes it from the runtime connection.')
param sqlAdministratorLogin string

@secure()
@description('SQL administrator password. Supply at deployment time; never commit it.')
param sqlAdministratorPassword string

@description('Use the App Service identity for SQL. Before enabling, create a contained database user for the emitted managed identity principal and grant migration/runtime permissions.')
param useManagedIdentitySqlAuthentication bool = false

@description('Immutable Entra tenant ID for the initial Intably administrator. Empty disables bootstrap.')
param firstAdminTenantId string = ''

@description('Immutable Entra object ID for the initial Intably administrator. Empty disables bootstrap.')
param firstAdminObjectId string = ''

@secure()
@description('Shared APIM-to-backend gateway key. Supply at deployment time and use the same secret in APIM.')
param backendGatewayKey string

@description('Allowed frontend origins, for example ["https://example.azurestaticapps.net"].')
param allowedCorsOrigins array

@description('Keep true for APIM tiers without VNet reachability. The site remains default-deny and requires an APIM allow rule.')
param backendPublicNetworkAccess bool = true

@description('APIM outbound IPv4 CIDRs allowed to call the public backend. Prefer the dedicated gateway outbound IPs for the deployed APIM instance.')
param apimOutboundIpCidrs array = []

@description('Allow the Azure ApiManagement service tag. This is broader than instance CIDRs and is mainly for tiers without stable dedicated outbound IPs.')
param allowApiManagementServiceTag bool = false

@description('Virtual network address space.')
param virtualNetworkAddressPrefix string = '10.40.0.0/16'

@description('App Service VNet integration subnet prefix.')
param appIntegrationSubnetPrefix string = '10.40.1.0/24'

@description('Private endpoint subnet prefix.')
param privateEndpointSubnetPrefix string = '10.40.2.0/24'

var suffix = toLower(uniqueString(subscription().subscriptionId, resourceGroup().id, workloadName, environmentName))
var namePrefix = '${workloadName}-${environmentName}'
var appName = '${namePrefix}-${suffix}'
var sqlServerName = '${namePrefix}-sql-${suffix}'
var keyVaultName = take(replace('${workloadName}${environmentName}${suffix}', '-', ''), 24)
var sqlManagedIdentityConnectionString = 'Server=tcp:${sqlServerName}.database.windows.net,1433;Initial Catalog=Intably;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;'
var sqlPasswordConnectionString = 'Server=tcp:${sqlServerName}.database.windows.net,1433;Initial Catalog=Intably;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;User ID=${sqlAdministratorLogin};Password=${sqlAdministratorPassword};'
var tags = {
  workload: workloadName
  environment: environmentName
  managedBy: 'bicep'
}

var cidrRestrictions = [
  for (cidr, index) in apimOutboundIpCidrs: {
    name: 'Allow-APIM-IP-${index + 1}'
    action: 'Allow'
    ipAddress: cidr
    priority: 100 + index
    description: 'Dedicated APIM outbound address'
  }
]
var serviceTagRestrictions = allowApiManagementServiceTag ? [
  {
    name: 'Allow-APIM-ServiceTag'
    action: 'Allow'
    ipAddress: 'ApiManagement'
    tag: 'ServiceTag'
    priority: 500
    description: 'Azure ApiManagement service tag; broader than instance CIDRs'
  }
] : []

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  tags: tags
  properties: {
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-appi'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: '${namePrefix}-vnet'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        virtualNetworkAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'app-integration'
        properties: {
          addressPrefix: appIntegrationSubnetPrefix
          delegations: [
            {
              name: 'web-app-delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource appIntegrationSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  name: 'app-integration'
  parent: virtualNetwork
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  name: 'private-endpoints'
  parent: virtualNetwork
}

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${namePrefix}-plan'
  location: location
  tags: tags
  sku: appServicePlanSku
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    restrictOutboundNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: 'Intably'
  parent: sqlServer
  location: location
  tags: tags
  sku: sqlDatabaseSku
  properties: {
    zoneRedundant: false
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

resource gatewayKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'backend-gateway-key'
  parent: keyVault
  properties: {
    value: backendGatewayKey
  }
}

resource databaseConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'database-connection-string'
  parent: keyVault
  properties: {
    value: useManagedIdentitySqlAuthentication
      ? sqlManagedIdentityConnectionString
      : sqlPasswordConnectionString
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    publicNetworkAccess: backendPublicNetworkAccess ? 'Enabled' : 'Disabled'
    virtualNetworkSubnetId: appIntegrationSubnet.id
    vnetRouteAllEnabled: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/health'
      appCommandLine: 'bash /home/site/wwwroot/migrate-and-start.sh'
      ipSecurityRestrictionsDefaultAction: 'Deny'
      ipSecurityRestrictions: concat(cidrRestrictions, serviceTagRestrictions)
      scmIpSecurityRestrictionsDefaultAction: 'Allow'
      scmIpSecurityRestrictionsUseMain: false
      appSettings: concat([
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'BackendTrust__Mode'
          value: 'TrustedGateway'
        }
        {
          name: 'BackendTrust__GatewayKey'
          value: '@Microsoft.KeyVault(SecretUri=${gatewayKeySecret.properties.secretUriWithVersion})'
        }
        {
          name: 'ConnectionStrings__Database'
          value: '@Microsoft.KeyVault(SecretUri=${databaseConnectionSecret.properties.secretUriWithVersion})'
        }
        {
          name: 'FirstAdmin__EntraTenantId'
          value: firstAdminTenantId
        }
        {
          name: 'FirstAdmin__EntraObjectId'
          value: firstAdminObjectId
        }
        {
          name: 'UserProvisioning__AutoProvisionAuthenticatedUsers'
          value: 'true'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
      ], [
        for (origin, index) in allowedCorsOrigins: {
          name: 'Cors__AllowedOrigins__${index}'
          value: origin
        }
      ])
    }
  }
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    )
  }
}

resource sqlPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.database.windows.net'
  location: 'global'
  tags: tags
}

resource keyVaultPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
  tags: tags
}

resource appPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = if (!backendPublicNetworkAccess) {
  name: 'privatelink.azurewebsites.net'
  location: 'global'
  tags: tags
}

resource sqlDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  name: '${namePrefix}-sql-link'
  parent: sqlPrivateDnsZone
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource keyVaultDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  name: '${namePrefix}-kv-link'
  parent: keyVaultPrivateDnsZone
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource appDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = if (!backendPublicNetworkAccess) {
  name: '${namePrefix}-app-link'
  parent: appPrivateDnsZone
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${namePrefix}-sql-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'sql'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

resource sqlPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  name: 'default'
  parent: sqlPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql'
        properties: {
          privateDnsZoneId: sqlPrivateDnsZone.id
        }
      }
    ]
  }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${namePrefix}-kv-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'vault'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource keyVaultPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  name: 'default'
  parent: keyVaultPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vault'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZone.id
        }
      }
    ]
  }
}

resource appPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = if (!backendPublicNetworkAccess) {
  name: '${namePrefix}-app-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'site'
        properties: {
          privateLinkServiceId: webApp.id
          groupIds: [
            'sites'
          ]
        }
      }
    ]
  }
}

resource appPrivateDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = if (!backendPublicNetworkAccess) {
  name: 'default'
  parent: appPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'site'
        properties: {
          privateDnsZoneId: appPrivateDnsZone.id
        }
      }
    ]
  }
}

output appServiceName string = webApp.name
output appServiceDefaultHostName string = webApp.properties.defaultHostName
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output keyVaultName string = keyVault.name
output managedIdentityPrincipalId string = webApp.identity.principalId
