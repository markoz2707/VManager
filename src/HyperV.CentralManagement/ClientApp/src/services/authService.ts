import { apiFetch, setToken, clearToken } from './apiClient';
import type { LoginResponse } from '../types/api';

export async function login(
  username: string,
  password: string
): Promise<LoginResponse> {
  const response = await apiFetch<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  });
  setToken(response.token);
  return response;
}

export function logout(): void {
  clearToken();
  window.location.href = '/login';
}
