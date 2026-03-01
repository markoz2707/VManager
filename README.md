# VManager - Multi-Hypervisor Virtual Infrastructure Management Platform

VManager is a centralized virtual infrastructure management platform, similar in concept to VMware vCenter, designed to manage **Hyper-V** (Windows) and **KVM/libvirt** (Linux) hypervisors from a single pane of glass. It provides a REST API agent deployed on each hypervisor host, a central management server with PostgreSQL backend, a React-based web dashboard, and a local CLI tool for Windows Server Core environments.

## Architecture Overview

```
                        +---------------------------+
                        |     VManager.CentralUI    |
                        |   (React 19 Dashboard)    |
                        +------------+--------------+
                                     |
                                     | REST API + SignalR WebSocket
                                     v
                        +---------------------------+
                        | HyperV.CentralManagement  |
                        |  (Central API Server)     |
                        |  - RBAC / JWT Auth        |
                        |  - VM Inventory Sync      |
                        |  - Migration Orchestrator |
                        |  - HA Engine / DRS Engine |
                        |  - Alerting System        |
                        |  - Metrics Storage        |
                        +------+----------+---------+
                               |          |
                        +------+--+  +----+------+
                        | Agent A |  | Agent B   |  ... (N agents)
                        | Hyper-V |  | KVM       |
                        +---------+  +-----------+
                        Windows Host  Linux Host
```

**Data flow:** The Central Management server periodically polls each registered Agent for VM state and metrics. Agents expose a REST API backed by native hypervisor APIs (WMI/HCS on Windows, libvirt on Linux). The React UI connects to the Central Management server via REST and receives real-time updates through SignalR.

---

## Projects

The solution contains **12 .NET projects**, **1 React project**, and **3 test projects**.

### Core Layer

#### HyperV.Contracts
| | |
|---|---|
| **Purpose** | Shared interfaces, DTOs, and model definitions used across all projects |
| **Target** | `net9.0` (platform-neutral) |
| **Key contents** | Provider interfaces (`IVmProvider`, `IHostProvider`, `INetworkProvider`, `IStorageProvider`, `IMetricsProvider`, `IMigrationProvider`), service interfaces (`IHostInfoService`, `IStorageService`, `IImageManagementService`, `IJobService`), and platform-neutral DTOs (`VmSummaryDto`, `HostInfoDto`, `CreateVmSpec`, etc.) |

This is the **abstraction layer** that enables multi-hypervisor support. Every hypervisor provider implements these interfaces, and the Agent controllers depend only on these abstractions, not on concrete implementations.

Each DTO includes an optional `Dictionary<string, object>? ExtendedProperties` field for hypervisor-specific data that doesn't fit the common model.

#### HyperV.Core.Wmi
| | |
|---|---|
| **Purpose** | Windows Management Instrumentation (WMI) wrapper for Hyper-V management via the `root\virtualization\v2` namespace |
| **Target** | `net9.0-windows7.0` |
| **Dependencies** | `System.Management` 9.0.8 |
| **Key services** | `VmService` (VM lifecycle, snapshots, migration, configuration), `NetworkService` / `WmiNetworkService` (virtual switch management), `MetricsService` (WMI metric collection/enablement), `ResourcePoolsService`, `ReplicationService`, `FibreChannelService`, `VmCreationService` |

This is the **primary Hyper-V management layer**. `VmService` methods return JSON strings that callers parse with `System.Text.Json`. This design originated from WMI queries returning dynamic schema results.

#### HyperV.Core.Hcs
| | |
|---|---|
| **Purpose** | Hyper-V Host Compute Service (HCS) API integration for modern container and lightweight VM operations |
| **Target** | `net9.0-windows7.0` |
| **Key services** | `VmService` (HCS-based VM operations), `ContainerService` (Windows container lifecycle) |

HCS provides a newer, JSON-based API for Hyper-V operations, complementing the legacy WMI approach. Used for container workloads and scenarios requiring the modern compute API.

#### HyperV.Core.Hcn
| | |
|---|---|
| **Purpose** | Host Compute Network (HCN) API for Hyper-V network management |
| **Target** | `net9.0-windows7.0` |
| **Key contents** | P/Invoke wrappers around `computenetwork.dll` for creating, querying, and managing virtual networks and endpoints |

#### HyperV.Core.Vhd
| | |
|---|---|
| **Purpose** | Virtual Hard Disk (VHD/VHDX) operations |
| **Target** | `net9.0-windows7.0` |
| **Key contents** | Disk creation, compaction, conversion, resizing, merging, and snapshot management using native Windows APIs |

---

### Agent Layer

#### HyperV.Agent
| | |
|---|---|
| **Purpose** | REST API server deployed on each hypervisor host, exposing VM management operations |
| **Target** | `net9.0-windows8.0` |
| **Default port** | `8743` (HTTPS) |
| **Key dependencies** | `Serilog`, `FluentValidation`, `Swashbuckle` (Swagger), `prometheus-net`, JWT auth |

**Controllers (10):**

| Controller | Route | Description |
|---|---|---|
| `ServiceController` | `/api/v1/service` | Health check and agent info |
| `VmsController` | `/api/v1/vms` | Full VM lifecycle: list, create, delete, start, stop, pause, resume, restart, configure CPU/memory, snapshots, migration |
| `HostController` | `/api/v1/host` | Host hardware info and system details |
| `NetworksController` | `/api/v1/networks` | Virtual switch management (HCN + WMI), Fibre Channel |
| `StorageController` | `/api/v1/storage` | Storage devices, VHD locations, disk operations |
| `ContainersController` | `/api/v1/containers` | Container creation (HCS lightweight + Hyper-V isolated) |
| `JobsController` | `/api/v1/jobs` | Long-running storage job tracking |
| `ReplicationController` | `/api/v1/replication` | VM replication relationships |
| `ImageManagementController` | `/api/v1/images` | VHD/VHDX file operations (compact, merge, convert, resize) |
| `StorageQoSController` | `/api/v1/storage/qos` | Storage Quality-of-Service policies |

**Background services:**
- `PrometheusMetricsCollector` - Collects host CPU/memory and VM counts every 15 seconds, exposes at `/metrics`

**Endpoints:**
- `/swagger` - OpenAPI documentation
- `/metrics` - Prometheus metrics
- `/api/v1/health` - Health check

#### VManager.Provider.HyperV
| | |
|---|---|
| **Purpose** | Adapter layer implementing provider interfaces (`IVmProvider`, `IHostProvider`, `IMetricsProvider`, `IMigrationProvider`) using WMI/HCS services |
| **Target** | `net9.0-windows8.0` |
| **Key files** | `HyperVVmProvider.cs`, `HyperVHostProvider.cs`, `HyperVMetricsProvider.cs`, `HyperVMigrationProvider.cs`, `HyperVServiceRegistration.cs` |

This project bridges the gap between the platform-neutral `IVmProvider` interface and the Windows-specific WMI layer. It parses JSON strings returned by `VmService` into typed DTOs and handles API differences (e.g., `VmService.TerminateVm` for force-stop vs `VmService.StopVm` for graceful shutdown).

Registered via `services.AddHyperVProvider()` extension method.

#### VManager.Libvirt
| | |
|---|---|
| **Purpose** | P/Invoke bindings for the libvirt C library (`libvirt.so`), enabling KVM/QEMU management on Linux |
| **Target** | `net9.0` (cross-platform) |
| **Key files** | `Native/LibvirtNative.cs` (P/Invoke declarations), `Connection/LibvirtConnection.cs` (managed wrapper with IDisposable) |
| **Note** | `AllowUnsafeBlocks` enabled for native interop |

Wraps libvirt functions: `virConnectOpen`, `virDomainDefineXML`, `virDomainCreate`, `virDomainShutdown`, `virNodeGetInfo`, `virStoragePoolLookupByName`, `virNetworkDefineXML`, etc.

#### VManager.Provider.KVM
| | |
|---|---|
| **Purpose** | KVM/QEMU provider implementing the same `IVmProvider`, `IHostProvider`, `IMetricsProvider`, `IMigrationProvider` interfaces using libvirt |
| **Target** | `net9.0` (Linux) |
| **Key files** | `KvmVmProvider.cs`, `KvmHostProvider.cs`, `KvmMetricsProvider.cs`, `KvmMigrationProvider.cs`, `KvmOptions.cs`, `KvmServiceRegistration.cs` |

Maps libvirt domain states to the common model (e.g., `VIR_DOMAIN_RUNNING` -> `"Running"`, `VIR_DOMAIN_PAUSED` -> `"Paused"`).

Configured via `KvmOptions` (LibvirtUri, default: `qemu:///system`). Registered via `services.AddKvmProvider()`.

---

### Central Management Layer

#### HyperV.CentralManagement
| | |
|---|---|
| **Purpose** | Central management server that aggregates data from all agents, provides RBAC, orchestration, alerting, HA, DRS, and serves the React UI |
| **Target** | `net9.0` (cross-platform) |
| **Database** | PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` |
| **Key dependencies** | `EF Core 9`, `JWT Bearer Auth`, `MailKit` (email notifications), `prometheus-net`, `System.DirectoryServices.Protocols` (LDAP) |

**Controllers (12):**

| Controller | Route | Auth | Description |
|---|---|---|---|
| `AuthController` | `/api/auth` | Public | JWT login (local + LDAP), token generation |
| `AgentsController` | `/api/agents` | `host:read/create/update` | Agent registration, status, list |
| `VmInventoryController` | `/api/v1/vms` | `vm:read/update/power/create/delete/migrate` | Cross-host VM inventory, power ops, folders, search, migration |
| `ClustersController` | `/api/clusters` | `cluster:read/create/update/delete` | Cluster management, node membership |
| `UsersController` | `/api/users` | `user:read/create/update/delete` | User CRUD with role assignment |
| `RolesController` | `/api/roles` | `user:read/create/update/delete` | Role and permission management |
| `AlertsController` | `/api/alerts` | `host:read/create/update` | Alert definitions, active/resolved instances |
| `MetricsController` | `/api/metrics` | `host:read` | Time-series metrics queries for hosts and VMs |
| `HaController` | `/api/ha` | `cluster:read/update` | HA configuration and events |
| `DrsController` | `/api/drs` | `cluster:read/update` | DRS configuration and recommendations |
| `AuditController` | `/api/audit` | `audit:read` | Audit log viewer |
| `NotificationChannelsController` | `/api/notification-channels` | `host:read/create/update/delete` | Email/Webhook/Slack/Teams channels |

**Background services (6):**

| Service | Interval | Description |
|---|---|---|
| `VmSyncBackgroundService` | 30s | Polls agents for VM state, updates inventory, broadcasts changes via SignalR |
| `AlertEvaluationService` | 30s | Evaluates alert definitions against current metrics, fires alerts, sends notifications |
| `MetricsCollectionService` | 60s | Collects host and VM metrics from agents, stores in PostgreSQL |
| `MetricsRetentionService` | 6h | Purges metric data older than 30 days |
| `HaEngine` | 10s | Monitors agent heartbeats, detects host failures, restarts VMs on surviving hosts |
| `DrsEngine` | 60s | Evaluates cluster resource balance, generates migration recommendations |

**RBAC system:**
- Attribute-based: `[RequirePermission("vm", "power")]`
- 7 resource types: `vm`, `host`, `cluster`, `network`, `storage`, `user`, `audit`
- 7 action types: `read`, `create`, `update`, `delete`, `power`, `migrate`, `snapshot`
- 4 built-in roles: `Administrator` (all), `Operator` (read + power/migrate/snapshot), `ReadOnly` (read all), `VmAdmin` (full VM + read infra)
- Scoped roles: roles can be restricted to specific clusters or agents

**SignalR hub** (`/hubs/vmanager`):
- Groups: `cluster:{id}`, `agent:{id}`, `alerts`, `migrations`
- Events: `VmStateChanged`, `AgentStatusChanged`, `AlertFired`, `AlertResolved`, `MigrationProgress`, `MetricsUpdate`

**Database models (17 entities):**
- Infrastructure: `AgentHost`, `Cluster`, `ClusterNode`, `RegistrationToken`
- Inventory: `VmInventory`, `VmFolder`
- RBAC: `UserAccount`, `Role`, `Permission`, `RolePermission`, `UserRole`
- Operations: `MigrationTask`, `AuditLog`
- Alerting: `AlertDefinition`, `AlertInstance`, `NotificationChannel`, `AlertNotificationChannel`
- Metrics: `MetricDataPoint`
- HA: `HaConfiguration`, `HaVmOverride`, `HaEvent`
- DRS: `DrsConfiguration`, `DrsRecommendation`

---

### Frontend

#### VManager.CentralUI
| | |
|---|---|
| **Purpose** | React-based web dashboard for centralized management |
| **Stack** | React 19, Vite 7, TypeScript 5.9, TailwindCSS 4 |
| **Dev server** | `http://localhost:3000` (proxies `/api` and `/hubs` to backend) |

**Key packages:**
- `@microsoft/signalr` - Real-time WebSocket events
- `@tanstack/react-query` - Server state management with caching
- `@tanstack/react-table` - Data tables with sorting, filtering, pagination
- `recharts` - Time-series charts for monitoring
- `lucide-react` - Icons
- `motion` - Animations
- `date-fns` - Date formatting

**Pages (14):**

| Page | Description |
|---|---|
| `LoginPage` | JWT authentication form |
| `DashboardPage` | Overview: VM counts, host health, active alerts, resource gauges |
| `AgentsPage` | Agent list with status badges and hypervisor type (Hyper-V/KVM) |
| `AgentDetailsPage` | Agent detail: VM list, CPU/memory gauges, performance charts |
| `VirtualMachinesPage` | Cross-host VM table with power operations (start/stop/shutdown/pause/resume/restart) |
| `ClustersPage` | Cluster CRUD, node membership management |
| `MigrationPage` | Migration tasks with progress bars and history |
| `AlertsPage` | Active/history/definitions tabs, acknowledge/resolve actions |
| `MonitoringPage` | Time-series area charts for CPU/memory metrics with agent selector |
| `HaPage` | HA configuration, enable/disable, event history |
| `DrsPage` | DRS configuration, recommendations with apply/reject |
| `UsersPage` | User management with role assignment |
| `RolesPage` | Permission matrix (resources x actions x roles) |
| `AuditPage` | Audit log table |

**Architecture:**
- `useAuth` hook - JWT parsing, login/logout, permission checking
- `useSignalR` hook - SignalR connection with auto-reconnect, group subscriptions
- `ProtectedRoute` component - Route guards based on JWT permissions
- `ApiClient` class - Centralized HTTP client with automatic Bearer token injection and 401 redirect
- React Query integration - SignalR events invalidate query caches for real-time updates

---

### CLI

#### HyperV.LocalShell (hvsh)
| | |
|---|---|
| **Purpose** | Terminal-based interactive shell for local Hyper-V management on Windows Server Core (no GUI) |
| **Target** | `net9.0-windows7.0` |
| **Assembly** | `hvsh.exe` |
| **Key dependencies** | `Spectre.Console` (rich terminal UI), `Spectre.Console.Cli` (command framework) |

Provides a local CLI alternative when a web browser is not available (e.g., Server Core installations).

---

### Test Projects

| Project | Target | Tests |
|---|---|---|
| `HyperV.Agent.Tests` | `net9.0-windows8.0` | Agent controller and integration tests |
| `HyperV.Core.Wmi.Tests` | `net9.0-windows7.0` | WMI service unit tests |
| `HyperV.Core.Hcs.Tests` | `net9.0-windows7.0` | HCS service unit tests |

All use **xUnit** with `coverlet.collector` for code coverage.

---

## Deployment

### Docker Compose (Central Management)

```bash
# Start PostgreSQL + Central Management
docker compose up -d

# Include PgAdmin for database management
docker compose --profile tools up -d
```

Services:
- **postgres** (port 5432) - PostgreSQL 16 database
- **central-management** (port 8080/8443) - Central Management API + React UI
- **pgadmin** (port 5050, optional) - Database admin interface

Environment variables (`.env` file):
```env
DB_PASSWORD=your_secure_password
JWT_SECRET=your_jwt_secret_min_32_chars
ADMIN_PASSWORD=initial_admin_password
LDAP_ENABLED=false
```

### Agent Deployment

**Windows (Hyper-V):**
```bash
dotnet publish src/HyperV.Agent -c Release -r win-x64 --self-contained
# Deploy to target host, run as Windows Service or standalone
```

**Linux (KVM):**
```bash
dotnet publish src/HyperV.Agent -c Release -r linux-x64 --self-contained
# Copy to /opt/vmanager-agent/ on target host
sudo cp deploy/vmanager-agent.service /etc/systemd/system/
sudo systemctl enable --now vmanager-agent
```

The agent auto-detects the platform and loads the appropriate provider:
- Windows -> `AddHyperVProvider()`
- Linux -> `AddKvmProvider()`

### React UI Development

```bash
cd src/VManager.CentralUI
npm install
npm run dev    # http://localhost:3000 with proxy to backend
npm run build  # Production build to dist/
```

The production build is served by the Central Management server as static files with SPA fallback.

---

## Build

```bash
# Restore and build entire solution
dotnet restore HyperV.sln
dotnet build HyperV.sln

# Build specific projects
dotnet build src/HyperV.Agent/HyperV.Agent.csproj
dotnet build src/HyperV.CentralManagement/HyperV.CentralManagement.csproj

# Run tests
dotnet test src/HyperV.Agent.Tests/
dotnet test src/HyperV.Core.Wmi.Tests/
dotnet test src/HyperV.Core.Hcs.Tests/
```

> **Note:** `HyperV.LocalManagement` is a legacy ASP.NET Framework web project and requires `msbuild` (not `dotnet build`). It is not part of the active development workflow.

---

## API Documentation

- **Agent Swagger:** `https://<agent-host>:8743/swagger`
- **Central Management Swagger:** `https://<central-host>:5101/swagger`
- **Prometheus Metrics (Agent):** `https://<agent-host>:8743/metrics`
- **Prometheus Metrics (Central):** `https://<central-host>:5101/metrics`
- **SignalR Hub:** `wss://<central-host>:5101/hubs/vmanager`

---

## Project Dependency Graph

```
HyperV.Contracts (net9.0)             <-- shared interfaces & DTOs
  ^          ^          ^
  |          |          |
  |  HyperV.Core.Wmi   HyperV.Core.Hcs   HyperV.Core.Vhd   HyperV.Core.Hcn
  |  (net9.0-win7.0)   (net9.0-win7.0)   (net9.0-win7.0)   (net9.0-win7.0)
  |          ^          ^                  ^                   ^
  |          |          |                  |                   |
  |          +---+------+------+-----------+-------------------+
  |              |
  |   VManager.Provider.HyperV (net9.0-win8.0)
  |              ^
  |              |
  +---> HyperV.Agent (net9.0-win8.0) <--- Web API server
  |
  |   VManager.Libvirt (net9.0)       <-- P/Invoke libvirt bindings
  |              ^
  |              |
  |   VManager.Provider.KVM (net9.0)
  |              ^
  |              |
  +---> [Future: cross-platform Agent using KVM provider on Linux]
  |
  +---> HyperV.CentralManagement (net9.0)  <--- Central server + PostgreSQL
  |
  +---> HyperV.LocalShell (net9.0-win7.0)  <--- CLI (hvsh.exe)
```

---

## External Documentation

Microsoft Hyper-V WMI API reference:
https://github.com/MicrosoftDocs/win32/tree/docs/desktop-src/HyperV_v2

Libvirt API reference:
https://libvirt.org/html/index.html
