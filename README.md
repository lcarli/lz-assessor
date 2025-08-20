# LZ Assessor

Projeto Azure Functions (Isolated .NET 8) para avaliação de ambientes (Landing Zone) via múltiplos executores (ARM, Graph, Cost) e checklist de conformidade.

## Estrutura

```text
lz-assessor/
  src/
    LzAssessor.Functions/
      Orchestration/
      Specs/
      Engine/
      Models/
      Program.cs
      appsettings.json
    LzAssessor.Functions.csproj
```

## Conceitos

- Orchestration: (futuro) orquestração Durable Functions de coleta e avaliação.
- Specs: definição e provedor de checklist.
- Engine: roteamento e executores individuais de avaliação.
- Models: contratos de request/result/snapshot.

## Próximos Passos

1. Decidir se Durable Functions será utilizado (adicionar pacote e atributos).
2. Implementar carregamento real do checklist (Storage, Git, etc.).
3. Implementar lógica dos executores (ARM: Azure Resource Graph/SDK, Graph: Microsoft Graph, Cost: Cost Management API).
4. Adicionar testes unitários (xUnit) e configurar pipeline CI.
5. Adicionar `local.settings.json` (não commitado) com `AzureWebJobsStorage` e outras variáveis.
6. Integrar mecanismos de secrets (User Secrets / Azure Key Vault).

## Execução Local

Após adicionar `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

Executar:

```bash
func start
```

(Requer Azure Functions Core Tools e `dotnet build`).

## Licença

Definir licença apropriada.
