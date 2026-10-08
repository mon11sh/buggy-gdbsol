import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
  BadgeCheck,
  MapPin,
  Hash,
  UserCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';

const CreateSavingsAccount = () => {
  const navigate = useNavigate();
  const { createSavingsAccount, verifyAadhar: verifyAadharAPI, isLoading } = useAccountStore();
  const { notifyAccountCreated } = useNotificationStore();

  const [formData, setFormData] = useState({
    aadhar_number: '',
    name: '',
    date_of_birth: '',
    gender: '',
    phone_no: '',
    address: '',
    pin: '',
    confirmPin: '',
    privilege: 'SILVER',
    initial_balance: 2000,
  });

  const [errors, setErrors] = useState({});
  const [step, setStep] = useState(1);
  const [aadharVerified, setAadharVerified] = useState(false);
  const [verifyingAadhar, setVerifyingAadhar] = useState(false);
  const [aadharData, setAadharData] = useState(null);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    
    if (errors[name]) {
      setErrors(prev => ({ ...prev, [name]: '' }));
    }

    if (name === 'aadhar_number') {
      setAadharVerified(false);
      setAadharData(null);
      setFormData(prev => ({
        ...prev,
        aadhar_number: value,
        name: '',
        date_of_birth: '',
        gender: '',
        phone_no: '',
        address: '',
      }));
    }
  };

  const verifyAadhar = async () => {
    if (!formData.aadhar_number.trim()) {
      setErrors({ aadhar_number: 'Aadhar number is required' });
      return;
    }

    if (!/^\d{12}$/.test(formData.aadhar_number)) {
      setErrors({ aadhar_number: 'Aadhar must be exactly 12 digits' });
      return;
    }

    setVerifyingAadhar(true);
    setErrors({});

    try {
      const data = await verifyAadharAPI(formData.aadhar_number);
      
      if (data && data.is_valid) {
        setAadharVerified(true);
        setAadharData(data);
        
        setFormData(prev => ({
          ...prev,
          name: data.name || '',
          date_of_birth: data.date_of_birth || '',
          gender: data.gender || '',
          phone_no: data.mobile_no || data.phone_no || '',
          address: data.address || '',
        }));
        
        toast.success(`Aadhar verified for ${data.name}!`);
      } else {
        const errorMsg = data?.message || 'Aadhar not found in UIDAI database';
        toast.error(errorMsg);
        setErrors({ aadhar_number: errorMsg });
      }
    } catch (error) {
      toast.error(error.message || 'Failed to verify Aadhar');
      setErrors({ aadhar_number: error.message || 'Verification failed' });
    } finally {
      setVerifyingAadhar(false);
    }
  };

  const validateStep1 = () => {
    if (!formData.aadhar_number.trim() || !aadharVerified) {
      setErrors({ aadhar_number: 'Please verify Aadhar first' });
      return false;
    }
    return true;
  };

  const validateStep2 = () => {
    const newErrors = {};

    if (!formData.pin) {
      newErrors.pin = 'PIN is required';
    } else if (!/^\d{4}$/.test(formData.pin)) {
      newErrors.pin = 'PIN must be exactly 4 digits';
    }

    if (formData.pin !== formData.confirmPin) {
      newErrors.confirmPin = 'PINs do not match';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleNext = () => {
    if (step === 1 && validateStep1()) {
      setStep(2);
    }
  };

  const handleBack = () => {
    setStep(prev => prev - 1);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!validateStep2()) return;

    try {
      const account = await createSavingsAccount({
        name: formData.name,
        date_of_birth: formData.date_of_birth,
        gender: formData.gender,
        phone_no: formData.phone_no,
        aadhar_number: formData.aadhar_number,
        address: formData.address,
        pin: formData.pin,
        privilege: formData.privilege,
        initial_balance: parseFloat(formData.initial_balance),
      });

      notifyAccountCreated(
        account.account_number.toString(),
        'SAVINGS',
        formData.name
      );

      toast.success(`Savings account #${account.account_number} created for ${formData.name}!`);
      navigate('/accounts');
    } catch (error) {
      toast.error(error.message || 'Failed to create account');
    }
  };

  const calculateAge = (dob) => {
    if (!dob) return '';
    const today = new Date();
    const birthDate = new Date(dob);
    let age = today.getFullYear() - birthDate.getFullYear();
    const m = today.getMonth() - birthDate.getMonth();
    if (m < 0 || (m === 0 && today.getDate() < birthDate.getDate())) {
      age--;
    }
    return age;
  };

  return (
    <div className="max-w-4xl mx-auto animate-fade-in">
      {/* Header */}
      <div className="flex items-center gap-4 mb-6">
        <button
          onClick={() => navigate('/accounts')}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="page-title">Create Savings Account</h1>
          <p className="text-gray-500 mt-1">Open a new individual savings account</p>
        </div>
      </div>

      {/* Progress Steps */}
      <div className="card p-6 mb-6">
        <div className="flex items-center justify-between">
          {[
            { num: 1, label: 'KYC Verification', icon: Shield, desc: 'Verify Aadhar' },
            { num: 2, label: 'Account Setup', icon: CreditCard, desc: 'PIN & Privileges' },
          ].map((s, idx) => (
            <div key={s.num} className="flex items-center flex-1">
              <div className="flex items-center gap-3">
                <div className={`w-12 h-12 rounded-full flex items-center justify-center ${
                  step > s.num ? 'bg-green-600 text-white' :
                  step === s.num ? 'bg-blue-100 text-blue-600 border-2 border-blue-600' :
                  'bg-gray-100 text-gray-400'
                }`}>
                  {step > s.num ? <CheckCircle className="w-6 h-6" /> : <s.icon className="w-6 h-6" />}
                </div>
                <div className="hidden md:block">
                  <span className={`font-medium block ${step >= s.num ? 'text-gray-900' : 'text-gray-400'}`}>
                    {s.label}
                  </span>
                  <span className="text-xs text-gray-500">{s.desc}</span>
                </div>
              </div>
              {idx < 1 && (
                <div className={`flex-1 h-1 mx-4 rounded ${step > s.num ? 'bg-green-600' : 'bg-gray-200'}`} />
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Form Card */}
      <div className="card p-8">
        <form onSubmit={handleSubmit}>
          
          {/* Step 1: KYC Verification */}
          {step === 1 && (
            <div className="space-y-6 animate-fade-in">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
                  <Shield className="w-6 h-6 text-blue-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">KYC Verification</h2>
                  <p className="text-sm text-gray-500">Enter Aadhar to auto-fetch your details from UIDAI</p>
                </div>
              </div>

              {/* Aadhar Input */}
              <div>
                <label className="input-label flex items-center gap-2">
                  <Hash className="w-4 h-4" />
                  Aadhar Number (UID) *
                </label>
                <div className="flex gap-3">
                  <input
                    type="text"
                    name="aadhar_number"
                    value={formData.aadhar_number}
                    onChange={handleChange}
                    className={`input-field flex-1 font-mono text-lg tracking-wider ${
                      errors.aadhar_number ? 'border-red-500' : ''
                    } ${aadharVerified ? 'border-green-500 bg-green-50' : ''}`}
                    placeholder="Enter 12-digit Aadhar number"
                    maxLength={12}
                    disabled={aadharVerified}
                  />
                  {aadharVerified ? (
                    <button
                      type="button"
                      onClick={() => {
                        setAadharVerified(false);
                        setAadharData(null);
                        setFormData(prev => ({
                          ...prev,
                          name: '',
                          date_of_birth: '',
                          gender: '',
                          phone_no: '',
                          address: '',
                        }));
                      }}
                      className="btn-secondary whitespace-nowrap"
                    >
                      Change Aadhar
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={verifyAadhar}
                      disabled={verifyingAadhar || !formData.aadhar_number.trim()}
                      className="btn-primary whitespace-nowrap min-w-[140px]"
                    >
                      {verifyingAadhar ? (
                        <Loader2 className="w-5 h-5 animate-spin mx-auto" />
                      ) : (
                        <>
                          <Shield className="w-4 h-4 mr-2" />
                          Verify Aadhar
                        </>
                      )}
                    </button>
                  )}
                </div>
                {errors.aadhar_number && (
                  <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                    <AlertCircle className="w-4 h-4" />
                    {errors.aadhar_number}
                  </p>
                )}
              </div>

              {/* Info Box */}
              {!aadharVerified && (
                <div className="p-4 bg-blue-50 border border-blue-200 rounded-xl">
                  <p className="text-sm font-medium text-blue-800 flex items-center gap-2">
                    <AlertCircle className="w-4 h-4" />
                    Enter a valid 12-digit Aadhar number and click "Verify Aadhar" to fetch details from UIDAI database.
                  </p>
                </div>
              )}

              {/* Verified Aadhar Details Display */}
              {aadharVerified && aadharData && (
                <div className="space-y-4">
                  {/* Success Banner */}
                  <div className="p-4 bg-green-50 border border-green-200 rounded-xl">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-green-600 rounded-full flex items-center justify-center">
                        <BadgeCheck className="w-6 h-6 text-white" />
                      </div>
                      <div>
                        <p className="font-semibold text-green-800">Aadhar Verified Successfully!</p>
                        <p className="text-sm text-green-600">Details fetched from UIDAI database</p>
                      </div>
                    </div>
                  </div>

                  {/* Person Details Card */}
                  <div className="bg-gradient-to-br from-blue-800 to-blue-900 text-white p-6 rounded-2xl shadow-xl">
                    <div className="flex items-start justify-between mb-4">
                      <div>
                        <p className="text-blue-300 text-sm">Full Name</p>
                        <h3 className="text-xl font-bold">{aadharData.name}</h3>
                      </div>
                      <span className="px-3 py-1 rounded-full text-xs font-semibold bg-green-500/20 text-green-400 border border-green-500/30">
                        VERIFIED
                      </span>
                    </div>

                    <div className="grid grid-cols-2 gap-4 text-sm">
                      <div>
                        <p className="text-blue-300 flex items-center gap-1">
                          <Hash className="w-3 h-3" /> Aadhar
                        </p>
                        <p className="font-mono text-blue-100">
                          {formData.aadhar_number.replace(/(\d{4})(\d{4})(\d{4})/, '$1 $2 $3')}
                        </p>
                      </div>
                      <div>
                        <p className="text-blue-300 flex items-center gap-1">
                          <Calendar className="w-3 h-3" /> Date of Birth
                        </p>
                        <p className="text-blue-100">
                          {new Date(aadharData.date_of_birth).toLocaleDateString('en-IN', { 
                            year: 'numeric', month: 'short', day: 'numeric' 
                          })} ({calculateAge(aadharData.date_of_birth)} yrs)
                        </p>
                      </div>
                      <div>
                        <p className="text-blue-300 flex items-center gap-1">
                          <User className="w-3 h-3" /> Gender
                        </p>
                        <p className="text-blue-100">{aadharData.gender}</p>
                      </div>
                      <div>
                        <p className="text-blue-300 flex items-center gap-1">
                          <Phone className="w-3 h-3" /> Mobile
                        </p>
                        <p className="text-blue-100">{aadharData.mobile_no}</p>
                      </div>
                      <div className="col-span-2">
                        <p className="text-blue-300 flex items-center gap-1">
                          <MapPin className="w-3 h-3" /> Address
                        </p>
                        <p className="text-blue-100">{aadharData.address}</p>
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {/* Navigation */}
              <div className="flex items-center justify-between pt-6 border-t border-gray-200">
                <button
                  type="button"
                  onClick={() => navigate('/accounts')}
                  className="btn-secondary"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleNext}
                  disabled={!aadharVerified}
                  className="btn-primary"
                >
                  Continue
                  <CheckCircle className="w-4 h-4 ml-2" />
                </button>
              </div>
            </div>
          )}

          {/* Step 2: Account Setup - PIN & Privileges */}
          {step === 2 && (
            <div className="space-y-6 animate-fade-in">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
                  <CreditCard className="w-6 h-6 text-purple-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">Account Setup</h2>
                  <p className="text-sm text-gray-500">Set PIN and account privileges</p>
                </div>
              </div>

              {/* Verified Person Summary */}
              <div className="p-4 bg-green-50 border border-green-200 rounded-xl mb-6">
                <div className="flex items-center gap-3">
                  <BadgeCheck className="w-6 h-6 text-green-600" />
                  <div>
                    <p className="font-semibold text-green-800">{formData.name}</p>
                    <p className="text-sm text-green-600">Aadhar: XXXX-XXXX-{formData.aadhar_number.slice(-4)}</p>
                  </div>
                </div>
              </div>

              {/* PIN Section */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <label className="input-label">4-Digit Transaction PIN *</label>
                  <input
                    type="password"
                    name="pin"
                    value={formData.pin}
                    onChange={handleChange}
                    className={`input-field ${errors.pin ? 'border-red-500' : ''}`}
                    placeholder="Enter 4-digit PIN"
                    maxLength={4}
                  />
                  {errors.pin && <p className="text-red-500 text-sm mt-1">{errors.pin}</p>}
                  <p className="text-xs text-gray-500 mt-1">Use 1234 for demo</p>
                </div>

                <div>
                  <label className="input-label">Confirm PIN *</label>
                  <input
                    type="password"
                    name="confirmPin"
                    value={formData.confirmPin}
                    onChange={handleChange}
                    className={`input-field ${errors.confirmPin ? 'border-red-500' : ''}`}
                    placeholder="Confirm 4-digit PIN"
                    maxLength={4}
                  />
                  {errors.confirmPin && <p className="text-red-500 text-sm mt-1">{errors.confirmPin}</p>}
                </div>
              </div>

              {/* Privilege Selection */}
              <div>
                <label className="input-label">Account Privilege</label>
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

              {/* Final Summary */}
              <div className="p-6 bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200 rounded-xl">
                <h3 className="font-semibold text-gray-900 mb-4 flex items-center gap-2">
                  <CheckCircle className="w-5 h-5 text-blue-600" />
                  Account Summary
                </h3>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm">
                  <div>
                    <span className="text-gray-500">Account Holder</span>
                    <p className="font-medium text-gray-900">{formData.name}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Aadhar</span>
                    <p className="font-mono text-gray-900">XXXX-XXXX-{formData.aadhar_number.slice(-4)}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Account Type</span>
                    <p className="font-medium text-gray-900">Savings Account</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Mobile</span>
                    <p className="font-medium text-gray-900">{formData.phone_no}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Gender</span>
                    <p className="font-medium text-gray-900">{formData.gender}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Privilege</span>
                    <p className="font-medium text-gray-900">{formData.privilege}</p>
                  </div>
                </div>
              </div>

              {/* Navigation */}
              <div className="flex items-center justify-between pt-6 border-t border-gray-200">
                <button
                  type="button"
                  onClick={handleBack}
                  className="btn-secondary"
                >
                  <ArrowLeft className="w-4 h-4 mr-2" />
                  Back
                </button>
                <button
                  type="submit"
                  disabled={isLoading}
                  className="btn-success flex items-center gap-2"
                >
                  {isLoading ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin" />
                      Creating Account...
                    </>
                  ) : (
                    <>
                      <PiggyBank className="w-4 h-4" />
                      Create Savings Account
                    </>
                  )}
                </button>
              </div>
            </div>
          )}
        </form>
      </div>
    </div>
  );
};

export default CreateSavingsAccount;
