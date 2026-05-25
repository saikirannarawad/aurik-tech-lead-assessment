import { NavLink, Route, Routes, Navigate } from "react-router-dom";
import { DashboardPage } from "./pages/DashboardPage";
import { MachineDetailPage } from "./pages/MachineDetailPage";
import { IngestionStatusPage } from "./pages/IngestionStatusPage";
import { VendorIngestionPage } from "./pages/VendorIngestionPage";

export function App() {
  return (
    <>
      <nav className="top">
        <strong>Aurik Equipment Monitoring</strong>
        <NavLink to="/dashboard" className={({ isActive }) => (isActive ? "active" : "")}>
          Dashboard
        </NavLink>
        <NavLink to="/vendor" className={({ isActive }) => (isActive ? "active" : "")}>
          Vendor Service
        </NavLink>
        <NavLink to="/ingestion" className={({ isActive }) => (isActive ? "active" : "")}>
          Ingestion Status
        </NavLink>
      </nav>
      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/machines/:machineId" element={<MachineDetailPage />} />
          <Route path="/vendor" element={<VendorIngestionPage />} />
          <Route path="/send" element={<Navigate to="/vendor" replace />} />
          <Route path="/ingestion" element={<IngestionStatusPage />} />
        </Routes>
      </main>
    </>
  );
}
