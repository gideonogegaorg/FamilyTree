# Family Tree Documentation

This directory contains comprehensive documentation for the Family Tree application, organized for both production features and testing approaches.

---

## 🔗 Development Setup

**For local development setup, see the [root README.md](../README.md)** which includes:**
- **appsettings.json** configuration (git ignored template system)
- **Build and run** instructions
- **Environment variables** and secrets

---

## 🚀 New User Guide

### Quick Start for Development

**For complete development setup:**

1. **🔧 Development Environment**: See [root README.md](../README.md#local-setup) for appsettings.json, build, and run instructions
2. **📖 Understand the Core Feature**: Start with [`tree-layout-orientation.md`](tree-layout-orientation.md) - learn about visual ranks, orientations, and lineage modes
3. **🗄️ Database Setup**: Use [`database-setup.md`](database-setup.md) - configure PostgreSQL and seed test data
4. **🧪 Testing Environment**: Follow [`testing-environment.md`](testing-environment.md) - set up test accounts and validation
5. **🔧 Run Tests**: Use [`ui-testing-approach.md`](ui-testing-approach.md) - understand testing strategy and run tests

### Critical Prerequisites

- **PostgreSQL**: Must be installed and running
- **.NET 10.0+**: Required for development
- **Seed Data**: Essential for UI validation (16-person family tree)
- **Test Account**: Required for UI testing (create via registration)

### Common Setup Issues

| Problem | Solution |
|---|---|
| **appsettings.json not found** | File is git ignored for security - check local configuration |
| **Database connection fails** | Verify PostgreSQL is running and check connection details |
| **Only see "Me" in family tree** | Seed data not loaded - run seed script from [`testing-environment.md`](testing-environment.md#run-seed-script) |
| **Tests fail with alignment errors** | Need complete family tree data - see UI testing requirements |

---

## Documentation Structure

### 📋 Production Documentation
**Focus**: Features, behavior, and implementation details for production functionality

| File | Purpose | Key Topics |
|---|---|---|
| [`tree-layout-orientation.md`](tree-layout-orientation.md) | **Core Feature Documentation** | Layout orientation, visual ranking, lineage modes, CSS/JS implementation |
| [`database-setup.md`](database-setup.md) | **Database Configuration** | PostgreSQL setup, seeding, migrations, MCP server integration |
| [`configure-service.md`](configure-service.md) | **Service Configuration** | Service setup, configuration options |
| [`coverage-pending.md`](coverage-pending.md) | **Test Coverage Planning** | Pending test coverage areas and planning |

### 🧪 Testing Documentation  
**Focus**: Testing strategy, validation approaches, and test implementation

| File | Purpose | Key Topics |
|---|---|---|
| [`ui-testing-approach.md`](ui-testing-approach.md) | **UI Testing Strategy** | Visual rank validation, relative positioning, test implementation, regression prevention |
| [`testing-environment.md`](testing-environment.md) | **Test Environment Setup** | Database seeding, test accounts, MCP setup, automation scripts |
| [`coverage-report-topic-NewUser.md`](coverage-report-topic-NewUser.md) | **Feature Test Coverage** | New user journey testing, coverage reporting |

---

## � Documentation Philosophy

#### Production vs Testing Separation

- **Production Docs**: Focus on **what** the system does and **how** it works
- **Testing Docs**: Focus on **how to validate** that the system works correctly

#### Audience Targeting

| Document | Primary Audience | Secondary Audience |
|---|---|---|
| Production docs | Developers implementing features | QA understanding expected behavior |
| Testing docs | QA engineers writing tests | Developers understanding test validation |

---

## Key Concepts Overview

### Visual Rank System
The family tree uses a **visual rank system** for node positioning:

- **Integer ranks** (0, 1, 2): Primary positioning levels
- **Half-ranks** (0.5, 1.5): Secondary partners and special positioning
- **Lineage modes**: Paternal (male primary) vs Maternal (female primary)

### Layout Orientation
Two display orientations with **90° rotation principle**:

- **Vertical**: Top-to-bottom flow, rows aligned by Y coordinate
- **Horizontal**: Left-to-right flow, columns aligned by X coordinate

### Testing Strategy
**Relative positioning validation** with browser tolerance:

- **Visual rank grouping**: Nodes grouped by `data-visual-rank` attributes
- **Tolerance-based alignment**: 50px tolerance for browser rendering differences
- **Behavior validation**: Tests validate what users see, not implementation details

---

## Contributing to Documentation

### Adding New Documentation

1. **Choose appropriate category**: Production vs Testing
2. **Follow naming convention**: `kebab-case.md`
3. **Update this README**: Add new file to the appropriate table
4. **Cross-reference**: Link between related documents

### Documentation Standards

- **Clear structure**: Use headings, tables, and code blocks
- **Practical examples**: Include real code snippets and configurations
- **Cross-references**: Link to related documentation
- **Maintenance**: Keep docs in sync with code changes

---

## File Index

### Complete File List

```
docs/
├── README.md                           # This file - documentation overview
├── tree-layout-orientation.md          # Core feature documentation
├── database-setup.md                   # NEW - Database configuration and seeding
├── ui-testing-approach.md              # UI testing strategy and implementation
├── testing-environment.md              # NEW - Test environment setup and automation
├── configure-service.md                # Service configuration guide
├── coverage-pending.md                 # Test coverage planning
└── coverage-report-topic-NewUser.md    # New user test coverage
```

---

## Quick Reference

### Most Important Files

1. **📖 [`tree-layout-orientation.md`](tree-layout-orientation.md)** - *Must read* for understanding the core feature
2. **🗄️ [`database-setup.md`](database-setup.md)** - *Essential* for database configuration and seeding
3. **🧪 [`ui-testing-approach.md`](ui-testing-approach.md)** - *Essential* for understanding test validation
4. **⚙️ [`testing-environment.md`](testing-environment.md)** - *Important* for test environment setup
5. **⚙️ [`configure-service.md`](configure-service.md)** - *Reference* for service setup

### Common Questions

| Question | Answer |
|---|---|
| **How does the layout work?** | See [`tree-layout-orientation.md`](tree-layout-orientation.md#column--row-alignment-by-rank) |
| **How do I set up the database?** | See [`database-setup.md`](database-setup.md#database-setup) |
| **How do I configure the test environment?** | See [`testing-environment.md`](testing-environment.md#database-setup-for-testing) |
| **How are tests structured?** | See [`ui-testing-approach.md`](ui-testing-approach.md#testing-architecture) |
| **What are visual ranks?** | See [`tree-layout-orientation.md`](tree-layout-orientation.md#tree-layout-ranking-system) |
| **How do tests validate positioning?** | See [`ui-testing-approach.md`](ui-testing-approach.md#visual-rank-testing-strategy) |
| **What's the difference between orientations?** | See [`tree-layout-orientation.md`](tree-layout-orientation.md#overview) |
| **How does the MCP server work?** | See [`database-setup.md`](database-setup.md#postgresql-mcp-integration) |

---

*This documentation is maintained alongside the codebase. Please keep it updated when making changes to features or testing approaches.*
