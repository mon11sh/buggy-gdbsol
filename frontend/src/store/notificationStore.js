import { create } from 'zustand';
import { persist } from 'zustand/middleware';

// Notification types
export const NOTIFICATION_TYPES = {
  SUCCESS: 'SUCCESS',
  ERROR: 'ERROR',
  WARNING: 'WARNING',
  INFO: 'INFO',
};

// Notification categories
export const NOTIFICATION_CATEGORIES = {
  ACCOUNT: 'ACCOUNT',
  TRANSACTION: 'TRANSACTION',
  SECURITY: 'SECURITY',
  SYSTEM: 'SYSTEM',
};

export const useNotificationStore = create(
  persist(
    (set, get) => ({
      notifications: [],
      unreadCount: 0,

      // Add a new notification
      addNotification: (notification) => {
        const newNotification = {
          id: Date.now() + Math.random(),
          timestamp: new Date().toISOString(),
          isRead: false,
          ...notification,
        };

        set(state => ({
          notifications: [newNotification, ...state.notifications].slice(0, 100), // Keep last 100
          unreadCount: state.unreadCount + 1,
        }));

        return newNotification;
      },

      // Mark notification as read
      markAsRead: (notificationId) => {
        set(state => ({
          notifications: state.notifications.map(n =>
            n.id === notificationId ? { ...n, isRead: true } : n
          ),
          unreadCount: Math.max(0, state.unreadCount - 1),
        }));
      },

      // Mark all notifications as read
      markAllAsRead: () => {
        set(state => ({
          notifications: state.notifications.map(n => ({ ...n, isRead: true })),
          unreadCount: 0,
        }));
      },

      // Delete a notification
      deleteNotification: (notificationId) => {
        set(state => {
          const notification = state.notifications.find(n => n.id === notificationId);
          return {
            notifications: state.notifications.filter(n => n.id !== notificationId),
            unreadCount: notification && !notification.isRead 
              ? Math.max(0, state.unreadCount - 1) 
              : state.unreadCount,
          };
        });
      },

      // Clear all notifications
      clearAll: () => {
        set({ notifications: [], unreadCount: 0 });
      },

      // Get notifications by category
      getByCategory: (category) => {
        return get().notifications.filter(n => n.category === category);
      },

      // Get unread notifications
      getUnread: () => {
        return get().notifications.filter(n => !n.isRead);
      },

      // Helper: Add account notification
      notifyAccountCreated: (accountNumber, accountType, holderName) => {
        get().addNotification({
          type: NOTIFICATION_TYPES.SUCCESS,
          category: NOTIFICATION_CATEGORIES.ACCOUNT,
          title: 'Account Created Successfully',
          message: `${accountType} account #${accountNumber} has been created for ${holderName}.`,
          accountNumber,
        });
      },

      // Helper: Add deposit notification
      notifyDeposit: (accountNumber, amount, newBalance) => {
        get().addNotification({
          type: NOTIFICATION_TYPES.SUCCESS,
          category: NOTIFICATION_CATEGORIES.TRANSACTION,
          title: 'Deposit Successful',
          message: `₹${amount.toLocaleString()} has been deposited to account #${accountNumber}. New balance: ₹${newBalance.toLocaleString()}`,
          accountNumber,
          amount,
        });
      },

      // Helper: Add withdrawal notification
      notifyWithdrawal: (accountNumber, amount, newBalance) => {
        get().addNotification({
          type: NOTIFICATION_TYPES.SUCCESS,
          category: NOTIFICATION_CATEGORIES.TRANSACTION,
          title: 'Withdrawal Successful',
          message: `₹${amount.toLocaleString()} has been withdrawn from account #${accountNumber}. New balance: ₹${newBalance.toLocaleString()}`,
          accountNumber,
          amount,
        });
      },

      // Helper: Add transfer notification
      notifyTransfer: (fromAccount, toAccount, amount, transferMode) => {
        get().addNotification({
          type: NOTIFICATION_TYPES.SUCCESS,
          category: NOTIFICATION_CATEGORIES.TRANSACTION,
          title: 'Transfer Successful',
          message: `₹${amount.toLocaleString()} has been transferred from #${fromAccount} to #${toAccount} via ${transferMode}.`,
          fromAccount,
          toAccount,
          amount,
          transferMode,
        });
      },

      // Helper: Add failed transaction notification
      notifyTransactionFailed: (type, reason) => {
        get().addNotification({
          type: NOTIFICATION_TYPES.ERROR,
          category: NOTIFICATION_CATEGORIES.TRANSACTION,
          title: `${type} Failed`,
          message: reason,
        });
      },

      // Helper: Add security notification
      notifySecurityEvent: (title, message) => {
        get().addNotification({
          type: NOTIFICATION_TYPES.WARNING,
          category: NOTIFICATION_CATEGORIES.SECURITY,
          title,
          message,
        });
      },
    }),
    {
      name: 'gdb-notifications',
    }
  )
);
