import React, { useState, useEffect, useCallback } from 'react';
import { Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './context/AuthContext';
import { LoginPage } from './pages/LoginPage';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import type { SidebarContextMenuEvent } from './components/Sidebar';
import { HostView } from './features/host/HostView';
import { VmView } from './features/vm/VmView';
import { ClusterView } from './features/cluster/ClusterView';
import { CreateVmModal } from './components/CreateVmModal';
import { ContextMenu } from './components/ContextMenu';
import { AddDatacenterModal } from './components/AddDatacenterModal';
import { AddClusterModal } from './components/AddClusterModal';
import { AddHostModal } from './components/AddHostModal';
import { AddHostToClusterModal } from './components/AddHostToClusterModal';
import { getInfrastructure } from './services/infrastructureService';
import { createDatacenter } from './services/datacenterService';
import { createCluster, addNodeToCluster } from './services/clusterService';
import { createAgent } from './services/agentService';
import * as vmService from './services/vmService';
import type { Host, VirtualMachine, VmStatus, InfrastructureNode } from './types';
import { VmStatus as VmStatusEnum } from './types';
import { ChevronRightIcon, DatacenterIcon, ClusterIcon, HostIcon, PlusCircleIcon } from './components/icons/Icons';

type ActiveModal =
  | null
  | { type: 'addDatacenter' }
  | { type: 'addCluster'; datacenterId: string }
  | { type: 'addHost'; datacenterId?: string }
  | { type: 'addHostToCluster'; clusterId: string; clusterName: string };

function computeClusteredHostIds(tree: InfrastructureNode[]): Set<string> {
  const ids = new Set<string>();
  function walk(nodes: InfrastructureNode[]) {
    for (const node of nodes) {
      if (node.type === 'cluster' && node.children) {
        for (const child of node.children) {
          if (child.type === 'host') {
            ids.add(child.id);
          }
        }
      }
      if (node.children) {
        walk(node.children);
      }
    }
  }
  walk(tree);
  return ids;
}

const Dashboard: React.FC = () => {
  const [infrastructureTree, setInfrastructureTree] = useState<InfrastructureNode[]>([]);
  const [hosts, setHosts] = useState<Host[]>([]);
  const [vms, setVms] = useState<VirtualMachine[]>([]);
  const [selectedNode, setSelectedNode] = useState<{ id: string; type: InfrastructureNode['type'] }>({ id: '', type: 'datacenter' });
  const [isLoading, setIsLoading] = useState(true);
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [contextMenu, setContextMenu] = useState<SidebarContextMenuEvent | null>(null);
  const [activeModal, setActiveModal] = useState<ActiveModal>(null);

  const selectedHost = hosts.find(h => h.id === selectedNode.id);
  const vmsForSelectedHost = selectedHost ? vms.filter(vm => vm.hostId === selectedHost.id) : [];
  const selectedVm = vms.find(vm => vm.id === selectedNode.id);

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await getInfrastructure();
      setInfrastructureTree(data.tree);
      setHosts(data.hosts);
      setVms(data.vms);

      // Auto-select first host if nothing selected
      if (!selectedNode.id && data.hosts.length > 0) {
        setSelectedNode({ id: data.hosts[0].id, type: 'host' });
      }
    } catch (error) {
      console.error('Failed to fetch data:', error);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleUpdateVmStatus = async (vmId: string, status: VmStatus) => {
    try {
      // Map UI status to API power operation
      let operation: 'start' | 'stop' | 'shutdown' | 'pause' | 'resume';
      switch (status) {
        case VmStatusEnum.Running:
        case VmStatusEnum.PoweredOn:
          operation = 'start';
          break;
        case VmStatusEnum.Stopped:
          operation = 'stop';
          break;
        case VmStatusEnum.Saved:
          operation = 'pause';
          break;
        default:
          return;
      }
      await vmService.powerOperation(vmId, operation);
      await fetchData();
    } catch (error) {
      console.error(`Failed to update status for VM ${vmId}:`, error);
    }
  };

  const handleCreateVm = async (_name: string, _os: VirtualMachine['os'], _cpuCores: number, _memoryGB: number, _diskSizeGB: number) => {
    // VM creation requires agent proxy - placeholder
    setIsCreateModalOpen(false);
    alert('VM creation requires agent proxy support. Coming soon.');
  };

  const handleSelectNode = (id: string, type: InfrastructureNode['type']) => {
    setSelectedNode({ id, type });
  };

  const handleSidebarContextMenu = (event: SidebarContextMenuEvent) => {
    setContextMenu(event);
  };

  const getContextMenuItems = () => {
    if (!contextMenu) return [];

    switch (contextMenu.nodeType) {
      case 'header':
        return [
          {
            label: 'Add Datacenter',
            icon: <DatacenterIcon className="w-4 h-4" />,
            onClick: () => setActiveModal({ type: 'addDatacenter' }),
          },
        ];
      case 'datacenter':
        return [
          {
            label: 'Add Host',
            icon: <HostIcon className="w-4 h-4" />,
            onClick: () => setActiveModal({ type: 'addHost', datacenterId: contextMenu.nodeId ?? undefined }),
          },
          {
            label: 'Add Cluster',
            icon: <ClusterIcon className="w-4 h-4" />,
            onClick: () => setActiveModal({ type: 'addCluster', datacenterId: contextMenu.nodeId! }),
          },
        ];
      case 'cluster':
        return [
          {
            label: 'Add Host',
            icon: <PlusCircleIcon className="w-4 h-4" />,
            onClick: () => setActiveModal({
              type: 'addHostToCluster',
              clusterId: contextMenu.nodeId!,
              clusterName: contextMenu.nodeName ?? 'Cluster',
            }),
          },
        ];
      default:
        return [];
    }
  };

  // Modal handlers
  const handleCreateDatacenter = async (name: string, description?: string) => {
    await createDatacenter(name, description);
    setActiveModal(null);
    await fetchData();
  };

  const handleCreateCluster = async (name: string, description?: string) => {
    if (activeModal?.type !== 'addCluster') return;
    await createCluster(name, description, activeModal.datacenterId);
    setActiveModal(null);
    await fetchData();
  };

  const handleCreateHost = async (hostname: string, apiBaseUrl: string, hostType: string) => {
    const datacenterId = activeModal?.type === 'addHost' ? activeModal.datacenterId : undefined;
    await createAgent(hostname, apiBaseUrl, undefined, hostType, datacenterId);
    setActiveModal(null);
    await fetchData();
  };

  const handleAddHostToCluster = async (agentHostId: string) => {
    if (activeModal?.type !== 'addHostToCluster') return;
    await addNodeToCluster(activeModal.clusterId, agentHostId);
    setActiveModal(null);
    await fetchData();
  };

  const renderContent = () => {
    if (isLoading) {
      return <div className="text-center py-12 text-gray-500">Loading infrastructure data...</div>;
    }

    switch(selectedNode.type) {
      case 'host':
        return <HostView
                  host={selectedHost}
                  vms={vmsForSelectedHost}
                  onUpdateVmStatus={handleUpdateVmStatus}
                  onOpenCreateModal={() => setIsCreateModalOpen(true)}
                />;
      case 'vm':
        return selectedVm ? <VmView vm={selectedVm} onUpdateVmStatus={handleUpdateVmStatus} /> : null;
      case 'cluster':
        return <ClusterView />;
      case 'datacenter':
        return (
          <div className="text-center py-16 text-gray-500">
            <h2 className="text-2xl font-light text-gray-700 mb-4">VManager Central</h2>
            <p>Select a host or VM from the sidebar to view details.</p>
            <div className="mt-8 grid grid-cols-1 md:grid-cols-3 gap-4 max-w-2xl mx-auto">
              <div className="bg-white rounded-lg border border-gray-200 p-6">
                <p className="text-3xl font-light text-blue-600">{hosts.length}</p>
                <p className="text-sm text-gray-500 mt-1">Hyper-V Hosts</p>
              </div>
              <div className="bg-white rounded-lg border border-gray-200 p-6">
                <p className="text-3xl font-light text-green-600">{vms.filter(v => v.status === VmStatusEnum.Running || v.status === VmStatusEnum.PoweredOn).length}</p>
                <p className="text-sm text-gray-500 mt-1">Running VMs</p>
              </div>
              <div className="bg-white rounded-lg border border-gray-200 p-6">
                <p className="text-3xl font-light text-gray-600">{vms.length}</p>
                <p className="text-sm text-gray-500 mt-1">Total VMs</p>
              </div>
            </div>
          </div>
        );
      default:
        return <div className="text-center py-16 text-gray-500"><p>Select an item from the sidebar.</p></div>;
    }
  }

  return (
    <div className="flex flex-col h-screen bg-gray-50 font-sans text-gray-900">
      <Header />
      <div className="flex flex-grow overflow-hidden">
        <aside className={`bg-white border-r border-gray-200 transition-all duration-300 ease-in-out ${isSidebarCollapsed ? 'w-0' : 'w-64'} flex-shrink-0`}>
           <div className="h-full overflow-y-auto">
             <Sidebar tree={infrastructureTree} onSelect={handleSelectNode} selectedId={selectedNode.id} onContextMenu={handleSidebarContextMenu} />
           </div>
        </aside>
        <div className="relative flex-shrink-0">
          <button onClick={() => setIsSidebarCollapsed(!isSidebarCollapsed)} className="absolute top-1/2 -right-3 z-10 w-6 h-16 bg-white border border-gray-300 rounded-r-md flex items-center justify-center hover:bg-gray-100 transform -translate-y-1/2">
            <ChevronRightIcon className={`w-4 h-4 transition-transform duration-300 ${isSidebarCollapsed ? '' : 'rotate-180'}`} />
          </button>
        </div>
        <main className="flex-grow p-4 lg:p-6 overflow-y-auto bg-gray-100">
          {renderContent()}
        </main>
      </div>

      {/* Context Menu */}
      {contextMenu && (
        <ContextMenu
          x={contextMenu.x}
          y={contextMenu.y}
          items={getContextMenuItems()}
          onClose={() => setContextMenu(null)}
        />
      )}

      {/* Create VM Modal */}
      {isCreateModalOpen && (
        <CreateVmModal
          onClose={() => setIsCreateModalOpen(false)}
          onCreate={handleCreateVm}
        />
      )}

      {/* Add Datacenter Modal */}
      {activeModal?.type === 'addDatacenter' && (
        <AddDatacenterModal
          onClose={() => setActiveModal(null)}
          onCreate={handleCreateDatacenter}
        />
      )}

      {/* Add Cluster Modal */}
      {activeModal?.type === 'addCluster' && (
        <AddClusterModal
          datacenterId={activeModal.datacenterId}
          onClose={() => setActiveModal(null)}
          onCreate={handleCreateCluster}
        />
      )}

      {/* Add Host Modal */}
      {activeModal?.type === 'addHost' && (
        <AddHostModal
          datacenterId={activeModal.datacenterId}
          onClose={() => setActiveModal(null)}
          onCreate={handleCreateHost}
        />
      )}

      {/* Add Host to Cluster Modal */}
      {activeModal?.type === 'addHostToCluster' && (
        <AddHostToClusterModal
          clusterId={activeModal.clusterId}
          clusterName={activeModal.clusterName}
          allHosts={hosts}
          clusteredHostIds={computeClusteredHostIds(infrastructureTree)}
          onClose={() => setActiveModal(null)}
          onAdd={handleAddHostToCluster}
        />
      )}
    </div>
  );
};

const ProtectedRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { isLoggedIn } = useAuth();
  if (!isLoggedIn) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
};

const App: React.FC = () => {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={
            <ProtectedRoute>
              <Dashboard />
            </ProtectedRoute>
          }
        />
      </Routes>
    </AuthProvider>
  );
};

export default App;
