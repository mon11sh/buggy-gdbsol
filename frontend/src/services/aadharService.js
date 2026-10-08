import { getApiErrorMessage } from './errorUtils';
/**
 * Aadhar Service
 * 
 * Handles all Aadhar verification related API calls.
 * Used for verifying individual's identity during savings account creation.
 */

import { aadharApi, API_BASE_URLS } from './apiConfig';

export const aadharService = {
  /**
   * Verify Aadhar Number
   * @param {string} aadharNumber - 12-digit Aadhar number
   * @returns {Promise<Object>} Aadhar holder details
   */
  verify: async (aadharNumber) => {
    try {
      console.log('Calling Aadhar API:', `${API_BASE_URLS.aadhar}/api/v1/verify/${aadharNumber}`);
      const response = await aadharApi.get(`/api/v1/verify/${aadharNumber}`);
      console.log('Aadhar API response:', response.data);
      return response.data;
    } catch (error) {
      console.error('Aadhar verification error:', error);
      throw new Error(getApiErrorMessage(error, 'Failed to verify Aadhar number. Please try again.'));
    }
  },

  /**
   * Get list of valid Aadhar numbers (for testing/demo)
   * @returns {Promise<Array>} List of valid Aadhar numbers
   */
  getValidNumbers: async () => {
    try {
      const response = await aadharApi.get('/api/v1/valid-numbers');
      return response.data;
    } catch (error) {
      console.error('Error fetching valid Aadhar numbers:', error);
      throw error;
    }
  },

  /**
   * Health check for Aadhar service
   * @returns {Promise<boolean>} Service health status
   */
  healthCheck: async () => {
    try {
      const response = await aadharApi.get('/health');
      return response.status === 200;
    } catch (error) {
      return false;
    }
  },
};

export default aadharService;
