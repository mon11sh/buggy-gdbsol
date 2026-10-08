import { getApiErrorMessage } from './errorUtils';
/**
 * Payment Gateway Service
 * 
 * Handles all Central Payment Gateway related API calls.
 * Used for validating and processing inter-bank transfers.
 */

import { paymentGatewayApi } from './apiConfig';

export const paymentGatewayService = {
  /**
   * Validate transfer before processing
   * @param {Object} transferData - Transfer details to validate
   * @returns {Promise<Object>} Validation result
   */
  validateTransfer: async (transferData) => {
    try {
      const response = await paymentGatewayApi.post('/api/v1/payment/validate', transferData);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Payment gateway validation failed'));
    }
  },

  /**
   * Process payment
   * @param {Object} paymentData - Payment details
   * @returns {Promise<Object>} Payment processing result
   */
  processPayment: async (paymentData) => {
    try {
      const response = await paymentGatewayApi.post('/api/v1/payment/process', paymentData);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Payment processing failed'));
    }
  },

  /**
   * Get payment status
   * @param {string} transactionId - Transaction ID
   * @returns {Promise<Object>} Payment status
   */
  getStatus: async (transactionId) => {
    try {
      const response = await paymentGatewayApi.get(`/api/v1/payment/status/${transactionId}`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Failed to get payment status'));
    }
  },

  /**
   * Verify payment completion
   * @param {string} transactionId - Transaction ID
   * @returns {Promise<Object>} Verification result
   */
  verifyCompletion: async (transactionId) => {
    try {
      const response = await paymentGatewayApi.get(`/api/v1/payment/verify/${transactionId}`);
      return response.data;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Payment verification failed'));
    }
  },
};

export default paymentGatewayService;
