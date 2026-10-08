import { create } from 'zustand';
import { usersService } from '../services/api';

export const useUserStore = create((set, get) => ({
  users: [],
  selectedUser: null,
  isLoading: false,
  loading: false, // alias for isLoading
  error: null,
  filters: {
    search: '',
    role: 'all',
    status: 'all',
  },

  // Fetch all users from backend
  fetchUsers: async () => {
    set({ isLoading: true, loading: true, error: null });
    
    try {
      const response = await usersService.getAll();
      // Handle both array response and object response {users: [...]}
      const users = Array.isArray(response) ? response : (response?.users || []);
      set({ users: users, isLoading: false, loading: false });
      return users;
    } catch (error) {
      console.error('Failed to fetch users:', error.message);
      set({ users: [], isLoading: false, loading: false, error: error.message });
      throw error;
    }
  },

  // Get user by login_id (from local state first, then backend)
  getUserById: async (loginId) => {
    const { users } = get();
    
    // First check local state
    const localUser = users.find(u => u.login_id === loginId);
    if (localUser) return localUser;
    
    // Fetch from backend
    try {
      const user = await usersService.getById(loginId);
      return user;
    } catch (error) {
      console.error('User not found:', error.message);
      return null;
    }
  },

  // Fetch user by login_id from backend
  fetchUserByLoginId: async (loginId) => {
    set({ isLoading: true, loading: true, error: null });
    try {
      const user = await usersService.getById(loginId);
      set({ selectedUser: user, isLoading: false, loading: false });
      return user;
    } catch (error) {
      console.error('Failed to fetch user:', error.message);
      set({ selectedUser: null, isLoading: false, loading: false, error: error.message });
      throw error;
    }
  },

  // Create new user
  createUser: async (userData) => {
    set({ isLoading: true, loading: true, error: null });

    try {
      const newUser = await usersService.create(userData);
      // Refresh the users list after creation
      await get().fetchUsers();
      return newUser;
    } catch (error) {
      set({ isLoading: false, loading: false, error: error.message });
      throw error;
    }
  },

  // Update user
  updateUser: async (loginId, updates) => {
    set({ isLoading: true, loading: true, error: null });

    try {
      const updatedUser = await usersService.update(loginId, updates);
      set(state => ({
        users: state.users.map(user =>
          user.login_id === loginId ? { ...user, ...updatedUser } : user
        ),
        isLoading: false,
        loading: false,
      }));
      return updatedUser;
    } catch (error) {
      set({ isLoading: false, loading: false, error: error.message });
      throw error;
    }
  },

  // Activate user by login_id
  activateUser: async (loginId) => {
    set({ isLoading: true, loading: true, error: null });
    try {
      const result = await usersService.activate(loginId);
      // Update local state
      set(state => ({
        users: state.users.map(user =>
          user.login_id === loginId ? { ...user, is_active: true } : user
        ),
        isLoading: false,
        loading: false,
      }));
      return result;
    } catch (error) {
      set({ isLoading: false, loading: false, error: error.message });
      throw error;
    }
  },

  // Deactivate user by login_id
  deactivateUser: async (loginId) => {
    set({ isLoading: true, loading: true, error: null });
    try {
      const result = await usersService.deactivate(loginId);
      // Update local state
      set(state => ({
        users: state.users.map(user =>
          user.login_id === loginId ? { ...user, is_active: false } : user
        ),
        isLoading: false,
        loading: false,
      }));
      return result;
    } catch (error) {
      set({ isLoading: false, loading: false, error: error.message });
      throw error;
    }
  },

  // Set filters
  setFilters: (filters) => {
    set(state => ({
      filters: { ...state.filters, ...filters },
    }));
  },

  // Get filtered users
  getFilteredUsers: () => {
    const { users, filters } = get();

    if (!users || users.length === 0) return [];

    return users.filter(user => {
      const matchesSearch =
        filters.search === '' ||
        (user.username && user.username.toLowerCase().includes(filters.search.toLowerCase())) ||
        (user.login_id && user.login_id.toLowerCase().includes(filters.search.toLowerCase())) ||
        (user.email && user.email.toLowerCase().includes(filters.search.toLowerCase()));

      const matchesRole =
        filters.role === 'all' ||
        user.role === filters.role;

      const matchesStatus =
        filters.status === 'all' ||
        (filters.status === 'active' && user.is_active) ||
        (filters.status === 'inactive' && !user.is_active);

      return matchesSearch && matchesRole && matchesStatus;
    });
  },

  // Get statistics from current users data
  getStatistics: () => {
    const { users } = get();
    
    // Ensure users is an array
    const userList = Array.isArray(users) ? users : [];
    
    if (userList.length === 0) {
      return {
        totalUsers: 0,
        activeUsers: 0,
        inactiveUsers: 0,
        adminCount: 0,
        tellerCount: 0,
        managerCount: 0,
      };
    }

    const activeUsers = userList.filter(u => u && u.is_active);
    const admins = userList.filter(u => u && u.role === 'ADMIN');
    const tellers = userList.filter(u => u && u.role === 'TELLER');
    const managers = userList.filter(u => u && u.role === 'MANAGER');

    return {
      totalUsers: userList.length,
      activeUsers: activeUsers.length,
      inactiveUsers: userList.length - activeUsers.length,
      adminCount: admins.length,
      tellerCount: tellers.length,
      managerCount: managers.length,
    };
  },

  setSelectedUser: (user) => {
    set({ selectedUser: user });
  },

  clearError: () => {
    set({ error: null });
  },
}));

