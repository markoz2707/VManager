import React, { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { HostCapabilities } from '../types';
import * as hostService from '../services/hostService';

interface HostContextType {
  capabilities: HostCapabilities | null;
  isLoading: boolean;
  isHyperV: boolean;
  isKVM: boolean;
}

const defaultCapabilities: HostCapabilities = {
  hypervisorType: 'Hyper-V',
  supportsLiveMigration: true,
  supportsDynamicMemory: true,
  supportsReplication: true,
  supportsFibreChannel: true,
  supportsStorageQoS: true,
  supportedDiskFormats: ['VHDX', 'VHD'],
  consoleType: 'RDP',
};

const HostContext = createContext<HostContextType>({
  capabilities: null,
  isLoading: true,
  isHyperV: true,
  isKVM: false,
});

export const HostContextProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [capabilities, setCapabilities] = useState<HostCapabilities | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    const fetchCapabilities = async () => {
      try {
        const caps = await hostService.getCapabilities();
        if (!cancelled) setCapabilities(caps);
      } catch {
        // Fallback to default (Hyper-V) if endpoint not available
        if (!cancelled) setCapabilities(defaultCapabilities);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };
    fetchCapabilities();
    return () => { cancelled = true; };
  }, []);

  const isHyperV = capabilities?.hypervisorType === 'Hyper-V';
  const isKVM = capabilities?.hypervisorType === 'KVM';

  return (
    <HostContext.Provider value={{ capabilities, isLoading, isHyperV, isKVM }}>
      {children}
    </HostContext.Provider>
  );
};

export const useHostContext = (): HostContextType => {
  return useContext(HostContext);
};

export const useIsHyperV = (): boolean => useHostContext().isHyperV;
export const useIsKVM = (): boolean => useHostContext().isKVM;
