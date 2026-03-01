import React, { useState } from 'react';
import { XMarkIcon, DatacenterIcon } from './icons/Icons';

interface AddDatacenterModalProps {
  onClose: () => void;
  onCreate: (name: string, description?: string) => Promise<void>;
}

export const AddDatacenterModal: React.FC<AddDatacenterModalProps> = ({ onClose, onCreate }) => {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isCreating, setIsCreating] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsCreating(true);
    try {
      await onCreate(name, description || undefined);
    } finally {
      setIsCreating(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/30 backdrop-blur-sm flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-gray-50 rounded-lg shadow-xl w-full max-w-lg border border-gray-300" onClick={e => e.stopPropagation()}>
        <div className="flex justify-between items-center p-4 border-b border-gray-200 bg-white rounded-t-lg">
          <div className="flex items-center">
            <DatacenterIcon className="w-6 h-6 mr-3 text-blue-600" />
            <h2 className="text-lg font-semibold text-gray-800">Add Datacenter</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          <div className="space-y-5">
            <div>
              <label className="block text-sm font-medium text-gray-600 mb-1">Name</label>
              <input
                type="text"
                value={name}
                onChange={e => setName(e.target.value)}
                required
                className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none"
                placeholder="e.g., Primary Datacenter"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-600 mb-1">Description</label>
              <textarea
                value={description}
                onChange={e => setDescription(e.target.value)}
                rows={3}
                className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none resize-none"
                placeholder="Optional description"
              />
            </div>
          </div>

          <div className="mt-8 pt-4 flex justify-end space-x-3 border-t border-gray-200">
            <button type="button" onClick={onClose} disabled={isCreating} className="px-4 py-2 bg-white text-gray-700 rounded-sm border border-gray-300 hover:bg-gray-100 transition-colors text-sm font-semibold disabled:opacity-50">
              Cancel
            </button>
            <button type="submit" disabled={!name || isCreating} className="px-4 py-2 bg-blue-600 text-white rounded-sm hover:bg-blue-700 transition-colors text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed">
              {isCreating ? 'Creating...' : 'Create Datacenter'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
