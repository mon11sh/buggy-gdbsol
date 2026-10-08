/**
 * Services Index
 * 
 * Central export file for all API services.
 * Import services from here for cleaner imports.
 * 
 * @example
 * // Import specific services
 * import { authService, accountsService } from './services';
 * 
 * // Or import all
 * import api from './services';
 * api.auth.login(credentials);
 */

// Individual service exports
export { aadharService } from './aadharService';
export { companyService, companyCrvService } from './companyService';
export { authService } from './authService';
export { usersService } from './usersService';
export { accountsService } from './accountsService';
export { transactionsService } from './transactionsService';
export { paymentGatewayService } from './paymentGatewayService';

// Re-export API configuration (for advanced usage)
export { API_BASE_URLS } from './apiConfig';

// Default export with all services
import { aadharService } from './aadharService';
import { companyService } from './companyService';
import { authService } from './authService';
import { usersService } from './usersService';
import { accountsService } from './accountsService';
import { transactionsService } from './transactionsService';
import { paymentGatewayService } from './paymentGatewayService';

export default {
  aadhar: aadharService,
  company: companyService,
  auth: authService,
  users: usersService,
  accounts: accountsService,
  transactions: transactionsService,
  paymentGateway: paymentGatewayService,
};
