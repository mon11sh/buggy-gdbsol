import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useAuthStore } from '../../store/authStore';
import {
  Search,
  Filter,
  Plus,
  CreditCard,
  Eye,
  Edit,
  MoreVertical,
  ChevronLeft,
  ChevronRight,
  Building2,
  PiggyBank,
  Download,
  RefreshCw,
} from 'lucide-react';
import { format } from 'date-fns';

const AccountsPage = () => {
  const navigate = useNavigate();
  const { hasRole } = useAuthStore();
  const { 
    accounts, 
    filters, 
    setFilters, 
    getFilteredAccounts, 
    fetchAccounts,
    isLoading 
  } = useAccountStore();

  const [showFilters, setShowFilters] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [openMenu, setOpenMenu] = useState(null);

  // The server returns one page, filtered by account_type (M3) and paginated (M7).
  // search / status / privilege are refined client-side over that page.
  const filteredAccounts = getFilteredAccounts();
  const hasNextPage = accounts.length === pageSize;

  const loadPage = () =>
    fetchAccounts({
      account_type: filters.accountType,
      skip: (currentPage - 1) * pageSize,
      limit: pageSize,
    });

  // Re-fetch from the server whenever the page, page size, or type filter changes.
  useEffect(() => {
    loadPage();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentPage, pageSize, filters.accountType]);

  const formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
    }).format(amount);
  };

  const getPrivilegeBadge = (privilege) => {
    switch (privilege) {
      case 'PREMIUM':
        return 'bg-amber-100 text-amber-800';
      case 'GOLD':
        return 'bg-purple-100 text-purple-800';
      case 'SILVER':
        return 'bg-gray-100 text-gray-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const getTypeBadge = (type) => {
    return type === 'SAVINGS' 
      ? 'bg-blue-100 text-blue-800' 
      : 'bg-green-100 text-green-800';
  };

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h1 className="page-title">Accounts</h1>
          <p className="text-gray-500 mt-1">
            Manage all bank accounts ({filteredAccounts.length} accounts)
          </p>
        </div>
        
        {hasRole(['ADMIN', 'TELLER']) && (
          <div className="flex items-center gap-3">
            <button
              onClick={() => navigate('/accounts/create-savings')}
              className="btn-primary flex items-center gap-2"
            >
              <PiggyBank className="w-4 h-4" />
              Savings Account
            </button>
            <button
              onClick={() => navigate('/accounts/create-current')}
              className="btn-success flex items-center gap-2"
            >
              <Building2 className="w-4 h-4" />
              Current Account
            </button>
          </div>
        )}
      </div>

      {/* Filters */}
      <div className="card p-4">
        <div className="flex flex-col md:flex-row gap-4">
          {/* Search */}
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search by name or account number..."
              value={filters.search}
              onChange={(e) => {
                setFilters({ search: e.target.value });
                setCurrentPage(1);
              }}
              className="w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
            />
          </div>

          {/* Filter Buttons */}
          <div className="flex items-center gap-3">
            <button
              onClick={() => setShowFilters(!showFilters)}
              className={`btn-secondary flex items-center gap-2 ${showFilters ? 'bg-gray-100' : ''}`}
            >
              <Filter className="w-4 h-4" />
              Filters
            </button>
            <button
              onClick={() => loadPage()}
              className="btn-secondary flex items-center gap-2"
              disabled={isLoading}
            >
              <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
              Refresh
            </button>
            <button className="btn-secondary flex items-center gap-2">
              <Download className="w-4 h-4" />
              Export
            </button>
          </div>
        </div>

        {/* Expanded Filters */}
        {showFilters && (
          <div className="mt-4 pt-4 border-t border-gray-200 grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label className="input-label">Account Type</label>
              <select
                value={filters.accountType}
                onChange={(e) => {
                  setFilters({ accountType: e.target.value });
                  setCurrentPage(1);
                }}
                className="input-field"
              >
                <option value="all">All Types</option>
                <option value="SAVINGS">Savings</option>
                <option value="CURRENT">Current</option>
              </select>
            </div>
            <div>
              <label className="input-label">Status</label>
              <select
                value={filters.status}
                onChange={(e) => {
                  setFilters({ status: e.target.value });
                  setCurrentPage(1);
                }}
                className="input-field"
              >
                <option value="all">All Status</option>
                <option value="active">Active</option>
                <option value="inactive">Inactive</option>
              </select>
            </div>
            <div>
              <label className="input-label">Privilege</label>
              <select
                value={filters.privilege}
                onChange={(e) => {
                  setFilters({ privilege: e.target.value });
                  setCurrentPage(1);
                }}
                className="input-field"
              >
                <option value="all">All Privileges</option>
                <option value="PREMIUM">Premium</option>
                <option value="GOLD">Gold</option>
                <option value="SILVER">Silver</option>
              </select>
            </div>
          </div>
        )}
      </div>

      {/* Accounts Table */}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="table-header">
              <tr>
                <th className="px-6 py-4">Account</th>
                <th className="px-6 py-4">Holder Name</th>
                <th className="px-6 py-4">Type</th>
                <th className="px-6 py-4">Balance</th>
                <th className="px-6 py-4">Privilege</th>
                <th className="px-6 py-4">Status</th>
                <th className="px-6 py-4">Opened</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {filteredAccounts.map((account) => (
                <tr key={account.account_number} className="hover:bg-gray-50 transition-colors">
                  <td className="table-cell">
                    <div className="flex items-center gap-3">
                      <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${
                        account.account_type === 'SAVINGS' ? 'bg-blue-100' : 'bg-green-100'
                      }`}>
                        {account.account_type === 'SAVINGS' 
                          ? <PiggyBank className="w-5 h-5 text-blue-600" />
                          : <Building2 className="w-5 h-5 text-green-600" />
                        }
                      </div>
                      <div>
                        <p className="font-semibold text-gray-900">#{account.account_number}</p>
                        <p className="text-xs text-gray-500">{account.ifsc_code}</p>
                      </div>
                    </div>
                  </td>
                  <td className="table-cell">
                    <p className="font-medium text-gray-900">{account.name}</p>
                    {account.account_type === 'CURRENT' && (
                      <p className="text-xs text-gray-500">{account.company_name}</p>
                    )}
                  </td>
                  <td className="table-cell">
                    <span className={`badge ${getTypeBadge(account.account_type)}`}>
                      {account.account_type}
                    </span>
                  </td>
                  <td className="table-cell">
                    <p className="font-semibold text-gray-900">{formatCurrency(account.balance)}</p>
                  </td>
                  <td className="table-cell">
                    <span className={`badge ${getPrivilegeBadge(account.privilege)}`}>
                      {account.privilege}
                    </span>
                  </td>
                  <td className="table-cell">
                    <span className={`badge ${account.is_active ? 'badge-success' : 'badge-danger'}`}>
                      {account.is_active ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="table-cell text-gray-500">
                    {account.activated_date ? format(new Date(account.activated_date), 'MMM d, yyyy') : '-'}
                  </td>
                  <td className="table-cell text-right">
                    <div className="flex items-center justify-end gap-2">
                      <button
                        onClick={() => navigate(`/accounts/${account.account_number}`)}
                        className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
                        title="View Details"
                      >
                        <Eye className="w-4 h-4 text-gray-600" />
                      </button>
                      {hasRole(['ADMIN', 'TELLER']) && (
                        <div className="relative">
                          <button
                            onClick={() => setOpenMenu(openMenu === account.account_number ? null : account.account_number)}
                            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
                          >
                            <MoreVertical className="w-4 h-4 text-gray-600" />
                          </button>
                          {openMenu === account.account_number && (
                            <div className="absolute right-0 mt-2 w-48 bg-white rounded-lg shadow-lg border border-gray-200 z-10">
                              <button
                                onClick={() => {
                                  navigate(`/accounts/${account.account_number}`);
                                  setOpenMenu(null);
                                }}
                                className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2"
                              >
                                <Eye className="w-4 h-4" /> View Details
                              </button>
                              <button
                                onClick={() => {
                                  const editPath = account.account_type === 'SAVINGS' 
                                    ? `/accounts/${account.account_number}/edit-savings`
                                    : `/accounts/${account.account_number}/edit-current`;
                                  navigate(editPath);
                                  setOpenMenu(null);
                                }}
                                className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2"
                              >
                                <Edit className="w-4 h-4" /> Edit Account
                              </button>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Empty State */}
        {filteredAccounts.length === 0 && (
          <div className="py-12 text-center">
            <CreditCard className="w-12 h-12 text-gray-300 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-gray-900 mb-1">No accounts found</h3>
            <p className="text-gray-500">Try adjusting your search or filters</p>
          </div>
        )}

        {/* Pagination (server-driven — skip/limit sent to the backend) */}
        <div className="px-6 py-4 border-t border-gray-200 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <p className="text-sm text-gray-500">
              Page {currentPage} · showing {accounts.length} of this page
            </p>
            <select
              value={pageSize}
              onChange={(e) => { setPageSize(Number(e.target.value)); setCurrentPage(1); }}
              className="border border-gray-300 rounded-lg text-sm py-1.5 px-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
              title="Accounts per page"
            >
              {[5, 10, 25, 50].map((n) => (
                <option key={n} value={n}>{n} / page</option>
              ))}
            </select>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
              disabled={currentPage === 1}
              className="p-2 border border-gray-300 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <button
              onClick={() => setCurrentPage(prev => prev + 1)}
              disabled={!hasNextPage}
              className="p-2 border border-gray-300 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      {/* Click outside to close menu */}
      {openMenu && (
        <div
          className="fixed inset-0 z-0"
          onClick={() => setOpenMenu(null)}
        />
      )}
    </div>
  );
};

export default AccountsPage;
