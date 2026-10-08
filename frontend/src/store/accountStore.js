import { create } from 'zustand';
import { accountsService, aadharService, companyCrvService } from '../services/api';

export const useAccountStore = create((set, get) => ({
  accounts: [],
  selectedAccount: null,
  isLoading: false,
  loading: false,
  error: null,
  filters: {
    search: '',
    accountType: 'all',
    status: 'all',
    privilege: 'all',
  },

  // Fetch accounts from backend. Accepts server-side params: { account_type, skip, limit }
  // (M3 filtering + M7 pagination are done by the backend, not in the browser).
  fetchAccounts: async (params = {}) => {
    set({ isLoading: true, error: null });

    try {
      const accounts = await accountsService.getAll(params);
      set({ accounts: accounts || [], isLoading: false });
      return accounts;
    } catch (error) {
      console.error('Failed to fetch accounts:', error.message);
      set({ accounts: [], isLoading: false, error: error.message });
      throw error;
    }
  },

  // Get account by number.
  // Always fetch the single-account endpoint first: it is authoritative and returns the COMPLETE
  // record (type-specific fields like company_name / date_of_birth / decrypted aadhaar). The list
  // endpoint is a lighter projection, so trusting its cached row here left the details page with
  // blank type-specific fields. Fall back to the cached list row only if the fetch fails.
  getAccountByNumber: async (accountNumber) => {
    const { accounts } = get();
    const accNum = typeof accountNumber === 'string' ? parseInt(accountNumber, 10) : accountNumber;

    try {
      const account = await accountsService.getByNumber(accountNumber);
      if (account) return account;
    } catch (error) {
      console.error('Account not found:', error.message);
    }

    // Fallback: whatever we already have cached from the list.
    return accounts.find(acc => acc.account_number === accNum) ?? null;
  },

  // Verify Aadhar for savings account
  verifyAadhar: async (aadharNumber) => {
    set({ isLoading: true, error: null });
    
    try {
      const result = await aadharService.verify(aadharNumber);
      set({ isLoading: false });
      return result;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Verify Company Registration for current account
  verifyCompanyRegistration: async (registrationNumber) => {
    set({ isLoading: true, error: null });
    
    try {
      const result = await companyCrvService.verify(registrationNumber);
      set({ isLoading: false });
      return result;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Create savings account
  createSavingsAccount: async (accountData) => {
    set({ isLoading: true, error: null });

    try {
      const newAccount = await accountsService.createSavings(accountData);
      set(state => ({
        accounts: [...state.accounts, newAccount],
        isLoading: false,
      }));
      return newAccount;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Create current account
  createCurrentAccount: async (accountData) => {
    set({ isLoading: true, error: null });

    try {
      const newAccount = await accountsService.createCurrent(accountData);
      set(state => ({
        accounts: [...state.accounts, newAccount],
        isLoading: false,
      }));
      return newAccount;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Update account
  updateAccount: async (accountNumber, updates) => {
    set({ isLoading: true, error: null });

    try {
      const updatedAccount = await accountsService.update(accountNumber, updates);
      set(state => ({
        accounts: state.accounts.map(acc =>
          acc.account_number === accountNumber ? updatedAccount : acc
        ),
        isLoading: false,
      }));
      return updatedAccount;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Activate account
  activateAccount: async (accountNumber) => {
    return await get().updateAccount(accountNumber, { is_active: true, closed_date: null });
  },

  // Deactivate account
  deactivateAccount: async (accountNumber) => {
    return await get().updateAccount(accountNumber, {
      is_active: false,
      closed_date: new Date().toISOString()
    });
  },

  // Close account (permanent) — hits the backend close endpoint.
  closeAccount: async (accountNumber) => {
    set({ isLoading: true, error: null });
    try {
      await accountsService.close(accountNumber);
      set({ isLoading: false });
      return true;
    } catch (error) {
      set({ isLoading: false, error: error.message });
      throw error;
    }
  },

  // Update balance locally (called after transaction)
  updateBalance: (accountNumber, amountChange) => {
    set(state => ({
      accounts: state.accounts.map(acc =>
        acc.account_number === parseInt(accountNumber)
          ? { ...acc, balance: acc.balance + amountChange }
          : acc
      ),
    }));
  },

  // Verify PIN
  verifyPin: async (accountNumber, pin) => {
    try {
      const result = await accountsService.verifyPin(accountNumber, pin);
      return result.valid === true || result.verified === true;
    } catch (error) {
      console.error('PIN verification failed:', error.message);
      throw error;
    }
  },

  // Set filters
  setFilters: (filters) => {
    set(state => ({
      filters: { ...state.filters, ...filters },
    }));
  },

  // Get filtered accounts
  getFilteredAccounts: () => {
    const { accounts, filters } = get();
    
    if (!accounts || accounts.length === 0) return [];
    
    return accounts.filter(account => {
      const matchesSearch =
        filters.search === '' ||
        (account.name && account.name.toLowerCase().includes(filters.search.toLowerCase())) ||
        (account.account_number && account.account_number.toString().includes(filters.search));

      // NOTE: account-type filtering is done SERVER-SIDE now (see AccountsPage /
      // fetchAccounts({ account_type })). Only search/status/privilege are refined
      // client-side over the page the server returned.

      const matchesStatus =
        filters.status === 'all' ||
        (filters.status === 'active' && account.is_active) ||
        (filters.status === 'inactive' && !account.is_active);

      const matchesPrivilege =
        filters.privilege === 'all' ||
        account.privilege === filters.privilege;

      return matchesSearch && matchesStatus && matchesPrivilege;
    });
  },

  // Get statistics from current accounts data
  getStatistics: () => {
    const { accounts } = get();
    
    if (!accounts || accounts.length === 0) {
      return {
        totalAccounts: 0,
        activeAccounts: 0,
        savingsAccounts: 0,
        currentAccounts: 0,
        totalBalance: 0,
        averageBalance: 0,
      };
    }
    
    const activeAccounts = accounts.filter(a => a.is_active);
    const savingsAccounts = accounts.filter(a => a.account_type === 'SAVINGS');
    const currentAccounts = accounts.filter(a => a.account_type === 'CURRENT');
    const totalBalance = accounts.reduce((sum, a) => sum + (a.balance || 0), 0);

    return {
      totalAccounts: accounts.length,
      activeAccounts: activeAccounts.length,
      savingsAccounts: savingsAccounts.length,
      currentAccounts: currentAccounts.length,
      totalBalance,
      averageBalance: accounts.length > 0 ? totalBalance / accounts.length : 0,
    };
  },

  setSelectedAccount: (account) => {
    set({ selectedAccount: account });
  },

  clearError: () => {
    set({ error: null });
  },
}));

