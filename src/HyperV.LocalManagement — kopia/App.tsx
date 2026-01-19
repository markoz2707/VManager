import React, { useState, useCallback } from 'react';
import { MemoryRouter, Routes, Route, Outlet } from 'react-router-dom';
import { MainLayout } from './components/Layout/MainLayout';
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

const App = () => {
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
        <MemoryRouter>
            <Routes>
                <Route path="/" element={<MainLayout notifications={notifications} onDismissNotification={dismissNotification} />}>
                    <Route index element={<DashboardPage />} />
                    <Route path="vms" element={<VirtualMachinesPage />} />
                    <Route path="vms/:id" element={<VirtualMachinesPage />} />
                    <Route path="containers" element={<ContainersPage />} />
                    <Route path="containers/:id" element={<ContainersPage />} />
                    <Route path="networking" element={<NetworkingPage />} />
                    <Route path="networking/:id" element={<NetworkingPage />} />
                    <Route path="storage" element={<StoragePage />} />
                    <Route path="storage/:id" element={<StoragePage />} />
                    <Route path="hypervisor" element={<HypervisorPage />} />
                </Route>
            </Routes>
        </MemoryRouter>
    );
};

export default App;