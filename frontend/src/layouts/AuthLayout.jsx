import { Outlet } from 'react-router-dom';
import { Building2 } from 'lucide-react';

const AuthLayout = () => {
  return (
    <div className="min-h-screen flex">
      {/* Left Side - Branding */}
      <div className="hidden lg:flex lg:w-1/2 gradient-bg relative overflow-hidden">
        <div className="absolute inset-0 bg-black/20"></div>
        
        {/* Decorative Elements */}
        <div className="absolute top-20 left-20 w-72 h-72 bg-white/10 rounded-full blur-3xl"></div>
        <div className="absolute bottom-20 right-20 w-96 h-96 bg-blue-400/20 rounded-full blur-3xl"></div>
        
        <div className="relative z-10 flex flex-col justify-center items-center w-full px-12 text-white">
          <div className="flex items-center gap-4 mb-8">
            <div className="w-16 h-16 bg-white/20 rounded-2xl flex items-center justify-center backdrop-blur-sm">
              <Building2 className="w-10 h-10 text-white" />
            </div>
            <div>
              <h1 className="text-4xl font-bold font-display">GDB</h1>
              <p className="text-blue-200 text-lg">Global Digital Bank</p>
            </div>
          </div>
          
          <div className="max-w-md text-center">
            <h2 className="text-3xl font-semibold mb-4 font-display">
              Welcome to Enterprise Banking
            </h2>
            <p className="text-blue-100 text-lg leading-relaxed">
              Secure, scalable, and modern banking platform built for 
              enterprise-grade performance and reliability.
            </p>
          </div>

          <div className="mt-12 grid grid-cols-3 gap-8 text-center">
            <div>
              <div className="text-4xl font-bold">99.9%</div>
              <div className="text-blue-200 text-sm mt-1">Uptime</div>
            </div>
            <div>
              <div className="text-4xl font-bold">256-bit</div>
              <div className="text-blue-200 text-sm mt-1">Encryption</div>
            </div>
            <div>
              <div className="text-4xl font-bold">24/7</div>
              <div className="text-blue-200 text-sm mt-1">Support</div>
            </div>
          </div>
        </div>
      </div>

      {/* Right Side - Form */}
      <div className="flex-1 flex items-center justify-center p-8 bg-gray-50">
        <div className="w-full max-w-md">
          <Outlet />
        </div>
      </div>
    </div>
  );
};

export default AuthLayout;
