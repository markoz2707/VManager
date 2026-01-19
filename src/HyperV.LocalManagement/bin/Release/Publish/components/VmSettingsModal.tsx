import React, { useState, useEffect } from 'react';
import { VirtualMachine } from '../types';
import { CloseIcon, SettingsIcon, ChipIcon, InfoIcon } from './Icons';
import { Button } from './Button';
import { Spinner } from './Spinner';
import { Tabs } from './Tabs';
import { AccordionItem } from './CreateVmWizard';
import { 
    MemoryChipIcon, 
    HardDiskIconSimple, 
    ScsiControllerIcon, 
    SataControllerIcon, 
    UsbControllerIconSimple,
    NetworkIcon,
    CdDvdIcon,
    VideoCardIconSimple,
    AddHardDiskIcon,
    AddNetworkAdapterIcon,
    AddOtherDeviceIcon
} from './Icons';

interface VmSettingsModalProps {
    isOpen: boolean;
    onClose: () => void;
    vm: VirtualMachine | null;
    onSave: (vmName: string, config: { 
        cpuCount: number; 
        memoryMB: number;
        newName: string;
    }) => void;
    isSaving: boolean;
}

const initialHardwareConfig = {
    cpu: 2,
    memory: 4, // in GB
    hardDiskSize: 40, // in GB
    scsiController: 'LSI Logic SAS',
    networkAdapter: 'DPORT_VLAN450_Miron',
    connectNetwork: true,
};

const initialVmOptionsConfig = {
    vmName: '',
    lockGuestOs: false,
    enableVbs: false,
};

export const VmSettingsModal: React.FC<VmSettingsModalProps> = ({ isOpen, onClose, vm, onSave, isSaving }) => {
    const [activeTab, setActiveTab] = useState('Virtual Hardware');
    const [hardwareConfig, setHardwareConfig] = useState(initialHardwareConfig);
    const [vmOptionsConfig, setVmOptionsConfig] = useState(initialVmOptionsConfig);

    useEffect(() => {
        if (vm) {
            setHardwareConfig({
                ...initialHardwareConfig,
                cpu: vm.cpuCount || 2,
                memory: vm.memoryMB ? Math.round(vm.memoryMB / 1024) : 4,
            });
            setVmOptionsConfig({
                ...initialVmOptionsConfig,
                vmName: vm.name,
            });
        }
    }, [vm]);

    if (!isOpen || !vm) return null;

    const handleSave = () => {
        onSave(vm.name, {
            cpuCount: hardwareConfig.cpu,
            memoryMB: hardwareConfig.memory * 1024,
            newName: vmOptionsConfig.vmName,
        });
    };
    
    const inputClass = "bg-white border border-gray-300 rounded-md shadow-sm py-1.5 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500 disabled:bg-gray-100";
    const dropdownClass = `${inputClass} w-full`;

    const renderVmOptions = () => (
        <div className="bg-white border border-gray-300 rounded-sm">
            <AccordionItem title="General Options" summary="" defaultOpen>
                <div className="grid grid-cols-[1fr_2fr] items-center gap-x-4 gap-y-2">
                    <label className="text-sm font-medium text-gray-700">VM Name:</label>
                    <input
                        type="text"
                        value={vmOptionsConfig.vmName}
                        onChange={e => setVmOptionsConfig(c => ({...c, vmName: e.target.value}))}
                        className={inputClass}
                    />
                </div>
            </AccordionItem>
            <AccordionItem title="Remote Console Options" summary="">
                 <div className="flex items-center">
                    <input type="checkbox" id="lock-guest" checked={vmOptionsConfig.lockGuestOs} onChange={e => setVmOptionsConfig(c => ({...c, lockGuestOs: e.target.checked}))} className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500" style={{ colorScheme: 'light' }} />
                    <label htmlFor="lock-guest" className="ml-2 text-sm text-gray-700">Lock the guest operating system when the last remote user disconnects</label>
                </div>
            </AccordionItem>
            <AccordionItem title="HV Tools" summary="Expand for HV Tools settings"><p className="text-sm text-gray-500">Settings available after creation.</p></AccordionItem>
            <AccordionItem title="Power management" summary="Expand for power management settings"><p className="text-sm text-gray-500">Settings available after creation.</p></AccordionItem>
            <AccordionItem title="Boot Options" summary="Expand for boot options"><p className="text-sm text-gray-500">Settings available after creation.</p></AccordionItem>
            <AccordionItem title="Advanced" summary="Expand for advanced settings"><p className="text-sm text-gray-500">Settings available after creation.</p></AccordionItem>
            <AccordionItem title="Fiber Channel NPIV" summary="Expand for fiber channel NPIV"><p className="text-sm text-gray-500">Settings available after creation.</p></AccordionItem>
            
            <div className="border-t border-gray-200">
                <div className="w-full flex items-center py-3 px-3 text-left">
                    <span className="font-medium text-sm text-gray-800 mr-auto" style={{paddingLeft: '2.75rem'}}>VBS</span>
                    <div className="flex items-center">
                        <input type="checkbox" id="vbs-enable" checked={vmOptionsConfig.enableVbs} onChange={e => setVmOptionsConfig(c => ({...c, enableVbs: e.target.checked}))} className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500" style={{ colorScheme: 'light' }}/>
                        <label htmlFor="vbs-enable" className="ml-2 text-sm text-gray-700 mr-2">Enable Virtualization Based Security</label>
                        {/* Fix: Replaced the `title` prop on the InfoIcon component with a wrapping `span` element that has a standard `title` attribute for tooltips. This resolves the TypeScript error as the `title` prop is not part of the SVGProps type for React components in this context. */}
                        <span className="cursor-pointer" title="Virtualization Based Security (VBS) uses hardware virtualization features to create and isolate a secure region of memory from the normal operating system.">
                            <InfoIcon className="h-5 w-5 text-blue-500"/>
                        </span>
                    </div>
                </div>
            </div>
        </div>
    );

    const renderVirtualHardware = () => (
        <div className="space-y-3">
            <div className="flex space-x-1">
                <Button variant="ghost" size="sm" leftIcon={<AddHardDiskIcon className="h-4 w-4" />}>Add hard disk</Button>
                <Button variant="ghost" size="sm" leftIcon={<AddNetworkAdapterIcon className="h-4 w-4" />}>Add network adapter</Button>
                <Button variant="ghost" size="sm" leftIcon={<AddOtherDeviceIcon className="h-4 w-4" />}>Add other device</Button>
            </div>
            <div className="bg-white border border-gray-300 rounded-sm">
                <AccordionItem icon={<ChipIcon className="h-5 w-5"/>} title="CPU" summary={hardwareConfig.cpu} hasWarning defaultOpen>
                    <div className="grid grid-cols-[1fr_2fr] items-center gap-x-4 gap-y-2">
                        <label className="text-sm font-medium text-gray-700">CPU</label>
                        <div className="flex items-center gap-2">
                            <select value={hardwareConfig.cpu} onChange={e => setHardwareConfig(c => ({...c, cpu: parseInt(e.target.value)}))} className={dropdownClass}>
                                {[...Array(8).keys()].map(i => <option key={i+1} value={i+1}>{i+1}</option>)}
                            </select>
                            {/* Fix: Replaced the `title` prop on the InfoIcon component with a wrapping `span` element that has a standard `title` attribute for tooltips. This resolves the TypeScript error as the `title` prop is not part of the SVGProps type for React components in this context. */}
                            <span className="cursor-pointer" title="Number of virtual CPUs.">
                                <InfoIcon className="h-5 w-5 text-blue-500"/>
                            </span>
                        </div>
                    </div>
                </AccordionItem>
                 <AccordionItem icon={<MemoryChipIcon className="h-5 w-5"/>} title="Memory" summary={`${hardwareConfig.memory} GB`} hasWarning>
                    <div className="grid grid-cols-[1fr_2fr] items-center gap-x-4 gap-y-2">
                        <label className="text-sm font-medium text-gray-700">Memory</label>
                        <div className="flex items-center gap-2">
                            <input type="number" value={hardwareConfig.memory} onChange={e => setHardwareConfig(c => ({...c, memory: parseInt(e.target.value)}))} className={`${inputClass} w-24`} />
                            <select className={dropdownClass} defaultValue="GB">
                                <option>GB</option>
                                <option>MB</option>
                            </select>
                        </div>
                    </div>
                </AccordionItem>
                 <AccordionItem icon={<HardDiskIconSimple className="h-5 w-5"/>} title="Hard disk 1" summary={`${hardwareConfig.hardDiskSize} GB`} hasWarning deletable>
                     <div className="grid grid-cols-[1fr_2fr] items-center gap-x-4 gap-y-2">
                        <label className="text-sm font-medium text-gray-700">Disk Size</label>
                        <div className="flex items-center gap-2">
                            <input type="number" value={hardwareConfig.hardDiskSize} onChange={e => setHardwareConfig(c => ({...c, hardDiskSize: parseInt(e.target.value)}))} className={`${inputClass} w-24`} />
                            <select className={dropdownClass} defaultValue="GB">
                                <option>GB</option>
                                <option>MB</option>
                                <option>TB</option>
                            </select>
                        </div>
                    </div>
                </AccordionItem>
                <AccordionItem icon={<ScsiControllerIcon className="h-5 w-5"/>} title="SCSI Controller 0" summary={hardwareConfig.scsiController} deletable>
                    <div className="grid grid-cols-[1fr_2fr] items-center gap-x-4 gap-y-2">
                        <label className="text-sm font-medium text-gray-700">Type</label>
                        <select value={hardwareConfig.scsiController} onChange={e => setHardwareConfig(c => ({...c, scsiController: e.target.value}))} className={dropdownClass}>
                            <option>LSI Logic SAS</option>
                            <option>VMware Paravirtual</option>
                            <option>LSI Logic Parallel</option>
                        </select>
                    </div>
                </AccordionItem>
                <AccordionItem icon={<SataControllerIcon className="h-5 w-5"/>} title="SATA Controller 0" summary="" deletable><div className="text-sm text-gray-500">No editable settings.</div></AccordionItem>
                <AccordionItem icon={<UsbControllerIconSimple className="h-5 w-5"/>} title="USB controller 1" summary="USB 3.1" deletable><div className="text-sm text-gray-500">No editable settings.</div></AccordionItem>
                <AccordionItem icon={<NetworkIcon className="h-5 w-5"/>} title="Network Adapter 1" summary={hardwareConfig.networkAdapter} deletable>
                    <div className="grid grid-cols-[1fr_2fr] items-start gap-x-4 gap-y-3">
                        <label className="text-sm font-medium text-gray-700 pt-1.5">Network</label>
                        <select value={hardwareConfig.networkAdapter} onChange={e => setHardwareConfig(c => ({...c, networkAdapter: e.target.value}))} className={dropdownClass}>
                            <option>DPORT_VLAN450_Miron</option>
                            <option>Default Switch</option>
                            <option>Internal</option>
                            <option>Not Connected</option>
                        </select>
                        <div></div>
                        <div className="flex items-center">
                            <input type="checkbox" id="net-connect" checked={hardwareConfig.connectNetwork} onChange={e => setHardwareConfig(c => ({...c, connectNetwork: e.target.checked}))} className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500" style={{ colorScheme: 'light' }}/>
                            <label htmlFor="net-connect" className="ml-2 text-sm text-gray-700">Connect</label>
                        </div>
                    </div>
                </AccordionItem>
                <AccordionItem icon={<CdDvdIcon className="h-5 w-5"/>} title="CD/DVD Drive 1" summary="Specify custom settings" deletable><div className="text-sm text-gray-500">Settings available after creation.</div></AccordionItem>
                <AccordionItem icon={<VideoCardIconSimple className="h-5 w-5"/>} title="Video Card" summary="Specify custom settings"><div className="text-sm text-gray-500">Settings available after creation.</div></AccordionItem>
            </div>
        </div>
    );

    return (
        <div className="fixed inset-0 bg-black bg-opacity-60 flex justify-center items-center z-50 p-4" onClick={onClose}>
            <div 
                className="bg-gray-100 rounded-sm shadow-xl w-full max-w-4xl h-[95vh] flex flex-col border border-panel-border"
                onClick={e => e.stopPropagation()}
            >
                <header className="flex justify-between items-center px-6 py-3 border-b border-gray-300 bg-white rounded-t-sm flex-shrink-0">
                    <h2 className="text-lg font-semibold text-gray-800 flex items-center">
                        <SettingsIcon className="h-5 w-5 mr-3 text-gray-600" />
                        Edit settings - {vm.name} (ESXi 6.7 virtual machine)
                    </h2>
                    <button onClick={onClose} className="text-gray-500 hover:text-gray-800">
                        <CloseIcon className="h-6 w-6" />
                    </button>
                </header>

                <main className="flex-1 overflow-hidden flex flex-col">
                    <div className="px-6 pt-2 bg-white border-b border-gray-300">
                        <Tabs tabs={['Virtual Hardware', 'VM Options']} activeTab={activeTab} onTabClick={setActiveTab} />
                    </div>
                    
                    <div className="flex-1 p-4 overflow-y-auto">
                         {activeTab === 'Virtual Hardware' && renderVirtualHardware()}
                         {activeTab === 'VM Options' && renderVmOptions()}
                    </div>
                </main>

                <footer className="px-6 py-3 border-t border-gray-300 bg-gray-100/80 backdrop-blur-sm rounded-b-sm flex justify-end space-x-3 flex-shrink-0">
                    <Button variant="secondary" onClick={onClose}>CANCEL</Button>
                    <Button onClick={handleSave} disabled={isSaving}>
                        {isSaving ? <Spinner size="sm" /> : 'SAVE'}
                    </Button>
                </footer>
            </div>
        </div>
    );
};