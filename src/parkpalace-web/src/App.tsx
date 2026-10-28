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
  const [currentView, setCurrentView] = useState<ViewType>(() => {
    if (typeof window !== 'undefined') {
      const urlParams = new URLSearchParams(window.location.search);
      const paramView = urlParams.get('view') as ViewType;
      if (paramView && ['dashboard', 'materials', 'suppliers', 'orders', 'warehouse', 'finance', 'calls', 'admin'].includes(paramView)) {
        return paramView;
      }
    }
    return 'dashboard';
  });
  const [activeRole, setActiveRole] = useState<string>(() => {
    if (typeof window !== 'undefined') {
      const urlParams = new URLSearchParams(window.location.search);
      const paramRole = urlParams.get('role');
      if (paramRole) {
        localStorage.setItem('parkpalace_active_role', paramRole);
        return paramRole;
      }
      return localStorage.getItem('parkpalace_active_role') || 'Admin';
    }
    return 'Admin';
  });
  const [sepidarStatus, setSepidarStatus] = useState<string>('در حال بررسی اتصال...');
  const [user, setUser] = useState<MeResponse>({
    username: 'sahar\\administrator',
    displayName: 'مدیر ارشد سیستم',
    isAuthenticated: true,
    roles: ['Admin'],
    permissions: [],
    activeRole: 'Admin',
    availableRoles: ['Admin', 'Procurement', 'Manager', 'Warehouse', 'Finance']
  });

  const [selectedMaterialId, setSelectedMaterialId] = useState<string | null>(() => {
    if (typeof window !== 'undefined') {
      const urlParams = new URLSearchParams(window.location.search);
      return urlParams.get('materialId') || null;
    }
    return null;
  });
  const [selectedSupplierId, setSelectedSupplierId] = useState<string | null>(null);

  const handleNavigateToMaterial = (materialId: string) => {
    setSelectedMaterialId(materialId);
    setCurrentView('materials');
  };

  const handleNavigateToSupplier = (supplierId: string) => {
    setSelectedSupplierId(supplierId);
    setCurrentView('suppliers');
  };

  const isViewAllowed = (view: ViewType, roles: string[], perms: string[] = []): boolean => {
    if (roles.includes('Admin')) return true;
    const hasAnyRole = (...rList: string[]) => roles.some(r => rList.includes(r));
    const hasPerm = (perm: string) => perms.includes(perm);

    switch (view) {
      case 'dashboard':
        return true;
      case 'materials':
        return hasPerm('Materials.View') || hasAnyRole('Procurement', 'Manager', 'Warehouse');
      case 'suppliers':
        return hasPerm('Suppliers.View') || hasAnyRole('Procurement', 'Manager', 'Finance');
      case 'orders':
        return hasPerm('Orders.View') || hasAnyRole('Procurement', 'Manager');
      case 'warehouse':
        return hasPerm('Warehouse.View') || hasPerm('Inventory.View') || hasAnyRole('Warehouse', 'Manager');
      case 'finance':
        return hasPerm('Finance.View') || hasPerm('Invoices.View') || hasAnyRole('Finance', 'Manager');
      case 'calls':
        return hasPerm('Calls.View') || hasAnyRole('Procurement', 'Manager');
      case 'admin':
        return roles.includes('Admin') || hasPerm('Admin.Users');
      default:
        return false;
    }
  };

  const loadUserData = (role?: string) => {
    fetchMe(role)
      .then((data) => {
        if (data.isAuthenticated) {
          setUser(data);
          const currentEffective = data.activeRole || data.roles[0] || 'Admin';
          setActiveRole(currentEffective);
          localStorage.setItem('parkpalace_active_role', currentEffective);
          if (!isViewAllowed(currentView, data.roles, data.permissions)) {
            setCurrentView('dashboard');
          }
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
    loadUserData(activeRole);
  }, [activeRole]);

  const handleRoleChange = (newRole: string) => {
    localStorage.setItem('parkpalace_active_role', newRole);
    setActiveRole(newRole);
    loadUserData(newRole);
  };

  return (
    <div className="app-container">
      <Sidebar
        currentView={currentView}
        onSelectView={setCurrentView}
        userRoles={user.roles}
        userPermissions={user.permissions}
      />

      <div className="main-wrapper">
        <Header 
          user={user} 
          sepidarStatus={sepidarStatus} 
          onRoleChange={handleRoleChange} 
        />

        <main className="content-area">
          {!isViewAllowed(currentView, user.roles, user.permissions) ? (
            <div className="view-card" style={{ textAlign: 'center', padding: '60px 20px' }}>
              <ShieldAlert size={48} color="#ef4444" style={{ margin: 'auto', marginBottom: '16px' }} />
              <h3>دسترسی غیرمجاز (HTTP 403 Forbidden)</h3>
              <p style={{ color: 'var(--text-muted)', marginTop: '8px' }}>
                نقش جاری شما ({user.roles.join(', ')}) مجوز لازم برای مشاهده این بخش را ندارد.
              </p>
              <button
                className="btn-primary"
                onClick={() => setCurrentView('dashboard')}
                style={{ marginTop: '20px', display: 'inline-flex', alignItems: 'center', gap: '8px', padding: '8px 20px' }}
              >
                بازگشت به داشبورد
              </button>
            </div>
          ) : (
            <>
              {currentView === 'dashboard' && (
                <DashboardView 
                  devRole={activeRole}
                  onNavigateView={setCurrentView}
                />
              )}
              {currentView === 'materials' && (
                <MaterialsView 
                  activeDevRole={activeRole}
                  selectedMaterialId={selectedMaterialId}
                  onClearSelectedMaterial={() => setSelectedMaterialId(null)}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'suppliers' && (
                <SuppliersView 
                  activeDevRole={activeRole}
                  selectedSupplierId={selectedSupplierId}
                  onClearSelectedSupplier={() => setSelectedSupplierId(null)}
                  onNavigateToMaterial={handleNavigateToMaterial}
                />
              )}
              {currentView === 'orders' && (
                <OrdersView 
                  activeDevRole={activeRole}
                  onNavigateToMaterial={handleNavigateToMaterial}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'warehouse' && (
                <WarehouseView 
                  activeDevRole={activeRole}
                  onNavigateToMaterial={handleNavigateToMaterial}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'finance' && (
                <FinanceView 
                  activeDevRole={activeRole}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'calls' && (
                <CallsView 
                  devRole={activeRole}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'admin' && (
                <AdminView activeDevRole={activeRole} />
              )}
            </>
          )}

          {user.availableRoles && user.availableRoles.length > 1 && (
            <div style={{ textAlign: 'center', padding: '16px 0', fontSize: '11px', color: 'var(--text-dim)' }}>
              نقش فعال فعلی: <strong style={{ color: '#38bdf8' }}>{user.roles.join(', ')}</strong> — جابجایی بین نقش‌های مجاز از منوی بالای صفحه در دسترس است.
            </div>
          )}
        </main>
      </div>

      {/* Global Real-time Asterisk AMI Incoming Call Popup */}
      <CallPopup 
        devRole={activeRole}
        onNavigateToSupplier={handleNavigateToSupplier}
        userExtension="101"
      />
    </div>
  );
};
