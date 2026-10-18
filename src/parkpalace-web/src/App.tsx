import React, { useEffect, useState } from 'react';
import { Sidebar } from './components/Sidebar';
import { Header } from './components/Header';
import { DashboardView } from './components/views/DashboardView';
import { MaterialsView } from './components/views/MaterialsView';
import { SuppliersView } from './components/views/SuppliersView';
import { OrdersView } from './components/views/OrdersView';
import { WarehouseView } from './components/views/WarehouseView';
import { FinanceView } from './components/views/FinanceView';
import { CallsView } from './components/views/CallsView';
import { CallPopup } from './components/CallPopup';
import { AdminView } from './components/views/AdminView';
import { fetchHealth, fetchMe } from './services/api';
import { MeResponse, ViewType } from './types';
import { ShieldAlert } from 'lucide-react';

export const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<ViewType>('dashboard');
  const [activeDevRole, setActiveDevRole] = useState<string>('Admin');
  const [sepidarStatus, setSepidarStatus] = useState<string>('پیکربندی نشده (فاز ۴)');
  const [user, setUser] = useState<MeResponse>({
    username: 'sahar\\administrator',
    displayName: 'مدیر ارشد سیستم',
    isAuthenticated: true,
    roles: ['Admin'],
    permissions: [],
  });

  const [selectedMaterialId, setSelectedMaterialId] = useState<string | null>(null);
  const [selectedSupplierId, setSelectedSupplierId] = useState<string | null>(null);

  const handleNavigateToMaterial = (materialId: string) => {
    setSelectedMaterialId(materialId);
    setCurrentView('materials');
  };

  const handleNavigateToSupplier = (supplierId: string) => {
    setSelectedSupplierId(supplierId);
    setCurrentView('suppliers');
  };

  const loadUserData = (role: string) => {
    fetchMe(role)
      .then((data) => {
        if (data.isAuthenticated) {
          setUser(data);
        }
      })
      .catch((err) => console.error('Fetch me failed', err));

    fetchHealth()
      .then((h) => {
        setSepidarStatus(h.sepidarIntegrationStatus);
      })
      .catch((err) => console.error('Fetch health failed', err));
  };

  useEffect(() => {
    loadUserData(activeDevRole);
  }, [activeDevRole]);

  const handleRoleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newRole = e.target.value;
    setActiveDevRole(newRole);
    if (newRole !== 'Admin' && currentView === 'admin') {
      setCurrentView('dashboard');
    }
  };

  return (
    <div className="app-container">
      <Sidebar
        currentView={currentView}
        onSelectView={setCurrentView}
        userRoles={user.roles}
      />

      <div className="main-wrapper">
        <Header user={user} sepidarStatus={sepidarStatus} />

        <main className="content-area">
          {currentView === 'dashboard' && (
            <DashboardView 
              devRole={activeDevRole}
              onNavigateView={setCurrentView}
            />
          )}
          {currentView === 'materials' && (
            <MaterialsView 
              activeDevRole={activeDevRole}
              selectedMaterialId={selectedMaterialId}
              onClearSelectedMaterial={() => setSelectedMaterialId(null)}
              onNavigateToSupplier={handleNavigateToSupplier}
            />
          )}
          {currentView === 'suppliers' && (
            <SuppliersView 
              activeDevRole={activeDevRole}
              selectedSupplierId={selectedSupplierId}
              onClearSelectedSupplier={() => setSelectedSupplierId(null)}
              onNavigateToMaterial={handleNavigateToMaterial}
            />
          )}
          {currentView === 'orders' && (
            <OrdersView 
              activeDevRole={activeDevRole}
              onNavigateToMaterial={handleNavigateToMaterial}
              onNavigateToSupplier={handleNavigateToSupplier}
            />
          )}
          {currentView === 'warehouse' && (
            <WarehouseView 
              activeDevRole={activeDevRole}
              onNavigateToMaterial={handleNavigateToMaterial}
              onNavigateToSupplier={handleNavigateToSupplier}
            />
          )}
          {currentView === 'finance' && <FinanceView />}
          {currentView === 'calls' && (
            <CallsView 
              devRole={activeDevRole}
              onNavigateToSupplier={handleNavigateToSupplier}
            />
          )}
          {currentView === 'admin' && (
            user.roles.includes('Admin') ? (
              <AdminView activeDevRole={activeDevRole} />
            ) : (
              <div className="view-card" style={{ textAlign: 'center', padding: '60px 20px' }}>
                <ShieldAlert size={48} color="#ef4444" style={{ margin: 'auto', marginBottom: '16px' }} />
                <h3>دسترسی غیرمجاز</h3>
                <p style={{ color: 'var(--text-muted)', marginTop: '8px' }}>
                  شما مجوز لازم برای مشاهده بخش مدیریت و دسترسی‌ها را ندارید.
                </p>
              </div>
            )
          )}

          {/* Development Auth Role Switcher Bar */}
          <div className="dev-controls">
            <label htmlFor="dev-role-select">حالت آزمایشی (Dev Identity Mock):</label>
            <select
              id="dev-role-select"
              className="dev-select"
              value={activeDevRole}
              onChange={handleRoleChange}
            >
              <option value="Admin">مدیر ارشد (Admin - دسترسی کامل)</option>
              <option value="Procurement">مسئول تدارکات (Procurement - صدور مستقیم سفارش)</option>
              <option value="Manager">مدیر ناظر (Manager - نظارت بر فرایند)</option>
              <option value="Warehouse">انباردار (Warehouse - رسید و کنترل موجودی)</option>
              <option value="Finance">امور مالی (Finance - تطبیق فاکتورهای سپیدار)</option>
            </select>
            <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>
              (در محیط عملیاتی IIS از Windows Negotiate Active Directory استفاده می‌شود)
            </span>
          </div>
        </main>
      </div>

      {/* Global Real-time Asterisk AMI Incoming Call Popup */}
      <CallPopup 
        devRole={activeDevRole}
        onNavigateToSupplier={handleNavigateToSupplier}
        userExtension="101"
      />
    </div>
  );
};
