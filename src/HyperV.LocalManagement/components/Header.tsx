import React from 'react';
import { HyperVIcon } from './HyperVIcon';
import { useAuth } from '../hooks/useAuth';
import { useTheme } from '../hooks/useTheme';
import { useNavigate } from 'react-router-dom';

const SunIcon: React.FC<{ className?: string }> = ({ className }) => (
    <svg className={className} fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 3v2.25m6.364.386l-1.591 1.591M21 12h-2.25m-.386 6.364l-1.591-1.591M12 18.75V21m-4.773-4.227l-1.591 1.591M5.25 12H3m4.227-4.773L5.636 5.636M15.75 12a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0z" />
    </svg>
);

const MoonIcon: React.FC<{ className?: string }> = ({ className }) => (
    <svg className={className} fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M21.752 15.002A9.718 9.718 0 0118 15.75c-5.385 0-9.75-4.365-9.75-9.75 0-1.33.266-2.597.748-3.752A9.753 9.753 0 003 11.25C3 16.635 7.365 21 12.75 21a9.753 9.753 0 009.002-5.998z" />
    </svg>
);

export const Header = () => {
    const { username, logout } = useAuth();
    const { theme, toggleTheme } = useTheme();
    const navigate = useNavigate();

    const handleLogout = () => {
        logout();
        navigate('/login', { replace: true });
    };

    return (
        <header className="bg-header dark:bg-gray-800 text-gray-200 h-12 flex items-center justify-between px-4 flex-shrink-0 z-10 shadow-md">
            <div className="flex items-center">
                <HyperVIcon className="h-7 w-7 mr-2 text-gray-400" />
                <span className="font-semibold text-lg">Hyper-V Management Console</span>
            </div>
            <div className="flex items-center space-x-3 text-sm">
                <button
                    onClick={toggleTheme}
                    className="p-1.5 rounded-md hover:bg-gray-600 transition-colors"
                    title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
                >
                    {theme === 'dark' ? <SunIcon className="h-5 w-5" /> : <MoonIcon className="h-5 w-5" />}
                </button>
                <span className="text-gray-300">{username || 'administrator'}@localhost</span>
                <button
                    onClick={handleLogout}
                    className="hover:text-white px-2 py-1 rounded hover:bg-gray-600 transition-colors"
                >
                    Logout
                </button>
            </div>
        </header>
    );
};
