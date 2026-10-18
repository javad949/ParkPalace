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
}

export const Sidebar: React.FC<SidebarProps> = ({ currentView, onSelectView, userRoles }) => {
  const isAdmin = userRoles.includes('Admin');

  const navItems = [
    { id: 'dashboard' as ViewType, label: 'داشبورد مدیریت', icon: LayoutDashboard },
    { id: 'materials' as ViewType, label: 'کاتالوگ مواد اولیه', icon: Package },
    { id: 'suppliers' as ViewType, label: 'تأمین‌کنندگان و مخاطبین', icon: Users },
    { id: 'orders' as ViewType, label: 'سفارشات و تدارکات', icon: ShoppingCart },
    { id: 'warehouse' as ViewType, label: 'انبار واحد و موجودی', icon: Warehouse },
    { id: 'finance' as ViewType, label: 'مالی و تطبیق فاکتورها', icon: Receipt },
    { id: 'calls' as ViewType, label: 'مرکز تماس (ایزابل)', icon: PhoneCall },
  ];

  if (isAdmin) {
    navItems.push({ id: 'admin' as ViewType, label: 'مدیریت و دسترسی‌ها', icon: ShieldCheck });
  }

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
        {navItems.map((item) => {
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
