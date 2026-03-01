import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { HyperVIcon } from '../components/HyperVIcon';
import { Spinner } from '../components/Spinner';
import { Button } from '../components/Button';

export const LoginPage: React.FC = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      await login(username, password);
      navigate('/', { replace: true });
    } catch (err: any) {
      setError(err.message || 'Login failed. Check your credentials.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-100 dark:bg-gray-900 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <HyperVIcon className="h-16 w-16 mx-auto text-primary-700 dark:text-primary-400 mb-4" />
          <h1 className="text-2xl font-bold text-gray-800 dark:text-gray-100">Hyper-V Management Console</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">Sign in to manage your virtual infrastructure</p>
        </div>

        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-lg p-8 border border-gray-200 dark:border-gray-700">
          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label htmlFor="username" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Username
              </label>
              <input
                id="username"
                type="text"
                autoComplete="username"
                autoFocus
                required
                value={username}
                onChange={e => setUsername(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="administrator"
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Password
              </label>
              <input
                id="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={e => setPassword(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="Enter your password"
              />
            </div>

            {error && (
              <div className="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 px-4 py-3 rounded-md text-sm">
                {error}
              </div>
            )}

            <Button
              type="submit"
              disabled={isLoading || !username || !password}
              className="w-full justify-center"
            >
              {isLoading ? <Spinner size="sm" /> : 'Sign In'}
            </Button>
          </form>
        </div>

        <p className="text-center text-xs text-gray-400 dark:text-gray-500 mt-6">
          VManager Agent v1.0 &middot; Hyper-V Management
        </p>
      </div>
    </div>
  );
};
