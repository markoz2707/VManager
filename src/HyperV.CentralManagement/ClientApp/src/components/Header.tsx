import React from 'react';
import { useAuth } from '../context/AuthContext';
import { RefreshIcon, UserCircleIcon, BellIcon, QuestionMarkCircleIcon, ChevronDownIcon, LogoutIcon } from './icons/Icons';

export const Header: React.FC = () => {
  const { username, logout } = useAuth();

  return (
    <header className="bg-gray-800 text-white shadow-md z-10 flex items-center justify-between px-4 py-2 flex-shrink-0">
      <div className="flex items-center">
        <div className="text-xl font-semibold">
          <span className="font-light">VManager</span> Central
        </div>
        <div className="ml-8 relative">
          <input
            type="search"
            placeholder="Search in all environments"
            className="bg-gray-700 text-gray-300 placeholder-gray-400 rounded-sm py-1 pl-8 pr-4 w-96 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <svg className="w-4 h-4 text-gray-400 absolute top-1/2 left-2 -translate-y-1/2" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
      </div>
      <div className="flex items-center space-x-4">
        <button className="text-gray-300 hover:text-white">
            <RefreshIcon className="w-5 h-5" />
        </button>
        <button className="text-gray-300 hover:text-white">
            <BellIcon className="w-5 h-5" />
        </button>
        <button className="text-gray-300 hover:text-white">
            <QuestionMarkCircleIcon className="w-5 h-5" />
        </button>
        <div className="flex items-center space-x-2">
            <UserCircleIcon className="w-6 h-6 text-gray-400"/>
            <span className="text-sm">{username ?? 'User'}</span>
            <ChevronDownIcon className="w-4 h-4"/>
        </div>
        <button onClick={logout} className="text-gray-300 hover:text-white" title="Logout">
            <LogoutIcon className="w-5 h-5" />
        </button>
      </div>
    </header>
  );
};
