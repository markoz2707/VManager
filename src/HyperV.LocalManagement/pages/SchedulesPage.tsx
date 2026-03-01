import React, { useCallback, useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { Button } from '../components/Button';
import { Modal } from '../components/Modal';
import { Card } from '../components/Card';
import { Spinner } from '../components/Spinner';
import { OutletContextType } from '../App';
import { scheduleService, ScheduledTask, CreateScheduleRequest } from '../services/scheduleService';
import { PlusIcon, RefreshIcon, TrashIcon } from '../components/Icons';

const CRON_PRESETS: { label: string; value: string }[] = [
  { label: 'Every hour', value: '0 * * * *' },
  { label: 'Every 6 hours', value: '0 */6 * * *' },
  { label: 'Every day at midnight', value: '0 0 * * *' },
  { label: 'Every day at 2 AM', value: '0 2 * * *' },
  { label: 'Every Sunday at midnight', value: '0 0 * * 0' },
  { label: 'Every Monday at 6 AM', value: '0 6 * * 1' },
  { label: 'First day of month at midnight', value: '0 0 1 * *' },
  { label: 'Custom', value: '' },
];

const ACTION_OPTIONS = [
  { label: 'Start VM', value: 'start' },
  { label: 'Stop VM', value: 'stop' },
  { label: 'Shutdown VM (graceful)', value: 'shutdown' },
  { label: 'Create Snapshot', value: 'snapshot' },
];

const formatDateTime = (iso: string | null): string => {
  if (!iso) return '--';
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
};

export const SchedulesPage: React.FC = () => {
  const { addNotification } = useOutletContext<OutletContextType>();

  const [schedules, setSchedules] = useState<ScheduledTask[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);
  const [togglingIds, setTogglingIds] = useState<Set<string>>(new Set());

  const [createForm, setCreateForm] = useState<CreateScheduleRequest>({
    name: '',
    cronExpression: '0 0 * * *',
    action: 'snapshot',
    targetVms: [],
  });
  const [targetVmsText, setTargetVmsText] = useState('');
  const [selectedPreset, setSelectedPreset] = useState('0 0 * * *');

  const fetchData = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await scheduleService.getSchedules();
      setSchedules(data);
    } catch (err: any) {
      addNotification('error', `Failed to load schedules: ${err.message}`);
    } finally {
      setIsLoading(false);
    }
  }, [addNotification]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const vms = targetVmsText
      .split(',')
      .map(v => v.trim())
      .filter(Boolean);
    if (vms.length === 0) {
      addNotification('error', 'Please specify at least one target VM.');
      return;
    }
    try {
      await scheduleService.createSchedule({
        ...createForm,
        targetVms: vms,
      });
      addNotification('success', `Schedule "${createForm.name}" created successfully.`);
      setIsCreateOpen(false);
      resetCreateForm();
      fetchData();
    } catch (err: any) {
      addNotification('error', `Failed to create schedule: ${err.message}`);
    }
  };

  const resetCreateForm = () => {
    setCreateForm({ name: '', cronExpression: '0 0 * * *', action: 'snapshot', targetVms: [] });
    setTargetVmsText('');
    setSelectedPreset('0 0 * * *');
  };

  const handleToggle = async (task: ScheduledTask) => {
    setTogglingIds(prev => new Set(prev).add(task.id));
    try {
      if (task.isEnabled) {
        await scheduleService.disableSchedule(task.id);
        addNotification('info', `Schedule "${task.name}" disabled.`);
      } else {
        await scheduleService.enableSchedule(task.id);
        addNotification('success', `Schedule "${task.name}" enabled.`);
      }
      fetchData();
    } catch (err: any) {
      addNotification('error', `Failed to toggle schedule: ${err.message}`);
    } finally {
      setTogglingIds(prev => {
        const next = new Set(prev);
        next.delete(task.id);
        return next;
      });
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await scheduleService.deleteSchedule(id);
      addNotification('success', 'Schedule deleted.');
      setDeleteConfirmId(null);
      fetchData();
    } catch (err: any) {
      addNotification('error', `Failed to delete schedule: ${err.message}`);
    }
  };

  const handlePresetChange = (value: string) => {
    setSelectedPreset(value);
    if (value) {
      setCreateForm(prev => ({ ...prev, cronExpression: value }));
    }
  };

  return (
    <div className="flex flex-col h-full">
      <header className="p-4 bg-panel-bg border-b border-panel-border flex items-center justify-between flex-shrink-0">
        <h1 className="text-lg font-semibold text-gray-800">Scheduled Tasks</h1>
        <div className="flex items-center space-x-1">
          <Button variant="toolbar" onClick={() => setIsCreateOpen(true)} leftIcon={<PlusIcon />}>
            Create Schedule
          </Button>
          <Button variant="toolbar" onClick={fetchData} leftIcon={<RefreshIcon />}>
            Refresh
          </Button>
        </div>
      </header>

      <main className="flex-1 overflow-y-auto p-4">
        <Card>
          {isLoading ? (
            <div className="p-6 flex justify-center">
              <Spinner />
            </div>
          ) : schedules.length === 0 ? (
            <div className="p-6 text-center text-gray-500">
              No scheduled tasks found. Click "Create Schedule" to add one.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-100 border-b border-panel-border">
                  <tr>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Name</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Cron</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Action</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Target VMs</th>
                    <th className="px-4 py-2 text-center font-semibold text-gray-600">Status</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Last Run</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Next Run</th>
                    <th className="px-4 py-2 text-left font-semibold text-gray-600">Last Result</th>
                    <th className="px-4 py-2 text-right font-semibold text-gray-600">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {schedules.map(task => (
                    <tr key={task.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2 font-medium text-gray-800">{task.name}</td>
                      <td className="px-4 py-2 text-gray-600 font-mono text-xs">{task.cronExpression}</td>
                      <td className="px-4 py-2 text-gray-600 capitalize">{task.action}</td>
                      <td className="px-4 py-2 text-gray-600">
                        <div className="flex flex-wrap gap-1">
                          {task.targetVms.map((vm, idx) => (
                            <span
                              key={idx}
                              className="inline-block px-2 py-0.5 text-xs bg-blue-100 text-blue-800 rounded"
                            >
                              {vm}
                            </span>
                          ))}
                        </div>
                      </td>
                      <td className="px-4 py-2 text-center">
                        <button
                          onClick={() => handleToggle(task)}
                          disabled={togglingIds.has(task.id)}
                          className={`relative inline-flex h-5 w-9 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                            task.isEnabled ? 'bg-green-500' : 'bg-gray-300'
                          } ${togglingIds.has(task.id) ? 'opacity-50 cursor-not-allowed' : ''}`}
                          title={task.isEnabled ? 'Click to disable' : 'Click to enable'}
                        >
                          <span
                            className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                              task.isEnabled ? 'translate-x-4' : 'translate-x-0'
                            }`}
                          />
                        </button>
                      </td>
                      <td className="px-4 py-2 text-gray-600 text-xs">{formatDateTime(task.lastRunUtc)}</td>
                      <td className="px-4 py-2 text-gray-600 text-xs">{formatDateTime(task.nextRunUtc)}</td>
                      <td className="px-4 py-2 text-gray-600 text-xs">
                        {task.lastRunResult ? (
                          <span
                            className={`inline-block px-2 py-0.5 rounded text-xs ${
                              task.lastRunResult.toLowerCase() === 'success'
                                ? 'bg-green-100 text-green-800'
                                : 'bg-red-100 text-red-800'
                            }`}
                          >
                            {task.lastRunResult}
                          </span>
                        ) : (
                          '--'
                        )}
                      </td>
                      <td className="px-4 py-2 text-right">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setDeleteConfirmId(task.id)}
                          title="Delete schedule"
                        >
                          <TrashIcon className="w-4 h-4 text-red-500" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </main>

      {/* Create Schedule Modal */}
      <Modal isOpen={isCreateOpen} onClose={() => setIsCreateOpen(false)} title="Create Scheduled Task">
        <form onSubmit={handleCreate} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Name</label>
            <input
              type="text"
              required
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={createForm.name}
              onChange={e => setCreateForm({ ...createForm, name: e.target.value })}
              placeholder="e.g. Nightly Snapshots"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Cron Preset</label>
            <select
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={selectedPreset}
              onChange={e => handlePresetChange(e.target.value)}
            >
              {CRON_PRESETS.map(p => (
                <option key={p.label} value={p.value}>
                  {p.label}{p.value ? ` (${p.value})` : ''}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Cron Expression</label>
            <input
              type="text"
              required
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm font-mono focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={createForm.cronExpression}
              onChange={e => {
                setCreateForm({ ...createForm, cronExpression: e.target.value });
                setSelectedPreset('');
              }}
              placeholder="0 0 * * *"
            />
            <p className="mt-1 text-xs text-gray-500">
              Format: minute hour day-of-month month day-of-week
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Action</label>
            <select
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={createForm.action}
              onChange={e => setCreateForm({ ...createForm, action: e.target.value })}
            >
              {ACTION_OPTIONS.map(opt => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Target VMs</label>
            <input
              type="text"
              required
              className="mt-1 block w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500"
              value={targetVmsText}
              onChange={e => setTargetVmsText(e.target.value)}
              placeholder="VM1, VM2, VM3"
            />
            <p className="mt-1 text-xs text-gray-500">Comma-separated list of VM names</p>
          </div>

          <div className="mt-6 flex justify-end space-x-3 pt-4 border-t border-gray-200">
            <Button
              type="button"
              variant="secondary"
              onClick={() => {
                setIsCreateOpen(false);
                resetCreateForm();
              }}
            >
              Cancel
            </Button>
            <Button type="submit">Create Schedule</Button>
          </div>
        </form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <Modal
        isOpen={deleteConfirmId !== null}
        onClose={() => setDeleteConfirmId(null)}
        title="Confirm Delete"
      >
        <p className="text-sm text-gray-700 mb-6">
          Are you sure you want to delete this scheduled task? This action cannot be undone.
        </p>
        <div className="flex justify-end space-x-3">
          <Button variant="secondary" onClick={() => setDeleteConfirmId(null)}>
            Cancel
          </Button>
          <Button
            variant="primary"
            onClick={() => deleteConfirmId && handleDelete(deleteConfirmId)}
          >
            Delete
          </Button>
        </div>
      </Modal>
    </div>
  );
};

export default SchedulesPage;
