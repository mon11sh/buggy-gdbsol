import { useState, useMemo, useEffect } from 'react';
import { useTransactionStore } from '../../store/transactionStore';
import { useAccountStore } from '../../store/accountStore';
import {
  BarChart3,
  Download,
  Calendar,
  TrendingUp,
  TrendingDown,
  ArrowRightLeft,
  DollarSign,
  PieChart,
  Users,
  CreditCard,
  RefreshCw,
  Filter,
} from 'lucide-react';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart as RechartsPie,
  Pie,
  Cell,
  BarChart,
  Bar,
  Legend,
} from 'recharts';
import { format, subDays, startOfMonth, endOfMonth, eachDayOfInterval, isWithinInterval } from 'date-fns';

// Helper to convert UTC timestamp to local Date object
const parseUTCDate = (dateValue) => {
  if (!dateValue) return null;
  try {
    let dateStr = String(dateValue);
    // Backend returns UTC timestamps without 'Z' suffix
    // Add 'Z' to indicate UTC, so JavaScript converts to local time correctly
    if (!dateStr.endsWith('Z') && !dateStr.includes('+') && !dateStr.includes('-', 10)) {
      dateStr = dateStr.replace(' ', 'T') + 'Z';
    }
    const date = new Date(dateStr);
    return isNaN(date.getTime()) ? null : date;
  } catch {
    return null;
  }
};

const ReportsPage = () => {
  const { transactions, fetchTransactions, isLoading } = useTransactionStore();
  const { accounts, fetchAccounts } = useAccountStore();

  const [dateRange, setDateRange] = useState('30');
  const [reportType, setReportType] = useState('overview');

  // Fetch data on mount
  useEffect(() => {
    const loadData = async () => {
      try {
        await Promise.all([
          fetchTransactions(),
          fetchAccounts()
        ]);
      } catch (error) {
        console.error('Failed to load report data:', error);
      }
    };
    loadData();
  }, []);

  // Ensure transactions is an array
  const transactionList = Array.isArray(transactions) ? transactions : [];
  const accountList = Array.isArray(accounts) ? accounts : [];

  // Date calculations
  const today = new Date();
  const startDate = dateRange === 'all' 
    ? new Date('2024-01-01') 
    : subDays(today, parseInt(dateRange));

  // Filter transactions by date range
  // Note: Backend returns 'timestamp' field for transaction date
  const filteredTransactions = useMemo(() => {
    if (!transactionList.length) return [];
    if (dateRange === 'all') return transactionList;
    return transactionList.filter(txn => {
      if (!txn) return false;
      // Support both 'timestamp' (from API) and 'created_at' (legacy)
      const txnDate = txn.timestamp || txn.created_at;
      if (!txnDate) return false;
      try {
        const parsedDate = parseUTCDate(txnDate);
        if (!parsedDate) return false;
        return isWithinInterval(parsedDate, { start: startDate, end: today });
      } catch {
        return false;
      }
    });
  }, [transactionList, dateRange, startDate]);

  // Calculate metrics
  const metrics = useMemo(() => {
    const deposits = filteredTransactions.filter(t => t.transaction_type === 'DEPOSIT');
    // Handle both WITHDRAW and WITHDRAWAL types from backend
    const withdrawals = filteredTransactions.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL');
    const transfers = filteredTransactions.filter(t => t.transaction_type === 'TRANSFER');

    return {
      totalTransactions: filteredTransactions.length,
      totalDeposits: deposits.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
      totalWithdrawals: withdrawals.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
      totalTransfers: transfers.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
      depositCount: deposits.length,
      withdrawalCount: withdrawals.length,
      transferCount: transfers.length,
      avgTransactionValue: filteredTransactions.length > 0
        ? filteredTransactions.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0) / filteredTransactions.length
        : 0,
      netFlow: deposits.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0) - withdrawals.reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
    };
  }, [filteredTransactions]);

  // Daily trend data
  const dailyTrendData = useMemo(() => {
    const days = eachDayOfInterval({ start: startDate, end: today });
    return days.map(day => {
      const dayTransactions = filteredTransactions.filter(t => {
        const txnDate = t.timestamp || t.created_at;
        const parsedDate = parseUTCDate(txnDate);
        return parsedDate && format(parsedDate, 'yyyy-MM-dd') === format(day, 'yyyy-MM-dd');
      });
      return {
        date: format(day, 'MMM d'),
        deposits: dayTransactions
          .filter(t => t.transaction_type === 'DEPOSIT')
          .reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        withdrawals: dayTransactions
          .filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL')
          .reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        transfers: dayTransactions
          .filter(t => t.transaction_type === 'TRANSFER')
          .reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
      };
    });
  }, [filteredTransactions, startDate]);

  // Transaction type distribution
  const transactionTypeData = [
    { name: 'Deposits', value: metrics.depositCount, color: '#10B981' },
    { name: 'Withdrawals', value: metrics.withdrawalCount, color: '#EF4444' },
    { name: 'Transfers', value: metrics.transferCount, color: '#3B82F6' },
  ];

  // Account type distribution
  const accountTypeData = [
    { name: 'Savings', value: accountList.filter(a => a && a.account_type === 'SAVINGS').length, color: '#3B82F6' },
    { name: 'Current', value: accountList.filter(a => a && a.account_type === 'CURRENT').length, color: '#8B5CF6' },
  ];

  // Account privilege distribution
  const privilegeData = [
    { name: 'Silver', value: accountList.filter(a => a && a.privilege === 'SILVER').length, color: '#6B7280' },
    { name: 'Gold', value: accountList.filter(a => a && a.privilege === 'GOLD').length, color: '#F59E0B' },
    { name: 'Premium', value: accountList.filter(a => a && a.privilege === 'PREMIUM').length, color: '#8B5CF6' },
  ];

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 0,
    }).format(amount || 0);
  };

  const handleExportReport = () => {
    const reportData = {
      generatedAt: new Date().toISOString(),
      dateRange: `Last ${dateRange} days`,
      metrics,
      transactions: filteredTransactions,
    };
    
    const blob = new Blob([JSON.stringify(reportData, null, 2)], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `report_${format(new Date(), 'yyyyMMdd')}.json`;
    a.click();
    window.URL.revokeObjectURL(url);
  };

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="page-title">Reports & Analytics</h1>
          <p className="text-gray-500">Comprehensive insights into banking operations</p>
        </div>
        <div className="flex items-center gap-3">
          <select
            value={dateRange}
            onChange={(e) => setDateRange(e.target.value)}
            className="input"
          >
            <option value="7">Last 7 Days</option>
            <option value="30">Last 30 Days</option>
            <option value="90">Last 90 Days</option>
            <option value="all">All Time</option>
          </select>
          <button onClick={handleExportReport} className="btn-primary flex items-center gap-2">
            <Download className="w-4 h-4" />
            Export Report
          </button>
        </div>
      </div>

      {/* Key Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <div className="card p-6">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center">
              <TrendingUp className="w-6 h-6 text-green-600" />
            </div>
            <span className="badge badge-success">+{metrics.depositCount}</span>
          </div>
          <p className="text-sm text-gray-500 mb-1">Total Deposits</p>
          <p className="text-2xl font-bold text-gray-900">{formatCurrency(metrics.totalDeposits)}</p>
        </div>

        <div className="card p-6">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <TrendingDown className="w-6 h-6 text-red-600" />
            </div>
            <span className="badge badge-danger">-{metrics.withdrawalCount}</span>
          </div>
          <p className="text-sm text-gray-500 mb-1">Total Withdrawals</p>
          <p className="text-2xl font-bold text-gray-900">{formatCurrency(metrics.totalWithdrawals)}</p>
        </div>

        <div className="card p-6">
          <div className="flex items-center justify-between mb-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <ArrowRightLeft className="w-6 h-6 text-blue-600" />
            </div>
            <span className="badge bg-blue-100 text-blue-800">{metrics.transferCount}</span>
          </div>
          <p className="text-sm text-gray-500 mb-1">Total Transfers</p>
          <p className="text-2xl font-bold text-gray-900">{formatCurrency(metrics.totalTransfers)}</p>
        </div>

        <div className="card p-6">
          <div className="flex items-center justify-between mb-4">
            <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${
              metrics.netFlow >= 0 ? 'bg-green-100' : 'bg-red-100'
            }`}>
              <DollarSign className={`w-6 h-6 ${metrics.netFlow >= 0 ? 'text-green-600' : 'text-red-600'}`} />
            </div>
          </div>
          <p className="text-sm text-gray-500 mb-1">Net Cash Flow</p>
          <p className={`text-2xl font-bold ${metrics.netFlow >= 0 ? 'text-green-600' : 'text-red-600'}`}>
            {metrics.netFlow >= 0 ? '+' : ''}{formatCurrency(metrics.netFlow)}
          </p>
        </div>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
        {/* Transaction Trend Chart */}
        <div className="lg:col-span-2 card p-6">
          <div className="flex items-center justify-between mb-6">
            <h2 className="section-title flex items-center gap-2">
              <BarChart3 className="w-5 h-5 text-primary-600" />
              Transaction Trends
            </h2>
          </div>
          <div className="h-80">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dailyTrendData.slice(-14)}>
                <defs>
                  <linearGradient id="colorDeposits" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#10B981" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#10B981" stopOpacity={0}/>
                  </linearGradient>
                  <linearGradient id="colorWithdrawals" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#EF4444" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#EF4444" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" />
                <XAxis dataKey="date" stroke="#9CA3AF" fontSize={12} />
                <YAxis stroke="#9CA3AF" fontSize={12} tickFormatter={(v) => `₹${v/1000}k`} />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: '#fff', 
                    border: '1px solid #E5E7EB',
                    borderRadius: '8px',
                    boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)'
                  }}
                  formatter={(value) => formatCurrency(value)}
                />
                <Legend />
                <Area 
                  type="monotone" 
                  dataKey="deposits" 
                  stroke="#10B981" 
                  fillOpacity={1} 
                  fill="url(#colorDeposits)"
                  name="Deposits"
                />
                <Area 
                  type="monotone" 
                  dataKey="withdrawals" 
                  stroke="#EF4444" 
                  fillOpacity={1} 
                  fill="url(#colorWithdrawals)"
                  name="Withdrawals"
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Transaction Type Distribution */}
        <div className="card p-6">
          <h2 className="section-title flex items-center gap-2 mb-6">
            <PieChart className="w-5 h-5 text-primary-600" />
            Transaction Distribution
          </h2>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <RechartsPie>
                <Pie
                  data={transactionTypeData}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={80}
                  paddingAngle={5}
                  dataKey="value"
                >
                  {transactionTypeData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: '#fff', 
                    border: '1px solid #E5E7EB',
                    borderRadius: '8px'
                  }}
                />
              </RechartsPie>
            </ResponsiveContainer>
          </div>
          <div className="space-y-2">
            {transactionTypeData.map((item) => (
              <div key={item.name} className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <div className={`w-3 h-3 rounded-full`} style={{ backgroundColor: item.color }} />
                  <span className="text-sm text-gray-600">{item.name}</span>
                </div>
                <span className="font-medium text-gray-900">{item.value}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Account Analytics */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
        {/* Account Type Distribution */}
        <div className="card p-6">
          <h2 className="section-title flex items-center gap-2 mb-6">
            <CreditCard className="w-5 h-5 text-primary-600" />
            Account Types
          </h2>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={accountTypeData}>
                <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" />
                <XAxis dataKey="name" stroke="#9CA3AF" />
                <YAxis stroke="#9CA3AF" />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: '#fff', 
                    border: '1px solid #E5E7EB',
                    borderRadius: '8px'
                  }}
                />
                <Bar dataKey="value" radius={[4, 4, 0, 0]}>
                  {accountTypeData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Account Privilege Distribution */}
        <div className="card p-6">
          <h2 className="section-title flex items-center gap-2 mb-6">
            <Users className="w-5 h-5 text-primary-600" />
            Account Privileges
          </h2>
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={privilegeData} layout="vertical">
                <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" />
                <XAxis type="number" stroke="#9CA3AF" />
                <YAxis type="category" dataKey="name" stroke="#9CA3AF" />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: '#fff', 
                    border: '1px solid #E5E7EB',
                    borderRadius: '8px'
                  }}
                />
                <Bar dataKey="value" radius={[0, 4, 4, 0]}>
                  {privilegeData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      {/* Summary Stats */}
      <div className="card p-6">
        <h2 className="section-title flex items-center gap-2 mb-6">
          <BarChart3 className="w-5 h-5 text-primary-600" />
          Summary Statistics
        </h2>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
          <div className="text-center p-4 bg-gray-50 rounded-lg">
            <p className="text-3xl font-bold text-primary-600">{metrics.totalTransactions}</p>
            <p className="text-sm text-gray-500 mt-1">Total Transactions</p>
          </div>
          <div className="text-center p-4 bg-gray-50 rounded-lg">
            <p className="text-3xl font-bold text-primary-600">{formatCurrency(metrics.avgTransactionValue)}</p>
            <p className="text-sm text-gray-500 mt-1">Avg. Transaction Value</p>
          </div>
          <div className="text-center p-4 bg-gray-50 rounded-lg">
            <p className="text-3xl font-bold text-primary-600">{accounts.length}</p>
            <p className="text-sm text-gray-500 mt-1">Total Accounts</p>
          </div>
          <div className="text-center p-4 bg-gray-50 rounded-lg">
            <p className="text-3xl font-bold text-primary-600">
              {formatCurrency(accounts.reduce((sum, a) => sum + a.balance, 0))}
            </p>
            <p className="text-sm text-gray-500 mt-1">Total Holdings</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ReportsPage;
