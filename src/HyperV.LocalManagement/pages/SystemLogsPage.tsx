import React, { useCallback, useEffect, useState, useRef } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import { logsService, LogEntry, LogQueryParams } from '../services/logsService';
import { RefreshIcon, ExclamationCircleIcon, ExclamationTriangleIcon, InfoIcon } from '../components/Icons';

type LogLevel = 'All' | 'Information' | 'Warning' | 'Error' | 'Critical';

const LOG_LEVELS: LogLevel[] = ['All', 'Information', 'Warning', 'Error', 'Critical'];
const DEFAULT_LIMIT = 100;

const LOG_SOURCES = [
    'Hyper-V-VMMS',
    'Hyper-V-Worker',
    'Hyper-V-VmSwitch',
    'Hyper-V-Hypervisor',
    'Hyper-V-StorageVSP',
    'System',
    'Application'
];

const getLevelBadge = (level: string) => {
    switch (level.toLowerCase()) {
        case 'critical':
            return <span className="px-2 py-0.5 text-xs font-bold rounded bg-red-700 text-white">CRITICAL</span>;
        case 'error':
            return <span className="px-2 py-0.5 text-xs font-bold rounded bg-red-500 text-white">ERROR</span>;
        case 'warning':
            return <span className="px-2 py-0.5 text-xs font-bold rounded bg-yellow-500 text-white">WARNING</span>;
        case 'information':
        default:
            return <span className="px-2 py-0.5 text-xs font-bold rounded bg-blue-500 text-white">INFO</span>;
    }
};

const getLevelIcon = (level: string) => {
    switch (level.toLowerCase()) {
        case 'critical':
        case 'error':
            return <ExclamationCircleIcon className="h-4 w-4 text-red-500" />;
        case 'warning':
            return <ExclamationTriangleIcon className="h-4 w-4 text-yellow-500" />;
        default:
            return <InfoIcon className="h-4 w-4 text-blue-500" />;
    }
};

const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return date.toLocaleString();
};

export const SystemLogsPage: React.FC = () => {
    const { addNotification } = useOutletContext<OutletContextType>();
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [sources, setSources] = useState<string[]>(LOG_SOURCES);
    const [selectedSource, setSelectedSource] = useState<string>('');
    const [selectedLevel, setSelectedLevel] = useState<LogLevel>('All');
    const [searchTerm, setSearchTerm] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [isPolling, setIsPolling] = useState(false);
    const [selectedEntry, setSelectedEntry] = useState<LogEntry | null>(null);
    const pollingInterval = useRef<number | null>(null);

    const fetchLogs = useCallback(async () => {
        try {
            const params: LogQueryParams = {
                limit: DEFAULT_LIMIT
            };

            if (selectedSource) params.source = selectedSource;
            if (selectedLevel !== 'All') params.level = selectedLevel;
            if (searchTerm) params.search = searchTerm;

            const response = await logsService.getLogs(params);
            setLogs(response.entries);
            if (response.sources && response.sources.length > 0) {
                setSources(response.sources);
            }
        } catch (err: any) {
            if (!isPolling) {
                addNotification('error', `Failed to fetch logs: ${err.message}`);
            }
        }
    }, [selectedSource, selectedLevel, searchTerm, isPolling, addNotification]);

    const handleRefresh = async () => {
        setIsLoading(true);
        await fetchLogs();
        setIsLoading(false);
    };

    useEffect(() => {
        handleRefresh();
    }, [selectedSource, selectedLevel]);

    useEffect(() => {
        if (isPolling) {
            fetchLogs();
            pollingInterval.current = window.setInterval(fetchLogs, 5000);
        } else {
            if (pollingInterval.current) {
                clearInterval(pollingInterval.current);
                pollingInterval.current = null;
            }
        }
        return () => {
            if (pollingInterval.current) clearInterval(pollingInterval.current);
        };
    }, [isPolling, fetchLogs]);

    const togglePolling = () => {
        if (!isPolling) fetchLogs();
        setIsPolling(!isPolling);
    };

    const handleSearch = (e: React.FormEvent) => {
        e.preventDefault();
        handleRefresh();
    };

    const handleExport = async (format: 'json' | 'csv') => {
        try {
            const params: LogQueryParams = {};
            if (selectedSource) params.source = selectedSource;
            if (selectedLevel !== 'All') params.level = selectedLevel;

            const blob = await logsService.exportLogs(params, format);
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `hyperv-logs-${new Date().toISOString().slice(0, 10)}.${format}`;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
            addNotification('success', `Logs exported successfully as ${format.toUpperCase()}`);
        } catch (err: any) {
            addNotification('error', `Failed to export logs: ${err.message}`);
        }
    };

    return (
        <div className="flex flex-col h-full bg-gray-50">
            <header className="p-4 bg-white border-b border-panel-border flex flex-col sm:flex-row items-center justify-between flex-shrink-0 shadow-sm gap-4">
                <div className="flex items-center gap-4 flex-wrap">
                    <h1 className="text-lg font-semibold text-gray-800">System Logs</h1>

                    <select
                        className="bg-white border border-gray-300 rounded-md shadow-sm py-1 px-3 text-sm focus:ring-primary-500 focus:border-primary-500"
                        value={selectedSource}
                        onChange={e => setSelectedSource(e.target.value)}
                    >
                        <option value="">All Sources</option>
                        {sources.map(source => (
                            <option key={source} value={source}>{source}</option>
                        ))}
                    </select>

                    <select
                        className="bg-white border border-gray-300 rounded-md shadow-sm py-1 px-3 text-sm focus:ring-primary-500 focus:border-primary-500"
                        value={selectedLevel}
                        onChange={e => setSelectedLevel(e.target.value as LogLevel)}
                    >
                        {LOG_LEVELS.map(level => (
                            <option key={level} value={level}>{level}</option>
                        ))}
                    </select>

                    <form onSubmit={handleSearch} className="flex gap-2">
                        <input
                            type="text"
                            placeholder="Search logs..."
                            className="bg-white border border-gray-300 rounded-md shadow-sm py-1 px-3 text-sm focus:ring-primary-500 focus:border-primary-500 w-48"
                            value={searchTerm}
                            onChange={e => setSearchTerm(e.target.value)}
                        />
                        <Button type="submit" variant="secondary" size="sm">Search</Button>
                    </form>
                </div>

                <div className="flex items-center gap-2">
                    <Button
                        variant={isPolling ? "secondary" : "primary"}
                        size="sm"
                        onClick={togglePolling}
                        leftIcon={<RefreshIcon className={`h-4 w-4 ${isPolling ? 'animate-spin' : ''}`} />}
                    >
                        {isPolling ? 'Stop Auto-Refresh' : 'Auto-Refresh'}
                    </Button>
                    <Button variant="secondary" size="sm" onClick={handleRefresh} disabled={isLoading}>
                        Refresh
                    </Button>
                    <div className="relative group">
                        <Button variant="secondary" size="sm">Export</Button>
                        <div className="absolute right-0 mt-1 py-1 w-32 bg-white rounded-md shadow-lg hidden group-hover:block z-10">
                            <button
                                className="block w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                                onClick={() => handleExport('json')}
                            >
                                Export JSON
                            </button>
                            <button
                                className="block w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                                onClick={() => handleExport('csv')}
                            >
                                Export CSV
                            </button>
                        </div>
                    </div>
                </div>
            </header>

            <main className="flex-1 overflow-hidden p-4 flex gap-4">
                <div className="flex-1 overflow-hidden">
                    {isLoading ? (
                        <div className="flex justify-center items-center h-64"><Spinner /></div>
                    ) : logs.length === 0 ? (
                        <div className="p-12 text-center bg-white border border-dashed border-gray-300 rounded-lg text-gray-500">
                            No log entries found. Adjust filters or check that the logging API is available.
                        </div>
                    ) : (
                        <Card>
                            <div className="overflow-auto max-h-[calc(100vh-200px)]">
                                <table className="min-w-full text-sm">
                                    <thead className="sticky top-0 bg-white z-10">
                                        <tr className="border-b border-gray-200">
                                            <th className="text-left py-2 px-2 font-semibold text-gray-500 uppercase text-xs w-8"></th>
                                            <th className="text-left py-2 px-2 font-semibold text-gray-500 uppercase text-xs w-40">Timestamp</th>
                                            <th className="text-left py-2 px-2 font-semibold text-gray-500 uppercase text-xs w-24">Level</th>
                                            <th className="text-left py-2 px-2 font-semibold text-gray-500 uppercase text-xs w-36">Source</th>
                                            <th className="text-left py-2 px-2 font-semibold text-gray-500 uppercase text-xs">Message</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-gray-100">
                                        {logs.map((entry) => (
                                            <tr
                                                key={entry.id}
                                                className={`hover:bg-gray-50 cursor-pointer ${selectedEntry?.id === entry.id ? 'bg-blue-50' : ''}`}
                                                onClick={() => setSelectedEntry(entry)}
                                            >
                                                <td className="py-2 px-2">{getLevelIcon(entry.level)}</td>
                                                <td className="py-2 px-2 text-gray-600 font-mono text-xs">{formatTimestamp(entry.timestamp)}</td>
                                                <td className="py-2 px-2">{getLevelBadge(entry.level)}</td>
                                                <td className="py-2 px-2 text-gray-700 font-mono text-xs truncate max-w-[150px]" title={entry.source}>{entry.source}</td>
                                                <td className="py-2 px-2 text-gray-700 truncate max-w-md" title={entry.message}>{entry.message}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </Card>
                    )}
                </div>

                {selectedEntry && (
                    <div className="w-96 flex-shrink-0">
                        <Card title="Log Entry Details">
                            <div className="space-y-3">
                                <div>
                                    <label className="text-xs font-semibold text-gray-500 uppercase">Timestamp</label>
                                    <p className="text-sm font-mono text-gray-700">{formatTimestamp(selectedEntry.timestamp)}</p>
                                </div>
                                <div>
                                    <label className="text-xs font-semibold text-gray-500 uppercase">Level</label>
                                    <p className="mt-1">{getLevelBadge(selectedEntry.level)}</p>
                                </div>
                                <div>
                                    <label className="text-xs font-semibold text-gray-500 uppercase">Source</label>
                                    <p className="text-sm font-mono text-gray-700">{selectedEntry.source}</p>
                                </div>
                                {selectedEntry.eventId && (
                                    <div>
                                        <label className="text-xs font-semibold text-gray-500 uppercase">Event ID</label>
                                        <p className="text-sm font-mono text-gray-700">{selectedEntry.eventId}</p>
                                    </div>
                                )}
                                {selectedEntry.category && (
                                    <div>
                                        <label className="text-xs font-semibold text-gray-500 uppercase">Category</label>
                                        <p className="text-sm font-mono text-gray-700">{selectedEntry.category}</p>
                                    </div>
                                )}
                                <div>
                                    <label className="text-xs font-semibold text-gray-500 uppercase">Message</label>
                                    <p className="text-sm text-gray-700 whitespace-pre-wrap break-words bg-gray-50 p-2 rounded border border-gray-200 max-h-64 overflow-auto">
                                        {selectedEntry.message}
                                    </p>
                                </div>
                                <Button
                                    variant="secondary"
                                    size="sm"
                                    onClick={() => setSelectedEntry(null)}
                                    className="w-full mt-4"
                                >
                                    Close Details
                                </Button>
                            </div>
                        </Card>
                    </div>
                )}
            </main>
        </div>
    );
};
