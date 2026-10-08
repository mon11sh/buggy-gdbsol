/**
 * API Services (Backward Compatibility)
 * 
 * This file re-exports all services from their individual files.
 * For new code, prefer importing from individual service files.
 * 
 * @example
 * // Preferred (new way)
 * import { authService } from '../services/authService';
 * 
 * // Legacy (still works)
 * import { authService, accountsService } from '../services/api';
 */

// Import all services
import { aadharService } from './aadharService';
import { companyService, companyCrvService } from './companyService';
import { authService } from './authService';
import { usersService } from './usersService';
import { accountsService } from './accountsService';
import { transactionsService } from './transactionsService';
import { paymentGatewayService } from './paymentGatewayService';
import { API_BASE_URLS } from './apiConfig';

// Named exports for backward compatibility
export {
  aadharService,
  companyService,
  companyCrvService,
  authService,
  usersService,
  accountsService,
  transactionsService,
  paymentGatewayService,
  API_BASE_URLS,
};

// Default export with all services
export default {
  aadhar: aadharService,
  company: companyService,
  auth: authService,
  users: usersService,
  accounts: accountsService,
  transactions: transactionsService,
  paymentGateway: paymentGatewayService,
};
