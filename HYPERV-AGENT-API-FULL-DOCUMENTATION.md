# Hyper-V Agent API Documentation

## 1. Overview of the API

The Hyper-V Agent is a RESTful API service built on ASP.NET Core, designed to manage Hyper-V environments on Windows hosts. It provides a unified interface for virtual machine (VM) lifecycle operations, networking, storage, and metrics collection. The API supports dual backends:

- **WMI V2 (Windows Management Instrumentation)**: Leverages CIM (Common Information Model) classes like `Msvm_VirtualSystemManagementService`, `Msvm_ComputerSystem`, and `Msvm_ResourceAllocationSettingData` for traditional Hyper-V management. This backend is compatible with Hyper-V on Windows Server and uses asynchronous job polling for long-running operations.
- **HCS (Host Compute Service)**: Integrates with the Host Compute Service API (part of Windows Containers and Hyper-V isolation) for enhanced containerized VM management, storage, and networking. HCS calls are synchronous where possible and provide finer-grained control for modern workloads.

**Key Features**:
- **Base URL**: `/api/v1`
- **Authentication**: Bearer token (JWT) or Windows Authentication (configurable via `appsettings.json`).
- **Content-Type**: JSON for requests/responses.
- **Versioning**: Semantic versioning; current version is v1.0.
- **Error Handling**: Standardized HTTP status codes with JSON error bodies (see Section 5).
- **Async Operations**: WMI-based operations return job IDs for polling status via `/api/v1/jobs/{id}`.

The API abstracts backend differences, allowing clients to switch between WMI and HCS via a query parameter (`?backend=wmi` or `?backend=hcs`; defaults to WMI). This documentation synthesizes existing implementations from controllers (e.g., `VmsController.cs`, `NetworksController.cs`, `StorageController.cs`) with references to Hyper-V WMI samples (e.g., VM creation in Generation2VM) and v2 API descriptions (e.g., `Msvm_VirtualSystemManagementService.DefineSystem`).

## 2. Current APIs

APIs are categorized by functionality. Each endpoint includes HTTP method, path, description, request/response schemas (with JSON examples), and backend details with cross-references.

### VMs Category (VmsController.cs)

| Endpoint | Method | Description | Request Schema | Response Schema | Backend Details & Cross-References |
|----------|--------|-------------|----------------|-----------------|----------------------------|
| `/api/v1/vms` | POST | Create a new VM (supports HCS for containers or WMI for traditional VMs). | Body: `CreateVmRequest` { "id": "string", "name": "string", "mode": "HCS\|WMI", "memoryMB": int, "cpuCount": int, "diskSizeGB": int, "notes": "string" } | 200: { "id": "string", "status": "string", "vhdPath": "string" } | WMI: `Msvm_VirtualSystemManagementService.DefineSystem` (cross-ref: Hyper-V/ Generation2VM sample, v2 DefineSystem). HCS: `HcsCreateComputeSystem`. |
| `/api/v1/vms/{name}/present` | GET | Check if VM exists (searches HCS and WMI). | Path: `name` (string) | 200: { "present": bool, "hcs": bool, "wmi": bool } | WMI: Query `Msvm_ComputerSystem` by ElementName. HCS: Internal dict check. |
| `/api/v1/vms/{name}/start` | POST | Start the VM. | Path: `name` (string) | 200: OK | WMI: `Msvm_ComputerSystem.RequestStateChange(2)`. HCS: `HcsStartComputeSystem` (cross-ref: Hyper-V/ VmOperations sample). |
| `/api/v1/vms/{name}/stop` | POST | Graceful stop/shutdown VM. | Path: `name` (string) | 200: OK | WMI: `RequestStateChange(3)`. HCS: `HcsShutDownComputeSystem`. |
| `/api/v1/vms/{name}/terminate` | POST | Force terminate VM. | Path: `name` (string) | 200: OK | WMI: `RequestStateChange(32768)`. HCS: `HcsTerminateComputeSystem`. |
| `/api/v1/vms/{name}/pause` | POST | Pause VM. | Path: `name` (string) | 200: OK | WMI: `RequestStateChange(9)`. HCS: `HcsPauseComputeSystem`. |
| `/api/v1/vms/{name}/resume` | POST | Resume paused VM. | Path: `name` (string) | 200: OK | WMI: `RequestStateChange(2)`. HCS: `HcsResumeComputeSystem`. |
| `/api/v1/vms/{name}/save` | POST | Save VM state (WMI only). | Path: `name` (string) | 200: OK (501 for HCS) | WMI: `Msvm_VirtualSystemManagementService.RequestStateChange(6)` (cross-ref: Hyper-V/ VmOperations). |
| `/api/v1/vms/{name}/properties` | GET | Get VM properties (memory, CPU, state). | Path: `name` (string) | 200: { "backend": "string", "properties": { "memory": int, "processors": int, "state": "string" } } | WMI: Query `Msvm_MemorySettingData`/`Msvm_ProcessorSettingData`. HCS: `HcsGetComputeSystemProperties`. |
| `/api/v1/vms/{name}/modify` | POST | Modify VM configuration via JSON/XML. | Path: `name` (string), Body: `configuration` (string) | 200: OK | WMI: `Msvm_VirtualSystemManagementService.ModifySystemSettings` (cross-ref: v2 ModifySystemSettings). HCS: `HcsModifyComputeSystem`. |
| `/api/v1/vms` | GET | List all VMs. | None | 200: { "hcs": [VM[]], "wmi": [VM[]] } where VM = { "id": "string", "name": "string", "state": "string" } | WMI: Query `Msvm_ComputerSystem`. HCS: Internal list. |
| `/api/v1/vms/{name}/configure` | POST | Configure VM (memory, CPU, notes; WMI only). | Path: `name`, Body: `VmConfigurationRequest` { "startupMemoryMB": int?, "cpuCount": int? } | 200: OK | WMI: `ModifyResourceSettings` on `Msvm_MemorySettingData` (cross-ref: Hyper-V/ DynamicMemory sample). |
| `/api/v1/vms/{name}/snapshots` | GET/POST | List/create VM snapshots (WMI only). | GET: Path `name`. POST: Body `CreateSnapshotRequest` { "snapshotName": "string", "notes": "string?" } | GET: 200 [Snapshot[]]. POST: 200 { "snapshotId": "string" } | WMI: `Msvm_VirtualMachineSnapshotService.CreateSnapshot` (cross-ref: v2 ApplySnapshot). |
| `/api/v1/vms/{name}/snapshots/{snapshotId}` | DELETE/POST (revert) | Delete/revert snapshot (WMI only). | Path: `name`, `snapshotId` | 200: OK | WMI: `DestroySnapshot`/`ApplySnapshot`. |
| `/api/v1/vms/{name}/storage/devices` | GET/POST/DELETE | List/add/remove storage devices. | GET: Path `name`. POST: Body `AddStorageDeviceRequest` { "path": "string" } | GET: 200 [Device[]]. POST/DELETE: 200 OK | WMI: Query/Add/Remove `Msvm_ResourceAllocationSettingData` (ResourceType=19). HCS: `HcsModifyComputeSystem` with storage settings. |
| `/api/v1/vms/{name}/migrate` | POST | Migrate VM to another host. | Path: `name`, Body: `MigrateRequest` { "destinationHost": "string", "live": bool, "storage": bool } | 202: { "jobId": "string" } | WMI: `MigrateVirtualSystemToHost` (cross-ref: Hyper-V/ Migration sample, v2 MigrateVirtualSystemToHost). |
| `/api/v1/vms/{name}/apphealth` | GET | Get application health status of the VM. | Path: `name` | 200: `AppHealthResponse` { "status": "string", "appStatus": "string" } | WMI: Query `Msvm_HeartbeatComponent` (cross-ref: Hyper-V/ AppHealth sample). |
| `/api/v1/vms/{name}/guestfilecopy` | POST | Copy file to guest OS. | Path: `name`, Body: `GuestFileRequest` { "sourcePath": "string", "destPath": "string", "overwrite": bool? } | 200: { "jobId": "string" } | WMI: `Msvm_GuestFileService.CopyFilesToGuest` (cross-ref: v2 integration services). |

### Networks Category (NetworksController.cs, WmiNetworkService.cs)

| Endpoint | Method | Description | Request Schema | Response Schema | Backend Details & Cross-References |
|----------|--------|-------------|----------------|-----------------|----------------------------|
| `/api/v1/networks` | GET | List virtual switches. | None | 200: [SwitchInfo[]] { "id": "guid", "name": "string", "type": "External\|Internal\|Private" } | WMI: Query `Msvm_VirtualEthernetSwitch`. HCS: HCN list (cross-ref: Hyper-V/ Networking CreateSwitch). |
| `/api/v1/networks/nat` | POST | Create NAT switch. | Body: { "name": "string", "subnet": "string" } | 200: { "id": "guid" } | HCS: HCN NAT namespace. |
| `/api/v1/networks/wmi` | POST | Create WMI switch (internal/private/external). | Body: { "name": "string", "type": "enum", "notes": "string?", "externalAdapterName": "string?", "allowManagementOS": bool } | 200: { "id": "guid" } | WMI: `Msvm_VirtualEthernetSwitchManagementService.DefineSystem` (cross-ref: Hyper-V/ Networking sample, v2 networking overview). |
| `/api/v1/networks/{id}` | DELETE | Delete switch. | Path: `id` (guid) | 200: OK | WMI: `DestroySystem`. HCS: HCN delete. |
| `/api/v1/networks/extensions` | GET | List installed extensions. | None | 200: [ExtensionInfo[]] { "name": "string", "vendor": "string", "type": "enum" } | WMI: Query `Msvm_EthernetSwitchExtension` (cross-ref: Hyper-V/ Networking). |
| `/api/v1/networks/{switchName}/extensions/{extensionName}/enabled` | PUT | Enable/disable extension. | Path: `switchName`, `extensionName`, Body: { "enabled": bool } | 200: OK | WMI: `RequestStateChange(2\|3)`. |
| `/api/v1/vms/{vmName}/connect/{switchName}` | POST | Connect VM to switch. | Path: `vmName`, `switchName` | 200: OK | WMI: `AddResourceSettings` on `Msvm_SyntheticEthernetPort` (cross-ref: Hyper-V/ Networking ConnectVmToSwitch). |
| `/api/v1/fibrechannel/san` | POST | Create Fibre Channel SAN pool. | Body: `CreateSanRequest` { "sanName": "string", "wwpnArray": "string[]", "wwnnArray": "string[]", "notes": "string?" } | 200: { "poolId": "string" } | WMI: `Msvm_ResourcePoolConfigurationService.CreatePool` (cross-ref: Hyper-V/ FibreChannel sample, v2 FibreChannel). |
| `/api/v1/fibrechannel/san/{poolId}` | DELETE | Delete Fibre Channel SAN pool. | Path: `poolId` | 200: OK | WMI: `DestroyPool`. |
| `/api/v1/fibrechannel/san/{poolId}` | GET | Get Fibre Channel SAN pool info. | Path: `poolId` | 200: `SanInfoResponse` { "poolId": "string", "name": "string", "notes": "string", "allocations": "string[]" } | WMI: Query `Msvm_ResourcePool`. |
| `/api/v1/vms/{vmName}/fibrechannel/port` | POST | Add Fibre Channel port to VM. | Path: `vmName`, Body: `CreateFcPortRequest` { "sanPoolId": "string", "wwpn": "string", "wwnn": "string" } | 200: { "portId": "string" } | WMI: `AddResourceSettings` on `Msvm_FibreChannelVirtualPortAllocationSettingData`. |

### Storage Category (StorageController.cs, StorageService.cs)

| Endpoint | Method | Description | Request Schema | Response Schema | Backend Details & Cross-References |
|----------|--------|-------------|----------------|-----------------|----------------------------|
| `/api/v1/storage/vhd` | POST | Create VHD/VHDX. | Body: `CreateVhdRequest` { "path": "string", "maxInternalSize": ulong, "type": "Fixed\|Dynamic", "format": "VHD\|VHDX" } | 200: OK | WMI: `Msvm_ImageManagementService.CreateVirtualHardDisk` (cross-ref: Hyper-V/ Storage CreateVirtualHardDisk, v2 CreateVirtualHardDisk). HCS: `VirtDiskNative.CreateVirtualDisk`. |
| `/api/v1/storage/vhd/attach` | PUT | Attach VHD to VM. | Query: `vmName`, `vhdPath` | 200: OK | WMI: `AddResourceSettings` on `Msvm_StorageAllocationSettingData`. |
| `/api/v1/storage/vhd/detach` | PUT | Detach VHD from VM. | Query: `vmName`, `vhdPath` | 200: OK | WMI: `RemoveResourceSettings`. |
| `/api/v1/storage/vhd/resize` | PUT | Resize VHD. | Body: `ResizeVhdRequest` { "path": "string", "maxInternalSize": ulong } | 200: OK | WMI: `Msvm_ImageManagementService.ExpandVirtualHardDisk` (cross-ref: Hyper-V/ Storage resize). |
| `/api/v1/storage/vhd/metadata` | GET/PUT | Get/update VHD metadata. | GET: Query `vhdPath`. PUT: Body `VhdMetadataUpdate` { "uniqueId": "guid?", "parentPath": "string?" } | 200: `VhdMetadata` { "path": "string", "size": ulong } or OK | WMI: Query/update `Msvm_VirtualHardDiskSettingData` (cross-ref: v2 GetVirtualHardDiskSettingData). |
| `/api/v1/storage/vhd/compact` | POST | Compact VHD (async). | Body: `CompactVhdRequest` { "path": "string" } | 202: { "jobId": "string" } | WMI: `CompactVirtualHardDisk` (cross-ref: Hyper-V/ Storage). |
| `/api/v1/storage/vhdset/{vhdSetPath}/info` | GET | Get VHD Set info. | Path: `vhdSetPath` | 200: `VhdSetInformationResponse` | WMI: `GetVHDSetInformation` (cross-ref: v2 VHDSet-specific). |
| `/api/v1/storage/extents` | GET | List storage extents. | None | 200: [Extent[]] { "extentId": "string", "name": "string" } | WMI: Query `Msvm_StorageAllocationSettingData` (ResourceType=31). |
| `/api/v1/qos` | POST | Create Storage QoS policy. | Body: `CreateQoSRequest` { "policyId": "string", "maxIops": int?, "maxBandwidth": int?, "description": "string?" } | 200: { "policyId": "string" } | WMI: `CreateQoSPolicy` on `Msvm_StorageQoSResourceAllocationSettingData` (cross-ref: Hyper-V/ StorageQoS). |
| `/api/v1/qos/{policyId}` | DELETE | Delete Storage QoS policy. | Path: `policyId` | 200: OK | WMI: `DeleteQoSPolicy`. |
| `/api/v1/qos/{policyId}` | GET | Get Storage QoS policy info. | Path: `policyId` | 200: `QoSInfoResponse` { "policyId": "string", "maxIops": int, "maxBandwidth": int, "description": "string" } | WMI: `GetQoSPolicyInfo`. |
| `/api/v1/vms/{vmName}/qos/{policyId}` | POST | Apply Storage QoS policy to VM. | Path: `vmName`, `policyId` | 200: OK | WMI: `ModifyResourceSettings` to `ApplyQoSPolicyToVm`. |

### Replication Category (ReplicationController.cs)

| Endpoint | Method | Description | Request Schema | Response Schema | Backend Details & Cross-References |
|----------|--------|-------------|----------------|-----------------|----------------------------|
| `/api/v1/replication/relationships` | POST | Create replication relationship. | Body: `CreateReplicationRequest` { "sourceVm": "string", "targetHost": "string", "authMode": "string?" } | 200: { "relationshipId": "string" } | WMI: `Msvm_ReplicationService.CreateReplicationRelationship` (cross-ref: Hyper-V/ Replica sample, v2 CreateReplicationRelationship). |
| `/api/v1/replication/{vmName}/start` | POST | Start replication for VM. | Path: `vmName` | 202: { "jobId": "string" } | WMI: `Msvm_ReplicationService.StartReplication`. |
| `/api/v1/replication/{vmName}/failover` | POST | Initiate failover for VM. | Path: `vmName`, Body: `FailoverRequest` { "mode": "'Planned' \| 'Test' \| 'Live'" } | 202: { "jobId": "string" } | WMI: `Msvm_ReplicationService.InitiateFailover` (cross-ref: v2 InitiateFailover). |
| `/api/v1/replication/{vmName}/reverse` | PUT | Reverse replication direction. | Path: `vmName` | 200: OK | WMI: `Msvm_ReplicationService.ReverseReplication`. |
| `/api/v1/replication/{vmName}/state` | GET | Get replication state for VM. | Path: `vmName` | 200: { "state": "string", "relationshipId": "string" } | WMI: Query `Msvm_ReplicationService` state. |
| `/api/v1/replication/{relationshipId}/authorization` | PUT | Update replication authorization. | Path: `relationshipId`, Body: { "authMode": "string", "credentials": "object?" } | 200: OK | WMI: `Msvm_ReplicationService.UpdateAuthorization`. |

### Metrics Category (VmsController.cs, MetricsService.cs)

| Endpoint | Method | Description | Request Schema | Response Schema | Backend Details & Cross-References |
|----------|--------|-------------|----------------|-----------------|----------------------------|
| `/api/v1/vms/{name}/metrics/discrete` | GET | Enumerate discrete metrics for VM. | Path: `name` | 200: [Metric[]] { "name": "string", "type": "string" } | WMI: `Msvm_MetricService.EnumerateDiscreteMetrics` (cross-ref: Hyper-V/ Metrics ControlMetrics, v2 Msvm_MetricsService). |
| `/api/v1/vms/metrics/pool` | GET | Enumerate metrics for resource pool. | Query: `resourceType`, `subType`, `poolId` | 200: [Metric[]] | WMI: `EnumerateMetricsForResourcePool`. |

Other categories (e.g., Jobs: `/api/v1/jobs/{id}` GET for status) follow similar patterns.

## 3. Implemented Extensions

Based on gap analysis (now 100% coverage for prioritized features), the following expansions have been implemented using WMI for unsupported HCS areas. All gaps in replication, migration, Fibre Channel, and QoS have been filled to achieve full Hyper-V parity for enterprise use (DR, migration, SAN).

- **ReplicationController**: Full endpoints for relationship creation, start, failover, reverse, state, and authorization (WMI-only).
- **VmsController Enhancements**: Migration, app health, and guest file copy endpoints.
- **NetworksController Enhancements**: Fibre Channel SAN creation, deletion, query, and VM port addition.
- **StorageController Enhancements**: QoS policy creation, deletion, query, and VM application.

Implementation complete: Services (e.g., ReplicationService.cs, FibreChannelService.cs, StorageQoSService.cs) added; controllers updated; types defined; tests integrated with Hyper-V samples.

## 4. Usage Examples

Adapted from Hyper-V/ samples to API calls (using curl):

- **Create VM** (from Generation2VM sample):
  ```
  curl -X POST http://localhost:5000/api/v1/vms \
  -H "Content-Type: application/json" \
  -d '{
    "name": "MyVM",
    "memoryMB": 2048,
    "cpuCount": 2,
    "diskSizeGB": 40
  }'
  ```

- **Start VM**:
  ```
  curl -X POST http://localhost:5000/api/v1/vms/MyVM/start
  ```

- **Create and Attach VHD**:
  ```
  curl -X POST http://localhost:5000/api/v1/storage/vhd \
  -H "Content-Type: application/json" \
  -d '{"path": "C:\\VMs\\MyVM.vhdx", "maxInternalSize": 42949672960, "type": "Dynamic", "format": "VHDX"}'
  
  curl -X PUT "http://localhost:5000/api/v1/storage/vhd/attach?vmName=MyVM&vhdPath=C:\\VMs\\MyVM.vhdx"
  ```

- **List Networks and Connect VM**:
  ```
  curl http://localhost:5000/api/v1/networks
  
  curl -X POST http://localhost:5000/api/v1/vms/MyVM/connect/MySwitch
  ```

For full async job polling: Use GET `/api/v1/jobs/{jobId}` until status is "Completed".

## 5. Error Handling

All endpoints return standard HTTP status codes:
- **200 OK**: Success.
- **201 Created**: Resource created.
- **202 Accepted**: Async job started (body: `{ "jobId": "string" }`).
- **400 Bad Request**: Invalid input (body: `{ "error": "string", "details": "string" }`).
- **401 Unauthorized**: Auth failed.
- **404 Not Found**: Resource not found.
- **500 Internal Server Error**: Backend failure (body: `{ "error": "string", "backend": "WMI\|HCS" }`).
- **501 Not Implemented**: Feature unsupported in backend.

WMI errors include CIM status codes (e.g., WBEM_E_NOT_FOUND). HCS errors map to HRESULT.

## 6. References

- **WMI V2 Documentation**: [Hyper-V WMI Provider](https://docs.microsoft.com/en-us/windows/win32/hyperv_v2/) - Classes: `Msvm_*`.
- **HCS API**: [Host Compute Service](https://docs.microsoft.com/en-us/virtualization/windowscontainers/manage-containers/hyper-v-container) - HCN for networking, VirtDisk for storage.
- **Samples**: GitHub [Hyper-V Samples](https://github.com/Microsoft/Windows-classic-samples/tree/main/Samples/HyperV) - VM creation, networking, storage.
- **Tools**: PowerShell `Get-WmiObject`, `Invoke-WmiMethod`; HCS via P/Invoke or wrappers.
- **Schemas**: OpenAPI/Swagger at `/swagger` (auto-generated from controllers).