import { useState, useEffect } from 'react';
import { useAuthStore } from '../../store/authStore';
import { transactionsService } from '../../services/api';
import {
  Shield,
  Crown,
  Award,
  Star,
  ArrowUpRight,
  RefreshCw,
  IndianRupee,
  TrendingUp,
  Clock,
  AlertCircle,
  CheckCircle,
  Loader2,
} from 'lucide-react';
import toast from 'react-hot-toast';

const DailyTransferLimitsPage = () => {
  const { user } = useAuthStore();
  const [transferLimits, setTransferLimits] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [lastUpdated, setLastUpdated] = useState(null);

  // Privilege tier configuration with icons and colors
  const privilegeConfig = {
    PREMIUM: {
      icon: Crown,
      color: 'from-amber-500 to-yellow-600',
      bgColor: 'bg-gradient-to-br from-amber-50 to-yellow-100',
      borderColor: 'border-amber-300',
      textColor: 'text-amber-700',
      badgeColor: 'bg-amber-100 text-amber-800',
      description: 'Highest tier with maximum benefits',
      features: ['Highest daily limit', 'Priority processing', 'Premium support'],
    },
    GOLD: {
      icon: Award,
      color: 'from-yellow-500 to-orange-500',
      bgColor: 'bg-gradient-to-br from-yellow-50 to-orange-100',
      borderColor: 'border-yellow-300',
      textColor: 'text-yellow-700',
      badgeColor: 'bg-yellow-100 text-yellow-800',
      description: 'Enhanced limits for valued customers',
      features: ['Higher daily limit', 'Fast processing', 'Priority support'],
    },
    SILVER: {
      icon: Star,
      color: 'from-slate-400 to-gray-500',
      bgColor: 'bg-gradient-to-br from-slate-50 to-gray-100',
      borderColor: 'border-slate-300',
      textColor: 'text-slate-700',
      badgeColor: 'bg-slate-100 text-slate-800',
      description: 'Standard tier for regular customers',
      features: ['Standard daily limit', 'Regular processing', 'Standard support'],
    },
  };

  const fetchTransferLimits = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await transactionsService.getTransferLimitRules();
      setTransferLimits(response || []);
      setLastUpdated(new Date());
    } catch (err) {
      console.error('Failed to fetch transfer limits:', err);
      setError(err.message || 'Failed to load transfer limits');
      toast.error(err.message || 'Failed to load transfer limits');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchTransferLimits();
  }, []);

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 0,
    }).format(amount);
  };

  const formatNumber = (num) => {
    return new Intl.NumberFormat('en-IN').format(num);
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <Loader2 className="w-12 h-12 animate-spin text-primary-600 mx-auto mb-4" />
          <p className="text-gray-500">Loading transfer limits...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <p className="text-red-600 font-medium mb-2">Error Loading Data</p>
          <p className="text-gray-500 mb-4">{error}</p>
          <button
            onClick={fetchTransferLimits}
            className="btn btn-primary"
          >
            <RefreshCw className="w-4 h-4 mr-2" />
            Try Again
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
            <Shield className="w-7 h-7 text-primary-600" />
            Daily Transfer Limits
          </h1>
          <p className="text-gray-500 mt-1">
            View transfer limits for different privilege tiers
          </p>
        </div>
        <div className="flex items-center gap-3">
          {lastUpdated && (
            <span className="text-sm text-gray-500 flex items-center gap-1">
              <Clock className="w-4 h-4" />
              Updated: {lastUpdated.toLocaleTimeString('en-IN')}
            </span>
          )}
          <button
            onClick={fetchTransferLimits}
            className="btn btn-outline flex items-center gap-2"
            disabled={isLoading}
          >
            <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        </div>
      </div>

      {/* Info Banner */}
      <div className="bg-blue-50 border border-blue-200 rounded-xl p-4 flex items-start gap-3">
        <AlertCircle className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
        <div>
          <p className="text-blue-800 font-medium">Understanding Transfer Limits</p>
          <p className="text-blue-600 text-sm mt-1">
            Transfer limits are based on the account holder's privilege tier. Higher tiers enjoy greater daily limits
            and per-transaction limits. Limits reset at midnight IST every day.
          </p>
        </div>
      </div>

      {/* Transfer Limit Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {transferLimits.map((limit) => {
          const config = privilegeConfig[limit.privilege] || privilegeConfig.SILVER;
          const Icon = config.icon;

          return (
            <div
              key={limit.privilege}
              className={`${config.bgColor} border-2 ${config.borderColor} rounded-2xl overflow-hidden shadow-lg hover:shadow-xl transition-all duration-300 transform hover:-translate-y-1`}
            >
              {/* Card Header */}
              <div className={`bg-gradient-to-r ${config.color} p-4 text-white`}>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="p-2 bg-white/20 rounded-lg">
                      <Icon className="w-6 h-6" />
                    </div>
                    <div>
                      <h3 className="text-xl font-bold">{limit.privilege}</h3>
                      <p className="text-white/80 text-sm">Privilege Tier</p>
                    </div>
                  </div>
                  <CheckCircle className="w-6 h-6 text-white/80" />
                </div>
              </div>

              {/* Card Body */}
              <div className="p-5 space-y-4">
                {/* Daily Limit */}
                <div className="bg-white rounded-xl p-4 shadow-sm">
                  <div className="flex items-center justify-between mb-2">
                    <span className={`text-sm font-medium ${config.textColor}`}>
                      Daily Transfer Limit
                    </span>
                    <TrendingUp className={`w-4 h-4 ${config.textColor}`} />
                  </div>
                  <div className="flex items-baseline gap-1">
                    <IndianRupee className={`w-5 h-5 ${config.textColor}`} />
                    <span className={`text-2xl font-bold ${config.textColor}`}>
                      {formatNumber(limit.daily_limit)}
                    </span>
                  </div>
                  <p className="text-gray-500 text-xs mt-1">
                    Maximum amount transferable per day
                  </p>
                </div>

                {/* Per Transaction Limit */}
                <div className="bg-white rounded-xl p-4 shadow-sm">
                  <div className="flex items-center justify-between mb-2">
                    <span className={`text-sm font-medium ${config.textColor}`}>
                      Per Transaction Limit
                    </span>
                    <ArrowUpRight className={`w-4 h-4 ${config.textColor}`} />
                  </div>
                  <div className="flex items-baseline gap-1">
                    <IndianRupee className={`w-5 h-5 ${config.textColor}`} />
                    <span className={`text-2xl font-bold ${config.textColor}`}>
                      {formatNumber(limit.transaction_limit)}
                    </span>
                  </div>
                  <p className="text-gray-500 text-xs mt-1">
                    Maximum amount per single transfer
                  </p>
                </div>

                {/* Description */}
                <div className="pt-2 border-t border-gray-200">
                  <p className="text-gray-600 text-sm">{config.description}</p>
                </div>

                {/* Features */}
                <div className="space-y-2">
                  {config.features.map((feature, idx) => (
                    <div key={idx} className="flex items-center gap-2 text-sm text-gray-600">
                      <CheckCircle className={`w-4 h-4 ${config.textColor}`} />
                      <span>{feature}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Empty State */}
      {transferLimits.length === 0 && !isLoading && (
        <div className="text-center py-12 bg-gray-50 rounded-xl">
          <Shield className="w-12 h-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 mb-2">No Transfer Limits Found</h3>
          <p className="text-gray-500 mb-4">
            Transfer limit rules have not been configured yet.
          </p>
          <button onClick={fetchTransferLimits} className="btn btn-primary">
            <RefreshCw className="w-4 h-4 mr-2" />
            Refresh
          </button>
        </div>
      )}

      {/* Summary Table */}
      {transferLimits.length > 0 && (
        <div className="card">
          <div className="card-header">
            <h2 className="text-lg font-semibold text-gray-900">Limits Comparison</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="bg-gray-50">
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Privilege Tier
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Daily Limit
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Per Transaction
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {transferLimits.map((limit) => {
                  const config = privilegeConfig[limit.privilege] || privilegeConfig.SILVER;
                  const Icon = config.icon;
                  return (
                    <tr key={limit.privilege} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="flex items-center gap-3">
                          <div className={`p-2 rounded-lg bg-gradient-to-r ${config.color}`}>
                            <Icon className="w-4 h-4 text-white" />
                          </div>
                          <span className={`font-medium ${config.textColor}`}>
                            {limit.privilege}
                          </span>
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-right">
                        <span className="font-semibold text-gray-900">
                          {formatCurrency(limit.daily_limit)}
                        </span>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-right">
                        <span className="font-semibold text-gray-900">
                          {formatCurrency(limit.transaction_limit)}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Standard Transfer Limits — static reference (per bank policy) */}
      <div className="card">
        <div className="card-header">
          <h2 className="text-lg font-semibold text-gray-900">Standard Transfer Limits (Reference)</h2>
          <p className="text-sm text-gray-500 mt-1">
            Bank policy limits by privilege tier. Independent of an individual account's daily usage.
          </p>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="bg-gray-50">
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Privilege Tier
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Daily Limit
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Per Transaction
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {[
                { privilege: 'PREMIUM', daily: 100000, perTxn: 50000 },
                { privilege: 'GOLD', daily: 50000, perTxn: 25000 },
                { privilege: 'SILVER', daily: 25000, perTxn: 12500 },
              ].map((row) => {
                const config = privilegeConfig[row.privilege] || privilegeConfig.SILVER;
                const Icon = config.icon;
                return (
                  <tr key={row.privilege} className="hover:bg-gray-50">
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="flex items-center gap-3">
                        <div className={`p-2 rounded-lg bg-gradient-to-r ${config.color}`}>
                          <Icon className="w-4 h-4 text-white" />
                        </div>
                        <span className={`font-medium ${config.textColor}`}>{row.privilege}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-right">
                      <span className="font-semibold text-gray-900">{formatCurrency(row.daily)}</span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-right">
                      <span className="font-semibold text-gray-900">{formatCurrency(row.perTxn)}</span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Teller Notice */}
      <div className="bg-green-50 border border-green-200 rounded-xl p-4 flex items-start gap-3">
        <CheckCircle className="w-5 h-5 text-green-600 mt-0.5 flex-shrink-0" />
        <div>
          <p className="text-green-800 font-medium">Teller Reference</p>
          <p className="text-green-600 text-sm mt-1">
            Use this information to guide customers about their transfer limits based on their account privilege tier.
            For limit upgrades, direct customers to the account upgrade process.
          </p>
        </div>
      </div>
    </div>
  );
};

export default DailyTransferLimitsPage;
