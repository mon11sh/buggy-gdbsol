import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useNotificationStore } from '../../store/notificationStore';
import {
  ArrowLeft,
  PiggyBank,
  User,
  Phone,
  Calendar,
  CreditCard,
  Shield,
  CheckCircle,
  AlertCircle,
  Loader2,
  Save,
  MapPin,
  Hash,
  Lock,
} from 'lucide-react';
import toast from 'react-hot-toast';

const EditSavingsAccount = () => {
  const { accountNumber } = useParams();
  const navigate = useNavigate();
  const { getAccountByNumber, updateAccount, isLoading } = useAccountStore();
  const { addNotification } = useNotificationStore();

  const [account, setAccount] = useState(null);
  const [formData, setFormData] = useState({
    name: '',
    phone_no: '',
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
        if (acc && acc.account_type === 'SAVINGS') {
          setAccount(acc);
          setFormData({
            name: acc.name || '',
            phone_no: acc.phone_no || '',
            address: acc.address || '',
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
          <p className="text-gray-500 mb-4">The savings account you're looking for doesn't exist.</p>
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
      newErrors.name = 'Name is required';
    }

    if (!formData.phone_no.trim()) {
      newErrors.phone_no = 'Phone number is required';
    } else if (!/^\d{10}$/.test(formData.phone_no)) {
      newErrors.phone_no = 'Phone must be 10 digits';
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
        phone_no: formData.phone_no,
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
        message: `Savings account #${account.account_number} has been updated successfully.`,
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
          <h1 className="page-title">Edit Savings Account</h1>
          <p className="text-gray-500 mt-1">Account #{account.account_number}</p>
        </div>
      </div>

      {/* Account Info Card */}
      <div className="card p-6 mb-6 bg-gradient-to-r from-blue-600 to-blue-700 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 bg-white/20 rounded-xl flex items-center justify-center">
              <PiggyBank className="w-7 h-7" />
            </div>
            <div>
              <p className="text-blue-100 text-sm">Savings Account</p>
              <p className="text-2xl font-bold">#{account.account_number}</p>
            </div>
          </div>
          <div className="text-right">
            <p className="text-blue-100 text-sm">Current Balance</p>
            <p className="text-2xl font-bold">
              ₹{account.balance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
            </p>
          </div>
        </div>
      </div>

      {/* Form */}
      <form onSubmit={handleSubmit}>
        <div className="card p-8 space-y-6">
          {/* Personal Details Section */}
          <div>
            <div className="flex items-center gap-3 mb-6">
              <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
                <User className="w-5 h-5 text-blue-600" />
              </div>
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Personal Details</h2>
                <p className="text-sm text-gray-500">Update account holder information</p>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="input-label flex items-center gap-2">
                  <User className="w-4 h-4" />
                  Full Name *
                </label>
                <input
                  type="text"
                  name="name"
                  value={formData.name}
                  onChange={handleChange}
                  className={`input-field ${errors.name ? 'border-red-500' : ''}`}
                  placeholder="Enter full name"
                />
                {errors.name && <p className="text-red-500 text-sm mt-1">{errors.name}</p>}
              </div>

              <div>
                <label className="input-label flex items-center gap-2">
                  <Phone className="w-4 h-4" />
                  Phone Number *
                </label>
                <input
                  type="tel"
                  name="phone_no"
                  value={formData.phone_no}
                  onChange={handleChange}
                  className={`input-field ${errors.phone_no ? 'border-red-500' : ''}`}
                  placeholder="10-digit phone number"
                  maxLength={10}
                />
                {errors.phone_no && <p className="text-red-500 text-sm mt-1">{errors.phone_no}</p>}
              </div>

              <div className="md:col-span-2">
                <label className="input-label flex items-center gap-2">
                  <MapPin className="w-4 h-4" />
                  Address
                </label>
                <textarea
                  name="address"
                  value={formData.address}
                  onChange={handleChange}
                  className="input-field"
                  rows={2}
                  placeholder="Enter address"
                />
              </div>
            </div>
          </div>

          {/* Read-only Fields */}
          <div className="p-4 bg-gray-50 rounded-xl">
            <p className="text-sm font-medium text-gray-500 mb-3">Verified Details (Read-only)</p>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
              <div>
                <span className="text-gray-500">Aadhar</span>
                <p className="font-mono text-gray-900">XXXX-XXXX-{account.aadhar_number?.slice(-4) || '****'}</p>
              </div>
              <div>
                <span className="text-gray-500">Date of Birth</span>
                <p className="text-gray-900">{account.date_of_birth || 'N/A'}</p>
              </div>
              <div>
                <span className="text-gray-500">Gender</span>
                <p className="text-gray-900">{account.gender || 'N/A'}</p>
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
                      ? 'border-blue-500 bg-blue-50 ring-2 ring-blue-200'
                      : 'border-gray-200 hover:border-gray-300'
                  }`}
                >
                  <span className="text-2xl">{priv.icon}</span>
                  <p className={`font-semibold mt-2 ${formData.privilege === priv.value ? 'text-blue-700' : 'text-gray-900'}`}>
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
              className="btn-primary flex items-center gap-2"
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

export default EditSavingsAccount;
