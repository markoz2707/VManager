import React from 'react';
import { NavLink } from 'react-router-dom';
import { VmIcon, NetworkIcon, HostIcon, StorageIcon, ContainerIcon, ReplicationIcon, MetricsIcon, HypervisorIcon } from './Icons';

const NavItem = ({ to, icon, label, count }: { to: string, icon: React.ReactNode, label: string, count?: number }) => {
    const activeClass = "bg-black/30 text-white";
    const inactiveClass = "text-gray-300 hover:bg-black/20 hover:text-white";

    return (
        <NavLink
            to={to}
            end={to === "/"}
            className={({ isActive }) =>
                `flex items-center px-3 py-2.5 text-sm font-medium transition-colors duration-150 relative ${isActive ? activeClass : inactiveClass}`
            }
        >
             {({ isActive }) => (
                <>
                    <div className={`absolute left-0 top-0 h-full w-1 ${isActive ? 'bg-primary' : 'bg-transparent'}`}></div>
                    {icon}
                    <span className="ml-3 flex-1">{label}</span>
                    {count !== undefined && (
                        <span className="text-xs font-mono bg-gray-600 text-white rounded-full px-2 py-0.5">{count}</span>
                    )}
                </>
             )}
        </NavLink>
    );
};


export const SideNav = ({ counts }: { counts: { vms: number; networks: number } }) => {
    return (
        <aside className="w-56 bg-sidebar text-gray-200 flex flex-col h-full flex-shrink-0">
            <div className="px-3 pt-3 pb-2 text-xs text-gray-400 uppercase tracking-wider">Navigator</div>
            <nav className="flex-1">
                <NavItem to="/" icon={<HostIcon className="h-5 w-5" />} label="Host" />
                <NavItem to="/vms" icon={<VmIcon className="h-5 w-5" />} label="Virtual Machines" count={counts.vms} />
                <NavItem to="/storage" icon={<StorageIcon className="h-5 w-5" />} label="Storage" />
                <NavItem to="/networking" icon={<NetworkIcon className="h-5 w-5" />} label="Networking" count={counts.networks} />
                <NavItem to="/containers" icon={<ContainerIcon className="h-5 w-5" />} label="Containers" />
                <NavItem to="/replication" icon={<ReplicationIcon className="h-5 w-5" />} label="Replication" />
                <NavItem to="/metrics" icon={<MetricsIcon className="h-5 w-5" />} label="Metrics" />
                <NavItem to="/hypervisor" icon={<HypervisorIcon className="h-5 w-5" />} label="Hypervisor" />
            </nav>
        </aside>
    );
};