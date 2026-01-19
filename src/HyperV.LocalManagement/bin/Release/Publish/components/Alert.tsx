import React from 'react';
import { InfoIcon, CheckCircleIcon, ExclamationCircleIcon } from './Icons';

type AlertType = 'info' | 'success' | 'error' | 'warning';

interface AlertProps {
  type: AlertType;
  children: React.ReactNode;
  title?: string;
}

const alertConfig = {
  info: {
    bgColor: 'bg-blue-100',
    borderColor: 'border-blue-300',
    textColor: 'text-blue-800',
    Icon: (props: React.SVGProps<SVGSVGElement>) => <InfoIcon {...props} />,
  },
  success: {
    bgColor: 'bg-green-100',
    borderColor: 'border-green-300',
    textColor: 'text-green-800',
    Icon: (props: React.SVGProps<SVGSVGElement>) => <CheckCircleIcon {...props} />,
  },
  error: {
    bgColor: 'bg-red-100',
    borderColor: 'border-red-300',
    textColor: 'text-red-800',
    Icon: (props: React.SVGProps<SVGSVGElement>) => <ExclamationCircleIcon {...props} />,
  },
  warning: {
    bgColor: 'bg-yellow-100',
    borderColor: 'border-yellow-300',
    textColor: 'text-yellow-800',
    Icon: (props: React.SVGProps<SVGSVGElement>) => <ExclamationCircleIcon {...props} />,
  },
};

export const Alert: React.FC<AlertProps> = ({ type, title, children }) => {
  const { bgColor, borderColor, textColor, Icon } = alertConfig[type];

  return (
    <div className={`${bgColor} border ${borderColor} ${textColor} text-sm px-4 py-3 rounded-sm relative flex`} role="alert">
      <Icon className="h-5 w-5 mr-3 flex-shrink-0 mt-0.5" />
      <div>
        {title && <p className="font-bold">{title}</p>}
        <span>{children}</span>
      </div>
    </div>
  );
};