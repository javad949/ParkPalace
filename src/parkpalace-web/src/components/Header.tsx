import React from 'react';
import { MeResponse, UserDto } from '../types';
import { formatJalali } from '../utils/jalali';
import { Calendar, User } from 'lucide-react';

export const ROLE_LABELS_FA: Record<string, string> = {
  Admin: 'مدیر ارشد سیستم',
  Procurement: 'مسئول تدارکات',
  Warehouse: 'سرپرست انبار',
  Finance: 'امور مالی و حسابداری',
  Manager: 'مدیر ناظر بازرگانی',
};

export const sanitizePersianDisplayName = (displayName?: string, username?: string): string => {
  if (!displayName) return username || 'کاربر سیستم';
  if (/[ØÙÚÂÛ]/.test(displayName) || displayName.includes('????')) {
    const u = (username || '').toLowerCase();
    if (u.includes('admin')) return 'مدیر ارشد سیستم (Admin)';
    if (u.includes('procurement')) return 'کارشناس تدارکات';
    if (u.includes('warehouse')) return 'سرپرست انبار';
    if (u.includes('finance')) return 'کارشناس حسابداری';
    if (u.includes('manager')) return 'مدیر ناظر بازرگانی';
    return username || 'کاربر سیستم';
  }
  return displayName;
};

interface HeaderProps {
  user: MeResponse;
  sepidarStatus: string;
  onRoleChange?: (newRole: string) => void;
  previewUsers?: UserDto[];
  previewUserId?: string;
  onPreviewUserChange?: (userId: string) => void;
  previewActiveRole?: string;
}

export const Header: React.FC<HeaderProps> = ({
  user,
  sepidarStatus,
  onRoleChange,
  previewUsers = [],
  previewUserId = '',
  onPreviewUserChange,
  previewActiveRole,
}) => {
  const todayJalali = formatJalali(new Date());

  const isLive = sepidarStatus.includes('Active') || sepidarStatus.includes('Live');
  const isSimulated = sepidarStatus.includes('Not configured') || sepidarStatus.includes('Fixture') || sepidarStatus.includes('Disabled');

  const getStatusText = () => {
    if (isLive) return 'متصل زنده (Read-Only)';
    if (isSimulated) return 'حالت آزمایشی (شبیه‌ساز فاز ۴ و ۵)';
    return sepidarStatus;
  };

  const getBadgeStyle = (): React.CSSProperties => {
    if (isLive) {
      return {
        background: 'rgba(16, 185, 129, 0.12)',
        color: '#10b981',
        border: '1px solid rgba(16, 185, 129, 0.3)'
      };
    }
    if (isSimulated) {
      return {
        background: 'rgba(56, 189, 248, 0.12)',
        color: '#38bdf8',
        border: '1px solid rgba(56, 189, 248, 0.25)'
      };
    }
    return {
      background: 'rgba(148, 163, 184, 0.12)',
      color: '#94a3b8',
      border: '1px solid rgba(148, 163, 184, 0.25)'
    };
  };

  const currentActiveRole = user.activeRole || user.roles[0] || 'Admin';
  const cleanDisplayName = sanitizePersianDisplayName(user.displayName, user.username);
  const canSwitchRoles = user.availableRoles && user.availableRoles.length > 1 && !!onRoleChange;

  return (
    <header className="top-header">
      <div className="header-meta">
        <div className="jalali-date">
          <Calendar size={15} />
          <span>امروز: {todayJalali}</span>
        </div>

        <div 
          className="integration-badge" 
          style={getBadgeStyle()}
          title={isLive ? 'اتصال مستقیم و زنده به پایگاه داده سپیدار برقرار است (خواندن صرف)' : 'ارتباط در حالت شبیه‌ساز امن (فازهای ۴ و ۵ فعال با داده‌های قطعی نانوایی)'}
        >
          <span 
            className="status-dot" 
            style={{ 
              background: isLive ? '#10b981' : isSimulated ? '#38bdf8' : '#94a3b8',
              boxShadow: isLive ? '0 0 8px rgba(16, 185, 129, 0.6)' : isSimulated ? '0 0 8px rgba(56, 189, 248, 0.5)' : 'none'
            }}
          ></span>
          <span>سپیدار: {getStatusText()}</span>
        </div>
      </div>

      <div className="user-profile">
        {previewUsers.length > 0 && onPreviewUserChange && (
          <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '11px', color: 'var(--text-muted)' }}>
            پیش‌نمایش
            {previewActiveRole ? ` (${ROLE_LABELS_FA[previewActiveRole] || previewActiveRole})` : ''}
            <select
              id="header-preview-user-select"
              className="role-selector-header"
              value={previewUserId}
              onChange={(e) => onPreviewUserChange(e.target.value)}
            >
              <option value="">خودم</option>
              {previewUsers.map((person) => (
                <option key={person.id} value={person.id}>
                  {person.displayName || person.username}
                </option>
              ))}
            </select>
          </label>
        )}
        {canSwitchRoles ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>نقش فعال:</span>
            <select
              id="header-active-role-select"
              className="role-selector-header"
              value={currentActiveRole}
              onChange={(e) => onRoleChange!(e.target.value)}
              title="تغییر نقش فعال از میان نقش‌های مجاز شما"
            >
              {user.availableRoles!.map((role) => (
                <option key={role} value={role}>
                  {ROLE_LABELS_FA[role] || role}
                </option>
              ))}
            </select>
          </div>
        ) : (
          <span className="role-pill" id="header-role-pill">
            {ROLE_LABELS_FA[currentActiveRole] || currentActiveRole}
          </span>
        )}

        <div style={{ textAlign: 'left', marginRight: '6px' }}>
          <div style={{ fontSize: '13px', fontWeight: 600 }} id="header-user-displayname">
            {cleanDisplayName}
          </div>
          <div style={{ fontSize: '11px', color: 'var(--text-dim)' }} id="header-user-username">
            {user.username}
          </div>
        </div>

        <div className="user-avatar">
          <User size={18} />
        </div>
      </div>
    </header>
  );
};
