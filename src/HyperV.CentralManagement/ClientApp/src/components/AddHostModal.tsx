import React, { useState } from 'react';
import { XMarkIcon, HostIcon } from './icons/Icons';

interface AddHostModalProps {
  datacenterId?: string;
  onClose: () => void;
  onCreate: (hostname: string, apiBaseUrl: string, hostType: string) => Promise<void>;
}

export const AddHostModal: React.FC<AddHostModalProps> = ({ onClose, onCreate }) => {
  const [hostname, setHostname] = useState('');
  const [apiBaseUrl, setApiBaseUrl] = useState('');
  const [hostType, setHostType] = useState('Hyper-V');
  const [isCreating, setIsCreating] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsCreating(true);
    try {
      await onCreate(hostname, apiBaseUrl, hostType);
    } finally {
      setIsCreating(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/30 backdrop-blur-sm flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-gray-50 rounded-lg shadow-xl w-full max-w-lg border border-gray-300" onClick={e => e.stopPropagation()}>
        <div className="flex justify-between items-center p-4 border-b border-gray-200 bg-white rounded-t-lg">
          <div className="flex items-center">
            <HostIcon className="w-6 h-6 mr-3 text-blue-600" />
            <h2 className="text-lg font-semibold text-gray-800">Add Host</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          <div className="space-y-5">
            <div>
              <label className="block text-sm font-medium text-gray-600 mb-1">Hostname</label>
              <input
                type="text"
                value={hostname}
                onChange={e => setHostname(e.target.value)}
                required
                className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none"
                placeholder="e.g., hyperv-node01"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-600 mb-1">API Base URL</label>
              <input
                type="url"
                value={apiBaseUrl}
                onChange={e => setApiBaseUrl(e.target.value)}
                required
                className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none"
                placeholder="https://hostname:8743"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-600 mb-1">Host Type</label>
              <select
                value={hostType}
                onChange={e => setHostType(e.target.value)}
                className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none"
              >
                <option value="Hyper-V">Hyper-V</option>
                <option value="KVM">KVM</option>
              </select>
            </div>
          </div>

          <div className="mt-8 pt-4 flex justify-end space-x-3 border-t border-gray-200">
            <button type="button" onClick={onClose} disabled={isCreating} className="px-4 py-2 bg-white text-gray-700 rounded-sm border border-gray-300 hover:bg-gray-100 transition-colors text-sm font-semibold disabled:opacity-50">
              Cancel
            </button>
            <button type="submit" disabled={!hostname || !apiBaseUrl || isCreating} className="px-4 py-2 bg-blue-600 text-white rounded-sm hover:bg-blue-700 transition-colors text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed">
              {isCreating ? 'Adding...' : 'Add Host'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
