import { Navigate, useLocation } from "react-router";
import { useEnvironment } from "../../context/EnvironmentContext";

interface EnvironmentGuardProps {
  children: React.ReactNode;
}

/**
 * Guard component that redirects to environment setup if no environments exist.
 * Used to wrap routes that require an active environment.
 */
export default function EnvironmentGuard({ children }: EnvironmentGuardProps) {
  const { environments, isLoading } = useEnvironment();
  const location = useLocation();

  // Show loading while checking environments
  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="text-center">
          <svg className="mx-auto h-8 w-8 animate-spin text-brand-600" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
          </svg>
          <p className="mt-2 text-sm text-gray-500">Loading environments...</p>
        </div>
      </div>
    );
  }

  // If no environments exist, redirect to setup (unless already on environments page)
  if (environments.length === 0 && location.pathname !== "/environments") {
    return <Navigate to="/setup-environment" replace />;
  }

  return <>{children}</>;
}
