import React, { useState, useEffect } from 'react';
import type { InfrastructureNode } from '../types';
import { ChevronRightIcon, DatacenterIcon, ClusterIcon, HostIcon, VmIcon, FolderIcon, ChevronDownIcon } from './icons/Icons';

export interface SidebarContextMenuEvent {
  x: number;
  y: number;
  nodeId: string | null;
  nodeType: InfrastructureNode['type'] | 'header';
  nodeName?: string;
}

interface TreeNodeProps {
  node: InfrastructureNode;
  onSelect: (id: string, type: InfrastructureNode['type']) => void;
  selectedId: string;
  onToggle: (id: string) => void;
  expandedIds: Set<string>;
  level: number;
  onContextMenu?: (event: SidebarContextMenuEvent) => void;
}

const TreeNode: React.FC<TreeNodeProps> = ({ node, onSelect, selectedId, onToggle, expandedIds, level, onContextMenu }) => {
  const isSelected = node.id === selectedId;
  const isExpanded = expandedIds.has(node.id);
  const hasChildren = node.children && node.children.length > 0;

  const getIcon = () => {
    switch (node.type) {
      case 'datacenter': return <DatacenterIcon className="w-4 h-4 mr-2" />;
      case 'cluster': return <ClusterIcon className="w-4 h-4 mr-2" />;
      case 'host': return <HostIcon className="w-4 h-4 mr-2" />;
      case 'vm': return <VmIcon className="w-4 h-4 mr-2" />;
      default: return <FolderIcon className="w-4 h-4 mr-2" />;
    }
  };

  const handleContextMenu = (e: React.MouseEvent) => {
    if (!onContextMenu) return;
    if (node.type === 'datacenter' || node.type === 'cluster') {
      e.preventDefault();
      e.stopPropagation();
      onContextMenu({
        x: e.clientX,
        y: e.clientY,
        nodeId: node.id,
        nodeType: node.type,
        nodeName: node.name,
      });
    }
  };

  return (
    <div>
      <div
        onClick={() => { onSelect(node.id, node.type); if (hasChildren && node.type !== 'vm') onToggle(node.id); }}
        onContextMenu={handleContextMenu}
        className={`flex items-center py-1 cursor-pointer rounded ${isSelected ? 'bg-blue-100 text-blue-800' : 'hover:bg-gray-100'}`}
        style={{ paddingLeft: `${level * 1.25}rem` }}
      >
        {hasChildren ? (
          <button onClick={(e) => { e.stopPropagation(); onToggle(node.id); }} className="mr-1 p-0.5 rounded hover:bg-gray-200">
            {isExpanded ? <ChevronDownIcon className="w-3 h-3" /> : <ChevronRightIcon className="w-3 h-3" />}
          </button>
        ) : <span className="w-4 mr-1"></span>}
        {getIcon()}
        <span className="text-sm">{node.name}</span>
      </div>
      {isExpanded && hasChildren && (
        <div>
          {node.children!.map(child => (
            <TreeNode key={child.id} node={child} onSelect={onSelect} selectedId={selectedId} onToggle={onToggle} expandedIds={expandedIds} level={level + 1} onContextMenu={onContextMenu} />
          ))}
        </div>
      )}
    </div>
  );
};

// Collect all non-VM node IDs for auto-expanding
function collectNodeIds(nodes: InfrastructureNode[]): string[] {
  const ids: string[] = [];
  for (const node of nodes) {
    if (node.type !== 'vm') {
      ids.push(node.id);
    }
    if (node.children) {
      ids.push(...collectNodeIds(node.children));
    }
  }
  return ids;
}

interface SidebarProps {
  tree: InfrastructureNode[];
  onSelect: (id: string, type: InfrastructureNode['type']) => void;
  selectedId: string;
  onContextMenu?: (event: SidebarContextMenuEvent) => void;
}

export const Sidebar: React.FC<SidebarProps> = ({ tree, onSelect, selectedId, onContextMenu }) => {
  const [expandedIds, setExpandedIds] = useState(new Set<string>());

  // Auto-expand all non-VM nodes when tree changes
  useEffect(() => {
    if (tree.length > 0) {
      setExpandedIds(new Set(collectNodeIds(tree)));
    }
  }, [tree]);

  const handleToggle = (id: string) => {
    setExpandedIds(prev => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  };

  const handleHeaderContextMenu = (e: React.MouseEvent) => {
    if (!onContextMenu) return;
    e.preventDefault();
    onContextMenu({
      x: e.clientX,
      y: e.clientY,
      nodeId: null,
      nodeType: 'header',
    });
  };

  return (
    <nav className="p-2 space-y-1">
      <div
        className="px-2 py-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wider select-none"
        onContextMenu={handleHeaderContextMenu}
      >
        Infrastructure
      </div>
      {tree.map(node => (
        <TreeNode key={node.id} node={node} onSelect={onSelect} selectedId={selectedId} onToggle={handleToggle} expandedIds={expandedIds} level={0} onContextMenu={onContextMenu} />
      ))}
    </nav>
  )
};
