# LZ Assessor - Landing Zone Assessment Tool

An automated Landing Zone assessor that evaluates Azure environments against best practices and compliance requirements.

## Overview

LZ Assessor is an automated Landing Zone assessment tool that:

- Reads declarative checklists (JSON) starting with the "Azure Billing and Microsoft Entra ID Tenants" category
- Collects technical evidence from tenants via official APIs
- Classifies each item (Compliant/Non-Compliant/etc.)
- Publishes results in structured format for self-service consumption

## Architecture

### Core Components

1. **Azure Functions (Isolated .NET 8)**
   - HTTP and Timer triggers
   - Execution and routing engine
   - Persistence system

2. **Executors (Plugins)**
   - `GraphExecutor`: Microsoft Graph / Entra ID
   - `ArmExecutor`: Azure Resource Manager
   - `CostExecutor`: Cost Management
   - Ready to expand: policy, defender, backup

3. **Evaluation Engine**
   - `LogicEvaluator`: interprets declarative logic
   - Support for `allOf`, `anyOf`, `exists`, `equals`, `lte`, `gte`, `contains`, `not`
   - Predicate application over JSON payloads

4. **Declarative Spec**
   - JSON file with check definitions
   - Declarative logic for each check
   - Evidence and fallback configuration

### Project Structure

```
lz-assessor/
├── src/
│   └── LzAssessor.NewVersion/
│       ├── Models/              # Data contracts and models
│       ├── Engine/              # Execution and evaluation core
│       ├── Executors/           # Source-specific executor implementations
│       ├── Specs/               # Specification loading
│       ├── Persistence/         # Persistence layer
│       ├── Triggers/            # HTTP and Timer triggers
│       └── Program.cs           # Configuration and DI
└── specs/
    └── billing_entra.json       # Spec for initial category
```

## API Endpoints

### Assessment Endpoints

- **POST** `/api/assessment/run` - Start a new assessment (simplified)
- **POST** `/api/assessment/run-orchestrated` - Start a new assessment (with orchestration)
- **GET** `/api/assessment/last?scope={tenantId}` - Get latest assessment results  
- **GET** `/api/assessment/history?tenantId={tenantId}` - Get assessment history

### Attestation Endpoints

- **POST** `/api/attestation/submit` - Submit a manual attestation
- **GET** `/api/attestation/tenant?tenantId={tenantId}` - Get attestations for a tenant
- **GET** `/api/attestation/check?tenantId={tenantId}&checkId={checkId}` - Get attestations for a specific check

### API Usage Examples

#### Running Assessment with Specific Tenant ID

```bash
# Simple assessment run
curl -X POST "https://your-function-app.azurewebsites.net/api/assessment/run" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "12345678-1234-1234-1234-123456789abc",
    "runId": "my-custom-run-id"
  }'

# Orchestrated assessment run (with robust error handling)
curl -X POST "https://your-function-app.azurewebsites.net/api/assessment/run-orchestrated" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "12345678-1234-1234-1234-123456789abc",
    "specUrl": "https://raw.githubusercontent.com/lcarli/lz-assessor/main/specs/billing_entra.json"
  }'

# Enhanced orchestrator with design areas and contract type filtering
curl -X POST "https://your-function-app.azurewebsites.net/api/orchestrator/run" \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "12345678-1234-1234-1234-123456789123",
    "ContractType": "EnterpriseAgreement",
    "DefaultRegion": "canadaeast",
    "DesignAreas": {
      "Billing": true,
      "IAM": false,
      "ResourceOrganization": false,
      "Network": false,
      "Governance": false,
      "Security": false,
      "DevOps": false,
      "Management": false
    }
  }'

# All design areas assessment
curl -X POST "https://your-function-app.azurewebsites.net/api/orchestrator/run" \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "12345678-1234-1234-1234-123456789123",
    "ContractType": "MicrosoftEntraIDTenants",
    "DefaultRegion": "canadaeast",
    "DesignAreas": {
      "Billing": true,
      "IAM": true,
      "ResourceOrganization": true,
      "Network": true,
      "Governance": true,
      "Security": true,
      "DevOps": true,
      "Management": true
    }
  }'
```

### Enhanced Orchestrator API

The new orchestrator API endpoint accepts a structured request that allows:

- **Contract Type Filtering**: Only questions applicable to the specified contract type are evaluated
- **Design Area Selection**: Choose which categories to assess (Billing, IAM, Network, etc.)
- **Regional Configuration**: Specify default Azure region for assessment queries

**Contract Types:**
- `EnterpriseAgreement`
- `MicrosoftCustomerAgreement` 
- `CloudSolutionProvider`
- `MicrosoftEntraIDTenants`

**Assessment Status Values:**
- `Fulfilled` - Check verified and compliant
- `Open` - Action item identified  
- `Manually` - Manual assessment required
- `NotRequired` - Not needed for current requirements
- `NotApplicable` - Not applicable for current design
- `NotVerified` - Not yet assessed

**Design Areas:**
- **Billing**: Cost management, budgets, exports (automated assessment)
- **IAM**: Identity and access management (manual assessment)
- **ResourceOrganization**: Management groups, naming, tagging (manual assessment)
- **Network**: Topology, segmentation, connectivity (manual assessment)
- **Governance**: Policies, compliance, resource locks (manual assessment)
- **Security**: Defender, key management, incident response (manual assessment)
- **DevOps**: CI/CD, source control, infrastructure as code (manual assessment)
- **Management**: Monitoring, backup, automation (manual assessment)
```

#### Submitting Manual Attestations

```bash
# Submit attestation for a manual check
curl -X POST "https://your-function-app.azurewebsites.net/api/attestation/submit" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "12345678-1234-1234-1234-123456789abc",
    "checkId": "ENTRA-SECURITY-DEFAULTS-OR-CA",
    "status": "Compliant",
    "comments": "Conditional Access policies are properly configured",
    "attestorName": "John Doe",
    "attestorEmail": "john.doe@company.com",
    "evidence": "Screenshots and policy documentation attached",
    "expirationDays": 90
  }'
```

#### Retrieving Assessment Results

```bash
# Get latest assessment for tenant
curl "https://your-function-app.azurewebsites.net/api/assessment/last?scope=12345678-1234-1234-1234-123456789abc"

# Get assessment history
curl "https://your-function-app.azurewebsites.net/api/assessment/history?tenantId=12345678-1234-1234-1234-123456789abc&maxRuns=5"

# Get attestations for tenant
curl "https://your-function-app.azurewebsites.net/api/attestation/tenant?tenantId=12345678-1234-1234-1234-123456789abc"
```

## Infrastructure

- **Workbook Template**: `templates/workbook.json` - Azure Workbook for results visualization
- **ARM Templates**: Ready for deployment with Log Analytics integration

The initial implementation focuses on the "Azure Billing and Microsoft Entra ID Tenants" category with the following checks:

### Entra ID Checks
- **ENTRA-DOMAINS-VERIFIED**: Tenant domain verification
- **ENTRA-SECURITY-DEFAULTS-OR-CA**: Security Defaults or CA baseline
- **ENTRA-MFA-REQUIRED-ALL-USERS**: MFA for all users
- **ENTRA-PIM-CRITICAL-ROLES**: PIM for critical roles
- **ENTRA-SSPR-ENABLED**: Self-Service Password Reset
- **ENTRA-LOGS-TO-LOG-ANALYTICS**: Logs sent to Log Analytics

### Billing Checks
- **BILLING-BUDGETS-PER-SUBSCRIPTION**: Budgets per subscription
- **BILLING-COST-EXPORTS**: Cost export configurations
- **BILLING-RBAC-SEPARATION**: RBAC separation for billing

## How It Works

### Execution Flow

1. **Trigger**: HTTP (`/api/assessment/run`) or Timer
2. **Discovery**: Capture tenant and subscriptions
3. **Load Spec**: Download JSON specification
4. **Execution**: Parallel fan-out of checks
5. **Evaluation**: Apply declarative logic
6. **Persistence**: Save results
7. **Result**: Return structured output

### Declarative Logic

Example check:
```json
{
  "id": "ENTRA-DOMAINS-VERIFIED",
  "logic": {
    "allOf": [
      { "gte": { "path": "$.domains.length()", "value": 1 } },
      { "equals": { "path": "$.domains[?(@.isVerified==false)].length()", "value": 0 } }
    ]
  }
}
```

### Result States

- **Compliant**: Rule satisfied
- **NonCompliant**: Rule not satisfied
- **ManualRequired**: No reliable API (requires attestation)
- **NotApplicable**: Not applicable to scope
- **Exempted**: Policy exception exists
- **Error**: Execution failure

## Getting Started

### Prerequisites

- .NET 8 SDK
- Azure Functions Core Tools v4
- Azure subscription with appropriate permissions

### Local Development

1. Configure `src/LzAssessor.NewVersion/local.settings.json`:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Assessment:SpecUrl": "https://raw.githubusercontent.com/lcarli/lz-assessor/main/specs/billing_entra.json"
  }
}
```

2. Run the application:
```bash
cd src/LzAssessor.NewVersion
func start
```

### Available Endpoints

- `POST /api/assessment/run` - Execute assessment
- `GET /api/assessment/last?scope={tenantId}` - Get latest assessment results
- `GET /api/assessment/history?tenantId={id}` - Assessment history

## Infrastructure

- **Workbook Template**: `templates/workbook.json` - Azure Workbook for results visualization
- **ARM Templates**: Ready for deployment with Log Analytics integration

## Configuration

### Required Permissions

**Managed Identity**:
- Reader on evaluated scopes
- Security Reader
- Policy Insights Reader
- Cost Management Reader

**App Registration (Graph)**:
- Directory.Read.All
- Policy.Read.All
- Reports.Read.All
- PrivilegedAccess.Read.AzureAD (for PIM)

### Persistence

- **Development**: In-memory
- **Production**: Log Analytics (configure `LogAnalytics:WorkspaceId`)

## Extensibility

### New Checks
- Add to spec JSON file (no redeploy required)
- Flexible declarative logic

### New Executors
- Implement `IExecutor`
- Register in DI
- Reference in spec

### New Profiles
- Different thresholds via spec
- Baseline/Regulated/Strict

## Quick Roadmap Overview

1. ✅ Base implementation with initial category
2. ✅ Complete API integration (Graph, Cost Management)
3. ✅ HTTP endpoints (POST /assessment/run, GET /assessment/last)
4. ✅ Workbook template for visualization
5. ✅ Durable Functions for robust orchestration
6. ⏳ Real Log Analytics persistence (Data Collection API)
7. ✅ Attestations for manual checks
8. ⏳ New categories (Network, Security, etc.)

**📋 See [Detailed Roadmap](docs/ROADMAP.md)** for comprehensive development plans, milestones, and important limitations including AI-generated specification constraints.

## Technical Notes

- **Zero hardcode**: Rules defined in JSON specs
- **Versioning**: RunId, SpecVersion, ChecklistCommit
- **Testability**: Mockable executors with recorded payloads
- **Observability**: Metrics per executor and coverage
- **⚠️ AI Limitation**: Specifications cannot be automatically generated using AI tools

This implementation provides a solid foundation for the complete Landing Zone assessment system as specified.

## Documentation

### 📖 Comprehensive Guides

- **[Architecture Documentation](docs/ARCHITECTURE.md)** - Complete system architecture with visual diagrams and component explanations
- **[Detailed Workflow](docs/WORKFLOW.md)** - Step-by-step explanation of assessment execution from request to results
- **[Specification Creation Guide](docs/SPEC_CREATION.md)** - Comprehensive guide for creating new assessment specifications and adding questions
- **[Project Roadmap](docs/ROADMAP.md)** - Development roadmap with completed milestones, planned features, and important limitations

### 🚀 Deployment and Setup

- **[Deployment Guide](docs/DEPLOYMENT.md)** - Complete deployment instructions including infrastructure setup, permissions, and monitoring
- **[macOS ARM64 Setup](docs/MACOS_ARM64_SETUP.md)** - Troubleshooting guide for Apple Silicon development environments

### ⚠️ Important Limitations

**AI-Generated Specifications**: Specifications cannot be automatically generated using AI tools. Each spec must be carefully crafted by human experts who understand compliance requirements, Azure APIs, and business logic. See the [Roadmap](docs/ROADMAP.md) for detailed explanation.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Review the [Specification Creation Guide](docs/SPEC_CREATION.md) for adding new checks
4. Follow the [Architecture Documentation](docs/ARCHITECTURE.md) for system understanding
5. Ensure tests pass and documentation is updated
6. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.