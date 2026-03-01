import React from 'react';
import type { VirtualMachine } from '../../../types';
import { InfoCircleIcon } from '../../../components/icons/Icons';

export const VmConfigureTab: React.FC<{ vm: VirtualMachine }> = ({ vm }) => {
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-sm p-8 text-center">
      <InfoCircleIcon className="w-12 h-12 mx-auto text-gray-300 mb-4" />
      <h2 className="text-xl font-semibold text-gray-700">VM Configuration</h2>
      <p className="mt-2 text-gray-500">
        Hardware configuration for <span className="font-semibold">{vm.name}</span> is coming soon.
      </p>
      <p className="mt-1 text-gray-400 text-sm">This feature requires agent proxy support.</p>
    </div>
  );
};
