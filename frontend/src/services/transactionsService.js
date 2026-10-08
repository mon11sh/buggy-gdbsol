import { getApiErrorMessage } from './errorUtils';
/**
 * Transactions Service
 * 
 * Handles all transaction related API calls.
 * Includes deposits, withdrawals, transfers, and transaction history.
 */

import { transactionsApi } from './apiConfig';

export const transactionsService = {
  /**
   * Get all transactions
   * @param {Object} params - Query parameters
   * @returns {Promise<Object>} Transaction list with pagination
   */
  getAll: async (params = {}) => {
    try {
      const queryParams = new URLSearchParams();
      if (params.skip) queryParams.append('skip', params.skip);
      // Default to higher limit to get more transactions
      queryParams.append('limit', params.limit || 100);
      if (params.type && params.type !== 'ALL') queryParams.append('type', params.type);
      if (params.start_date) queryParams.append('start_date', params.start_date);
      if (params.end_date) queryParams.append('end_date', params.end_date);
      // Sort by newest first
      queryParams.append('sort_by', 'id');
      queryParams.append('order', 'desc');
      
      const url = `/api/v1/transaction-logs?${queryParams.toString()}`;
      console.log('Fetching transactions from:', url);
      const response = await transactionsApi.get(url);
      console.log('Transactions response:', response.data);
      // Backend returns { logs: [], total: X, skip, limit, has_more }
      return response.data;
    } catch (error) {
      console.error('Fetch transactions error:', error.response?.data);
      throw new Error(getApiErrorMessage(error, 'Failed to fetch transactions'));
    }
  },

  /**
   * Get transactions by account number
   * @param {string|number} accountNumber - Account number
   * @param {Object} params - Query parameters
   * @returns {Promise<Object>} Account transactions with pagination
   */
  getByAccount: async (accountNumber, params = {}) => {
    try {
      const queryParams = new URLSearchParams();
      if (params.skip) queryParams.append('skip', params.skip);
      if (params.limit) queryParams.append('limit', params.limit);
      if (params.type && params.type !== 'ALL') queryParams.append('type', params.type);
      if (params.start_date) queryParams.append('start_date', params.start_date);
      if (params.end_date) queryParams.append('end_date', params.end_date);
      
      const url = `/api/v1/transactions/account/${accountNumber}${queryParams.toString() ? '?' + queryParams.toString() : ''}`;
      const response = await transactionsApi.get(url);
      // Backend returns { account_number, logs: [], total_count, skip, limit, has_more }
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to fetch transactions'));
    }
  },

  /**
   * Get transaction statistics/summary
   * @returns {Promise<Object>} Transaction statistics
   */
  getStats: async () => {
    try {
      // Fetch all transactions to calculate stats
      const response = await transactionsApi.get('/api/v1/transaction-logs?limit=1000');
      const logs = response.data?.logs || [];
      
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      
      const todayTransactions = logs.filter(t => new Date(t.timestamp || t.created_at) >= today);
      
      // Handle both WITHDRAW and WITHDRAWAL types from backend
      const deposits = logs.filter(t => t.transaction_type === 'DEPOSIT');
      const withdrawals = logs.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL');
      const transfers = logs.filter(t => t.transaction_type === 'TRANSFER');
      
      return {
        totalTransactions: response.data?.total || logs.length,
        todayTransactions: todayTransactions.length,
        totalDeposits: deposits.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        totalWithdrawals: withdrawals.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        totalTransfers: transfers.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        depositCount: deposits.length,
        withdrawalCount: withdrawals.length,
        transferCount: transfers.length,
      };
    } catch (error) {
      console.error('Failed to fetch transaction stats:', error);
      return {
        totalTransactions: 0,
        todayTransactions: 0,
        totalDeposits: 0,
        totalWithdrawals: 0,
        totalTransfers: 0,
        depositCount: 0,
        withdrawalCount: 0,
        transferCount: 0,
      };
    }
  },

  /**
   * Process deposit
   * @param {string|number} accountNumber - Account number
   * @param {number} amount - Deposit amount
   * @param {string} pin - Account PIN
   * @returns {Promise<Object>} Deposit result
   */
  deposit: async (accountNumber, amount, pin) => {
    try {
      const response = await transactionsApi.post('/api/v1/transactions/deposit', {
        account_number: parseInt(accountNumber),
        amount: parseFloat(amount),
        pin,
      });
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Deposit failed'));
    }
  },

  /**
   * Process withdrawal
   * @param {string|number} accountNumber - Account number
   * @param {number} amount - Withdrawal amount
   * @param {string} pin - Account PIN
   * @returns {Promise<Object>} Withdrawal result
   */
  withdraw: async (accountNumber, amount, pin) => {
    try {
      const response = await transactionsApi.post('/api/v1/transactions/withdraw', {
        account_number: parseInt(accountNumber),
        amount: parseFloat(amount),
        pin,
      });
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Withdrawal failed'));
    }
  },

  /**
   * Process transfer
   * @param {string|number} fromAccount - Source account number
   * @param {string|number} toAccount - Destination account number
   * @param {number} amount - Transfer amount
   * @param {string} pin - Source account PIN
   * @param {string} transferMode - Transfer mode (NEFT, IMPS, RTGS)
   * @returns {Promise<Object>} Transfer result
   */
  transfer: async (fromAccount, toAccount, amount, pin, transferMode = 'NEFT') => {
    try {
      const response = await transactionsApi.post('/api/v1/transactions/transfer', {
        from_account: parseInt(fromAccount),
        to_account: parseInt(toAccount),
        amount: parseFloat(amount),
        pin,
        transfer_mode: transferMode,
      });
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Transfer failed'));
    }
  },

  /**
   * Get transfer limit rules for all privilege tiers
   * @returns {Promise<Array>} List of transfer limit rules
   */
  getTransferLimitRules: async () => {
    try {
      const response = await transactionsApi.get('/api/v1/transfer-limits/rules/all');
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to fetch transfer limits'));
    }
  },

  /**
   * Get transfer limit for a specific account
   * @param {string|number} accountNumber - Account number
   * @returns {Promise<Object>} Account transfer limit details
   */
  getTransferLimit: async (accountNumber) => {
    try {
      const response = await transactionsApi.get(`/api/v1/transfer-limits/${accountNumber}`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to fetch transfer limit'));
    }
  },
};

export default transactionsService;
