/**
 * API Configuration
 * 
 * Central configuration for all API endpoints and axios instances.
 * This file provides the base setup that all service files use.
 */

import axios from 'axios';
import { getApiErrorMessage } from './errorUtils';

// API Base URLs from Environment Variables
export const API_BASE_URLS = {
  aadhar: import.meta.env.VITE_AADHAR_SERVICE_URL,
  company: import.meta.env.VITE_COMPANY_CRV_SERVICE_URL,
  auth: import.meta.env.VITE_AUTH_SERVICE_URL,
  users: import.meta.env.VITE_USERS_SERVICE_URL,
  accounts: import.meta.env.VITE_ACCOUNTS_SERVICE_URL,
  transactions: import.meta.env.VITE_TRANSACTIONS_SERVICE_URL,
  notification: import.meta.env.VITE_NOTIFICATION_SERVICE_URL,
  paymentGateway: import.meta.env.VITE_PAYMENT_GATEWAY_URL,
};

// Create axios instances for each service
export const aadharApi = axios.create({
  baseURL: API_BASE_URLS.aadhar,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const companyApi = axios.create({
  baseURL: API_BASE_URLS.company,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const authApi = axios.create({
  baseURL: API_BASE_URLS.auth,
  timeout: 30000, // remote DB (e.g. Supabase) can be slow
  headers: {
    'Content-Type': 'application/json',
  },
});

export const usersApi = axios.create({
  baseURL: API_BASE_URLS.users,
  timeout: 30000, // remote DB (e.g. Supabase) can be slow
  headers: {
    'Content-Type': 'application/json',
  },
});

export const accountsApi = axios.create({
  baseURL: API_BASE_URLS.accounts,
  timeout: 45000, // account creation also verifies Aadhaar/company; remote DB can be slow
  headers: {
    'Content-Type': 'application/json',
  },
});

export const transactionsApi = axios.create({
  baseURL: API_BASE_URLS.transactions,
  timeout: 90000, // a transfer makes ~15 sequential DB round-trips; remote DB (Supabase) needs headroom
  headers: {
    'Content-Type': 'application/json',
  },
});

export const paymentGatewayApi = axios.create({
  baseURL: API_BASE_URLS.paymentGateway,
  timeout: 15000, // Longer timeout for payment processing
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token
const addAuthToken = (config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
};

// Add interceptors to protected services
[authApi, usersApi, accountsApi, transactionsApi, paymentGatewayApi].forEach((api) => {
  api.interceptors.request.use(addAuthToken, (error) => Promise.reject(error));
});

// Response interceptor for error handling
const handleResponseError = (error) => {
  // Attach a single, clean, user-facing message to every rejected request so any
  // consumer can rely on `error.userMessage` regardless of the backend's shape.
  error.userMessage = getApiErrorMessage(error);

  if (error.response?.status === 401) {
    // Check if this is a PIN verification failure (not an auth failure)
    const errorCode = error.response?.data?.detail?.error_code || error.response?.data?.error_code;
    const requestUrl = error.config?.url || '';
    const baseUrl = error.config?.baseURL || '';

    // The Central Payment Gateway is an INTERNAL service (authenticated with a
    // service API key, not the user's JWT). A 401 from it means "the browser
    // isn't allowed to call it directly" — NOT that the user's session expired.
    // Its transfer "validation" is best-effort, so never log the user out for it
    // (that was logging users out mid-transfer, before they saw the result).
    const isPaymentGateway =
      baseUrl === API_BASE_URLS.paymentGateway || requestUrl.includes('/payment/');

    // Don't logout for PIN verification failures or best-effort gateway calls.
    if (errorCode === 'INVALID_PIN' || requestUrl.includes('verify-pin') || isPaymentGateway) {
      return Promise.reject(error);
    }

    // Token expired or invalid - clear all auth state and redirect to login
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    localStorage.removeItem('gdb-auth-storage'); // Clear Zustand persisted state
    window.location.href = '/login';
  }
  return Promise.reject(error);
};

// Apply to EVERY instance (including the public aadhar/company ones) so error
// normalization is uniform across the whole app.
[aadharApi, companyApi, authApi, usersApi, accountsApi, transactionsApi, paymentGatewayApi].forEach((api) => {
  api.interceptors.response.use((response) => response, handleResponseError);
});
