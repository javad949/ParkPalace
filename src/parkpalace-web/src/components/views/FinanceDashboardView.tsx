import React, { useEffect, useState } from 'react';
import { fetchFinancialDashboard, fetchPartyLedger, linkDirectorySepidar } from '../../services/api';
import { FinancialDashboardDto, FinancialFigureDto, PartyLedgerDto } from '../../types';
import { consumeDrillForView, drillBannerLabel } from '../../utils/drillNavigation';

const statusLabel = (status: FinancialFigureDto['status'] | PartyLedgerDto['linesStatus']) => {
  const name = typeof status === 'number'
    ? ['', 'Verified', 'Unverified', 'Blocked', 'Unavailable', 'NoData', 'Zero'][status] || String(status)
    : status;
  switch (name) {
    case 'Verified': return 'تأییدشده';
    case 'Unverified': return 'هنوز تأیید نشده';
    case 'Blocked': return 'مسدود';
    case 'Unavailable': return 'ارتباط ناموفق';
    case 'NoData': return 'داده‌ای نیست';
    case 'Zero': return 'صفر تأییدشده';
    default: return name;
  }
};

const MonthlyChart: React.FC<{ rows: NonNullable<FinancialDashboardDto['monthly']> }> = ({ rows }) => {
  if (rows.length === 0) return null;
  const max = Math.max(...rows.map(row => Math.abs(row.net)), 1);
  const months = Array.from(new Set(rows.map(row => row.monthStart))).sort();
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
      {months.map(month => (
        <div key={month}>
          <div style={{ marginBottom: '4px' }}>{new Date(month).toLocaleDateString('fa-IR', { year: 'numeric', month: 'long' })}</div>
          {rows.filter(row => row.monthStart === month).map(row => {
            const width = `${Math.max(2, Math.round((Math.abs(row.net) / max) * 100))}%`;
            const color = row.side === 'Payable' ? 'rgba(251, 113, 133, 0.85)' : 'rgba(125, 211, 252, 0.9)';
            const label = `${row.previousPeriod ? 'سال قبل · ' : ''}${row.side === 'Payable' ? 'بدهی' : 'مطالبات'}`;
            return (
              <div key={`${row.side}-${row.previousPeriod}`} style={{ display: 'flex', alignItems: 'center', gap: '8px', opacity: row.previousPeriod ? 0.55 : 1 }}>
                <div style={{ width: '120px', color: '#94a3b8', fontSize: '0.75rem' }}>{label}</div>
                <div style={{ flex: 1, background: 'rgba(15, 23, 42, 0.5)', borderRadius: '999px' }}>
                  <div style={{ width, height: '10px', borderRadius: '999px', background: color }} />
                </div>
                <div style={{ width: '110px', textAlign: 'left' }} dir="ltr">{row.net.toLocaleString('fa-IR')}</div>
              </div>
            );
          })}
        </div>
      ))}
    </div>
  );
};

const FigureCard: React.FC<{ figure: FinancialFigureDto; highlighted?: boolean }> = ({ figure, highlighted }) => (
  <article style={{
    background: 'rgba(15, 23, 42, 0.72)',
    border: highlighted ? '1px solid rgba(56, 189, 248, 0.85)' : '1px solid var(--border-color)',
    boxShadow: highlighted ? '0 0 0 1px rgba(56, 189, 248, 0.35)' : undefined,
    borderRadius: '12px',
    padding: '14px 16px',
    minHeight: '140px'
  }}>
    <div style={{ color: '#94a3b8', fontSize: '0.85rem' }}>{figure.title}</div>
    <div style={{ fontSize: '1.35rem', fontWeight: 800, margin: '10px 0', color: '#f8fafc' }}>
      {figure.amount == null ? '—' : figure.amount.toLocaleString('fa-IR')}
    </div>
    <div style={{ color: '#fbbf24', fontWeight: 700, fontSize: '0.8rem' }}>{statusLabel(figure.status)}</div>
    <p style={{ color: '#94a3b8', fontSize: '0.78rem', margin: '8px 0 0' }}>{figure.reason}</p>
  </article>
);

export const FinanceDashboardView: React.FC<{ devRole?: string }> = ({ devRole }) => {
  const [fiscalYear, setFiscalYear] = useState('');
  const [fromUtc, setFromUtc] = useState('');
  const [toUtc, setToUtc] = useState('');
  const [partyCategory, setPartyCategory] = useState('');
  const [search, setSearch] = useState('');
  const [followUpOwner, setFollowUpOwner] = useState('');
  const [dashboard, setDashboard] = useState<FinancialDashboardDto | null>(null);
  const [ledger, setLedger] = useState<PartyLedgerDto | null>(null);
  const [linkCode, setLinkCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [focusSide, setFocusSide] = useState<'receivables' | 'payables' | null>(null);
  const [drillNote, setDrillNote] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchFinancialDashboard({
        fiscalYear: fiscalYear ? Number(fiscalYear) : undefined,
        fromUtc: fromUtc ? new Date(fromUtc).toISOString() : undefined,
        toUtc: toUtc ? new Date(toUtc).toISOString() : undefined,
        partyCategory: partyCategory ? Number(partyCategory) : undefined,
        search: search || undefined,
        followUpOwner: followUpOwner || undefined
      }, devRole);
      setDashboard(data);
    } catch (err: any) {
      setDashboard(null);
      setError(err.message || 'داشبورد مالی بارگذاری نشد.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const drill = consumeDrillForView('financeDashboard');
    if (drill.side === 'receivables' || drill.side === 'payables') {
      setFocusSide(drill.side);
      setDrillNote(drillBannerLabel(drill));
    }
  }, []);

  useEffect(() => { load(); }, [devRole]);

  const openParty = async (partyId: string) => {
    setError(null);
    try {
      const next = await fetchPartyLedger(partyId, true, devRole);
      setLedger(next);
      setLinkCode(next.sepidarCode || '');
    } catch (err: any) {
      setError(err.message || 'پرونده طرف‌حساب باز نشد.');
    }
  };

  const linkSepidar = async () => {
    if (!ledger || !linkCode.trim()) return;
    setError(null);
    try {
      await linkDirectorySepidar(ledger.partyId, linkCode.trim(), devRole);
      await openParty(ledger.partyId);
      await load();
    } catch (err: any) {
      setError(err.message || 'اتصال کد سپیدار تأیید نشد.');
    }
  };

  const clearFilters = async () => {
    setFiscalYear('');
    setFromUtc('');
    setToUtc('');
    setPartyCategory('');
    setSearch('');
    setFollowUpOwner('');
    setLoading(true);
    setError(null);
    try {
      setDashboard(await fetchFinancialDashboard({}, devRole));
    } catch (err: any) {
      setDashboard(null);
      setError(err.message || 'داشبورد مالی بارگذاری نشد.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <section className="view-card" style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
      <header>
        <h2 style={{ margin: 0 }}>داشبورد مالی</h2>
        <p style={{ color: 'var(--text-muted)', margin: '6px 0 0' }}>
          مطالبات و بدهی از گردش سند سپیدار همین شرکت ساخته می‌شوند. دریافت، پرداخت و سررسید تا وقتی سندشان مشخص نشود خط تیره می‌مانند و خط تیره یعنی صفر نیست.
        </p>
        {drillNote && (
          <div role="status" style={{ marginTop: 10, color: '#7dd3fc', fontSize: '0.85rem' }}>{drillNote}</div>
        )}
      </header>

      {dashboard && (
        <div style={{ background: 'rgba(56, 189, 248, 0.08)', border: '1px solid rgba(56, 189, 248, 0.3)', borderRadius: '10px', padding: '12px 14px' }}>
          <div>سال مالی: {dashboard.catalogFiscalYear ? `${dashboard.catalogFiscalYear.title} (شناسه ${dashboard.catalogFiscalYear.fiscalYearId})` : 'ثبت نشده'} — خواندن زنده: {dashboard.catalogFiscalYear?.liveRead ? 'بله' : 'خیر'}</div>
          <div>
            بازه جاری: {dashboard.currentPeriodFromUtc ? new Date(dashboard.currentPeriodFromUtc).toLocaleDateString('fa-IR') : '—'}
            {' تا '}
            {dashboard.currentPeriodToUtc ? new Date(dashboard.currentPeriodToUtc).toLocaleDateString('fa-IR') : '—'}
            {dashboard.previousPeriodFromUtc && (
              <> — بازه معادل سال قبل: {new Date(dashboard.previousPeriodFromUtc).toLocaleDateString('fa-IR')} تا {dashboard.previousPeriodToUtc ? new Date(dashboard.previousPeriodToUtc).toLocaleDateString('fa-IR') : '—'}</>
            )}
          </div>
          <div>آخرین خواندن موفق دفتر مالی: {dashboard.lastSuccessfulFinancialReadUtc ? new Date(dashboard.lastSuccessfulFinancialReadUtc).toLocaleString('fa-IR') : 'داده‌ای نیست'}</div>
          <div>وضعیت: {dashboard.freshness} — {dashboard.freshnessDetail}</div>
          <div>{dashboard.comparisonStatus}</div>
        </div>
      )}

      <form onSubmit={e => { e.preventDefault(); load(); }} style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', alignItems: 'end' }}>
        <label>سال مالی
          <select value={fiscalYear} onChange={e => setFiscalYear(e.target.value)} style={{ display: 'block', marginTop: '4px' }}>
            <option value="">سال جاری</option>
            {(dashboard?.fiscalYears || []).map(year => (
              <option key={year.fiscalYearId} value={year.title}>{year.title}</option>
            ))}
          </select>
        </label>
        <label>از تاریخ<input type="date" value={fromUtc} onChange={e => setFromUtc(e.target.value)} style={{ display: 'block', marginTop: '4px' }} /></label>
        <label>تا تاریخ<input type="date" value={toUtc} onChange={e => setToUtc(e.target.value)} style={{ display: 'block', marginTop: '4px' }} /></label>
        <label>نوع طرف‌حساب
          <select value={partyCategory} onChange={e => setPartyCategory(e.target.value)} style={{ display: 'block', marginTop: '4px' }}>
            <option value="">همه</option>
            <option value="1">تأمین‌کننده</option>
            <option value="3">مشتری</option>
          </select>
        </label>
        <label>نام یا کد سپیدار<input value={search} onChange={e => setSearch(e.target.value)} style={{ display: 'block', marginTop: '4px' }} /></label>
        <label>مسئول پیگیری CRM<input value={followUpOwner} onChange={e => setFollowUpOwner(e.target.value)} style={{ display: 'block', marginTop: '4px' }} /></label>
        <button className="btn-primary" type="submit" disabled={loading}>اعمال فیلتر</button>
        <button type="button" onClick={clearFilters}>پاک کردن فیلترها</button>
      </form>

      {dashboard && dashboard.activeFilters.length > 0 && (
        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
          {dashboard.activeFilters.map(f => (
            <span key={f.key} style={{ background: 'rgba(251, 191, 36, 0.15)', color: '#fbbf24', borderRadius: '999px', padding: '2px 10px' }}>{f.label}</span>
          ))}
        </div>
      )}

      {error && <div role="alert" style={{ color: '#f87171' }}>{error}</div>}
      {loading && <div>در حال بارگذاری...</div>}

      {dashboard && (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '12px' }}>
            {dashboard.cards.map(card => (
              <FigureCard
                key={card.code}
                figure={card}
                highlighted={!!focusSide && card.code === focusSide}
              />
            ))}
          </div>

          {(dashboard.accountScope || []).length > 0 && (
            <section>
              <h3>معین‌هایی که در جمع آمده‌اند</h3>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
                {dashboard.accountScope!.map(account => (
                  <span key={`${account.side}-${account.code}`} style={{
                    border: '1px solid var(--border-color)',
                    borderRadius: '999px',
                    padding: '2px 10px',
                    color: account.side === 'Payable' ? '#fb7185' : '#7dd3fc'
                  }}>
                    {account.code} {account.title}
                  </span>
                ))}
              </div>
            </section>
          )}

          <section>
            <h3>مقایسه ماهانه</h3>
            {(dashboard.monthly || []).length === 0 && dashboard.monthlyComparison.map(item => (
              <p key={item.code}><strong>{statusLabel(item.status)}.</strong> {item.reason}</p>
            ))}
            <MonthlyChart rows={dashboard.monthly || []} />
          </section>

          <section>
            <h3>سررسید</h3>
            <p><strong>{statusLabel(dashboard.dueDates.status)}.</strong> {dashboard.dueDates.reason}</p>
          </section>

          <section>
            <h3>بزرگ‌ترین مانده‌ها</h3>
            <p><strong>{statusLabel(dashboard.largestBalances.status)}.</strong> {dashboard.largestBalances.reason}</p>
            {(dashboard.topParties || []).length > 0 && (
              <table className="data-table">
                <thead>
                  <tr><th>سمت</th><th>کد تفصیلی</th><th>عنوان سپیدار</th><th>مانده</th><th>پرونده CRM</th></tr>
                </thead>
                <tbody>
                  {dashboard.topParties!.map(row => (
                    <tr key={`${row.side}-${row.code}`}>
                      <td>{row.side === 'Payable' ? 'بدهی' : 'مطالبات'}</td>
                      <td dir="ltr">{row.code}</td>
                      <td>{row.title}</td>
                      <td>{row.net.toLocaleString('fa-IR')}</td>
                      <td>
                        {row.partyId
                          ? <button type="button" onClick={() => openParty(row.partyId!)}>{row.crmName || 'پرونده'}</button>
                          : 'در CRM یک پروندهٔ یکتا ندارد'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {(dashboard.sepidarSearchMessage || (dashboard.sepidarSearchHits || []).length > 0) && (
            <section>
              <h3>نتایج سپیدار</h3>
              <p>{dashboard.sepidarSearchMessage || 'جستجوی زنده سپیدار برای نام یا کد تفصیلی.'}</p>
              <table className="data-table">
                <thead>
                  <tr><th>کد تفصیلی</th><th>عنوان سپیدار</th><th>شناسه Party</th><th>پرونده CRM</th><th>نگاشت</th></tr>
                </thead>
                <tbody>
                  {(dashboard.sepidarSearchHits || []).length === 0 && (
                    <tr><td colSpan={5}>موردی در سپیدار پیدا نشد.</td></tr>
                  )}
                  {(dashboard.sepidarSearchHits || []).map(row => (
                    <tr key={`${row.code}-${row.partyRef || row.title}`}>
                      <td dir="ltr">{row.code}</td>
                      <td>{row.title}</td>
                      <td dir="ltr">{row.partyRef || '—'}</td>
                      <td>
                        {row.partyId
                          ? <button type="button" onClick={() => { setLinkCode(row.code); openParty(row.partyId!); }}>{row.crmName || 'پرونده'}</button>
                          : 'در CRM یک پروندهٔ یکتا ندارد'}
                      </td>
                      <td>{row.mappingVerified ? 'تأییدشده' : 'تأیید نشده'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          )}

          <section>
            <h3>نیازمند بررسی</h3>
            <p>طرف‌حساب بدون شناسه سپیدار: {dashboard.unmappedPartyCount.toLocaleString('fa-IR')}{dashboard.reviewSampleCapped ? ' (نمونه فهرست سقف دارد)' : ''}</p>
            <table className="data-table">
              <thead>
                <tr><th>طرف‌حساب</th><th>نوع</th><th>مشکل</th><th></th></tr>
              </thead>
              <tbody>
                {dashboard.needsReview.length === 0 && (
                  <tr><td colSpan={4}>در این نمونه مورد مبهمی نیست. این به معنی صفر بودن مطالبات نیست.</td></tr>
                )}
                {dashboard.needsReview.map(row => (
                  <tr key={row.partyId}>
                    <td>{row.displayName}</td>
                    <td>{row.category}</td>
                    <td>{row.issue}</td>
                    <td><button type="button" onClick={() => openParty(row.partyId)}>پرونده</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>

          <section>
            <h3>پیگیری‌های باز CRM</h3>
            <p>این فهرست سررسید مالی سپیدار نیست؛ وظیفه پیگیری ثبت‌شده در CRM است.</p>
            <ul>
              {dashboard.openFollowUps.map(item => (
                <li key={item.noteId}>
                  {item.partyName || 'بدون طرف‌حساب'} — {item.owner || 'بدون مسئول'} — {item.noteText}
                  {item.partyId && <button type="button" onClick={() => openParty(item.partyId!)} style={{ marginRight: '8px' }}>پرونده</button>}
                </li>
              ))}
              {dashboard.openFollowUps.length === 0 && <li>پیگیری بازی با این فیلتر نیست.</li>}
            </ul>
          </section>
        </>
      )}

      {ledger && (
        <section style={{ borderTop: '1px solid var(--border-color)', paddingTop: '12px' }}>
          <h3>پرونده {ledger.displayName}</h3>
          <p>نگاشت سپیدار: {ledger.mappingVerified ? `شناسه ${ledger.sepidarPartyRef}` : 'ندارد'}</p>
          {!ledger.mappingVerified && (
            <form onSubmit={e => { e.preventDefault(); linkSepidar(); }} style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', alignItems: 'end' }}>
              <label>کد تفصیلی سپیدار
                <input value={linkCode} onChange={e => setLinkCode(e.target.value)} placeholder="مثلاً 10749" style={{ display: 'block', marginTop: '4px' }} />
              </label>
              <button className="btn-primary" type="submit">تأیید اتصال</button>
            </form>
          )}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '8px' }}>
            <FigureCard figure={ledger.openingBalance} />
            <FigureCard figure={ledger.totalDebit} />
            <FigureCard figure={ledger.totalCredit} />
            <FigureCard figure={ledger.receipts} />
            <FigureCard figure={ledger.payments} />
            <FigureCard figure={ledger.closingBalance} />
          </div>
          <p><strong>{statusLabel(ledger.linesStatus)}.</strong> {ledger.linesReason}</p>
          <p>تعداد سطر سند: {ledger.totalLineCount == null ? 'نامشخص' : ledger.totalLineCount.toLocaleString('fa-IR')}</p>
          {ledger.lines.length > 0 && (
            <table className="data-table">
              <thead>
                <tr><th>تاریخ</th><th>شماره</th><th>شرح</th><th>بدهکار</th><th>بستانکار</th><th>مانده</th><th>مرجع سپیدار</th></tr>
              </thead>
              <tbody>
                {ledger.lines.map((line, index) => (
                  <tr key={`${line.sepidarReference}-${index}`}>
                    <td>{line.dateUtc ? new Date(line.dateUtc).toLocaleDateString('fa-IR') : '—'}</td>
                    <td>{line.documentNumber || '—'}</td>
                    <td>{line.description || '—'}</td>
                    <td>{line.debit ?? '—'}</td>
                    <td>{line.credit ?? '—'}</td>
                    <td>{line.runningBalance ?? '—'}</td>
                    <td>{line.sepidarReference || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <h4>مخاطبان CRM</h4>
          <ul>{ledger.contacts.map(c => <li key={c.id}>{c.personName} {c.position ? `(${c.position})` : ''}</li>)}</ul>
          <h4>تماس‌ها</h4>
          <ul>{ledger.calls.map(c => <li key={c.id}>{c.callerNumber} → {c.destinationNumber}</li>)}</ul>
          <h4>پیگیری‌ها</h4>
          <ul>{ledger.followUps.map(f => <li key={f.noteId}>{f.owner || 'بدون مسئول'}: {f.noteText}</li>)}</ul>
        </section>
      )}
    </section>
  );
};
