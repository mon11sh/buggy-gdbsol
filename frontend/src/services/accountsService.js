import { getApiErrorMessage, getApiErrorCode } from './errorUtils';
/**
 * Accounts Service
 * 
 * Handles all account management related API calls.
 * Includes creating, updating, and fetching bank accounts.
 */

import { accountsApi, API_BASE_URLS } from './apiConfig';

export const accountsService = {
  /**
   * Get all accounts
   * @returns {Promise<Array>} List of all accounts
   */
  getAll: async (params = {}) => {
    try {
      // Server-driven filtering (M3) + pagination (M7): forward query params so the
      // backend does the WHERE/skip/limit work (the browser no longer re-derives it).
      const query = new URLSearchParams();
      if (params.account_type && params.account_type !== 'all') query.append('account_type', params.account_type);
      if (params.skip !== undefined && params.skip !== null) query.append('skip', params.skip);
      if (params.limit !== undefined && params.limit !== null) query.append('limit', params.limit);
      const qs = query.toString();
      const response = await accountsApi.get(`/api/v1/accounts${qs ? `?${qs}` : ''}`);
      return response.data;
    } catch (error) {
      console.error('Fetch accounts error:', error.response?.status, error.response?.data);
      throw new Error(getApiErrorMessage(error, 'Failed to fetch accounts'));
    }
  },

  /**
   * Get portfolio summary — server-computed totals (Module 9 functional analytics).
   * @returns {Promise<Object>} { total_accounts, active_accounts, total_balance, by_privilege }
   */
  getSummary: async () => {
    try {
      const response = await accountsApi.get('/api/v1/accounts/summary');
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to fetch account summary'));
    }
  },

  /**
   * Get account by account number
   * @param {string|number} accountNumber - Account number
   * @returns {Promise<Object>} Account details
   */
  getByNumber: async (accountNumber) => {
    try {
      const response = await accountsApi.get(`/api/v1/accounts/${accountNumber}`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Account not found'));
    }
  },

  /**
   * Create savings account
   * @param {Object} accountData - Savings account data
   * @returns {Promise<Object>} Created account data
   */
  createSavings: async (accountData) => {
    try {
      const response = await accountsApi.post('/api/v1/accounts/savings', accountData);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to create savings account'));
    }
  },

  /**
   * Create current account
   * @param {Object} accountData - Current account data
   * @returns {Promise<Object>} Created account data
   */
  createCurrent: async (accountData) => {
    try {
      const response = await accountsApi.post('/api/v1/accounts/current', accountData);
      return response.data;
    } catch (error) {
      const message = getApiErrorMessage(error, 'Failed to create current account');
      if (getApiErrorCode(error) === 'DUPLICATE_CONSTRAINT' || /already exists|duplicate/i.test(message)) {
        throw new Error('A current account already exists for this company registration number');
      }
      throw new Error(message);
    }
  },

  /**
   * Update account
   * @param {string|number} accountNumber - Account number
   * @param {Object} accountData - Updated account data
   * @returns {Promise<Object>} Updated account data
   */
  update: async (accountNumber, accountData) => {
    try {
      const response = await accountsApi.put(`/api/v1/accounts/${accountNumber}`, accountData);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to update account'));
    }
  },

  /**
   * Close (permanently) an account.
   * @param {string|number} accountNumber - Account number
   * @returns {Promise<Object>} Close result
   */
  close: async (accountNumber) => {
    try {
      const response = await accountsApi.post(`/api/v1/accounts/${accountNumber}/close`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to close account'));
    }
  },

  /**
   * Verify account PIN
   * @param {string|number} accountNumber - Account number
   * @param {string} pin - PIN to verify
   * @returns {Promise<Object>} Verification result
   */
  verifyPin: async (accountNumber, pin) => {
    try {
      const response = await accountsApi.post(`/api/v1/accounts/${accountNumber}/verify-pin`, { pin });
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'PIN verification failed'));
    }
  },
};

export default accountsService;
