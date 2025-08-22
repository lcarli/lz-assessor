# LZ Assessor - Creating New Specifications Guide

## Overview

This comprehensive guide explains how to create new assessment specifications (specs) for LZ Assessor, add new questions, and extend the system to evaluate additional aspects of Azure Landing Zones.

**⚠️ Important Note: AI-Generated Specs Limitation**
> **Specifications cannot be automatically generated using AI tools.** Each spec must be carefully crafted by human experts who understand the specific compliance requirements, Azure APIs, and business logic needed for accurate assessments. AI tools lack the domain expertise and contextual understanding required to create reliable, actionable assessment rules.

## Specification Structure Overview

### Basic Spec File Anatomy

```json
{
  "specVersion": "2.0.0",
  "category": "Category Name",
  "profile": "baseline",
  "defaults": {
    "scope": "tenant",
    "severity": "High", 
    "coverage": "Auto",
    "attestation": {
      "enabled": true,
      "expiresDays": 180
    }
  },
  "checks": [
    // Individual check definitions
  ]
}
```

### Key Components Explained

**SpecVersion**: Version identifier for tracking changes and ensuring compatibility
**Category**: Logical grouping (e.g., "Network Security", "Identity Management")
**Profile**: Assessment intensity level (baseline, regulated, strict)
**Defaults**: Common settings applied to all checks unless overridden
**Checks**: Array of individual assessment rules

## Step-by-Step Spec Creation Process

### Step 1: Define Spec Metadata

Choose appropriate values for your assessment category:

```json
{
  "specVersion": "2.0.0",
  "category": "Network Security and Connectivity", 
  "profile": "baseline",
  "defaults": {
    "scope": "subscription",        // tenant, subscription, resourceGroup
    "severity": "Medium",           // Low, Medium, High, Critical
    "coverage": "Manual",           // Auto, Manual, Hybrid
    "attestation": {
      "enabled": true,
      "expiresDays": 90            // Attestation validity period
    }
  }
}
```

**Best Practices:**
- Use descriptive, consistent category names
- Start with "baseline" profile for initial implementation
- Set realistic default severity levels
- Enable attestation for manual checks

### Step 2: Plan Your Assessment Questions

Before writing checks, document:

1. **Compliance Requirements**
   - Which standards/frameworks apply (NIST, CIS, ISO 27001)
   - Specific control requirements
   - Risk levels and business impact

2. **Technical Requirements**
   - Which Azure APIs provide the necessary data
   - Authentication and permission requirements
   - Data sources and query patterns

3. **Assessment Scope**
   - Tenant-wide vs subscription-specific
   - Resource type targeting
   - Exclusion criteria

**Example Planning Document:**
```markdown
## Network Security Assessment Plan

### Requirements
- NSG rules follow least privilege principle
- Subnets have appropriate NSGs attached
- Network Watcher enabled in each region
- DDoS Protection Standard configured

### Data Sources
- ARM: Network Security Groups, Virtual Networks
- ARM: Network Watcher instances  
- ARM: DDoS Protection Plans

### Scope
- All subscriptions in tenant
- Focus on production environments
- Exclude sandbox subscriptions
```

### Step 3: Create Individual Checks

#### Check Structure Template

```json
{
  "id": "NETWORK-NSG-ATTACHED",
  "title": "Network Security Groups attached to all subnets",
  "severity": "High",
  "executor": "arm",
  "sources": ["ARM.Networks.VirtualNetworks", "ARM.Networks.NetworkSecurityGroups"],
  "scope": "subscription",
  "logic": {
    // Declarative logic definition
  },
  "evidence": {
    "summaryTemplate": "NSG attachment status for subnets",
    "linkTemplates": [
      "https://portal.azure.com/#@{tenantId}/resource/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}/overview"
    ],
    "capture": ["subnetCount", "unprotectedSubnets"]
  },
  "fallback": {
    "manualRequired": false,
    "reason": null
  },
  "remediationHint": "Attach Network Security Groups to unprotected subnets",
  "metadata": {
    "contractTypes": ["EnterpriseAgreement", "MicrosoftCustomerAgreement"],
    "subcategory": "NetworkSecurity", 
    "framework": ["NIST", "CIS"],
    "automationLevel": "Full"
  }
}
```

#### Field-by-Field Explanation

**id**: Unique identifier using convention: `CATEGORY-SUBCATEGORY-DESCRIPTION`
- Examples: `NETWORK-NSG-ATTACHED`, `IAM-RBAC-SEPARATION`, `SECURITY-DEFENDER-ENABLED`

**title**: Human-readable description of the check
- Be specific and actionable
- Focus on the desired outcome, not the current state

**severity**: Impact level (Critical, High, Medium, Low)
- Critical: Security vulnerabilities, compliance violations
- High: Best practice deviations with significant risk
- Medium: Optimization opportunities
- Low: Informational findings

**executor**: Which execution engine handles this check
- `graph`: Microsoft Graph / Entra ID APIs
- `arm`: Azure Resource Manager APIs 
- `cost`: Cost Management APIs
- `manual`: Human validation required

**sources**: Array of data sources required
- Follow naming convention: `{API}.{Service}.{Resource}`
- Examples: `Graph.v1.Domains`, `ARM.Compute.VirtualMachines`, `Cost.Management.Budgets`

### Step 4: Define Declarative Logic

#### Logic Operators Reference

**Basic Operators:**
```json
{
  "exists": { "path": "$.property" },
  "equals": { "path": "$.value", "value": "expected" },
  "gte": { "path": "$.count", "value": 1 },
  "lte": { "path": "$.limit", "value": 100 },
  "contains": { "path": "$.array", "value": "item" },
  "not": { /* negation of any condition */ }
}
```

**Compound Operators:**
```json
{
  "allOf": [
    { "exists": { "path": "$.requiredProperty" } },
    { "gte": { "path": "$.count", "value": 1 } }
  ],
  "anyOf": [
    { "equals": { "path": "$.status", "value": "enabled" } },
    { "equals": { "path": "$.status", "value": "active" } }
  ]
}
```

#### JSONPath Expressions

LZ Assessor uses JSONPath for data navigation:

```json
// Basic property access
"$.virtualNetworks.length()"

// Array filtering  
"$.subnets[?(@.networkSecurityGroup)]"

// Complex filtering
"$.virtualMachines[?(@.properties.osProfile.adminUsername != 'azureuser')]"

// Aggregation
"$.budgets[?(@.properties.amount.value > 1000)].length()"
```

#### Real-World Logic Examples

**Example 1: NSG Attachment Check**
```json
{
  "logic": {
    "allOf": [
      { "exists": { "path": "$.virtualNetworks" } },
      { "gte": { "path": "$.virtualNetworks.length()", "value": 1 } },
      {
        "equals": { 
          "path": "$.virtualNetworks[*].properties.subnets[?(!@.properties.networkSecurityGroup)].length()", 
          "value": 0 
        }
      }
    ]
  }
}
```

**Example 2: Budget Configuration Check**
```json
{
  "logic": {
    "allOf": [
      { "gte": { "path": "$.subscriptions.length()", "value": 1 } },
      {
        "anyOf": [
          { "gte": { "path": "$.budgets.length()", "value": 1 } },
          { "exists": { "path": "$.costAlerts[?(@.enabled == true)]" } }
        ]
      }
    ]
  }
}
```

**Example 3: Multi-Factor Authentication Check**
```json
{
  "logic": {
    "allOf": [
      { "exists": { "path": "$.conditionalAccessPolicies" } },
      { 
        "gte": { 
          "path": "$.conditionalAccessPolicies[?(@.grantControls.builtInControls[*] == 'mfa')].length()", 
          "value": 1 
        } 
      },
      {
        "equals": {
          "path": "$.conditionalAccessPolicies[?(@.grantControls.builtInControls[*] == 'mfa' && @.state == 'enabled')].length()",
          "value": "$.conditionalAccessPolicies[?(@.grantControls.builtInControls[*] == 'mfa')].length()"
        }
      }
    ]
  }
}
```

### Step 5: Configure Evidence Templates

Evidence templates generate human-readable summaries and actionable links:

```json
{
  "evidence": {
    "summaryTemplate": "Found {virtualNetworks.length()} virtual networks with {unprotectedSubnets.length()} unprotected subnets",
    "linkTemplates": [
      "https://portal.azure.com/#@{tenantId}/resource/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}/subnets"
    ],
    "capture": [
      "virtualNetworks.length() as vnetCount",
      "virtualNetworks[*].properties.subnets[?(!@.properties.networkSecurityGroup)] as unprotectedSubnets"
    ]
  }
}
```

**Template Variables:**
- Use `{variable}` syntax for dynamic values
- Variables come from API responses and JSONPath expressions
- Common variables: `{tenantId}`, `{subscriptionId}`, `{resourceGroupName}`

**Best Practices:**
- Provide specific counts and lists in summaries
- Include direct links to relevant Azure Portal pages
- Capture diagnostic data for troubleshooting

### Step 6: Add Metadata and Classification

```json
{
  "metadata": {
    "contractTypes": ["EnterpriseAgreement", "MicrosoftCustomerAgreement"],
    "subcategory": "NetworkSecurity",
    "framework": ["NIST-800-53", "CIS"],
    "controlId": ["SC-7", "4.1"],
    "automationLevel": "Full",
    "dataClassification": "Technical",
    "businessImpact": "High",
    "lastReviewed": "2024-01-15",
    "reviewer": "Security Team"
  }
}
```

## Creating Executors for New Data Sources

### When to Create a New Executor

Create a new executor when you need to:
- Access a new Azure API family (e.g., Defender, Backup)
- Implement custom authentication patterns
- Add non-Azure data sources
- Apply specialized data transformation logic

### Executor Implementation Template

```csharp
using LzAssessor.NewVersion.Models;

namespace LzAssessor.NewVersion.Executors;

public sealed class CustomExecutor : ExecutorBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    
    public override string Name => "custom";

    public CustomExecutor(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public override async Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Collect API data based on check.Sources
            var apiData = await CollectApiDataAsync(check.Sources, tenantId, scope, cancellationToken);
            
            // 2. Evaluate logic against collected data
            var isCompliant = await _evaluator.EvaluateAsync(check.Logic, apiData);
            
            // 3. Generate evidence
            var evidence = GenerateEvidence(check.Evidence, apiData);
            
            // 4. Return result
            return new AssessmentResult(
                RunId: runId,
                TenantId: tenantId,
                Scope: scope,
                QuestionId: check.Id,
                Title: check.Title,
                Pillar: DeterminePillar(check),
                Status: isCompliant ? AssessmentStatus.Compliant : AssessmentStatus.NonCompliant,
                Severity: check.Severity,
                Coverage: "Auto",
                Evidence: evidence,
                Metadata: CreateMetadata(check),
                EvaluatedAt: DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            return CreateErrorResult(runId, tenantId, scope, check, ex);
        }
    }

    private async Task<Dictionary<string, object>> CollectApiDataAsync(
        string[] sources, 
        string tenantId, 
        string scope, 
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, object>();
        
        foreach (var source in sources)
        {
            switch (source)
            {
                case "Custom.API.Resource":
                    data["resource"] = await CallCustomApiAsync(tenantId, scope, cancellationToken);
                    break;
                    
                default:
                    throw new NotSupportedException($"Source '{source}' not supported by {Name} executor");
            }
        }
        
        return data;
    }

    private async Task<object> CallCustomApiAsync(string tenantId, string scope, CancellationToken cancellationToken)
    {
        // Implement custom API logic
        var token = await GetAccessTokenAsync();
        var response = await _httpClient.GetAsync($"https://api.example.com/data?tenant={tenantId}&scope={scope}", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<object>(cancellationToken);
    }
}
```

### Register New Executor

Add to `Program.cs`:
```csharp
services.AddSingleton<IExecutor, CustomExecutor>();
```

## Testing Your Specifications

### Local Testing Setup

1. **Create Test Spec File**
   ```json
   // /specs/test_network.json
   {
     "specVersion": "2.0.0",
     "category": "Network Security Test",
     "profile": "baseline",
     "checks": [
       // Your test checks
     ]
   }
   ```

2. **Configure Local Settings**
   ```json
   // local.settings.json
   {
     "Values": {
       "Assessment:SpecUrl": "", // Empty for local file loading
     }
   }
   ```

3. **Run Local Assessment**
   ```bash
   cd src/LzAssessor.NewVersion
   func start
   
   # Test your spec
   curl -X POST "http://localhost:7071/api/assessment/run" \
     -H "Content-Type: application/json" \
     -d '{"specUrl": "test_network.json", "tenantId": "your-tenant-id"}'
   ```

### Validation Checklist

Before deploying your spec:

- [ ] **JSON Validation**: Spec file parses correctly
- [ ] **Logic Syntax**: All JSONPath expressions are valid  
- [ ] **Data Sources**: Required APIs are accessible
- [ ] **Permissions**: Executor has appropriate Azure roles
- [ ] **Evidence**: Templates generate meaningful output
- [ ] **Error Handling**: Graceful failure for missing data
- [ ] **Performance**: Checks complete within reasonable time
- [ ] **Documentation**: Metadata is complete and accurate

### Common Issues and Solutions

**Issue: JSONPath Expression Errors**
```json
// ❌ Incorrect - Missing quotes
{ "path": $.property, "value": "test" }

// ✅ Correct
{ "path": "$.property", "value": "test" }
```

**Issue: Missing Data Sources**
```json
// ❌ Executor doesn't support this source
"sources": ["NonExistent.API.Resource"]

// ✅ Use supported sources
"sources": ["ARM.Network.VirtualNetworks"]
```

**Issue: Overly Complex Logic**
```json
// ❌ Complex nested logic hard to debug
{
  "allOf": [
    {
      "anyOf": [
        { "allOf": [ /* deeply nested */ ] }
      ]
    }
  ]
}

// ✅ Simpler, more maintainable logic
{
  "allOf": [
    { "exists": { "path": "$.resource" } },
    { "gte": { "path": "$.resource.count", "value": 1 } }
  ]
}
```

## Deployment and Distribution

### Spec File Hosting

**Option 1: GitHub Repository**
```bash
# Host in GitHub for version control
https://raw.githubusercontent.com/yourorg/lz-specs/main/network_security.json
```

**Option 2: Azure Storage**
```bash
# Host in Azure Blob Storage for enterprise distribution
https://mystorageaccount.blob.core.windows.net/specs/network_security.json
```

**Option 3: Local File System**
```bash
# Local development and testing
/specs/network_security.json
```

### Versioning Strategy

```json
{
  "specVersion": "2.1.0", // Update when adding/changing checks
  "category": "Network Security",
  "metadata": {
    "lastModified": "2024-01-15",
    "changeLog": [
      "2.1.0: Added DDoS protection checks",
      "2.0.0: Initial network security implementation"
    ]
  }
}
```

### Production Deployment

1. **Test in Development**
   - Validate with sample tenants
   - Performance testing with large environments
   - Security review of new data access

2. **Staged Rollout**
   - Deploy to test environment first
   - Gradually enable for production tenants
   - Monitor for errors and performance issues

3. **Documentation Updates**
   - Update API documentation
   - Create user guides for new checks
   - Update compliance mapping documentation

## Best Practices Summary

### Specification Design
- ✅ Start simple, iterate based on feedback
- ✅ Use consistent naming conventions
- ✅ Provide meaningful evidence and remediation hints
- ✅ Test thoroughly before production deployment
- ❌ Don't create overly complex logic expressions
- ❌ Avoid hardcoding tenant-specific values
- ❌ Don't skip error handling and fallback scenarios

### Security Considerations
- ✅ Follow principle of least privilege for API access
- ✅ Validate all input data and API responses
- ✅ Log security-relevant events and errors
- ❌ Don't store sensitive data in specs or logs
- ❌ Avoid exposing internal system details in evidence

### Performance Optimization
- ✅ Batch API calls where possible
- ✅ Use appropriate JSONPath expressions
- ✅ Cache frequently accessed data
- ❌ Don't create inefficient nested loops in logic
- ❌ Avoid unnecessary API calls for large datasets

This comprehensive guide provides the foundation for creating robust, maintainable specifications that extend LZ Assessor's capabilities while maintaining security, performance, and reliability standards.