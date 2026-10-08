import { create } from 'zustand';
import { transactionsService } from '../services/api';

export const useTransactionStore = create((set, get) => ({
  transactions: [],
  totalTransactions: 0,
  isLoading: false,
  loading: false,
  error: null,
  stats: null,
  filters: {
    search: '',
    transactionType: 'all',
    dateFrom: '',
    dateTo: '',
    accountNumber: '',
  },

  // Fetch all transactions from backend
  fetchTransactions: async (params = {}) => {
    set({ isLoading: true, error: null });
    
    try {
      const response = await transactionsService.getAll(params);
      console.log('Raw API response:', response);
      // Backend returns { logs: [], total, skip, limit, has_more }
      const transactions = response.logs || response || [];
      console.log('Parsed transactions:', transactions.length, 'items');
      if (transactions.length > 0) {
        console.log('First transaction sample:', transactions[0]);
      }
      set({ 
        transactions: transactions, 
        totalTransactions: response.total || transactions.length,
        isLoading: false 
      });
      return transactions;
    } catch (error) {
      console.error('Failed to fetch transactions:', error.message);
      set({ transactions: [], isLoading: false, error: error.message });
      throw error;
    }
  },

  // Fetch transactions for a specific account
  fetchTransactionsByAccount: async (accountNumber, params = {}) => {
    set({ isLoading: true, error: null });
    
    try {
      const response = await transactionsService.getByAccount(accountNumber, params);
      // Backend returns { account_number, logs: [], total_count, skip, limit, has_more }
      const transactions = response.logs || response || [];
      set({ isLoading: false });
      return transactions;
    } catch (error) {
      console.error('Failed to fetch account transactions:', error.message);
      set({ isLoading: false, error: error.message });
      return [];
    }
  },

  // Fetch transaction statistics
  fetchStats: async () => {
    try {
      const stats = await transactionsService.getStats();
      set({ stats });
      return stats;
    } catch (error) {
      console.error('Failed to fetch transaction stats:', error.message);
      return null;
    }
  },

  // Process deposit
  processDeposit: async (accountNumber, amount, description, pin) => {
    set({ isLoading: true, error: null });

    try {
      const result = await transactionsService.deposit(accountNumber, amount, pin);
      
      // Add to local state
      const newTransaction = {
        id: result.id || Date.now(),
        transaction_id: result.transaction_id || 'TXN' + Date.now(),
        transaction_type: 'DEPOSIT',
        to_account: parseInt(accountNumber),
        account_number: parseInt(accountNumber),
        amount: parseFloat(amount),
        balance_after: result.balance_after,
        description: description || 'Cash deposit',
        status: 'COMPLETED',
        created_at: result.created_at || new Date().toISOString(),
        performed_by: 'current_user',
      };

      set(state => ({
        transactions: [newTransaction, ...state.transactions],
        isLoading: false,
      }));

      return result;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Process withdrawal
  processWithdrawal: async (accountNumber, amount, description, pin) => {
    set({ isLoading: true, error: null });

    try {
      const result = await transactionsService.withdraw(accountNumber, amount, pin);
      
      // Add to local state
      const newTransaction = {
        id: result.id || Date.now(),
        transaction_id: result.transaction_id || 'TXN' + Date.now(),
        transaction_type: 'WITHDRAWAL',
        from_account: parseInt(accountNumber),
        account_number: parseInt(accountNumber),
        amount: parseFloat(amount),
        balance_after: result.balance_after,
        description: description || 'Cash withdrawal',
        status: 'COMPLETED',
        created_at: result.created_at || new Date().toISOString(),
        performed_by: 'current_user',
      };

      set(state => ({
        transactions: [newTransaction, ...state.transactions],
        isLoading: false,
      }));

      return result;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Process transfer (with Central Payment Gateway validation)
  processTransfer: async (fromAccount, toAccount, amount, description, pin, transferMode = 'NEFT') => {
    set({ isLoading: true, error: null });

    try {
      // Process the transfer through the transactions service. The transactions
      // service itself calls the Central Payment Gateway (/process) server-side
      // with the internal service key — the gateway is an INTERNAL service, so the
      // browser must not call it directly (that only produced a 401 and no value).
      const result = await transactionsService.transfer(fromAccount, toAccount, amount, pin, transferMode);

      // Add to local state
      const newTransaction = {
        id: result.id || Date.now(),
        transaction_id: result.transaction_id || 'TXN' + Date.now(),
        transaction_type: 'TRANSFER',
        from_account: parseInt(fromAccount),
        to_account: parseInt(toAccount),
        amount: parseFloat(amount),
        transfer_mode: result.transfer_mode || transferMode,
        description: description || 'Fund transfer',
        status: 'COMPLETED',
        created_at: result.created_at || new Date().toISOString(),
        performed_by: 'current_user',
      };

      set(state => ({
        transactions: [newTransaction, ...state.transactions],
        isLoading: false,
      }));

      return result;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Set filters
  setFilters: (filters) => {
    set(state => ({
      filters: { ...state.filters, ...filters },
    }));
  },

  // Get filtered transactions
  getFilteredTransactions: () => {
    const { transactions, filters } = get();

    if (!transactions || transactions.length === 0) return [];

    return transactions.filter(txn => {
      const matchesSearch =
        filters.search === '' ||
        (txn.id && txn.id.toString().includes(filters.search)) ||
        (txn.transaction_id && txn.transaction_id.toLowerCase().includes(filters.search.toLowerCase())) ||
        (txn.description && txn.description.toLowerCase().includes(filters.search.toLowerCase())) ||
        (txn.account_number && txn.account_number.toString().includes(filters.search)) ||
        (txn.from_account && txn.from_account.toString().includes(filters.search)) ||
        (txn.to_account && txn.to_account.toString().includes(filters.search));

      const matchesType =
        filters.transactionType === 'all' ||
        txn.transaction_type === filters.transactionType;

      const matchesAccount =
        filters.accountNumber === '' ||
        (txn.account_number && txn.account_number.toString() === filters.accountNumber) ||
        (txn.from_account && txn.from_account.toString() === filters.accountNumber) ||
        (txn.to_account && txn.to_account.toString() === filters.accountNumber);

      const txnDate = new Date(txn.created_at);
      const matchesDateFrom =
        filters.dateFrom === '' ||
        txnDate >= new Date(filters.dateFrom);

      const matchesDateTo =
        filters.dateTo === '' ||
        txnDate <= new Date(filters.dateTo + 'T23:59:59');

      return matchesSearch && matchesType && matchesAccount && matchesDateFrom && matchesDateTo;
    });
  },

  // Get transactions for account
  getTransactionsForAccount: (accountNumber) => {
    const { transactions } = get();
    if (!transactions || transactions.length === 0) return [];
    
    return transactions.filter(
      txn =>
        txn.account_number === accountNumber ||
        txn.from_account === accountNumber ||
        txn.to_account === accountNumber
    );
  },

  // Get statistics from current transactions data
  getStatistics: () => {
    const { transactions } = get();
    
    if (!transactions || transactions.length === 0) {
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

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const todayTransactions = transactions.filter(
      txn => new Date(txn.created_at) >= today
    );

    const deposits = transactions.filter(t => t.transaction_type === 'DEPOSIT');
    // Handle both WITHDRAW and WITHDRAWAL types from backend
    const withdrawals = transactions.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL');
    const transfers = transactions.filter(t => t.transaction_type === 'TRANSFER');

    const totalDeposits = deposits.reduce((sum, t) => sum + (parseFloat(t.amount) || 0), 0);
    const totalWithdrawals = withdrawals.reduce((sum, t) => sum + (parseFloat(t.amount) || 0), 0);
    const totalTransfers = transfers.reduce((sum, t) => sum + (parseFloat(t.amount) || 0), 0);

    // Today's counts by type
    const todayDeposits = todayTransactions.filter(t => t.transaction_type === 'DEPOSIT').length;
    const todayWithdrawals = todayTransactions.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL').length;

    return {
      totalTransactions: transactions.length,
      todayTransactions: todayTransactions.length,
      totalDeposits,
      totalWithdrawals,
      totalTransfers,
      depositCount: todayDeposits,
      withdrawalCount: todayWithdrawals,
      transferCount: transfers.length,
    };
  },

  clearError: () => {
    set({ error: null });
  },
}));

