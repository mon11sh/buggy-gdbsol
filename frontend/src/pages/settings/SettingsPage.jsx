import { useState, useEffect } from 'react';
import { useAuthStore } from '../../store/authStore';
import {
  Settings,
  Bell,
  Lock,
  Globe,
  Moon,
  Sun,
  Shield,
  Key,
  Smartphone,
  Check,
  AlertCircle,
  Eye,
  EyeOff,
  Loader2,
  Save,
  RefreshCw,
} from 'lucide-react';
import toast from 'react-hot-toast';

const SettingsPage = () => {
  const { user } = useAuthStore();
  
  const [activeTab, setActiveTab] = useState('general');
  const [loading, setLoading] = useState(false);
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);

  // General Settings
  const [generalSettings, setGeneralSettings] = useState({
    language: 'en',
    timezone: 'Asia/Kolkata',
    dateFormat: 'DD/MM/YYYY',
    currency: 'INR',
  });

  // Notification Settings
  const [notificationSettings, setNotificationSettings] = useState({
    emailNotifications: true,
    transactionAlerts: true,
    loginAlerts: true,
    marketingEmails: false,
    weeklyReports: true,
    smsAlerts: false,
  });

  // Security Settings
  const [securitySettings, setSecuritySettings] = useState({
    twoFactorEnabled: false,
    sessionTimeout: '30',
    ipRestriction: false,
  });

  // Password Change
  const [passwordData, setPasswordData] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });
  const [passwordErrors, setPasswordErrors] = useState({});

  // Appearance Settings
  const [appearance, setAppearance] = useState({
    theme: 'light',
    sidebarCollapsed: false,
    compactMode: false,
  });

  const handleSaveSettings = async () => {
    setLoading(true);
    try {
      await new Promise(resolve => setTimeout(resolve, 1000));
      toast.success('Settings saved successfully!');
    } catch (error) {
      toast.error(error.message || 'Failed to save settings');
    } finally {
      setLoading(false);
    }
  };

  const handlePasswordChange = async () => {
    const errors = {};
    
    if (!passwordData.currentPassword) {
      errors.currentPassword = 'Current password is required';
    }
    if (!passwordData.newPassword) {
      errors.newPassword = 'New password is required';
    } else if (passwordData.newPassword.length < 8) {
      errors.newPassword = 'Password must be at least 8 characters';
    }
    if (passwordData.newPassword !== passwordData.confirmPassword) {
      errors.confirmPassword = 'Passwords do not match';
    }

    setPasswordErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setLoading(true);
    try {
      await new Promise(resolve => setTimeout(resolve, 1500));
      toast.success('Password changed successfully!');
      setPasswordData({ currentPassword: '', newPassword: '', confirmPassword: '' });
    } catch (error) {
      toast.error(error.message || 'Failed to change password');
    } finally {
      setLoading(false);
    }
  };

  const tabs = [
    { id: 'general', label: 'General', icon: Settings },
    { id: 'notifications', label: 'Notifications', icon: Bell },
    { id: 'security', label: 'Security', icon: Lock },
    { id: 'appearance', label: 'Appearance', icon: Moon },
  ];

  const Toggle = ({ enabled, onChange, label, description }) => (
    <div className="flex items-center justify-between py-4">
      <div>
        <p className="font-medium text-gray-900">{label}</p>
        <p className="text-sm text-gray-500">{description}</p>
      </div>
      <button
        onClick={() => onChange(!enabled)}
        className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out ${
          enabled ? 'bg-primary-600' : 'bg-gray-200'
        }`}
      >
        <span
          className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
            enabled ? 'translate-x-5' : 'translate-x-0'
          }`}
        />
      </button>
    </div>
  );

  return (
    <div className="animate-fade-in">
      {/* Header */}
      <div className="mb-6">
        <h1 className="page-title">Settings</h1>
        <p className="text-gray-500">Manage your account preferences</p>
      </div>

      <div className="flex gap-6">
        {/* Sidebar */}
        <div className="w-64 flex-shrink-0">
          <div className="card p-2">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg text-left transition-colors ${
                  activeTab === tab.id
                    ? 'bg-primary-50 text-primary-600'
                    : 'text-gray-600 hover:bg-gray-50'
                }`}
              >
                <tab.icon className="w-5 h-5" />
                <span className="font-medium">{tab.label}</span>
              </button>
            ))}
          </div>
        </div>

        {/* Content */}
        <div className="flex-1">
          {/* General Settings */}
          {activeTab === 'general' && (
            <div className="card p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
                  <Globe className="w-6 h-6 text-primary-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">General Settings</h2>
                  <p className="text-sm text-gray-500">Configure regional and display preferences</p>
                </div>
              </div>

              <div className="space-y-6">
                <div className="grid grid-cols-2 gap-6">
                  <div>
                    <label className="label">Language</label>
                    <select
                      value={generalSettings.language}
                      onChange={(e) => setGeneralSettings(prev => ({ ...prev, language: e.target.value }))}
                      className="input w-full"
                    >
                      <option value="en">English</option>
                      <option value="hi">Hindi</option>
                      <option value="ta">Tamil</option>
                      <option value="te">Telugu</option>
                    </select>
                  </div>

                  <div>
                    <label className="label">Timezone</label>
                    <select
                      value={generalSettings.timezone}
                      onChange={(e) => setGeneralSettings(prev => ({ ...prev, timezone: e.target.value }))}
                      className="input w-full"
                    >
                      <option value="Asia/Kolkata">IST (Asia/Kolkata)</option>
                      <option value="UTC">UTC</option>
                      <option value="America/New_York">EST (New York)</option>
                      <option value="Europe/London">GMT (London)</option>
                    </select>
                  </div>

                  <div>
                    <label className="label">Date Format</label>
                    <select
                      value={generalSettings.dateFormat}
                      onChange={(e) => setGeneralSettings(prev => ({ ...prev, dateFormat: e.target.value }))}
                      className="input w-full"
                    >
                      <option value="DD/MM/YYYY">DD/MM/YYYY</option>
                      <option value="MM/DD/YYYY">MM/DD/YYYY</option>
                      <option value="YYYY-MM-DD">YYYY-MM-DD</option>
                    </select>
                  </div>

                  <div>
                    <label className="label">Currency</label>
                    <select
                      value={generalSettings.currency}
                      onChange={(e) => setGeneralSettings(prev => ({ ...prev, currency: e.target.value }))}
                      className="input w-full"
                    >
                      <option value="INR">INR (₹)</option>
                      <option value="USD">USD ($)</option>
                      <option value="EUR">EUR (€)</option>
                      <option value="GBP">GBP (£)</option>
                    </select>
                  </div>
                </div>

                <div className="pt-4 border-t border-gray-200">
                  <button
                    onClick={handleSaveSettings}
                    disabled={loading}
                    className="btn-primary flex items-center gap-2"
                  >
                    {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                    Save Changes
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Notification Settings */}
          {activeTab === 'notifications' && (
            <div className="card p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
                  <Bell className="w-6 h-6 text-blue-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">Notification Preferences</h2>
                  <p className="text-sm text-gray-500">Manage how you receive notifications</p>
                </div>
              </div>

              <div className="divide-y divide-gray-100">
                <Toggle
                  enabled={notificationSettings.emailNotifications}
                  onChange={(v) => setNotificationSettings(prev => ({ ...prev, emailNotifications: v }))}
                  label="Email Notifications"
                  description="Receive notifications via email"
                />
                <Toggle
                  enabled={notificationSettings.transactionAlerts}
                  onChange={(v) => setNotificationSettings(prev => ({ ...prev, transactionAlerts: v }))}
                  label="Transaction Alerts"
                  description="Get notified for every transaction"
                />
                <Toggle
                  enabled={notificationSettings.loginAlerts}
                  onChange={(v) => setNotificationSettings(prev => ({ ...prev, loginAlerts: v }))}
                  label="Login Alerts"
                  description="Get notified for new login attempts"
                />
                <Toggle
                  enabled={notificationSettings.weeklyReports}
                  onChange={(v) => setNotificationSettings(prev => ({ ...prev, weeklyReports: v }))}
                  label="Weekly Reports"
                  description="Receive weekly summary reports"
                />
                <Toggle
                  enabled={notificationSettings.smsAlerts}
                  onChange={(v) => setNotificationSettings(prev => ({ ...prev, smsAlerts: v }))}
                  label="SMS Alerts"
                  description="Receive critical alerts via SMS"
                />
                <Toggle
                  enabled={notificationSettings.marketingEmails}
                  onChange={(v) => setNotificationSettings(prev => ({ ...prev, marketingEmails: v }))}
                  label="Marketing Emails"
                  description="Receive promotional content"
                />
              </div>

              <div className="pt-4 mt-4 border-t border-gray-200">
                <button
                  onClick={handleSaveSettings}
                  disabled={loading}
                  className="btn-primary flex items-center gap-2"
                >
                  {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  Save Changes
                </button>
              </div>
            </div>
          )}

          {/* Security Settings */}
          {activeTab === 'security' && (
            <div className="space-y-6">
              <div className="card p-6">
                <div className="flex items-center gap-3 mb-6">
                  <div className="w-12 h-12 bg-amber-100 rounded-xl flex items-center justify-center">
                    <Shield className="w-6 h-6 text-amber-600" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold text-gray-900">Security Settings</h2>
                    <p className="text-sm text-gray-500">Manage account security options</p>
                  </div>
                </div>

                <div className="divide-y divide-gray-100">
                  <Toggle
                    enabled={securitySettings.twoFactorEnabled}
                    onChange={(v) => setSecuritySettings(prev => ({ ...prev, twoFactorEnabled: v }))}
                    label="Two-Factor Authentication"
                    description="Add an extra layer of security"
                  />
                  <div className="py-4">
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="font-medium text-gray-900">Session Timeout</p>
                        <p className="text-sm text-gray-500">Auto-logout after inactivity</p>
                      </div>
                      <select
                        value={securitySettings.sessionTimeout}
                        onChange={(e) => setSecuritySettings(prev => ({ ...prev, sessionTimeout: e.target.value }))}
                        className="input"
                      >
                        <option value="15">15 minutes</option>
                        <option value="30">30 minutes</option>
                        <option value="60">1 hour</option>
                        <option value="120">2 hours</option>
                      </select>
                    </div>
                  </div>
                  <Toggle
                    enabled={securitySettings.ipRestriction}
                    onChange={(v) => setSecuritySettings(prev => ({ ...prev, ipRestriction: v }))}
                    label="IP Restriction"
                    description="Restrict access to specific IP addresses"
                  />
                </div>
              </div>

              {/* Change Password */}
              <div className="card p-6">
                <div className="flex items-center gap-3 mb-6">
                  <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
                    <Key className="w-6 h-6 text-red-600" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold text-gray-900">Change Password</h2>
                    <p className="text-sm text-gray-500">Update your account password</p>
                  </div>
                </div>

                <div className="space-y-4">
                  <div>
                    <label className="label">Current Password</label>
                    <div className="relative">
                      <input
                        type={showCurrentPassword ? 'text' : 'password'}
                        value={passwordData.currentPassword}
                        onChange={(e) => setPasswordData(prev => ({ ...prev, currentPassword: e.target.value }))}
                        className={`input w-full pr-10 ${passwordErrors.currentPassword ? 'border-red-500' : ''}`}
                      />
                      <button
                        type="button"
                        onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400"
                      >
                        {showCurrentPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                      </button>
                    </div>
                    {passwordErrors.currentPassword && (
                      <p className="text-red-500 text-sm mt-1">{passwordErrors.currentPassword}</p>
                    )}
                  </div>

                  <div>
                    <label className="label">New Password</label>
                    <div className="relative">
                      <input
                        type={showNewPassword ? 'text' : 'password'}
                        value={passwordData.newPassword}
                        onChange={(e) => setPasswordData(prev => ({ ...prev, newPassword: e.target.value }))}
                        className={`input w-full pr-10 ${passwordErrors.newPassword ? 'border-red-500' : ''}`}
                      />
                      <button
                        type="button"
                        onClick={() => setShowNewPassword(!showNewPassword)}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400"
                      >
                        {showNewPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                      </button>
                    </div>
                    {passwordErrors.newPassword && (
                      <p className="text-red-500 text-sm mt-1">{passwordErrors.newPassword}</p>
                    )}
                  </div>

                  <div>
                    <label className="label">Confirm New Password</label>
                    <input
                      type="password"
                      value={passwordData.confirmPassword}
                      onChange={(e) => setPasswordData(prev => ({ ...prev, confirmPassword: e.target.value }))}
                      className={`input w-full ${passwordErrors.confirmPassword ? 'border-red-500' : ''}`}
                    />
                    {passwordErrors.confirmPassword && (
                      <p className="text-red-500 text-sm mt-1">{passwordErrors.confirmPassword}</p>
                    )}
                  </div>

                  <button
                    onClick={handlePasswordChange}
                    disabled={loading}
                    className="btn-primary flex items-center gap-2"
                  >
                    {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Key className="w-4 h-4" />}
                    Change Password
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* Appearance Settings */}
          {activeTab === 'appearance' && (
            <div className="card p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
                  <Moon className="w-6 h-6 text-purple-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">Appearance</h2>
                  <p className="text-sm text-gray-500">Customize the look and feel</p>
                </div>
              </div>

              <div className="space-y-6">
                <div>
                  <label className="label">Theme</label>
                  <div className="grid grid-cols-3 gap-4">
                    {[
                      { value: 'light', label: 'Light', icon: Sun },
                      { value: 'dark', label: 'Dark', icon: Moon },
                      { value: 'system', label: 'System', icon: Settings },
                    ].map((themeOption) => (
                      <button
                        key={themeOption.value}
                        onClick={() => setAppearance(prev => ({ ...prev, theme: themeOption.value }))}
                        className={`p-4 rounded-lg border-2 transition-all ${
                          appearance.theme === themeOption.value
                            ? 'border-primary-500 bg-primary-50'
                            : 'border-gray-200 hover:border-gray-300'
                        }`}
                      >
                        <themeOption.icon className={`w-6 h-6 mx-auto mb-2 ${
                          appearance.theme === themeOption.value ? 'text-primary-600' : 'text-gray-400'
                        }`} />
                        <p className={`text-sm font-medium ${
                          appearance.theme === themeOption.value ? 'text-primary-600' : 'text-gray-600'
                        }`}>
                          {themeOption.label}
                        </p>
                      </button>
                    ))}
                  </div>
                </div>

                <div className="divide-y divide-gray-100">
                  <Toggle
                    enabled={appearance.compactMode}
                    onChange={(v) => setAppearance(prev => ({ ...prev, compactMode: v }))}
                    label="Compact Mode"
                    description="Reduce spacing and padding"
                  />
                  <Toggle
                    enabled={appearance.sidebarCollapsed}
                    onChange={(v) => setAppearance(prev => ({ ...prev, sidebarCollapsed: v }))}
                    label="Collapsed Sidebar"
                    description="Start with collapsed sidebar"
                  />
                </div>

                <div className="pt-4 border-t border-gray-200">
                  <button
                    onClick={handleSaveSettings}
                    disabled={loading}
                    className="btn-primary flex items-center gap-2"
                  >
                    {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                    Save Changes
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default SettingsPage;
