import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../store/authStore';
import { useAccountStore } from '../../store/accountStore';
import { useTransactionStore } from '../../store/transactionStore';
import { useUserStore } from '../../store/userStore';
import { transactionsService, accountsService } from '../../services/api';
import {
  CreditCard,
  Users,
  ArrowUpRight,
  ArrowDownRight,
  TrendingUp,
  TrendingDown,
  DollarSign,
  Activity,
  PiggyBank,
  Building,
  ArrowRightLeft,
  CheckCircle2,
  AlertCircle,
  XCircle,
  Loader2,
  RefreshCw,
} from 'lucide-react';
import { format } from 'date-fns';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  BarChart,
  Bar,
} from 'recharts';

const DashboardPage = () => {
  const navigate = useNavigate();
  const { user, hasRole } = useAuthStore();
  const { accounts, fetchAccounts, getStatistics: getAccountStats } = useAccountStore();
  const { transactions, fetchTransactions, getStatistics: getTransactionStats } = useTransactionStore();
  const { users, fetchUsers, getStatistics: getUserStats } = useUserStore();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [serviceStatus, setServiceStatus] = useState({
    accounts: 'checking',
    transactions: 'checking',
    paymentGateway: 'checking',
  });
  const [dashboardStats, setDashboardStats] = useState({
    monthlyGrowth: 0,
    previousMonthBalance: 0,
  });
  // Server-computed portfolio totals (Module 9 functional analytics via /accounts/summary)
  const [summary, setSummary] = useState(null);


  // Check service health status
  const checkServiceHealth = async () => {
    const status = { accounts: 'offline', transactions: 'offline', paymentGateway: 'offline' };
    
    try {
      await accountsService.getAll();
      status.accounts = 'online';
    } catch (err) {
      console.error('Accounts service check failed:', err);
    }
    
    try {
      await transactionsService.getAll({ limit: 1 });
      status.transactions = 'online';
    } catch (err) {
      console.error('Transactions service check failed:', err);
    }
    
    // Payment gateway status can be inferred from transactions service
    status.paymentGateway = status.transactions === 'online' ? 'online' : 'offline';
    
    setServiceStatus(status);
    return status;
  };

  // Calculate monthly growth from transactions
  const calculateMonthlyGrowth = (txnList, accountList) => {
    const now = new Date();
    const thisMonth = now.getMonth();
    const lastMonth = thisMonth === 0 ? 11 : thisMonth - 1;
    const thisYear = now.getFullYear();
    const lastMonthYear = thisMonth === 0 ? thisYear - 1 : thisYear;
    
    // Calculate this month's net (deposits - withdrawals)
    const thisMonthTxns = txnList.filter(t => {
      const txnDate = new Date(t.created_at || t.timestamp);
      return txnDate.getMonth() === thisMonth && txnDate.getFullYear() === thisYear;
    });
    
    const lastMonthTxns = txnList.filter(t => {
      const txnDate = new Date(t.created_at || t.timestamp);
      return txnDate.getMonth() === lastMonth && txnDate.getFullYear() === lastMonthYear;
    });
    
    const thisMonthDeposits = thisMonthTxns.filter(t => t.transaction_type === 'DEPOSIT').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0);
    const thisMonthWithdrawals = thisMonthTxns.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0);
    const thisMonthNet = thisMonthDeposits - thisMonthWithdrawals;
    
    const lastMonthDeposits = lastMonthTxns.filter(t => t.transaction_type === 'DEPOSIT').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0);
    const lastMonthWithdrawals = lastMonthTxns.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0);
    const lastMonthNet = lastMonthDeposits - lastMonthWithdrawals;
    
    // Calculate current total balance
    const currentBalance = accountList.reduce((sum, a) => sum + (a.balance || 0), 0);
    const previousBalance = currentBalance - thisMonthNet;
    
    // Calculate growth percentage
    let growthPercentage = 0;
    if (previousBalance > 0) {
      growthPercentage = ((currentBalance - previousBalance) / previousBalance) * 100;
    } else if (currentBalance > 0) {
      growthPercentage = 100; // If previous was 0 and now we have balance, it's 100% growth
    }
    
    return {
      monthlyGrowth: Math.round(growthPercentage * 10) / 10, // Round to 1 decimal
      previousMonthBalance: previousBalance,
      thisMonthNet,
      lastMonthNet,
    };
  };

  // Fetch data on mount
  useEffect(() => {
    const loadDashboardData = async () => {
      setLoading(true);
      setError(null);
      try {
        // Check service health first
        await checkServiceHealth();
        
        // Fetch accounts first (most important)
        const accountsData = await fetchAccounts().catch(err => {
          console.error('Accounts fetch error:', err);
          return [];
        });

        // Server-computed portfolio summary (Module 9)
        const summaryData = await accountsService.getSummary().catch(err => {
          console.error('Summary fetch error:', err);
          return null;
        });
        setSummary(summaryData);
        
        // Try to fetch transactions (may fail if service not running)
        const txnData = await fetchTransactions().catch(err => {
          console.error('Transactions fetch error:', err);
          return [];
        });
        
        // Calculate monthly growth from real data
        const growth = calculateMonthlyGrowth(txnData || [], accountsData || []);
        setDashboardStats(growth);
        
        // Fetch users only for admin
        if (hasRole && hasRole('ADMIN')) {
          await fetchUsers().catch(err => console.error('Users fetch error:', err));
        }
      } catch (err) {
        console.error('Failed to load dashboard data:', err);
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    loadDashboardData();
  }, []);

  // Refresh data function
  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await checkServiceHealth();
      const accountsData = await fetchAccounts();
      const txnData = await fetchTransactions();
      const summaryData = await accountsService.getSummary().catch(() => null);
      setSummary(summaryData);
      const growth = calculateMonthlyGrowth(txnData || [], accountsData || []);
      setDashboardStats(growth);
      if (hasRole && hasRole('ADMIN')) {
        await fetchUsers();
      }
    } catch (err) {
      console.error('Refresh failed:', err);
    } finally {
      setRefreshing(false);
    }
  };

  const accountStats = getAccountStats ? getAccountStats() : { totalBalance: 0, totalAccounts: 0, activeAccounts: 0, savingsAccounts: 0, currentAccounts: 0 };
  // Prefer the SERVER-computed portfolio totals (Module 9). Falls back to the
  // client calc only if the summary endpoint hasn't loaded yet.
  const portfolio = summary
    ? {
        totalBalance: summary.total_balance ?? 0,
        totalAccounts: summary.total_accounts ?? 0,
        activeAccounts: summary.active_accounts ?? 0,
      }
    : {
        totalBalance: accountStats.totalBalance,
        totalAccounts: accountStats.totalAccounts,
        activeAccounts: accountStats.activeAccounts,
      };
  const transactionStats = getTransactionStats ? getTransactionStats() : { todayTransactions: 0, depositCount: 0, withdrawalCount: 0, transferCount: 0, totalTransfers: 0 };
  const userStats = getUserStats ? getUserStats() : { totalUsers: 0, activeUsers: 0, inactiveUsers: 0 };

  // Calculate today's transaction count directly from transactions
  const calculateTodayTransactions = () => {
    const txnList = transactions || [];
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    
    const todayTxns = txnList.filter(t => {
      if (!t) return false;
      // Support both 'timestamp' (from API) and 'created_at' (legacy)
      const dateField = t.timestamp || t.created_at;
      if (!dateField) return false;
      const txnDate = new Date(dateField);
      txnDate.setHours(0, 0, 0, 0);
      return txnDate.getTime() === today.getTime();
    });
    
    return {
      total: todayTxns.length,
      deposits: todayTxns.filter(t => t.transaction_type === 'DEPOSIT').length,
      withdrawals: todayTxns.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL').length,
    };
  };
  
  const todayStats = calculateTodayTransactions();

  // Generate chart data from actual transactions - Start from Monday
  const generateChartData = () => {
    const dayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
    const today = new Date();
    const data = [];
    
    const txnList = transactions || [];
    
    // Find the Monday of the current week
    const currentDay = today.getDay();
    const mondayOffset = currentDay === 0 ? -6 : 1 - currentDay; // Sunday is 0, so we need to go back 6 days
    const monday = new Date(today);
    monday.setDate(today.getDate() + mondayOffset);
    
    // Generate data for Monday to Sunday
    for (let i = 0; i < 7; i++) {
      const date = new Date(monday);
      date.setDate(monday.getDate() + i);
      const dayName = dayNames[i];
      
      const dayTransactions = txnList.filter(t => {
        if (!t) return false;
        // Support both 'timestamp' (from API) and 'created_at' (legacy)
        const dateField = t.timestamp || t.created_at;
        if (!dateField) return false;
        const txnDate = new Date(dateField);
        return txnDate.toDateString() === date.toDateString();
      });
      
      data.push({
        name: dayName,
        deposits: dayTransactions.filter(t => t.transaction_type === 'DEPOSIT').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        withdrawals: dayTransactions.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        transfers: dayTransactions.filter(t => t.transaction_type === 'TRANSFER').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
      });
    }
    return data;
  };

  // Generate monthly bar chart data
  const generateMonthlyData = () => {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const txnList = transactions || [];
    const currentYear = new Date().getFullYear();
    
    return months.map((month, index) => {
      const monthTxns = txnList.filter(t => {
        if (!t) return false;
        // Support both 'timestamp' (from API) and 'created_at' (legacy)
        const dateField = t.timestamp || t.created_at;
        if (!dateField) return false;
        const txnDate = new Date(dateField);
        return txnDate.getMonth() === index && txnDate.getFullYear() === currentYear;
      });
      
      return {
        name: month,
        deposits: monthTxns.filter(t => t.transaction_type === 'DEPOSIT').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        withdrawals: monthTxns.filter(t => t.transaction_type === 'WITHDRAW' || t.transaction_type === 'WITHDRAWAL').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
        transfers: monthTxns.filter(t => t.transaction_type === 'TRANSFER').reduce((sum, t) => sum + parseFloat(t.amount || 0), 0),
      };
    });
  };

  const transactionChartData = generateChartData();

  const accountList = accounts || [];
  
  const accountTypeData = [
    { name: 'Savings', value: accountStats.savingsAccounts || 0, color: '#3b82f6' },
    { name: 'Current', value: accountStats.currentAccounts || 0, color: '#10b981' },
  ];

  const privilegeData = [
    { name: 'Premium', value: accountList.filter(a => a && a.privilege === 'PREMIUM').length, color: '#f59e0b' },
    { name: 'Gold', value: accountList.filter(a => a && a.privilege === 'GOLD').length, color: '#8b5cf6' },
    { name: 'Silver', value: accountList.filter(a => a && a.privilege === 'SILVER').length, color: '#6b7280' },
  ];

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 0,
    }).format(amount || 0);
  };

  const recentTransactions = (transactions || []).slice(0, 5);

  const getTransactionIcon = (type) => {
    switch (type) {
      case 'DEPOSIT': return <ArrowDownRight className="w-4 h-4 text-green-500" />;
      case 'WITHDRAW':
      case 'WITHDRAWAL': return <ArrowUpRight className="w-4 h-4 text-red-500" />;
      case 'TRANSFER': return <ArrowRightLeft className="w-4 h-4 text-blue-500" />;
      default: return <Activity className="w-4 h-4 text-gray-500" />;
    }
  };

  const getTransactionColor = (type) => {
    switch (type) {
      case 'DEPOSIT': return 'text-green-600';
      case 'WITHDRAW':
      case 'WITHDRAWAL': return 'text-red-600';
      case 'TRANSFER': return 'text-blue-600';
      default: return 'text-gray-600';
    }
  };

  // Safe date formatting helper - converts UTC timestamp to local time
  const formatDate = (dateValue, formatStr = 'MMM d, h:mm a') => {
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

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <Loader2 className="w-12 h-12 animate-spin text-primary-600 mx-auto mb-4" />
          <p className="text-gray-500">Loading dashboard data...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <p className="text-gray-900 font-medium">Failed to load dashboard</p>
          <p className="text-gray-500 text-sm mt-1">{error}</p>
          <button 
            onClick={() => window.location.reload()} 
            className="btn-primary mt-4"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  // Safe hasRole check
  const checkRole = (roles) => {
    if (!hasRole) return false;
    if (Array.isArray(roles)) {
      return roles.some(r => hasRole(r));
    }
    return hasRole(roles);
  };

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Welcome Header */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h1 className="page-title">Welcome back, {user?.username?.split(' ')[0]}!</h1>
          <p className="text-gray-500 mt-1">
            Here's what's happening with your bank today.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={handleRefresh}
            disabled={refreshing}
            className="flex items-center gap-2 px-3 py-2 text-sm text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors disabled:opacity-50"
          >
            <RefreshCw className={`w-4 h-4 ${refreshing ? 'animate-spin' : ''}`} />
            {refreshing ? 'Refreshing...' : 'Refresh'}
          </button>
          <span className="text-sm text-gray-500">
            {format(new Date(), 'EEEE, MMMM d, yyyy')}
          </span>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {/* Total Balance */}
        <div className="card p-6 bg-gradient-to-br from-primary-500 to-primary-700 text-white">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-primary-100 text-sm font-medium">Total Balance</p>
              <p className="text-2xl font-bold mt-1">{formatCurrency(portfolio.totalBalance)}</p>
              <p className={`text-sm mt-2 flex items-center gap-1 ${dashboardStats.monthlyGrowth >= 0 ? 'text-primary-200' : 'text-red-200'}`}>
                {dashboardStats.monthlyGrowth >= 0 ? (
                  <TrendingUp className="w-4 h-4" />
                ) : (
                  <TrendingDown className="w-4 h-4" />
                )}
                {dashboardStats.monthlyGrowth >= 0 ? '+' : ''}{dashboardStats.monthlyGrowth}% from last month
              </p>
            </div>
            <div className="w-12 h-12 bg-white/20 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6" />
            </div>
          </div>
        </div>

        {/* Total Accounts */}
        <div className="card p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-gray-500 text-sm font-medium">Total Accounts</p>
              <p className="text-2xl font-bold text-gray-900 mt-1">{portfolio.totalAccounts}</p>
              <p className="text-sm mt-2">
                <span className="text-green-600 font-medium">{portfolio.activeAccounts} active</span>
                <span className="text-gray-400 mx-1">•</span>
                <span className="text-gray-500">{portfolio.totalAccounts - portfolio.activeAccounts} inactive</span>
              </p>
            </div>
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <CreditCard className="w-6 h-6 text-blue-600" />
            </div>
          </div>
        </div>

        {/* Today's Transactions */}
        <div className="card p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-gray-500 text-sm font-medium">Today's Transactions</p>
              <p className="text-2xl font-bold text-gray-900 mt-1">{todayStats.total}</p>
              <p className="text-sm mt-2">
                <span className="text-green-600 font-medium">{todayStats.deposits} deposits</span>
                <span className="text-gray-400 mx-1">•</span>
                <span className="text-red-600 font-medium">{todayStats.withdrawals} withdrawals</span>
              </p>
            </div>
            <div className="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center">
              <Activity className="w-6 h-6 text-green-600" />
            </div>
          </div>
        </div>

        {/* Users (Admin Only) or Transfers */}
        {checkRole('ADMIN') ? (
          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-gray-500 text-sm font-medium">Total Users</p>
                <p className="text-2xl font-bold text-gray-900 mt-1">{userStats.totalUsers}</p>
                <p className="text-sm mt-2">
                  <span className="text-green-600 font-medium">{userStats.activeUsers} active</span>
                  <span className="text-gray-400 mx-1">•</span>
                  <span className="text-gray-500">{userStats.inactiveUsers} inactive</span>
                </p>
              </div>
              <div className="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
                <Users className="w-6 h-6 text-purple-600" />
              </div>
            </div>
          </div>
        ) : (
          <div className="card p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-gray-500 text-sm font-medium">Total Transfers</p>
                <p className="text-2xl font-bold text-gray-900 mt-1">{formatCurrency(transactionStats.totalTransfers)}</p>
                <p className="text-sm mt-2 text-gray-500">
                  {transactionStats.transferCount} transfers this month
                </p>
              </div>
              <div className="w-12 h-12 bg-amber-100 rounded-xl flex items-center justify-center">
                <ArrowRightLeft className="w-6 h-6 text-amber-600" />
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Transaction Trends */}
        <div className="lg:col-span-2 card p-6">
          <div className="flex items-center justify-between mb-6">
            <h2 className="section-title">Transaction Trends</h2>
            <select className="text-sm border border-gray-300 rounded-lg px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-primary-500">
              <option>Last 7 days</option>
              <option>Last 30 days</option>
              <option>Last 90 days</option>
            </select>
          </div>
          <div className="h-72">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={transactionChartData}>
                <defs>
                  <linearGradient id="colorDeposits" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#10b981" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#10b981" stopOpacity={0}/>
                  </linearGradient>
                  <linearGradient id="colorWithdrawals" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#ef4444" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#ef4444" stopOpacity={0}/>
                  </linearGradient>
                  <linearGradient id="colorTransfers" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#3b82f6" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                <XAxis dataKey="name" stroke="#9ca3af" fontSize={12} />
                <YAxis stroke="#9ca3af" fontSize={12} tickFormatter={(v) => `₹${v/1000}k`} />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: 'white', 
                    border: '1px solid #e5e7eb',
                    borderRadius: '8px',
                    boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)'
                  }}
                  formatter={(value) => [`₹${value.toLocaleString()}`, '']}
                />
                <Area type="monotone" dataKey="deposits" stroke="#10b981" fillOpacity={1} fill="url(#colorDeposits)" strokeWidth={2} />
                <Area type="monotone" dataKey="withdrawals" stroke="#ef4444" fillOpacity={1} fill="url(#colorWithdrawals)" strokeWidth={2} />
                <Area type="monotone" dataKey="transfers" stroke="#3b82f6" fillOpacity={1} fill="url(#colorTransfers)" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </div>
          <div className="flex items-center justify-center gap-6 mt-4">
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 bg-green-500 rounded-full"></div>
              <span className="text-sm text-gray-600">Deposits</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 bg-red-500 rounded-full"></div>
              <span className="text-sm text-gray-600">Withdrawals</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 bg-blue-500 rounded-full"></div>
              <span className="text-sm text-gray-600">Transfers</span>
            </div>
          </div>
        </div>

        {/* Account Distribution */}
        <div className="card p-6">
          <h2 className="section-title mb-6">Account Distribution</h2>
          <div className="h-48">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={accountTypeData}
                  cx="50%"
                  cy="50%"
                  innerRadius={50}
                  outerRadius={80}
                  paddingAngle={5}
                  dataKey="value"
                >
                  {accountTypeData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="space-y-3 mt-4">
            {accountTypeData.map((item) => (
              <div key={item.name} className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <div className="w-3 h-3 rounded-full" style={{ backgroundColor: item.color }}></div>
                  <span className="text-sm text-gray-600">{item.name}</span>
                </div>
                <span className="text-sm font-semibold text-gray-900">{item.value}</span>
              </div>
            ))}
          </div>

          <div className="border-t border-gray-200 mt-6 pt-6">
            <h3 className="text-sm font-medium text-gray-700 mb-3">By Privilege</h3>
            <div className="space-y-2">
              {privilegeData.map((item) => (
                <div key={item.name} className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className="w-3 h-3 rounded-full" style={{ backgroundColor: item.color }}></div>
                    <span className="text-sm text-gray-600">{item.name}</span>
                  </div>
                  <span className="text-sm font-semibold text-gray-900">{item.value}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Recent Activity & Quick Actions */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Recent Transactions */}
        <div className="lg:col-span-2 card">
          <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
            <h2 className="section-title">Recent Transactions</h2>
            <button 
              onClick={() => navigate('/transactions')}
              className="text-sm text-primary-600 hover:text-primary-700 font-medium"
            >
              View All
            </button>
          </div>
          {recentTransactions.length === 0 ? (
            <div className="px-6 py-12 text-center">
              <Activity className="w-12 h-12 text-gray-300 mx-auto mb-3" />
              <p className="text-gray-500">No recent transactions</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-100">
              {recentTransactions.map((txn) => (
                <div key={txn.id || txn.reference_id} className="px-6 py-4 hover:bg-gray-50 transition-colors">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-4">
                      <div className={`w-10 h-10 rounded-full flex items-center justify-center ${
                        txn.transaction_type === 'DEPOSIT' ? 'bg-green-100' :
                        (txn.transaction_type === 'WITHDRAW' || txn.transaction_type === 'WITHDRAWAL') ? 'bg-red-100' : 'bg-blue-100'
                      }`}>
                        {getTransactionIcon(txn.transaction_type)}
                      </div>
                      <div>
                        <p className="text-sm font-medium text-gray-900">
                          {txn.transaction_type === 'TRANSFER' 
                            ? `Transfer #${txn.from_account || txn.account_number} → #${txn.to_account}`
                            : `${(txn.transaction_type === 'WITHDRAW' ? 'Withdrawal' : txn.transaction_type)?.charAt(0)?.toUpperCase() + (txn.transaction_type === 'WITHDRAW' ? 'Withdrawal' : txn.transaction_type)?.slice(1)?.toLowerCase()} - #${txn.account_number || txn.to_account || txn.from_account}`
                          }
                        </p>
                        <p className="text-xs text-gray-500">
                          {txn.description || 'Transaction'} • {formatDate(txn.timestamp || txn.created_at)}
                        </p>
                      </div>
                    </div>
                    <div className="text-right">
                      <p className={`text-sm font-semibold ${getTransactionColor(txn.transaction_type)}`}>
                        {(txn.transaction_type === 'WITHDRAW' || txn.transaction_type === 'WITHDRAWAL') ? '-' : '+'}{formatCurrency(txn.amount)}
                      </p>
                      <span className="badge badge-success text-xs">
                        <CheckCircle2 className="w-3 h-3 mr-1" />
                        {txn.status || 'SUCCESS'}
                      </span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Quick Actions */}
        <div className="card p-6">
          <h2 className="section-title mb-4">Quick Actions</h2>
          <div className="space-y-3">
            {/* Only TELLER can create accounts */}
            {checkRole('TELLER') && (
              <>
                <button
                  onClick={() => navigate('/accounts/create-savings')}
                  className="w-full flex items-center gap-3 p-3 bg-blue-50 hover:bg-blue-100 rounded-lg transition-colors text-left"
                >
                  <div className="w-10 h-10 bg-blue-500 rounded-lg flex items-center justify-center">
                    <PiggyBank className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-gray-900">New Savings Account</p>
                    <p className="text-xs text-gray-500">Create individual account</p>
                  </div>
                </button>

                <button
                  onClick={() => navigate('/accounts/create-current')}
                  className="w-full flex items-center gap-3 p-3 bg-green-50 hover:bg-green-100 rounded-lg transition-colors text-left"
                >
                  <div className="w-10 h-10 bg-green-500 rounded-lg flex items-center justify-center">
                    <Building className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-gray-900">New Current Account</p>
                    <p className="text-xs text-gray-500">Create business account</p>
                  </div>
                </button>
              </>
            )}

            {checkRole('TELLER') && (
              <>
                <button
                  onClick={() => navigate('/transactions/deposit')}
                  className="w-full flex items-center gap-3 p-3 bg-emerald-50 hover:bg-emerald-100 rounded-lg transition-colors text-left"
                >
                  <div className="w-10 h-10 bg-emerald-500 rounded-lg flex items-center justify-center">
                    <ArrowDownRight className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-gray-900">Make Deposit</p>
                    <p className="text-xs text-gray-500">Credit account balance</p>
                  </div>
                </button>

                <button
                  onClick={() => navigate('/transactions/transfer')}
                  className="w-full flex items-center gap-3 p-3 bg-purple-50 hover:bg-purple-100 rounded-lg transition-colors text-left"
                >
                  <div className="w-10 h-10 bg-purple-500 rounded-lg flex items-center justify-center">
                    <ArrowRightLeft className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-gray-900">Fund Transfer</p>
                    <p className="text-xs text-gray-500">Transfer between accounts</p>
                  </div>
                </button>

                <button
                  onClick={() => navigate('/transactions/withdraw')}
                  className="w-full flex items-center gap-3 p-3 bg-amber-50 hover:bg-amber-100 rounded-lg transition-colors text-left"
                >
                  <div className="w-10 h-10 bg-amber-500 rounded-lg flex items-center justify-center">
                    <ArrowUpRight className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-gray-900">Withdraw Funds</p>
                    <p className="text-xs text-gray-500">Cash withdrawal</p>
                  </div>
                </button>
              </>
            )}

            {!checkRole(['TELLER', 'ADMIN']) && (
              <div className="text-center py-4 text-gray-500 text-sm">
                <p>View accounts and transactions from the menu</p>
              </div>
            )}
          </div>

          {/* System Status */}
          <div className="mt-6 pt-6 border-t border-gray-200">
            <h3 className="text-sm font-medium text-gray-700 mb-3">System Status</h3>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm text-gray-600">Account Service</span>
                {serviceStatus.accounts === 'checking' ? (
                  <span className="flex items-center gap-1 text-gray-500 text-sm">
                    <Loader2 className="w-4 h-4 animate-spin" /> Checking
                  </span>
                ) : serviceStatus.accounts === 'online' ? (
                  <span className="flex items-center gap-1 text-green-600 text-sm">
                    <CheckCircle2 className="w-4 h-4" /> Online
                  </span>
                ) : (
                  <span className="flex items-center gap-1 text-red-600 text-sm">
                    <XCircle className="w-4 h-4" /> Offline
                  </span>
                )}
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-gray-600">Transaction Service</span>
                {serviceStatus.transactions === 'checking' ? (
                  <span className="flex items-center gap-1 text-gray-500 text-sm">
                    <Loader2 className="w-4 h-4 animate-spin" /> Checking
                  </span>
                ) : serviceStatus.transactions === 'online' ? (
                  <span className="flex items-center gap-1 text-green-600 text-sm">
                    <CheckCircle2 className="w-4 h-4" /> Online
                  </span>
                ) : (
                  <span className="flex items-center gap-1 text-red-600 text-sm">
                    <XCircle className="w-4 h-4" /> Offline
                  </span>
                )}
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-gray-600">Payment Gateway</span>
                {serviceStatus.paymentGateway === 'checking' ? (
                  <span className="flex items-center gap-1 text-gray-500 text-sm">
                    <Loader2 className="w-4 h-4 animate-spin" /> Checking
                  </span>
                ) : serviceStatus.paymentGateway === 'online' ? (
                  <span className="flex items-center gap-1 text-green-600 text-sm">
                    <CheckCircle2 className="w-4 h-4" /> Online
                  </span>
                ) : (
                  <span className="flex items-center gap-1 text-red-600 text-sm">
                    <XCircle className="w-4 h-4" /> Offline
                  </span>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default DashboardPage;
