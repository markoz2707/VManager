import React from 'react';
import { ClusterIcon } from '../../components/icons/Icons';

export const ClusterView: React.FC = () => {
  return (
    <div className="bg-white border border-gray-200 rounded p-6 text-center text-gray-500">
      <ClusterIcon className="w-12 h-12 mx-auto text-gray-300 mb-4" />
      <h2 className="text-xl font-semibold text-gray-700">Cluster View</h2>
      <p className="mt-2">Cluster management dashboard is coming soon.</p>
    </div>
  );
};
