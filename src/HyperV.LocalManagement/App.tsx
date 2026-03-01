import React, { useState, useCallback, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Outlet, Navigate, useLocation } from 'react-router-dom';
import { SideNav } from './components/SideNav';
import { Header } from './components/Header';
import { HostPage } from './pages/DashboardPage';
import { VirtualMachinesPage } from './pages/VirtualMachinesPage';
import { ContainersPage } from './pages/ContainersPage';
import { NetworkingPage } from './pages/NetworkingPage';
import { StoragePage } from './pages/StoragePage';
import { HypervisorPage } from './pages/HypervisorPage';
import { ReplicationPage } from './pages/ReplicationPage';
import { MetricsPage } from './pages/MetricsPage';
import { SystemLogsPage } from './pages/SystemLogsPage';
import { LoginPage } from './pages/LoginPage';
import { Notification, NotificationType } from './types';
import { AuthProvider, useAuth } from './hooks/useAuth';
import { HostContextProvider } from './hooks/useHostContext';
import * as api from './services/hypervService';

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

const ProtectedRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const { isAuthenticated } = useAuth();
    const location = useLocation();

    if (!isAuthenticated) {
        return <Navigate to="/login" state={{ from: location }} replace />;
    }

    return <>{children}</>;
};

const Layout = () => {
    const [notifications, setNotifications] = useState<Notification[]>([]);
    const [counts, setCounts] = useState({ vms: 0, networks: 0 });

    const addNotification = useCallback((type: NotificationType, message: string) => {
        const id = Date.now();
        setNotifications(prev => [...prev, { id, type, message }]);
    }, []);

    useEffect(() => {
        const fetchCounts = async () => {
            try {
                const stats = await api.getDashboardStats();
                setCounts({ vms: stats.totalVms, networks: stats.totalNetworks });
            } catch (error) {
                console.error("Failed to fetch counts for sidenav:", error);
            }
        };
        fetchCounts();
    }, []);

    const dismissNotification = useCallback((id: number) => {
        setNotifications(prev => prev.filter(n => n.id !== id));
    }, []);

    const context: OutletContextType = { addNotification };

    return (
        <div className="relative flex flex-col h-screen bg-content-bg dark:bg-gray-900">
             <div className="fixed top-5 right-5 z-50 space-y-2">
                {notifications.map(n => (
                    <NotificationComponent key={n.id} notification={n} onDismiss={dismissNotification} />
                ))}
            </div>
            <Header />
            <div className="flex flex-1 overflow-hidden">
                <SideNav counts={counts} />
                <div className="flex-1 overflow-hidden">
                    <Outlet context={context} />
                </div>
            </div>
        </div>
    )
}

const App = () => {
    return (
        <AuthProvider>
            <HostContextProvider>
                <BrowserRouter>
                    <Routes>
                        <Route path="/login" element={<LoginPage />} />
                        <Route path="/" element={<ProtectedRoute><Layout /></ProtectedRoute>}>
                            <Route index element={<HostPage />} />
                            <Route path="vms" element={<VirtualMachinesPage />} />
                            <Route path="containers" element={<ContainersPage />} />
                            <Route path="networking" element={<NetworkingPage />} />
                            <Route path="storage" element={<StoragePage />} />
                            <Route path="replication" element={<ReplicationPage />} />
                            <Route path="metrics" element={<MetricsPage />} />
                            <Route path="logs" element={<SystemLogsPage />} />
                            <Route path="hypervisor" element={<HypervisorPage />} />
                        </Route>
                    </Routes>
                </BrowserRouter>
            </HostContextProvider>
        </AuthProvider>
    );
};

export default App;
