import React, { useState, useEffect } from 'react';
import {
  TrendingUp,
  ShoppingCart,
  AlertTriangle,
  CheckCircle2,
  Clock,
  PhoneCall,
  FileSpreadsheet,
  Download,
  RefreshCw,
  Layers,
  ShieldCheck,
  Activity,
  Package,
  ExternalLink,
  AlertCircle,
  ChevronLeft
} from 'lucide-react';
import {
  fetchRoleDashboard,
  exportReport,
  fetchIntegrationHealth
} from '../../services/api';
import {
  RoleDashboardDto,
  IntegrationHealthReportDto,
  ReportFilterParams,
  ViewType
} from '../../types';
import { toPersianDigits } from '../../utils/jalali';

interface DashboardViewProps {
  devRole?: string;
  onNavigateView?: (view: ViewType) => void;
}

export const DashboardView: React.FC<DashboardViewProps> = ({ devRole = 'Admin', onNavigateView }) => {
  const [activeRole, setActiveRole] = useState<'Procurement' | 'Manager' | 'Warehouse' | 'Finance' | 'Admin'>('Manager');
  const [dashboardData, setDashboardData] = useState<RoleDashboardDto | null>(null);
  const [integrationHealth, setIntegrationHealth] = useState<IntegrationHealthReportDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [exporting, setExporting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [dateRangeFilter, setDateRangeFilter] = useState<'all' | '7d' | '30d'>('30d');
  const [hoveredTrendIndex, setHoveredTrendIndex] = useState<number | null>(null);

  const getFilterParams = (): ReportFilterParams => {
    const params: ReportFilterParams = {};
    const now = new Date();
    if (dateRangeFilter === '7d') {
      const past = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
      params.startDateUtc = past.toISOString();
    } else if (dateRangeFilter === '30d') {
      const past = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
      params.startDateUtc = past.toISOString();
    }
    return params;
  };

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const filters = getFilterParams();
      const [dash, health] = await Promise.all([
        fetchRoleDashboard(activeRole, filters, devRole),
        fetchIntegrationHealth(devRole).catch(() => null)
      ]);
      setDashboardData(dash);
      setIntegrationHealth(health);
    } catch (err: any) {
      console.error('Failed to load dashboard data', err);
      setError(err.message || 'خطا در بارگذاری داده‌های تحلیلی داشبورد');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [activeRole, dateRangeFilter, devRole]);

  const handleExport = async (format: 'csv' | 'xlsx') => {
    setExporting(true);
    try {
      const reportType = activeRole === 'Warehouse' ? 'InventoryHealth'
        : activeRole === 'Finance' ? 'FinanceExceptions'
        : activeRole === 'Procurement' ? 'PurchaseOrders'
        : 'PurchaseOrders';

      const filters = getFilterParams();
      await exportReport({
        reportType,
        format,
        startDateUtc: filters.startDateUtc,
        endDateUtc: filters.endDateUtc
      }, devRole);
    } catch (err: any) {
      alert(`خطا در خروجی: ${err.message}`);
    } finally {
      setExporting(false);
    }
  };

  const metrics = dashboardData?.summaryMetrics || {};

  return (
    <div className="view-card" style={{ padding: '24px', background: 'var(--bg-main)' }}>
      {/* Top Header & Controls */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px', marginBottom: '24px' }}>
        <div>
          <h2 style={{ fontSize: '22px', fontWeight: '800', color: '#fff', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Activity size={24} color="#0284c7" />
            داشبورد جامع هوش تجاری و تحلیل عملیات پارک پالاس
          </h2>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginTop: '6px' }}>
            پایش آنلاین زنجیره تأمین، سفارشات تدارکات، ورود کالا، تطبیق سه‌طرفه مالی و گزارشات ایزابل
          </p>
        </div>

        {/* Global Action Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
          {/* Jalali Date Filter */}
          <div style={{ display: 'flex', background: 'var(--bg-card)', borderRadius: '8px', padding: '3px', border: '1px solid var(--border-color)' }}>
            <button
              onClick={() => setDateRangeFilter('7d')}
              style={{
                padding: '6px 12px',
                borderRadius: '6px',
                fontSize: '12px',
                fontWeight: '600',
                border: 'none',
                cursor: 'pointer',
                background: dateRangeFilter === '7d' ? 'var(--primary)' : 'transparent',
                color: dateRangeFilter === '7d' ? '#fff' : 'var(--text-muted)'
              }}
            >
              ۷ روز اخیر
            </button>
            <button
              onClick={() => setDateRangeFilter('30d')}
              style={{
                padding: '6px 12px',
                borderRadius: '6px',
                fontSize: '12px',
                fontWeight: '600',
                border: 'none',
                cursor: 'pointer',
                background: dateRangeFilter === '30d' ? 'var(--primary)' : 'transparent',
                color: dateRangeFilter === '30d' ? '#fff' : 'var(--text-muted)'
              }}
            >
              ۳۰ روز اخیر
            </button>
            <button
              onClick={() => setDateRangeFilter('all')}
              style={{
                padding: '6px 12px',
                borderRadius: '6px',
                fontSize: '12px',
                fontWeight: '600',
                border: 'none',
                cursor: 'pointer',
                background: dateRangeFilter === 'all' ? 'var(--primary)' : 'transparent',
                color: dateRangeFilter === 'all' ? '#fff' : 'var(--text-muted)'
              }}
            >
              کل دوره
            </button>
          </div>

          {/* Export Dropdown / Buttons */}
          <button
            onClick={() => handleExport('xlsx')}
            disabled={exporting}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              padding: '7px 14px',
              background: '#065f46',
              color: '#34d399',
              border: '1px solid #059669',
              borderRadius: '8px',
              cursor: exporting ? 'not-allowed' : 'pointer',
              fontSize: '12px',
              fontWeight: '600'
            }}
          >
            <FileSpreadsheet size={15} />
            خروجی اکسل
          </button>

          <button
            onClick={() => handleExport('csv')}
            disabled={exporting}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              padding: '7px 14px',
              background: 'var(--bg-card)',
              color: 'var(--text-main)',
              border: '1px solid var(--border-color)',
              borderRadius: '8px',
              cursor: exporting ? 'not-allowed' : 'pointer',
              fontSize: '12px',
              fontWeight: '600'
            }}
          >
            <Download size={15} />
            خروجی CSV
          </button>

          <button
            onClick={loadData}
            style={{
              display: 'flex',
              alignItems: 'center',
              padding: '8px',
              background: 'var(--bg-card)',
              color: 'var(--text-muted)',
              border: '1px solid var(--border-color)',
              borderRadius: '8px',
              cursor: 'pointer'
            }}
            title="به‌روزرسانی داده‌ها"
          >
            <RefreshCw size={15} className={loading ? 'spin' : ''} />
          </button>
        </div>
      </div>

      {/* Role Navigation Bar */}
      <div style={{ display: 'flex', gap: '8px', borderBottom: '1px solid var(--border-color)', paddingBottom: '12px', marginBottom: '24px', overflowX: 'auto' }}>
        {[
          { id: 'Manager', label: 'داشبورد جامع مدیریت', desc: 'دید کلان زنجیره تأمین' },
          { id: 'Procurement', label: 'داشبورد تدارکات و خرید', desc: 'سفارشات و تأمین‌کنندگان' },
          { id: 'Warehouse', label: 'داشبورد انبار و موجودی', desc: 'موجودی و رسید کالا' },
          { id: 'Finance', label: 'داشبورد انطباق مالی', desc: 'تطبیق سه‌طرفه و سپیدار' },
          { id: 'Admin', label: 'داشبورد سلامت سیستم', desc: 'پایش سرویس‌ها و اتصالات' }
        ].map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveRole(tab.id as any)}
            style={{
              padding: '10px 18px',
              borderRadius: '8px',
              border: activeRole === tab.id ? '1px solid var(--primary)' : '1px solid transparent',
              background: activeRole === tab.id ? 'var(--primary-light)' : 'var(--bg-card)',
              color: activeRole === tab.id ? '#38bdf8' : 'var(--text-muted)',
              cursor: 'pointer',
              fontWeight: activeRole === tab.id ? '700' : '500',
              fontSize: '13px',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'flex-start',
              gap: '2px',
              transition: 'all 0.2s'
            }}
          >
            <span>{tab.label}</span>
            <span style={{ fontSize: '10px', color: 'var(--text-dim)', fontWeight: 'normal' }}>{tab.desc}</span>
          </button>
        ))}
      </div>

      {/* Freshness & Environment Warning Banner */}
      {integrationHealth && (
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(30, 41, 59, 0.6)',
          border: '1px solid #334155',
          borderRadius: '10px',
          padding: '12px 18px',
          marginBottom: '24px',
          fontSize: '12px',
          color: 'var(--text-muted)'
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <span style={{
              display: 'inline-block',
              width: '8px',
              height: '8px',
              borderRadius: '50%',
              background: '#10b981',
              boxShadow: '0 0 8px #10b981'
            }}></span>
            <strong style={{ color: '#f8fafc' }}>وضعیت اتصالات:</strong>
            <span>دیتابیس CRM (SQL Server): <strong style={{ color: '#34d399' }}>فعال ({integrationHealth.databaseLatencyMs}ms)</strong></span>
            <span>|</span>
            <span>سپیدار: <span style={{ color: '#f59e0b', background: 'rgba(245, 158, 11, 0.1)', padding: '2px 6px', borderRadius: '4px' }}>آداپتور آزمایشی (Fixture)</span></span>
            <span>|</span>
            <span>ایزابل PBX: <span style={{ color: '#38bdf8', background: 'rgba(56, 189, 248, 0.1)', padding: '2px 6px', borderRadius: '4px' }}>شبیه‌ساز قطعی (Simulated)</span></span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <ShieldCheck size={14} color="#38bdf8" />
            <span>نشان اصالت داده: <strong style={{ color: '#38bdf8' }}>{dashboardData?.freshnessBadge || 'داده‌های زنده CRM'}</strong></span>
          </div>
        </div>
      )}

      {/* Loading & Error States */}
      {loading && (
        <div style={{ textAlign: 'center', padding: '60px', color: 'var(--text-muted)' }}>
          <RefreshCw size={36} className="spin" style={{ margin: 'auto', marginBottom: '16px', color: 'var(--primary)' }} />
          <p>در حال محاسبه آماری شاخص‌های عملکردی و تجمیع داده‌ها...</p>
        </div>
      )}

      {error && !loading && (
        <div style={{
          background: 'var(--danger-light)',
          border: '1px solid var(--danger)',
          borderRadius: '10px',
          padding: '18px',
          color: '#fca5a5',
          marginBottom: '24px',
          display: 'flex',
          alignItems: 'center',
          gap: '12px'
        }}>
          <AlertCircle size={22} color="#ef4444" />
          <div>
            <strong>خطا در دریافت اطلاعات داشبورد:</strong>
            <p style={{ marginTop: '4px', fontSize: '13px' }}>{error}</p>
          </div>
        </div>
      )}

      {!loading && !error && (
        <>
          {/* Role-Specific Metric Cards */}
          <div className="stats-grid" style={{ marginBottom: '24px' }}>
            {activeRole === 'Manager' && (
              <>
                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>حجم کل خرید دوره</span>
                    <ShoppingCart size={18} color="#0284c7" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#fff' }}>
                    {toPersianDigits(Number(metrics.totalPurchaseVolume || 0).toLocaleString())} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>ریال</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>تعداد: {toPersianDigits(metrics.totalOrdersCount || 0)} سفارش</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>نرخ ایفای سفارشات (Fulfillment)</span>
                    <CheckCircle2 size={18} color="#10b981" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#34d399' }}>
                    ٪{toPersianDigits(metrics.fulfillmentRate || 0)}
                  </strong>
                  <div style={{ width: '100%', height: '4px', background: '#334155', borderRadius: '2px', marginTop: '6px' }}>
                    <div style={{ width: `${Math.min(100, Number(metrics.fulfillmentRate || 0))}%`, height: '100%', background: '#10b981', borderRadius: '2px' }} />
                  </div>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>نرخ موفقیت تطبیق ۳طرفه</span>
                    <TrendingUp size={18} color="#38bdf8" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#38bdf8' }}>
                    ٪{toPersianDigits(metrics.matchingSuccessRate || 0)}
                  </strong>
                  <span style={{ fontSize: '11px', color: Number(metrics.openMatchingExceptions) > 0 ? '#f59e0b' : 'var(--text-dim)' }}>
                    مغایرت‌های باز: {toPersianDigits(metrics.openMatchingExceptions || 0)} مورد
                  </span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>ارزش کل موجودی انبار</span>
                    <Package size={18} color="#a855f7" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#c084fc' }}>
                    {toPersianDigits(Number(metrics.totalInventoryValue || 0).toLocaleString())} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>ریال</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: Number(metrics.criticalInventorySkus) > 0 ? '#ef4444' : 'var(--text-dim)' }}>
                    اقلام در وضعیت بحرانی: {toPersianDigits(metrics.criticalInventorySkus || 0)} قلم
                  </span>
                </div>
              </>
            )}

            {activeRole === 'Procurement' && (
              <>
                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>حجم خرید تدارکات</span>
                    <ShoppingCart size={18} color="#0284c7" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#fff' }}>
                    {toPersianDigits(Number(metrics.totalPurchaseVolume || 0).toLocaleString())} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>ریال</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>سفارشات جاری: {toPersianDigits(metrics.activeOrdersCount || 0)}</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>تحویل به‌موقع تأمین‌کنندگان</span>
                    <Clock size={18} color="#10b981" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#34d399' }}>
                    ٪{toPersianDigits(metrics.onTimeDeliveryRate || 0)}
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>شاخص وفاداری تأمین‌کننده</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>اقلام بحرانی نیازمند سفارش</span>
                    <AlertTriangle size={18} color="#ef4444" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#f87171' }}>
                    {toPersianDigits(metrics.criticalStockCount || 0)} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>قلم کالا</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: '#fca5a5' }}>رسیده به آستانه ذخیره ایمنی</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>پیشنهادات خرید فعال سیستم</span>
                    <Layers size={18} color="#f59e0b" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#fbbf24' }}>
                    {toPersianDigits(metrics.activeRecommendationsCount || 0)} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>پیشنهاد روزانه</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>پیگیری‌های معوق: {toPersianDigits(metrics.overdueFollowUpsCount || 0)}</span>
                </div>
              </>
            )}

            {activeRole === 'Warehouse' && (
              <>
                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>تنوع کالایی انبار (SKUs)</span>
                    <Package size={18} color="#0284c7" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#fff' }}>
                    {toPersianDigits(metrics.totalSkus || 0)} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>قلم فعال</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>انبار مرکزی پارک پالاس</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>سفارشات در انتظار تحویل</span>
                    <Clock size={18} color="#f59e0b" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#fbbf24' }}>
                    {toPersianDigits(metrics.pendingReceiptsCount || 0)} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>سفارش باز</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>جهت دریافت و بازرسی کیفی (QC)</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>موجودی راکد (Stale Inventory)</span>
                    <AlertTriangle size={18} color="#a855f7" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#c084fc' }}>
                    {toPersianDigits(metrics.staleInventoryCount || 0)} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>قلم</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>بیش از ۹۰ روز بدون گردش</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>وضعیت همگام‌سازی انبار</span>
                    <CheckCircle2 size={18} color="#10b981" />
                  </div>
                  <strong style={{ fontSize: '18px', color: '#34d399' }}>
                    {metrics.syncStatus || 'سالم'}
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>انعکاس موجودی با سپیدار</span>
                </div>
              </>
            )}

            {activeRole === 'Finance' && (
              <>
                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>مجموع فاکتورهای وارده</span>
                    <ShoppingCart size={18} color="#0284c7" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#fff' }}>
                    {toPersianDigits(Number(metrics.totalInvoicedAmount || 0).toLocaleString())} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>ریال</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>فاکتورهای ثبت‌شده تأمین‌کنندگان</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>مغایرت‌های باز ۳طرفه</span>
                    <AlertTriangle size={18} color="#ef4444" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#f87171' }}>
                    {toPersianDigits(metrics.openExceptionsCount || 0)} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>پرونده</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: '#fca5a5' }}>
                    مغایرت قیمت: {toPersianDigits(metrics.priceMismatchCount || 0)} | تعداد: {toPersianDigits(metrics.quantityMismatchCount || 0)}
                  </span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>انطباق با اسناد سپیدار</span>
                    <CheckCircle2 size={18} color="#10b981" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#34d399' }}>
                    {toPersianDigits(metrics.sepidarLinkedCount || 0)} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>سند قطعی</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>صف انتظار اتصال: {toPersianDigits(metrics.sepidarBacklogCount || 0)}</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>مجموع انحرافات مالی</span>
                    <TrendingUp size={18} color="#f59e0b" />
                  </div>
                  <strong style={{ fontSize: '20px', color: '#fbbf24' }}>
                    {toPersianDigits(Number(metrics.totalVarianceAmount || 0).toLocaleString())} <span style={{ fontSize: '11px', fontWeight: 'normal', color: 'var(--text-dim)' }}>ریال</span>
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>نیازمند صدور اعلامیه بدهکار/بستانکار</span>
                </div>
              </>
            )}

            {activeRole === 'Admin' && (
              <>
                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>وضعیت سرویس Backend API</span>
                    <Activity size={18} color="#10b981" />
                  </div>
                  <strong style={{ fontSize: '18px', color: '#34d399' }}>
                    {metrics.apiHealth || 'Healthy'}
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>ASP.NET Core 8 روی پورت فعال</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>دیتابیس CRM (SQL Server)</span>
                    <ShieldCheck size={18} color="#0284c7" />
                  </div>
                  <strong style={{ fontSize: '18px', color: '#38bdf8' }}>
                    {metrics.databaseHealth || 'Healthy'}
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>تاخیر پاسخگویی: {toPersianDigits(metrics.dbLatencyMs || 0)} میلی‌ثانیه</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>وضعیت آداپتور ایزابل (Asterisk)</span>
                    <PhoneCall size={18} color="#38bdf8" />
                  </div>
                  <strong style={{ fontSize: '18px', color: '#38bdf8' }}>
                    {metrics.issabelStatus || 'Connected'}
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>شنود AMI رویدادمحور</span>
                </div>

                <div className="stat-box">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>آخرین چرخه همگام‌سازی انبار</span>
                    <RefreshCw size={18} color="#a855f7" />
                  </div>
                  <strong style={{ fontSize: '18px', color: '#c084fc' }}>
                    {metrics.inventorySyncLastRun || 'Success'}
                  </strong>
                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>اقلام پردازش‌شده: {toPersianDigits(metrics.inventorySyncItems || 0)}</span>
                </div>
              </>
            )}
          </div>

          {/* Interactive Charts & Visualizations Section */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(450px, 1fr))', gap: '20px', marginBottom: '24px' }}>
            {/* Purchase Orders Trends SVG Chart */}
            <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border-color)', borderRadius: '12px', padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <h3 style={{ fontSize: '15px', fontWeight: '700', color: '#fff' }}>روند هفتگی و ماهانه حجم خرید (ریال)</h3>
                  <p style={{ fontSize: '12px', color: 'var(--text-dim)', marginTop: '2px' }}>محاسبه شده بر مبنای تاریخ صدور رسمی سفارشات</p>
                </div>
                <span style={{ fontSize: '11px', background: 'var(--primary-light)', color: '#38bdf8', padding: '3px 8px', borderRadius: '4px' }}>
                  {toPersianDigits(dashboardData?.trendItems.length || 0)} دوره زمانی
                </span>
              </div>

              {dashboardData?.trendItems && dashboardData.trendItems.length > 0 ? (
                <div>
                  {/* SVG Bar/Area Chart */}
                  <div style={{ width: '100%', height: '220px', display: 'flex', alignItems: 'flex-end', gap: '10px', paddingTop: '20px', borderBottom: '1px solid var(--border-accent)' }}>
                    {(() => {
                      const maxVal = Math.max(...dashboardData.trendItems.map(t => Number(t.totalAmount)), 1);
                      return dashboardData.trendItems.slice(-8).map((t, idx) => {
                        const heightPct = Math.max(10, (Number(t.totalAmount) / maxVal) * 100);
                        const isHovered = hoveredTrendIndex === idx;
                        return (
                          <div
                            key={t.period}
                            onMouseEnter={() => setHoveredTrendIndex(idx)}
                            onMouseLeave={() => setHoveredTrendIndex(null)}
                            style={{
                              flex: 1,
                              display: 'flex',
                              flexDirection: 'column',
                              alignItems: 'center',
                              height: '100%',
                              justifyContent: 'flex-end',
                              position: 'relative',
                              cursor: 'pointer'
                            }}
                          >
                            {/* Tooltip */}
                            {isHovered && (
                              <div style={{
                                position: 'absolute',
                                bottom: `${heightPct + 10}%`,
                                background: '#1e293b',
                                border: '1px solid #475569',
                                color: '#fff',
                                padding: '6px 10px',
                                borderRadius: '6px',
                                fontSize: '11px',
                                whiteSpace: 'nowrap',
                                zIndex: 10,
                                boxShadow: '0 4px 12px rgba(0,0,0,0.5)',
                                pointerEvents: 'none'
                              }}>
                                <div><strong>{toPersianDigits(t.jalaliDate)}</strong></div>
                                <div>مبلغ: {toPersianDigits(Number(t.totalAmount).toLocaleString())} ریال</div>
                                <div>سفارشات: {toPersianDigits(t.orderCount)} عدد</div>
                              </div>
                            )}

                            {/* Bar */}
                            <div style={{
                              width: '80%',
                              height: `${heightPct}%`,
                              background: isHovered ? 'linear-gradient(180deg, #38bdf8, #0284c7)' : 'linear-gradient(180deg, #0284c7, #0f172a)',
                              borderRadius: '6px 6px 0 0',
                              transition: 'all 0.2s',
                              boxShadow: isHovered ? '0 0 12px rgba(56, 189, 248, 0.5)' : 'none'
                            }} />

                            {/* Date Label */}
                            <span style={{ fontSize: '10px', color: 'var(--text-dim)', marginTop: '8px', transform: 'rotate(-25deg)', whiteSpace: 'nowrap' }}>
                              {toPersianDigits(t.jalaliDate ? t.jalaliDate.slice(-5) : t.period.slice(-5))}
                            </span>
                          </div>
                        );
                      });
                    })()}
                  </div>
                </div>
              ) : (
                <div style={{ textAlign: 'center', padding: '50px 0', color: 'var(--text-dim)', fontSize: '13px' }}>
                  اطلاعاتی در بازه زمانی انتخابی یافت نشد.
                </div>
              )}
            </div>

            {/* Top Suppliers Performance Ranking */}
            <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border-color)', borderRadius: '12px', padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <h3 style={{ fontSize: '15px', fontWeight: '700', color: '#fff' }}>رتبه‌بندی عملکرد تأمین‌کنندگان</h3>
                  <p style={{ fontSize: '12px', color: 'var(--text-dim)', marginTop: '2px' }}>ارزیابی بر اساس حجم خرید، زمان تحویل و کیفیت اقلام</p>
                </div>
                {onNavigateView && (
                  <button
                    onClick={() => onNavigateView('suppliers')}
                    style={{
                      background: 'transparent',
                      border: 'none',
                      color: 'var(--primary)',
                      fontSize: '12px',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '4px'
                    }}
                  >
                    پرونده‌ها <ChevronLeft size={14} />
                  </button>
                )}
              </div>

              {dashboardData?.topSuppliers && dashboardData.topSuppliers.length > 0 ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                  {dashboardData.topSuppliers.map((supp, idx) => (
                    <div
                      key={supp.supplierId}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '10px 14px',
                        background: 'rgba(30, 41, 59, 0.4)',
                        border: '1px solid var(--border-color)',
                        borderRadius: '8px'
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <div style={{
                          width: '28px',
                          height: '28px',
                          borderRadius: '6px',
                          background: idx === 0 ? '#f59e0b' : idx === 1 ? '#94a3b8' : '#334155',
                          color: '#fff',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          fontWeight: '700',
                          fontSize: '12px'
                        }}>
                          {toPersianDigits(idx + 1)}
                        </div>
                        <div>
                          <strong style={{ fontSize: '13px', color: '#fff' }}>{supp.supplierName}</strong>
                          <div style={{ fontSize: '11px', color: 'var(--text-dim)', marginTop: '2px' }}>
                            {toPersianDigits(supp.totalOrdersCount)} سفارش | تحویل: {toPersianDigits(supp.averageDeliveryLeadTimeDays)} روز
                          </div>
                        </div>
                      </div>

                      <div style={{ textAlign: 'left' }}>
                        <strong style={{ fontSize: '13px', color: '#38bdf8' }}>
                          {toPersianDigits(Number(supp.totalSpend).toLocaleString())} ریال
                        </strong>
                        <div style={{ fontSize: '11px', color: '#34d399', marginTop: '2px' }}>
                          ایفای تعهدات: ٪{toPersianDigits(supp.fulfillmentRate)}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div style={{ textAlign: 'center', padding: '50px 0', color: 'var(--text-dim)', fontSize: '13px' }}>
                  هیچ سابقه‌ای برای تأمین‌کنندگان ثبت نشده است.
                </div>
              )}
            </div>
          </div>

          {/* Action Items & Operational Tasks Section */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border-color)', borderRadius: '12px', padding: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <h3 style={{ fontSize: '15px', fontWeight: '700', color: '#fff' }}>کارتابل هشدارهای اضطراری و وظایف عملیاتی</h3>
                <p style={{ fontSize: '12px', color: 'var(--text-dim)', marginTop: '2px' }}>اقلام نیازمند اقدام فوری بر اساس نقش فعال سازمانی</p>
              </div>
              <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                {toPersianDigits(dashboardData?.actionItems.length || 0)} اقدام در انتظار
              </span>
            </div>

            {dashboardData?.actionItems && dashboardData.actionItems.length > 0 ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                {dashboardData.actionItems.map(item => (
                  <div
                    key={item.id}
                    onClick={() => onNavigateView && onNavigateView(item.targetView as any)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '12px 16px',
                      borderRadius: '8px',
                      background: item.severity === 'Critical' ? 'rgba(239, 68, 68, 0.08)' : 'rgba(245, 158, 11, 0.08)',
                      border: item.severity === 'Critical' ? '1px solid rgba(239, 68, 68, 0.3)' : '1px solid rgba(245, 158, 11, 0.3)',
                      cursor: onNavigateView ? 'pointer' : 'default',
                      transition: 'all 0.2s'
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                      <AlertTriangle size={18} color={item.severity === 'Critical' ? '#ef4444' : '#f59e0b'} />
                      <div>
                        <strong style={{ fontSize: '13px', color: '#fff' }}>{item.title}</strong>
                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>{item.subtitle}</p>
                      </div>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>{item.jalaliDate}</span>
                      <ExternalLink size={14} color="var(--primary)" />
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ textAlign: 'center', padding: '30px', color: '#34d399', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px' }}>
                <CheckCircle2 size={18} />
                <span>کلیه عملیات و سفارشات در وضعیت پایدار قرار دارند و هیچ وظیفه معوقی وجود ندارد.</span>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};
