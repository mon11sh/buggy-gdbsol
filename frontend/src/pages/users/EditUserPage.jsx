import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
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
  UserCog,
  AtSign,
} from 'lucide-react';
import toast from 'react-hot-toast';

const EditUserPage = () => {
  const navigate = useNavigate();
  const { loginId } = useParams();
  const { fetchUserByLoginId, updateUser } = useUserStore();

  const [loading, setLoading] = useState(false);
  const [fetching, setFetching] = useState(true);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [userData, setUserData] = useState(null);
  const [formData, setFormData] = useState({
    username: '',
    role: '',
    password: '',
    confirmPassword: '',
  });
  const [errors, setErrors] = useState({});

  useEffect(() => {
    loadUser();
  }, [loginId]);

  const loadUser = async () => {
    setFetching(true);
    try {
      const user = await fetchUserByLoginId(loginId);
      if (user) {
        setUserData(user);
        setFormData({
          username: user.username || '',
          role: user.role || 'TELLER',
          password: '',
          confirmPassword: '',
        });
      } else {
        toast.error('User not found');
        navigate('/users');
      }
    } catch (error) {
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to load user';
      toast.error(errorMessage);
      navigate('/users');
    } finally {
      setFetching(false);
    }
  };

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    setErrors(prev => ({ ...prev, [name]: '' }));
  };

  const validateForm = () => {
    const newErrors = {};

    // Username validation (optional but if provided must be valid)
    if (formData.username && (formData.username.length < 1 || formData.username.length > 255)) {
      newErrors.username = 'Username must be between 1 and 255 characters';
    }

    // Password validation (optional - only if changing password)
    if (formData.password) {
      if (formData.password.length < 8) {
        newErrors.password = 'Password must be at least 8 characters';
      }
      // Confirm password only required if password is provided
      if (!formData.confirmPassword) {
        newErrors.confirmPassword = 'Please confirm your password';
      } else if (formData.password !== formData.confirmPassword) {
        newErrors.confirmPassword = 'Passwords do not match';
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!validateForm()) return;

    setLoading(true);
    try {
      // Only include fields that have values
      const updateData = {};
      if (formData.username && formData.username !== userData.username) {
        updateData.username = formData.username;
      }
      if (formData.role && formData.role !== userData.role) {
        updateData.role = formData.role;
      }
      if (formData.password) {
        updateData.password = formData.password;
      }

      // Check if there are any changes
      if (Object.keys(updateData).length === 0) {
        toast.info('No changes to save');
        return;
      }

      await updateUser(loginId, updateData);
      toast.success('User updated successfully!');
      navigate('/users');
    } catch (error) {
      const errorMessage = error.response?.data?.detail || error.message || 'Failed to update user';
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

  if (fetching) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <Loader2 className="w-10 h-10 animate-spin text-primary-500 mx-auto mb-4" />
          <p className="text-gray-500">Loading user details...</p>
        </div>
      </div>
    );
  }

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
          <h1 className="page-title">Edit User</h1>
          <p className="text-gray-500">Update user information for {loginId}</p>
        </div>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="card p-6 mb-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
              <UserCog className="w-6 h-6 text-primary-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">User Information</h2>
              <p className="text-sm text-gray-500">Update the user's details</p>
            </div>
          </div>

          <div className="space-y-6">
            {/* Login ID (Read-only) */}
            <div>
              <label className="label flex items-center gap-2">
                <AtSign className="w-4 h-4" />
                Login ID
              </label>
              <input
                type="text"
                value={loginId}
                disabled
                className="input w-full bg-gray-100 cursor-not-allowed"
              />
              <p className="text-xs text-gray-500 mt-1">Login ID cannot be changed</p>
            </div>

            {/* Username (Display Name) */}
            <div>
              <label className="label flex items-center gap-2">
                <User className="w-4 h-4" />
                Username (Display Name)
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

            {/* Role */}
            <div>
              <label className="label flex items-center gap-2">
                <Shield className="w-4 h-4" />
                Role
              </label>
              <select
                name="role"
                value={formData.role}
                onChange={handleInputChange}
                className="input w-full"
              >
                <option value="TELLER">Teller</option>
                <option value="MANAGER">Manager</option>
                <option value="ADMIN">Admin</option>
              </select>
              <p className="text-xs text-gray-500 mt-1">User's access level in the system</p>
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
              <h2 className="text-lg font-semibold text-gray-900">Change Password</h2>
              <p className="text-sm text-gray-500">Leave blank to keep current password</p>
            </div>
          </div>

          <div className="space-y-6">
            {/* New Password */}
            <div>
              <label className="label flex items-center gap-2">
                <Lock className="w-4 h-4" />
                New Password
              </label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  name="password"
                  value={formData.password}
                  onChange={handleInputChange}
                  placeholder="Leave blank to keep current"
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
                        className={`h-1 flex-1 rounded-full ${
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

            {/* Confirm New Password */}
            {formData.password && (
              <div>
                <label className="label flex items-center gap-2">
                  <Check className="w-4 h-4" />
                  Confirm New Password
                </label>
                <div className="relative">
                  <input
                    type={showConfirmPassword ? 'text' : 'password'}
                    name="confirmPassword"
                    value={formData.confirmPassword}
                    onChange={handleInputChange}
                    placeholder="Re-enter new password"
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
                  <p className="text-green-600 text-sm mt-1 flex items-center gap-1">
                    <Check className="w-4 h-4" /> Passwords match
                  </p>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex justify-end gap-4">
          <button
            type="button"
            onClick={() => navigate('/users')}
            className="btn btn-secondary"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={loading}
            className="btn btn-primary flex items-center gap-2"
          >
            {loading ? (
              <>
                <Loader2 className="w-5 h-5 animate-spin" />
                Saving...
              </>
            ) : (
              <>
                <Check className="w-5 h-5" />
                Save Changes
              </>
            )}
          </button>
        </div>
      </form>
    </div>
  );
};

export default EditUserPage;
