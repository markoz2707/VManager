import React from 'react';

interface GaugeProps {
    value: number; // 0 to 100
    label: string;
    subLabel?: string;
    color?: string;
    size?: number;
}

export const Gauge: React.FC<GaugeProps> = ({ value, label, subLabel, color = '#3b82f6', size = 120 }) => {
    const strokeWidth = 10;
    const radius = (size - strokeWidth) / 2;
    const circumference = radius * 2 * Math.PI;
    const offset = circumference - (value / 100) * circumference;

    return (
        <div className="flex flex-col items-center justify-center">
            <div className="relative" style={{ width: size, height: size }}>
                {/* Background Circle */}
                <svg className="transform -rotate-90 w-full h-full">
                    <circle
                        className="text-slate-700"
                        strokeWidth={strokeWidth}
                        stroke="currentColor"
                        fill="transparent"
                        r={radius}
                        cx={size / 2}
                        cy={size / 2}
                    />
                    {/* Progress Circle */}
                    <circle
                        className="transition-all duration-1000 ease-out"
                        strokeWidth={strokeWidth}
                        strokeDasharray={circumference}
                        strokeDashoffset={offset}
                        strokeLinecap="round"
                        stroke={color}
                        fill="transparent"
                        r={radius}
                        cx={size / 2}
                        cy={size / 2}
                    />
                </svg>
                <div className="absolute top-0 left-0 w-full h-full flex flex-col items-center justify-center">
                    <span className="text-2xl font-bold text-white">{Math.round(value)}%</span>
                </div>
            </div>
            <div className="mt-2 text-center">
                <div className="text-sm font-medium text-slate-300">{label}</div>
                {subLabel && <div className="text-xs text-slate-500">{subLabel}</div>}
            </div>
        </div>
    );
};
