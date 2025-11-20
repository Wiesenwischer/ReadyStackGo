import { BrowserRouter as Router, Routes, Route } from "react-router";
import AppLayout from "./layout/AppLayout";
import { ScrollToTop } from "./components/common/ScrollToTop";
import Dashboard from "./pages/Dashboard";
import Containers from "./pages/Containers";
import Stacks from "./pages/Stacks";
import Login from "./pages/Auth/Login";
import Wizard from "./pages/Wizard";
import { AuthProvider } from "./context/AuthContext";
import { ThemeProvider } from "./context/ThemeContext";
import { EnvironmentProvider } from "./context/EnvironmentContext";
import ProtectedRoute from "./components/auth/ProtectedRoute";
import WizardGuard from "./components/wizard/WizardGuard";

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
              <Route
                element={
                  <ProtectedRoute>
                    <EnvironmentProvider>
                      <AppLayout />
                    </EnvironmentProvider>
                  </ProtectedRoute>
                }
              >
                <Route index path="/" element={<Dashboard />} />
                <Route path="/containers" element={<Containers />} />
                <Route path="/stacks" element={<Stacks />} />
              </Route>
            </Routes>
          </WizardGuard>
        </Router>
      </AuthProvider>
    </ThemeProvider>
  );
}
