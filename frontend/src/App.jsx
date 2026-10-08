import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { useAuthStore } from './store/authStore';

// Layouts
import AuthLayout from './layouts/AuthLayout';
import DashboardLayout from './layouts/DashboardLayout';

// Auth Pages
import LoginPage from './pages/auth/LoginPage';

// Dashboard Pages
import DashboardPage from './pages/dashboard/DashboardPage';

// Account Pages
import AccountsPage from './pages/accounts/AccountsPage';
import CreateSavingsAccount from './pages/accounts/CreateSavingsAccount';
import CreateCurrentAccount from './pages/accounts/CreateCurrentAccount';
import AccountDetailsPage from './pages/accounts/AccountDetailsPage';
import EditSavingsAccount from './pages/accounts/EditSavingsAccount';
import EditCurrentAccount from './pages/accounts/EditCurrentAccount';

// Transaction Pages
import TransactionsPage from './pages/transactions/TransactionsPage';
import DepositPage from './pages/transactions/DepositPage';
import WithdrawPage from './pages/transactions/WithdrawPage';
import TransferPage from './pages/transactions/TransferPage';
import DailyTransferLimitsPage from './pages/transactions/DailyTransferLimitsPage';

// User Management Pages
import UsersPage from './pages/users/UsersPage';
import CreateUserPage from './pages/users/CreateUserPage';
import EditUserPage from './pages/users/EditUserPage';

// Reports Pages
import ReportsPage from './pages/reports/ReportsPage';

// Settings
import SettingsPage from './pages/settings/SettingsPage';
import ProfilePage from './pages/profile/ProfilePage';

// Protected Route Component
const ProtectedRoute = ({ children, allowedRoles = [] }) => {
  const { isAuthenticated, user, checkTokenExpiry } = useAuthStore();

  // Check token expiry on every render
  const isValidToken = checkTokenExpiry();

  if (!isAuthenticated || !isValidToken) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles.length > 0 && !allowedRoles.includes(user?.role)) {
    return <Navigate to="/dashboard" replace />;
  }

  return children;
};

// Public Route Component (redirects to dashboard if already logged in)
const PublicRoute = ({ children }) => {
  const { isAuthenticated } = useAuthStore();

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }

  return children;
};

function App() {
  return (
    <BrowserRouter>
      <Toaster
        position="top-right"
        toastOptions={{
          duration: 4000,
          style: {
            background: '#363636',
            color: '#fff',
          },
          success: {
            style: {
              background: '#10b981',
            },
          },
          error: {
            style: {
              background: '#ef4444',
            },
          },
        }}
      />
      
      <Routes>
        {/* Public Routes */}
        <Route element={<AuthLayout />}>
          <Route
            path="/login"
            element={
              <PublicRoute>
                <LoginPage />
              </PublicRoute>
            }
          />
        </Route>

        {/* Protected Routes */}
        <Route
          element={
            <ProtectedRoute>
              <DashboardLayout />
            </ProtectedRoute>
          }
        >
          {/* Dashboard */}
          <Route path="/dashboard" element={<DashboardPage />} />

          {/* Accounts */}
          <Route path="/accounts" element={<AccountsPage />} />
          <Route
            path="/accounts/create-savings"
            element={
              <ProtectedRoute allowedRoles={['ADMIN', 'TELLER']}>
                <CreateSavingsAccount />
              </ProtectedRoute>
            }
          />
          <Route
            path="/accounts/create-current"
            element={
              <ProtectedRoute allowedRoles={['ADMIN', 'TELLER']}>
                <CreateCurrentAccount />
              </ProtectedRoute>
            }
          />
          <Route path="/accounts/:accountNumber" element={<AccountDetailsPage />} />
          <Route
            path="/accounts/:accountNumber/edit-savings"
            element={
              <ProtectedRoute allowedRoles={['ADMIN', 'TELLER']}>
                <EditSavingsAccount />
              </ProtectedRoute>
            }
          />
          <Route
            path="/accounts/:accountNumber/edit-current"
            element={
              <ProtectedRoute allowedRoles={['ADMIN', 'TELLER']}>
                <EditCurrentAccount />
              </ProtectedRoute>
            }
          />

          {/* Transactions */}
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route
            path="/transactions/deposit"
            element={
              <ProtectedRoute allowedRoles={['TELLER']}>
                <DepositPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/transactions/withdraw"
            element={
              <ProtectedRoute allowedRoles={['TELLER']}>
                <WithdrawPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/transactions/transfer"
            element={
              <ProtectedRoute allowedRoles={['TELLER']}>
                <TransferPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/transactions/transfer-limits"
            element={
              <ProtectedRoute allowedRoles={['TELLER']}>
                <DailyTransferLimitsPage />
              </ProtectedRoute>
            }
          />

          {/* Users - Admin Only */}
          <Route
            path="/users"
            element={
              <ProtectedRoute allowedRoles={['ADMIN']}>
                <UsersPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/users/create"
            element={
              <ProtectedRoute allowedRoles={['ADMIN']}>
                <CreateUserPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/users/edit/:loginId"
            element={
              <ProtectedRoute allowedRoles={['ADMIN']}>
                <EditUserPage />
              </ProtectedRoute>
            }
          />

          {/* Reports */}
          <Route
            path="/reports"
            element={
              <ProtectedRoute allowedRoles={['ADMIN', 'MANAGER']}>
                <ReportsPage />
              </ProtectedRoute>
            }
          />

          {/* Settings */}
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/profile" element={<ProfilePage />} />
        </Route>

        {/* Redirect root to login (PublicRoute will redirect to dashboard if authenticated) */}
        <Route path="/" element={<Navigate to="/login" replace />} />

        {/* 404 Route - redirect to login */}
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
