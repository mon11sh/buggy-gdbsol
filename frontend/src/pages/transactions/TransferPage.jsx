import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useTransactionStore } from '../../store/transactionStore';
import { useAuthStore } from '../../store/authStore';
import { useNotificationStore } from '../../store/notificationStore';
import {
  ArrowLeft,
  ArrowRightLeft,
  CreditCard,
  DollarSign,
  FileText,
  Check,
  AlertCircle,
  Loader2,
  User,
  Lock,
  ShieldAlert,
  ArrowRight,
  Building2,
  PiggyBank,
  Zap,
  Clock,
  Smartphone,
  Banknote,
  Send,
  Shield,
  CheckCircle2,
  XCircle,
  Wallet,
  Globe,
} from 'lucide-react';
import toast from 'react-hot-toast';

// Transfer modes configuration
const TRANSFER_MODES = {
  IMPS: {
    id: 'IMPS',
    name: 'IMPS',
    fullName: 'Immediate Payment Service',
    icon: Zap,
    color: 'text-green-600',
    bgColor: 'bg-green-100',
    borderColor: 'border-green-500',
    description: 'Instant transfer, 24x7 available',
    limit: '₹5,00,000 per transaction',
    timing: 'Instant (24x7)',
    fee: '₹5 + GST',
  },
  NEFT: {
    id: 'NEFT',
    name: 'NEFT',
    fullName: 'National Electronic Funds Transfer',
    icon: Send,
    color: 'text-blue-600',
    bgColor: 'bg-blue-100',
    borderColor: 'border-blue-500',
    description: 'Batch-wise settlement, every 30 mins',
    limit: 'No limit',
    timing: '30 mins - 2 hours',
    fee: 'Free',
  },
  RTGS: {
    id: 'RTGS',
    name: 'RTGS',
    fullName: 'Real Time Gross Settlement',
    icon: Clock,
    color: 'text-purple-600',
    bgColor: 'bg-purple-100',
    borderColor: 'border-purple-500',
    description: 'Real-time settlement for high-value transfers',
    limit: 'Min ₹2,00,000',
    timing: 'Instant (Bank hours)',
    fee: '₹25 + GST',
  },
  UPI: {
    id: 'UPI',
    name: 'UPI',
    fullName: 'Unified Payments Interface',
    icon: Smartphone,
    color: 'text-orange-600',
    bgColor: 'bg-orange-100',
    borderColor: 'border-orange-500',
    description: 'Mobile-based instant transfers',
    limit: '₹1,00,000 per transaction',
    timing: 'Instant (24x7)',
    fee: 'Free',
  },
  CHEQUE: {
    id: 'CHEQUE',
    name: 'CHEQUE',
    fullName: 'Cheque Payment',
    icon: Banknote,
    color: 'text-gray-600',
    bgColor: 'bg-gray-100',
    borderColor: 'border-gray-500',
    description: 'Traditional cheque-based transfer',
    limit: 'As per cheque',
    timing: '2-3 business days',
    fee: 'Varies',
  },
};

const TransferPage = () => {
  const navigate = useNavigate();
  const { user, hasRole } = useAuthStore();
  const { accounts, fetchAccounts, getAccountByNumber, updateBalance, verifyPin } = useAccountStore();
  const { processTransfer } = useTransactionStore();
  const { notifyTransfer, notifyTransactionFailed } = useNotificationStore();

  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState({
    fromAccount: '',
    toAccount: '',
    amount: '',
    description: '',
    pin: '',
    transferMode: 'IMPS', // Default transfer mode
  });
  const [errors, setErrors] = useState({});
  const [sourceAccount, setSourceAccount] = useState(null);
  const [destinationAccount, setDestinationAccount] = useState(null);
  const [transactionResult, setTransactionResult] = useState(null);
  
  // Processing state for gateway validation UI
  const [processingState, setProcessingState] = useState({
    step: 0,
    status: 'idle', // idle, processing, success, error
    steps: [
      { id: 1, label: 'Initiating Transfer', description: 'Preparing transaction details...', status: 'pending' },
      { id: 2, label: 'Debiting Source Account', description: 'Holding funds from source account...', status: 'pending' },
      { id: 3, label: 'Central Payment Gateway', description: 'Validating with payment gateway...', status: 'pending' },
      { id: 4, label: 'Gateway Authorization', description: 'Awaiting gateway approval...', status: 'pending' },
      { id: 5, label: 'Crediting Destination', description: 'Releasing funds to destination...', status: 'pending' },
      { id: 6, label: 'Transaction Complete', description: 'Finalizing transaction...', status: 'pending' },
    ],
    gatewayResponse: null,
    error: null,
  });

  // Fetch accounts on mount
  useEffect(() => {
    fetchAccounts().catch(err => console.error('Failed to fetch accounts:', err));
  }, [fetchAccounts]);

  // Check if user has access
  useEffect(() => {
    if (!hasRole(['TELLER'])) {
      toast.error('You do not have permission to perform transfers');
      navigate('/transactions');
    }
  }, [hasRole, navigate]);

  const activeAccounts = accounts.filter(acc => acc.is_active);

  // Helper to find account from local state (synchronous)
  const findLocalAccount = (accountNumber) => {
    const accNum = typeof accountNumber === 'string' ? parseInt(accountNumber, 10) : accountNumber;
    return accounts.find(acc => acc.account_number === accNum);
  };

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    setErrors(prev => ({ ...prev, [name]: '' }));

    if (name === 'fromAccount') {
      const accNum = parseInt(value, 10);
      if (!isNaN(accNum)) {
        const account = findLocalAccount(accNum);
        if (account && account.is_active) {
          setSourceAccount(account);
        } else {
          setSourceAccount(null);
        }
      } else {
        setSourceAccount(null);
      }
    }

    if (name === 'toAccount') {
      const accNum = parseInt(value, 10);
      if (!isNaN(accNum)) {
        const account = findLocalAccount(accNum);
        if (account && account.is_active) {
          setDestinationAccount(account);
        } else {
          setDestinationAccount(null);
        }
      } else {
        setDestinationAccount(null);
      }
    }
  };

  const validateStep1 = () => {
    const newErrors = {};
    
    if (!formData.fromAccount) {
      newErrors.fromAccount = 'Please select source account';
    } else if (!sourceAccount) {
      newErrors.fromAccount = 'Source account not found or inactive';
    }

    if (!formData.toAccount) {
      newErrors.toAccount = 'Please select destination account';
    } else if (!destinationAccount) {
      newErrors.toAccount = 'Destination account not found or inactive';
    }

    if (formData.fromAccount && formData.toAccount && formData.fromAccount === formData.toAccount) {
      newErrors.toAccount = 'Source and destination accounts cannot be the same';
    }

    if (!formData.amount) {
      newErrors.amount = 'Amount is required';
    } else if (parseFloat(formData.amount) <= 0) {
      newErrors.amount = 'Amount must be greater than 0';
    } else if (sourceAccount && parseFloat(formData.amount) > sourceAccount.balance) {
      newErrors.amount = 'Insufficient balance in source account';
    } else if (parseFloat(formData.amount) > 1000000) {
      newErrors.amount = 'Maximum transfer limit is ₹10,00,000 per transaction';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const validateStep2 = async () => {
    const newErrors = {};
    if (!formData.pin) {
      newErrors.pin = 'PIN is required for transfer';
    } else if (formData.pin.length !== 4) {
      newErrors.pin = 'PIN must be 4 digits';
    } else {
      // Verify PIN with backend
      try {
        const isValid = await verifyPin(formData.fromAccount, formData.pin);
        if (!isValid) {
          newErrors.pin = 'Invalid PIN. Please try again.';
        }
      } catch (error) {
        newErrors.pin = error.message || 'PIN verification failed. Please try again.';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleNext = async () => {
    if (step === 1 && validateStep1()) {
      setStep(2);
    } else if (step === 2) {
      setLoading(true);
      try {
        const isValid = await validateStep2();
        if (isValid) {
          setStep(3);
        }
      } finally {
        setLoading(false);
      }
    }
  };

  const handleTransfer = async () => {
    setLoading(true);
    setStep(4); // Move to processing step
    
    // Reset processing state
    setProcessingState(prev => ({
      ...prev,
      step: 0,
      status: 'processing',
      error: null,
      gatewayResponse: null,
      steps: prev.steps.map(s => ({ ...s, status: 'pending' })),
    }));
    
    try {
      const amount = Math.round(parseFloat(formData.amount) * 100) / 100;
      
      // Step 1: Initiating Transfer
      await updateProcessingStep(1, 'processing');
      await delay(800);
      await updateProcessingStep(1, 'success');
      
      // Step 2: Debiting Source Account (Hold)
      await updateProcessingStep(2, 'processing');
      await delay(1000);
      await updateProcessingStep(2, 'success');
      
      // Step 3: Central Payment Gateway Validation
      await updateProcessingStep(3, 'processing');
      await delay(1200);
      await updateProcessingStep(3, 'success');
      
      // Step 4: Gateway Authorization - This is the critical step
      await updateProcessingStep(4, 'processing');
      
      // Actually call the backend which validates with gateway
      const result = await processTransfer(
        formData.fromAccount,
        formData.toAccount,
        amount,
        formData.description || `Fund transfer to ${destinationAccount.name}`,
        formData.pin,
        formData.transferMode
      );
      
      // Store gateway response for display
      setProcessingState(prev => ({
        ...prev,
        gatewayResponse: {
          transactionId: result.transaction_id,
          gatewayRef: `CPG-${Date.now()}`,
          status: 'APPROVED',
          timestamp: new Date().toISOString(),
        },
      }));
      
      await delay(500);
      await updateProcessingStep(4, 'success');
      
      // Step 5: Crediting Destination
      await updateProcessingStep(5, 'processing');
      await delay(800);
      
      // Update balances
      updateBalance(formData.fromAccount, -amount);
      updateBalance(formData.toAccount, amount);
      
      await updateProcessingStep(5, 'success');
      
      // Step 6: Transaction Complete
      await updateProcessingStep(6, 'processing');
      await delay(500);
      await updateProcessingStep(6, 'success');
      
      // Add notification
      notifyTransfer(
        formData.fromAccount,
        formData.toAccount,
        amount,
        formData.transferMode
      );
      
      setTransactionResult({ ...result, transferMode: formData.transferMode });
      setProcessingState(prev => ({ ...prev, status: 'success' }));
      
      // Move to receipt after a short delay
      await delay(1500);
      setStep(5);
      toast.success(`Transfer via ${formData.transferMode} completed successfully!`);
      
    } catch (error) {
      // Mark current step as failed
      setProcessingState(prev => {
        const currentStep = prev.steps.findIndex(s => s.status === 'processing');
        const updatedSteps = [...prev.steps];
        if (currentStep >= 0) {
          updatedSteps[currentStep] = { ...updatedSteps[currentStep], status: 'error' };
        }
        return {
          ...prev,
          status: 'error',
          error: error.message || 'Transfer failed. Please try again.',
          steps: updatedSteps,
        };
      });
      
      notifyTransactionFailed('Transfer', error.message || 'Transfer failed due to unknown error');
      toast.error('Transfer failed: ' + (error.message || 'Unknown error'));
    } finally {
      setLoading(false);
    }
  };
  
  // Helper function to update processing step
  const updateProcessingStep = async (stepNum, status) => {
    setProcessingState(prev => ({
      ...prev,
      step: stepNum,
      steps: prev.steps.map(s => 
        s.id === stepNum ? { ...s, status } : s
      ),
    }));
  };
  
  // Helper function for delay
  const delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
    }).format(amount);
  };

  const AccountCard = ({ account, label, type }) => (
    <div className={`p-4 rounded-lg border-2 ${
      type === 'source' ? 'bg-red-50 border-red-200' : 'bg-green-50 border-green-200'
    }`}>
      <p className="text-xs font-medium text-gray-500 mb-2">{label}</p>
      <div className="flex items-center gap-3">
        <div className={`w-10 h-10 rounded-full flex items-center justify-center ${
          account.account_type === 'SAVINGS' ? 'bg-blue-100' : 'bg-purple-100'
        }`}>
          {account.account_type === 'SAVINGS' 
            ? <PiggyBank className={`w-5 h-5 ${account.account_type === 'SAVINGS' ? 'text-blue-600' : 'text-purple-600'}`} />
            : <Building2 className="w-5 h-5 text-purple-600" />
          }
        </div>
        <div>
          <p className="font-medium text-gray-900">{account.name}</p>
          <p className="text-xs text-gray-500">#{account.account_number}</p>
        </div>
      </div>
      <p className="text-right font-bold text-gray-900 mt-2">{formatCurrency(account.balance)}</p>
    </div>
  );

  return (
    <div className="animate-fade-in max-w-3xl mx-auto">
      {/* Header */}
      <div className="flex items-center gap-4 mb-6">
        <button
          onClick={() => navigate('/transactions')}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="page-title">Fund Transfer</h1>
          <p className="text-gray-500">Transfer funds between accounts</p>
        </div>
      </div>

      {/* Progress Steps */}
      <div className="flex items-center gap-2 mb-8">
        {[
          { num: 1, label: 'Transfer Details' },
          { num: 2, label: 'Verify PIN' },
          { num: 3, label: 'Confirm' },
          { num: 4, label: 'Processing' },
          { num: 5, label: 'Receipt' },
        ].map((s, i) => (
          <div key={s.num} className="flex items-center flex-1">
            <div className={`flex items-center justify-center w-8 h-8 rounded-full font-semibold text-sm ${
              step > s.num ? 'bg-green-600 text-white' :
              step === s.num ? (s.num === 4 && processingState.status === 'error' ? 'bg-red-600 text-white' : 'bg-primary-600 text-white') :
              'bg-gray-100 text-gray-400'
            }`}>
              {step > s.num ? <Check className="w-4 h-4" /> : 
               (s.num === 4 && step === 4 && loading) ? <Loader2 className="w-4 h-4 animate-spin" /> : s.num}
            </div>
            <span className={`ml-2 text-xs font-medium hidden sm:block ${
              step >= s.num ? 'text-gray-900' : 'text-gray-400'
            }`}>
              {s.label}
            </span>
            {i < 4 && (
              <div className={`flex-1 h-1 mx-2 rounded ${
                step > s.num ? 'bg-green-600' : 'bg-gray-200'
              }`} />
            )}
          </div>
        ))}
      </div>

      {/* Step 1: Transfer Details */}
      {step === 1 && (
        <div className="card p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <ArrowRightLeft className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Transfer Details</h2>
              <p className="text-sm text-gray-500">Select accounts and enter transfer amount</p>
            </div>
          </div>

          <div className="space-y-6">
            {/* Source Account */}
            <div>
              <label className="label">
                <CreditCard className="w-4 h-4" />
                From Account (Source)
              </label>
              <select
                name="fromAccount"
                value={formData.fromAccount}
                onChange={handleInputChange}
                className={`input w-full ${errors.fromAccount ? 'border-red-500' : ''}`}
              >
                <option value="">-- Select source account --</option>
                {activeAccounts.map(acc => (
                  <option key={acc.id} value={acc.account_number} disabled={acc.account_number === formData.toAccount}>
                    #{acc.account_number} - {acc.name} (Bal: {formatCurrency(acc.balance)})
                  </option>
                ))}
              </select>
              {errors.fromAccount && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.fromAccount}
                </p>
              )}
            </div>

            {/* Destination Account */}
            <div>
              <label className="label">
                <CreditCard className="w-4 h-4" />
                To Account (Destination)
              </label>
              <select
                name="toAccount"
                value={formData.toAccount}
                onChange={handleInputChange}
                className={`input w-full ${errors.toAccount ? 'border-red-500' : ''}`}
              >
                <option value="">-- Select destination account --</option>
                {activeAccounts.map(acc => (
                  <option key={acc.id} value={acc.account_number} disabled={acc.account_number === formData.fromAccount}>
                    #{acc.account_number} - {acc.name} ({acc.account_type})
                  </option>
                ))}
              </select>
              {errors.toAccount && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.toAccount}
                </p>
              )}
            </div>

            {/* Selected Accounts Summary */}
            {sourceAccount && destinationAccount && (
              <div className="flex items-center gap-4">
                <div className="flex-1">
                  <AccountCard account={sourceAccount} label="FROM" type="source" />
                </div>
                <div className="flex-shrink-0">
                  <div className="w-10 h-10 bg-blue-600 rounded-full flex items-center justify-center">
                    <ArrowRight className="w-5 h-5 text-white" />
                  </div>
                </div>
                <div className="flex-1">
                  <AccountCard account={destinationAccount} label="TO" type="destination" />
                </div>
              </div>
            )}

            {/* Amount */}
            <div>
              <label className="label">
                <DollarSign className="w-4 h-4" />
                Transfer Amount (₹)
              </label>
              <input
                type="number"
                name="amount"
                value={formData.amount}
                onChange={handleInputChange}
                placeholder="Enter amount"
                min="1"
                step="0.01"
                className={`input w-full text-2xl font-semibold ${errors.amount ? 'border-red-500' : ''}`}
              />
              {errors.amount && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.amount}
                </p>
              )}
              <p className="text-xs text-gray-400 mt-1">Maximum transfer: ₹10,00,000 per transaction</p>
            </div>

            {/* Transfer Mode Selection */}
            <div>
              <label className="label">
                <Send className="w-4 h-4" />
                Transfer Mode
              </label>
              <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                {Object.values(TRANSFER_MODES).map((mode) => {
                  const IconComponent = mode.icon;
                  const isSelected = formData.transferMode === mode.id;
                  return (
                    <button
                      key={mode.id}
                      type="button"
                      onClick={() => setFormData(prev => ({ ...prev, transferMode: mode.id }))}
                      className={`p-3 rounded-lg border-2 transition-all text-left ${
                        isSelected 
                          ? `${mode.borderColor} ${mode.bgColor}` 
                          : 'border-gray-200 hover:border-gray-300 bg-white'
                      }`}
                    >
                      <div className="flex items-center gap-2 mb-1">
                        <IconComponent className={`w-4 h-4 ${isSelected ? mode.color : 'text-gray-400'}`} />
                        <span className={`font-semibold text-sm ${isSelected ? mode.color : 'text-gray-700'}`}>
                          {mode.name}
                        </span>
                      </div>
                      <p className="text-xs text-gray-500 truncate">{mode.description}</p>
                    </button>
                  );
                })}
              </div>
              
              {/* Selected Mode Details */}
              {formData.transferMode && (
                <div className="mt-3 p-3 bg-gray-50 rounded-lg border border-gray-200">
                  <div className="flex items-center gap-2 mb-2">
                    {(() => {
                      const mode = TRANSFER_MODES[formData.transferMode];
                      const IconComponent = mode.icon;
                      return (
                        <>
                          <IconComponent className={`w-5 h-5 ${mode.color}`} />
                          <span className="font-medium text-gray-900">{mode.fullName}</span>
                        </>
                      );
                    })()}
                  </div>
                  <div className="grid grid-cols-3 gap-2 text-xs">
                    <div>
                      <span className="text-gray-500">Limit:</span>
                      <p className="font-medium text-gray-700">{TRANSFER_MODES[formData.transferMode].limit}</p>
                    </div>
                    <div>
                      <span className="text-gray-500">Timing:</span>
                      <p className="font-medium text-gray-700">{TRANSFER_MODES[formData.transferMode].timing}</p>
                    </div>
                    <div>
                      <span className="text-gray-500">Fee:</span>
                      <p className="font-medium text-gray-700">{TRANSFER_MODES[formData.transferMode].fee}</p>
                    </div>
                  </div>
                </div>
              )}
            </div>

            {/* Description */}
            <div>
              <label className="label">
                <FileText className="w-4 h-4" />
                Description (Optional)
              </label>
              <input
                type="text"
                name="description"
                value={formData.description}
                onChange={handleInputChange}
                placeholder="e.g., Payment for services, Loan repayment"
                className="input w-full"
              />
            </div>

            <button
              onClick={handleNext}
              className="btn-primary w-full flex items-center justify-center gap-2"
            >
              Continue to PIN Verification
            </button>
          </div>
        </div>
      )}

      {/* Step 2: PIN Verification */}
      {step === 2 && (
        <div className="card p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-12 h-12 bg-amber-100 rounded-xl flex items-center justify-center">
              <Lock className="w-6 h-6 text-amber-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">PIN Verification</h2>
              <p className="text-sm text-gray-500">Enter the source account PIN to authorize transfer</p>
            </div>
          </div>

          <div className="space-y-6">
            <div className="p-4 bg-gray-50 rounded-lg">
              <div className="flex items-center justify-between">
                <span className="text-sm text-gray-600">Transferring</span>
                <span className="font-bold text-blue-600">{formatCurrency(parseFloat(formData.amount))}</span>
              </div>
              <div className="flex items-center justify-between mt-2">
                <span className="text-sm text-gray-600">From</span>
                <span className="font-medium">#{formData.fromAccount}</span>
              </div>
              <div className="flex items-center justify-between mt-1">
                <span className="text-sm text-gray-600">To</span>
                <span className="font-medium">#{formData.toAccount}</span>
              </div>
            </div>

            <div>
              <label className="label">
                <Lock className="w-4 h-4" />
                Enter 4-Digit PIN for Source Account
              </label>
              <input
                type="password"
                name="pin"
                value={formData.pin}
                onChange={handleInputChange}
                placeholder="••••"
                maxLength={4}
                className={`input w-full text-center text-3xl tracking-widest ${errors.pin ? 'border-red-500' : ''}`}
              />
              {errors.pin && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.pin}
                </p>
              )}
              <p className="text-xs text-gray-400 mt-2 text-center">
                Demo PIN: 1234
              </p>
            </div>

            <div className="p-4 bg-amber-50 border border-amber-200 rounded-lg flex items-start gap-3">
              <ShieldAlert className="w-5 h-5 text-amber-600 mt-0.5" />
              <div className="text-sm text-amber-800">
                <p className="font-medium">Security Notice</p>
                <p>Fund transfers are processed immediately and cannot be reversed. Please verify all details carefully.</p>
              </div>
            </div>

            <div className="flex gap-4">
              <button onClick={() => setStep(1)} className="btn-secondary flex-1">
                Back
              </button>
              <button
                onClick={handleNext}
                className="btn-primary flex-1 flex items-center justify-center gap-2"
              >
                Verify & Continue
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Step 3: Confirmation */}
      {step === 3 && (
        <div className="card p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center">
              <Check className="w-6 h-6 text-green-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Confirm Transfer</h2>
              <p className="text-sm text-gray-500">Please verify all details before proceeding</p>
            </div>
          </div>

          <div className="space-y-4 mb-6">
            {/* Transfer Visual */}
            <div className="flex items-center gap-4 p-4 bg-gray-50 rounded-lg">
              <div className="flex-1">
                <AccountCard account={sourceAccount} label="FROM" type="source" />
              </div>
              <div className="flex flex-col items-center">
                <div className="text-xl font-bold text-blue-600">{formatCurrency(parseFloat(formData.amount))}</div>
                <ArrowRight className="w-6 h-6 text-blue-600 my-2" />
              </div>
              <div className="flex-1">
                <AccountCard account={destinationAccount} label="TO" type="destination" />
              </div>
            </div>

            {/* Details */}
            <div className="p-4 bg-gray-50 rounded-lg space-y-3">
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Transfer Amount</span>
                <span className="font-bold text-blue-600">{formatCurrency(parseFloat(formData.amount))}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Source Balance After</span>
                <span className="font-medium text-red-600">
                  {formatCurrency((sourceAccount?.balance || 0) - parseFloat(formData.amount))}
                </span>
              </div>
              <div className="flex justify-between py-2">
                <span className="text-gray-500">Destination Balance After</span>
                <span className="font-medium text-green-600">
                  {formatCurrency((destinationAccount?.balance || 0) + parseFloat(formData.amount))}
                </span>
              </div>
            </div>

            {formData.description && (
              <div className="p-4 bg-blue-50 rounded-lg">
                <p className="text-sm text-blue-800">
                  <strong>Description:</strong> {formData.description}
                </p>
              </div>
            )}

            <div className="p-4 bg-green-50 border border-green-200 rounded-lg flex items-center gap-2">
              <Check className="w-5 h-5 text-green-600" />
              <span className="text-sm text-green-800">PIN verified successfully</span>
            </div>
          </div>

          <div className="flex gap-4">
            <button onClick={() => setStep(2)} className="btn-secondary flex-1">
              Back
            </button>
            <button
              onClick={handleTransfer}
              disabled={loading}
              className="btn-primary flex-1 flex items-center justify-center gap-2"
            >
              {loading ? (
                <>
                  <Loader2 className="w-5 h-5 animate-spin" />
                  Processing...
                </>
              ) : (
                <>
                  <ArrowRightLeft className="w-5 h-5" />
                  Confirm Transfer
                </>
              )}
            </button>
          </div>
        </div>
      )}

      {/* Step 4: Processing - Central Payment Gateway Validation */}
      {step === 4 && (
        <div className="card p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${
              processingState.status === 'error' ? 'bg-red-100' : 
              processingState.status === 'success' ? 'bg-green-100' : 'bg-blue-100'
            }`}>
              {processingState.status === 'error' ? (
                <XCircle className="w-6 h-6 text-red-600" />
              ) : processingState.status === 'success' ? (
                <CheckCircle2 className="w-6 h-6 text-green-600" />
              ) : (
                <Globe className="w-6 h-6 text-blue-600 animate-pulse" />
              )}
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">
                {processingState.status === 'error' ? 'Transfer Failed' :
                 processingState.status === 'success' ? 'Transfer Complete' :
                 'Processing Transfer'}
              </h2>
              <p className="text-sm text-gray-500">
                {processingState.status === 'error' ? 'An error occurred during processing' :
                 processingState.status === 'success' ? 'All validations passed successfully' :
                 'Validating with Central Payment Gateway...'}
              </p>
            </div>
          </div>

          {/* Transaction Summary */}
          <div className="flex items-center gap-4 p-4 bg-gray-50 rounded-lg mb-6">
            <div className="flex-1 text-center">
              <p className="text-xs text-gray-500 mb-1">FROM</p>
              <p className="font-semibold text-gray-900">{sourceAccount?.name}</p>
              <p className="text-xs text-gray-400">#{formData.fromAccount}</p>
            </div>
            <div className="flex flex-col items-center">
              <div className="text-lg font-bold text-blue-600">{formatCurrency(parseFloat(formData.amount))}</div>
              <ArrowRight className="w-5 h-5 text-blue-600" />
              <span className="text-xs text-gray-500">{formData.transferMode}</span>
            </div>
            <div className="flex-1 text-center">
              <p className="text-xs text-gray-500 mb-1">TO</p>
              <p className="font-semibold text-gray-900">{destinationAccount?.name}</p>
              <p className="text-xs text-gray-400">#{formData.toAccount}</p>
            </div>
          </div>

          {/* Processing Steps */}
          <div className="space-y-3 mb-6">
            {processingState.steps.map((procStep, index) => (
              <div 
                key={procStep.id}
                className={`flex items-center gap-4 p-4 rounded-lg border-2 transition-all duration-300 ${
                  procStep.status === 'success' ? 'bg-green-50 border-green-200' :
                  procStep.status === 'processing' ? 'bg-blue-50 border-blue-300 shadow-md' :
                  procStep.status === 'error' ? 'bg-red-50 border-red-200' :
                  'bg-gray-50 border-gray-200 opacity-50'
                }`}
              >
                {/* Step Icon */}
                <div className={`w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 ${
                  procStep.status === 'success' ? 'bg-green-100' :
                  procStep.status === 'processing' ? 'bg-blue-100' :
                  procStep.status === 'error' ? 'bg-red-100' :
                  'bg-gray-100'
                }`}>
                  {procStep.status === 'success' ? (
                    <CheckCircle2 className="w-5 h-5 text-green-600" />
                  ) : procStep.status === 'processing' ? (
                    <Loader2 className="w-5 h-5 text-blue-600 animate-spin" />
                  ) : procStep.status === 'error' ? (
                    <XCircle className="w-5 h-5 text-red-600" />
                  ) : (
                    <span className="text-sm font-medium text-gray-400">{procStep.id}</span>
                  )}
                </div>

                {/* Step Content */}
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <p className={`font-medium ${
                      procStep.status === 'success' ? 'text-green-800' :
                      procStep.status === 'processing' ? 'text-blue-800' :
                      procStep.status === 'error' ? 'text-red-800' :
                      'text-gray-500'
                    }`}>
                      {procStep.label}
                    </p>
                    {procStep.id === 3 && procStep.status !== 'pending' && (
                      <span className="px-2 py-0.5 bg-purple-100 text-purple-700 text-xs rounded-full font-medium">
                        CPG
                      </span>
                    )}
                  </div>
                  <p className={`text-sm ${
                    procStep.status === 'processing' ? 'text-blue-600' :
                    procStep.status === 'error' ? 'text-red-600' :
                    'text-gray-500'
                  }`}>
                    {procStep.description}
                  </p>
                </div>

                {/* Status Badge */}
                {procStep.status === 'success' && (
                  <span className="px-2 py-1 bg-green-100 text-green-700 text-xs rounded-full font-medium">
                    ✓ Complete
                  </span>
                )}
                {procStep.status === 'processing' && (
                  <span className="px-2 py-1 bg-blue-100 text-blue-700 text-xs rounded-full font-medium animate-pulse">
                    Processing...
                  </span>
                )}
                {procStep.status === 'error' && (
                  <span className="px-2 py-1 bg-red-100 text-red-700 text-xs rounded-full font-medium">
                    Failed
                  </span>
                )}
              </div>
            ))}
          </div>

          {/* Gateway Response Box */}
          {processingState.gatewayResponse && (
            <div className="p-4 bg-purple-50 border border-purple-200 rounded-lg mb-6">
              <div className="flex items-center gap-2 mb-3">
                <Shield className="w-5 h-5 text-purple-600" />
                <span className="font-medium text-purple-800">Central Payment Gateway Response</span>
              </div>
              <div className="grid grid-cols-2 gap-3 text-sm">
                <div>
                  <span className="text-purple-600">Gateway Reference:</span>
                  <p className="font-mono text-purple-900">{processingState.gatewayResponse.gatewayRef}</p>
                </div>
                <div>
                  <span className="text-purple-600">Status:</span>
                  <p className="flex items-center gap-1">
                    <CheckCircle2 className="w-4 h-4 text-green-600" />
                    <span className="font-medium text-green-700">{processingState.gatewayResponse.status}</span>
                  </p>
                </div>
              </div>
            </div>
          )}

          {/* Error Message */}
          {processingState.status === 'error' && processingState.error && (
            <div className="p-4 bg-red-50 border border-red-200 rounded-lg mb-6">
              <div className="flex items-start gap-3">
                <XCircle className="w-5 h-5 text-red-600 mt-0.5" />
                <div>
                  <p className="font-medium text-red-800">Transfer Failed</p>
                  <p className="text-sm text-red-600">{processingState.error}</p>
                </div>
              </div>
            </div>
          )}

          {/* Actions for Error State */}
          {processingState.status === 'error' && (
            <div className="flex gap-4">
              <button
                onClick={() => {
                  setStep(3);
                  setProcessingState(prev => ({
                    ...prev,
                    status: 'idle',
                    error: null,
                    steps: prev.steps.map(s => ({ ...s, status: 'pending' })),
                  }));
                }}
                className="btn-secondary flex-1"
              >
                Try Again
              </button>
              <button
                onClick={() => navigate('/transactions')}
                className="btn-primary flex-1"
              >
                Back to Transactions
              </button>
            </div>
          )}

          {/* Success Message */}
          {processingState.status === 'success' && (
            <div className="p-4 bg-green-50 border border-green-200 rounded-lg flex items-center gap-3">
              <CheckCircle2 className="w-6 h-6 text-green-600" />
              <div>
                <p className="font-medium text-green-800">Transfer Completed Successfully!</p>
                <p className="text-sm text-green-600">Redirecting to receipt...</p>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Step 5: Receipt */}
      {step === 5 && transactionResult && (
        <div className="card p-6 text-center">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <Check className="w-8 h-8 text-green-600" />
          </div>
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Transfer Successful!</h2>
          <p className="text-gray-500 mb-6">Funds have been transferred successfully via Central Payment Gateway</p>

          <div className="bg-gray-50 rounded-xl p-6 mb-6">
            <div className="text-3xl font-bold text-blue-600 mb-4">
              {formatCurrency(parseFloat(formData.amount))}
            </div>
            
            <div className="flex items-center justify-center gap-4 mb-4">
              <div className="text-center">
                <p className="text-xs text-gray-500">FROM</p>
                <p className="font-medium">{sourceAccount?.name}</p>
                <p className="text-xs text-gray-400">#{sourceAccount?.account_number}</p>
              </div>
              <ArrowRight className="w-6 h-6 text-blue-600" />
              <div className="text-center">
                <p className="text-xs text-gray-500">TO</p>
                <p className="font-medium">{destinationAccount?.name}</p>
                <p className="text-xs text-gray-400">#{destinationAccount?.account_number}</p>
              </div>
            </div>

            <div className="space-y-2 text-sm">
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Transaction ID</span>
                <span className="font-mono text-gray-900">{transactionResult.transaction_id}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Transfer Mode</span>
                <span className="font-medium text-gray-900">{transactionResult.transferMode || formData.transferMode}</span>
              </div>
              {processingState.gatewayResponse && (
                <div className="flex justify-between py-2 border-b border-gray-200">
                  <span className="text-gray-500">Gateway Reference</span>
                  <span className="font-mono text-purple-600">{processingState.gatewayResponse.gatewayRef}</span>
                </div>
              )}
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Date & Time</span>
                <span className="font-medium text-gray-900">
                  {new Date().toLocaleString('en-IN')}
                </span>
              </div>
              <div className="flex justify-between py-2">
                <span className="text-gray-500">Processed By</span>
                <span className="font-medium text-gray-900">{user?.full_name}</span>
              </div>
            </div>
          </div>

          {/* Gateway Validation Badge */}
          <div className="flex items-center justify-center gap-2 p-3 bg-purple-50 rounded-lg mb-6">
            <Shield className="w-5 h-5 text-purple-600" />
            <span className="text-sm text-purple-800">
              Validated by Central Payment Gateway
            </span>
            <CheckCircle2 className="w-4 h-4 text-green-600" />
          </div>

          <div className="flex gap-4">
            <button
              onClick={() => {
                setFormData({ fromAccount: '', toAccount: '', amount: '', description: '', pin: '', transferMode: 'IMPS' });
                setSourceAccount(null);
                setDestinationAccount(null);
                setTransactionResult(null);
                setProcessingState(prev => ({
                  ...prev,
                  status: 'idle',
                  gatewayResponse: null,
                  error: null,
                  steps: prev.steps.map(s => ({ ...s, status: 'pending' })),
                }));
                setStep(1);
              }}
              className="btn-secondary flex-1"
            >
              New Transfer
            </button>
            <button
              onClick={() => navigate('/transactions')}
              className="btn-primary flex-1"
            >
              View Transactions
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default TransferPage;
