import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useTransactionStore } from '../../store/transactionStore';
import { useAuthStore } from '../../store/authStore';
import { useNotificationStore } from '../../store/notificationStore';
import {
  ArrowLeft,
  ArrowUpRight,
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

const WithdrawPage = () => {
  const navigate = useNavigate();
  const { user, hasRole } = useAuthStore();
  const { accounts, fetchAccounts, getAccountByNumber, updateBalance, verifyPin } = useAccountStore();
  const { processWithdrawal } = useTransactionStore();
  const { notifyWithdrawal, notifyTransactionFailed } = useNotificationStore();

  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState({
    accountNumber: '',
    amount: '',
    description: '',
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
      toast.error('You do not have permission to perform withdrawals');
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
    } else if (selectedAccount && parseFloat(formData.amount) > selectedAccount.balance) {
      newErrors.amount = 'Insufficient balance';
    } else if (parseFloat(formData.amount) > 200000) {
      newErrors.amount = 'Maximum withdrawal limit is ₹2,00,000 per transaction';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const validateStep2 = async () => {
    const newErrors = {};
    if (!formData.pin) {
      newErrors.pin = 'PIN is required for withdrawal';
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

  const handleWithdraw = async () => {
    setLoading(true);
    try {
      // Use Math.round to avoid floating-point precision issues with currency
      const amount = Math.round(parseFloat(formData.amount) * 100) / 100;
      
      const result = await processWithdrawal(
        formData.accountNumber,
        amount,
        formData.description || 'Cash withdrawal',
        formData.pin
      );

      // Update account balance
      const newBalance = selectedAccount.balance - amount;
      updateBalance(formData.accountNumber, -amount);

      // Send notification
      notifyWithdrawal(formData.accountNumber, amount, newBalance);

      setTransactionResult(result);
      setStep(4);
      toast.success('Withdrawal processed successfully!');
    } catch (error) {
      notifyTransactionFailed('Withdrawal', error.message || 'Withdrawal failed due to unknown error');
      toast.error(error.message || 'Failed to process withdrawal');
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
          <h1 className="page-title">Cash Withdrawal</h1>
          <p className="text-gray-500">Withdraw funds from an account</p>
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
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <ArrowUpRight className="w-6 h-6 text-red-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Withdrawal Details</h2>
              <p className="text-sm text-gray-500">Select account and enter withdrawal amount</p>
            </div>
          </div>

          <div className="space-y-6">
            {/* Account Selection */}
            <div>
              <label className="label">
                <CreditCard className="w-4 h-4" />
                Select Account
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
                    #{acc.account_number} - {acc.name} (Bal: {formatCurrency(acc.balance)})
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
              <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center">
                      <User className="w-5 h-5 text-blue-600" />
                    </div>
                    <div>
                      <p className="font-medium text-gray-900">{selectedAccount.name}</p>
                      <p className="text-sm text-gray-500">{selectedAccount.account_type} Account</p>
                    </div>
                  </div>
                  <div className="text-right">
                    <p className="text-sm text-gray-500">Available Balance</p>
                    <p className="text-xl font-bold text-gray-900">{formatCurrency(selectedAccount.balance)}</p>
                  </div>
                </div>
              </div>
            )}

            {/* Amount */}
            <div>
              <label className="label">
                <DollarSign className="w-4 h-4" />
                Withdrawal Amount (₹)
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
              <p className="text-xs text-gray-400 mt-1">Maximum withdrawal: ₹2,00,000 per transaction</p>
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
                placeholder="e.g., Cash withdrawal, ATM withdrawal"
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
              <p className="text-sm text-gray-500">Enter the 4-digit PIN to authorize withdrawal</p>
            </div>
          </div>

          <div className="space-y-6">
            <div className="p-4 bg-gray-50 rounded-lg">
              <p className="text-sm text-gray-600">
                Withdrawing <span className="font-bold text-red-600">{formatCurrency(parseFloat(formData.amount))}</span> from account <span className="font-medium">#{formData.accountNumber}</span>
              </p>
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
                <p>Never share your PIN with anyone. Bank staff will never ask for your PIN.</p>
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
              <h2 className="text-lg font-semibold text-gray-900">Confirm Withdrawal</h2>
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
                <span className="text-gray-500">Current Balance</span>
                <span className="font-medium text-gray-900">{formatCurrency(selectedAccount?.balance || 0)}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-200">
                <span className="text-gray-500">Withdrawal Amount</span>
                <span className="font-bold text-red-600 text-xl">-{formatCurrency(parseFloat(formData.amount))}</span>
              </div>
              <div className="flex justify-between py-2">
                <span className="text-gray-500">New Balance</span>
                <span className="font-bold text-gray-900 text-xl">
                  {formatCurrency((selectedAccount?.balance || 0) - parseFloat(formData.amount))}
                </span>
              </div>
            </div>

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
              onClick={handleWithdraw}
              disabled={loading}
              className="btn-danger flex-1 flex items-center justify-center gap-2"
            >
              {loading ? (
                <>
                  <Loader2 className="w-5 h-5 animate-spin" />
                  Processing...
                </>
              ) : (
                <>
                  <ArrowUpRight className="w-5 h-5" />
                  Confirm Withdrawal
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
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Withdrawal Successful!</h2>
          <p className="text-gray-500 mb-6">Please collect your cash from the counter</p>

          <div className="bg-gray-50 rounded-xl p-6 mb-6">
            <div className="text-4xl font-bold text-red-600 mb-4">
              -{formatCurrency(parseFloat(formData.amount))}
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
                <span className="text-gray-500">Remaining Balance</span>
                <span className="font-bold text-gray-900">
                  {formatCurrency((selectedAccount?.balance || 0) - parseFloat(formData.amount))}
                </span>
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
                setFormData({ accountNumber: '', amount: '', description: '', pin: '' });
                setSelectedAccount(null);
                setTransactionResult(null);
                setStep(1);
              }}
              className="btn-secondary flex-1"
            >
              New Withdrawal
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

export default WithdrawPage;
