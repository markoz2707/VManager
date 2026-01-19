import React from 'react';
import { HyperVIcon } from './HyperVIcon';

export const Header = () => {
    return (
        <header className="bg-header text-gray-200 h-12 flex items-center justify-between px-4 flex-shrink-0 z-10 shadow-md">
            <div className="flex items-center">
                <HyperVIcon className="h-7 w-7 mr-2 text-gray-400" />
                <span className="font-semibold text-lg">Hyper-V Management Console</span>
            </div>
            <div className="flex items-center space-x-4 text-sm">
                <span>administrator@localhost</span>
                <a href="#" className="hover:text-white">Help</a>
                <a href="#" className="hover:text-white">Search</a>
            </div>
        </header>
    );
};