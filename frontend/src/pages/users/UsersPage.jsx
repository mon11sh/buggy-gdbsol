import { useState, useEffect, useRef, createPortal } from 'react';
import { useNavigate } from 'react-router-dom';
import ReactDOM from 'react-dom';
import { useUserStore } from '../../store/userStore';
import { useAuthStore } from '../../store/authStore';
import {
  Search,
  Filter,
  Plus,
  Users,
  UserCheck,
  UserX,
  UserCog,
  Shield,
  Edit,
  MoreVertical,
  ChevronLeft,
  ChevronRight,
  Calendar,
  Clock,
  RefreshCw,
  User,
} from 'lucide-react';
import { format } from 'date-fns';
import toast from 'react-hot-toast';

const UsersPage = () => {
  const navigate = useNavigate();
  const { hasRole, user: currentUser } = useAuthStore();
  const dropdownRef = useRef(null);
  const { users, loading, fetchUsers, activateUser, deactivateUser } = useUserStore();

  const [searchTerm, setSearchTerm] = useState('');
  const [roleFilter, setRoleFilter] = useState('ALL');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedUser, setSelectedUser] = useState(null);
  const [showActions, setShowActions] = useState(null);
  const [dropdownPosition, setDropdownPosition] = useState({ top: 0, left: 0, openUp: false });
  const itemsPerPage = 10;

  // Fetch users on component mount
  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (showActions && !event.target.closest('.action-dropdown')) {
        setShowActions(null);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showActions]);

  // Ensure users is an array for statistics
  const userList = Array.isArray(users) ? users : [];

  // Statistics
  const stats = {
    total: userList.length,
    active: userList.filter(u => u && u.is_active).length,
    inactive: userList.filter(u => u && !u.is_active).length,
    admins: userList.filter(u => u && u.role === 'ADMIN').length,
    managers: userList.filter(u => u && u.role === 'MANAGER').length,
    tellers: userList.filter(u => u && u.role === 'TELLER').length,
  };

  // Filter users
  const filteredUsers = userList.filter((user) => {
    if (!user) return false;
    const matchesSearch = 
      user.username?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      user.login_id?.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesRole = roleFilter === 'ALL' || user.role === roleFilter;
    const matchesStatus = statusFilter === 'ALL' || 
      (statusFilter === 'ACTIVE' && user.is_active) ||
      (statusFilter === 'INACTIVE' && !user.is_active);

    return matchesSearch && matchesRole && matchesStatus;
  });

  // Pagination
  const totalPages = Math.ceil(filteredUsers.length / itemsPerPage);
  const paginatedUsers = filteredUsers.slice(
    (currentPage - 1) * itemsPerPage,
    currentPage * itemsPerPage
  );

  const getRoleBadge = (role) => {
    const badges = {
      ADMIN: 'bg-purple-100 text-purple-800',
      MANAGER: 'bg-blue-100 text-blue-800',
      TELLER: 'bg-green-100 text-green-800',
    };
    return (
      <span className={`badge ${badges[role] || 'bg-gray-100 text-gray-800'}`}>
        {role}
      </span>
    );
  };

  const handleToggleStatus = async (user) => {
    try {
      if (user.is_active) {
        await deactivateUser(user.login_id);
        toast.success(`User ${user.username} deactivated`);
      } else {
        await activateUser(user.login_id);
        toast.success(`User ${user.username} activated`);
      }
    } catch (error) {
      toast.error(error.message || 'Failed to update user status');
    }
    setShowActions(null);
  };

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="page-title">User Management</h1>
          <p className="text-gray-500">Manage system users and their roles</p>
        </div>
        {hasRole('ADMIN') && (
          <button
            onClick={() => navigate('/users/create')}
            className="btn-primary flex items-center gap-2"
          >
            <Plus className="w-4 h-4" />
            Add New User
          </button>
        )}
      </div>

      {/* Statistics */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-6">
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-primary-100 rounded-lg flex items-center justify-center">
              <Users className="w-5 h-5 text-primary-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Total Users</p>
              <p className="text-xl font-bold text-gray-900">{stats.total}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <UserCheck className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Active</p>
              <p className="text-xl font-bold text-green-600">{stats.active}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-red-100 rounded-lg flex items-center justify-center">
              <UserX className="w-5 h-5 text-red-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Inactive</p>
              <p className="text-xl font-bold text-red-600">{stats.inactive}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
              <Shield className="w-5 h-5 text-purple-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Admins</p>
              <p className="text-xl font-bold text-purple-600">{stats.admins}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <UserCog className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Managers</p>
              <p className="text-xl font-bold text-blue-600">{stats.managers}</p>
            </div>
          </div>
        </div>
        <div className="card p-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <Users className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Tellers</p>
              <p className="text-xl font-bold text-green-600">{stats.tellers}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="card mb-6 overflow-visible">
        <div className="p-4 border-b border-gray-200">
          <div className="flex flex-col lg:flex-row gap-4">
            {/* Search */}
            <div className="flex-1 relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
              <input
                type="text"
                placeholder="Search by name, username, or email..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="input pl-10 w-full"
              />
            </div>

            {/* Filters */}
            <div className="flex gap-3">
              <select
                value={roleFilter}
                onChange={(e) => setRoleFilter(e.target.value)}
                className="input"
              >
                <option value="ALL">All Roles</option>
                <option value="ADMIN">Admin</option>
                <option value="MANAGER">Manager</option>
                <option value="TELLER">Teller</option>
              </select>

              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="input"
              >
                <option value="ALL">All Status</option>
                <option value="ACTIVE">Active</option>
                <option value="INACTIVE">Inactive</option>
              </select>

              <button
                onClick={() => {
                  setSearchTerm('');
                  setRoleFilter('ALL');
                  setStatusFilter('ALL');
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
        <div className="px-4 py-2 bg-gray-50 border-b border-gray-200 text-sm text-gray-600">
          Showing {paginatedUsers.length} of {filteredUsers.length} users
        </div>

        {/* Users Table */}
        {loading ? (
          <div className="flex items-center justify-center py-12">
            <RefreshCw className="w-6 h-6 animate-spin text-primary-600" />
          </div>
        ) : paginatedUsers.length > 0 ? (
          <div className="overflow-visible">
            <table className="w-full">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    User
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Login ID
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Role
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Created At
                  </th>
                  {hasRole('ADMIN') && (
                    <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Actions
                    </th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {paginatedUsers.map((user) => (
                  <tr key={user.login_id || user.user_id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="flex items-center gap-3">
                        <div className={`w-10 h-10 rounded-full flex items-center justify-center text-white font-semibold ${
                          user.role === 'ADMIN' ? 'bg-purple-600' :
                          user.role === 'MANAGER' ? 'bg-blue-600' : 'bg-green-600'
                        }`}>
                          {user.username?.charAt(0).toUpperCase() || 'U'}
                        </div>
                        <div>
                          <p className="font-medium text-gray-900">{user.username}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="flex items-center gap-2 text-sm text-gray-600">
                        <User className="w-4 h-4 text-gray-400" />
                        @{user.login_id}
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      {getRoleBadge(user.role)}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className={`badge ${user.is_active ? 'badge-success' : 'badge-danger'}`}>
                        {user.is_active ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                      {user.created_at ? (
                        <div className="flex items-center gap-1">
                          <Calendar className="w-3 h-3" />
                          {format(new Date(user.created_at), 'MMM d, yyyy')}
                        </div>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                    {hasRole('ADMIN') && (
                      <td className="px-6 py-4 whitespace-nowrap text-right">
                        <div className="relative inline-block action-dropdown">
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              if (showActions === user.login_id) {
                                setShowActions(null);
                              } else {
                                const rect = e.currentTarget.getBoundingClientRect();
                                const spaceBelow = window.innerHeight - rect.bottom;
                                const openUp = spaceBelow < 120; // dropdown height approx 100px
                                setDropdownPosition({
                                  top: openUp ? rect.top - 8 : rect.bottom + 8,
                                  left: rect.right - 160, // 160px = dropdown width
                                  openUp
                                });
                                setShowActions(user.login_id);
                              }
                            }}
                            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
                            disabled={user.login_id === currentUser?.login_id}
                          >
                            <MoreVertical className="w-4 h-4 text-gray-500" />
                          </button>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="py-12 text-center">
            <Users className="w-12 h-12 text-gray-300 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-gray-900 mb-1">No users found</h3>
            <p className="text-gray-500">
              {searchTerm || roleFilter !== 'ALL' || statusFilter !== 'ALL'
                ? 'Try adjusting your filters'
                : 'No users have been created yet'}
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

      {/* Click outside to close */}
      {showActions && (
        <div className="fixed inset-0 z-40" onClick={() => setShowActions(null)} />
      )}

      {/* Dropdown Portal */}
      {showActions && ReactDOM.createPortal(
        <div 
          className="fixed w-40 bg-white rounded-lg shadow-lg border border-gray-200 py-1 action-dropdown"
          style={{ 
            top: dropdownPosition.openUp ? 'auto' : dropdownPosition.top,
            bottom: dropdownPosition.openUp ? window.innerHeight - dropdownPosition.top : 'auto',
            left: dropdownPosition.left,
            zIndex: 9999 
          }}
        >
          <button
            onClick={() => {
              navigate(`/users/edit/${showActions}`);
              setShowActions(null);
            }}
            className="w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2"
          >
            <Edit className="w-4 h-4" /> Edit
          </button>
          <button
            onClick={() => {
              const user = paginatedUsers.find(u => u.login_id === showActions);
              if (user) handleToggleStatus(user);
            }}
            className={`w-full px-4 py-2 text-left text-sm hover:bg-gray-50 flex items-center gap-2 ${
              paginatedUsers.find(u => u.login_id === showActions)?.is_active ? 'text-amber-600' : 'text-green-600'
            }`}
          >
            {paginatedUsers.find(u => u.login_id === showActions)?.is_active ? (
              <>
                <UserX className="w-4 h-4" /> Deactivate
              </>
            ) : (
              <>
                <UserCheck className="w-4 h-4" /> Activate
              </>
            )}
          </button>
        </div>,
        document.body
      )}
    </div>
  );
};

export default UsersPage;
