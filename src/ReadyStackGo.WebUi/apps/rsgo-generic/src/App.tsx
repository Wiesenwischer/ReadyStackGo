import { BrowserRouter as Router, Routes, Route } from "react-router";
import AppLayout from "@rsgo/ui-generic/layout/AppLayout";
import { ScrollToTop } from "@rsgo/ui-generic/components/common/ScrollToTop";
import Dashboard from "@rsgo/ui-generic/pages/Dashboard";
import Containers from "@rsgo/ui-generic/pages/Monitoring/Containers";
import Volumes from "@rsgo/ui-generic/pages/Monitoring/Volumes";
import VolumeDetail from "@rsgo/ui-generic/pages/Monitoring/VolumeDetail";
import Deployments from "@rsgo/ui-generic/pages/Deployments/Deployments";
import DeploymentDetail from "@rsgo/ui-generic/pages/Deployments/DeploymentDetail";
import UpgradeStack from "@rsgo/ui-generic/pages/Deployments/UpgradeStack";
import RollbackStack from "@rsgo/ui-generic/pages/Deployments/RollbackStack";
import RemoveStack from "@rsgo/ui-generic/pages/Deployments/RemoveStack";
import HealthDashboard from "@rsgo/ui-generic/pages/Monitoring/HealthDashboard";
import ServiceHealthDetail from "@rsgo/ui-generic/pages/Monitoring/ServiceHealthDetail";
import ContainerLogs from "@rsgo/ui-generic/pages/Monitoring/ContainerLogs";
import StackCatalog from "@rsgo/ui-generic/pages/Catalog/StackCatalog";
import ProductDetail from "@rsgo/ui-generic/pages/Catalog/ProductDetail";
import DeployStack from "@rsgo/ui-generic/pages/Deployments/DeployStack";
import DeployProduct from "@rsgo/ui-generic/pages/Deployments/DeployProduct";
import UpgradeProduct from "@rsgo/ui-generic/pages/Deployments/UpgradeProduct";
import RemoveProduct from "@rsgo/ui-generic/pages/Deployments/RemoveProduct";
import RetryProduct from "@rsgo/ui-generic/pages/Deployments/RetryProduct";
import RedeployProduct from "@rsgo/ui-generic/pages/Deployments/RedeployProduct";
import StopProduct from "@rsgo/ui-generic/pages/Deployments/StopProduct";
import RestartProduct from "@rsgo/ui-generic/pages/Deployments/RestartProduct";
import EnterMaintenanceProduct from "@rsgo/ui-generic/pages/Deployments/EnterMaintenanceProduct";
import ExitMaintenanceProduct from "@rsgo/ui-generic/pages/Deployments/ExitMaintenanceProduct";
import ProductDeploymentDetail from "@rsgo/ui-generic/pages/Deployments/ProductDeploymentDetail";
import Environments from "@rsgo/ui-generic/pages/Environments/Environments";
import AddEnvironment from "@rsgo/ui-generic/pages/Environments/AddEnvironment";
import DeleteEnvironment from "@rsgo/ui-generic/pages/Environments/DeleteEnvironment";
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
  Licenses,
} from "@rsgo/ui-generic/pages/Settings";
import SetupEnvironment from "@rsgo/ui-generic/pages/Environments/SetupEnvironment";
import SetupOrganization from "@rsgo/ui-generic/pages/Settings/Organization/SetupOrganization";
import Login from "@rsgo/ui-generic/pages/Auth/Login";
import Wizard from "@rsgo/ui-generic/pages/Wizard";
import Onboarding from "@rsgo/ui-generic/pages/Onboarding";
import Profile from "@rsgo/ui-generic/pages/Profile/Profile";
import NotFound from "@rsgo/ui-generic/pages/NotFound";
import UpdateStatus from "@rsgo/ui-generic/pages/UpdateStatus";
import { AuthProvider } from "@rsgo/ui-generic/context/AuthContext";
import { ThemeProvider } from "@rsgo/ui-generic/context/ThemeContext";
import { EnvironmentProvider } from "@rsgo/ui-generic/context/EnvironmentContext";
import ProtectedRoute from "@rsgo/ui-generic/components/auth/ProtectedRoute";
import WizardGuard from "@rsgo/ui-generic/components/wizard/WizardGuard";
import OnboardingGuard from "@rsgo/ui-generic/components/onboarding/OnboardingGuard";
import EnvironmentGuard from "@rsgo/ui-generic/components/environment/EnvironmentGuard";

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
                  path="/containers/:id/logs"
                  element={
                    <EnvironmentGuard>
                      <ContainerLogs />
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
                  path="/health/:deploymentId/:serviceName"
                  element={
                    <EnvironmentGuard>
                      <ServiceHealthDetail />
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
                <Route
                  path="/deploy-product/:productId"
                  element={
                    <EnvironmentGuard>
                      <DeployProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/upgrade-product/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <UpgradeProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/remove-product/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <RemoveProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/retry-product/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <RetryProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/redeploy-product/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <RedeployProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/stop-product/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <StopProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/restart-product/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <RestartProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/enter-maintenance/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <EnterMaintenanceProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/exit-maintenance/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <ExitMaintenanceProduct />
                    </EnvironmentGuard>
                  }
                />
                <Route
                  path="/product-deployments/:productDeploymentId"
                  element={
                    <EnvironmentGuard>
                      <ProductDeploymentDetail />
                    </EnvironmentGuard>
                  }
                />
                {/* Profile page - doesn't require active environment */}
                <Route path="/profile" element={<Profile />} />
                {/* Environments page - doesn't require active environment */}
                <Route path="/environments" element={<Environments />} />
                <Route path="/environments/add" element={<AddEnvironment />} />
                <Route path="/environments/:id/delete" element={<DeleteEnvironment />} />
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
                <Route path="/settings/licenses" element={<Licenses />} />
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
