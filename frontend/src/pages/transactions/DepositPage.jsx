import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useTransactionStore } from '../../store/transactionStore';
import { useAuthStore } from '../../store/authStore';
import { useNotificationStore } from '../../store/notificationStore';
import {
  ArrowLeft,
  ArrowDownRight,
  CreditCard,
  DollarSign,
  FileText,
  Check,
  AlertCircle,
  Loader2,
  User,
  Lock,
  ShieldAlert,
} from 'lucide-react';
import toast from 'react-hot-toast';

const DepositPage = () => {
  const navigate = useNavigate();
  const { user, hasRole } = useAuthStore();
  const { accounts, fetchAccounts, getAccountByNumber, updateBalance, verifyPin } = useAccountStore();
  const { processDeposit } = useTransactionStore();
  const { notifyDeposit, notifyTransactionFailed } = useNotificationStore();

  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState({
    accountNumber: '',
    amount: '',
    description: '',
    depositorName: '',
    depositorPhone: '',
    pin: '',
  });
  const [errors, setErrors] = useState({});
  const [selectedAccount, setSelectedAccount] = useState(null);
  const [transactionResult, setTransactionResult] = useState(null);

  // Fetch accounts on mount
  useEffect(() => {
    fetchAccounts().catch(err => console.error('Failed to fetch accounts:', err));
  }, [fetchAccounts]);

  // Check if user has access
  useEffect(() => {
    if (!hasRole(['TELLER'])) {
      toast.error('You do not have permission to perform deposits');
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

    if (name === 'accountNumber') {
      const accNum = parseInt(value, 10);
      if (!isNaN(accNum)) {
        const account = findLocalAccount(accNum);
        if (account && account.is_active) {
          setSelectedAccount(account);
        } else {
          setSelectedAccount(null);
        }
      } else {
        setSelectedAccount(null);
      }
    }
  };

  const validateStep1 = () => {
    const newErrors = {};
    if (!formData.accountNumber) {
      newErrors.accountNumber = 'Please select or enter an account number';
    } else if (!selectedAccount) {
      newErrors.accountNumber = 'Account not found or inactive';
    }

    if (!formData.amount) {
      newErrors.amount = 'Amount is required';
    } else if (parseFloat(formData.amount) <= 0) {
      newErrors.amount = 'Amount must be greater than 0';
    } else if (parseFloat(formData.amount) > 10000000) {
      newErrors.amount = 'Maximum deposit amount is ₹1,00,00,000';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const validateStep2 = async () => {
    const newErrors = {};
    if (!formData.pin) {
      newErrors.pin = 'PIN is required for deposit';
    } else if (formData.pin.length !== 4) {
      newErrors.pin = 'PIN must be 4 digits';
    } else {
      // Verify PIN with backend
      try {
        const isValid = await verifyPin(formData.accountNumber, formData.pin);
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

  const handleDeposit = async () => {
    setLoading(true);
    try {
      // Use Math.round to avoid floating-point precision issues with currency
      const amount = Math.round(parseFloat(formData.amount) * 100) / 100;
      
      const result = await processDeposit(
        formData.accountNumber,
        amount,
        formData.description || `Cash deposit by ${formData.depositorName || 'Customer'}`,
        formData.pin
      );

      // Update account balance
      const newBalance = selectedAccount.balance + amount;
      updateBalance(formData.accountNumber, amount);

      // Send notification
      notifyDeposit(formData.accountNumber, amount, newBalance);

      setTransactionResult(result);
      setStep(4);
      toast.success('Deposit processed successfully!');
    } catch (error) {
      notifyTransactionFailed('Deposit', error.message || 'Deposit failed due to unknown error');
      toast.error(error.message || 'Failed to process deposit');
    } finally {
      setLoading(false);
    }
  };

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
    }).format(amount);
  };

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
          <h1 className="page-title">Cash Deposit</h1>
          <p className="text-gray-500">Deposit funds into an account</p>
        </div>
      </div>

      {/* Progress Steps */}
      <div className="flex items-center gap-2 mb-8">
        {[
          { num: 1, label: 'Account & Amount' },
          { num: 2, label: 'Verify PIN' },
          { num: 3, label: 'Confirm' },
          { num: 4, label: 'Receipt' },
        ].map((s, i) => (
          <div key={s.num} className="flex items-center flex-1">
            <div className={`flex items-center justify-center w-8 h-8 rounded-full font-semibold text-sm ${
              step > s.num ? 'bg-green-600 text-white' :
              step === s.num ? 'bg-primary-600 text-white' :
              'bg-gray-100 text-gray-400'
            }`}>
              {step > s.num ? <Check className="w-4 h-4" /> : s.num}
            </div>
            <span className={`ml-2 text-xs font-medium hidden sm:block ${
              step >= s.num ? 'text-gray-900' : 'text-gray-400'
            }`}>
              {s.label}
            </span>
            {i < 3 && (
              <div className={`flex-1 h-1 mx-2 rounded ${
                step > s.num ? 'bg-green-600' : 'bg-gray-200'
              }`} />
            )}
          </div>
        ))}
      </div>

      {/* Step 1: Account & Amount */}
      {step === 1 && (
        <div className="card p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center">
              <ArrowDownRight className="w-6 h-6 text-green-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Deposit Details</h2>
              <p className="text-sm text-gray-500">Enter the account and amount for deposit</p>
            </div>
          </div>

          <div className="space-y-6">
            {/* Account Selection */}
            <div>
              <label className="label">
                <CreditCard className="w-4 h-4" />
                Select Account or Enter Account Number
              </label>
              <select
                name="accountNumber"
                value={formData.accountNumber}
                onChange={handleInputChange}
                className={`input w-full ${errors.accountNumber ? 'border-red-500' : ''}`}
              >
                <option value="">-- Select an account --</option>
                {activeAccounts.map(acc => (
                  <option key={acc.id} value={acc.account_number}>
                    #{acc.account_number} - {acc.name} ({acc.account_type})
                  </option>
                ))}
              </select>
              {errors.accountNumber && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.accountNumber}
                </p>
              )}
            </div>

            {/* Selected Account Info */}
            {selectedAccount && (
              <div className="p-4 bg-green-50 border border-green-200 rounded-lg">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 bg-green-100 rounded-full flex items-center justify-center">
                    <User className="w-5 h-5 text-green-600" />
                  </div>
                  <div>
                    <p className="font-medium text-gray-900">{selectedAccount.name}</p>
                    <p className="text-sm text-gray-500">
                      {selectedAccount.account_type} Account • Balance: {formatCurrency(selectedAccount.balance)}
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Amount */}
            <div>
              <label className="label">
                <DollarSign className="w-4 h-4" />
                Deposit Amount (₹)
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
                placeholder="e.g., Salary deposit, Cash deposit"
                className="input w-full"
              />
            </div>

            {/* Depositor Details */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="label">Depositor Name</label>
                <input
                  type="text"
                  name="depositorName"
                  value={formData.depositorName}
                  onChange={handleInputChange}
                  placeholder="Name of person depositing"
                  className="input w-full"
                />
              </div>
              <div>
                <label className="label">Depositor Phone</label>
                <input
                  type="tel"
                  name="depositorPhone"
                  value={formData.depositorPhone}
                  onChange={handleInputChange}
                  placeholder="Phone number"
                  className="input w-full"
                />
              </div>
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
              <p className="text-sm text-gray-500">Enter the account PIN to authorize deposit</p>
            </div>
          </div>

          <div className="space-y-6">
            <div className="p-4 bg-gray-50 rounded-lg">
              <p className="text-sm text-gray-600">
                Depositing <span className="font-bold text-green-600">+{formatCurrency(parseFloat(formData.amount))}</span> to account <span className="font-medium">#{formData.accountNumber}</span>
              </p>
              <p className="text-sm text-gray-500 mt-1">Account Holder: {selectedAccount?.name}</p>
            </div>

            <div>
              <label className="label">
                <Lock className="w-4 h-4" />
                Enter 4-Digit PIN
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
                <p>PIN verification is required for all transactions.</p>
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
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <AlertCircle className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Confirm Deposit</h2>
              <p className="text-sm text-gray-500">Please verify all details before proceeding</p>
            </div>
          </div>

          <div className="space-y-4 mb-6">
            <div className="p-4 bg-gray-50 rounded-lg space-y-3">
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Account Number</span>
                <span className="font-medium text-gray-900">#{selectedAccount?.account_number}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Account Holder</span>
                <span className="font-medium text-gray-900">{selectedAccount?.name}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Account Type</span>
                <span className="font-medium text-gray-900">{selectedAccount?.account_type}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Current Balance</span>
                <span className="font-medium text-gray-900">{formatCurrency(selectedAccount?.balance || 0)}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Deposit Amount</span>
                <span className="font-bold text-green-600 text-xl">+{formatCurrency(parseFloat(formData.amount))}</span>
              </div>
              <div className="flex justify-between py-2">
                <span className="text-gray-500">New Balance</span>
                <span className="font-bold text-gray-900 text-xl">
                  {formatCurrency((selectedAccount?.balance || 0) + parseFloat(formData.amount))}
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

            {formData.depositorName && (
              <div className="p-4 bg-gray-50 rounded-lg">
                <p className="text-sm text-gray-600">
                  <strong>Deposited by:</strong> {formData.depositorName}
                  {formData.depositorPhone && ` (${formData.depositorPhone})`}
                </p>
              </div>
            )}

            <div className="p-4 bg-green-50 border border-green-200 rounded-lg flex items-center gap-2">
              <Check className="w-5 h-5 text-green-600" />
              <span className="text-sm text-green-800">PIN verified successfully</span>
            </div>
          </div>

          <div className="flex gap-4">
            <button
              onClick={() => setStep(2)}
              className="btn-secondary flex-1"
            >
              Back
            </button>
            <button
              onClick={handleDeposit}
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
                  <Check className="w-5 h-5" />
                  Confirm Deposit
                </>
              )}
            </button>
          </div>
        </div>
      )}

      {/* Step 4: Receipt */}
      {step === 4 && transactionResult && (
        <div className="card p-6 text-center">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <Check className="w-8 h-8 text-green-600" />
          </div>
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Deposit Successful!</h2>
          <p className="text-gray-500 mb-6">The funds have been credited to the account</p>

          <div className="bg-gray-50 rounded-xl p-6 mb-6">
            <div className="text-4xl font-bold text-green-600 mb-4">
              +{formatCurrency(parseFloat(formData.amount))}
            </div>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Transaction ID</span>
                <span className="font-mono text-gray-900">{transactionResult.transaction_id}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Account</span>
                <span className="font-medium text-gray-900">#{selectedAccount?.account_number}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Beneficiary</span>
                <span className="font-medium text-gray-900">{selectedAccount?.name}</span>
              </div>
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

          <div className="flex gap-4">
            <button
              onClick={() => {
                setFormData({
                  accountNumber: '',
                  amount: '',
                  description: '',
                  depositorName: '',
                  depositorPhone: '',
                  pin: '',
                });
                setSelectedAccount(null);
                setTransactionResult(null);
                setStep(1);
              }}
              className="btn-secondary flex-1"
            >
              New Deposit
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

export default DepositPage;
