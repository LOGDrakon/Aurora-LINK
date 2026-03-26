# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [Aurora-LINK\Aurora-LINK (Package)\Aurora-LINK (Package).wapproj](#aurora-linkaurora-link-(package)aurora-link-(package)wapproj)
  - [Aurora-LINK\Aurora-LINK\Aurora-LINK.csproj](#aurora-linkaurora-linkaurora-linkcsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 2 | 1 require upgrade |
| Total NuGet Packages | 5 | All compatible |
| Total Code Files | 22 |  |
| Total Code Files with Incidents | 1 |  |
| Total Lines of Code | 3358 |  |
| Total Number of Issues | 1 |  |
| Estimated LOC to modify | 0+ | at least 0,0% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| [Aurora-LINK\Aurora-LINK (Package)\Aurora-LINK (Package).wapproj](#aurora-linkaurora-link-(package)aurora-link-(package)wapproj) | net451 | 🟢 Low | 0 | 0 |  | WinUI, Sdk Style = True |
| [Aurora-LINK\Aurora-LINK\Aurora-LINK.csproj](#aurora-linkaurora-linkaurora-linkcsproj) | net10.0-windows10.0.19041.0 | ✅ None | 0 | 0 |  | WinForms, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 5 | 100,0% |
| ⚠️ Incompatible | 0 | 0,0% |
| 🔄 Upgrade Recommended | 0 | 0,0% |
| ***Total NuGet Packages*** | ***5*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 0 |  |
| ***Total APIs Analyzed*** | ***0*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| LINK.Client | 2.0.1 |  | [Aurora-LINK.csproj](#aurora-linkaurora-linkaurora-linkcsproj) | ✅Compatible |
| LINK.Core | 2.0.1 |  | [Aurora-LINK.csproj](#aurora-linkaurora-linkaurora-linkcsproj) | ✅Compatible |
| LINK.Transport.Serial | 2.0.1 |  | [Aurora-LINK.csproj](#aurora-linkaurora-linkaurora-linkcsproj) | ✅Compatible |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.7705 |  | [Aurora-LINK (Package).wapproj](#aurora-linkaurora-link-(package)aurora-link-(package)wapproj)<br/>[Aurora-LINK.csproj](#aurora-linkaurora-linkaurora-linkcsproj) | ✅Compatible |
| Microsoft.WindowsAppSDK | 1.8.260209005 |  | [Aurora-LINK (Package).wapproj](#aurora-linkaurora-link-(package)aurora-link-(package)wapproj)<br/>[Aurora-LINK.csproj](#aurora-linkaurora-linkaurora-linkcsproj) | ✅Compatible |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;Aurora-LINK (Package).wapproj</b><br/><small>net451</small>"]
    P2["<b>📦&nbsp;Aurora-LINK.csproj</b><br/><small>net10.0-windows10.0.19041.0</small>"]
    P1 --> P2
    click P1 "#aurora-linkaurora-link-(package)aurora-link-(package)wapproj"
    click P2 "#aurora-linkaurora-linkaurora-linkcsproj"

```

## Project Details

<a id="aurora-linkaurora-link-(package)aurora-link-(package)wapproj"></a>
### Aurora-LINK\Aurora-LINK (Package)\Aurora-LINK (Package).wapproj

#### Project Info

- **Current Target Framework:** net451
- **Proposed Target Framework:** net10.0-windows10.0.26100.0
- **SDK-style**: True
- **Project Kind:** WinUI
- **Dependencies**: 1
- **Dependants**: 0
- **Number of Files**: 51
- **Number of Files with Incidents**: 1
- **Lines of Code**: 0
- **Estimated LOC to modify**: 0+ (at least 0,0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["Aurora-LINK (Package).wapproj"]
        MAIN["<b>📦&nbsp;Aurora-LINK (Package).wapproj</b><br/><small>net451</small>"]
        click MAIN "#aurora-linkaurora-link-(package)aurora-link-(package)wapproj"
    end
    subgraph downstream["Dependencies (1"]
        P2["<b>📦&nbsp;Aurora-LINK.csproj</b><br/><small>net10.0-windows10.0.19041.0</small>"]
        click P2 "#aurora-linkaurora-linkaurora-linkcsproj"
    end
    MAIN --> P2

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 0 |  |
| ***Total APIs Analyzed*** | ***0*** |  |

<a id="aurora-linkaurora-linkaurora-linkcsproj"></a>
### Aurora-LINK\Aurora-LINK\Aurora-LINK.csproj

#### Project Info

- **Current Target Framework:** net10.0-windows10.0.19041.0✅
- **SDK-style**: True
- **Project Kind:** WinForms
- **Dependencies**: 0
- **Dependants**: 1
- **Number of Files**: 24
- **Lines of Code**: 3358
- **Estimated LOC to modify**: 0+ (at least 0,0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph upstream["Dependants (1)"]
        P1["<b>📦&nbsp;Aurora-LINK (Package).wapproj</b><br/><small>net451</small>"]
        click P1 "#aurora-linkaurora-link-(package)aurora-link-(package)wapproj"
    end
    subgraph current["Aurora-LINK.csproj"]
        MAIN["<b>📦&nbsp;Aurora-LINK.csproj</b><br/><small>net10.0-windows10.0.19041.0</small>"]
        click MAIN "#aurora-linkaurora-linkaurora-linkcsproj"
    end
    P1 --> MAIN

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 0 |  |
| ***Total APIs Analyzed*** | ***0*** |  |

