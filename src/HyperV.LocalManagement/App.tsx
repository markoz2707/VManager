import React, { useState, useCallback, useEffect } from 'react';
import { MemoryRouter, Routes, Route, Outlet } from 'react-router-dom';
import { SideNav } from './components/SideNav';
import  Footer  from './components/Footer';
import { DashboardPage } from './pages/DashboardPage';
import { VirtualMachinesPage } from './pages/VirtualMachinesPage';
import { ContainersPage } from './pages/ContainersPage';
import { NetworkingPage } from './pages/NetworkingPage';
import { StoragePage } from './pages/StoragePage';
import { HypervisorPage } from './pages/HypervisorPage';
import { Notification, NotificationType } from './types';

export type OutletContextType = {
  addNotification: (type: NotificationType, message: string) => void;
};

const NotificationComponent: React.FC<{ notification: Notification; onDismiss: (id: number) => void }> = ({ notification, onDismiss }) => {
    useEffect(() => {
        const timer = setTimeout(() => {
            onDismiss(notification.id);
        }, 5000);
        return () => clearTimeout(timer);
    }, [notification, onDismiss]);

    const bgColors: Record<NotificationType, string> = {
        success: 'bg-green-600',
        error: 'bg-red-600',
        info: 'bg-blue-600',
    };

    return (
        <div className={`p-4 rounded-md text-white shadow-lg ${bgColors[notification.type]}`}>
            {notification.message}
        </div>
    );
};

const Layout = () => {
    const [notifications, setNotifications] = useState<Notification[]>([]);

    const addNotification = useCallback((type: NotificationType, message: string) => {
        const id = Date.now();
        setNotifications(prev => [...prev, { id, type, message }]);
    }, []);

    const dismissNotification = useCallback((id: number) => {
        setNotifications(prev => prev.filter(n => n.id !== id));
    }, []);
    
    const context: OutletContextType = { addNotification };

    return (
        <div className="relative flex flex-col h-screen">
             <div className="fixed top-5 right-5 z-50 space-y-2">
                {notifications.map(n => (
                    <NotificationComponent key={n.id} notification={n} onDismiss={dismissNotification} />
                ))}
            </div>
            <div className="flex flex-1">
                <SideNav />
                <main className="flex-1 overflow-y-auto">
                    <Outlet context={context} />
                </main>
            </div>
            <Footer />
        </div>
    )
}

const App = () => {
    return (
        <MemoryRouter>
            <Routes>
                <Route path="/" element={<Layout />}>
                    <Route index element={<DashboardPage />} />
                    <Route path="vms" element={<VirtualMachinesPage />} />
                    <Route path="containers" element={<ContainersPage />} />
                    <Route path="networking" element={<NetworkingPage />} />
                    <Route path="storage" element={<StoragePage />} />
                    <Route path="hypervisor" element={<HypervisorPage />} />
                </Route>
            </Routes>
        </MemoryRouter>
    );
};

export default App;