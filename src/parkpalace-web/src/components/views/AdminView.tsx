import React, { useEffect, useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { RoleDto } from '../../types';
import { fetchRoles } from '../../services/api';
import { toPersianDigits } from '../../utils/jalali';

interface AdminViewProps {
  activeDevRole: string;
}

export const AdminView: React.FC<AdminViewProps> = ({ activeDevRole }) => {
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    fetchRoles(activeDevRole)
      .then((data) => {
        setRoles(data);
        setError(null);
      })
      .catch((err) => {
        setError(err.message || 'خطا در دریافت لیست نقش‌ها');
      })
      .finally(() => setLoading(false));
  }, [activeDevRole]);

  return (
    <div className="view-card">
      <div className="view-header">
        <h2>مدیریت دسترسی‌ها و ماتریس نقش‌ها (RBAC)</h2>
        <p>پیکربندی نقش‌های سازمانی، مجوزهای سیستمی و تطبیق کاربران اکتیودایرکتوری (AD)</p>
      </div>

      {error ? (
        <div style={{ padding: '16px', background: 'rgba(239, 68, 68, 0.1)', color: '#f87171', borderRadius: '8px' }}>
          {error}
        </div>
      ) : loading ? (
        <div style={{ color: 'var(--text-muted)' }}>در حال بارگذاری نقش‌ها و مجوزها از سرور...</div>
      ) : (
        <div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', gap: '16px' }}>
            {roles.map((r) => (
              <div key={r.id} style={{
                background: 'var(--bg-sidebar)',
                border: '1px solid var(--border-color)',
                borderRadius: '10px',
                padding: '18px'
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '8px' }}>
                  <ShieldCheck size={18} color="#38bdf8" />
                  <strong style={{ fontSize: '15px' }}>{r.name}</strong>
                  <span style={{ fontSize: '11px', background: 'var(--primary-light)', color: '#38bdf8', padding: '2px 8px', borderRadius: '10px' }}>
                    {toPersianDigits(r.permissions.length)} مجوز
                  </span>
                </div>
                <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '14px', minHeight: '36px' }}>
                  {r.description}
                </p>

                <div style={{ borderTop: '1px solid var(--border-color)', paddingTop: '10px' }}>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)', display: 'block', marginBottom: '6px' }}>مجوزهای اختصاص‌یافته:</span>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                    {r.permissions.slice(0, 5).map((p) => (
                      <span key={p} style={{
                        fontSize: '11px',
                        background: 'rgba(255,255,255,0.04)',
                        padding: '2px 6px',
                        borderRadius: '4px',
                        color: 'var(--text-muted)'
                      }}>
                        {p}
                      </span>
                    ))}
                    {r.permissions.length > 5 && (
                      <span style={{ fontSize: '11px', color: '#38bdf8' }}>
                        +{toPersianDigits(r.permissions.length - 5)} مورد دیگر
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
