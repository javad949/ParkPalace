import React, { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';
import { fetchExecutiveDashboard, fetchPartyLedger } from '../../services/api';
import {
  ExecutiveDashboardDto,
  ExecutiveKpiCardDto,
  PartyLedgerDto,
  ViewType
} from '../../types';
import { toPersianDigits } from '../../utils/jalali';
import { JalaliDatePicker } from '../JalaliDatePicker';

interface Props {
  devRole?: string;
  onNavigateView?: (view: ViewType, drillQuery?: string) => void;
}

const statusName = (status: ExecutiveKpiCardDto['status']) => {
  if (typeof status === 'number') {
    return ['', 'Verified', 'Unverified', 'Blocked', 'Unavailable', 'NoData', 'Zero'][status] || String(status);
  }
  return status;
};

const statusLabelFa = (status: ExecutiveKpiCardDto['status']) => {
  switch (statusName(status)) {
    case 'Verified': return 'تأییدشده';
    case 'Unverified': return 'نیازمند اعتبارسنجی';
    case 'Blocked': return 'مسدود';
    case 'Unavailable': return 'ارتباط ناموفق';
    case 'NoData': return 'داده‌ای نیست';
    case 'Zero': return 'صفر تأییدشده';
    default: return String(status);
  }
};

const isNonNumericStatus = (status: ExecutiveKpiCardDto['status']) => {
  const n = statusName(status);
  return n === 'Blocked' || n === 'Unavailable' || n === 'NoData';
};

const formatValue = (card: ExecutiveKpiCardDto) => {
  if (isNonNumericStatus(card.status)) return '—';
  if (card.amount != null) return toPersianDigits(card.amount.toLocaleString('fa-IR'));
  if (card.numericValue != null) return toPersianDigits(String(card.numericValue));
  return '—';
};

const isoDate = (v?: string | null) => (v ? v.slice(0, 10) : undefined);

export const ExecutiveDashboardView: React.FC<Props> = ({ devRole, onNavigateView }) => {
  const [data, setData] = useState<ExecutiveDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fiscalYear, setFiscalYear] = useState('');
  const [fromUtc, setFromUtc] = useState('');
  const [toUtc, setToUtc] = useState('');
  const [partyCategory, setPartyCategory] = useState('');
  const [search, setSearch] = useState('');
  const [followUpOwner, setFollowUpOwner] = useState('');
  const [ledger, setLedger] = useState<PartyLedgerDto | null>(null);
  const [ledgerError, setLedgerError] = useState<string | null>(null);

  const selectedYear = useMemo(() => {
    const id = fiscalYear ? Number(fiscalYear) : data?.slicers.activeFiscalYearId;
    return (data?.slicers.fiscalYears || []).find((y) => y.fiscalYearId === id) || null;
  }, [fiscalYear, data]);

  const minDate = isoDate(selectedYear?.startDate);
  const maxDate = isoDate(selectedYear?.endDate);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const fy = fiscalYear ? Number(fiscalYear) : undefined;
      const result = await fetchExecutiveDashboard({
        fiscalYear: Number.isFinite(fy as number) ? fy : undefined,
        fromUtc: fromUtc || undefined,
        toUtc: toUtc || undefined,
        partyCategory: partyCategory ? Number(partyCategory) : undefined,
        search: search || undefined,
        followUpOwner: followUpOwner || undefined
      }, devRole);
      setData(result);
    } catch (err: any) {
      setError(err.message || 'بارگذاری داشبورد تصمیم ناموفق بود.');
      setData(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [devRole]);

  const openParty = async (partyId: string) => {
    setLedgerError(null);
    try {
      const row = await fetchPartyLedger(partyId, true, devRole);
      setLedger(row);
    } catch (err: any) {
      setLedger(null);
      setLedgerError(err.message || 'پرونده مالی در دسترس نیست.');
    }
  };

  const drill = (target?: string | null, query?: string | null) => {
    if (!target) return;
    const map: Record<string, ViewType> = {
      orders: 'orders',
      warehouse: 'warehouse',
      finance: 'finance',
      financeDashboard: 'financeDashboard',
      calls: 'calls',
      materials: 'materials'
    };
    const view = map[target];
    if (!view) return;
    try {
      sessionStorage.setItem('parkpalace_drill', JSON.stringify({
        view,
        query: query || '',
        at: new Date().toISOString()
      }));
    } catch { /* ignore */ }
    if (onNavigateView) onNavigateView(view, query || undefined);
  };

  const slicerMap = Object.fromEntries((data?.slicers.items || []).map((s) => [s.key, s]));

  return (
    <section className="view-card" style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
      <header style={{ display: 'flex', justifyContent: 'space-between', gap: '12px', flexWrap: 'wrap' }}>
        <div>
          <h2 style={{ margin: 0 }}>داشبورد تصمیم مدیریتی</h2>
          <p style={{ color: 'var(--text-muted)', margin: '6px 0 0', maxWidth: '720px' }}>
            وضعیت عملیاتی و مالی در یک نگاه، موارد نیازمند اقدام، و اشخاص/کالاهایی که باید بررسی شوند.
            ارقام سپیدار فقط با مجوز مالی صریح و با برچسب اعتبارسنجی نمایش داده می‌شوند؛ عدد ساختگی نشان داده نمی‌شود.
          </p>
        </div>
        <button type="button" className="btn-primary" onClick={load} disabled={loading} style={{ display: 'inline-flex', gap: 8, alignItems: 'center' }}>
          <RefreshCw size={16} /> به‌روزرسانی
        </button>
      </header>

      {data && (
        <div style={{ background: 'rgba(15, 23, 42, 0.55)', border: '1px solid var(--border-color)', borderRadius: 10, padding: '12px 14px', fontSize: '0.86rem' }}>
          <div>{data.freshness.opsFreshnessBadge}</div>
          <div>سال مالی: {data.freshness.fiscalYearLabel || '—'}</div>
          <div>آخرین خواندن موفق دفتر: {data.freshness.lastSuccessfulReadUtc ? new Date(data.freshness.lastSuccessfulReadUtc).toLocaleString('fa-IR') : 'داده‌ای نیست'}</div>
          {data.freshness.financeFreshness && (
            <div>وضعیت مالی: {data.freshness.financeFreshness} — {data.freshness.financeFreshnessDetail}</div>
          )}
          {data.comparison && (
            <div>
              مقایسه سال قبل: {data.comparison.hasEquivalentPreviousPeriod
                ? `فعال (${data.comparison.previousFromUtc ? new Date(data.comparison.previousFromUtc).toLocaleDateString('fa-IR') : '—'} تا ${data.comparison.previousToUtc ? new Date(data.comparison.previousToUtc).toLocaleDateString('fa-IR') : '—'})`
                : 'مسدود / دادهٔ معتبر نیست'}
              {data.comparison.statusNote ? ` — ${data.comparison.statusNote}` : ''}
            </div>
          )}
          <div>پرچم کامل‌بودن: {(data.freshness.completenessFlags || []).join('، ') || '—'}</div>
          <div style={{ marginTop: 6, color: '#93c5fd' }}>{data.viewer.permissionsNote}</div>
          {data.financeAccessNote && (
            <div style={{ marginTop: 6, color: '#fbbf24', display: 'flex', gap: 8, alignItems: 'flex-start' }}>
              <AlertTriangle size={16} style={{ marginTop: 2 }} />
              <span>{data.financeAccessNote}</span>
            </div>
          )}
        </div>
      )}

      <form
        onSubmit={(e) => { e.preventDefault(); load(); }}
        style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'end' }}
      >
        {slicerMap.fiscalYear && (
          <label style={{ opacity: slicerMap.fiscalYear.enabled ? 1 : 0.45 }}>
            {slicerMap.fiscalYear.label}
            <select
              disabled={!slicerMap.fiscalYear.enabled}
              value={fiscalYear}
              onChange={(e) => setFiscalYear(e.target.value)}
              title={slicerMap.fiscalYear.disabledReason || slicerMap.fiscalYear.appliesTo || undefined}
              style={{ display: 'block', marginTop: 4 }}
            >
              <option value="">پیش‌فرض</option>
              {(data?.slicers.fiscalYears || []).map((y) => (
                <option key={y.fiscalYearId} value={y.fiscalYearId}>{y.title}</option>
              ))}
            </select>
            {!slicerMap.fiscalYear.enabled && (
              <span style={{ display: 'block', color: '#64748b', fontSize: '0.72rem' }}>{slicerMap.fiscalYear.disabledReason}</span>
            )}
          </label>
        )}

        {slicerMap.dateRange && (
          <div style={{ opacity: slicerMap.dateRange.enabled ? 1 : 0.45 }} title={slicerMap.dateRange.disabledReason || undefined}>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10 }}>
              <JalaliDatePicker
                label="از تاریخ شمسی"
                value={fromUtc || null}
                onChange={setFromUtc}
                minDate={minDate}
                maxDate={maxDate}
                disabled={!slicerMap.dateRange.enabled}
              />
              <JalaliDatePicker
                label="تا تاریخ شمسی"
                value={toUtc || null}
                onChange={setToUtc}
                minDate={minDate}
                maxDate={maxDate}
                disabled={!slicerMap.dateRange.enabled}
              />
            </div>
            {!slicerMap.dateRange.enabled && (
              <span style={{ display: 'block', color: '#64748b', fontSize: '0.72rem', marginTop: 4 }}>
                {slicerMap.dateRange.disabledReason || 'غیرفعال'}
              </span>
            )}
          </div>
        )}

        {slicerMap.partyType && (
          <label style={{ opacity: slicerMap.partyType.enabled ? 1 : 0.45 }} title={slicerMap.partyType.disabledReason || undefined}>
            {slicerMap.partyType.label}
            <select
              disabled={!slicerMap.partyType.enabled}
              value={partyCategory}
              onChange={(e) => setPartyCategory(e.target.value)}
              style={{ display: 'block', marginTop: 4 }}
            >
              <option value="">همه</option>
              <option value="1">تأمین‌کننده</option>
              <option value="3">مشتری</option>
            </select>
            <span style={{ display: 'block', color: '#fbbf24', fontSize: '0.72rem' }}>
              {slicerMap.partyType.appliesTo === 'needs-review-only' ? 'فقط فهرست بازبینی — نه جمع AR/AP' : (slicerMap.partyType.disabledReason || '')}
            </span>
          </label>
        )}

        {slicerMap.partySearch && (
          <label style={{ opacity: slicerMap.partySearch.enabled ? 1 : 0.45 }}>
            {slicerMap.partySearch.label}
            <input
              disabled={!slicerMap.partySearch.enabled}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ display: 'block', marginTop: 4 }}
            />
          </label>
        )}

        {slicerMap.followUpOwner && (
          <label style={{ opacity: slicerMap.followUpOwner.enabled ? 1 : 0.45 }}>
            {slicerMap.followUpOwner.label}
            <input
              disabled={!slicerMap.followUpOwner.enabled}
              value={followUpOwner}
              onChange={(e) => setFollowUpOwner(e.target.value)}
              style={{ display: 'block', marginTop: 4 }}
            />
          </label>
        )}

        {(data?.slicers.items || [])
          .filter((s) => !s.enabled && !['fiscalYear', 'partyType', 'partySearch', 'followUpOwner', 'dateRange'].includes(s.key))
          .map((s) => (
            <span key={s.key} title={s.disabledReason || ''} style={{ color: '#64748b', fontSize: '0.8rem', alignSelf: 'center' }}>
              {s.label}: غیرفعال — {s.disabledReason}
            </span>
          ))}

        <button className="btn-primary" type="submit" disabled={loading}>اعمال</button>
      </form>

      {error && <div role="alert" style={{ color: '#f87171' }}>{error}</div>}
      {loading && <div>در حال بارگذاری...</div>}

      {data && (
        <>
          <section>
            <h3 style={{ marginBottom: 10 }}>۱) وضعیت</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))', gap: 12 }}>
              {data.cards.map((card) => (
                <article
                  key={`${card.contractId}-${card.code}`}
                  onClick={() => {
                    if (isNonNumericStatus(card.status) && !card.drillTarget) return;
                    drill(card.drillTarget, card.drillQuery);
                  }}
                  style={{
                    background: 'rgba(15, 23, 42, 0.72)',
                    border: '1px solid var(--border-color)',
                    borderRadius: 12,
                    padding: '14px 16px',
                    cursor: card.drillTarget ? 'pointer' : 'default',
                    minHeight: 168
                  }}
                >
                  <div style={{ color: '#94a3b8', fontSize: '0.82rem' }}>{card.title}</div>
                  <div style={{ fontSize: '1.3rem', fontWeight: 800, margin: '10px 0' }}>
                    {formatValue(card)}
                    {!isNonNumericStatus(card.status) && card.unit && formatValue(card) !== '—' ? (
                      <span style={{ fontSize: '0.75rem', marginRight: 6, color: '#94a3b8' }}>{card.unit}</span>
                    ) : null}
                  </div>
                  <div style={{
                    color: statusName(card.status) === 'Unverified' ? '#fbbf24'
                      : statusName(card.status) === 'Blocked' || statusName(card.status) === 'Unavailable' ? '#fb7185'
                        : '#86efac',
                    fontWeight: 700,
                    fontSize: '0.78rem'
                  }}>
                    {statusLabelFa(card.status)} · {card.contractId}
                  </div>
                  <p style={{ color: '#94a3b8', fontSize: '0.75rem', margin: '8px 0 0' }}>{card.reason}</p>
                  {card.validationCause && (
                    <p style={{ color: '#fbbf24', fontSize: '0.72rem', margin: '4px 0 0' }}>علت عدم تأیید: {card.validationCause}</p>
                  )}
                  {card.previousPeriodAmount != null && (
                    <p style={{ color: '#93c5fd', fontSize: '0.72rem', margin: '4px 0 0' }}>
                      دوره مشابه قبل: {toPersianDigits(card.previousPeriodAmount.toLocaleString('fa-IR'))}
                      {card.previousPeriodNote ? ` (${card.previousPeriodNote})` : ''}
                    </p>
                  )}
                  <div style={{ color: '#64748b', fontSize: '0.7rem', marginTop: 8 }}>
                    منبع: {card.source}
                    {card.lastSuccessfulReadUtc
                      ? ` · آخرین خواندن: ${new Date(card.lastSuccessfulReadUtc).toLocaleString('fa-IR')}`
                      : ''}
                  </div>
                </article>
              ))}
            </div>
          </section>

          <section>
            <h3 style={{ marginBottom: 10 }}>۲) نیازمند اقدام</h3>
            {data.actionItems.length === 0 ? (
              <p style={{ color: 'var(--text-muted)' }}>مورد فوری در بازه جاری ثبت نشده است.</p>
            ) : (
              <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 8 }}>
                {data.actionItems.map((item) => (
                  <li
                    key={`${item.id}-${item.title}`}
                    onClick={() => drill(item.targetView)}
                    style={{
                      border: '1px solid var(--border-color)',
                      borderRadius: 10,
                      padding: '10px 12px',
                      cursor: 'pointer',
                      background: item.severity === 'Critical' ? 'rgba(248, 113, 113, 0.08)' : 'rgba(15,23,42,0.5)'
                    }}
                  >
                    <strong>{item.title}</strong>
                    <div style={{ color: '#94a3b8', fontSize: '0.82rem' }}>{item.subtitle}</div>
                    <div style={{ color: '#64748b', fontSize: '0.75rem', marginTop: 4 }}>{item.jalaliDate} · {item.severity}</div>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section>
            <h3 style={{ marginBottom: 10 }}>۳) تمرکز بررسی</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 14 }}>
              <div>
                <h4>بیشترین مانده مطالبات</h4>
                {(data.topDebtors || []).length === 0 ? <p style={{ color: '#64748b' }}>—</p> : (
                  <table className="data-table">
                    <thead><tr><th>طرف‌حساب</th><th>مانده</th><th></th></tr></thead>
                    <tbody>
                      {data.topDebtors.map((row) => (
                        <tr key={`d-${row.code}`}>
                          <td>{row.crmName || row.title}<div dir="ltr" style={{ color: '#64748b', fontSize: '0.75rem' }}>{row.code}</div></td>
                          <td>{toPersianDigits(row.net.toLocaleString('fa-IR'))}</td>
                          <td>{row.partyId ? <button type="button" onClick={() => openParty(row.partyId!)}>پرونده</button> : null}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
              <div>
                <h4>بیشترین مانده بدهی</h4>
                {(data.topCreditors || []).length === 0 ? <p style={{ color: '#64748b' }}>—</p> : (
                  <table className="data-table">
                    <thead><tr><th>طرف‌حساب</th><th>مانده</th><th></th></tr></thead>
                    <tbody>
                      {data.topCreditors.map((row) => (
                        <tr key={`c-${row.code}`}>
                          <td>{row.crmName || row.title}<div dir="ltr" style={{ color: '#64748b', fontSize: '0.75rem' }}>{row.code}</div></td>
                          <td>{toPersianDigits(row.net.toLocaleString('fa-IR'))}</td>
                          <td>{row.partyId ? <button type="button" onClick={() => openParty(row.partyId!)}>پرونده</button> : null}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
              <div>
                <h4>مواد بحرانی / هشدار</h4>
                {(data.criticalMaterials || []).length === 0 ? <p style={{ color: '#64748b' }}>—</p> : (
                  <ul style={{ paddingRight: 18, margin: 0 }}>
                    {data.criticalMaterials.map((m) => (
                      <li key={m.materialId} style={{ marginBottom: 6, cursor: 'pointer' }} onClick={() => drill('warehouse', 'filter=critical')}>
                        {m.materialName} ({m.materialCode}) — {m.inventoryRiskStatus}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              <div>
                <h4>نیازمند نگاشت / بازبینی</h4>
                {(data.needsReview || []).length === 0 ? <p style={{ color: '#64748b' }}>—</p> : (
                  <ul style={{ paddingRight: 18, margin: 0 }}>
                    {data.needsReview.slice(0, 8).map((p) => (
                      <li key={p.partyId} style={{ marginBottom: 6 }}>
                        <button type="button" onClick={() => openParty(p.partyId)} style={{ background: 'none', border: 'none', color: '#7dd3fc', cursor: 'pointer', padding: 0 }}>
                          {p.displayName}
                        </button>
                        <div style={{ color: '#94a3b8', fontSize: '0.78rem' }}>{p.issue}</div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </section>

          {(data.monthlyTrend || []).length > 0 && (
            <section>
              <h3>روند ماهانه (سپیدار — نیازمند اعتبارسنجی)</h3>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {data.monthlyTrend.slice(0, 12).map((row) => (
                  <div key={`${row.monthStart}-${row.side}-${row.previousPeriod}`} style={{ display: 'flex', gap: 12, fontSize: '0.85rem' }}>
                    <span style={{ width: 140 }}>{new Date(row.monthStart).toLocaleDateString('fa-IR', { year: 'numeric', month: 'short' })}</span>
                    <span style={{ width: 90 }}>{row.side === 'Payable' ? 'بدهی' : 'مطالبات'}</span>
                    <span dir="ltr">{toPersianDigits(row.net.toLocaleString('fa-IR'))}</span>
                    {row.previousPeriod ? <span style={{ color: '#64748b' }}>سال قبل</span> : null}
                  </div>
                ))}
              </div>
            </section>
          )}
        </>
      )}

      {ledgerError && <div role="alert" style={{ color: '#f87171' }}>{ledgerError}</div>}
      {ledger && (
        <section style={{ borderTop: '1px solid var(--border-color)', paddingTop: 16 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ margin: 0 }}>پرونده طرف‌حساب: {ledger.displayName}</h3>
            <button type="button" onClick={() => setLedger(null)}>بستن</button>
          </div>
          <p style={{ color: '#94a3b8' }}>
            نگاشت: {ledger.mappingVerified ? 'تأییدشده' : 'ناقص'} · مانده پایان:{' '}
            {ledger.closingBalance.amount == null ? '—' : toPersianDigits(ledger.closingBalance.amount.toLocaleString('fa-IR'))}
            {' '}({statusLabelFa(ledger.closingBalance.status)})
          </p>
          {(ledger.lines || []).length > 0 && (
            <table className="data-table">
              <thead><tr><th>تاریخ</th><th>شماره</th><th>شرح</th><th>بدهکار</th><th>بستانکار</th></tr></thead>
              <tbody>
                {ledger.lines.slice(0, 15).map((line, i) => (
                  <tr key={i}>
                    <td>{line.dateUtc ? new Date(line.dateUtc).toLocaleDateString('fa-IR') : '—'}</td>
                    <td dir="ltr">{line.documentNumber || '—'}</td>
                    <td>{line.description || line.documentType || '—'}</td>
                    <td>{line.debit != null ? toPersianDigits(line.debit.toLocaleString('fa-IR')) : '—'}</td>
                    <td>{line.credit != null ? toPersianDigits(line.credit.toLocaleString('fa-IR')) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {ledger.linesStatus && (ledger.lines || []).length === 0 && (
            <p style={{ color: '#fbbf24' }}>{ledger.linesReason}</p>
          )}
        </section>
      )}
    </section>
  );
};
