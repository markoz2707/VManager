import React from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { MoreVerticalIcon } from '../../../components/icons/Icons';

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

export const HostMonitorTab: React.FC<{ data: any[] }> = ({ data }) => {
    return (
        <Card title="Performance (Last Hour)">
            <div style={{ width: '100%', height: 300 }}>
                <ResponsiveContainer>
                    <LineChart data={data} margin={{ top: 5, right: 20, left: -10, bottom: 5 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e0e0e0" />
                        <XAxis dataKey="time" tick={{ fontSize: 12 }} />
                        <YAxis tickFormatter={(value) => `${value}%`} domain={[0, 100]} tick={{ fontSize: 12 }} />
                        <Tooltip />
                        <Legend wrapperStyle={{fontSize: "14px"}} />
                        <Line type="monotone" dataKey="cpu" name="CPU Usage (%)" stroke="#3b82f6" strokeWidth={2} dot={{ r: 2 }} activeDot={{ r: 6 }} />
                        <Line type="monotone" dataKey="memory" name="Memory Usage (%)" stroke="#10b981" strokeWidth={2} dot={{ r: 2 }} activeDot={{ r: 6 }} />
                    </LineChart>
                </ResponsiveContainer>
            </div>
        </Card>
    );
};
