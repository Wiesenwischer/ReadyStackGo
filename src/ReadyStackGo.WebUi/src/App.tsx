import { BrowserRouter as Router, Routes, Route } from "react-router";
import AppLayout from "./layout/AppLayout";
import { ScrollToTop } from "./components/common/ScrollToTop";
import Dashboard from "./pages/Dashboard";
import Containers from "./pages/Containers";
import Stacks from "./pages/Stacks";

export default function App() {
  return (
    <Router>
      <ScrollToTop />
      <Routes>
        <Route element={<AppLayout />}>
          <Route index path="/" element={<Dashboard />} />
          <Route path="/containers" element={<Containers />} />
          <Route path="/stacks" element={<Stacks />} />
        </Route>
      </Routes>
    </Router>
  );
}
