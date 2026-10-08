import { useState } from 'react';
import { useAuthStore } from '../../store/authStore';
import {
  User,
  Mail,
  Phone,
  Shield,
  Calendar,
  Clock,
  Edit,
  Camera,
  Save,
  X,
  AlertCircle,
  Loader2,
  MapPin,
  Building,
} from 'lucide-react';
import { format } from 'date-fns';
import toast from 'react-hot-toast';

const ProfilePage = () => {
  const { user, updateProfile } = useAuthStore();
  
  const [isEditing, setIsEditing] = useState(false);
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState({
    full_name: user?.full_name || '',
    email: user?.email || '',
    phone: user?.phone || '',
    address: user?.address || 'Mumbai, Maharashtra, India',
    department: user?.department || 'Banking Operations',
    bio: user?.bio || 'Passionate about providing excellent banking services.',
  });
  const [errors, setErrors] = useState({});

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    setErrors(prev => ({ ...prev, [name]: '' }));
  };

  const validateForm = () => {
    const newErrors = {};
    
    if (!formData.full_name?.trim()) {
      newErrors.full_name = 'Full name is required';
    }
    if (!formData.email?.trim()) {
      newErrors.email = 'Email is required';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      newErrors.email = 'Invalid email format';
    }
    if (formData.phone && !/^\d{10}$/.test(formData.phone.replace(/[-\s]/g, ''))) {
      newErrors.phone = 'Invalid phone number';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validateForm()) return;

    setLoading(true);
    try {
      await new Promise(resolve => setTimeout(resolve, 1500));
      if (updateProfile) {
        updateProfile(formData);
      }
      toast.success('Profile updated successfully!');
      setIsEditing(false);
    } catch (error) {
      toast.error(error.message || 'Failed to update profile');
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    setFormData({
      full_name: user?.full_name || '',
      email: user?.email || '',
      phone: user?.phone || '',
      address: user?.address || 'Mumbai, Maharashtra, India',
      department: user?.department || 'Banking Operations',
      bio: user?.bio || 'Passionate about providing excellent banking services.',
    });
    setErrors({});
    setIsEditing(false);
  };

  const getRoleBadge = (role) => {
    const styles = {
      ADMIN: 'bg-purple-100 text-purple-800 border-purple-200',
      MANAGER: 'bg-blue-100 text-blue-800 border-blue-200',
      TELLER: 'bg-green-100 text-green-800 border-green-200',
    };
    return styles[role] || 'bg-gray-100 text-gray-800 border-gray-200';
  };

  return (
    <div className="animate-fade-in max-w-4xl mx-auto">
      {/* Header */}
      <div className="mb-6">
        <h1 className="page-title">My Profile</h1>
        <p className="text-gray-500">View and manage your profile information</p>
      </div>

      {/* Profile Card */}
      <div className="card overflow-hidden mb-6">
        {/* Cover Image */}
        <div className="h-32 bg-gradient-to-r from-primary-500 to-primary-700 relative">
          <div className="absolute bottom-0 left-6 transform translate-y-1/2">
            <div className="relative">
              <div className={`w-24 h-24 rounded-full border-4 border-white flex items-center justify-center text-3xl font-bold text-white ${
                user?.role === 'ADMIN' ? 'bg-purple-600' :
                user?.role === 'MANAGER' ? 'bg-blue-600' : 'bg-green-600'
              }`}>
                {user?.full_name?.charAt(0).toUpperCase() || 'U'}
              </div>
              {isEditing && (
                <button className="absolute bottom-0 right-0 p-2 bg-white rounded-full shadow-lg hover:bg-gray-50 transition-colors">
                  <Camera className="w-4 h-4 text-gray-600" />
                </button>
              )}
            </div>
          </div>
          <div className="absolute top-4 right-4">
            {!isEditing ? (
              <button
                onClick={() => setIsEditing(true)}
                className="flex items-center gap-2 px-4 py-2 bg-white/20 hover:bg-white/30 text-white rounded-lg backdrop-blur-sm transition-colors"
              >
                <Edit className="w-4 h-4" />
                Edit Profile
              </button>
            ) : (
              <div className="flex gap-2">
                <button
                  onClick={handleCancel}
                  className="flex items-center gap-2 px-4 py-2 bg-white text-gray-700 rounded-lg hover:bg-gray-50 transition-colors"
                >
                  <X className="w-4 h-4" />
                  Cancel
                </button>
                <button
                  onClick={handleSave}
                  disabled={loading}
                  className="flex items-center gap-2 px-4 py-2 bg-green-500 text-white rounded-lg hover:bg-green-600 transition-colors"
                >
                  {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                  Save
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Profile Info */}
        <div className="pt-16 px-6 pb-6">
          <div className="flex items-center gap-3 mb-6">
            <div>
              {isEditing ? (
                <input
                  type="text"
                  name="full_name"
                  value={formData.full_name}
                  onChange={handleInputChange}
                  className={`text-2xl font-bold text-gray-900 border-b-2 bg-transparent focus:outline-none ${
                    errors.full_name ? 'border-red-500' : 'border-primary-500'
                  }`}
                />
              ) : (
                <h2 className="text-2xl font-bold text-gray-900">{user?.full_name}</h2>
              )}
              <p className="text-gray-500">@{user?.username}</p>
            </div>
            <span className={`badge px-3 py-1 border ${getRoleBadge(user?.role)}`}>
              <Shield className="w-3 h-3 mr-1" />
              {user?.role}
            </span>
          </div>

          {/* Bio */}
          {isEditing ? (
            <textarea
              name="bio"
              value={formData.bio}
              onChange={handleInputChange}
              rows={2}
              className="w-full text-gray-600 border rounded-lg p-3 resize-none focus:outline-none focus:ring-2 focus:ring-primary-500"
              placeholder="Tell us about yourself..."
            />
          ) : (
            <p className="text-gray-600 mb-6">{formData.bio}</p>
          )}
        </div>
      </div>

      {/* Details Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Contact Information */}
        <div className="card p-6">
          <h3 className="section-title mb-4 flex items-center gap-2">
            <Mail className="w-5 h-5 text-primary-600" />
            Contact Information
          </h3>
          <div className="space-y-4">
            <div>
              <label className="text-sm text-gray-500">Email Address</label>
              {isEditing ? (
                <div>
                  <input
                    type="email"
                    name="email"
                    value={formData.email}
                    onChange={handleInputChange}
                    className={`input w-full mt-1 ${errors.email ? 'border-red-500' : ''}`}
                  />
                  {errors.email && (
                    <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                      <AlertCircle className="w-4 h-4" /> {errors.email}
                    </p>
                  )}
                </div>
              ) : (
                <p className="font-medium text-gray-900">{user?.email}</p>
              )}
            </div>
            <div>
              <label className="text-sm text-gray-500">Phone Number</label>
              {isEditing ? (
                <div>
                  <input
                    type="tel"
                    name="phone"
                    value={formData.phone}
                    onChange={handleInputChange}
                    className={`input w-full mt-1 ${errors.phone ? 'border-red-500' : ''}`}
                    placeholder="Enter phone number"
                  />
                  {errors.phone && (
                    <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                      <AlertCircle className="w-4 h-4" /> {errors.phone}
                    </p>
                  )}
                </div>
              ) : (
                <p className="font-medium text-gray-900">{user?.phone || 'Not provided'}</p>
              )}
            </div>
            <div>
              <label className="text-sm text-gray-500">Address</label>
              {isEditing ? (
                <input
                  type="text"
                  name="address"
                  value={formData.address}
                  onChange={handleInputChange}
                  className="input w-full mt-1"
                  placeholder="Enter address"
                />
              ) : (
                <p className="font-medium text-gray-900 flex items-center gap-1">
                  <MapPin className="w-4 h-4 text-gray-400" />
                  {formData.address}
                </p>
              )}
            </div>
          </div>
        </div>

        {/* Work Information */}
        <div className="card p-6">
          <h3 className="section-title mb-4 flex items-center gap-2">
            <Building className="w-5 h-5 text-primary-600" />
            Work Information
          </h3>
          <div className="space-y-4">
            <div>
              <label className="text-sm text-gray-500">Employee ID</label>
              <p className="font-medium text-gray-900">EMP-{String(user?.id || 1).padStart(5, '0')}</p>
            </div>
            <div>
              <label className="text-sm text-gray-500">Role</label>
              <p className="font-medium text-gray-900">{user?.role}</p>
            </div>
            <div>
              <label className="text-sm text-gray-500">Department</label>
              {isEditing ? (
                <select
                  name="department"
                  value={formData.department}
                  onChange={handleInputChange}
                  className="input w-full mt-1"
                >
                  <option value="Banking Operations">Banking Operations</option>
                  <option value="Customer Service">Customer Service</option>
                  <option value="IT Department">IT Department</option>
                  <option value="Management">Management</option>
                  <option value="Finance">Finance</option>
                </select>
              ) : (
                <p className="font-medium text-gray-900">{formData.department}</p>
              )}
            </div>
          </div>
        </div>

        {/* Account Activity */}
        <div className="card p-6">
          <h3 className="section-title mb-4 flex items-center gap-2">
            <Clock className="w-5 h-5 text-primary-600" />
            Account Activity
          </h3>
          <div className="space-y-4">
            <div>
              <label className="text-sm text-gray-500">Account Created</label>
              <p className="font-medium text-gray-900 flex items-center gap-1">
                <Calendar className="w-4 h-4 text-gray-400" />
                {format(new Date('2024-01-15'), 'MMMM d, yyyy')}
              </p>
            </div>
            <div>
              <label className="text-sm text-gray-500">Last Login</label>
              <p className="font-medium text-gray-900 flex items-center gap-1">
                <Clock className="w-4 h-4 text-gray-400" />
                {user?.last_login 
                  ? format(new Date(user.last_login), 'MMM d, yyyy h:mm a')
                  : format(new Date(), 'MMM d, yyyy h:mm a')
                }
              </p>
            </div>
            <div>
              <label className="text-sm text-gray-500">Account Status</label>
              <span className="badge badge-success">Active</span>
            </div>
          </div>
        </div>

        {/* Security */}
        <div className="card p-6">
          <h3 className="section-title mb-4 flex items-center gap-2">
            <Shield className="w-5 h-5 text-primary-600" />
            Security
          </h3>
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">Two-Factor Authentication</p>
                <p className="text-sm text-gray-500">Add extra security to your account</p>
              </div>
              <span className="badge bg-amber-100 text-amber-800">Not Enabled</span>
            </div>
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">Password</p>
                <p className="text-sm text-gray-500">Last changed 30 days ago</p>
              </div>
              <button className="text-primary-600 text-sm font-medium hover:underline">
                Change
              </button>
            </div>
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">Active Sessions</p>
                <p className="text-sm text-gray-500">1 device currently logged in</p>
              </div>
              <button className="text-primary-600 text-sm font-medium hover:underline">
                Manage
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProfilePage;
