import React from 'react';
import { MeResponse } from '../types';
import { formatJalali } from '../utils/jalali';
import { Calendar, User } from 'lucide-react';

interface HeaderProps {
  user: MeResponse;
  sepidarStatus: string;
}

export const Header: React.FC<HeaderProps> = ({ user, sepidarStatus }) => {
  const todayJalali = formatJalali(new Date());

  return (
    <header className="top-header">
      <div className="header-meta">
        <div className="jalali-date">
          <Calendar size={15} />
          <span>امروز: {todayJalali}</span>
        </div>

        <div className="integration-badge" title="وضعیت یکپارچه‌سازی با پایگاه داده سپیدار">
          <span className="status-dot"></span>
          <span>سپیدار: {sepidarStatus}</span>
        </div>
      </div>

      <div className="user-profile">
        {user.roles.map((role) => (
          <span key={role} className="role-pill">
            {role === 'Admin' ? 'مدیر ارشد' :
             role === 'Procurement' ? 'تدارکات' :
             role === 'Warehouse' ? 'انباردار' :
             role === 'Finance' ? 'امور مالی' :
             role === 'Manager' ? 'مدیریت' : role}
          </span>
        ))}

        <div style={{ textAlign: 'left', marginRight: '6px' }}>
          <div style={{ fontSize: '13px', fontWeight: 600 }}>{user.displayName}</div>
          <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>{user.username}</div>
        </div>

        <div className="user-avatar">
          <User size={18} />
        </div>
      </div>
    </header>
  );
};
