import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTransactionStore } from '../../store/transactionStore';
import { useAccountStore } from '../../store/accountStore';
import { useAuthStore } from '../../store/authStore';
import {
  Search,
  Filter,
  Download,
  ArrowDownRight,
  ArrowUpRight,
  ArrowRightLeft,
  Calendar,
  CheckCircle,
  Clock,
  XCircle,
  Eye,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
  DollarSign,
  TrendingUp,
  TrendingDown,
  Loader2,
} from 'lucide-react';
import { format, subDays, isWithinInterval, startOfDay, endOfDay } from 'date-fns';

const TransactionsPage = () => {
  const navigate = useNavigate();
  const { hasRole } = useAuthStore();
  const { transactions, isLoading, fetchTransactions } = useTransactionStore();
  const { accounts, fetchAccounts } = useAccountStore();

  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState('ALL');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [dateRange, setDateRange] = useState('ALL');
  const [currentPage, setCurrentPage] = useState(1);
  const [showFilters, setShowFilters] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const itemsPerPage = 10;

  // Fetch transactions on component mount
  useEffect(() => {
    const loadData = async () => {
      try {
        await fetchAccounts().catch(err => console.error('Accounts error:', err));
        await fetchTransactions().catch(err => console.error('Transactions error:', err));
      } catch (error) {
        console.error('Failed to load data:', error);
      }
    };
    loadData();
  }, []);

  // Refresh transactions
  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await fetchTransactions();
    } catch (error) {
      console.error('Failed to refresh:', error);
    } finally {
      setRefreshing(false);
    }
  };

  // Safe transactions array
  const txnList = transactions || [];

  // Calculate statistics - handle both WITHDRAW and WITHDRAWAL
  const stats = {
    total: txnList.length,
    deposits: txnList.filter(t => t.transaction_type === 'DEPOSIT').length,
    withdrawals: txnList.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL').length,
    transfers: txnList.filter(t => t.transaction_type === 'TRANSFER').length,
    totalDeposits: txnList
      .filter(t => t.transaction_type === 'DEPOSIT')
      .reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
    totalWithdrawals: txnList
      .filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL')
      .reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
  };

  // Filter transactions
  const filteredTransactions = txnList.filter((txn) => {
    const searchLower = searchTerm.toLowerCase();
    const matchesSearch = 
      String(txn.transaction_id || '').toLowerCase().includes(searchLower) ||
      String(txn.reference_id || '').toLowerCase().includes(searchLower) ||
      String(txn.from_account || '').includes(searchTerm) ||
      String(txn.to_account || '').includes(searchTerm) ||
      String(txn.account_number || '').includes(searchTerm) ||
      String(txn.description || '').toLowerCase().includes(searchLower);

    // Type filter - compare uppercase to handle any case differences
    const txnType = (txn.transaction_type || '').toUpperCase();
    const matchesType = typeFilter === 'ALL' || txnType === typeFilter.toUpperCase();
    
    // Status filter - handle both SUCCESS and COMPLETED
    const txnStatus = (txn.status || '').toUpperCase();
    const matchesStatus = statusFilter === 'ALL' || txnStatus === statusFilter.toUpperCase();

    let matchesDate = true;
    if (dateRange !== 'ALL') {
      try {
        // Handle both timestamp (backend) and created_at (legacy)
        const dateStr = txn.timestamp || txn.created_at;
        if (!dateStr) return true; // If no date, include the transaction
        
        const txnDate = new Date(dateStr);
        if (isNaN(txnDate.getTime())) return true; // Invalid date, include it
        
        const today = new Date();
        const daysAgo = parseInt(dateRange);
        
        // For "Today" (value="1"), compare just the date portion
        if (daysAgo === 1) {
          // Compare dates only (ignore time)
          const txnDateOnly = new Date(txnDate.getFullYear(), txnDate.getMonth(), txnDate.getDate());
          const todayDateOnly = new Date(today.getFullYear(), today.getMonth(), today.getDate());
          matchesDate = txnDateOnly.getTime() === todayDateOnly.getTime();
        } else {
          // For "Last X days", use range
          const startDate = startOfDay(subDays(today, daysAgo));
          matchesDate = txnDate >= startDate && txnDate <= endOfDay(today);
        }
      } catch (error) {
        console.error('Date filter error:', error);
        matchesDate = true; // On error, include the transaction
      }
    }

    return matchesSearch && matchesType && matchesStatus && matchesDate;
  });

  // Sort by date (newest first)
  const sortedTransactions = [...filteredTransactions].sort(
    (a, b) => new Date(b.timestamp || b.created_at) - new Date(a.timestamp || a.created_at)
  );

  // Pagination
  const totalPages = Math.ceil(sortedTransactions.length / itemsPerPage);
  const paginatedTransactions = sortedTransactions.slice(
    (currentPage - 1) * itemsPerPage,
    currentPage * itemsPerPage
  );

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
    }).format(amount || 0);
  };

  // Safe date formatting helper - converts UTC timestamp to local time
  const safeFormatDate = (dateValue, formatStr = 'MMM d, yyyy • h:mm a') => {
    if (!dateValue) return 'N/A';
    try {
      // Backend returns UTC timestamps without 'Z' suffix
      // Add 'Z' to indicate UTC, so JavaScript converts to local time correctly
      let dateStr = String(dateValue);
      if (!dateStr.endsWith('Z') && !dateStr.includes('+') && !dateStr.includes('-', 10)) {
        // Replace space with 'T' for ISO format and add 'Z' for UTC
        dateStr = dateStr.replace(' ', 'T') + 'Z';
      }
      const date = new Date(dateStr);
      if (isNaN(date.getTime())) return 'N/A';
      return format(date, formatStr);
    } catch {
      return 'N/A';
    }
  };

  const getTransactionIcon = (type) => {
    const upperType = (type || '').toUpperCase();
    switch (upperType) {
      case 'DEPOSIT': return <ArrowDownRight className="w-5 h-5 text-green-600" />;
      case 'WITHDRAW':
      case 'WITHDRAWAL': return <ArrowUpRight className="w-5 h-5 text-red-600" />;
      case 'TRANSFER': return <ArrowRightLeft className="w-5 h-5 text-blue-600" />;
      default: return <DollarSign className="w-5 h-5 text-gray-600" />;
    }
  };

  const getStatusBadge = (status) => {
    switch (status) {
      case 'COMPLETED':
      case 'SUCCESS':
        return (
          <span className="badge badge-success">
            <CheckCircle className="w-3 h-3 mr-1" /> SUCCESS
          </span>
        );
      case 'PENDING':
        return (
          <span className="badge badge-warning">
            <Clock className="w-3 h-3 mr-1" /> Pending
          </span>
        );
      case 'FAILED':
        return (
          <span className="badge badge-danger">
            <XCircle className="w-3 h-3 mr-1" /> Failed
          </span>
        );
      default:
        return <span className="badge">{status}</span>;
    }
  };

  const getAccountName = (accountNumber) => {
    const account = accounts.find(a => a.account_number === accountNumber);
    return account?.name || `#${accountNumber}`;
  };

  const handleExport = () => {
    // Create CSV content
    const headers = ['Transaction ID', 'Date', 'Type', 'From', 'To', 'Amount', 'Status', 'Description'];
    const rows = sortedTransactions.map(txn => [
      txn.transaction_id,
      safeFormatDate(txn.created_at, 'yyyy-MM-dd HH:mm:ss'),
      txn.transaction_type,
      txn.from_account || '-',
      txn.to_account || '-',
      txn.amount,
      txn.status,
      txn.description,
    ]);
    
    const csvContent = [
      headers.join(','),
      ...rows.map(row => row.join(','))
    ].join('\n');

    // Download file
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `transactions_${format(new Date(), 'yyyyMMdd')}.csv`;
    a.click();
    window.URL.revokeObjectURL(url);
  };

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="page-title">Transactions</h1>
          <p className="text-gray-500">View and manage all transactions</p>
        </div>
        <div className="flex items-center gap-3">
          <button onClick={handleExport} className="btn-secondary flex items-center gap-2">
            <Download className="w-4 h-4" />
            Export
          </button>
          {hasRole(['TELLER']) && (
            <div className="flex rounded-lg border border-gray-200 overflow-hidden">
              <button 
                onClick={() => navigate('/transactions/deposit')}
                className="px-4 py-2 text-sm font-medium bg-green-50 text-green-700 hover:bg-green-100 border-r border-gray-200"
              >
                Deposit
              </button>
              <button 
                onClick={() => navigate('/transactions/withdraw')}
                className="px-4 py-2 text-sm font-medium bg-red-50 text-red-700 hover:bg-red-100 border-r border-gray-200"
              >
                Withdraw
              </button>
              <button 
                onClick={() => navigate('/transactions/transfer')}
                className="px-4 py-2 text-sm font-medium bg-blue-50 text-blue-700 hover:bg-blue-100"
              >
                Transfer
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-primary-100 rounded-lg flex items-center justify-center">
              <DollarSign className="w-5 h-5 text-primary-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">Total Transactions</p>
              <p className="text-xl font-bold text-gray-900">{stats.total}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <TrendingUp className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">Total Deposits</p>
              <p className="text-xl font-bold text-green-600">{formatCurrency(stats.totalDeposits)}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-red-100 rounded-lg flex items-center justify-center">
              <TrendingDown className="w-5 h-5 text-red-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">Total Withdrawals</p>
              <p className="text-xl font-bold text-red-600">{formatCurrency(stats.totalWithdrawals)}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <ArrowRightLeft className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">Transfers</p>
              <p className="text-xl font-bold text-blue-600">{stats.transfers}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="card mb-6">
        <div className="p-4 border-b border-gray-200">
          <div className="flex flex-col lg:flex-row gap-4">
            {/* Search */}
            <div className="flex-1 relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
              <input
                type="text"
                placeholder="Search by ID, account, or description..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="input pl-10 w-full"
              />
            </div>

            {/* Filters */}
            <div className="flex gap-3">
              <select
                value={typeFilter}
                onChange={(e) => setTypeFilter(e.target.value)}
                className="input"
              >
                <option value="ALL">All Types</option>
                <option value="DEPOSIT">Deposits</option>
                <option value="WITHDRAW">Withdrawals</option>
                <option value="TRANSFER">Transfers</option>
              </select>

              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="input"
              >
                <option value="ALL">All Status</option>
                <option value="SUCCESS">Success</option>
                <option value="PENDING">Pending</option>
                <option value="FAILED">Failed</option>
              </select>

              <select
                value={dateRange}
                onChange={(e) => setDateRange(e.target.value)}
                className="input"
              >
                <option value="ALL">All Time</option>
                <option value="1">Today</option>
                <option value="7">Last 7 Days</option>
                <option value="30">Last 30 Days</option>
                <option value="90">Last 90 Days</option>
              </select>

              <button
                onClick={() => {
                  setSearchTerm('');
                  setTypeFilter('ALL');
                  setStatusFilter('ALL');
                  setDateRange('ALL');
                }}
                className="btn-secondary flex items-center gap-2"
              >
                <RefreshCw className="w-4 h-4" />
                Reset
              </button>
            </div>
          </div>
        </div>

        {/* Results Count */}
        <div className="px-4 py-2 bg-gray-50 border-b border-gray-200 text-sm text-gray-600 flex items-center justify-between">
          <span>Showing {paginatedTransactions.length} of {filteredTransactions.length} transactions</span>
          <button
            onClick={handleRefresh}
            disabled={refreshing || isLoading}
            className="text-primary-600 hover:text-primary-700 flex items-center gap-1 text-sm"
          >
            <RefreshCw className={`w-4 h-4 ${refreshing ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        </div>

        {/* Transaction List */}
        {isLoading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-8 h-8 animate-spin text-primary-600" />
          </div>
        ) : paginatedTransactions.length > 0 ? (
          <div className="divide-y divide-gray-100">
            {paginatedTransactions.map((txn) => {
              // Normalize transaction type for display
              const txnType = txn.transaction_type?.toUpperCase();
              const isWithdraw = txnType === 'WITHDRAW' || txnType === 'WITHDRAWAL';
              const isDeposit = txnType === 'DEPOSIT';
              const isTransfer = txnType === 'TRANSFER';
              const displayType = isWithdraw ? 'WITHDRAWAL' : txnType;
              
              return (
              <div
                key={txn.id || txn.transaction_id || txn.reference_id}
                className="p-4 hover:bg-gray-50 transition-colors cursor-pointer"
                onClick={() => {/* View transaction details */}}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${
                      isDeposit ? 'bg-green-100' :
                      isWithdraw ? 'bg-red-100' : 'bg-blue-100'
                    }`}>
                      {getTransactionIcon(txnType)}
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <p className="font-medium text-gray-900">{displayType}</p>
                        <span className="text-xs text-gray-400">#{txn.reference_id || txn.transaction_id || txn.id}</span>
                      </div>
                      <p className="text-sm text-gray-500 mt-0.5">
                        {isTransfer ? (
                          <>
                            From <span className="font-medium">{getAccountName(txn.from_account || txn.account_number)}</span>
                            {' → '}
                            To <span className="font-medium">{getAccountName(txn.to_account)}</span>
                          </>
                        ) : isDeposit ? (
                          <>To <span className="font-medium">{getAccountName(txn.to_account || txn.account_number)}</span></>
                        ) : (
                          <>From <span className="font-medium">{getAccountName(txn.from_account || txn.account_number)}</span></>
                        )}
                      </p>
                      <p className="text-xs text-gray-400 mt-1">{txn.description}</p>
                    </div>
                  </div>

                  <div className="flex items-center gap-6">
                    <div className="text-right">
                      <p className={`text-lg font-semibold ${
                        isDeposit ? 'text-green-600' :
                        isWithdraw ? 'text-red-600' : 'text-blue-600'
                      }`}>
                        {isWithdraw ? '-' : 
                         isDeposit ? '+' : ''}
                        {formatCurrency(txn.amount)}
                      </p>
                      <p className="text-xs text-gray-400 mt-1">
                        {safeFormatDate(txn.timestamp || txn.created_at)}
                      </p>
                    </div>
                    {getStatusBadge(txn.status || 'SUCCESS')}
                  </div>
                </div>
              </div>
            );
            })}
          </div>
        ) : (
          <div className="py-12 text-center">
            <DollarSign className="w-12 h-12 text-gray-300 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-gray-900 mb-1">No transactions found</h3>
            <p className="text-gray-500">
              {searchTerm || typeFilter !== 'ALL' || statusFilter !== 'ALL' || dateRange !== 'ALL'
                ? 'Try adjusting your filters'
                : 'No transactions have been recorded yet'}
            </p>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="px-4 py-3 border-t border-gray-200 flex items-center justify-between">
            <p className="text-sm text-gray-500">
              Page {currentPage} of {totalPages}
            </p>
            <div className="flex gap-2">
              <button
                onClick={() => setCurrentPage(Math.max(1, currentPage - 1))}
                disabled={currentPage === 1}
                className="btn-secondary disabled:opacity-50"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                let pageNum;
                if (totalPages <= 5) {
                  pageNum = i + 1;
                } else if (currentPage <= 3) {
                  pageNum = i + 1;
                } else if (currentPage >= totalPages - 2) {
                  pageNum = totalPages - 4 + i;
                } else {
                  pageNum = currentPage - 2 + i;
                }
                return (
                  <button
                    key={pageNum}
                    onClick={() => setCurrentPage(pageNum)}
                    className={`px-3 py-1 rounded-lg text-sm ${
                      currentPage === pageNum
                        ? 'bg-primary-600 text-white'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    {pageNum}
                  </button>
                );
              })}
              <button
                onClick={() => setCurrentPage(Math.min(totalPages, currentPage + 1))}
                disabled={currentPage === totalPages}
                className="btn-secondary disabled:opacity-50"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default TransactionsPage;
