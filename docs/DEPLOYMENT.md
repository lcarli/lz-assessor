# LZ Assessor Deployment Guide

This guide covers deploying the complete LZ Assessor solution to Azure.

## Prerequisites

- Azure subscription with appropriate permissions
- Resource group for deployment
- Log Analytics workspace (or create new one)
- PowerShell or Azure CLI
- .NET 8 SDK (for local development)

## 1. Deploy Infrastructure

### Create Resource Group
```bash
az group create --name lz-assessor-rg --location westus2
```

### Deploy Log Analytics Workspace
```bash
az monitor log-analytics workspace create \
  --resource-group lz-assessor-rg \
  --workspace-name lz-assessor-workspace \
  --location westus2
```

### Deploy Workbook
```bash
# Get workspace resource ID
WORKSPACE_ID=$(az monitor log-analytics workspace show \
  --resource-group lz-assessor-rg \
  --workspace-name lz-assessor-workspace \
  --query id --output tsv)

# Deploy workbook template
az deployment group create \
  --resource-group lz-assessor-rg \
  --template-file templates/workbook.json \
  --parameters \
    workbookDisplayName="LZ Assessment Dashboard" \
    workspaceResourceId="$WORKSPACE_ID"
```

## 2. Deploy Function App

### Create Function App Resources
```bash
# Create storage account
az storage account create \
  --name lzassessorstorage \
  --resource-group lz-assessor-rg \
  --location westus2 \
  --sku Standard_LRS

# Create function app
az functionapp create \
  --resource-group lz-assessor-rg \
  --consumption-plan-location westus2 \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --name lz-assessor-func \
  --storage-account lzassessorstorage
```

### Configure Function App Settings
```bash
# Configure assessment spec URL
az functionapp config appsettings set \
  --name lz-assessor-func \
  --resource-group lz-assessor-rg \
  --settings \
    "Assessment:SpecUrl=https://raw.githubusercontent.com/lcarli/lz-assessor/main/specs/billing_entra.json" \
    "LogAnalytics:WorkspaceId=$WORKSPACE_ID"
```

### Deploy Function Code
```bash
# From the src/LzAssessor.NewVersion directory
dotnet publish --configuration Release
func azure functionapp publish lz-assessor-func
```

## 3. Configure Permissions

### Create Managed Identity
```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name lz-assessor-func \
  --resource-group lz-assessor-rg
```

### Assign Required Roles
```bash
# Get the function app's managed identity principal ID
PRINCIPAL_ID=$(az functionapp identity show \
  --name lz-assessor-func \
  --resource-group lz-assessor-rg \
  --query principalId --output tsv)

# Assign Reader role (for resource discovery)
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Reader" \
  --scope "/subscriptions/$(az account show --query id --output tsv)"

# Assign Security Reader role  
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Security Reader" \
  --scope "/subscriptions/$(az account show --query id --output tsv)"

# Assign Cost Management Reader role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Cost Management Reader" \
  --scope "/subscriptions/$(az account show --query id --output tsv)"
```

### Configure Graph API Permissions
For Microsoft Graph API access, you'll need to create an App Registration with the following permissions:
- `Directory.Read.All`
- `Policy.Read.All` 
- `Reports.Read.All`
- `PrivilegedAccess.Read.AzureAD` (for PIM checks)

## 4. Test Deployment

### Run Assessment
```bash
# Get function app URL
FUNC_URL=$(az functionapp show \
  --name lz-assessor-func \
  --resource-group lz-assessor-rg \
  --query defaultHostName --output tsv)

# Trigger assessment
curl -X POST "https://$FUNC_URL/api/assessment/run"

# Check latest results  
curl "https://$FUNC_URL/api/assessment/last?scope=<your-tenant-id>"
```

### View Results in Workbook
1. Navigate to Azure Portal
2. Go to your resource group
3. Open the "LZ Assessment Dashboard" workbook
4. Enter your tenant ID in the parameter field
5. Review assessment results across different views

## 5. Monitoring and Maintenance

### Application Insights
The function app automatically creates Application Insights for monitoring:
- View function execution logs
- Monitor performance metrics
- Set up alerts for failures

### Log Analytics Queries
Query assessment data directly:
```kql
ALZ_Assessment_CL
| where TenantId_s == "your-tenant-id"
| summarize 
    TotalChecks = dcount(QuestionId_s),
    CompliantChecks = countif(Status_s == "Compliant"),
    NonCompliantChecks = countif(Status_s == "NonCompliant")
| extend ComplianceScore = round(todouble(CompliantChecks) / todouble(TotalChecks) * 100, 1)
```

### Schedule Regular Assessments
Set up a timer trigger in the function app to run assessments automatically:
- Daily: For active environments
- Weekly: For stable environments  
- Monthly: For baseline compliance reporting

## Troubleshooting

### Common Issues
1. **Permission Denied**: Verify managed identity has required roles
2. **Graph API Errors**: Check app registration permissions
3. **No Data in Workbook**: Verify Log Analytics workspace configuration
4. **Function Timeouts**: Increase timeout settings for large tenants

### Log Locations
- Function execution logs: Application Insights
- Assessment results: Log Analytics (ALZ_Assessment_CL table)
- Build/deployment logs: Function App deployment center

This completes the basic deployment of the LZ Assessor system. The solution will automatically discover tenant resources, evaluate compliance against the specification, and provide results through both API endpoints and the visual workbook dashboard.