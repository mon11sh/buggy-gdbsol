import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useTransactionStore } from '../../store/transactionStore';
import { useAuthStore } from '../../store/authStore';
import {
  ArrowLeft,
  CreditCard,
  PiggyBank,
  Building2,
  User,
  Phone,
  Calendar,
  Shield,
  Globe,
  FileText,
  ArrowDownRight,
  ArrowUpRight,
  ArrowRightLeft,
  Edit,
  Power,
  PowerOff,
  MoreVertical,
  Clock,
  CheckCircle,
  XCircle,
} from 'lucide-react';
import { format } from 'date-fns';
import toast from 'react-hot-toast';

const AccountDetailsPage = () => {
  const { accountNumber } = useParams();
  const navigate = useNavigate();
  const { hasRole } = useAuthStore();
  const { getAccountByNumber, activateAccount, deactivateAccount, closeAccount } = useAccountStore();
  const { getTransactionsForAccount } = useTransactionStore();

  const [account, setAccount] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [activeTab, setActiveTab] = useState('overview');
  const [showActions, setShowActions] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchAccount = async () => {
      setLoading(true);
      try {
        console.log('Fetching account:', accountNumber);
        const acc = await getAccountByNumber(accountNumber);
        console.log('Account data received:', acc);
        if (acc) {
          setAccount(acc);
          setTransactions(getTransactionsForAccount(acc.account_number));
        }
      } catch (error) {
        console.error('Error fetching account:', error);
      } finally {
        setLoading(false);
      }
    };
    
    fetchAccount();
  }, [accountNumber, getAccountByNumber, getTransactionsForAccount]);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
          <p className="text-gray-500">Loading account details...</p>
        </div>
      </div>
    );
  }

  if (!account) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <CreditCard className="w-16 h-16 text-gray-300 mx-auto mb-4" />
          <h2 className="text-xl font-semibold text-gray-900 mb-2">Account Not Found</h2>
          <p className="text-gray-500 mb-4">The account you're looking for doesn't exist.</p>
          <button onClick={() => navigate('/accounts')} className="btn-primary">
            Back to Accounts
          </button>
        </div>
      </div>
    );
  }

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
    }).format(amount);
  };

  const handleToggleStatus = async () => {
    try {
      if (account.is_active) {
        await deactivateAccount(account.account_number);
        toast.success('Account deactivated successfully');
      } else {
        await activateAccount(account.account_number);
        toast.success('Account activated successfully');
      }
      // Refresh account data after status change
      const updatedAccount = await getAccountByNumber(accountNumber);
      setAccount(updatedAccount);
    } catch (error) {
      toast.error(error.message || 'Failed to update account status');
    }
    setShowActions(false);
  };

  const handleClose = async () => {
    try {
      await closeAccount(account.account_number);
      toast.success('Account closed');
      // Re-read from the backend so the real persisted status is shown.
      const updatedAccount = await getAccountByNumber(account.account_number);
      setAccount(updatedAccount);
    } catch (error) {
      toast.error(error.message || 'Failed to close account');
    }
    setShowActions(false);
  };

  const getTransactionIcon = (type) => {
    switch (type) {
      case 'DEPOSIT': return <ArrowDownRight className="w-4 h-4 text-green-500" />;
      case 'WITHDRAWAL': return <ArrowUpRight className="w-4 h-4 text-red-500" />;
      case 'TRANSFER': return <ArrowRightLeft className="w-4 h-4 text-blue-500" />;
      default: return <CreditCard className="w-4 h-4 text-gray-500" />;
    }
  };

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate('/accounts')}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="page-title">Account #{account.account_number}</h1>
              <span className={`badge ${account.is_active ? 'badge-success' : 'badge-danger'}`}>
                {account.is_active ? 'Active' : 'Inactive'}
              </span>
            </div>
            <p className="text-gray-500 mt-1">{account.name}</p>
          </div>
        </div>

        {hasRole(['ADMIN', 'TELLER']) && (
          <div className="relative">
            <button
              onClick={() => setShowActions(!showActions)}
              className="btn-secondary flex items-center gap-2"
            >
              <MoreVertical className="w-4 h-4" />
              Actions
            </button>
            {showActions && (
              <div className="absolute right-0 mt-2 w-48 bg-white rounded-lg shadow-lg border border-gray-200 z-10">
                <button
                  onClick={() => {
                    const editRoute = account.account_type === 'SAVINGS' 
                      ? `/accounts/${account.account_number}/edit-savings`
                      : `/accounts/${account.account_number}/edit-current`;
                    navigate(editRoute);
                    setShowActions(false);
                  }}
                  className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2"
                >
                  <Edit className="w-4 h-4" /> Edit Account
                </button>
                {hasRole('ADMIN') && (
                  <button
                    onClick={handleToggleStatus}
                    className={`w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2 ${
                      account.is_active ? 'text-red-600' : 'text-green-600'
                    }`}
                  >
                    {account.is_active ? (
                      <>
                        <PowerOff className="w-4 h-4" /> Deactivate
                      </>
                    ) : (
                      <>
                        <Power className="w-4 h-4" /> Activate
                      </>
                    )}
                  </button>
                )}
                {hasRole('ADMIN') && account.is_active && (
                  <button
                    onClick={handleClose}
                    className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2 text-red-600"
                  >
                    <XCircle className="w-4 h-4" /> Close Account
                  </button>
                )}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Balance Card */}
      <div className="card p-6 mb-6 bg-gradient-to-br from-primary-500 to-primary-700 text-white">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-primary-100 text-sm font-medium">Available Balance</p>
            <p className="text-4xl font-bold mt-1">{formatCurrency(account.balance)}</p>
            <p className="text-primary-200 text-sm mt-2">
              Last updated: {format(new Date(), 'MMM d, yyyy h:mm a')}
            </p>
          </div>
          <div className="w-20 h-20 bg-white/20 rounded-2xl flex items-center justify-center">
            {account.account_type === 'SAVINGS' 
              ? <PiggyBank className="w-10 h-10" />
              : <Building2 className="w-10 h-10" />
            }
          </div>
        </div>

        {/* Quick Actions - Only for TELLER */}
        {account.is_active && hasRole(['TELLER']) && (
          <div className="flex gap-3 mt-6">
            <button
              onClick={() => navigate('/transactions/deposit')}
              className="flex-1 py-2 bg-white/20 hover:bg-white/30 rounded-lg text-sm font-medium transition-colors"
            >
              Deposit
            </button>
            <button
              onClick={() => navigate('/transactions/withdraw')}
              className="flex-1 py-2 bg-white/20 hover:bg-white/30 rounded-lg text-sm font-medium transition-colors"
            >
              Withdraw
            </button>
            <button
              onClick={() => navigate('/transactions/transfer')}
              className="flex-1 py-2 bg-white/20 hover:bg-white/30 rounded-lg text-sm font-medium transition-colors"
            >
              Transfer
            </button>
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="flex gap-8">
          {['overview', 'transactions'].map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
                activeTab === tab
                  ? 'border-primary-600 text-primary-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {tab.charAt(0).toUpperCase() + tab.slice(1)}
            </button>
          ))}
        </nav>
      </div>

      {/* Tab Content */}
      {activeTab === 'overview' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Account Information */}
          <div className="card p-6">
            <h2 className="section-title mb-4 flex items-center gap-2">
              <CreditCard className="w-5 h-5 text-primary-600" />
              Account Information
            </h2>
            <div className="space-y-4">
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">Account Number</span>
                <span className="font-medium text-gray-900">#{account.account_number ?? '-'}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">Account Type</span>
                <span className={`badge ${account.account_type === 'SAVINGS' ? 'bg-blue-100 text-blue-800' : 'bg-green-100 text-green-800'}`}>
                  {account.account_type ?? '-'}
                </span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">Privilege</span>
                <span className={`badge ${
                  account.privilege === 'PREMIUM' ? 'bg-amber-100 text-amber-800' :
                  account.privilege === 'GOLD' ? 'bg-purple-100 text-purple-800' :
                  'bg-gray-100 text-gray-800'
                }`}>
                  {account.privilege ?? '-'}
                </span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">Minimum Balance</span>
                <span className="font-medium text-gray-900">
                  ₹{Number(account.minimum_balance ?? 0).toLocaleString('en-IN')}
                </span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">Monthly Maintenance Fee</span>
                <span className="font-medium text-gray-900">
                  ₹{Number(account.monthly_maintenance_fee ?? 0).toLocaleString('en-IN')}
                </span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">Bank Name</span>
                <span className="font-medium text-gray-900">{account.bank_name ?? '-'}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">Branch</span>
                <span className="font-medium text-gray-900">{account.bank_branch ?? '-'}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">IFSC Code</span>
                <span className="font-medium text-gray-900">{account.ifsc_code ?? '-'}</span>
              </div>
              <div className="flex justify-between py-2">
                <span className="text-gray-500">Opened On</span>
                <span className="font-medium text-gray-900">
                  {account.activated_date ? format(new Date(account.activated_date), 'MMM d, yyyy') : '-'}
                </span>
              </div>
            </div>
          </div>

          {/* Personal/Company Information */}
          <div className="card p-6">
            <h2 className="section-title mb-4 flex items-center gap-2">
              {account.account_type === 'SAVINGS' 
                ? <><User className="w-5 h-5 text-blue-600" /> Personal Information</>
                : <><Building2 className="w-5 h-5 text-green-600" /> Company Information</>
              }
            </h2>
            <div className="space-y-4">
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500">
                  {account.account_type === 'SAVINGS' ? 'Full Name' : 'Authorized Person'}
                </span>
                <span className="font-medium text-gray-900">{account.name ?? '-'}</span>
              </div>

              {account.account_type === 'SAVINGS' ? (
                <>
                  <div className="flex justify-between py-2 border-b border-gray-100">
                    <span className="text-gray-500">Date of Birth</span>
                    <span className="font-medium text-gray-900">
                      {account.date_of_birth ? format(new Date(account.date_of_birth), 'MMM d, yyyy') : '-'}
                    </span>
                  </div>
                  <div className="flex justify-between py-2 border-b border-gray-100">
                    <span className="text-gray-500">Gender</span>
                    <span className="font-medium text-gray-900">{account.gender ?? '-'}</span>
                  </div>
                  <div className="flex justify-between py-2 border-b border-gray-100">
                    <span className="text-gray-500">Phone</span>
                    <span className="font-medium text-gray-900">{account.phone_no ?? '-'}</span>
                  </div>
                  <div className="flex justify-between py-2">
                    <span className="text-gray-500">Aadhar</span>
                    <span className="font-medium text-gray-900">
                      {account.aadhar_number ? `XXXX XXXX ${account.aadhar_number.slice(-4)}` : '-'}
                    </span>
                  </div>
                </>
              ) : (
                <>
                  <div className="flex justify-between py-2 border-b border-gray-100">
                    <span className="text-gray-500">Company Name</span>
                    <span className="font-medium text-gray-900">{account.company_name ?? '-'}</span>
                  </div>
                  <div className="flex justify-between py-2 border-b border-gray-100">
                    <span className="text-gray-500">Registration No</span>
                    <span className="font-medium text-gray-900">{account.registration_no ?? '-'}</span>
                  </div>
                  <div className="flex justify-between py-2">
                    <span className="text-gray-500">Website</span>
                    {account.website ? (
                      <a href={account.website} target="_blank" rel="noopener noreferrer" 
                         className="font-medium text-primary-600 hover:underline">
                        {account.website}
                      </a>
                    ) : (
                      <span className="font-medium text-gray-900">-</span>
                    )}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {activeTab === 'transactions' && (
        <div className="card overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-200">
            <h2 className="section-title">Transaction History</h2>
          </div>
          {transactions.length > 0 ? (
            <div className="divide-y divide-gray-100">
              {transactions.map((txn) => (
                <div key={txn.id} className="px-6 py-4 hover:bg-gray-50 transition-colors">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-4">
                      <div className={`w-10 h-10 rounded-full flex items-center justify-center ${
                        txn.transaction_type === 'DEPOSIT' ? 'bg-green-100' :
                        txn.transaction_type === 'WITHDRAWAL' ? 'bg-red-100' : 'bg-blue-100'
                      }`}>
                        {getTransactionIcon(txn.transaction_type)}
                      </div>
                      <div>
                        <p className="text-sm font-medium text-gray-900">
                          {txn.transaction_type}
                          {txn.transaction_type === 'TRANSFER' && (
                            <span className="text-gray-500">
                              {txn.from_account === account.account_number 
                                ? ` → #${txn.to_account}`
                                : ` ← #${txn.from_account}`
                              }
                            </span>
                          )}
                        </p>
                        <p className="text-xs text-gray-500">
                          {txn.description} • {format(new Date(txn.created_at), 'MMM d, h:mm a')}
                        </p>
                      </div>
                    </div>
                    <div className="text-right">
                      <p className={`text-sm font-semibold ${
                        txn.transaction_type === 'DEPOSIT' || 
                        (txn.transaction_type === 'TRANSFER' && txn.to_account === account.account_number)
                          ? 'text-green-600' : 'text-red-600'
                      }`}>
                        {txn.transaction_type === 'DEPOSIT' || 
                         (txn.transaction_type === 'TRANSFER' && txn.to_account === account.account_number)
                          ? '+' : '-'
                        }
                        {formatCurrency(txn.amount)}
                      </p>
                      <span className="badge badge-success text-xs">
                        <CheckCircle className="w-3 h-3 mr-1" />
                        {txn.status}
                      </span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="py-12 text-center">
              <Clock className="w-12 h-12 text-gray-300 mx-auto mb-4" />
              <h3 className="text-lg font-medium text-gray-900 mb-1">No transactions yet</h3>
              <p className="text-gray-500">Transactions for this account will appear here</p>
            </div>
          )}
        </div>
      )}

      {/* Click outside to close */}
      {showActions && (
        <div className="fixed inset-0 z-0" onClick={() => setShowActions(false)} />
      )}
    </div>
  );
};

export default AccountDetailsPage;
