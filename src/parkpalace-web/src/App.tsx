import React, { useEffect, useState } from 'react';
import { Sidebar } from './components/Sidebar';
import { Header, ROLE_LABELS_FA } from './components/Header';
import { DashboardView } from './components/views/DashboardView';
import { ExecutiveDashboardView } from './components/views/ExecutiveDashboardView';
import { MaterialsView } from './components/views/MaterialsView';
import { SuppliersView } from './components/views/SuppliersView';
import { OrdersView } from './components/views/OrdersView';
import { WarehouseView } from './components/views/WarehouseView';
import { FinanceView } from './components/views/FinanceView';
import { FinanceDashboardView } from './components/views/FinanceDashboardView';
import { DirectoryView } from './components/views/DirectoryView';
import { CallsView } from './components/views/CallsView';
import { CallPopup } from './components/CallPopup';
import { AdminView } from './components/views/AdminView';
import { fetchHealth, fetchMe, fetchUsers, fetchUserAccessPreview } from './services/api';
import { MeResponse, UserAccessPreviewDto, UserDto, ViewType } from './types';
import { roleTitleFa } from './utils/roleLabels';
import { ShieldAlert } from 'lucide-react';

export const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<ViewType>(() => {
    if (typeof window !== 'undefined') {
      const urlParams = new URLSearchParams(window.location.search);
      const paramView = urlParams.get('view');
      if (paramView === 'directorySuppliers') return 'suppliers';
      if (paramView && ['dashboard', 'executiveDashboard', 'materials', 'suppliers', 'sepidarParties', 'contacts', 'orders', 'warehouse', 'finance', 'financeDashboard', 'calls', 'admin'].includes(paramView)) {
        return paramView as ViewType;
      }
    }
    return 'dashboard';
  });
  const [activeRole, setActiveRole] = useState<string>(() => {
    if (typeof window !== 'undefined') {
      const urlParams = new URLSearchParams(window.location.search);
      const paramRole = urlParams.get('role');
      // Admin panel must stay on Admin — never restore Manager as active role
      if (paramRole && paramRole !== 'Manager') {
        localStorage.setItem('parkpalace_active_role', paramRole);
        return paramRole;
      }
      const stored = localStorage.getItem('parkpalace_active_role');
      if (stored && stored !== 'Manager' && stored !== 'None') return stored;
      localStorage.setItem('parkpalace_active_role', 'Admin');
      return 'Admin';
    }
    return 'Admin';
  });
  const [sepidarStatus, setSepidarStatus] = useState<string>('در حال بررسی اتصال...');
  const [previewUsers, setPreviewUsers] = useState<UserDto[]>([]);
  const [previewUserId, setPreviewUserId] = useState('');
  const [previewAccess, setPreviewAccess] = useState<UserAccessPreviewDto | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [authLoading, setAuthLoading] = useState(true);
  const [user, setUser] = useState<MeResponse>({
    username: '',
    displayName: '',
    isAuthenticated: false,
    roles: [],
    permissions: [],
    activeRole: 'None',
    availableRoles: []
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

  const isViewAllowed = (
    view: ViewType,
    roles: string[],
    perms: string[] = [],
    allowAdminBypass = true
  ): boolean => {
    if (allowAdminBypass && roles.includes('Admin')) return true;
    const hasAnyRole = (...rList: string[]) => roles.some(r => rList.includes(r));
    const hasPerm = (perm: string) => perms.includes(perm);

    switch (view) {
      case 'dashboard':
        return true;
      case 'executiveDashboard':
        return hasPerm('Reports.View') || hasPerm('Reports.Manager') || hasPerm('Org.ReportsView') || hasPerm('Finance.PartyLedgerView')
          || hasAnyRole('Manager', 'Finance', 'Procurement', 'Warehouse', 'OrganizationManager');
      case 'materials':
        return hasPerm('Materials.View') || hasAnyRole('Procurement', 'Manager', 'Warehouse');
      case 'suppliers':
        return hasPerm('Suppliers.View') || hasAnyRole('Procurement', 'Manager', 'Finance');
      case 'sepidarParties':
        return hasPerm('Suppliers.View') || hasPerm('Finance.View') || hasAnyRole('Procurement', 'Manager', 'Finance');
      case 'contacts':
        return hasPerm('Suppliers.View') || hasPerm('Calls.View') || hasPerm('Contacts.View') || hasAnyRole('Procurement', 'Manager', 'Finance');
      case 'orders':
        return hasPerm('Orders.View') || hasAnyRole('Procurement', 'Manager');
      case 'warehouse':
        return hasPerm('Warehouse.View') || hasPerm('Inventory.View') || hasAnyRole('Warehouse', 'Manager');
      case 'finance':
        return hasPerm('Finance.View') || hasPerm('Invoices.View') || hasAnyRole('Finance', 'Manager');
      case 'financeDashboard':
        return hasPerm('Finance.View') || hasPerm('Reports.Manager') || hasPerm('Reports.Finance') || hasAnyRole('Finance', 'Manager');
      case 'calls':
        return hasPerm('Calls.View');
      case 'admin':
        return roles.includes('Admin') || hasPerm('Admin.Users');
      default:
        return false;
    }
  };

  const isRealAdmin = (() => {
    const uname = (user.username || '').toLowerCase();
    return (
      user.roles.some(r => r.toLowerCase() === 'admin') ||
      (user.availableRoles || []).some(r => r.toLowerCase() === 'admin') ||
      user.activeRole === 'Admin' ||
      uname.includes('administrator') ||
      uname.endsWith('\\j.asg') ||
      uname === 'j.asg'
    );
  })();

  // While previewing another user, UI follows their access; API auth stays Admin.
  const effectiveRoles = previewAccess?.roles ?? user.roles;
  const effectivePermissions = previewAccess?.permissions ?? user.permissions;
  const effectiveActiveRole = previewAccess?.activeRole || (isRealAdmin ? 'Admin' : activeRole);
  const uiDevRole = previewAccess ? (previewAccess.activeRole || 'None') : (isRealAdmin ? 'Admin' : activeRole);
  const allowAdminBypass = !previewUserId;

  const loadUserData = (role?: string) => {
    // Never request Manager as active role for the header; Admin stays Admin.
    const requested = !role || role === 'Manager' ? 'Admin' : role;
    fetchMe(requested)
      .then((data) => {
        if (data.isAuthenticated) {
          const hasAdmin =
            data.roles.some(r => r.toLowerCase() === 'admin') ||
            (data.availableRoles || []).some(r => r.toLowerCase() === 'admin') ||
            data.activeRole === 'Admin' ||
            (data.username || '').toLowerCase().includes('administrator') ||
            (data.username || '').toLowerCase().endsWith('\\j.asg');

          if (hasAdmin && data.activeRole !== 'Admin') {
            // Re-fetch strictly as Admin so permissions/menus match مدیر سامانه
            return fetchMe('Admin').then((adminData) => {
              setUser({
                ...adminData,
                activeRole: 'Admin',
                roles: adminData.roles.includes('Admin')
                  ? adminData.roles
                  : ['Admin', ...adminData.roles.filter(r => r !== 'Manager')],
                availableRoles: ['Admin']
              });
              setAuthLoading(false);
              setActiveRole('Admin');
              localStorage.setItem('parkpalace_active_role', 'Admin');
              if (!isViewAllowed(currentView, previewAccess?.roles ?? ['Admin'], previewAccess?.permissions ?? adminData.permissions, !previewUserId)) {
                setCurrentView('dashboard');
              }
            });
          }

          if (hasAdmin) {
            setUser({
              ...data,
              activeRole: 'Admin',
              roles: data.roles.includes('Admin') ? data.roles.filter(r => r !== 'Manager' || data.roles.includes('Admin')) : ['Admin'],
              // Hide Manager from own role switcher — people picker replaces it
              availableRoles: ['Admin']
            });
            setActiveRole('Admin');
            localStorage.setItem('parkpalace_active_role', 'Admin');
          } else {
            setUser(data);
            const currentEffective = data.activeRole || data.roles[0] || 'None';
            setActiveRole(currentEffective);
            localStorage.setItem('parkpalace_active_role', currentEffective);
          }
          setAuthLoading(false);

          const rolesForCheck = previewAccess?.roles ?? (hasAdmin ? ['Admin'] : data.roles);
          const permsForCheck = previewAccess?.permissions ?? data.permissions;
          if (!isViewAllowed(currentView, rolesForCheck, permsForCheck, !previewUserId)) {
            setCurrentView('dashboard');
          }
        }
      })
      .catch((err) => {
        console.error('Fetch me failed', err);
        setUser({ username: '', displayName: '', isAuthenticated: false, roles: [], permissions: [], activeRole: 'None', availableRoles: [] });
        setAuthLoading(false);
      });

    fetchHealth()
      .then((h) => {
        setSepidarStatus(h.sepidarIntegrationStatus);
      })
      .catch((err) => console.error('Fetch health failed', err));
  };

  useEffect(() => {
    loadUserData(activeRole);
  }, [activeRole]);

  // Load people list for Admin panel-preview dropdown (all enabled users)
  useEffect(() => {
    const uname = (user.username || '').toLowerCase();
    const hasAdmin =
      user.roles.some(r => r.toLowerCase() === 'admin') ||
      (user.availableRoles || []).some(r => r.toLowerCase() === 'admin') ||
      user.activeRole === 'Admin' ||
      uname.includes('administrator') ||
      uname.endsWith('\\j.asg') ||
      uname === 'j.asg';
    if (!user.isAuthenticated || !hasAdmin) {
      setPreviewUserId('');
      setPreviewUsers([]);
      setPreviewAccess(null);
      return;
    }
    fetchUsers('Admin')
      .then(data => setPreviewUsers(data.filter(u => u.isEnabled)))
      .catch(() => setPreviewUsers([]));
  }, [user.isAuthenticated, user.activeRole, user.roles, user.availableRoles, user.username]);

  // When preview person changes, load their effective access
  useEffect(() => {
    if (!previewUserId) {
      setPreviewAccess(null);
      setPreviewError(null);
      return;
    }
    let cancelled = false;
    setPreviewError(null);
    fetchUserAccessPreview(previewUserId, 'Admin')
      .then((data) => {
        if (cancelled) return;
        setPreviewAccess(data);
        setPreviewError(null);
        if (!isViewAllowed(currentView, data.roles, data.permissions, false)) {
          setCurrentView('dashboard');
        }
      })
      .catch((err) => {
        console.error('Access preview failed', err);
        if (!cancelled) {
          setPreviewAccess(null);
          // Keep the selected person visible; show error instead of silently snapping back
          setPreviewError(err instanceof Error ? err.message : 'پیش‌نمایش دسترسی این کاربر در دسترس نیست.');
        }
      });
    return () => { cancelled = true; };
  }, [previewUserId]);

  const handleRoleChange = (newRole: string) => {
    // Admins stay on Admin; Manager and others go into the people-preview list instead
    if (isRealAdmin && newRole !== 'Admin') {
      localStorage.setItem('parkpalace_active_role', 'Admin');
      setActiveRole('Admin');
      loadUserData('Admin');
      return;
    }
    localStorage.setItem('parkpalace_active_role', newRole);
    setActiveRole(newRole);
    loadUserData(newRole);
  };

  const handlePreviewUserChange = (userId: string) => {
    setPreviewError(null);
    setPreviewUserId(userId);
    if (!userId) {
      setPreviewAccess(null);
    }
  };

  const handleSelectView = (view: ViewType, drillQuery?: string) => {
    if (drillQuery) {
      try {
        sessionStorage.setItem('parkpalace_drill', JSON.stringify({ view, query: drillQuery, at: new Date().toISOString() }));
      } catch { /* ignore */ }
    }
    const url = new URL(window.location.href);
    url.searchParams.set('view', view);
    if (drillQuery) url.searchParams.set('drill', drillQuery);
    else url.searchParams.delete('drill');
    window.history.replaceState({}, '', url.toString());
    if (!isViewAllowed(view, effectiveRoles, effectivePermissions, allowAdminBypass)) {
      return;
    }
    setCurrentView(view);
  };

  return (
    <div className="app-container">
      {user.isAuthenticated && <Sidebar
        currentView={currentView}
        onSelectView={handleSelectView}
        userRoles={effectiveRoles}
        userPermissions={effectivePermissions}
      />}

      <div className="main-wrapper">
        {user.isAuthenticated && <Header
          user={isRealAdmin
            ? { ...user, activeRole: 'Admin', roles: ['Admin'], availableRoles: ['Admin'] }
            : user}
          sepidarStatus={sepidarStatus}
          onRoleChange={handleRoleChange}
          previewUsers={previewUsers}
          previewUserId={previewUserId}
          onPreviewUserChange={handlePreviewUserChange}
          previewActiveRole={previewAccess?.activeRole}
        />}

        <main className="content-area">
          {previewError && previewUserId && (
            <div style={{
              background: 'rgba(239, 68, 68, 0.1)',
              border: '1px solid rgba(239, 68, 68, 0.35)',
              borderRadius: '10px',
              padding: '10px 16px',
              marginBottom: '16px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '12px',
              color: '#f87171',
              flexWrap: 'wrap'
            }}>
              <div style={{ fontSize: '0.88rem' }}>{previewError}</div>
              <button
                type="button"
                className="btn-secondary"
                style={{ fontSize: '0.78rem', padding: '4px 10px' }}
                onClick={() => handlePreviewUserChange('')}
              >
                بازگشت به پنل خودم
              </button>
            </div>
          )}

          {previewAccess && (
            <div style={{
              background: 'rgba(245, 158, 11, 0.1)',
              border: '1px solid rgba(245, 158, 11, 0.35)',
              borderRadius: '10px',
              padding: '10px 16px',
              marginBottom: '16px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '12px',
              color: '#fbbf24',
              flexWrap: 'wrap'
            }}>
              <div style={{ fontSize: '0.88rem' }}>
                در حال بررسی دسترسی به‌عنوان{' '}
                <strong>{previewAccess.displayName || previewAccess.username}</strong>
                {' '}— نقش:{' '}
                <strong>{ROLE_LABELS_FA[previewAccess.activeRole] || roleTitleFa(previewAccess.activeRole)}</strong>
                {previewAccess.extension ? ` · داخلی ${previewAccess.extension}` : ''}
              </div>
              <button
                type="button"
                className="btn-secondary"
                style={{ fontSize: '0.78rem', padding: '4px 10px' }}
                onClick={() => handlePreviewUserChange('')}
              >
                بازگشت به مدیر سامانه
              </button>
            </div>
          )}

          {user.isAuthenticated && !previewAccess && user.roles.length === 0 && (
            <div style={{
              background: 'rgba(234, 179, 8, 0.1)',
              border: '1px solid rgba(234, 179, 8, 0.3)',
              borderRadius: '10px',
              padding: '14px 18px',
              marginBottom: '20px',
              display: 'flex',
              alignItems: 'center',
              gap: '12px',
              color: '#facc15'
            }}>
              <ShieldAlert size={24} color="#facc15" style={{ flexShrink: 0 }} />
              <div>
                <div style={{ fontWeight: 700, fontSize: '0.95rem' }}>حساب کاربری شما فعال است، اما هنوز هیچ نقشی به شما تخصیص نیافته است.</div>
                <div style={{ fontSize: '0.82rem', color: 'var(--text-muted)', marginTop: '2px' }}>
                  تماس‌های داخلی خود را از مرکز تماس ببینید. برای دسترسی به بخش‌های دیگر، راهبر سیستم باید نقش مناسب را تخصیص دهد.
                </div>
              </div>
            </div>
          )}

          {authLoading ? (
            <div className="view-card" style={{ textAlign: 'center', padding: '60px 20px' }}>در حال بررسی اجازهٔ ورود...</div>
          ) : !user.isAuthenticated ? (
            <div className="view-card" style={{ textAlign: 'center', padding: '60px 20px' }}>
              <h3>ورود به CRM مجاز نیست</h3>
              <p>برای فعال‌سازی حساب با مدیر سیستم تماس بگیرید.</p>
            </div>
          ) : !isViewAllowed(currentView, effectiveRoles, effectivePermissions, allowAdminBypass) ? (
            <div className="view-card" style={{ textAlign: 'center', padding: '60px 20px' }}>
              <ShieldAlert size={48} color="#ef4444" style={{ margin: 'auto', marginBottom: '16px' }} />
              <h3>دسترسی غیرمجاز (HTTP 403 Forbidden)</h3>
              <p style={{ color: 'var(--text-muted)', marginTop: '8px' }}>
                {previewAccess
                  ? `کاربر انتخاب‌شده (${previewAccess.displayName}) مجوز لازم برای این بخش را ندارد.`
                  : `نقش جاری شما (${effectiveRoles.join(', ')}) مجوز لازم برای مشاهده این بخش را ندارد.`}
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
                  devRole={uiDevRole}
                  onNavigateView={handleSelectView}
                />
              )}
              {currentView === 'executiveDashboard' && (
                <ExecutiveDashboardView
                  devRole={uiDevRole}
                  onNavigateView={handleSelectView}
                />
              )}
              {currentView === 'materials' && (
                <MaterialsView
                  activeDevRole={uiDevRole}
                  selectedMaterialId={selectedMaterialId}
                  onClearSelectedMaterial={() => setSelectedMaterialId(null)}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'suppliers' && (
                <SuppliersView
                  activeDevRole={uiDevRole}
                  selectedSupplierId={selectedSupplierId}
                  onClearSelectedSupplier={() => setSelectedSupplierId(null)}
                  onNavigateToMaterial={handleNavigateToMaterial}
                />
              )}
              {currentView === 'sepidarParties' && <DirectoryView mode="sepidarParties" devRole={uiDevRole} />}
              {currentView === 'contacts' && <DirectoryView mode="contacts" devRole={uiDevRole} />}
              {currentView === 'orders' && (
                <OrdersView
                  activeDevRole={uiDevRole}
                  onNavigateToMaterial={handleNavigateToMaterial}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'warehouse' && (
                <WarehouseView
                  activeDevRole={uiDevRole}
                  onNavigateToMaterial={handleNavigateToMaterial}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'finance' && (
                <FinanceView
                  activeDevRole={uiDevRole}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'financeDashboard' && (
                <FinanceDashboardView devRole={uiDevRole} />
              )}
              {currentView === 'calls' && (
                <CallsView
                  devRole={uiDevRole}
                  previewUserId={previewUserId}
                  viewerExtension={previewAccess?.extension || user.extension}
                  onNavigateToSupplier={handleNavigateToSupplier}
                />
              )}
              {currentView === 'admin' && !previewUserId && (
                <AdminView activeDevRole="Admin" />
              )}
            </>
          )}

          {!previewAccess && !isRealAdmin && user.availableRoles && user.availableRoles.length > 1 && (
            <div style={{ textAlign: 'center', padding: '16px 0', fontSize: '11px', color: 'var(--text-dim)' }}>
              نقش فعال فعلی:{' '}
              <strong style={{ color: '#38bdf8' }}>
                {ROLE_LABELS_FA[effectiveActiveRole] || roleTitleFa(effectiveActiveRole)}
              </strong>
              {' '}— جابجایی بین نقش‌های مجاز از منوی بالای صفحه در دسترس است.
            </div>
          )}
        </main>
      </div>

      {/* Global Real-time Asterisk AMI Incoming Call Popup — always for real admin extension */}
      {user.isAuthenticated && !previewUserId && (
        <CallPopup
          devRole={isRealAdmin ? 'Admin' : activeRole}
          onNavigateToSupplier={handleNavigateToSupplier}
          userExtension={user.extension || ''}
        />
      )}
    </div>
  );
};
