import { BrowserRouter as Router, Routes, Route } from "react-router";
import AppLayout from "./layout/AppLayout";
import { ScrollToTop } from "./components/common/ScrollToTop";
import Dashboard from "./pages/Dashboard";
import Containers from "./pages/Containers";
import Deployments from "./pages/Deployments";
import DeploymentDetail from "./pages/DeploymentDetail";
import UpgradeStack from "./pages/UpgradeStack";
import RollbackStack from "./pages/RollbackStack";
import RemoveStack from "./pages/RemoveStack";
import HealthDashboard from "./pages/HealthDashboard";
import StackCatalog from "./pages/StackCatalog";
import ProductDetail from "./pages/ProductDetail";
import DeployStack from "./pages/DeployStack";
import Environments from "./pages/Environments";
import SetupEnvironment from "./pages/SetupEnvironment";
import Login from "./pages/Auth/Login";
import Wizard from "./pages/Wizard";
import NotFound from "./pages/NotFound";
import { AuthProvider } from "./context/AuthContext";
import { ThemeProvider } from "./context/ThemeContext";
import { EnvironmentProvider } from "./context/EnvironmentContext";
import ProtectedRoute from "./components/auth/ProtectedRoute";
import WizardGuard from "./components/wizard/WizardGuard";
import EnvironmentGuard from "./components/environment/EnvironmentGuard";

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <Router>
          <ScrollToTop />
          <WizardGuard>
            <Routes>
              <Route path="/wizard" element={<Wizard />} />
              <Route path="/login" element={<Login />} />
              {/* Setup environment page - standalone, no layout */}
              <Route
                path="/setup-environment"
                element={
                  <ProtectedRoute>
                    <EnvironmentProvider>
                      <SetupEnvironment />
                    </EnvironmentProvider>
                  </ProtectedRoute>
                }
              />
              <Route
                element={
                  <ProtectedRoute>
                    <EnvironmentProvider>
                      <AppLayout />
                    </EnvironmentProvider>
                  </ProtectedRoute>
                }
              >
                {/* Routes that require an active environment */}
                <Route
                  index
                  path="/"
                  element={
                    <EnvironmentGuard>
                      <Dashboard />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/containers"
                  element={
                    <EnvironmentGuard>
                      <Containers />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/deployments"
                  element={
                    <EnvironmentGuard>
                      <Deployments />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/deployments/:stackName"
                  element={
                    <EnvironmentGuard>
                      <DeploymentDetail />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/deployments/:stackName/upgrade"
                  element={
                    <EnvironmentGuard>
                      <UpgradeStack />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/deployments/:stackName/rollback"
                  element={
                    <EnvironmentGuard>
                      <RollbackStack />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/deployments/:stackName/remove"
                  element={
                    <EnvironmentGuard>
                      <RemoveStack />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/health"
                  element={
                    <EnvironmentGuard>
                      <HealthDashboard />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/catalog"
                  element={
                    <EnvironmentGuard>
                      <StackCatalog />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/catalog/:productId"
                  element={
                    <EnvironmentGuard>
                      <ProductDetail />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/deploy/:stackId"
                  element={
                    <EnvironmentGuard>
                      <DeployStack />
                    </EnvironmentGuard>
                  }
                />
                {/* Environments page - doesn't require active environment */}
                <Route path="/environments" element={<Environments />} />
              </Route>
              {/* 404 catch-all route */}
              <Route path="*" element={<NotFound />} />
            </Routes>
          </WizardGuard>
        </Router>
      </AuthProvider>
    </ThemeProvider>
  );
}
