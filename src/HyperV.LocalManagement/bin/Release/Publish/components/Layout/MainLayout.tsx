import React from 'react';
import { Outlet } from 'react-router-dom';
import { TreeView } from '../Navigation/TreeView';
import { Notification, NotificationType } from '../../types';
import { DashboardIcon, ServerIcon } from '../Icons';

interface MainLayoutProps {
    notifications: Notification[];
    onDismissNotification: (id: number) => void;
}

export const MainLayout: React.FC<MainLayoutProps> = ({ notifications, onDismissNotification }) => {

    const NotificationComponent: React.FC<{ notification: Notification; onDismiss: (id: number) => void }> = ({ notification, onDismiss }) => {
        const bgColors: Record<NotificationType, string> = {
            success: 'bg-green-600/90 border-green-500',
            error: 'bg-red-600/90 border-red-500',
            info: 'bg-blue-600/90 border-blue-500',
        };

        return (
            <div className={`
                p-3 rounded-lg text-white shadow-lg backdrop-blur-md border 
                flex justify-between items-center min-w-[300px] animate-fade-in
                ${bgColors[notification.type]}
            `}>
                <span className="text-sm font-medium">{notification.message}</span>
                <button
                    onClick={() => onDismiss(notification.id)}
                    className="ml-4 text-white/70 hover:text-white"
                >
                    ×
                </button>
            </div>
        );
    };

    return (
        <div className="flex flex-col h-screen bg-[var(--bg-app)] text-[var(--text-main)] overflow-hidden">
            {/* Header */}
            <header className="h-12 glass-header flex items-center px-4 justify-between z-20">
                <div className="flex items-center space-x-3">
                    <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center shadow-lg shadow-blue-500/20">
                        <ServerIcon className="w-5 h-5 text-white" />
                    </div>
                    <h1 className="text-lg font-bold tracking-tight bg-gradient-to-r from-white to-slate-400 bg-clip-text text-transparent">
                        Hyper-V Manager
                    </h1>
                </div>
                <div className="flex items-center space-x-4">
                    <div className="text-xs text-slate-400">
                        Connected to: <span className="text-green-400 font-mono ml-1">localhost</span>
                    </div>
                    <div className="w-8 h-8 rounded-full bg-slate-700 border border-slate-600 flex items-center justify-center">
                        <span className="text-xs font-bold">AD</span>
                    </div>
                </div>
            </header>

            {/* Main Content Area */}
            <div className="flex flex-1 overflow-hidden">
                {/* Sidebar Navigation */}
                <TreeView />

                {/* Page Content */}
                <main className="flex-1 overflow-hidden flex flex-col relative">
                    {/* Notifications Overlay */}
                    <div className="absolute top-4 right-4 z-50 space-y-2 pointer-events-none">
                        <div className="pointer-events-auto space-y-2">
                            {notifications.map(n => (
                                <NotificationComponent key={n.id} notification={n} onDismiss={onDismissNotification} />
                            ))}
                        </div>
                    </div>

                    {/* Scrollable Content */}
                    <div className="flex-1 overflow-y-auto p-6">
                        <Outlet />
                    </div>
                </main>
            </div>

            {/* Status Bar */}
            <footer className="h-6 bg-slate-900 border-t border-slate-800 flex items-center px-4 text-[10px] text-slate-500 justify-between select-none">
                <div className="flex space-x-4">
                    <span>Ready</span>
                    <span>User: Administrator</span>
                </div>
                <div className="flex space-x-4">
                    <span>v1.0.0</span>
                    <span>Latency: &lt;1ms</span>
                </div>
            </footer>
        </div>
    );
};
