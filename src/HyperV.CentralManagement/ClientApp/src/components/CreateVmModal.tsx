import React, { useState } from 'react';
import { XMarkIcon, ServerStackIcon } from './icons/Icons';
import type { VirtualMachine } from '../types';

interface CreateVmModalProps {
  onClose: () => void;
  onCreate: (name: string, os: VirtualMachine['os'], cpuCores: number, memoryGB: number, diskSizeGB: number) => Promise<void>;
}

export const CreateVmModal: React.FC<CreateVmModalProps> = ({ onClose, onCreate }) => {
  const [name, setName] = useState('');
  const [os, setOs] = useState<VirtualMachine['os']>('windows');
  const [cpuCores, setCpuCores] = useState(2);
  const [memoryGB, setMemoryGB] = useState(4);
  const [diskSizeGB, setDiskSizeGB] = useState(128);
  const [isCreating, setIsCreating] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsCreating(true);
    await onCreate(name, os, cpuCores, memoryGB, diskSizeGB);
    setIsCreating(false);
  };

  const FormRow: React.FC<{label: string, children: React.ReactNode}> = ({label, children}) => (
    <div>
      <label className="block text-sm font-medium text-gray-600 mb-1">{label}</label>
      {children}
    </div>
  );

  return (
    <div className="fixed inset-0 bg-black/30 backdrop-blur-sm flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-gray-50 rounded-lg shadow-xl w-full max-w-lg border border-gray-300" onClick={e => e.stopPropagation()}>
        <div className="flex justify-between items-center p-4 border-b border-gray-200 bg-white rounded-t-lg">
            <div className="flex items-center">
                <ServerStackIcon className="w-6 h-6 mr-3 text-blue-600"/>
                <h2 className="text-lg font-semibold text-gray-800">Create New Virtual Machine</h2>
            </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          <div className="space-y-5">
            <FormRow label="VM Name">
              <input type="text" value={name} onChange={e => setName(e.target.value)} required className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none" placeholder="e.g., WebServer-Dev"/>
            </FormRow>

            <FormRow label="Operating System">
              <select value={os} onChange={e => setOs(e.target.value as VirtualMachine['os'])} className="w-full bg-white border border-gray-300 rounded-sm p-2 text-sm text-gray-900 focus:ring-1 focus:ring-blue-500 focus:border-blue-500 outline-none">
                <option value="windows">Windows Server 2022</option>
                <option value="linux">Ubuntu Server 22.04</option>
                <option value="linux-other">Other Linux</option>
                <option value="windows-other">Other Windows</option>
              </select>
            </FormRow>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-2">
                <FormRow label={`CPU Cores: ${cpuCores}`}>
                     <input type="range" min="1" max="16" value={cpuCores} onChange={e => setCpuCores(Number(e.target.value))} className="w-full h-1.5 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-blue-600" />
                </FormRow>
                <FormRow label={`Memory: ${memoryGB} GB`}>
                    <input type="range" min="1" max="64" step="1" value={memoryGB} onChange={e => setMemoryGB(Number(e.target.value))} className="w-full h-1.5 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-blue-600" />
                </FormRow>
                 <FormRow label={`Disk: ${diskSizeGB} GB`}>
                     <input type="range" min="20" max="1024" step="4" value={diskSizeGB} onChange={e => setDiskSizeGB(Number(e.target.value))} className="w-full h-1.5 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-blue-600" />
                </FormRow>
            </div>
          </div>

          <div className="mt-8 pt-4 flex justify-end space-x-3 border-t border-gray-200">
            <button type="button" onClick={onClose} disabled={isCreating} className="px-4 py-2 bg-white text-gray-700 rounded-sm border border-gray-300 hover:bg-gray-100 transition-colors text-sm font-semibold disabled:opacity-50">
              Cancel
            </button>
            <button type="submit" disabled={!name || isCreating} className="px-4 py-2 bg-blue-600 text-white rounded-sm hover:bg-blue-700 transition-colors text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed">
              {isCreating ? 'Creating...' : 'Create VM'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
