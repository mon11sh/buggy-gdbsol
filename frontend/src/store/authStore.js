import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { authService } from '../services/api';

// ============================================
// JWT Token Helper Functions
// ============================================
const decodeJWT = (token) => {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch (error) {
    console.error('Failed to decode JWT:', error);
    return null;
  }
};

const isTokenExpired = (token) => {
  if (!token) return true;
  
  const decoded = decodeJWT(token);
  if (!decoded || !decoded.exp) return true;
  
  // Check if token is expired (with 30 second buffer)
  const currentTime = Math.floor(Date.now() / 1000);
  return decoded.exp < (currentTime + 30);
};

export const useAuthStore = create(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      isLoading: false,
      error: null,

      // ============================================
      // LOGIN - Real Backend API
      // ============================================
      login: async (login_id, password) => {
        set({ isLoading: true, error: null });

        try {
          const response = await authService.login({ login_id, password });
          
          // Store token in localStorage for API interceptors
          localStorage.setItem('token', response.access_token);
          
          set({
            user: {
              id: response.user_id,
              user_id: response.user_id,
              login_id: response.login_id,
              username: response.username || response.login_id,
              full_name: response.full_name || response.login_id,
              email: response.email,
              role: response.role,
              last_login: new Date().toISOString(),
            },
            token: response.access_token,
            isAuthenticated: true,
            isLoading: false,
            error: null,
          });

          return { success: true };
        } catch (error) {
          console.error('Login failed:', error.message);
          set({ 
            isLoading: false, 
            error: error.message || 'Invalid login credentials' 
          });
          return { success: false, error: error.message };
        }
      },

      // ============================================
      // LOGOUT - Real Backend API
      // ============================================
      logout: async () => {
        // Clear local state FIRST for immediate UI update
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem('gdb-auth-storage'); // Clear Zustand persisted state
        
        set({
          user: null,
          token: null,
          isAuthenticated: false,
          error: null,
        });

        // Then call backend logout (don't wait for it)
        try {
          await authService.logout();
        } catch (error) {
          console.error('Backend logout failed:', error.message);
        }
      },

      // ============================================
      // CHECK TOKEN EXPIRY - Called on each protected route
      // ============================================
      checkTokenExpiry: () => {
        const token = get().token || localStorage.getItem('token');
        
        if (isTokenExpired(token)) {
          console.log('Token expired, clearing session');
          localStorage.removeItem('token');
          localStorage.removeItem('user');
          localStorage.removeItem('gdb-auth-storage');
          set({
            user: null,
            token: null,
            isAuthenticated: false,
            error: null,
          });
          return false;
        }
        return true;
      },

      // ============================================
      // VERIFY TOKEN - Check if session is valid
      // ============================================
      verifySession: async () => {
        const token = localStorage.getItem('token');
        if (!token) {
          localStorage.removeItem('gdb-auth-storage');
          set({ isAuthenticated: false, user: null, token: null });
          return false;
        }

        // First check if token is expired locally
        if (isTokenExpired(token)) {
          console.log('Token expired locally');
          localStorage.removeItem('token');
          localStorage.removeItem('gdb-auth-storage');
          set({ isAuthenticated: false, user: null, token: null });
          return false;
        }

        try {
          const response = await authService.verifyToken();
          if (response.valid) {
            set({
              user: {
                id: response.user_id,
                user_id: response.user_id,
                login_id: response.login_id,
                role: response.role,
              },
              isAuthenticated: true,
            });
            return true;
          }
        } catch (error) {
          console.error('Session verification failed:', error.message);
          // Clear invalid session
          localStorage.removeItem('token');
          localStorage.removeItem('gdb-auth-storage');
          set({ isAuthenticated: false, user: null, token: null });
          return false;
        }

        return false;
      },

      clearError: () => {
        set({ error: null });
      },

      // Update user profile
      updateProfile: (updates) => {
        set((state) => ({
          user: state.user ? { ...state.user, ...updates } : null,
        }));
      },

      // Check if user has specific role
      hasRole: (roles) => {
        const { user } = get();
        if (!user) return false;
        if (Array.isArray(roles)) {
          return roles.includes(user.role);
        }
        return user.role === roles;
      },

      // Check if user can perform action
      canPerformAction: (action) => {
        const { user } = get();
        if (!user) return false;

        const rolePermissions = {
          ADMIN: [
            'create_account', 'view_accounts', 'update_account', 'close_account',
            'activate_account', 'inactivate_account',
            'deposit', 'withdraw', 'transfer',
            'view_users', 'create_user', 'update_user', 'delete_user',
            'view_reports', 'view_all_transactions',
          ],
          TELLER: [
            'create_account', 'view_accounts', 'update_account',
            'deposit', 'withdraw', 'transfer',
            'view_transactions',
          ],
          MANAGER: [
            'view_accounts',
            'withdraw', 'transfer',
            'view_reports', 'view_transactions',
          ],
        };

        return rolePermissions[user.role]?.includes(action) || false;
      },
    }),
    {
      name: 'gdb-auth-storage',
      partialize: (state) => ({
        user: state.user,
        token: state.token,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
);
