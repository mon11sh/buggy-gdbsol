import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useNotificationStore } from '../../store/notificationStore';
import {
  ArrowLeft,
  Building2,
  User,
  Phone,
  Globe,
  CreditCard,
  Shield,
  CheckCircle,
  AlertCircle,
  Loader2,
  Save,
  MapPin,
  Hash,
  Lock,
  Mail,
  Briefcase,
} from 'lucide-react';
import toast from 'react-hot-toast';

const EditCurrentAccount = () => {
  const { accountNumber } = useParams();
  const navigate = useNavigate();
  const { getAccountByNumber, updateAccount, isLoading } = useAccountStore();
  const { addNotification } = useNotificationStore();

  const [account, setAccount] = useState(null);
  const [formData, setFormData] = useState({
    name: '',
    company_name: '',
    website: '',
    phone: '',
    email: '',
    address: '',
    privilege: 'SILVER',
    pin: '',
    confirmPin: '',
  });
  const [errors, setErrors] = useState({});
  const [changingPin, setChangingPin] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchAccount = async () => {
      setLoading(true);
      try {
        const acc = await getAccountByNumber(accountNumber);
        if (acc && acc.account_type === 'CURRENT') {
          setAccount(acc);
          setFormData({
            name: acc.name || '',
            company_name: acc.company_name || '',
            website: acc.website || '',
            phone: acc.phone || acc.phone_no || '',
            email: acc.email || '',
            address: acc.address || acc.company_address || '',
            privilege: acc.privilege || 'SILVER',
            pin: '',
            confirmPin: '',
          });
        }
      } catch (error) {
        console.error('Error fetching account:', error);
      } finally {
        setLoading(false);
      }
    };
    
    fetchAccount();
  }, [accountNumber, getAccountByNumber]);

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
          <p className="text-gray-500 mb-4">The current account you're looking for doesn't exist.</p>
          <button onClick={() => navigate('/accounts')} className="btn-primary">
            Back to Accounts
          </button>
        </div>
      </div>
    );
  }

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    
    if (errors[name]) {
      setErrors(prev => ({ ...prev, [name]: '' }));
    }
  };

  const validateForm = () => {
    const newErrors = {};

    if (!formData.name.trim()) {
      newErrors.name = 'Authorized person name is required';
    }

    if (!formData.company_name.trim()) {
      newErrors.company_name = 'Company name is required';
    }

    if (formData.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      newErrors.email = 'Invalid email format';
    }

    if (formData.phone && !/^\d{10,12}$/.test(formData.phone.replace(/[- ]/g, ''))) {
      newErrors.phone = 'Invalid phone number';
    }

    if (changingPin) {
      if (!formData.pin) {
        newErrors.pin = 'New PIN is required';
      } else if (!/^\d{4}$/.test(formData.pin)) {
        newErrors.pin = 'PIN must be exactly 4 digits';
      }

      if (formData.pin !== formData.confirmPin) {
        newErrors.confirmPin = 'PINs do not match';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!validateForm()) return;

    try {
      const updates = {
        name: formData.name,
        company_name: formData.company_name,
        website: formData.website,
        phone: formData.phone,
        email: formData.email,
        address: formData.address,
        privilege: formData.privilege,
      };

      if (changingPin && formData.pin) {
        updates.pin = formData.pin;
      }

      await updateAccount(account.account_number, updates);

      addNotification({
        type: 'SUCCESS',
        category: 'ACCOUNT',
        title: 'Account Updated',
        message: `Current account #${account.account_number} for ${formData.company_name} has been updated.`,
      });

      toast.success('Account updated successfully!');
      navigate(`/accounts/${account.account_number}`);
    } catch (error) {
      toast.error(error.message || 'Failed to update account');
    }
  };

  return (
    <div className="max-w-3xl mx-auto animate-fade-in">
      {/* Header */}
      <div className="flex items-center gap-4 mb-6">
        <button
          onClick={() => navigate(`/accounts/${account.account_number}`)}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="page-title">Edit Current Account</h1>
          <p className="text-gray-500 mt-1">Account #{account.account_number}</p>
        </div>
      </div>

      {/* Account Info Card */}
      <div className="card p-6 mb-6 bg-gradient-to-r from-green-600 to-green-700 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 bg-white/20 rounded-xl flex items-center justify-center">
              <Building2 className="w-7 h-7" />
            </div>
            <div>
              <p className="text-green-100 text-sm">Current Account</p>
              <p className="text-2xl font-bold">#{account.account_number}</p>
            </div>
          </div>
          <div className="text-right">
            <p className="text-green-100 text-sm">Current Balance</p>
            <p className="text-2xl font-bold">
              ₹{account.balance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
            </p>
          </div>
        </div>
      </div>

      {/* Form */}
      <form onSubmit={handleSubmit}>
        <div className="card p-8 space-y-6">
          {/* Company Details Section */}
          <div>
            <div className="flex items-center gap-3 mb-6">
              <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
                <Building2 className="w-5 h-5 text-green-600" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Company Details</h2>
                <p className="text-sm text-gray-500">Update company information</p>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="md:col-span-2">
                <label className="input-label flex items-center gap-2">
                  <Building2 className="w-4 h-4" />
                  Company Name *
                </label>
                <input
                  type="text"
                  name="company_name"
                  value={formData.company_name}
                  onChange={handleChange}
                  className={`input-field ${errors.company_name ? 'border-red-500' : ''}`}
                  placeholder="Enter company name"
                />
                {errors.company_name && <p className="text-red-500 text-sm mt-1">{errors.company_name}</p>}
              </div>

              <div>
                <label className="input-label flex items-center gap-2">
                  <Globe className="w-4 h-4" />
                  Website
                </label>
                <input
                  type="url"
                  name="website"
                  value={formData.website}
                  onChange={handleChange}
                  className="input-field"
                  placeholder="https://www.company.com"
                />
              </div>

              <div>
                <label className="input-label flex items-center gap-2">
                  <Phone className="w-4 h-4" />
                  Company Phone
                </label>
                <input
                  type="tel"
                  name="phone"
                  value={formData.phone}
                  onChange={handleChange}
                  className={`input-field ${errors.phone ? 'border-red-500' : ''}`}
                  placeholder="Company phone number"
                />
                {errors.phone && <p className="text-red-500 text-sm mt-1">{errors.phone}</p>}
              </div>

              <div>
                <label className="input-label flex items-center gap-2">
                  <Mail className="w-4 h-4" />
                  Company Email
                </label>
                <input
                  type="email"
                  name="email"
                  value={formData.email}
                  onChange={handleChange}
                  className={`input-field ${errors.email ? 'border-red-500' : ''}`}
                  placeholder="info@company.com"
                />
                {errors.email && <p className="text-red-500 text-sm mt-1">{errors.email}</p>}
              </div>

              <div>
                <label className="input-label flex items-center gap-2">
                  <User className="w-4 h-4" />
                  Authorized Person *
                </label>
                <input
                  type="text"
                  name="name"
                  value={formData.name}
                  onChange={handleChange}
                  className={`input-field ${errors.name ? 'border-red-500' : ''}`}
                  placeholder="Authorized signatory name"
                />
                {errors.name && <p className="text-red-500 text-sm mt-1">{errors.name}</p>}
              </div>

              <div className="md:col-span-2">
                <label className="input-label flex items-center gap-2">
                  <MapPin className="w-4 h-4" />
                  Company Address
                </label>
                <textarea
                  name="address"
                  value={formData.address}
                  onChange={handleChange}
                  className="input-field"
                  rows={2}
                  placeholder="Enter company address"
                />
              </div>
            </div>
          </div>

          {/* Read-only Fields */}
          <div className="p-4 bg-gray-50 rounded-xl">
            <p className="text-sm font-medium text-gray-500 mb-3">Verified Details (Read-only)</p>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm">
              <div>
                <span className="text-gray-500">CIN/Registration No.</span>
                <p className="font-mono text-gray-900">{account.registration_no || 'N/A'}</p>
              </div>
              <div>
                <span className="text-gray-500">Account Type</span>
                <p className="text-gray-900">Current Account</p>
              </div>
              <div>
                <span className="text-gray-500">Status</span>
                <span className={`inline-flex px-2 py-0.5 rounded text-xs font-medium ${
                  account.is_active ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'
                }`}>
                  {account.is_active ? 'Active' : 'Inactive'}
                </span>
              </div>
            </div>
          </div>

          {/* Privilege Section */}
          <div>
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
                <Shield className="w-5 h-5 text-purple-600" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Account Privilege</h2>
                <p className="text-sm text-gray-500">Change account tier</p>
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              {[
                { value: 'SILVER', label: 'Silver', desc: 'Standard limits', icon: '🥈' },
                { value: 'GOLD', label: 'Gold', desc: 'Enhanced limits', icon: '🥇' },
                { value: 'PREMIUM', label: 'Premium', desc: 'Maximum limits', icon: '💎' },
              ].map((priv) => (
                <button
                  key={priv.value}
                  type="button"
                  onClick={() => setFormData(prev => ({ ...prev, privilege: priv.value }))}
                  className={`p-4 rounded-xl border-2 text-left transition-all ${
                    formData.privilege === priv.value
                      ? 'border-green-500 bg-green-50 ring-2 ring-green-200'
                      : 'border-gray-200 hover:border-gray-300'
                  }`}
                >
                  <span className="text-2xl">{priv.icon}</span>
                  <p className={`font-semibold mt-2 ${formData.privilege === priv.value ? 'text-green-700' : 'text-gray-900'}`}>
                    {priv.label}
                  </p>
                  <p className="text-xs text-gray-500 mt-1">{priv.desc}</p>
                </button>
              ))}
            </div>
          </div>

          {/* PIN Change Section */}
          <div>
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-amber-100 rounded-lg flex items-center justify-center">
                  <Lock className="w-5 h-5 text-amber-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">Change PIN</h2>
                  <p className="text-sm text-gray-500">Update transaction PIN</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setChangingPin(!changingPin)}
                className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                  changingPin 
                    ? 'bg-gray-200 text-gray-700' 
                    : 'bg-amber-100 text-amber-700 hover:bg-amber-200'
                }`}
              >
                {changingPin ? 'Cancel' : 'Change PIN'}
              </button>
            </div>

            {changingPin && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 p-4 bg-amber-50 rounded-xl border border-amber-200 animate-fade-in">
                <div>
                  <label className="input-label">New 4-Digit PIN *</label>
                  <input
                    type="password"
                    name="pin"
                    value={formData.pin}
                    onChange={handleChange}
                    className={`input-field ${errors.pin ? 'border-red-500' : ''}`}
                    placeholder="Enter new PIN"
                    maxLength={4}
                  />
                  {errors.pin && <p className="text-red-500 text-sm mt-1">{errors.pin}</p>}
                </div>

                <div>
                  <label className="input-label">Confirm New PIN *</label>
                  <input
                    type="password"
                    name="confirmPin"
                    value={formData.confirmPin}
                    onChange={handleChange}
                    className={`input-field ${errors.confirmPin ? 'border-red-500' : ''}`}
                    placeholder="Confirm new PIN"
                    maxLength={4}
                  />
                  {errors.confirmPin && <p className="text-red-500 text-sm mt-1">{errors.confirmPin}</p>}
                </div>
              </div>
            )}
          </div>

          {/* Actions */}
          <div className="flex items-center justify-between pt-6 border-t border-gray-200">
            <button
              type="button"
              onClick={() => navigate(`/accounts/${account.account_number}`)}
              className="btn-secondary"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="btn-success flex items-center gap-2"
            >
              {isLoading ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Saving...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4" />
                  Save Changes
                </>
              )}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
};

export default EditCurrentAccount;
