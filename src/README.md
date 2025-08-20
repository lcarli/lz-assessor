# LZ Assessor - New Version

Este é a nova versão do LZ Assessor, implementando a arquitetura completa especificada para avaliação automática de Landing Zones.

## Visão Geral

O LZ Assessor é um assessor automático de Landing Zone que:

- Lê checklists declarativos (JSON) começando pela categoria "Azure Billing and Microsoft Entra ID Tenants"
- Coleta evidências técnicas do tenant via APIs oficiais
- Classifica cada item (Compliant/Non-Compliant/etc.)
- Publica resultados em formato estruturado para consumo self-service

## Arquitetura

### Componentes Principais

1. **Azure Functions (Isolated .NET 8)**
   - Triggers HTTP e Timer
   - Engine de execução e roteamento
   - Sistema de persistência

2. **Executores (Plugins)**
   - `GraphExecutor`: Microsoft Graph / Entra ID
   - `ArmExecutor`: Azure Resource Manager
   - `CostExecutor`: Cost Management
   - Prontos para crescer: policy, defender, backup

3. **Engine de Avaliação**
   - `LogicEvaluator`: interpreta lógica declarativa
   - Suporte a `allOf`, `anyOf`, `exists`, `equals`, `lte`, `gte`, `contains`, `not`
   - Aplicação de predicados sobre payloads JSON

4. **Spec Declarativo**
   - Arquivo JSON com definições de checks
   - Lógica declarativa para cada check
   - Configuração de evidências e fallbacks

### Estrutura de Pastas

```
NEWVERSION/
├── LzAssessor.NewVersion/
│   ├── Models/              # Contratos e modelos de dados
│   ├── Engine/              # Core de execução e avaliação
│   ├── Executors/           # Implementação de executores por fonte
│   ├── Specs/               # Carregamento de especificações
│   ├── Persistence/         # Camada de persistência
│   ├── Triggers/            # HTTP e Timer triggers
│   └── Program.cs           # Configuração e DI
└── specs/
    └── billing_entra.json   # Spec para categoria inicial
```

## Categoria Inicial: Azure Billing and Microsoft Entra ID Tenants

A implementação inicial foca na categoria "Azure Billing and Microsoft Entra ID Tenants" com os seguintes checks:

### Checks do Entra ID
- **ENTRA-DOMAINS-VERIFIED**: Verificação de domínios do tenant
- **ENTRA-SECURITY-DEFAULTS-OR-CA**: Security Defaults ou baseline de CA
- **ENTRA-MFA-REQUIRED-ALL-USERS**: MFA para todos os usuários
- **ENTRA-PIM-CRITICAL-ROLES**: PIM para roles críticas
- **ENTRA-SSPR-ENABLED**: Self-Service Password Reset
- **ENTRA-LOGS-TO-LOG-ANALYTICS**: Logs enviados para Log Analytics

### Checks de Billing
- **BILLING-BUDGETS-PER-SUBSCRIPTION**: Budgets por subscription
- **BILLING-COST-EXPORTS**: Exports de custo configurados
- **BILLING-RBAC-SEPARATION**: Separação de RBAC para billing

## Como Funciona

### Fluxo de Execução

1. **Disparo**: HTTP (`/api/assessment/run`) ou Timer
2. **Discovery**: Captura tenant e subscriptions
3. **Load Spec**: Baixa especificação JSON
4. **Execução**: Fan-out paralelo dos checks
5. **Avaliação**: Aplicação de lógica declarativa
6. **Persistência**: Gravação dos resultados
7. **Resultado**: Retorno estruturado

### Lógica Declarativa

Exemplo de check:
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

### Estados de Resultado

- **Compliant**: Regra atendida
- **NonCompliant**: Regra não atendida
- **ManualRequired**: Sem API confiável (requer atestado)
- **NotApplicable**: Não se aplica ao escopo
- **Exempted**: Existe exceção policy
- **Error**: Falha de execução

## Executando

### Localmente

1. Configure `local.settings.json`:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Assessment:SpecUrl": "https://raw.githubusercontent.com/lcarli/lz-assessor/main/NEWVERSION/specs/billing_entra.json"
  }
}
```

2. Execute:
```bash
func start
```

### Endpoints Disponíveis

- `POST /api/assessment/run` - Executa avaliação
- `GET /api/assessment/history?tenantId={id}` - Histórico de avaliações

## Configuração

### Permissões Necessárias

**Managed Identity**:
- Reader nos escopos avaliados
- Security Reader
- Policy Insights Reader
- Cost Management Reader

**App Registration (Graph)**:
- Directory.Read.All
- Policy.Read.All
- Reports.Read.All
- PrivilegedAccess.Read.AzureAD (para PIM)

### Persistência

- **Desenvolvimento**: In-memory
- **Produção**: Log Analytics (configurar `LogAnalytics:WorkspaceId`)

## Compatibilidade com Apple Silicon (macOS ARM64)

Para resolver problemas de compatibilidade com macOS ARM64, certifique-se de:

### Pré-requisitos
1. **Azure Functions Core Tools v4** compatível com ARM64:
   ```bash
   # Desinstalar versão antiga se necessário
   npm uninstall -g azure-functions-core-tools
   
   # Instalar versão ARM64
   brew install azure/functions/azure-functions-core-tools@4
   ```

2. **.NET 8 SDK ARM64**:
   ```bash
   # Verificar versão instalada
   dotnet --version
   
   # Deve retornar 8.0.x
   # Se não, baixar .NET 8 ARM64 de https://dot.net
   ```

### Troubleshooting

**Erro "incompatible architecture (have 'x86_64', need 'arm64')":**
1. Limpar cache do NuGet: `dotnet nuget locals all --clear`
2. Executar com runtime específico: `dotnet run --runtime osx-arm64`
3. Definir variável de ambiente: `export DOTNET_CLI_ARCHITECTURE=arm64`
4. Usar func com arquitetura correta: `arch -arm64 func start`

**Se o problema persistir:**
- Verificar se o Rosetta 2 está instalado: `softwareupdate --install-rosetta`
- Reinstalar Azure Functions Core Tools via Homebrew
- Usar Docker como alternativa para desenvolvimento

## Extensibilidade

### Novos Checks
- Adicionar no arquivo spec JSON (sem redeploy)
- Lógica declarativa flexível

### Novos Executores
- Implementar `IExecutor`
- Registrar no DI
- Referenciar no spec

### Novos Perfis
- Diferentes thresholds via spec
- Baseline/Regulated/Strict

## Próximos Passos

1. ✅ Implementação base com categoria inicial
2. 🚧 Integração completa das APIs (Graph, Cost Management)
3. ⏳ Durable Functions para orquestração robusta
4. ⏳ Workbook do Azure para visualização
5. ⏳ Atestados para checks manuais
6. ⏳ Novas categorias (Network, Security, etc.)

## Observações Técnicas

- **Zero hardcode**: Regras definidas em specs JSON
- **Versionamento**: RunId, SpecVersion, ChecklistCommit
- **Testabilidade**: Executores mockáveis com payloads gravados
- **Observabilidade**: Métricas por executor e cobertura

Esta implementação fornece a base sólida para o sistema completo de avaliação de Landing Zones conforme especificado.