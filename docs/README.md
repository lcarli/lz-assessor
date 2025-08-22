# LZ Assessor Documentation Index

Welcome to the comprehensive documentation for LZ Assessor - an automated Landing Zone assessment tool for Azure environments.

## 📚 Documentation Structure

### Core Documentation

#### 🏗️ [Architecture Documentation](ARCHITECTURE.md)
Complete system architecture with visual diagrams and detailed component explanations:
- High-level architecture diagram with Mermaid
- System components and responsibilities
- Data flow architecture
- Security architecture
- Scalability and performance considerations
- Extensibility points

#### ⚙️ [Detailed Workflow](WORKFLOW.md)
Step-by-step explanation of assessment execution from request to results:
- Complete assessment workflow (8 phases)
- Request initiation and orchestrator activation
- Environment discovery process
- Specification loading and validation
- Check execution planning and coordination
- Parallel execution and result aggregation
- Error handling and recovery strategies
- Performance optimization techniques

#### 📝 [Specification Creation Guide](SPEC_CREATION.md)
Comprehensive guide for creating new assessment specifications and adding questions:
- Specification structure and anatomy
- Step-by-step spec creation process
- Declarative logic definition with examples
- Evidence template configuration
- Creating custom executors
- Testing and validation procedures
- **Important**: AI limitations for spec creation

#### 🗺️ [Project Roadmap](ROADMAP.md)
Development roadmap with completed milestones, planned features, and important limitations:
- Completed milestones and current status
- Upcoming development phases
- **Critical limitation**: AI-generated specifications cannot be created
- Technical constraints and dependencies
- Resource requirements and success metrics

### Setup and Deployment

#### 🚀 [Deployment Guide](DEPLOYMENT.md)
Complete deployment instructions including infrastructure setup, permissions, and monitoring:
- Prerequisites and infrastructure setup
- Function App deployment and configuration
- Permission configuration for Azure APIs
- Testing and monitoring setup
- Troubleshooting common issues

#### 🍎 [macOS ARM64 Setup](MACOS_ARM64_SETUP.md)
Troubleshooting guide for Apple Silicon development environments:
- Architecture compatibility issues
- Docker and container setup
- Local development configuration

## 📖 Quick Navigation

### For New Users
1. Start with [Architecture Documentation](ARCHITECTURE.md) to understand the system
2. Review [Workflow Documentation](WORKFLOW.md) to understand how assessments work
3. Follow [Deployment Guide](DEPLOYMENT.md) to set up your environment

### For Developers
1. Read [Architecture Documentation](ARCHITECTURE.md) for system understanding
2. Review [Specification Creation Guide](SPEC_CREATION.md) for adding new checks
3. Check [Roadmap](ROADMAP.md) for development priorities and constraints

### For Contributors
1. Review [Specification Creation Guide](SPEC_CREATION.md) for contribution guidelines
2. Understand [Workflow Documentation](WORKFLOW.md) for system behavior
3. Check [Roadmap](ROADMAP.md) for planned features and limitations

## 🚨 Critical Information

### AI Limitations
**Important**: Specifications cannot be automatically generated using AI tools. Each specification must be:
- Created by human experts with domain knowledge
- Validated against real Azure environments
- Reviewed for security and compliance accuracy
- Maintained as Azure services evolve

See [Roadmap - AI Limitations](ROADMAP.md#-ai-generated-specification-limitation) for detailed explanation.

### System Requirements
- .NET 8 SDK
- Azure subscription with appropriate permissions
- Azure Functions Core Tools v4
- Access to Microsoft Graph and Azure Resource Manager APIs

### Support and Resources
- **GitHub Issues**: Report bugs and feature requests
- **Documentation Updates**: Submit PRs for documentation improvements
- **Security Issues**: Follow responsible disclosure process

## 📄 Document Maintenance

### Last Updated
- Architecture Documentation: 2024-08-21
- Workflow Documentation: 2024-08-21  
- Specification Creation Guide: 2024-08-21
- Project Roadmap: 2024-08-21
- Deployment Guide: [See file for date]
- macOS ARM64 Setup: [See file for date]

### Review Schedule
- **Quarterly**: Technical accuracy and API updates
- **Semi-Annual**: Architecture and design reviews
- **Annual**: Complete documentation overhaul

### Contributing to Documentation
1. Follow markdown best practices
2. Include code examples where appropriate
3. Update this index when adding new documents
4. Ensure cross-references are accurate
5. Test all links and examples before submitting

---

*This documentation provides comprehensive coverage of LZ Assessor capabilities, limitations, and usage patterns. For additional questions or clarifications, please refer to the specific documentation sections or create an issue in the repository.*