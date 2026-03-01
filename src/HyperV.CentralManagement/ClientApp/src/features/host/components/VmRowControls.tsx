import React from 'react';
import { VmStatus } from '../../../types';
import type { VmStatus as VmStatusType } from '../../../types';
import { PlayIcon, StopIcon, PauseIcon, ArrowPathIcon } from '../../../components/icons/Icons';

interface VmRowControlsProps {
  status: VmStatusType;
  onAction: (newStatus: VmStatusType) => void;
}

const ControlButton: React.FC<{ title: string, onClick: () => void; disabled: boolean; children: React.ReactNode; className: string; }> = ({ title, onClick, disabled, children, className }) => (
  <button
    title={title}
    onClick={onClick}
    disabled={disabled}
    className={`p-1.5 rounded-full transition-colors duration-200 disabled:opacity-40 disabled:cursor-not-allowed ${className}`}
  >
    {children}
  </button>
);

export const VmRowControls: React.FC<VmRowControlsProps> = ({ status, onAction }) => {
  const isRunning = status === VmStatus.Running || status === VmStatus.PoweredOn;
  const isStopped = status === VmStatus.Stopped;

  return (
    <div className="flex items-center space-x-2">
      <ControlButton
        title="Start"
        onClick={() => onAction(VmStatus.Running)}
        disabled={isRunning}
        className="text-green-600 hover:bg-green-100"
      >
        <PlayIcon className="w-5 h-5" />
      </ControlButton>
      <ControlButton
        title="Stop"
        onClick={() => onAction(VmStatus.Stopped)}
        disabled={isStopped}
        className="text-red-600 hover:bg-red-100"
      >
        <StopIcon className="w-5 h-5" />
      </ControlButton>
      <ControlButton
        title="Save State"
        onClick={() => onAction(VmStatus.Saved)}
        disabled={!isRunning}
        className="text-yellow-600 hover:bg-yellow-100"
      >
        <PauseIcon className="w-5 h-5" />
      </ControlButton>
      <ControlButton
        title="Restart"
        onClick={() => {
            onAction(VmStatus.Stopped);
            setTimeout(() => onAction(VmStatus.Running), 1000);
        }}
        disabled={!isRunning}
        className="text-blue-600 hover:bg-blue-100"
      >
        <ArrowPathIcon className="w-5 h-5" />
      </ControlButton>
    </div>
  );
};
