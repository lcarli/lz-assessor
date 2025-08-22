# LZ Assessor - Detailed Workflow Documentation

## Overview

This document provides a comprehensive, step-by-step explanation of how LZ Assessor executes Landing Zone assessments, from initial request to final results.

## Complete Assessment Workflow

### Phase 1: Request Initiation

#### Step 1.1: API Request Reception
```http
POST /api/assessment/run-orchestrated
Content-Type: application/json

{
  "tenantId": "12345678-1234-1234-1234-123456789abc",
  "specUrl": "https://example.com/specs/billing_entra.json"
}
```

**What happens:**
- Azure Functions HTTP trigger receives the request
- Request validation occurs (JSON format, required fields)
- Unique RunId is generated if not provided
- Request is queued for orchestrator processing

#### Step 1.2: Orchestrator Activation
```csharp
[FunctionName("AssessmentOrchestrator")]
public async Task<AssessmentRun> RunAssessment(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
```

**What happens:**
- Durable Functions orchestrator takes control
- Request parameters are extracted and validated
- Execution context is established with fault tolerance
- Orchestrator begins coordinating the assessment pipeline

### Phase 2: Environment Discovery

#### Step 2.1: Tenant Discovery
```csharp
public async Task<DiscoverySnapshot> DiscoverEnvironmentAsync(
    string tenantId, 
    CancellationToken cancellationToken)
```

**Detailed Process:**
1. **Authentication Setup**
   - Managed Identity token acquisition for Azure APIs
   - App Registration token for Microsoft Graph APIs
   - Permission validation for target tenant

2. **Tenant Information Collection**
   ```csharp
   var tenantInfo = await graphClient.Organization
       .Request()
       .GetAsync(cancellationToken);
   ```
   - Retrieves basic tenant metadata
   - Validates tenant accessibility
   - Captures tenant display name and verified domains

3. **Subscription Enumeration**
   ```csharp
   var subscriptions = await armClient.Subscriptions
       .ListAsync(cancellationToken);
   ```
   - Lists all accessible subscriptions in tenant
   - Filters by subscription state (active only)
   - Captures subscription metadata (name, ID, state)

4. **Permission Verification**
   - Validates required roles on each subscription
   - Checks Graph API permissions
   - Documents any access limitations

**Output:**
```json
{
  "tenantId": "12345678-1234-1234-1234-123456789abc",
  "subscriptions": ["sub1-guid", "sub2-guid"],
  "metadata": {
    "tenantName": "Contoso Corp",
    "verifiedDomains": ["contoso.com"],
    "capturedAt": "2024-01-15T10:30:00Z",
    "accessLevel": "FullAccess"
  }
}
```

### Phase 3: Specification Loading

#### Step 3.1: Spec Source Determination
```csharp
var useLocalSpecs = string.IsNullOrEmpty(configuration["Assessment:SpecUrl"]);
ISpecLoader loader = useLocalSpecs ? 
    new FileSpecLoader() : 
    new HttpSpecLoader(httpClientFactory, configuration);
```

**Decision Logic:**
- **Local Development**: Uses file-based loader for specs in `/specs` directory
- **Production**: Uses HTTP loader to fetch specs from configured URL
- **Custom Request**: Uses URL provided in assessment request

#### Step 3.2: Spec File Loading
```csharp
public async Task<SpecFile> LoadAsync(string? specUrl, CancellationToken cancellationToken)
{
    var fileName = specUrl ?? "billing_entra.json";
    var filePath = Path.Combine(_basePath, fileName);
    
    using var stream = File.OpenRead(filePath);
    var spec = await JsonSerializer.DeserializeAsync<SpecFile>(stream, JsonOptions, cancellationToken);
    
    return spec;
}
```

**Detailed Process:**
1. **URL Resolution**
   - Determines spec source (local file vs HTTP endpoint)
   - Resolves relative paths for local development
   - Validates URL format and accessibility

2. **Spec Download/Load**
   - Downloads spec file over HTTPS (production)
   - Reads local file (development)
   - Handles network failures with retry logic

3. **Spec Validation**
   - JSON schema validation
   - Required field verification (specVersion, category, checks)
   - Logic expression syntax validation

4. **Spec Parsing**
   - Deserializes JSON to strongly-typed C# objects
   - Validates check definitions and logic expressions
   - Applies default values from spec configuration

**Example Loaded Spec:**
```json
{
  "specVersion": "2.0.0",
  "category": "Azure Billing and Microsoft Entra ID Tenants",
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
    {
      "id": "ENTRA-DOMAINS-VERIFIED",
      "title": "All tenant domains must be verified",
      "executor": "graph",
      "sources": ["Graph.v1.Domains"],
      "logic": {
        "allOf": [
          { "gte": { "path": "$.domains.length()", "value": 1 } },
          { "equals": { "path": "$.domains[?(@.isVerified==false)].length()", "value": 0 } }
        ]
      }
    }
  ]
}
```

### Phase 4: Check Execution Planning

#### Step 4.1: Execution Plan Generation
```csharp
public async Task<ExecutionPlan> CreateExecutionPlanAsync(
    SpecFile spec, 
    DiscoverySnapshot snapshot)
```

**Detailed Process:**
1. **Check Filtering**
   - Applies design area filters (if specified)
   - Filters by contract type compatibility
   - Excludes disabled or not applicable checks

2. **Executor Assignment**
   - Maps each check to appropriate executor based on `executor` field
   - Validates executor availability and registration
   - Groups checks by executor for batch processing

3. **Dependency Analysis**
   - Identifies checks with prerequisite data requirements
   - Creates execution order to minimize API calls
   - Optimizes for parallel execution where possible

4. **Resource Scoping**
   - Determines execution scope (tenant, subscription, resource group)
   - Maps checks to appropriate Azure subscriptions
   - Validates permission requirements for each scope

**Execution Plan Output:**
```json
{
  "totalChecks": 25,
  "executorGroups": [
    {
      "executor": "graph",
      "checks": ["ENTRA-DOMAINS-VERIFIED", "ENTRA-MFA-REQUIRED"],
      "estimatedDuration": "30s",
      "requiredPermissions": ["Directory.Read.All"]
    },
    {
      "executor": "cost",
      "checks": ["BILLING-BUDGETS-PER-SUBSCRIPTION"],
      "estimatedDuration": "45s",
      "requiredPermissions": ["Cost Management Reader"]
    }
  ]
}
```

### Phase 5: Parallel Check Execution

#### Step 5.1: Executor Coordination
```csharp
var tasks = executorGroups.Select(group => 
    ExecuteChecksForExecutorAsync(group.Executor, group.Checks, snapshot)
).ToArray();

var results = await Task.WhenAll(tasks);
```

**Orchestration Details:**
- Parallel execution across different executors
- Fault isolation - failure in one executor doesn't stop others
- Progress tracking and intermediate result collection
- Timeout handling for long-running operations

#### Step 5.2: Individual Check Execution

**Graph Executor Example:**
```csharp
public async Task<AssessmentResult> ExecuteAsync(
    string runId,
    string tenantId, 
    string scope,
    SpecCheck check,
    DiscoverySnapshot snapshot)
{
    // 1. API Data Collection
    var apiData = await CollectApiDataAsync(check.Sources);
    
    // 2. Logic Evaluation  
    var isCompliant = await _evaluator.EvaluateAsync(check.Logic, apiData);
    
    // 3. Evidence Generation
    var evidence = GenerateEvidence(check.Evidence, apiData);
    
    // 4. Result Assembly
    return new AssessmentResult(
        runId, tenantId, scope, check.Id, check.Title,
        DeterminePillar(check),
        isCompliant ? AssessmentStatus.Compliant : AssessmentStatus.NonCompliant,
        check.Severity, "Auto", evidence, metadata, DateTimeOffset.UtcNow
    );
}
```

**Detailed Execution Steps:**

1. **API Data Collection**
   ```csharp
   foreach (var source in check.Sources)
   {
       switch (source)
       {
           case "Graph.v1.Domains":
               data["domains"] = await graphClient.Domains.Request().GetAsync();
               break;
           case "Graph.v1.Policies":
               data["policies"] = await graphClient.Policies.Request().GetAsync();
               break;
       }
   }
   ```

2. **Logic Evaluation Process**
   ```csharp
   public async Task<bool> EvaluateAsync(JsonElement logic, Dictionary<string, object> data)
   {
       if (logic.TryGetProperty("allOf", out var allOfElement))
       {
           var conditions = allOfElement.EnumerateArray();
           return await conditions.AllAsync(condition => EvaluateAsync(condition, data));
       }
       
       if (logic.TryGetProperty("equals", out var equalsElement))
       {
           var path = equalsElement.GetProperty("path").GetString();
           var expectedValue = equalsElement.GetProperty("value");
           var actualValue = JSONPath.Evaluate(path, data);
           return actualValue.Equals(expectedValue);
       }
       
       // Additional logic operators...
   }
   ```

3. **Evidence Generation**
   ```csharp
   private Evidence GenerateEvidence(SpecEvidence evidenceConfig, Dictionary<string, object> apiData)
   {
       var summary = TemplateEngine.Render(evidenceConfig.SummaryTemplate, apiData);
       var links = evidenceConfig.LinkTemplates?.Select(template => 
           TemplateEngine.Render(template, apiData));
           
       return new Evidence(summary, links, JsonSerializer.Serialize(apiData));
   }
   ```

#### Step 5.3: Manual Check Handling
```csharp
public class ManualExecutor : ExecutorBase
{
    public override Task<AssessmentResult> ExecuteAsync(...)
    {
        return Task.FromResult(new AssessmentResult(
            runId, tenantId, scope, check.Id, check.Title,
            DeterminePillar(check),
            AssessmentStatus.Manually, // Requires human attestation
            check.Severity, "Manual",
            new Evidence(check.Fallback?.Reason ?? "Manual assessment required"),
            metadata, DateTimeOffset.UtcNow
        ));
    }
}
```

### Phase 6: Result Aggregation

#### Step 6.1: Result Collection
```csharp
var allResults = new List<AssessmentResult>();
foreach (var executorResults in parallelExecutionResults)
{
    allResults.AddRange(executorResults.Where(r => r != null));
}
```

**Aggregation Process:**
1. **Result Consolidation**
   - Collects results from all executor tasks
   - Handles partial failures and timeouts
   - Applies result validation and sanitization

2. **Statistics Calculation**
   ```csharp
   var summary = new AssessmentSummary(
       totalChecks: allResults.Count,
       compliantChecks: allResults.Count(r => r.Status == AssessmentStatus.Compliant),
       nonCompliantChecks: allResults.Count(r => r.Status == AssessmentStatus.NonCompliant),
       manualChecks: allResults.Count(r => r.Status == AssessmentStatus.Manually)
   );
   ```

3. **Quality Assurance**
   - Validates result completeness
   - Checks for missing required fields
   - Applies business rules for result classification

#### Step 6.2: Attestation Integration
```csharp
public async Task<AssessmentResult[]> ApplyAttestationsAsync(
    AssessmentResult[] results, 
    string tenantId)
{
    var attestations = await _attestationPersistence.GetValidAttestationsAsync(tenantId);
    
    return results.Select(result => 
    {
        var attestation = attestations.FirstOrDefault(a => a.CheckId == result.QuestionId);
        if (attestation != null && !attestation.IsExpired)
        {
            return result with { 
                Status = attestation.Status,
                Evidence = result.Evidence with { 
                    Summary = $"Attested: {attestation.Comments}" 
                }
            };
        }
        return result;
    }).ToArray();
}
```

### Phase 7: Result Persistence

#### Step 7.1: Persistence Strategy Selection
```csharp
var useLogAnalytics = !string.IsNullOrEmpty(configuration["LogAnalytics:WorkspaceId"]);
IAssessmentPersistence persistence = useLogAnalytics ? 
    new LogAnalyticsPersistence(logsQueryClient) : 
    new InMemoryPersistence();
```

#### Step 7.2: Data Persistence
```csharp
public async Task StoreAssessmentAsync(AssessmentRun assessment)
{
    // Log Analytics Table Structure
    var logEntry = new
    {
        RunId = assessment.RunId,
        TenantId = assessment.TenantId,
        SpecVersion = assessment.SpecVersion,
        Category = assessment.Category,
        StartedAt = assessment.StartedAt,
        CompletedAt = assessment.CompletedAt,
        TotalChecks = assessment.TotalChecks,
        CompliantChecks = assessment.CompliantChecks,
        NonCompliantChecks = assessment.NonCompliantChecks,
        ManualChecks = assessment.ManualChecks,
        Results = assessment.Results.Select(r => new
        {
            QuestionId = r.QuestionId,
            Title = r.Title,
            Status = r.Status.ToString(),
            Severity = r.Severity,
            Evidence = r.Evidence.Summary,
            EvaluatedAt = r.EvaluatedAt
        })
    };
    
    await _logsIngestionClient.UploadAsync(
        ruleId: _dataCollectionRuleId,
        streamName: "Custom-ALZ_Assessment_CL",
        logs: new[] { logEntry });
}
```

### Phase 8: Response Generation

#### Step 8.1: API Response Assembly
```csharp
var response = new AssessmentRun(
    RunId: runId,
    TenantId: request.TenantId,
    SpecVersion: spec.SpecVersion,
    Category: spec.Category,
    Results: finalResults,
    Snapshot: discoverySnapshot,
    StartedAt: startTime,
    CompletedAt: DateTimeOffset.UtcNow,
    TotalChecks: finalResults.Length,
    CompliantChecks: finalResults.Count(r => r.Status == AssessmentStatus.Compliant),
    NonCompliantChecks: finalResults.Count(r => r.Status == AssessmentStatus.NonCompliant),
    ManualChecks: finalResults.Count(r => r.Status == AssessmentStatus.Manually)
);
```

#### Step 8.2: Client Response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "runId": "run-2024-01-15-10-30-00",
  "tenantId": "12345678-1234-1234-1234-123456789abc",
  "specVersion": "2.0.0",
  "category": "Azure Billing and Microsoft Entra ID Tenants",
  "startedAt": "2024-01-15T10:30:00Z",
  "completedAt": "2024-01-15T10:32:15Z",
  "totalChecks": 25,
  "compliantChecks": 18,
  "nonCompliantChecks": 4,
  "manualChecks": 3,
  "results": [
    {
      "questionId": "ENTRA-DOMAINS-VERIFIED",
      "title": "All tenant domains must be verified",
      "pillar": "Identity",
      "status": "Compliant",
      "severity": "High",
      "evidence": {
        "summary": "All 2 domains are verified: contoso.com, contoso.onmicrosoft.com",
        "links": ["https://portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/CustomDomainNames"]
      },
      "evaluatedAt": "2024-01-15T10:30:45Z"
    }
  ]
}
```

## Error Handling Workflow

### Executor-Level Error Handling
```csharp
try
{
    var result = await executor.ExecuteAsync(runId, tenantId, scope, check, snapshot);
    return result;
}
catch (HttpRequestException ex) when (ex.Message.Contains("Forbidden"))
{
    return new AssessmentResult(
        runId, tenantId, scope, check.Id, check.Title, pillar,
        AssessmentStatus.Error, check.Severity, "Auto",
        new Evidence($"Access denied: {ex.Message}"),
        metadata, DateTimeOffset.UtcNow
    );
}
catch (Exception ex)
{
    _logger.LogError(ex, "Check execution failed for {CheckId}", check.Id);
    return new AssessmentResult(
        runId, tenantId, scope, check.Id, check.Title, pillar,
        AssessmentStatus.Error, check.Severity, "Auto",
        new Evidence($"Execution error: {ex.Message}"),
        metadata, DateTimeOffset.UtcNow
    );
}
```

### Orchestrator-Level Error Recovery
```csharp
[FunctionName("AssessmentOrchestrator")]
public async Task<AssessmentRun> RunAssessment(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
{
    try
    {
        var request = context.GetInput<AssessmentRequest>();
        
        // Step 1: Discovery with retry
        var snapshot = await context.CallActivityWithRetryAsync<DiscoverySnapshot>(
            "DiscoverEnvironment", 
            new RetryOptions(TimeSpan.FromSeconds(30), 3),
            request.TenantId);
            
        // Step 2: Spec loading with fallback
        SpecFile spec;
        try
        {
            spec = await context.CallActivityAsync<SpecFile>("LoadSpec", request.SpecUrl);
        }
        catch
        {
            // Fallback to default spec
            spec = await context.CallActivityAsync<SpecFile>("LoadSpec", null);
        }
        
        // Step 3: Parallel execution with partial failure tolerance
        var executionTasks = CreateExecutionTasks(spec, snapshot);
        var results = new List<AssessmentResult>();
        
        foreach (var task in executionTasks)
        {
            try
            {
                var taskResults = await task;
                results.AddRange(taskResults);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Executor task failed, continuing with partial results");
                // Continue with other tasks
            }
        }
        
        return new AssessmentRun(/* assembled from partial results */);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Assessment orchestration failed");
        throw new AssessmentException("Assessment failed", ex);
    }
}
```

## Performance Optimization Strategies

### 1. API Call Optimization
- Batch Graph API requests where possible
- Cache frequently accessed data within assessment run
- Use parallel execution for independent data sources
- Implement circuit breaker pattern for unreliable APIs

### 2. Result Caching
- Cache discovery data for tenant/subscription enumeration
- Cache spec files with version-based invalidation
- Implement result caching for identical assessment requests

### 3. Resource Management
- Connection pooling for HTTP clients
- Proper disposal of Azure SDK clients
- Memory-efficient JSON processing for large payloads

This comprehensive workflow documentation provides the foundation for understanding how LZ Assessor processes assessments from start to finish, with detailed explanations of each phase, error handling strategies, and performance considerations.