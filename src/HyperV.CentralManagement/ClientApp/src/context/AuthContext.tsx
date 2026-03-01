import React, { createContext, useContext, useState, useCallback } from 'react';
import { isAuthenticated, getToken, clearToken } from '../services/apiClient';
import * as authService from '../services/authService';

interface AuthContextType {
  isLoggedIn: boolean;
  username: string | null;
  roles: string[];
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | null>(null);

function getUsernameFromToken(token: string | null): string | null {
  if (!token) return null;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return (
      payload.unique_name ??
      payload.sub ??
      payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ??
      null
    );
  } catch {
    return null;
  }
}

function getRolesFromToken(token: string | null): string[] {
  if (!token) return [];
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    const roles =
      payload.role ??
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
      [];
    return Array.isArray(roles) ? roles : [roles];
  } catch {
    return [];
  }
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const [isLoggedIn, setIsLoggedIn] = useState(isAuthenticated());
  const [username, setUsername] = useState<string | null>(
    getUsernameFromToken(getToken())
  );
  const [roles, setRoles] = useState<string[]>(getRolesFromToken(getToken()));

  const login = useCallback(async (user: string, password: string) => {
    const response = await authService.login(user, password);
    setIsLoggedIn(true);
    setUsername(getUsernameFromToken(response.token));
    setRoles(response.roles ?? getRolesFromToken(response.token));
  }, []);

  const logout = useCallback(() => {
    clearToken();
    setIsLoggedIn(false);
    setUsername(null);
    setRoles([]);
  }, []);

  return (
    <AuthContext.Provider value={{ isLoggedIn, username, roles, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export function useAuth(): AuthContextType {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
