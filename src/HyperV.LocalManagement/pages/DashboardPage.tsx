
import React, { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { getDashboardStats } from '../services/hypervService';
import { VmIcon, NetworkIcon, ChipIcon } from '../components/Icons';
import { OutletContextType } from '../App';

interface Stats {
  runningVms: number;
  totalVms: number;
  totalNetworks: number;
  totalMemoryAssigned: number;
}

export const DashboardPage = () => {
  const [stats, setStats] = useState<Stats | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { addNotification } = useOutletContext<OutletContextType>();

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const data = await getDashboardStats();
        setStats(data);
      } catch (err: any) {
        addNotification('error', `Failed to load dashboard stats: ${err.message}`);
      } finally {
        setIsLoading(false);
      }
    };
    fetchStats();
  }, [addNotification]);

  if (isLoading) {
    return <div className="flex justify-center items-center h-full"><Spinner /></div>;
  }
  
  if(!stats) {
      return <div className="p-6 text-center text-gray-400">Could not load dashboard stats.</div>
  }

  return (
    <div className="p-6">
        <h1 className="text-3xl font-bold mb-6 text-white">Dashboard Overview</h1>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            <Card className="border-t-4 border-green-500">
                <div className="flex items-center">
                    <VmIcon className="h-12 w-12 text-green-400"/>
                    <div className="ml-4">
                        <p className="text-lg text-gray-400">Running VMs</p>
                        <p className="text-3xl font-bold text-white">{stats.runningVms} / {stats.totalVms}</p>
                    </div>
                </div>
            </Card>
            <Card className="border-t-4 border-blue-500">
                 <div className="flex items-center">
                    <NetworkIcon className="h-12 w-12 text-blue-400"/>
                    <div className="ml-4">
                        <p className="text-lg text-gray-400">Virtual Networks</p>
                        <p className="text-3xl font-bold text-white">{stats.totalNetworks}</p>
                    </div>
                </div>
            </Card>
            <Card className="border-t-4 border-purple-500">
                <div className="flex items-center">
                    <ChipIcon className="h-12 w-12 text-purple-400"/>
                    <div className="ml-4">
                        <p className="text-lg text-gray-400">Assigned Memory</p>
                        <p className="text-3xl font-bold text-white">{(stats.totalMemoryAssigned / 1024).toFixed(1)} GB</p>
                    </div>
                </div>
            </Card>
        </div>
    </div>
  );
};
