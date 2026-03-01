import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';

const HUB_URL = 'https://localhost:8743/hubs/agent';

export interface VmStateChangedEvent {
    vmName: string;
    oldState: string;
    newState: string;
}

export interface MetricsUpdateEvent {
    metrics: unknown;
}

export interface JobProgressEvent {
    jobId: string;
    progress: number;
    status: string;
}

export interface ContainerStateChangedEvent {
    containerId: string;
    oldState: string;
    newState: string;
}

export type SignalRGroup = 'vm-events' | 'metrics' | 'jobs' | 'containers';

interface UseSignalROptions {
    /** Groups to subscribe to on connect */
    groups?: SignalRGroup[];
    /** Called when a VM state change is received */
    onVmStateChanged?: (event: VmStateChangedEvent) => void;
    /** Called when metrics are updated */
    onMetricsUpdate?: (event: MetricsUpdateEvent) => void;
    /** Called when job progress is reported */
    onJobProgress?: (event: JobProgressEvent) => void;
    /** Called when a container state changes */
    onContainerStateChanged?: (event: ContainerStateChangedEvent) => void;
    /** Whether the connection should be active (default: true) */
    enabled?: boolean;
}

const groupSubscribeMethods: Record<SignalRGroup, string> = {
    'vm-events': 'SubscribeToVmEvents',
    'metrics': 'SubscribeToMetrics',
    'jobs': 'SubscribeToJobs',
    'containers': 'SubscribeToContainers',
};

export function useSignalR(options: UseSignalROptions = {}) {
    const {
        groups = [],
        onVmStateChanged,
        onMetricsUpdate,
        onJobProgress,
        onContainerStateChanged,
        enabled = true,
    } = options;

    const [isConnected, setIsConnected] = useState(false);
    const connectionRef = useRef<signalR.HubConnection | null>(null);
    const callbacksRef = useRef(options);
    callbacksRef.current = options;

    const connect = useCallback(async () => {
        if (connectionRef.current) return;

        const token = localStorage.getItem('vmanager_token');

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(HUB_URL, {
                accessTokenFactory: () => token || '',
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets,
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Register event handlers
        connection.on('VmStateChanged', (vmName: string, oldState: string, newState: string) => {
            callbacksRef.current.onVmStateChanged?.({ vmName, oldState, newState });
        });

        connection.on('MetricsUpdate', (metrics: unknown) => {
            callbacksRef.current.onMetricsUpdate?.({ metrics });
        });

        connection.on('JobProgress', (jobId: string, progress: number, status: string) => {
            callbacksRef.current.onJobProgress?.({ jobId, progress, status });
        });

        connection.on('ContainerStateChanged', (containerId: string, oldState: string, newState: string) => {
            callbacksRef.current.onContainerStateChanged?.({ containerId, oldState, newState });
        });

        connection.onreconnected(async () => {
            setIsConnected(true);
            // Re-subscribe to groups on reconnect
            for (const group of groups) {
                try {
                    await connection.invoke(groupSubscribeMethods[group]);
                } catch { /* ignore */ }
            }
        });

        connection.onclose(() => setIsConnected(false));
        connection.onreconnecting(() => setIsConnected(false));

        connectionRef.current = connection;

        try {
            await connection.start();
            setIsConnected(true);

            // Subscribe to requested groups
            for (const group of groups) {
                try {
                    await connection.invoke(groupSubscribeMethods[group]);
                } catch (err) {
                    console.warn(`Failed to subscribe to ${group}:`, err);
                }
            }
        } catch (err) {
            console.warn('SignalR connection failed:', err);
            connectionRef.current = null;
            setIsConnected(false);
        }
    }, [groups.join(',')]);

    const disconnect = useCallback(async () => {
        if (connectionRef.current) {
            try {
                await connectionRef.current.stop();
            } catch { /* ignore */ }
            connectionRef.current = null;
            setIsConnected(false);
        }
    }, []);

    useEffect(() => {
        if (enabled) {
            connect();
        }
        return () => {
            disconnect();
        };
    }, [enabled, connect, disconnect]);

    return { isConnected };
}
