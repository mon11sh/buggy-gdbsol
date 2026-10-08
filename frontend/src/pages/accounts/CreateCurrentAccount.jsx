import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAccountStore } from '../../store/accountStore';
import { useNotificationStore } from '../../store/notificationStore';
import {
  ArrowLeft,
  Building2,
  Building,
  Globe,
  CreditCard,
  Shield,
  CheckCircle,
  AlertCircle,
  Loader2,
  FileText,
  BadgeCheck,
  User,
  Phone,
  Mail,
  MapPin,
  Calendar,
  Hash,
  ExternalLink,
  Users,
  Briefcase,
} from 'lucide-react';
import toast from 'react-hot-toast';

const CreateCurrentAccount = () => {
  const navigate = useNavigate();
  const { createCurrentAccount, verifyCompanyRegistration, isLoading } = useAccountStore();
  const { notifyAccountCreated } = useNotificationStore();

  const [formData, setFormData] = useState({
    registration_no: '',
    // These will be auto-filled from CIN verification
    company_name: '',
    company_type: '',
    company_address: '',
    company_website: '',
    company_email: '',
    company_phone: '',
    // User entered fields
    authorized_person: '',
    designation: '',
    mobile: '',
    email: '',
    pin: '',
    confirmPin: '',
    privilege: 'SILVER',
  });

  const [errors, setErrors] = useState({});
  const [step, setStep] = useState(1);
  const [companyVerified, setCompanyVerified] = useState(false);
  const [verifyingCompany, setVerifyingCompany] = useState(false);
  const [companyData, setCompanyData] = useState(null);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    
    if (errors[name]) {
      setErrors(prev => ({ ...prev, [name]: '' }));
    }

    // Reset company verification if registration number changes
    if (name === 'registration_no') {
      setCompanyVerified(false);
      setCompanyData(null);
      // Clear auto-filled company fields
      setFormData(prev => ({
        ...prev,
        registration_no: value,
        company_name: '',
        company_type: '',
        company_address: '',
        company_website: '',
        company_email: '',
        company_phone: '',
      }));
    }
  };

  // Step 1: Verify CIN - just need CIN to be verified
  const validateStep1 = () => {
    const newErrors = {};

    if (!formData.registration_no.trim()) {
      newErrors.registration_no = 'Company registration number (CIN) is required';
    } else if (!companyVerified) {
      newErrors.registration_no = 'Please verify the CIN first';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  // Step 2: Authorized person details
  const validateStep2 = () => {
    const newErrors = {};

    if (!formData.authorized_person.trim()) {
      newErrors.authorized_person = 'Authorized person name is required';
    }

    if (!formData.designation.trim()) {
      newErrors.designation = 'Designation is required';
    }

    if (!formData.mobile.trim()) {
      newErrors.mobile = 'Mobile number is required';
    } else if (!/^\d{10}$/.test(formData.mobile)) {
      newErrors.mobile = 'Mobile must be 10 digits';
    }

    if (!formData.email.trim()) {
      newErrors.email = 'Email is required';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      newErrors.email = 'Invalid email format';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const validateStep3 = () => {
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

  const verifyCompany = async () => {
    if (!formData.registration_no.trim()) {
      setErrors({ registration_no: 'Registration number is required' });
      return;
    }

    const cinUpper = formData.registration_no.toUpperCase().trim();
    
    if (!/^[A-Z0-9]{21}$/.test(cinUpper)) {
      setErrors({ registration_no: 'CIN must be exactly 21 alphanumeric characters' });
      return;
    }

    setVerifyingCompany(true);
    setErrors({});

    try {
      const data = await verifyCompanyRegistration(cinUpper);
      
      if (data) {
        setCompanyVerified(true);
        setCompanyData(data);
        
        // Auto-fill ALL company details from verified data
        setFormData(prev => ({
          ...prev,
          registration_no: cinUpper,
          company_name: data.company_name || '',
          company_type: data.type || data.company_type || '',
          company_address: data.address || data.company_address || '',
          company_website: data.website || '',
          company_email: data.email || '',
          company_phone: data.phone || '',
        }));
        
        toast.success(`Company "${data.company_name}" verified successfully!`);
      } else {
        toast.error('CIN not found in registry');
        setErrors({ registration_no: 'Company not found in MCA registry' });
      }
    } catch (error) {
      toast.error(error.message || 'Failed to verify company');
      setErrors({ registration_no: error.message || 'Verification failed' });
    } finally {
      setVerifyingCompany(false);
    }
  };

  const handleNext = () => {
    if (step === 1 && validateStep1()) {
      setStep(2);
    } else if (step === 2 && validateStep2()) {
      setStep(3);
    }
  };

  const handleBack = () => {
    setStep(prev => prev - 1);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!validateStep3()) return;

    try {
      // Only send fields that the backend expects
      const account = await createCurrentAccount({
        name: formData.authorized_person,
        company_name: formData.company_name,
        registration_no: formData.registration_no,
        website: formData.company_website || null,
        pin: formData.pin,
        privilege: formData.privilege,
      });

      // Send notification with company name
      notifyAccountCreated(
        account.account_number.toString(),
        'CURRENT',
        formData.company_name
      );

      toast.success(`Current account #${account.account_number} created for ${formData.company_name}!`);
      navigate('/accounts');
    } catch (error) {
      toast.error(error.message || 'Failed to create account');
    }
  };

  return (
    <div className="max-w-3xl mx-auto animate-fade-in">
      {/* Header */}
      <div className="flex items-center gap-4 mb-6">
        <button
          onClick={() => navigate('/accounts')}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="page-title">Create Current Account</h1>
          <p className="text-gray-500 mt-1">Open a new business/corporate current account</p>
        </div>
      </div>

      {/* Progress Steps */}
      <div className="card p-6 mb-6">
        <div className="flex items-center justify-between">
          {[
            { num: 1, label: 'Verify Company', icon: Shield, desc: 'CIN Verification' },
            { num: 2, label: 'Authorized Person', icon: User, desc: 'Signatory Details' },
            { num: 3, label: 'Account Setup', icon: CreditCard, desc: 'PIN & Privileges' },
          ].map((s, idx) => (
            <div key={s.num} className="flex items-center">
              <div className={`flex items-center gap-3 ${step >= s.num ? 'text-green-600' : 'text-gray-400'}`}>
                <div className={`w-12 h-12 rounded-full flex items-center justify-center ${
                  step > s.num ? 'bg-green-600 text-white' :
                  step === s.num ? 'bg-green-100 text-green-600 border-2 border-green-600' :
                  'bg-gray-100 text-gray-400'
                }`}>
                  {step > s.num ? <CheckCircle className="w-6 h-6" /> : <s.icon className="w-6 h-6" />}
                </div>
                <div className="hidden md:block">
                  <span className="font-medium block">{s.label}</span>
                  <span className="text-xs text-gray-500">{s.desc}</span>
                </div>
              </div>
              {idx < 2 && (
                <div className={`w-12 md:w-20 h-1 mx-4 rounded ${step > s.num ? 'bg-green-600' : 'bg-gray-200'}`} />
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Form Card */}
      <div className="card p-8">
        <form onSubmit={handleSubmit}>
          {/* Step 1: Company Verification - CIN First */}
          {step === 1 && (
            <div className="space-y-6 animate-fade-in">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 bg-amber-100 rounded-xl flex items-center justify-center">
                  <FileText className="w-6 h-6 text-amber-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">Company Verification</h2>
                  <p className="text-sm text-gray-500">Enter CIN to fetch company details from MCA/ROC registry</p>
                </div>
              </div>

              {/* CIN Input */}
              <div>
                <label className="input-label flex items-center gap-2">
                  <Hash className="w-4 h-4" />
                  Company Identification Number (CIN) *
                </label>
                <div className="flex gap-3">
                  <input
                    type="text"
                    name="registration_no"
                    value={formData.registration_no}
                    onChange={handleChange}
                    className={`input-field flex-1 uppercase font-mono text-lg tracking-wider ${
                      errors.registration_no ? 'border-red-500' : ''
                    } ${companyVerified ? 'border-green-500 bg-green-50' : ''}`}
                    placeholder="Enter 21-character CIN"
                    maxLength={21}
                    disabled={companyVerified}
                  />
                  {companyVerified ? (
                    <button
                      type="button"
                      onClick={() => {
                        setCompanyVerified(false);
                        setCompanyData(null);
                        setFormData(prev => ({
                          ...prev,
                          company_name: '',
                          company_type: '',
                          company_address: '',
                          company_website: '',
                          company_email: '',
                          company_phone: '',
                        }));
                      }}
                      className="btn-secondary whitespace-nowrap"
                    >
                      Change CIN
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={verifyCompany}
                      disabled={verifyingCompany || !formData.registration_no.trim()}
                      className="btn-primary whitespace-nowrap min-w-[140px]"
                    >
                      {verifyingCompany ? (
                        <Loader2 className="w-5 h-5 animate-spin mx-auto" />
                      ) : (
                        <>
                          <Shield className="w-4 h-4 mr-2" />
                          Verify CIN
                        </>
                      )}
                    </button>
                  )}
                </div>
                {errors.registration_no && (
                  <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                    <AlertCircle className="w-4 h-4" />
                    {errors.registration_no}
                  </p>
                )}
              </div>

              {/* Info Box - Only show if not verified */}
              {!companyVerified && (
                <div className="p-4 bg-blue-50 border border-blue-200 rounded-xl">
                  <p className="text-sm font-medium text-blue-800 flex items-center gap-2">
                    <AlertCircle className="w-4 h-4" />
                    Enter a valid 21-character CIN (Corporate Identification Number) and click "Verify CIN" to fetch company details from MCA registry.
                  </p>
                </div>
              )}

              {/* Verified Company Details - Show after CIN verification */}
              {companyVerified && companyData && (
                <div className="space-y-4">
                  {/* Success Banner */}
                  <div className="p-4 bg-green-50 border border-green-200 rounded-xl">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-green-600 rounded-full flex items-center justify-center">
                        <BadgeCheck className="w-6 h-6 text-white" />
                      </div>
                      <div>
                        <p className="font-semibold text-green-800">Company Verified Successfully!</p>
                        <p className="text-sm text-green-600">Details fetched from MCA/ROC registry</p>
                      </div>
                    </div>
                  </div>

                  {/* Company Details Card */}
                  <div className="bg-gradient-to-br from-slate-800 to-slate-900 text-white p-6 rounded-2xl shadow-xl">
                    <div className="flex items-start justify-between mb-4">
                      <div>
                        <p className="text-slate-400 text-sm">Company Name</p>
                        <h3 className="text-xl font-bold">{companyData.company_name}</h3>
                      </div>
                      <span className="px-3 py-1 rounded-full text-xs font-semibold bg-green-500/20 text-green-400 border border-green-500/30">
                        {companyData.is_valid ? 'VERIFIED' : 'INVALID'}
                      </span>
                    </div>

                    <div className="grid grid-cols-2 gap-4 text-sm">
                      <div>
                        <p className="text-slate-400 flex items-center gap-1">
                          <Hash className="w-3 h-3" /> CIN
                        </p>
                        <p className="font-mono text-slate-200">{formData.registration_no}</p>
                      </div>
                      <div>
                        <p className="text-slate-400 flex items-center gap-1">
                          <Building className="w-3 h-3" /> Type
                        </p>
                        <p className="text-slate-200">{companyData.type || 'N/A'}</p>
                      </div>
                      <div>
                        <p className="text-slate-400 flex items-center gap-1">
                          <Calendar className="w-3 h-3" /> Incorporated
                        </p>
                        <p className="text-slate-200">{companyData.incorporation_date ? new Date(companyData.incorporation_date).toLocaleDateString('en-IN', { year: 'numeric', month: 'short', day: 'numeric' }) : 'N/A'}</p>
                      </div>
                      <div>
                        <p className="text-slate-400 flex items-center gap-1">
                          <Globe className="w-3 h-3" /> Website
                        </p>
                        <p className="text-slate-200">{companyData.website || 'N/A'}</p>
                      </div>
                      <div className="col-span-2">
                        <p className="text-slate-400 flex items-center gap-1">
                          <MapPin className="w-3 h-3" /> Registered Address
                        </p>
                        <p className="text-slate-200">{companyData.address || 'N/A'}</p>
                      </div>
                      <div>
                        <p className="text-slate-400 flex items-center gap-1">
                          <Mail className="w-3 h-3" /> Email
                        </p>
                        <p className="text-slate-200">{companyData.email || 'N/A'}</p>
                      </div>
                      <div>
                        <p className="text-slate-400 flex items-center gap-1">
                          <Phone className="w-3 h-3" /> Phone
                        </p>
                        <p className="text-slate-200">{companyData.phone || 'N/A'}</p>
                      </div>
                      <div>
                        <p className="text-slate-400">Paid-up Capital</p>
                        <p className="text-slate-200">₹{companyData.paid_up_capital || 'N/A'}</p>
                      </div>
                    </div>

                    <div className="mt-4 pt-4 border-t border-slate-700">
                      <p className="text-slate-400 text-sm mb-2 flex items-center gap-1">
                        <Users className="w-3 h-3" /> Directors
                      </p>
                      <div className="flex flex-wrap gap-2">
                        {companyData.directors && Array.isArray(companyData.directors) && companyData.directors.length > 0 ? (
                          companyData.directors.map((director, i) => (
                            <span key={i} className="px-3 py-1 bg-slate-700 rounded-full text-sm">
                              {director}
                            </span>
                          ))
                        ) : (
                          <span className="text-slate-400 text-sm">No directors listed</span>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Step 2: Authorized Person Details */}
          {step === 2 && (
            <div className="space-y-6 animate-fade-in">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
                  <User className="w-6 h-6 text-blue-600" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold text-gray-900">Authorized Signatory Details</h2>
                  <p className="text-sm text-gray-500">Person authorized to operate the account</p>
                </div>
              </div>

              {/* Company Summary from Step 1 */}
              <div className="p-4 bg-green-50 border border-green-200 rounded-xl mb-6">
                <div className="flex items-center gap-3">
                  <BadgeCheck className="w-6 h-6 text-green-600" />
                  <div>
                    <p className="font-semibold text-green-800">{formData.company_name}</p>
                    <p className="text-sm text-green-600">CIN: {formData.registration_no}</p>
                  </div>
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
                    name="authorized_person"
                    value={formData.authorized_person}
                    onChange={handleChange}
                    className={`input-field ${errors.authorized_person ? 'border-red-500' : ''}`}
                    placeholder="Enter full name"
                  />
                  {errors.authorized_person && <p className="text-red-500 text-sm mt-1">{errors.authorized_person}</p>}
                  
                  {/* Quick fill from directors */}
                  {companyData?.directors && (
                    <div className="mt-2 flex flex-wrap gap-1">
                      <span className="text-xs text-gray-500">Directors:</span>
                      {companyData.directors.map((dir, i) => (
                        <button
                          key={i}
                          type="button"
                          onClick={() => setFormData(prev => ({ ...prev, authorized_person: dir }))}
                          className="text-xs px-2 py-0.5 bg-blue-100 text-blue-700 rounded hover:bg-blue-200"
                        >
                          {dir}
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                <div>
                  <label className="input-label">Designation *</label>
                  <select
                    name="designation"
                    value={formData.designation}
                    onChange={handleChange}
                    className={`input-field ${errors.designation ? 'border-red-500' : ''}`}
                  >
                    <option value="">Select designation</option>
                    <option value="Director">Director</option>
                    <option value="Managing Director">Managing Director</option>
                    <option value="CEO">Chief Executive Officer (CEO)</option>
                    <option value="CFO">Chief Financial Officer (CFO)</option>
                    <option value="Company Secretary">Company Secretary</option>
                    <option value="Authorized Signatory">Authorized Signatory</option>
                    <option value="Partner">Partner</option>
                    <option value="Proprietor">Proprietor</option>
                  </select>
                  {errors.designation && <p className="text-red-500 text-sm mt-1">{errors.designation}</p>}
                </div>

                <div>
                  <label className="input-label flex items-center gap-2">
                    <Phone className="w-4 h-4" />
                    Mobile Number *
                  </label>
                  <input
                    type="tel"
                    name="mobile"
                    value={formData.mobile}
                    onChange={handleChange}
                    className={`input-field ${errors.mobile ? 'border-red-500' : ''}`}
                    placeholder="10-digit mobile number"
                    maxLength={10}
                  />
                  {errors.mobile && <p className="text-red-500 text-sm mt-1">{errors.mobile}</p>}
                </div>

                <div>
                  <label className="input-label flex items-center gap-2">
                    <Mail className="w-4 h-4" />
                    Email Address *
                  </label>
                  <input
                    type="email"
                    name="email"
                    value={formData.email}
                    onChange={handleChange}
                    className={`input-field ${errors.email ? 'border-red-500' : ''}`}
                    placeholder="authorized@company.com"
                  />
                  {errors.email && <p className="text-red-500 text-sm mt-1">{errors.email}</p>}
                </div>
              </div>

              <div className="p-4 bg-amber-50 border border-amber-200 rounded-lg">
                <div className="flex items-start gap-3">
                  <AlertCircle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
                  <div>
                    <p className="text-sm font-medium text-amber-800">Important</p>
                    <p className="text-sm text-amber-600 mt-1">
                      The authorized person must be a director or have a board resolution authorizing them to operate the account.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Step 3: Account Setup */}
          {step === 3 && (
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

                <div className="md:col-span-2">
                  <label className="input-label">Account Privilege</label>
                  <div className="grid grid-cols-3 gap-3">
                    {[
                      { value: 'SILVER', label: 'Silver', desc: 'Standard limits', color: 'slate' },
                      { value: 'GOLD', label: 'Gold', desc: 'Enhanced limits', color: 'amber' },
                      { value: 'PREMIUM', label: 'Premium', desc: 'Maximum limits', color: 'purple' },
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
                        <p className={`font-semibold ${formData.privilege === priv.value ? 'text-green-700' : 'text-gray-900'}`}>
                          {priv.label}
                        </p>
                        <p className="text-xs text-gray-500 mt-1">{priv.desc}</p>
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Final Summary */}
              <div className="p-6 bg-gradient-to-r from-green-50 to-emerald-50 border border-green-200 rounded-xl">
                <h3 className="font-semibold text-gray-900 mb-4 flex items-center gap-2">
                  <CheckCircle className="w-5 h-5 text-green-600" />
                  Account Summary
                </h3>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm">
                  <div>
                    <span className="text-gray-500">Company</span>
                    <p className="font-medium text-gray-900">{formData.company_name}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">CIN</span>
                    <p className="font-mono text-gray-900 text-xs">{formData.registration_no}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Account Type</span>
                    <p className="font-medium text-gray-900">Current Account</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Authorized Person</span>
                    <p className="font-medium text-gray-900">{formData.authorized_person}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Designation</span>
                    <p className="font-medium text-gray-900">{formData.designation}</p>
                  </div>
                  <div>
                    <span className="text-gray-500">Privilege</span>
                    <p className="font-medium text-gray-900">{formData.privilege}</p>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Navigation Buttons */}
          <div className="flex items-center justify-between mt-8 pt-6 border-t border-gray-200">
            {step > 1 ? (
              <button type="button" onClick={handleBack} className="btn-secondary flex items-center gap-2">
                <ArrowLeft className="w-4 h-4" />
                Back
              </button>
            ) : (
              <button type="button" onClick={() => navigate('/accounts')} className="btn-secondary">
                Cancel
              </button>
            )}

            {step < 3 ? (
              <button 
                type="button" 
                onClick={handleNext} 
                className="btn-success flex items-center gap-2"
                disabled={step === 1 && !companyVerified}
              >
                Continue
                <CheckCircle className="w-4 h-4" />
              </button>
            ) : (
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
                    <Building2 className="w-4 h-4" />
                    Create Current Account
                  </>
                )}
              </button>
            )}
          </div>
        </form>
      </div>
    </div>
  );
};

export default CreateCurrentAccount;
