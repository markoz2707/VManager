import React, { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react';
import * as authService from '../services/authService';

interface AuthContextType {
  isAuthenticated: boolean;
  username: string | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | null>(null);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [token, setToken] = useState<string | null>(authService.getToken());
  const [username, setUsername] = useState<string | null>(authService.getUsername());

  const isAuthenticated = !!token && authService.isAuthenticated();

  const login = useCallback(async (user: string, password: string) => {
    const response = await authService.login(user, password);
    setToken(response.token);
    setUsername(user);
  }, []);

  const logout = useCallback(() => {
    authService.logout();
    setToken(null);
    setUsername(null);
  }, []);

  useEffect(() => {
    const handleStorage = (e: StorageEvent) => {
      if (e.key === 'vmanager_token') {
        setToken(e.newValue);
        if (!e.newValue) setUsername(null);
      }
    };
    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, []);

  return (
    <AuthContext.Provider value={{ isAuthenticated, username, token, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
