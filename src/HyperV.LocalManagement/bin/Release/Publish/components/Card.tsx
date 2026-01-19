import React from 'react';

interface CardProps {
    children: React.ReactNode;
    className?: string;
    title?: string | React.ReactNode;
}

export const Card: React.FC<CardProps> = ({ children, className, title }) => {
    return (
        <div className={`bg-panel-bg shadow-sm border border-panel-border rounded-sm overflow-hidden ${className}`}>
            {title && (
                <div className="px-4 py-2 border-b border-panel-border bg-gray-50">
                    <h3 className="text-sm font-bold text-gray-700 uppercase tracking-wide">{title}</h3>
                </div>
            )}
            <div className="p-4">
                {children}
            </div>
        </div>
    );
};