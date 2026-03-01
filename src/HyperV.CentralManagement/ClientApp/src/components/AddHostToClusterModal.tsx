import React, { useState, useMemo } from 'react';
import { XMarkIcon, ClusterIcon } from './icons/Icons';
import type { Host } from '../types';

interface AddHostToClusterModalProps {
  clusterId: string;
  clusterName: string;
  allHosts: Host[];
  clusteredHostIds: Set<string>;
  onClose: () => void;
  onAdd: (agentHostId: string) => Promise<void>;
}

export const AddHostToClusterModal: React.FC<AddHostToClusterModalProps> = ({
  clusterName,
  allHosts,
  clusteredHostIds,
  onClose,
  onAdd,
}) => {
  const [search, setSearch] = useState('');
  const [selectedHostId, setSelectedHostId] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);

  const availableHosts = useMemo(() => {
    return allHosts.filter(h => !clusteredHostIds.has(h.id));
  }, [allHosts, clusteredHostIds]);

  const filteredHosts = useMemo(() => {
    if (!search) return availableHosts;
    const lower = search.toLowerCase();
    return availableHosts.filter(h => h.name.toLowerCase().includes(lower));
  }, [availableHosts, search]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedHostId) return;
    setIsAdding(true);
    try {
      await onAdd(selectedHostId);
    } finally {
      setIsAdding(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/30 backdrop-blur-sm flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-gray-50 rounded-lg shadow-xl w-full max-w-lg border border-gray-300" onClick={e => e.stopPropagation()}>
        <div className="flex justify-between items-center p-4 border-b border-gray-200 bg-white rounded-t-lg">
          <div className="flex items-center">
            <ClusterIcon className="w-6 h-6 mr-3 text-blue-600" />
            <h2 className="text-lg font-semibold text-gray-800">Add Host to {clusterName}</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          <div className="space-y-4">
            <div>
              <input
                type="text"
                value={search}
                onChange={e => setSearch(e.target.value)}
                className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none"
                placeholder="Search hosts..."
              />
            </div>

            <div className="max-h-60 overflow-y-auto border border-gray-200 rounded bg-white">
              {filteredHosts.length === 0 ? (
                <div className="p-4 text-sm text-gray-500 text-center">
                  {availableHosts.length === 0 ? 'No unassigned hosts available.' : 'No hosts match your search.'}
                </div>
              ) : (
                filteredHosts.map(host => (
                  <label
                    key={host.id}
                    className={`flex items-center px-3 py-2 cursor-pointer hover:bg-blue-50 border-b border-gray-100 last:border-b-0 ${selectedHostId === host.id ? 'bg-blue-50' : ''}`}
                  >
                    <input
                      type="radio"
                      name="host"
                      value={host.id}
                      checked={selectedHostId === host.id}
                      onChange={() => setSelectedHostId(host.id)}
                      className="mr-3 accent-blue-600"
                    />
                    <div>
                      <span className="text-sm font-medium text-gray-800">{host.name}</span>
                      <span className="text-xs text-gray-500 ml-2">({host.state})</span>
                    </div>
                  </label>
                ))
              )}
            </div>
          </div>

          <div className="mt-6 pt-4 flex justify-end space-x-3 border-t border-gray-200">
            <button type="button" onClick={onClose} disabled={isAdding} className="px-4 py-2 bg-white text-gray-700 rounded-sm border border-gray-300 hover:bg-gray-100 transition-colors text-sm font-semibold disabled:opacity-50">
              Cancel
            </button>
            <button type="submit" disabled={!selectedHostId || isAdding} className="px-4 py-2 bg-blue-600 text-white rounded-sm hover:bg-blue-700 transition-colors text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed">
              {isAdding ? 'Adding...' : 'Add to Cluster'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
