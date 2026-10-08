import { getApiErrorMessage } from './errorUtils';
/**
 * Auth Service
 * 
 * Handles all authentication related API calls.
 * Includes login, logout, registration, and token verification.
 */

import { authApi } from './apiConfig';

export const authService = {
  /**
   * Login user
   * @param {Object} credentials - { login_id, password }
   * @returns {Promise<Object>} User data with token
   */
  login: async (credentials) => {
    try {
      console.log('Login attempt with:', credentials.login_id);
      const response = await authApi.post('/api/v1/auth/login', credentials);
      console.log('Login response:', response.data);
      return response.data;
    } catch (error) {
      console.error('Login error:', error.response?.status, error.response?.data);
      throw new Error(getApiErrorMessage(error, 'Login failed'));
    }
  },

  /**
   * Register new user
   * @param {Object} userData - User registration data
   * @returns {Promise<Object>} Created user data
   */
  register: async (userData) => {
    try {
      const response = await authApi.post('/api/v1/auth/register', userData);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Registration failed'));
    }
  },

  /**
   * Verify current token
   * @returns {Promise<Object>} Token verification result
   */
  verifyToken: async () => {
    try {
      const response = await authApi.get('/api/v1/auth/verify');
      return response.data;
    } catch (error) {
      throw new Error('Token verification failed');
    }
  },

  /**
   * Logout user
   * Clears local storage regardless of server response
   */
  logout: async () => {
    try {
      await authApi.post('/api/v1/auth/logout');
    } catch (error) {
      // Continue with local logout even if server logout fails
    } finally {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
    }
  },
};

export default authService;
