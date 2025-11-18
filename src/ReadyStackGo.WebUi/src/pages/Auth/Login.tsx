import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      await login(username, password);
      navigate('/');
    } catch (err) {
      setError('Invalid username or password');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="relative p-6 bg-white dark:bg-gray-900 sm:p-0">
      <div className="relative flex flex-col justify-center w-full h-screen lg:flex-row dark:bg-gray-900">
        {/* Left side - Login Form */}
        <div className="flex flex-col flex-1">
          <div className="flex flex-col justify-center flex-1 w-full max-w-md mx-auto">
            <div>
              <div className="mb-5 sm:mb-8">
                <h1 className="mb-2 font-semibold text-gray-800 text-title-sm dark:text-white/90 sm:text-title-md">
                  Sign In to ReadyStackGo
                </h1>
                <p className="text-sm text-gray-500 dark:text-gray-400">
                  Enter your username and password to manage your Docker stacks
                </p>
              </div>

              <form onSubmit={handleSubmit}>
                <div className="space-y-6">
                  {error && (
                    <div className="p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
                      {error}
                    </div>
                  )}

                  <div>
                    <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
                      Username <span className="text-error-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={username}
                      onChange={(e) => setUsername(e.target.value)}
                      placeholder="admin"
                      required
                      className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
                    />
                  </div>

                  <div>
                    <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
                      Password <span className="text-error-500">*</span>
                    </label>
                    <div className="relative">
                      <input
                        type={showPassword ? 'text' : 'password'}
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        placeholder="Enter your password"
                        required
                        className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
                      />
                      <button
                        type="button"
                        onClick={() => setShowPassword(!showPassword)}
                        className="absolute z-30 -translate-y-1/2 cursor-pointer right-4 top-1/2"
                      >
                        {showPassword ? (
                          <svg
                            className="w-5 h-5 fill-gray-500 dark:fill-gray-400"
                            viewBox="0 0 20 20"
                            fill="currentColor"
                          >
                            <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                            <path fillRule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clipRule="evenodd" />
                          </svg>
                        ) : (
                          <svg
                            className="w-5 h-5 fill-gray-500 dark:fill-gray-400"
                            viewBox="0 0 20 20"
                            fill="currentColor"
                          >
                            <path fillRule="evenodd" d="M3.707 2.293a1 1 0 00-1.414 1.414l14 14a1 1 0 001.414-1.414l-1.473-1.473A10.014 10.014 0 0019.542 10C18.268 5.943 14.478 3 10 3a9.958 9.958 0 00-4.512 1.074l-1.78-1.781zm4.261 4.26l1.514 1.515a2.003 2.003 0 012.45 2.45l1.514 1.514a4 4 0 00-5.478-5.478z" clipRule="evenodd" />
                            <path d="M12.454 16.697L9.75 13.992a4 4 0 01-3.742-3.741L2.335 6.578A9.98 9.98 0 00.458 10c1.274 4.057 5.065 7 9.542 7 .847 0 1.669-.105 2.454-.303z" />
                          </svg>
                        )}
                      </button>
                    </div>
                  </div>

                  <div>
                    <button
                      type="submit"
                      disabled={isLoading}
                      className="inline-flex items-center justify-center w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed px-7"
                    >
                      {isLoading ? 'Signing in...' : 'Sign in'}
                    </button>
                  </div>
                </div>
              </form>

              <div className="mt-5">
                <p className="text-sm font-normal text-center text-gray-500 dark:text-gray-400 sm:text-start">
                  Default credentials: <span className="font-medium text-gray-700 dark:text-gray-300">admin / admin</span>
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Right side - Branding with Grid Background */}
        <div className="items-center hidden w-full h-full lg:w-1/2 bg-brand-950 dark:bg-white/5 lg:grid">
          <div className="relative flex items-center justify-center z-1">
            {/* Grid Background */}
            <div className="absolute inset-0 overflow-hidden">
              <svg className="absolute inset-0 w-full h-full" xmlns="http://www.w3.org/2000/svg">
                <defs>
                  <pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse">
                    <path d="M 40 0 L 0 0 0 40" fill="none" stroke="rgba(255,255,255,0.05)" strokeWidth="1"/>
                  </pattern>
                </defs>
                <rect width="100%" height="100%" fill="url(#grid)" />
              </svg>
            </div>

            {/* Logo and Description */}
            <div className="relative flex flex-col items-center max-w-xs">
              <div className="mb-6">
                <h2 className="text-4xl font-bold text-white">ReadyStackGo</h2>
              </div>
              <p className="text-center text-gray-400 dark:text-white/60">
                Deploy and manage Docker stacks with ease. A modern, lightweight platform for container orchestration.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
