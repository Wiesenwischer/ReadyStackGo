import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import {
  getStoredAuth,
  login as authLogin,
  logout as authLogout,
  setAuthDirectly as authSetAuthDirectly,
  type AuthUser,
} from '@rsgo/core/services/AuthService';

interface AuthContextType {
  user: AuthUser | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  setAuthDirectly: (token: string, username: string, role: string) => void;
  logout: () => void;
  isAuthenticated: boolean;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const stored = getStoredAuth();
    if (stored.isAuthenticated) {
      setToken(stored.token);
      setUser(stored.user);
    }
    setIsLoading(false);
  }, []);

  const login = async (username: string, password: string) => {
    const state = await authLogin(username, password);
    setToken(state.token);
    setUser(state.user);
  };

  const setAuthDirectly = (newToken: string, username: string, role: string) => {
    const state = authSetAuthDirectly(newToken, username, role);
    setToken(state.token);
    setUser(state.user);
  };

  const logout = () => {
    authLogout();
    setToken(null);
    setUser(null);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        login,
        setAuthDirectly,
        logout,
        isAuthenticated: !!token,
        isLoading,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
