import { BrowserRouter as Router, Routes, Route } from "react-router";
import AppLayout from "./layout/AppLayout";
import { ScrollToTop } from "./components/common/ScrollToTop";
import Dashboard from "./pages/Dashboard";
import Containers from "./pages/Monitoring/Containers";
import Volumes from "./pages/Monitoring/Volumes";
import VolumeDetail from "./pages/Monitoring/VolumeDetail";
import Deployments from "./pages/Deployments/Deployments";
import DeploymentDetail from "./pages/Deployments/DeploymentDetail";
import UpgradeStack from "./pages/Deployments/UpgradeStack";
import RollbackStack from "./pages/Deployments/RollbackStack";
import RemoveStack from "./pages/Deployments/RemoveStack";
import HealthDashboard from "./pages/Monitoring/HealthDashboard";
import StackCatalog from "./pages/Catalog/StackCatalog";
import ProductDetail from "./pages/Catalog/ProductDetail";
import DeployStack from "./pages/Deployments/DeployStack";
import Environments from "./pages/Environments/Environments";
import {
  SettingsIndex,
  StackSourcesList,
  AddStackSourceSelect,
  AddLocalSource,
  AddGitSource,
  AddFromCatalog,
  DeleteStackSource,
  RegistriesList,
  AddRegistry,
  EditRegistry,
  DeleteRegistry,
  TlsOverview,
  TlsConfigSelect,
  ConfigureLetsEncrypt,
  UploadCertificate,
  ResetToSelfSigned,
  CiCdList,
  SystemInfo,
} from "./pages/Settings";
import SetupEnvironment from "./pages/Environments/SetupEnvironment";
import SetupOrganization from "./pages/Settings/Organization/SetupOrganization";
import Login from "./pages/Auth/Login";
import Wizard from "./pages/Wizard";
import Onboarding from "./pages/Onboarding";
import Profile from "./pages/Profile/Profile";
import NotFound from "./pages/NotFound";
import UpdateStatus from "./pages/UpdateStatus";
import { AuthProvider } from "./context/AuthContext";
import { ThemeProvider } from "./context/ThemeContext";
import { EnvironmentProvider } from "./context/EnvironmentContext";
import ProtectedRoute from "./components/auth/ProtectedRoute";
import WizardGuard from "./components/wizard/WizardGuard";
import OnboardingGuard from "./components/onboarding/OnboardingGuard";
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
              {/* Update status page - standalone, no layout */}
              <Route
                path="/update"
                element={
                  <ProtectedRoute>
                    <UpdateStatus />
                  </ProtectedRoute>
                }
              />
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
              {/* Onboarding page - standalone, outside AppLayout */}
              <Route
                path="/onboarding"
                element={
                  <ProtectedRoute>
                    <Onboarding />
                  </ProtectedRoute>
                }
              />
              <Route
                element={
                  <ProtectedRoute>
                    <EnvironmentProvider>
                      <OnboardingGuard>
                        <AppLayout />
                      </OnboardingGuard>
                    </EnvironmentProvider>
                  </ProtectedRoute>
                }
              >
                {/* Dashboard with EnvironmentGuard — redirects to /environments when none exist */}
                <Route
                  index
                  path="/"
                  element={
                    <EnvironmentGuard>
                      <Dashboard />
                    </EnvironmentGuard>
                  }
                />
                {/* Routes that require an active environment */}
                <Route
                  path="/containers"
                  element={
                    <EnvironmentGuard>
                      <Containers />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/volumes"
                  element={
                    <EnvironmentGuard>
                      <Volumes />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/volumes/:name"
                  element={
                    <EnvironmentGuard>
                      <VolumeDetail />
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
                {/* Profile page - doesn't require active environment */}
                <Route path="/profile" element={<Profile />} />
                {/* Environments page - doesn't require active environment */}
                <Route path="/environments" element={<Environments />} />
                {/* Settings pages - don't require active environment */}
                <Route path="/settings" element={<SettingsIndex />} />
                <Route path="/settings/organization" element={<SetupOrganization />} />
                <Route path="/settings/stack-sources" element={<StackSourcesList />} />
                <Route path="/settings/stack-sources/add" element={<AddStackSourceSelect />} />
                <Route path="/settings/stack-sources/add/local" element={<AddLocalSource />} />
                <Route path="/settings/stack-sources/add/git" element={<AddGitSource />} />
                <Route path="/settings/stack-sources/add/catalog" element={<AddFromCatalog />} />
                <Route path="/settings/stack-sources/:id/delete" element={<DeleteStackSource />} />
                <Route path="/settings/registries" element={<RegistriesList />} />
                <Route path="/settings/registries/add" element={<AddRegistry />} />
                <Route path="/settings/registries/:id/edit" element={<EditRegistry />} />
                <Route path="/settings/registries/:id/delete" element={<DeleteRegistry />} />
                <Route path="/settings/tls" element={<TlsOverview />} />
                <Route path="/settings/tls/configure" element={<TlsConfigSelect />} />
                <Route path="/settings/tls/letsencrypt" element={<ConfigureLetsEncrypt />} />
                <Route path="/settings/tls/upload" element={<UploadCertificate />} />
                <Route path="/settings/tls/selfsigned" element={<ResetToSelfSigned />} />
                <Route path="/settings/cicd" element={<CiCdList />} />
                <Route path="/settings/system" element={<SystemInfo />} />
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
