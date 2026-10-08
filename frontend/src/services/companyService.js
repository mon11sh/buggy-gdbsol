import { getApiErrorMessage } from './errorUtils';
/**
 * Company Service
 * 
 * Handles all company verification related API calls.
 * Used for verifying company registration during current account creation.
 */

import { companyApi } from './apiConfig';

export const companyService = {
  /**
   * Verify Company Registration Number (CIN)
   * @param {string} registrationNumber - Company registration/CIN number
   * @returns {Promise<Object>} Company details
   */
  verify: async (registrationNumber) => {
    try {
      const response = await companyApi.get(`/api/v1/company/verify/${registrationNumber}`);
      return response.data;
    } catch (error) {
      console.error('Company verification error:', error);
      throw new Error(getApiErrorMessage(error, 'Failed to verify company registration. Please try again.'));
    }
  },

  /**
   * Get list of valid company registration numbers (for testing/demo)
   * @returns {Promise<Array>} List of valid companies
   */
  getValidCompanies: async () => {
    try {
      const response = await companyApi.get('/api/v1/company/valid-companies');
      return response.data;
    } catch (error) {
      console.error('Error fetching valid companies:', error);
      throw error;
    }
  },

  /**
   * Health check for company service
   * @returns {Promise<boolean>} Service health status
   */
  healthCheck: async () => {
    try {
      const response = await companyApi.get('/health');
      return response.status === 200;
    } catch (error) {
      return false;
    }
  },
};

// Alias for backward compatibility
export const companyCrvService = companyService;

export default companyService;
