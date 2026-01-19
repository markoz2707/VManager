
import React from 'react';
import { NavLink } from 'react-router-dom';
import { VmIcon, NetworkIcon, ChipIcon, DashboardIcon, ContainerIcon } from './Icons';

const NavItem = ({ to, icon, label }: { to: string, icon: React.ReactNode, label: string }) => {
    const activeClass = "bg-primary-600 text-white";
    const inactiveClass = "text-gray-400 hover:bg-gray-700 hover:text-white";

    return (
        <NavLink
            to={to}
            className={({ isActive }) =>
                `flex items-center px-3 py-2 text-sm font-medium rounded-md transition-colors duration-150 ${isActive ? activeClass : inactiveClass}`
            }
        >
            {icon}
            <span className="ml-3">{label}</span>
        </NavLink>
    );
};


export const SideNav = () => {
    return (
        <nav className="w-64 bg-gray-900 border-r border-gray-800 p-4 flex flex-col h-full">
            <div className="flex items-center mb-8">
                <ChipIcon className="h-8 w-8 text-primary-400" />
                <h1 className="ml-2 text-xl font-bold text-white">Hyper-V Console</h1>
            </div>
            <div className="space-y-2">
                <NavItem to="/" icon={<DashboardIcon className="h-6 w-6" />} label="Dashboard" />
                <NavItem to="/vms" icon={<VmIcon className="h-6 w-6" />} label="Virtual Machines" />
                <NavItem to="/containers" icon={<ContainerIcon className="h-6 w-6" />} label="Containers" />
                <NavItem to="/networking" icon={<NetworkIcon className="h-6 w-6" />} label="Networking" />
                <NavItem to="/storage" icon={<NetworkIcon className="h-6 w-6" />} label="Storage" />
                <NavItem to="/hypervisor" icon={<ChipIcon className="h-6 w-6" />} label="Hypervisor" />
            </div>
        </nav>
    );
};
