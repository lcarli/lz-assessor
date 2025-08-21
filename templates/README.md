# LZ Assessor Infrastructure Templates

This directory contains ARM templates and other infrastructure-as-code files for deploying the LZ Assessor components.

## Workbook Template

The `workbook.json` file contains an Azure Workbook template that provides comprehensive visualization of assessment results.

### Features

- **Assessment Summary**: High-level metrics including total checks, compliance score, and last run time
- **Compliance by Pillar**: Breakdown of compliance status by assessment category (Identity, Billing, etc.)
- **Non-Compliant Items**: Detailed list of gaps requiring attention, sorted by severity
- **Compliance Trend**: Historical view of compliance score over time

### Deployment

```bash
# Deploy the workbook template
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file templates/workbook.json \
  --parameters \
    workbookDisplayName="LZ Assessment Dashboard" \
    workspaceResourceId="/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.OperationalInsights/workspaces/<workspace>"
```

### Prerequisites

- Log Analytics workspace where assessment results are stored
- ALZ_Assessment_CL custom table with assessment data
- Appropriate permissions to create workbooks in the target resource group

### Query Requirements

The workbook expects assessment data in the following Log Analytics table structure:

```
ALZ_Assessment_CL
├── TenantId_s (string)
├── RunId_s (string) 
├── QuestionId_s (string)
├── Title_s (string)
├── Pillar_s (string)
├── Status_s (string)
├── Severity_s (string)
├── Evidence_Summary_s (string)
└── TimeGenerated (datetime)
```

This matches the structure created by the LZ Assessor persistence layer.