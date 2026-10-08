import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useUserStore } from '../../store/userStore';
import {
  ArrowLeft,
  User,
  Lock,
  Shield,
  Eye,
  EyeOff,
  Check,
  AlertCircle,
  Loader2,
  UserPlus,
  AtSign,
} from 'lucide-react';
import toast from 'react-hot-toast';

const CreateUserPage = () => {
  const navigate = useNavigate();
  const { createUser } = useUserStore();

  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [formData, setFormData] = useState({
    username: '',
    login_id: '',
    role: 'TELLER',
    password: '',
    confirmPassword: '',
  });
  const [errors, setErrors] = useState({});

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    setErrors(prev => ({ ...prev, [name]: '' }));
  };

  const validateForm = () => {
    const newErrors = {};

    // Username validation (display name)
    if (!formData.username) {
      newErrors.username = 'Username is required';
    } else if (formData.username.length < 1 || formData.username.length > 255) {
      newErrors.username = 'Username must be between 1 and 255 characters';
    }

    // Login ID validation
    if (!formData.login_id) {
      newErrors.login_id = 'Login ID is required';
    } else if (formData.login_id.length < 3 || formData.login_id.length > 50) {
      newErrors.login_id = 'Login ID must be between 3 and 50 characters';
    } else if (!/^[a-zA-Z0-9._-]+$/.test(formData.login_id)) {
      newErrors.login_id = 'Login ID can only contain letters, numbers, dots, hyphens, and underscores';
    }

    // Password validation
    if (!formData.password) {
      newErrors.password = 'Password is required';
    } else if (formData.password.length < 8) {
      newErrors.password = 'Password must be at least 8 characters';
    }

    // Confirm password
    if (!formData.confirmPassword) {
      newErrors.confirmPassword = 'Please confirm your password';
    } else if (formData.password !== formData.confirmPassword) {
      newErrors.confirmPassword = 'Passwords do not match';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!validateForm()) return;

    setLoading(true);
    try {
      await createUser({
        username: formData.username,
        login_id: formData.login_id,
        password: formData.password,
        role: formData.role,
      });

      toast.success('User created successfully!');
      navigate('/users');
    } catch (error) {
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to create user';
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const getPasswordStrength = () => {
    const password = formData.password;
    if (!password) return { strength: 0, label: '', color: '' };
    
    let strength = 0;
    if (password.length >= 8) strength++;
    if (/[a-z]/.test(password)) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/\d/.test(password)) strength++;
    if (/[!@#$%^&*(),.?":{}|<>]/.test(password)) strength++;

    const levels = [
      { label: 'Very Weak', color: 'bg-red-500' },
      { label: 'Weak', color: 'bg-orange-500' },
      { label: 'Fair', color: 'bg-yellow-500' },
      { label: 'Good', color: 'bg-blue-500' },
      { label: 'Strong', color: 'bg-green-500' },
    ];

    return { strength, ...levels[strength - 1] || levels[0] };
  };

  const passwordStrength = getPasswordStrength();

  return (
    <div className="animate-fade-in max-w-2xl mx-auto">
      {/* Header */}
      <div className="flex items-center gap-4 mb-6">
        <button
          onClick={() => navigate('/users')}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="page-title">Create New User</h1>
          <p className="text-gray-500">Add a new user to the system</p>
        </div>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="card p-6 mb-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
              <UserPlus className="w-6 h-6 text-primary-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">User Information</h2>
              <p className="text-sm text-gray-500">Enter the user's details</p>
            </div>
          </div>

          <div className="space-y-6">
            {/* Username (Display Name) */}
            <div>
              <label className="label flex items-center gap-2">
                <User className="w-4 h-4" />
                Username (Display Name) *
              </label>
              <input
                type="text"
                name="username"
                value={formData.username}
                onChange={handleInputChange}
                placeholder="e.g., John Doe"
                className={`input w-full ${errors.username ? 'border-red-500' : ''}`}
              />
              {errors.username && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.username}
                </p>
              )}
              <p className="text-xs text-gray-500 mt-1">This will be displayed as the user's name in the system</p>
            </div>

            {/* Login ID */}
            <div>
              <label className="label flex items-center gap-2">
                <AtSign className="w-4 h-4" />
                Login ID *
              </label>
              <input
                type="text"
                name="login_id"
                value={formData.login_id}
                onChange={handleInputChange}
                placeholder="e.g., john.doe"
                className={`input w-full ${errors.login_id ? 'border-red-500' : ''}`}
              />
              {errors.login_id && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.login_id}
                </p>
              )}
              <p className="text-xs text-gray-500 mt-1">Unique identifier used for login (letters, numbers, dots, hyphens, underscores)</p>
            </div>

            {/* Role */}
            <div>
              <label className="label flex items-center gap-2">
                <Shield className="w-4 h-4" />
                User Role *
              </label>
              <div className="grid grid-cols-3 gap-4">
                {[
                  { value: 'TELLER', label: 'Teller', description: 'Basic operations', bgColor: 'bg-green-50', borderColor: 'border-green-500', textColor: 'text-green-600' },
                  { value: 'MANAGER', label: 'Manager', description: 'Team oversight', bgColor: 'bg-blue-50', borderColor: 'border-blue-500', textColor: 'text-blue-600' },
                  { value: 'ADMIN', label: 'Admin', description: 'Full access', bgColor: 'bg-purple-50', borderColor: 'border-purple-500', textColor: 'text-purple-600' },
                ].map((role) => (
                  <label
                    key={role.value}
                    className={`p-4 rounded-lg border-2 cursor-pointer transition-all ${
                      formData.role === role.value
                        ? `${role.borderColor} ${role.bgColor}`
                        : 'border-gray-200 hover:border-gray-300'
                    }`}
                  >
                    <input
                      type="radio"
                      name="role"
                      value={role.value}
                      checked={formData.role === role.value}
                      onChange={handleInputChange}
                      className="hidden"
                    />
                    <div className="flex items-center gap-2 mb-1">
                      <div className={`w-4 h-4 rounded-full border-2 flex items-center justify-center ${
                        formData.role === role.value
                          ? `${role.borderColor} ${role.bgColor}`
                          : 'border-gray-300'
                      }`}>
                        {formData.role === role.value && (
                          <Check className={`w-3 h-3 ${role.textColor}`} />
                        )}
                      </div>
                      <span className="font-medium">{role.label}</span>
                    </div>
                    <p className="text-xs text-gray-500">{role.description}</p>
                  </label>
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* Password Section */}
        <div className="card p-6 mb-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-12 h-12 bg-amber-100 rounded-xl flex items-center justify-center">
              <Lock className="w-6 h-6 text-amber-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Security</h2>
              <p className="text-sm text-gray-500">Set the user's password</p>
            </div>
          </div>

          <div className="space-y-6">
            {/* Password */}
            <div>
              <label className="label flex items-center gap-2">
                <Lock className="w-4 h-4" />
                Password *
              </label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  name="password"
                  value={formData.password}
                  onChange={handleInputChange}
                  placeholder="Enter password (min 8 characters)"
                  className={`input w-full pr-10 ${errors.password ? 'border-red-500' : ''}`}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>
              {errors.password && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.password}
                </p>
              )}
              
              {/* Password Strength Indicator */}
              {formData.password && (
                <div className="mt-2">
                  <div className="flex gap-1 mb-1">
                    {[1, 2, 3, 4, 5].map((level) => (
                      <div
                        key={level}
                        className={`h-1 flex-1 rounded ${
                          level <= passwordStrength.strength
                            ? passwordStrength.color
                            : 'bg-gray-200'
                        }`}
                      />
                    ))}
                  </div>
                  <p className="text-xs text-gray-500">
                    Password strength: <span className="font-medium">{passwordStrength.label}</span>
                  </p>
                </div>
              )}
            </div>

            {/* Confirm Password */}
            <div>
              <label className="label flex items-center gap-2">
                <Lock className="w-4 h-4" />
                Confirm Password *
              </label>
              <div className="relative">
                <input
                  type={showConfirmPassword ? 'text' : 'password'}
                  name="confirmPassword"
                  value={formData.confirmPassword}
                  onChange={handleInputChange}
                  placeholder="Confirm password"
                  className={`input w-full pr-10 ${errors.confirmPassword ? 'border-red-500' : ''}`}
                />
                <button
                  type="button"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showConfirmPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>
              {errors.confirmPassword && (
                <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                  <AlertCircle className="w-4 h-4" /> {errors.confirmPassword}
                </p>
              )}
              {formData.confirmPassword && formData.password === formData.confirmPassword && (
                <p className="text-green-500 text-sm mt-1 flex items-center gap-1">
                  <Check className="w-4 h-4" /> Passwords match
                </p>
              )}
            </div>
          </div>
        </div>

        {/* Actions */}
        <div className="flex gap-4">
          <button
            type="button"
            onClick={() => navigate('/users')}
            className="btn-secondary flex-1"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={loading}
            className="btn-primary flex-1 flex items-center justify-center gap-2"
          >
            {loading ? (
              <>
                <Loader2 className="w-5 h-5 animate-spin" />
                Creating User...
              </>
            ) : (
              <>
                <UserPlus className="w-5 h-5" />
                Create User
              </>
            )}
          </button>
        </div>
      </form>
    </div>
  );
};

export default CreateUserPage;
