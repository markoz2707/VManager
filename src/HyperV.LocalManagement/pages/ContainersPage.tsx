import React, { useState, useEffect, useCallback } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Container, VmEnvironment } from '../types';
import { containerService } from '../services/hypervService';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { PlayIcon, StopIcon, PauseIcon, PlusIcon, TrashIcon, ResumeIcon } from '../components/Icons';
import { OutletContextType } from '../App';
import { Tabs } from '../components/Tabs';

type ContainerAction = 'start' | 'stop' | 'pause' | 'resume' | 'terminate';

const statusColors: Record<string, string> = {
    'Running': 'bg-green-500',
    'Stopped': 'bg-red-500',
    'Paused': 'bg-yellow-500',
};

const ContainerCard: React.FC<{
    container: Container;
    onAction: (id: string, action: ContainerAction) => void,
    onDelete: (id: string) => void,
    isBusy: boolean;
}> = ({ container, onAction, onDelete, isBusy }) => {
    return (
        <div className="bg-gray-800 rounded-lg shadow-md p-4 flex flex-col justify-between transition-transform transform hover:scale-105">
            <div>
                <div className="flex justify-between items-start">
                    <h3 className="text-lg font-bold text-white">{container.name}</h3>
                    <div className="flex items-center">
                        <span className={`h-3 w-3 rounded-full mr-2 ${statusColors[container.state] || 'bg-gray-500'}`}></span>
                        <span className="text-sm font-medium text-gray-300">{container.state}</span>
                    </div>
                </div>
                <div className="text-sm text-gray-400 mt-2">
                    <div>Image: <span className="text-gray-200">{container.image}</span></div>
                    <div className="mt-1 grid grid-cols-2 gap-2">
                        <span>CPUs: <span className="font-semibold text-gray-200">{container.cpuCount}</span></span>
                        <span>Memory: <span className="font-semibold text-gray-200">{container.memoryMB} MB</span></span>
                    </div>
                </div>
            </div>
            <div className="mt-4 flex space-x-2">
                <Button size="sm" variant="ghost" disabled={isBusy || container.state !== 'Stopped'} onClick={() => onAction(container.id, 'start')}>
                    <PlayIcon className="h-4 w-4" />
                </Button>
                <Button size="sm" variant="ghost" disabled={isBusy || container.state !== 'Running'} onClick={() => onAction(container.id, 'stop')}>
                    <StopIcon className="h-4 w-4" />
                </Button>
                <Button size="sm" variant="ghost" disabled={isBusy || container.state !== 'Running'} onClick={() => onAction(container.id, 'pause')}>
                    <PauseIcon className="h-4 w-4" />
                </Button>
                <Button size="sm" variant="ghost" disabled={isBusy || container.state !== 'Paused'} onClick={() => onAction(container.id, 'resume')}>
                    <ResumeIcon className="h-4 w-4" />
                </Button>
                <Button size="sm" variant="secondary" disabled={isBusy} onClick={() => onDelete(container.id)}>
                    <TrashIcon className="h-4 w-4" />
                </Button>
            </div>
        </div>
    );
};

export const ContainersPage = () => {
    const { addNotification } = useOutletContext<OutletContextType>();
    const [allContainers, setAllContainers] = useState<{ WMI: Container[], HCS: Container[] }>({ WMI: [], HCS: [] });
    const [activeTab, setActiveTab] = useState<VmEnvironment>('HCS');
    const [isLoading, setIsLoading] = useState(true);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [busyContainer, setBusyContainer] = useState<string | null>(null);

    const [newContainer, setNewContainer] = useState({
        name: '',
        image: 'mcr.microsoft.com/windows/servercore:ltsc2022',
        memoryMB: '2048',
        cpuCount: '2',
        storageSizeGB: '20'
    });

    const fetchContainers = useCallback(async () => {
        try {
            const containerList = await containerService.getContainers();
            setAllContainers(containerList);
        } catch (err: any) {
            addNotification('error', `Failed to load containers: ${err.message}`);
        } finally {
            setIsLoading(false);
        }
    }, [addNotification]);

    useEffect(() => {
        setIsLoading(true);
        fetchContainers();
    }, [fetchContainers]);

    const handleAction = async (id: string, action: ContainerAction) => {
        setBusyContainer(id);
        const actionMap = {
            start: containerService.startContainer,
            stop: containerService.stopContainer,
            pause: containerService.pauseContainer,
            resume: containerService.resumeContainer,
            terminate: containerService.terminateContainer,
        };

        try {
            await actionMap[action](id);
            addNotification('success', `Container action '${action}' completed successfully.`);
            setTimeout(fetchContainers, 1000);
        } catch (err: any) {
            addNotification('error', `Action failed: ${err.message}`);
        } finally {
            setBusyContainer(null);
        }
    };

    const handleDelete = async (id: string) => {
        setBusyContainer(id);
        try {
            await containerService.deleteContainer(id);
            addNotification('success', 'Container deleted successfully.');
            fetchContainers();
        } catch (err: any) {
            addNotification('error', `Failed to delete container: ${err.message}`);
        } finally {
            setBusyContainer(null);
        }
    };

    const handleCreateContainer = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await containerService.createContainer({
                name: newContainer.name,
                image: newContainer.image,
                memoryMB: parseInt(newContainer.memoryMB, 10),
                cpuCount: parseInt(newContainer.cpuCount, 10),
                storageSizeGB: parseInt(newContainer.storageSizeGB, 10),
                environment: {},
                portMappings: {},
                volumeMounts: {},
                environmentType: activeTab
            });
            addNotification('success', `Container '${newContainer.name}' created successfully.`);
            setIsModalOpen(false);
            setNewContainer({
                name: '',
                image: 'mcr.microsoft.com/windows/servercore:ltsc2022',
                memoryMB: '2048',
                cpuCount: '2',
                storageSizeGB: '20'
            });
            fetchContainers();
        } catch (err: any) {
            addNotification('error', `Failed to create container: ${err.message}`);
        }
    };

    const displayedContainers = allContainers[activeTab] || [];

    return (
        <div className="p-6">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-3xl font-bold text-white">Containers</h1>
                <Button onClick={() => setIsModalOpen(true)} leftIcon={<PlusIcon />}>Create {activeTab} Container</Button>
            </div>

            <Tabs tabs={['HCS', 'WMI']} activeTab={activeTab} onTabClick={(tab) => setActiveTab(tab as VmEnvironment)} />

            {isLoading ? <div className="flex justify-center items-center h-96"><Spinner /></div> : (
                displayedContainers.length > 0 ? (
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
                        {displayedContainers.map(container => <ContainerCard
                            key={container.id}
                            container={container}
                            onAction={handleAction}
                            onDelete={handleDelete}
                            isBusy={busyContainer === container.id}
                        />)}
                    </div>
                ) : (
                    <div className="text-center py-12 text-gray-500">
                        <p>No containers found in the {activeTab} environment.</p>
                    </div>
                )
            )}

            <Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} title={`Create New ${activeTab} Container`}>
                <form onSubmit={handleCreateContainer}>
                    <div className="space-y-4">
                        <div>
                            <label htmlFor="container-name" className="block text-sm font-medium text-gray-300">Container Name</label>
                            <input 
                                type="text" 
                                id="container-name" 
                                value={newContainer.name} 
                                onChange={e => setNewContainer({ ...newContainer, name: e.target.value })} 
                                className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" 
                                required 
                            />
                        </div>
                        <div>
                            <label htmlFor="container-image" className="block text-sm font-medium text-gray-300">Container Image</label>
                            <input 
                                type="text" 
                                id="container-image" 
                                value={newContainer.image} 
                                onChange={e => setNewContainer({ ...newContainer, image: e.target.value })} 
                                className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" 
                                required 
                            />
                        </div>
                        <div>
                            <label htmlFor="cpu-count" className="block text-sm font-medium text-gray-300">CPU Count</label>
                            <input 
                                type="number" 
                                id="cpu-count" 
                                min="1" 
                                max="16" 
                                value={newContainer.cpuCount} 
                                onChange={e => setNewContainer({ ...newContainer, cpuCount: e.target.value })} 
                                className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" 
                                required 
                            />
                        </div>
                        <div>
                            <label htmlFor="memory-mb" className="block text-sm font-medium text-gray-300">Memory (MB)</label>
                            <input 
                                type="number" 
                                id="memory-mb" 
                                step="512" 
                                min="512" 
                                value={newContainer.memoryMB} 
                                onChange={e => setNewContainer({ ...newContainer, memoryMB: e.target.value })} 
                                className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" 
                                required 
                            />
                        </div>
                        <div>
                            <label htmlFor="storage-size-gb" className="block text-sm font-medium text-gray-300">Storage Size (GB)</label>
                            <input 
                                type="number" 
                                id="storage-size-gb" 
                                step="10" 
                                min="10" 
                                value={newContainer.storageSizeGB} 
                                onChange={e => setNewContainer({ ...newContainer, storageSizeGB: e.target.value })} 
                                className="mt-1 block w-full bg-gray-700 border border-gray-600 rounded-md shadow-sm py-2 px-3 text-white focus:outline-none focus:ring-primary-500 focus:border-primary-500" 
                                required 
                            />
                        </div>
                    </div>
                    <div className="mt-6 flex justify-end space-x-3">
                        <Button type="button" variant="secondary" onClick={() => setIsModalOpen(false)}>Cancel</Button>
                        <Button type="submit">Create</Button>
                    </div>
                </form>
            </Modal>
        </div>
    );
};