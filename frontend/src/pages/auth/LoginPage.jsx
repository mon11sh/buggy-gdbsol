import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useAuthStore } from '../../store/authStore';
import { Building2, Eye, EyeOff, AlertCircle, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';

// Validation Schema
const loginSchema = z.object({
  loginId: z.string().min(1, 'Login ID is required'),
  password: z.string().min(1, 'Password is required'),
  rememberMe: z.boolean().optional(),
});

const LoginPage = () => {
  const [showPassword, setShowPassword] = useState(false);
  
  const { login, isLoading, error, clearError } = useAuthStore();
  const navigate = useNavigate();

  // React Hook Form
  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      loginId: '',
      password: '',
      rememberMe: false,
    },
  });

  const onSubmit = async (data) => {
    clearError();

    const result = await login(data.loginId, data.password);
    
    if (result.success) {
      toast.success('Login successful! Welcome to GDB.');
      navigate('/dashboard');
    } else {
      toast.error(result.error || 'Login failed');
    }
  };

  // Demo credentials
  const demoCredentials = [
    { role: 'ADMIN', loginId: 'admin', password: 'Welcome@1' },
    { role: 'TELLER', loginId: 'john.doe', password: 'Welcome@1' },
    { role: 'MANAGER', loginId: 'manager.manager', password: 'Welcome@1' },
  ];

  const fillDemoCredentials = (cred) => {
    setValue('loginId', cred.loginId);
    setValue('password', cred.password);
  };

  return (
    <div className="animate-fade-in">
      {/* Mobile Logo */}
      <div className="lg:hidden flex items-center justify-center gap-3 mb-8">
        <div className="w-12 h-12 gradient-bg rounded-xl flex items-center justify-center">
          <Building2 className="w-7 h-7 text-white" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900 font-display">GDB</h1>
          <p className="text-sm text-gray-500">Global Digital Bank</p>
        </div>
      </div>

      {/* Login Card */}
      <div className="card p-8">
        <div className="mb-8">
          <h2 className="text-2xl font-bold text-gray-900 font-display">Welcome Back</h2>
          <p className="text-gray-500 mt-1">Please sign in to continue</p>
        </div>

        {error && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-medium text-red-800">Authentication Failed</p>
              <p className="text-sm text-red-600">{error}</p>
            </div>
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          <div>
            <label htmlFor="loginId" className="input-label">
              Login ID
            </label>
            <input
              id="loginId"
              type="text"
              {...register('loginId')}
              className={`input-field ${errors.loginId ? 'border-red-500' : ''}`}
              placeholder="Enter your login ID"
              disabled={isLoading}
            />
            {errors.loginId && (
              <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                <AlertCircle className="w-4 h-4" />
                {errors.loginId.message}
              </p>
            )}
          </div>

          <div>
            <label htmlFor="password" className="input-label">
              Password
            </label>
            <div className="relative">
              <input
                id="password"
                type={showPassword ? 'text' : 'password'}
                {...register('password')}
                className={`input-field pr-12 ${errors.password ? 'border-red-500' : ''}`}
                placeholder="Enter your password"
                disabled={isLoading}
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              >
                {showPassword ? (
                  <EyeOff className="w-5 h-5" />
                ) : (
                  <Eye className="w-5 h-5" />
                )}
              </button>
            </div>
            {errors.password && (
              <p className="text-red-500 text-sm mt-1 flex items-center gap-1">
                <AlertCircle className="w-4 h-4" />
                {errors.password.message}
              </p>
            )}
          </div>

          <div className="flex items-center justify-between">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                {...register('rememberMe')}
                className="w-4 h-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              />
              <span className="text-sm text-gray-600">Remember me</span>
            </label>
            <button type="button" className="text-sm text-primary-600 hover:text-primary-700 font-medium">
              Forgot password?
            </button>
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full btn-primary flex items-center justify-center gap-2 py-3"
          >
            {isLoading ? (
              <>
                <Loader2 className="w-5 h-5 animate-spin" />
                Signing in...
              </>
            ) : (
              'Sign In'
            )}
          </button>
        </form>

        {/* Demo Credentials */}
        <div className="mt-8 pt-6 border-t border-gray-200">
          <p className="text-sm text-gray-500 mb-3 text-center">Demo Credentials</p>
          <div className="space-y-2">
            {demoCredentials.map((cred) => (
              <button
                key={cred.role}
                type="button"
                onClick={() => fillDemoCredentials(cred)}
                className="w-full p-3 text-left bg-gray-50 hover:bg-gray-100 rounded-lg transition-colors border border-gray-200"
              >
                <div className="flex items-center justify-between">
                  <div>
                    <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${
                      cred.role === 'ADMIN' ? 'bg-red-100 text-red-700' :
                      cred.role === 'TELLER' ? 'bg-blue-100 text-blue-700' :
                      'bg-purple-100 text-purple-700'
                    }`}>
                      {cred.role}
                    </span>
                  </div>
                  <div className="text-right">
                    <p className="text-sm font-medium text-gray-700">{cred.loginId}</p>
                    <p className="text-xs text-gray-500">{cred.password}</p>
                  </div>
                </div>
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Footer */}
      <p className="text-center text-sm text-gray-500 mt-6">
        © 2024 Global Digital Bank. All rights reserved.
      </p>
    </div>
  );
};

export default LoginPage;
