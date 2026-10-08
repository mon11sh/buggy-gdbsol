import { getApiErrorMessage } from './errorUtils';
/**
 * Users Service
 * 
 * Handles all user management related API calls.
 * Admin-only operations for managing system users.
 */

import { usersApi } from './apiConfig';

export const usersService = {
  /**
   * Get all users
   * @returns {Promise<Array>} List of all users
   */
  getAll: async () => {
    try {
      const response = await usersApi.get('/api/v1/users');
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to fetch users'));
    }
  },

  /**
   * Get user by login_id
   * @param {string} loginId - User login ID
   * @returns {Promise<Object>} User details
   */
  getById: async (loginId) => {
    try {
      const response = await usersApi.get(`/api/v1/users/${loginId}`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to fetch user'));
    }
  },

  /**
   * Create new user
   * @param {Object} userData - User data (username, login_id, password, role)
   * @returns {Promise<Object>} Created user data
   */
  create: async (userData) => {
    try {
      const response = await usersApi.post('/api/v1/users', userData);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to create user'));
    }
  },

  /**
   * Update existing user
   * @param {string} loginId - User login ID
   * @param {Object} userData - Updated user data (username, password, role)
   * @returns {Promise<Object>} Updated user data
   */
  update: async (loginId, userData) => {
    try {
      const response = await usersApi.put(`/api/v1/users/${loginId}`, userData);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to update user'));
    }
  },

  /**
   * Activate user
   * @param {string} loginId - User login ID
   * @returns {Promise<Object>} Updated user data
   */
  activate: async (loginId) => {
    try {
      const response = await usersApi.patch(`/api/v1/users/${loginId}/activate`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to activate user'));
    }
  },

  /**
   * Deactivate (inactivate) user
   * @param {string} loginId - User login ID
   * @returns {Promise<Object>} Updated user data
   */
  deactivate: async (loginId) => {
    try {
      const response = await usersApi.patch(`/api/v1/users/${loginId}/inactivate`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to deactivate user'));
    }
  },
};

export default usersService;
