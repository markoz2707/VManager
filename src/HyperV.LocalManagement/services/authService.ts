import { fetchApi } from './baseService';

const TOKEN_KEY = 'vmanager_token';
const USER_KEY = 'vmanager_user';

export interface LoginResponse {
  token: string;
  expiresIn?: number;
}

export const login = async (username: string, password: string): Promise<LoginResponse> => {
  const response = await fetchApi('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  });
  if (response?.token) {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, username);
  }
  return response;
};

export const logout = (): void => {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
};

export const getToken = (): string | null => {
  return localStorage.getItem(TOKEN_KEY);
};

export const getUsername = (): string | null => {
  return localStorage.getItem(USER_KEY);
};

export const isAuthenticated = (): boolean => {
  const token = getToken();
  if (!token) return false;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload.exp * 1000 > Date.now();
  } catch {
    return false;
  }
};

export const refreshToken = async (): Promise<LoginResponse | null> => {
  try {
    const response = await fetchApi('/auth/refresh', { method: 'POST' });
    if (response?.token) {
      localStorage.setItem(TOKEN_KEY, response.token);
      return response;
    }
  } catch {
    logout();
  }
  return null;
};
