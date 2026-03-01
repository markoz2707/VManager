import React, { useState, useEffect } from 'react';
import type { VirtualMachine, VmPerformanceDataPoint } from '../../../types';
import * as metricsService from '../../../services/metricsService';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { ChevronDownIcon, ChevronRightIcon, MoreVerticalIcon } from '../../../components/icons/Icons';

const Card: React.FC<{ title: string; children: React.ReactNode; className?: string }> = ({ title, children, className }) => (
  <div className={`bg-white border border-gray-200 rounded ${className}`}>
    <div className="flex justify-between items-center p-3 border-b border-gray-200">
      <h4 className="text-sm font-semibold">{title}</h4>
      <button className="text-gray-400 hover:text-gray-600">
        <MoreVerticalIcon className="w-4 h-4" />
      </button>
    </div>
    <div className="p-3 text-sm">{children}</div>
  </div>
);

const PerformanceSidebar: React.FC = () => {
  const [expanded, setExpanded] = useState({
    issues: false,
    performance: true,
    tasks: false,
  });

  const toggle = (key: keyof typeof expanded) => {
    setExpanded(prev => ({ ...prev, [key]: !prev[key] }));
  };

  const NavItem: React.FC<{ label: string; active?: boolean; level?: number }> = ({ label, active, level = 0 }) => (
    <a href="#" className={`block text-sm py-1.5 px-3 rounded ${active ? 'bg-blue-100 text-blue-800 font-semibold' : 'hover:bg-gray-100'}`} style={{ paddingLeft: `${0.75 + level * 1}rem` }}>{label}</a>
  );

  const NavGroup: React.FC<{ label: string; children: React.ReactNode; isOpen: boolean; onToggle: () => void }> = ({ label, children, isOpen, onToggle }) => (
    <div>
      <button onClick={onToggle} className="w-full flex items-center text-left text-sm font-semibold py-1.5 px-2 hover:bg-gray-100 rounded">
        {isOpen ? <ChevronDownIcon className="w-4 h-4 mr-1"/> : <ChevronRightIcon className="w-4 h-4 mr-1"/>}
        {label}
      </button>
      {isOpen && <div className="mt-1">{children}</div>}
    </div>
  );

  return (
    <aside className="w-64 flex-shrink-0 bg-white border border-gray-200 rounded p-2">
      <NavGroup label="Alarms" isOpen={expanded.issues} onToggle={() => toggle('issues')}>
        <NavItem label="All Issues" level={1} />
        <NavItem label="Triggered Alarms" level={1} />
      </NavGroup>
      <NavGroup label="Performance" isOpen={expanded.performance} onToggle={() => toggle('performance')}>
        <NavItem label="Overview" active level={1} />
        <NavItem label="Advanced" level={1} />
      </NavGroup>
      <NavGroup label="Events" isOpen={expanded.tasks} onToggle={() => toggle('tasks')}>
        <NavItem label="Tasks" level={1} />
        <NavItem label="Events" level={1} />
        <NavItem label="Utilization" level={1} />
      </NavGroup>
    </aside>
  );
};

export const VmMonitorTab: React.FC<{ vm: VirtualMachine }> = ({ vm }) => {
    const [data, setData] = useState<VmPerformanceDataPoint[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [timeRange, setTimeRange] = useState('');

    useEffect(() => {
        const fetchData = async () => {
            if (!vm) return;
            setIsLoading(true);
            try {
                const result = await metricsService.getVmPerformanceOverview(vm.id);
                setData(result);
                if (result.length > 0) {
                    const now = new Date();
                    const startTime = new Date(now.getTime() - 60 * 60 * 1000);
                    const options: Intl.DateTimeFormatOptions = { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' };
                    setTimeRange(`${startTime.toLocaleDateString([], options)} - ${now.toLocaleDateString([], options)}`);
                }
            } catch (error) {
                console.error("Failed to fetch performance data", error);
            } finally {
                setIsLoading(false);
            }
        };
        fetchData();
    }, [vm.id]);

    if (isLoading) {
        return <div className="text-center py-16 text-gray-500">Loading performance data...</div>;
    }

    if (!data.length) {
        return <div className="text-center py-16 text-gray-500">No performance data available.</div>;
    }

    const legendStyle = { fontSize: '12px', marginLeft: '10px' };
    const tickStyle = { fontSize: 10 };

    return (
        <div className="flex space-x-4">
            <PerformanceSidebar />
            <div className="flex-grow space-y-4">
                <div className="bg-white border border-gray-200 rounded p-3">
                    <h3 className="text-lg font-semibold text-gray-800">Performance Overview</h3>
                    <div className="flex items-center space-x-4 text-sm mt-2">
                        <div>
                            <span className="text-gray-500">Period: </span>
                            <select className="font-semibold bg-transparent border-none focus:ring-0 p-0">
                                <option>Real-time</option>
                            </select>
                        </div>
                        <div className="font-semibold">{timeRange}</div>
                    </div>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                    <Card title="CPU">
                        <ResponsiveContainer width="100%" height={250}>
                            <LineChart data={data} margin={{ top: 5, right: 20, left: -10, bottom: 5 }}>
                                <CartesianGrid strokeDasharray="3 3" />
                                <XAxis dataKey="time" tick={tickStyle} />
                                <YAxis yAxisId="left" label={{ value: '%', angle: -90, position: 'insideLeft', fontSize: 12, dy: -10 }} tick={tickStyle} domain={[0, 100]}/>
                                <YAxis yAxisId="right" orientation="right" label={{ value: 'MHz', angle: -90, position: 'insideRight', fontSize: 12, dy: 10 }} tick={tickStyle} />
                                <Tooltip contentStyle={{fontSize: "12px"}}/>
                                <Legend wrapperStyle={legendStyle} />
                                <Line yAxisId="left" type="monotone" dataKey="cpuUsage" name={`Usage for ${vm.name}`} stroke="#8884d8" dot={false} strokeWidth={2}/>
                                <Line yAxisId="left" type="monotone" dataKey="cpuReady" name={`Ready for ${vm.name}`} stroke="#82ca9d" dot={false} strokeWidth={2}/>
                                <Line yAxisId="right" type="monotone" dataKey="cpuUsageMhz" name="Usage in MHz" stroke="#d0028a" dot={false} strokeWidth={2}/>
                            </LineChart>
                        </ResponsiveContainer>
                    </Card>
                    <Card title="Memory">
                        <ResponsiveContainer width="100%" height={250}>
                             <LineChart data={data} margin={{ top: 5, right: 20, left: -10, bottom: 5 }}>
                                <CartesianGrid strokeDasharray="3 3" />
                                <XAxis dataKey="time" tick={tickStyle} />
                                <YAxis label={{ value: 'KB', angle: -90, position: 'insideLeft', fontSize: 12, dy: -10 }} tick={tickStyle} tickFormatter={(val) => `${(val/1000).toFixed(0)}K`} />
                                <Tooltip contentStyle={{fontSize: "12px"}} formatter={(value: number | undefined) => value != null ? `${(value/1000).toFixed(2)} K` : '—'}/>
                                <Legend wrapperStyle={legendStyle} />
                                <Line type="monotone" dataKey="memActive" name="Active" stroke="#82ca9d" dot={false} strokeWidth={2}/>
                                <Line type="monotone" dataKey="memConsumed" name="Consumed" stroke="#3b82f6" dot={false} strokeWidth={2}/>
                                <Line type="monotone" dataKey="memGranted" name="Granted" stroke="#8884d8" dot={false} strokeWidth={2}/>
                                <Line type="monotone" dataKey="memBalloon" name="Balloon" stroke="#d0028a" dot={false} strokeWidth={2}/>
                            </LineChart>
                        </ResponsiveContainer>
                    </Card>
                    <Card title="Memory Rate">
                        <ResponsiveContainer width="100%" height={250}>
                            <LineChart data={data} margin={{ top: 5, right: 20, left: -10, bottom: 5 }}>
                                <CartesianGrid strokeDasharray="3 3" />
                                <XAxis dataKey="time" tick={tickStyle} />
                                <YAxis label={{ value: 'KBps', angle: -90, position: 'insideLeft', fontSize: 12, dy: -10 }} tick={tickStyle} />
                                <Tooltip contentStyle={{fontSize: "12px"}}/>
                                <Legend wrapperStyle={legendStyle} />
                                <Line type="monotone" dataKey="memSwapInRate" name="Swap in rate" stroke="#82ca9d" dot={false} strokeWidth={2}/>
                                <Line type="monotone" dataKey="memSwapOutRate" name="Swap out rate" stroke="#8884d8" dot={false} strokeWidth={2}/>
                            </LineChart>
                        </ResponsiveContainer>
                    </Card>
                    <Card title="Disk">
                       <ResponsiveContainer width="100%" height={250}>
                             <LineChart data={data} margin={{ top: 5, right: 20, left: -10, bottom: 5 }}>
                                <CartesianGrid strokeDasharray="3 3" />
                                <XAxis dataKey="time" tick={tickStyle} />
                                <YAxis yAxisId="left" label={{ value: 'ms', angle: -90, position: 'insideLeft', fontSize: 12, dy: -10 }} tick={tickStyle} />
                                <YAxis yAxisId="right" orientation="right" label={{ value: 'KBps', angle: -90, position: 'insideRight', fontSize: 12, dy: 10 }} tick={tickStyle} />
                                <Tooltip contentStyle={{fontSize: "12px"}}/>
                                <Legend wrapperStyle={legendStyle} />
                                <Line yAxisId="left" type="monotone" dataKey="diskHighestLatency" name="Highest latency" stroke="#82ca9d" dot={false} strokeWidth={2}/>
                                <Line yAxisId="right" type="monotone" dataKey="diskUsageRate" name={`Usage for ${vm.name}`} stroke="#8884d8" dot={false} strokeWidth={2}/>
                            </LineChart>
                        </ResponsiveContainer>
                    </Card>
                </div>
            </div>
        </div>
    );
};
