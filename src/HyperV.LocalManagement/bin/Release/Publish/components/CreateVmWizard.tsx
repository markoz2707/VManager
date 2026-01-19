import React, { useState, useMemo, useEffect } from 'react';
import { useOutletContext } from 'react-router-dom';
import * as api from '../services/hypervService';
import { StorageLocation } from '../types';
import { OutletContextType } from '../App';
// DO add comment above each fix.
// Fix: Removed non-existent ClusterIcon export from the imports.
import { CloseIcon, ChevronRightIcon, InfoIcon, ActionsIcon, FolderIcon, DatacenterIcon, HostIcon, CheckCircleIcon, VmIcon, ExclamationTriangleIcon } from './Icons';
import { Button } from './Button';
import { Spinner } from './Spinner';
import { Tabs } from './Tabs';

const steps = [
    { number: 1, title: 'Select a creation type' },
    { number: 2, title: 'Select a name and folder' },
    { number: 3, title: 'Select storage' },
    { number: 4, title: 'Select compatibility' },
    { number: 5, title: 'Select a guest OS' },
    { number: 6, title: 'Customize hardware' },
    { number: 7, title: 'Ready to complete' },
];

const initialVmConfig = {
    name: '',
    creationType: 'Create a new virtual machine',
    folderPath: 'Syta',
    storagePath: '', // e.g. 'C:\\VMs'
    compatibility: 'Generation 2',
    secureBoot: true, // Default to true for Gen 2
    guestOsVersion: 'Microsoft Windows Server 2022 (64-bit)',
    hardware: {
        cpu: 2,
        memory: 4,
        diskSize: 90,
        network: 'host210-to-host212',
    },
};

const WizardStepper = ({ currentStep }: { currentStep: number }) => (
    <div className="w-72 bg-gray-50 p-6 border-r border-gray-200 flex-shrink-0">
        <h2 className="text-xl font-semibold text-gray-800 mb-6">New Virtual Machine</h2>
        <ul className="space-y-1">
            {steps.map(step => (
                <li key={step.number} className={`flex items-center p-2 rounded-md ${currentStep === step.number ? 'bg-primary-50' : ''}`}>
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center mr-4 flex-shrink-0 text-sm font-bold ${currentStep >= step.number ? 'bg-primary-700 text-white' : 'bg-gray-300 text-gray-600'}`}>
                        {step.number}
                    </div>
                    <span className={`font-semibold text-sm ${currentStep === step.number ? 'text-primary-800' : 'text-gray-700'}`}>{step.title}</span>
                </li>
            ))}
        </ul>
    </div>
);

// Fix: Refactored AccordionItem to use a standard React.FC and a named props interface.
// This resolves TypeScript errors where the `children` prop was not correctly inferred
// from the JSX structure when using an inline type definition, which caused "Property 'children' is missing" errors.
interface AccordionItemProps {
    title: string;
    summary: React.ReactNode;
    children: React.ReactNode;
    defaultOpen?: boolean;
    icon?: React.ReactNode;
    hasWarning?: boolean;
    onDelete?: () => void;
    deletable?: boolean;
}

export const AccordionItem: React.FC<AccordionItemProps> = ({ title, summary, children, defaultOpen = false, icon, hasWarning, onDelete, deletable }) => {
    const [isOpen, setIsOpen] = useState(defaultOpen);
    return (
        <div className="border-b border-gray-200 last:border-b-0">
            <div className="w-full flex items-center py-2 px-3 text-left hover:bg-gray-50/50">
                <button onClick={() => setIsOpen(!isOpen)} className="flex items-center flex-1 focus:outline-none text-left">
                    <ChevronRightIcon className={`h-5 w-5 text-gray-500 transform transition-transform duration-200 flex-shrink-0 ${isOpen ? 'rotate-90' : ''}`} />
                    {icon && <div className="mx-2 text-gray-600 w-5 h-5 flex-shrink-0">{icon}</div>}
                    <span className="font-medium text-sm text-gray-800 mr-2">{title}</span>
                    {hasWarning && <ExclamationTriangleIcon className="h-4 w-4 text-yellow-500 mx-1 flex-shrink-0" />}
                </button>
                <div className="text-sm text-gray-600 mr-4 ml-auto whitespace-nowrap">{summary}</div>
                {deletable && onDelete ? (
                    <button onClick={onDelete} className="text-gray-400 hover:text-red-600 p-1">
                        <CloseIcon className="h-4 w-4" />
                    </button>
                ) : (
                    // Placeholder to keep alignment consistent
                    <div className="w-6 h-6"></div>
                )}
            </div>
            {isOpen && <div className="bg-gray-50/80 p-4 border-t border-gray-200">{children}</div>}
        </div>
    );
};

const CompatibilityCheck = () => (
    <div className="mt-4 p-4 bg-green-50 border border-green-200 rounded-md flex items-center">
        <CheckCircleIcon className="h-5 w-5 text-green-600 mr-3 flex-shrink-0" />
        <p className="text-sm text-green-800">Compatibility checks succeeded.</p>
    </div>
);

// Fix: Changed TreeViewItem to a standard React.FC with an interface for its props.
// This resolves the TypeScript error where the `key` prop, used in a `.map()` loop, was being
// incorrectly flagged as an unknown property because the inline type definition did not
// account for React's special props.
interface TreeViewItemProps {
    icon: React.ReactNode;
    label: string;
    level?: number;
    selected: string;
    onSelect: (label: string) => void;
    expanded?: boolean;
    onToggle?: () => void;
}

const TreeViewItem: React.FC<TreeViewItemProps> = ({ icon, label, level = 0, selected, onSelect, expanded, onToggle }) => {
    const isSelected = selected === label;
    return (
        <div 
            className={`flex items-center p-1.5 cursor-pointer rounded ${isSelected ? 'bg-primary-700 text-white' : 'hover:bg-gray-100'}`}
            style={{ paddingLeft: `${level * 24 + 12}px` }}
            onClick={() => onSelect(label)}
        >
            <ChevronRightIcon className={`h-5 w-5 mr-1 text-gray-400 transition-transform ${expanded ? 'rotate-90' : ''}`} onClick={onToggle ? (e) => { e.stopPropagation(); onToggle(); } : undefined}/>
            {icon}
            <span className="ml-2 text-sm">{label}</span>
        </div>
    );
};

const Step1CreationType = ({ vmConfig, setVmConfig }: { vmConfig: typeof initialVmConfig; setVmConfig: React.Dispatch<React.SetStateAction<typeof initialVmConfig>>}) => {
    const creationTypes = [
        { name: 'Create a new virtual machine', description: 'This option guides you through creating a new virtual machine. You will be able to customize processors, memory, network connections, and storage. You will need to install a guest operating system after creation.' },
        { name: 'Deploy from template', description: 'Deploy a new virtual machine from a pre-configured template.' },
        { name: 'Clone an existing virtual machine', description: 'Create an exact copy of an existing virtual machine.' },
        { name: 'Clone virtual machine to template', description: 'Create a template from an existing virtual machine.' },
        { name: 'Convert template to virtual machine', description: 'Convert an existing template into a virtual machine.' },
        { name: 'Clone template to template', description: 'Create a copy of an existing template.' },
    ];
    const selectedType = creationTypes.find(t => t.name === vmConfig.creationType) || creationTypes[0];

    return (
        <div>
            <h3 className="font-bold text-xl text-gray-800">Select a creation type</h3>
            <p className="text-sm text-gray-500 mt-1 mb-4">How would you like to create a virtual machine?</p>
            <div className="flex -mx-4">
                <div className="w-1/2 px-4">
                    <div className="border border-gray-300 rounded-md p-1 h-72 overflow-y-auto">
                        {creationTypes.map((type, index) => (
                            <div
                                key={type.name}
                                onClick={() => index === 0 && setVmConfig(c => ({...c, creationType: type.name}))}
                                className={`p-2 rounded text-sm ${index === 0 ? 'cursor-pointer' : 'cursor-not-allowed text-gray-400'} ${vmConfig.creationType === type.name ? 'bg-primary-700 text-white' : (index === 0 ? 'hover:bg-gray-100' : '')}`}
                            >
                                {type.name}
                            </div>
                        ))}
                    </div>
                </div>
                <div className="w-1/2 px-4">
                    <p className="text-sm text-gray-600 bg-gray-50 p-4 rounded-md h-72">{selectedType.description}</p>
                </div>
            </div>
        </div>
    );
};

const Step2NameAndFolder = ({ vmConfig, setVmConfig }: { vmConfig: typeof initialVmConfig; setVmConfig: React.Dispatch<React.SetStateAction<typeof initialVmConfig>>}) => {
    const commonInputClass = "mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500";
    const folderData = [
        { icon: <DatacenterIcon className="h-5 w-5"/>, label: 'vcenter-prod.itss.local' },
        { icon: <FolderIcon className="h-5 w-5"/>, label: 'Syta', level: 1 },
        { icon: <FolderIcon className="h-5 w-5"/>, label: 'Discovered virtual machine', level: 1 },
        { icon: <FolderIcon className="h-5 w-5"/>, label: 'hci-fio-PROD-vms', level: 1 },
        { icon: <FolderIcon className="h-5 w-5"/>, label: 'Kamery', level: 1 },
        { icon: <FolderIcon className="h-5 w-5"/>, label: 'Openshift', level: 1 },
        { icon: <FolderIcon className="h-5 w-5"/>, label: 'PROD', level: 1 },
    ];
    return (
        <div>
            <h3 className="font-bold text-xl text-gray-800">Select a name and folder</h3>
            <p className="text-sm text-gray-500 mt-1 mb-4">Specify a unique name and target location</p>
            <div className="mb-6">
                <label className="block text-sm font-medium text-gray-700">Virtual machine name:</label>
                <input type="text" value={vmConfig.name} onChange={e => setVmConfig(c => ({ ...c, name: e.target.value }))} className={commonInputClass} />
            </div>
            <div>
                 <label className="block text-sm font-medium text-gray-700">Select a location for the virtual machine.</label>
                 <div className="mt-2 border border-gray-300 rounded-md p-2 h-64 overflow-y-auto">
                    {folderData.map(item => (
                        <TreeViewItem 
                            key={item.label}
                            icon={item.icon} 
                            label={item.label} 
                            level={item.level}
                            selected={vmConfig.folderPath}
                            onSelect={(label) => setVmConfig(c => ({...c, folderPath: label}))}
                        />
                    ))}
                 </div>
            </div>
        </div>
    );
};

const Step3Storage = ({ vmConfig, setVmConfig }: { vmConfig: typeof initialVmConfig; setVmConfig: React.Dispatch<React.SetStateAction<typeof initialVmConfig>>}) => {
    const [storageLocations, setStorageLocations] = useState<StorageLocation[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const { addNotification } = useOutletContext<OutletContextType>();
    
    useEffect(() => {
        const fetchStorage = async () => {
            setIsLoading(true);
            try {
                // Fetch locations that can hold the specified disk size
                const locations = await api.storageService.getSuitableVhdLocations(vmConfig.hardware.diskSize);
                const suitableLocations = locations.filter(l => l.isSuitable);
                setStorageLocations(suitableLocations);
                
                // Auto-select first suitable path if none is selected
                if (!vmConfig.storagePath && suitableLocations.length > 0 && suitableLocations[0].suggestedPaths.length > 0) {
                    setVmConfig(c => ({...c, storagePath: suitableLocations[0].suggestedPaths[0]}));
                }
            } catch (err: any) {
                addNotification('error', `Failed to load storage locations: ${err.message}`);
            } finally {
                setIsLoading(false);
            }
        };
        fetchStorage();
    }, [vmConfig.hardware.diskSize, addNotification, setVmConfig]);


    return (
        <div>
            <h3 className="font-bold text-xl text-gray-800">Select storage</h3>
            <p className="text-sm text-gray-500 mt-1 mb-4">Select a location for the virtual disk files.</p>
            
            {isLoading ? <Spinner /> : (
                <div className="border border-gray-300 rounded-md overflow-hidden">
                    <table className="min-w-full text-sm">
                        <thead className="bg-gray-100 text-gray-600">
                            <tr>
                                <th className="p-2 text-left font-semibold">Location</th>
                                <th className="p-2 text-left font-semibold">Free Space</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white">
                            {storageLocations.length > 0 ? storageLocations.map(loc => (
                                <tr key={loc.drive} className="border-t border-gray-200">
                                    <td className="p-2 font-medium align-top">
                                        <div className="font-bold">{loc.drive}</div>
                                        <div className="pl-4">
                                            {loc.suggestedPaths.map(path => (
                                                <div key={path} className="flex items-center my-1">
                                                    <input
                                                        type="radio"
                                                        id={`path-${path.replace(/\\/g, '-')}`}
                                                        name="storagePath"
                                                        value={path}
                                                        checked={vmConfig.storagePath === path}
                                                        onChange={e => setVmConfig(c => ({ ...c, storagePath: e.target.value }))}
                                                        className="h-4 w-4 text-primary-600 border-gray-300 focus:ring-primary-500"
                                                        style={{ colorScheme: 'light' }}
                                                    />
                                                    <label htmlFor={`path-${path.replace(/\\/g, '-')}`} className="ml-2 font-mono text-xs cursor-pointer">{path}</label>
                                                </div>
                                            ))}
                                        </div>
                                    </td>
                                    <td className="p-2 align-top">{loc.freeSpaceGb.toFixed(2)} GB</td>
                                </tr>
                            )) : (
                                <tr><td colSpan={2} className="p-4 text-center text-gray-500">No suitable storage locations found for a {vmConfig.hardware.diskSize} GB disk.</td></tr>
                            )}
                        </tbody>
                    </table>
                </div>
            )}
            <CompatibilityCheck />
        </div>
    );
};

const Step6CustomizeHardware = ({ vmConfig, setVmConfig }: { vmConfig: typeof initialVmConfig; setVmConfig: React.Dispatch<React.SetStateAction<typeof initialVmConfig>> }) => {
    const updateHardware = (key: keyof typeof vmConfig.hardware, value: any) => {
        setVmConfig(c => ({ ...c, hardware: { ...c.hardware, [key]: value } }));
    };
    const inputClass = "w-full bg-white text-gray-900 border-gray-300 rounded-md shadow-sm py-1.5 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500";
    return (
        <div>
            <div className="flex justify-between items-center mb-4">
                <div>
                     <h3 className="font-bold text-xl text-gray-800">Customize hardware</h3>
                     <p className="text-sm text-gray-500 mt-1">Configure the virtual machine hardware</p>
                </div>
                <Button variant="ghost">ADD NEW DEVICE</Button>
            </div>
            <Tabs tabs={['Virtual Hardware', 'VM Options', 'Advanced Parameters']} activeTab="Virtual Hardware" onTabClick={() => {}} />
            <div className="mt-4 border-t border-gray-200">
                <AccordionItem title="CPU" summary={vmConfig.hardware.cpu} defaultOpen>
                    <div className="grid grid-cols-2 gap-4">
                        <label className="text-sm font-medium">CPU</label>
                        <input type="number" min="1" max="16" value={vmConfig.hardware.cpu} onChange={e => updateHardware('cpu', parseInt(e.target.value))} className={inputClass} style={{ colorScheme: 'light' }} />
                    </div>
                </AccordionItem>
                <AccordionItem title="Memory" summary={`${vmConfig.hardware.memory} GB`}>
                     <div className="grid grid-cols-2 gap-4">
                        <label className="text-sm font-medium">Memory</label>
                        <div className="flex items-center">
                          <input type="number" min="1" value={vmConfig.hardware.memory} onChange={e => updateHardware('memory', parseInt(e.target.value))} className={inputClass} style={{ colorScheme: 'light' }} />
                          <span className="ml-2 text-sm font-semibold">GB</span>
                        </div>
                    </div>
                </AccordionItem>
                <AccordionItem title="New Hard disk" summary={`${vmConfig.hardware.diskSize} GB`}>
                    <div className="grid grid-cols-2 gap-4">
                        <label className="text-sm font-medium">Disk Size</label>
                        <div className="flex items-center">
                            <input type="number" min="1" value={vmConfig.hardware.diskSize} onChange={e => updateHardware('diskSize', parseInt(e.target.value))} className={inputClass} style={{ colorScheme: 'light' }} />
                            <span className="ml-2 text-sm font-semibold">GB</span>
                        </div>
                    </div>
                </AccordionItem>
                 <AccordionItem title="New SCSI controller" summary="LSI Logic SAS"><p>Controller settings can be modified after creation.</p></AccordionItem>
                 <AccordionItem title="New Network" summary={vmConfig.hardware.network}>
                     <div className="grid grid-cols-2 gap-4">
                        <label className="text-sm font-medium">Network Adapter</label>
                        <select value={vmConfig.hardware.network} onChange={e => updateHardware('network', e.target.value)} className={inputClass}>
                            <option>host210-to-host212</option>
                            <option>Default Switch</option>
                            <option>Not Connected</option>
                        </select>
                    </div>
                 </AccordionItem>
                 <AccordionItem title="New CD/DVD Drive" summary="Client Device"><p>Media can be connected after creation.</p></AccordionItem>
                 <AccordionItem title="New USB Controller" summary="USB 3.2"><p>USB settings can be modified after creation.</p></AccordionItem>
            </div>
        </div>
    );
};

const SummaryRow = ({ label, value }: { label: string; value: React.ReactNode }) => (
    <div className="flex justify-between py-2 border-b border-gray-200">
        <dt className="text-sm text-gray-600">{label}</dt>
        <dd className="text-sm font-medium text-gray-800 text-right">{value}</dd>
    </div>
);

const Step7Summary = ({ vmConfig }: { vmConfig: typeof initialVmConfig }) => (
    <div>
        <h3 className="font-bold text-xl text-gray-800 mb-4">Ready to complete</h3>
        <dl>
            <SummaryRow label="Name" value={vmConfig.name} />
            <SummaryRow label="Location" value={vmConfig.folderPath} />
            <SummaryRow label="Compute Resource" value="Local Host" />
            <SummaryRow label="Storage Path" value={vmConfig.storagePath || 'Default'} />
            <SummaryRow label="Guest OS" value={vmConfig.guestOsVersion} />
            <SummaryRow label="Compatibility" value={vmConfig.compatibility} />
            <SummaryRow label="Secure Boot" value={vmConfig.secureBoot && vmConfig.compatibility === 'Generation 2' ? 'Enabled' : 'Disabled'} />
            <SummaryRow label="CPU" value={`${vmConfig.hardware.cpu} vCPU`} />
            <SummaryRow label="Memory" value={`${vmConfig.hardware.memory} GB`} />
            <SummaryRow label="Hard Disk" value={`${vmConfig.hardware.diskSize} GB`} />
            <SummaryRow label="VHD Path" value={vmConfig.storagePath ? `${vmConfig.storagePath}\\${vmConfig.name}.vhdx` : 'Default Path'} />
            <SummaryRow label="Network" value={vmConfig.hardware.network} />
        </dl>
    </div>
);

const StepContent = ({ step, vmConfig, setVmConfig }: { step: number; vmConfig: typeof initialVmConfig; setVmConfig: React.Dispatch<React.SetStateAction<typeof initialVmConfig>> }) => {
    const commonInputClass = "mt-1 block w-full bg-white border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-primary-500 focus:border-primary-500";
    switch (step) {
        case 1: return <Step1CreationType vmConfig={vmConfig} setVmConfig={setVmConfig} />;
        case 2: return <Step2NameAndFolder vmConfig={vmConfig} setVmConfig={setVmConfig} />;
        case 3: return <Step3Storage vmConfig={vmConfig} setVmConfig={setVmConfig} />;
        case 4: return (
            <div>
                <h3 className="font-bold text-xl text-gray-800">Select compatibility</h3>
                <p className="text-sm text-gray-500 mt-1 mb-4">Choose VM generation</p>
                <div className="mt-4">
                    <label className="block text-sm font-medium text-gray-700">VM Generation</label>
                    <select 
                        value={vmConfig.compatibility} 
                        onChange={e => {
                            const isGen2 = e.target.value === 'Generation 2';
                            setVmConfig(c => ({
                                ...c, 
                                compatibility: e.target.value,
                                secureBoot: isGen2 // Enable secure boot by default for Gen2, disable for Gen1
                            }))
                        }} 
                        className={commonInputClass}
                    >
                        <option>Generation 1</option>
                        <option>Generation 2</option>
                    </select>
                </div>
                <div className="mt-4 flex items-center">
                    <input 
                        id="secureBoot" 
                        type="checkbox"
                        className="h-4 w-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500"
                        style={{ colorScheme: 'light' }}
                        checked={vmConfig.secureBoot}
                        onChange={e => setVmConfig(c => ({...c, secureBoot: e.target.checked}))}
                        disabled={vmConfig.compatibility !== 'Generation 2'}
                    />
                    <label 
                        htmlFor="secureBoot" 
                        className={`ml-2 text-sm font-medium ${vmConfig.compatibility !== 'Generation 2' ? 'text-gray-400' : 'text-gray-700'}`}
                    >
                        Enable Secure Boot (requires Generation 2)
                    </label>
                </div>
            </div>
        );
        case 5: return <div><h3 className="font-bold text-xl text-gray-800">Select a guest OS</h3><p className="text-sm text-gray-500 mt-1 mb-4">Choose the guest operating system</p><label className="block text-sm font-medium text-gray-700">Guest OS</label><select value={vmConfig.guestOsVersion} onChange={e => setVmConfig(c => ({...c, guestOsVersion: e.target.value}))} className={commonInputClass}><option>Microsoft Windows Server 2022 (64-bit)</option><option>Microsoft Windows Server 2019 (64-bit)</option><option>Ubuntu Linux (64-bit)</option></select></div>;
        case 6: return <Step6CustomizeHardware vmConfig={vmConfig} setVmConfig={setVmConfig} />;
        case 7: return <Step7Summary vmConfig={vmConfig} />;
        default: return null;
    }
};

export const CreateVmWizard = ({ isOpen, onClose, onComplete }: { isOpen: boolean; onClose: () => void; onComplete: () => void; }) => {
    const { addNotification } = useOutletContext<OutletContextType>();
    const [currentStep, setCurrentStep] = useState(1);
    const [vmConfig, setVmConfig] = useState(initialVmConfig);
    const [isCreating, setIsCreating] = useState(false);

    const handleNext = () => {
        if (currentStep < steps.length) {
            setCurrentStep(currentStep + 1);
        }
    };
    const handleBack = () => {
        if (currentStep > 1) {
            setCurrentStep(currentStep - 1);
        }
    };
    
    const handleFinish = async () => {
        setIsCreating(true);
        try {
            const generation = vmConfig.compatibility === 'Generation 2' ? 2 : 1;
            // Construct the full VHD path. Use backslashes for Windows paths.
            const finalVhdPath = vmConfig.storagePath ? `${vmConfig.storagePath}\\${vmConfig.name}.vhdx` : undefined;

            const vmPayload = {
                id: `vm-${crypto.randomUUID()}`,
                name: vmConfig.name,
                mode: 'WMI', // Will be converted to 1 in vmService
                memoryMB: vmConfig.hardware.memory * 1024,
                cpuCount: vmConfig.hardware.cpu,
                diskSizeGB: vmConfig.hardware.diskSize,
                generation: generation,
                secureBoot: vmConfig.secureBoot && generation === 2,
                vhdPath: finalVhdPath,
                switchName: vmConfig.hardware.network,
                notes: `Guest OS: ${vmConfig.guestOsVersion}`
            };

            await api.vmService.createVm(vmPayload);

            addNotification('success', `VM '${vmConfig.name}' created successfully.`);
            onComplete();
        } catch (err: any) {
            addNotification('error', `Failed to create VM: ${err.message}`);
        } finally {
            setIsCreating(false);
        }
    };
    
    const isNextDisabled = useMemo(() => {
        if (currentStep === 2 && !vmConfig.name.trim()) return true;
        if (currentStep === 3 && !vmConfig.storagePath) return true;
        return false;
    }, [currentStep, vmConfig.name, vmConfig.storagePath]);

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-60 flex justify-center items-center z-40" aria-modal="true" role="dialog">
            <div className="bg-panel-bg w-full max-w-7xl h-[90vh] max-h-[850px] flex flex-col shadow-2xl rounded-md">
                <div className="flex-1 flex overflow-hidden">
                    <WizardStepper currentStep={currentStep} />
                    <div className="flex-1 flex flex-col p-8 overflow-y-auto relative">
                        <button onClick={onClose} className="absolute top-4 right-4 text-gray-500 hover:text-gray-800 z-50">
                            <CloseIcon className="h-6 w-6" />
                        </button>
                        <div className="flex-1">
                            <StepContent step={currentStep} vmConfig={vmConfig} setVmConfig={setVmConfig} />
                        </div>
                        <div className="mt-auto pt-6 border-t border-gray-200">
                             <div className="flex justify-between items-center">
                                <span className="text-sm text-gray-500">Compatibility: ESXi 8.0 U2 and later (VM version 21)</span>
                                <div className="flex space-x-2">
                                    <Button variant="ghost" onClick={onClose}>CANCEL</Button>
                                    <Button variant="secondary" onClick={handleBack} disabled={currentStep === 1}>BACK</Button>
                                    {currentStep < steps.length ? (
                                        <Button onClick={handleNext} disabled={isNextDisabled}>NEXT</Button>
                                    ) : (
                                        <Button onClick={handleFinish} disabled={isCreating || !vmConfig.name}>
                                            {isCreating ? <Spinner size="sm" /> : 'FINISH'}
                                        </Button>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};