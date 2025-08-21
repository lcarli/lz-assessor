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

- **POST** `/api/assessment/run` - Start a new assessment
- **GET** `/api/assessment/last?scope={tenantId}` - Get latest assessment results  
- **GET** `/api/assessment/history?tenantId={tenantId}` - Get assessment history

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

## Roadmap

1. ✅ Base implementation with initial category
2. ✅ Complete API integration (Graph, Cost Management)
3. ✅ HTTP endpoints (POST /assessment/run, GET /assessment/last)
4. ✅ Workbook template for visualization
5. ⏳ Durable Functions for robust orchestration
6. ⏳ Real Log Analytics persistence (Data Collection API)
7. ⏳ Attestations for manual checks
8. ⏳ New categories (Network, Security, etc.)

## Technical Notes

- **Zero hardcode**: Rules defined in JSON specs
- **Versioning**: RunId, SpecVersion, ChecklistCommit
- **Testability**: Mockable executors with recorded payloads
- **Observability**: Metrics per executor and coverage

This implementation provides a solid foundation for the complete Landing Zone assessment system as specified.

## Development on macOS ARM64

If you're developing on Apple Silicon Macs and encounter architecture compatibility issues, see our [macOS ARM64 Setup Guide](docs/MACOS_ARM64_SETUP.md) for detailed troubleshooting steps.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Ensure tests pass
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.