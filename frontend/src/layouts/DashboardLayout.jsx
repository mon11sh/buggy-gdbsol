import { useState } from 'react';
import { Outlet, NavLink, useNavigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { useNotificationStore } from '../store/notificationStore';
import ErrorBoundary from '../components/ErrorBoundary';
import {
  Building2,
  LayoutDashboard,
  CreditCard,
  ArrowLeftRight,
  Users,
  BarChart3,
  Settings,
  LogOut,
  Menu,
  X,
  Bell,
  Search,
  ChevronDown,
  UserCircle,
  PlusCircle,
  Wallet,
  ArrowDownCircle,
  ArrowUpCircle,
  SendHorizontal,
  Check,
  CheckCheck,
  Trash2,
  CreditCard as AccountIcon,
  ArrowRightLeft,
  Shield,
  Info,
} from 'lucide-react';

const DashboardLayout = () => {
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  
  const { user, logout, hasRole } = useAuthStore();
  const { 
    notifications, 
    unreadCount, 
    markAsRead, 
    markAllAsRead, 
    deleteNotification,
    clearAll 
  } = useNotificationStore();
  const navigate = useNavigate();
  const location = useLocation();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  // Get notification icon based on category
  const getNotificationIcon = (category) => {
    switch (category) {
      case 'ACCOUNT': return AccountIcon;
      case 'TRANSACTION': return ArrowRightLeft;
      case 'SECURITY': return Shield;
      case 'SYSTEM': return Info;
      default: return Bell;
    }
  };

  // Get notification color based on type
  const getNotificationColor = (type) => {
    switch (type) {
      case 'SUCCESS': return 'text-green-600 bg-green-100';
      case 'ERROR': return 'text-red-600 bg-red-100';
      case 'WARNING': return 'text-yellow-600 bg-yellow-100';
      case 'INFO': return 'text-blue-600 bg-blue-100';
      default: return 'text-gray-600 bg-gray-100';
    }
  };

  // Format time ago
  const formatTimeAgo = (timestamp) => {
    const now = new Date();
    const date = new Date(timestamp);
    const diff = now - date;
    
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);
    
    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    return `${days}d ago`;
  };

  // Navigation items based on role
  const getNavItems = () => {
    const items = [
      {
        name: 'Dashboard',
        path: '/dashboard',
        icon: LayoutDashboard,
        roles: ['ADMIN', 'TELLER', 'MANAGER'],
      },
      {
        name: 'Accounts',
        path: '/accounts',
        icon: CreditCard,
        roles: ['ADMIN', 'TELLER', 'MANAGER'],
        // Only TELLER can create accounts, ADMIN can only view
        subItems: hasRole('TELLER') ? [
          { name: 'All Accounts', path: '/accounts' },
          { name: 'Create Savings', path: '/accounts/create-savings', icon: PlusCircle },
          { name: 'Create Current', path: '/accounts/create-current', icon: PlusCircle },
        ] : [
          { name: 'All Accounts', path: '/accounts' },
        ],
      },
      {
        name: 'Transactions',
        path: '/transactions',
        icon: ArrowLeftRight,
        roles: ['ADMIN', 'TELLER', 'MANAGER'],
        subItems: hasRole('TELLER') ? [
          { name: 'All Transactions', path: '/transactions' },
          { name: 'Deposit', path: '/transactions/deposit', icon: ArrowDownCircle },
          { name: 'Withdraw', path: '/transactions/withdraw', icon: ArrowUpCircle },
          { name: 'Transfer', path: '/transactions/transfer', icon: SendHorizontal },
          { name: 'Transfer Limits', path: '/transactions/transfer-limits', icon: Shield },
        ] : [
          { name: 'All Transactions', path: '/transactions' },
        ],
      },
      {
        name: 'Users',
        path: '/users',
        icon: Users,
        roles: ['ADMIN'],
        subItems: [
          { name: 'All Users', path: '/users' },
          { name: 'Create User', path: '/users/create', icon: PlusCircle },
        ],
      },
      {
        name: 'Reports',
        path: '/reports',
        icon: BarChart3,
        roles: ['ADMIN', 'MANAGER'],
      },
      {
        name: 'Settings',
        path: '/settings',
        icon: Settings,
        roles: ['ADMIN', 'TELLER', 'MANAGER'],
      },
    ];

    return items.filter(item => item.roles.some(role => user?.role === role));
  };

  const navItems = getNavItems();

  const getRoleBadgeColor = (role) => {
    switch (role) {
      case 'ADMIN': return 'bg-red-100 text-red-800';
      case 'TELLER': return 'bg-blue-100 text-blue-800';
      case 'MANAGER': return 'bg-purple-100 text-purple-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Mobile Menu Overlay */}
      {mobileMenuOpen && (
        <div 
          className="fixed inset-0 bg-black/50 z-40 lg:hidden"
          onClick={() => setMobileMenuOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        className={`fixed top-0 left-0 z-50 h-full bg-white border-r border-gray-200 transition-all duration-300 
          ${sidebarOpen ? 'w-64' : 'w-20'} 
          ${mobileMenuOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}`}
      >
        {/* Logo */}
        <div className="h-16 flex items-center justify-between px-4 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 gradient-bg rounded-xl flex items-center justify-center flex-shrink-0">
              <Building2 className="w-6 h-6 text-white" />
            </div>
            {sidebarOpen && (
              <div className="overflow-hidden">
                <h1 className="font-bold text-gray-900 font-display">GDB</h1>
                <p className="text-xs text-gray-500">Banking Portal</p>
              </div>
            )}
          </div>
          <button
            onClick={() => setMobileMenuOpen(false)}
            className="lg:hidden p-1 hover:bg-gray-100 rounded"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Navigation */}
        <nav className="p-4 space-y-1 overflow-y-auto h-[calc(100vh-8rem)]">
          {navItems.map((item) => (
            <div key={item.path}>
              <NavLink
                to={item.path}
                onClick={() => setMobileMenuOpen(false)}
                className={({ isActive }) =>
                  `sidebar-link ${isActive && location.pathname === item.path ? 'sidebar-link-active' : ''}`
                }
              >
                <item.icon className="w-5 h-5 flex-shrink-0" />
                {sidebarOpen && <span>{item.name}</span>}
              </NavLink>
              
              {/* Sub Items */}
              {sidebarOpen && item.subItems && (
                <div className="ml-9 mt-1 space-y-1">
                  {item.subItems.map((subItem) => (
                    <NavLink
                      key={subItem.path}
                      to={subItem.path}
                      onClick={() => setMobileMenuOpen(false)}
                      className={({ isActive }) =>
                        `block px-3 py-2 text-sm rounded-lg transition-colors ${
                          isActive
                            ? 'text-primary-700 bg-primary-50 font-medium'
                            : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'
                        }`
                      }
                    >
                      <div className="flex items-center gap-2">
                        {subItem.icon && <subItem.icon className="w-4 h-4" />}
                        {subItem.name}
                      </div>
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          ))}
        </nav>

        {/* Sidebar Toggle */}
        <div className="absolute bottom-0 left-0 right-0 p-4 border-t border-gray-200 bg-white">
          <button
            onClick={() => setSidebarOpen(!sidebarOpen)}
            className="hidden lg:flex items-center justify-center w-full py-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
          >
            <Menu className="w-5 h-5" />
            {sidebarOpen && <span className="ml-2 text-sm">Collapse</span>}
          </button>
        </div>
      </aside>

      {/* Main Content */}
      <div className={`transition-all duration-300 ${sidebarOpen ? 'lg:ml-64' : 'lg:ml-20'}`}>
        {/* Top Header */}
        <header className="h-16 bg-white border-b border-gray-200 sticky top-0 z-30">
          <div className="h-full px-4 flex items-center justify-between">
            {/* Left Side */}
            <div className="flex items-center gap-4">
              <button
                onClick={() => setMobileMenuOpen(true)}
                className="lg:hidden p-2 hover:bg-gray-100 rounded-lg"
              >
                <Menu className="w-5 h-5" />
              </button>
              
              {/* Search */}
              <div className="hidden md:flex items-center">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <input
                    type="text"
                    placeholder="Search accounts, transactions..."
                    className="w-64 lg:w-80 pl-10 pr-4 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                  />
                </div>
              </div>
            </div>

            {/* Right Side */}
            <div className="flex items-center gap-3">
              {/* Notifications */}
              <div className="relative">
                <button
                  onClick={() => setNotificationsOpen(!notificationsOpen)}
                  className="relative p-2 hover:bg-gray-100 rounded-lg transition-colors"
                >
                  <Bell className="w-5 h-5 text-gray-600" />
                  {unreadCount > 0 && (
                    <span className="absolute -top-1 -right-1 min-w-5 h-5 px-1.5 bg-red-500 text-white text-xs font-bold rounded-full flex items-center justify-center">
                      {unreadCount > 99 ? '99+' : unreadCount}
                    </span>
                  )}
                </button>

                {notificationsOpen && (
                  <div className="absolute right-0 mt-2 w-96 bg-white rounded-xl shadow-lg border border-gray-200 overflow-hidden z-50">
                    {/* Header */}
                    <div className="px-4 py-3 border-b border-gray-200 flex items-center justify-between">
                      <div>
                        <h3 className="font-semibold text-gray-900">Notifications</h3>
                        {unreadCount > 0 && (
                          <p className="text-xs text-gray-500">{unreadCount} unread</p>
                        )}
                      </div>
                      <div className="flex items-center gap-2">
                        {unreadCount > 0 && (
                          <button 
                            onClick={markAllAsRead}
                            className="text-xs text-primary-600 hover:text-primary-700 font-medium flex items-center gap-1"
                          >
                            <CheckCheck className="w-3.5 h-3.5" />
                            Mark all read
                          </button>
                        )}
                        {notifications.length > 0 && (
                          <button 
                            onClick={clearAll}
                            className="text-xs text-gray-500 hover:text-red-600 font-medium flex items-center gap-1"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                            Clear
                          </button>
                        )}
                      </div>
                    </div>
                    
                    {/* Notification List */}
                    <div className="max-h-80 overflow-y-auto">
                      {notifications.length === 0 ? (
                        <div className="px-4 py-8 text-center">
                          <Bell className="w-12 h-12 text-gray-300 mx-auto mb-3" />
                          <p className="text-sm text-gray-500">No notifications yet</p>
                          <p className="text-xs text-gray-400 mt-1">
                            You'll see notifications for account activities here
                          </p>
                        </div>
                      ) : (
                        notifications.slice(0, 10).map((notif) => {
                          const IconComponent = getNotificationIcon(notif.category);
                          const colorClass = getNotificationColor(notif.type);
                          return (
                            <div
                              key={notif.id}
                              className={`px-4 py-3 hover:bg-gray-50 cursor-pointer border-b border-gray-100 last:border-0 ${
                                !notif.read ? 'bg-blue-50/50' : ''
                              }`}
                              onClick={() => markAsRead(notif.id)}
                            >
                              <div className="flex gap-3">
                                <div className={`w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 ${colorClass}`}>
                                  <IconComponent className="w-4 h-4" />
                                </div>
                                <div className="flex-1 min-w-0">
                                  <div className="flex items-start justify-between gap-2">
                                    <p className={`text-sm ${!notif.read ? 'font-semibold text-gray-900' : 'font-medium text-gray-700'}`}>
                                      {notif.title}
                                    </p>
                                    <button 
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        deleteNotification(notif.id);
                                      }}
                                      className="text-gray-400 hover:text-red-500 p-0.5"
                                    >
                                      <X className="w-3.5 h-3.5" />
                                    </button>
                                  </div>
                                  <p className="text-sm text-gray-500 line-clamp-2">{notif.message}</p>
                                  <p className="text-xs text-gray-400 mt-1">{formatTimeAgo(notif.timestamp)}</p>
                                </div>
                                {!notif.read && (
                                  <div className="w-2 h-2 bg-primary-500 rounded-full flex-shrink-0 mt-1.5"></div>
                                )}
                              </div>
                            </div>
                          );
                        })
                      )}
                    </div>
                    
                    {/* Footer */}
                    {notifications.length > 10 && (
                      <div className="px-4 py-2 border-t border-gray-200 bg-gray-50 text-center">
                        <button 
                          onClick={() => {
                            navigate('/notifications');
                            setNotificationsOpen(false);
                          }}
                          className="text-sm text-primary-600 hover:text-primary-700 font-medium"
                        >
                          View all {notifications.length} notifications
                        </button>
                      </div>
                    )}
                  </div>
                )}
              </div>

              {/* User Menu */}
              <div className="relative">
                <button
                  onClick={() => setUserMenuOpen(!userMenuOpen)}
                  className="flex items-center gap-3 p-2 hover:bg-gray-100 rounded-lg transition-colors"
                >
                  <div className="w-8 h-8 bg-primary-100 rounded-full flex items-center justify-center">
                    <UserCircle className="w-5 h-5 text-primary-600" />
                  </div>
                  <div className="hidden md:block text-left">
                    <p className="text-sm font-medium text-gray-900">{user?.username}</p>
                    <p className="text-xs text-gray-500">{user?.login_id}</p>
                  </div>
                  <ChevronDown className="w-4 h-4 text-gray-400 hidden md:block" />
                </button>

                {userMenuOpen && (
                  <div className="absolute right-0 mt-2 w-56 bg-white rounded-xl shadow-lg border border-gray-200 overflow-hidden z-50">
                    <div className="px-4 py-3 border-b border-gray-200">
                      <p className="text-sm font-medium text-gray-900">{user?.username}</p>
                      <p className="text-sm text-gray-500">{user?.login_id}</p>
                      <span className={`inline-block mt-1 px-2 py-0.5 rounded text-xs font-medium ${getRoleBadgeColor(user?.role)}`}>
                        {user?.role}
                      </span>
                    </div>
                    <div className="py-1">
                      <button
                        onClick={() => { navigate('/profile'); setUserMenuOpen(false); }}
                        className="w-full px-4 py-2 text-left text-sm text-gray-700 hover:bg-gray-50 flex items-center gap-2"
                      >
                        <UserCircle className="w-4 h-4" />
                        My Profile
                      </button>
                      <button
                        onClick={() => { navigate('/settings'); setUserMenuOpen(false); }}
                        className="w-full px-4 py-2 text-left text-sm text-gray-700 hover:bg-gray-50 flex items-center gap-2"
                      >
                        <Settings className="w-4 h-4" />
                        Settings
                      </button>
                    </div>
                    <div className="py-1 border-t border-gray-200">
                      <button
                        onClick={handleLogout}
                        className="w-full px-4 py-2 text-left text-sm text-red-600 hover:bg-red-50 flex items-center gap-2"
                      >
                        <LogOut className="w-4 h-4" />
                        Sign Out
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </header>

        {/* Page Content */}
        <main className="p-6">
          <ErrorBoundary resetKey={location.pathname}>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>

      {/* Click outside to close dropdowns */}
      {(userMenuOpen || notificationsOpen) && (
        <div
          className="fixed inset-0 z-20"
          onClick={() => { setUserMenuOpen(false); setNotificationsOpen(false); }}
        />
      )}
    </div>
  );
};

export default DashboardLayout;
