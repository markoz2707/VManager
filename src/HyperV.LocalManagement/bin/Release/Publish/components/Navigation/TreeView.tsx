import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
    ChevronRightIcon,
    ChevronDownIcon,
    DatacenterIcon,
    ServerIcon,
    VmIcon,
    NetworkIcon,
    StorageIcon,
    ContainerIcon,
    FolderIcon
} from '../Icons';

export type TreeNodeType = 'datacenter' | 'host' | 'folder' | 'vm' | 'network' | 'storage' | 'container';

export interface TreeNode {
    id: string;
    label: string;
    type: TreeNodeType;
    path?: string;
    children?: TreeNode[];
    expanded?: boolean;
}

const TreeNodeItem: React.FC<{
    node: TreeNode;
    level: number;
    onToggle: (id: string) => void;
    onSelect: (node: TreeNode) => void;
    selectedId?: string;
}> = ({ node, level, onToggle, onSelect, selectedId }) => {
    const hasChildren = node.children && node.children.length > 0;
    const isSelected = node.id === selectedId;

    const getIcon = (type: TreeNodeType) => {
        const className = "w-4 h-4 mr-2 text-gray-400";
        switch (type) {
            case 'datacenter': return <DatacenterIcon className={className} />;
            case 'host': return <ServerIcon className={className} />;
            case 'folder': return <FolderIcon className={className} />;
            case 'vm': return <VmIcon className={className} />;
            case 'network': return <NetworkIcon className={className} />;
            case 'storage': return <StorageIcon className={className} />;
            case 'container': return <ContainerIcon className={className} />;
            default: return <FolderIcon className={className} />;
        }
    };

    return (
        <div>
            <div
                className={`
                    flex items-center py-1 px-2 cursor-pointer select-none transition-colors duration-150
                    ${isSelected ? 'bg-blue-600/30 text-white border-l-2 border-blue-500' : 'text-gray-300 hover:bg-slate-800/50'}
                `}
                style={{ paddingLeft: `${level * 1.5}rem` }}
                onClick={() => onSelect(node)}
            >
                <div
                    className="p-1 mr-1 rounded hover:bg-slate-700/50"
                    onClick={(e) => {
                        if (hasChildren) {
                            e.stopPropagation();
                            onToggle(node.id);
                        }
                    }}
                >
                    {hasChildren ? (
                        node.expanded ?
                            <ChevronDownIcon className="w-3 h-3 text-gray-500" /> :
                            <ChevronRightIcon className="w-3 h-3 text-gray-500" />
                    ) : <div className="w-3 h-3" />}
                </div>

                {getIcon(node.type)}
                <span className="text-sm truncate">{node.label}</span>
            </div>

            {node.expanded && node.children && (
                <div>
                    {node.children.map(child => (
                        <TreeNodeItem
                            key={child.id}
                            node={child}
                            level={level + 1}
                            onToggle={onToggle}
                            onSelect={onSelect}
                            selectedId={selectedId}
                        />
                    ))}
                </div>
            )}
        </div>
    );
};

export const TreeView: React.FC = () => {
    const navigate = useNavigate();
    const location = useLocation();

    // Initial Tree Structure (Mock Data for now, will be dynamic later)
    const [treeData, setTreeData] = useState<TreeNode[]>([
        {
            id: 'dc-local',
            label: 'Local Datacenter',
            type: 'datacenter',
            expanded: true,
            children: [
                {
                    id: 'host-local',
                    label: 'localhost',
                    type: 'host',
                    path: '/',
                    expanded: true,
                    children: [
                        {
                            id: 'folder-vms',
                            label: 'Virtual Machines',
                            type: 'folder',
                            expanded: true,
                            children: [
                                { id: 'vm-1', label: 'Windows 11 Dev', type: 'vm', path: '/vms/vm-1' },
                                { id: 'vm-2', label: 'Ubuntu Server', type: 'vm', path: '/vms/vm-2' }
                            ]
                        },
                        {
                            id: 'folder-containers',
                            label: 'Containers',
                            type: 'folder',
                            expanded: false,
                            children: [
                                { id: 'cnt-1', label: 'nginx-proxy', type: 'container', path: '/containers/cnt-1' }
                            ]
                        },
                        {
                            id: 'folder-networks',
                            label: 'Networks',
                            type: 'folder',
                            expanded: false,
                            children: [
                                { id: 'net-nat', label: 'Default Switch', type: 'network', path: '/networking/net-nat' }
                            ]
                        },
                        {
                            id: 'folder-storage',
                            label: 'Storage',
                            type: 'folder',
                            expanded: false,
                            children: [
                                { id: 'store-local', label: 'Local Disk (C:)', type: 'storage', path: '/storage/store-local' }
                            ]
                        }
                    ]
                }
            ]
        }
    ]);

    const [selectedId, setSelectedId] = useState<string>('host-local');

    const handleToggle = (id: string) => {
        const toggleNode = (nodes: TreeNode[]): TreeNode[] => {
            return nodes.map(node => {
                if (node.id === id) {
                    return { ...node, expanded: !node.expanded };
                }
                if (node.children) {
                    return { ...node, children: toggleNode(node.children) };
                }
                return node;
            });
        };
        setTreeData(toggleNode(treeData));
    };

    const handleSelect = (node: TreeNode) => {
        setSelectedId(node.id);
        if (node.path) {
            navigate(node.path);
        } else if (node.type === 'folder') {
            // Navigate to list view based on folder type
            if (node.id === 'folder-vms') navigate('/vms');
            if (node.id === 'folder-containers') navigate('/containers');
            if (node.id === 'folder-networks') navigate('/networking');
            if (node.id === 'folder-storage') navigate('/storage');
        }
    };

    return (
        <div className="w-64 bg-slate-900 border-r border-slate-800 h-full flex flex-col">
            <div className="p-3 border-b border-slate-800">
                <h2 className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Navigator</h2>
            </div>
            <div className="flex-1 overflow-y-auto py-2">
                {treeData.map(node => (
                    <TreeNodeItem
                        key={node.id}
                        node={node}
                        level={0}
                        onToggle={handleToggle}
                        onSelect={handleSelect}
                        selectedId={selectedId}
                    />
                ))}
            </div>
        </div>
    );
};
