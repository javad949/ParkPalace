import React from 'react';
import { ViewType } from '../types';
import { 
  LayoutDashboard, 
  Package, 
  Users, 
  ShoppingCart, 
  Warehouse, 
  Receipt, 
  PhoneCall, 
  ShieldCheck 
} from 'lucide-react';

interface SidebarProps {
  currentView: ViewType;
  onSelectView: (view: ViewType) => void;
  userRoles: string[];
  userPermissions?: string[];
}

export const Sidebar: React.FC<SidebarProps> = ({ currentView, onSelectView, userRoles, userPermissions = [] }) => {
  const isAdmin = userRoles.includes('Admin');
  const hasAnyRole = (...roles: string[]) => userRoles.some(r => roles.includes(r));
  const hasPerm = (perm: string) => isAdmin || userPermissions.includes(perm);

  const navItems: Array<{ id: ViewType; label: string; icon: any; isVisible: boolean }> = [
    { 
      id: 'dashboard', 
      label: 'داشبورد مدیریت', 
      icon: LayoutDashboard,
      isVisible: true 
    },
    {
      id: 'executiveDashboard',
      label: 'داشبورد تصمیم',
      icon: LayoutDashboard,
      isVisible: hasPerm('Reports.View') || hasPerm('Reports.Manager') || hasPerm('Org.ReportsView') || hasPerm('Finance.PartyLedgerView')
        || hasAnyRole('Admin', 'Manager', 'Finance', 'Procurement', 'Warehouse', 'OrganizationManager')
    },
    { 
      id: 'materials', 
      label: 'کاتالوگ مواد اولیه', 
      icon: Package,
      isVisible: hasPerm('Materials.View') || hasAnyRole('Admin', 'Procurement', 'Manager', 'Warehouse')
    },
    { 
      id: 'suppliers', 
      label: 'تأمین‌کنندگان', 
      icon: Users,
      isVisible: hasPerm('Suppliers.View') || hasAnyRole('Admin', 'Procurement', 'Manager', 'Finance')
    },
    { 
      id: 'sepidarParties', 
      label: 'طرف‌حساب‌های سپیدار', 
      icon: Users,
      isVisible: hasPerm('Suppliers.View') || hasPerm('Finance.View') || hasAnyRole('Admin', 'Procurement', 'Manager', 'Finance')
    },
    { 
      id: 'contacts', 
      label: 'مخاطبان', 
      icon: Users,
      isVisible: hasPerm('Suppliers.View') || hasPerm('Calls.View') || hasAnyRole('Admin', 'Procurement', 'Manager', 'Finance')
    },
    {
      id: 'orders',
      label: 'سفارشات و تدارکات', 
      icon: ShoppingCart,
      isVisible: hasPerm('Orders.View') || hasAnyRole('Admin', 'Procurement', 'Manager')
    },
    { 
      id: 'warehouse', 
      label: 'انبار واحد و موجودی', 
      icon: Warehouse,
      isVisible: hasPerm('Warehouse.View') || hasPerm('Inventory.View') || hasAnyRole('Admin', 'Warehouse', 'Manager')
    },
    { 
      id: 'financeDashboard', 
      label: 'داشبورد مالی', 
      icon: Receipt,
      isVisible: hasPerm('Finance.View') || hasPerm('Reports.Manager') || hasPerm('Reports.Finance') || hasAnyRole('Admin', 'Finance', 'Manager')
    },
    { 
      id: 'finance', 
      label: 'مالی و تطبیق فاکتورها', 
      icon: Receipt,
      isVisible: hasPerm('Finance.View') || hasPerm('Invoices.View') || hasAnyRole('Admin', 'Finance', 'Manager')
    },
    { 
      id: 'calls', 
      label: 'مرکز تماس (ایزابل)', 
      icon: PhoneCall,
      isVisible: hasPerm('Calls.View')
    },
    { 
      id: 'admin', 
      label: 'کاربران و دسترسی‌ها', 
      icon: ShieldCheck,
      isVisible: isAdmin || hasPerm('Admin.Users')
    }
  ];

  const visibleNavItems = navItems.filter(item => item.isVisible);

  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <div className="logo-badge">P</div>
        <div className="logo-text">
          <h1>پارک پالاس</h1>
          <span>سامانه جامع خرید و تدارکات</span>
        </div>
      </div>

      <ul className="nav-list">
        {visibleNavItems.map((item) => {
          const Icon = item.icon;
          const isActive = currentView === item.id;
          return (
            <li
              key={item.id}
              className={`nav-item ${isActive ? 'active' : ''}`}
              onClick={() => onSelectView(item.id)}
            >
              <Icon size={19} />
              <span>{item.label}</span>
            </li>
          );
        })}
      </ul>
    </aside>
  );
};
