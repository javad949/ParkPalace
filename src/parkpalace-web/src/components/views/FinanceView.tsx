import React, { useEffect, useState } from 'react';
import {
  Receipt,
  FileCheck,
  RefreshCw,
  Search,
  Plus,
  Link2,
  ShieldCheck
} from 'lucide-react';
import {
  fetchSupplierInvoices,
  createSupplierInvoice,
  evaluateThreeWayMatch,
  fetchMatchExceptions,
  resolveMatchException,
  fetchSepidarCandidates,
  linkSepidarInvoice,
  unlinkSepidarInvoice,
  fetchSepidarReconciliationHistory,
  fetchSuppliers,
  fetchMaterials,
  fetchOrders
} from '../../services/api';
import {
  SupplierInvoiceDto,
  CreateSupplierInvoiceRequest,
  ThreeWayMatchResultDto,
  SepidarCandidateInvoiceDto,
  SepidarFinancialReconciliationDto,
  PartyDto,
  MaterialDto,
  PurchaseOrderDto
} from '../../types';
import { JalaliDatePicker } from '../JalaliDatePicker';
import { formatJalali, dateOnlyToIsoUtc } from '../../utils/jalali';

interface FinanceViewProps {
  activeDevRole?: string;
  onNavigateToSupplier?: (supplierId: string) => void;
}

export const FinanceView: React.FC<FinanceViewProps> = ({
  activeDevRole = 'Finance',
  onNavigateToSupplier: _onNavigateToSupplier
}) => {
  const [activeTab, setActiveTab] = useState<'invoices' | 'matching' | 'sepidar'>('invoices');

  // Invoices State
  const [invoices, setInvoices] = useState<SupplierInvoiceDto[]>([]);
  const [invoicesLoading, setInvoicesLoading] = useState(false);
  const [searchInvoices, setSearchInvoices] = useState('');

  // New Invoice Modal
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [suppliers, setSuppliers] = useState<PartyDto[]>([]);
  const [materials, setMaterials] = useState<MaterialDto[]>([]);
  const [orders, setOrders] = useState<PurchaseOrderDto[]>([]);
  const [createSubmitting, setCreateSubmitting] = useState(false);
  const [invoiceForm, setInvoiceForm] = useState<{
    supplierId: string;
    purchaseOrderId: string;
    invoiceNumber: string;
    invoiceDateUtc: string;
    currency: string;
    notes: string;
    lines: Array<{
      materialId: string;
      invoicedQuantity: number;
      unitPrice: number;
      taxAmount: number;
      discountAmount: number;
    }>;
  }>({
    supplierId: '',
    purchaseOrderId: '',
    invoiceNumber: '',
    invoiceDateUtc: new Date().toISOString().split('T')[0],
    currency: 'IRR',
    notes: '',
    lines: [{ materialId: '', invoicedQuantity: 100, unitPrice: 0, taxAmount: 0, discountAmount: 0 }]
  });

  // Matching State
  const [matchExceptions, setMatchExceptions] = useState<ThreeWayMatchResultDto[]>([]);
  const [matchingLoading, setMatchingLoading] = useState(false);
  const [activeMatch, setActiveMatch] = useState<ThreeWayMatchResultDto | null>(null);
  const [tolerance, setTolerance] = useState(1.0);
  const [resolvingMatch, setResolvingMatch] = useState<ThreeWayMatchResultDto | null>(null);
  const [resolutionAction, setResolutionAction] = useState('ApprovedWithVariance');
  const [resolutionNotes, setResolutionNotes] = useState('');
  const [resolveSubmitting, setResolveSubmitting] = useState(false);

  // Sepidar Reconciliation State
  const [reconciliationHistory, setReconciliationHistory] = useState<SepidarFinancialReconciliationDto[]>([]);
  const [candidatesLoading, setCandidatesLoading] = useState(false);
  const [candidates, setCandidates] = useState<SepidarCandidateInvoiceDto[]>([]);
  const [invoiceForCandidates, setInvoiceForCandidates] = useState<SupplierInvoiceDto | null>(null);
  const [unlinkItem, setUnlinkItem] = useState<SepidarFinancialReconciliationDto | null>(null);
  const [unlinkReason, setUnlinkReason] = useState('');
  const [unlinkSubmitting, setUnlinkSubmitting] = useState(false);

  // Alerts
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Load Data
  const loadInvoices = async () => {
    setInvoicesLoading(true);
    try {
      const data = await fetchSupplierInvoices(undefined, activeDevRole);
      setInvoices(data);
    } catch (err: any) {
      console.error('Failed to load invoices', err);
    } finally {
      setInvoicesLoading(false);
    }
  };

  const loadMatchingExceptions = async () => {
    setMatchingLoading(true);
    try {
      const data = await fetchMatchExceptions(activeDevRole);
      setMatchExceptions(data);
    } catch (err: any) {
      console.error('Failed to load match exceptions', err);
    } finally {
      setMatchingLoading(false);
    }
  };

  const loadSepidarHistory = async () => {
    try {
      const history = await fetchSepidarReconciliationHistory(activeDevRole);
      setReconciliationHistory(history);
    } catch (err) {
      console.error('Failed to load Sepidar history', err);
    }
  };

  const loadDropdowns = async () => {
    try {
      const [suppRes, matRes, orderRes] = await Promise.all([
        fetchSuppliers({ pageSize: 100 }, activeDevRole),
        fetchMaterials({ pageSize: 100 }, activeDevRole),
        fetchOrders({ pageSize: 100 }, activeDevRole)
      ]);
      setSuppliers(suppRes.items);
      setMaterials(matRes.items);
      setOrders(orderRes.items);
    } catch (err) {
      console.error('Failed to load dropdowns', err);
    }
  };

  useEffect(() => {
    loadInvoices();
    loadMatchingExceptions();
    loadSepidarHistory();
    loadDropdowns();
  }, [activeDevRole]);

  // Create Invoice Submit
  const handleCreateInvoice = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!invoiceForm.supplierId || !invoiceForm.invoiceNumber.trim()) {
      setErrorMessage('انتخاب تأمین‌کننده و ورود شماره فاکتور الزامی است');
      return;
    }
    if (invoiceForm.lines.some(l => !l.materialId || l.invoicedQuantity <= 0)) {
      setErrorMessage('کلیه ردیف‌های فاکتور باید دارای کالا و مقدار معتبر باشند');
      return;
    }

    setCreateSubmitting(true);
    setErrorMessage(null);
    try {
      const payload: CreateSupplierInvoiceRequest = {
        supplierId: invoiceForm.supplierId,
        purchaseOrderId: invoiceForm.purchaseOrderId || undefined,
        invoiceNumber: invoiceForm.invoiceNumber.trim(),
        invoiceDateUtc: dateOnlyToIsoUtc(invoiceForm.invoiceDateUtc)!,
        currency: invoiceForm.currency,
        notes: invoiceForm.notes,
        lines: invoiceForm.lines.map(l => ({
          materialId: l.materialId,
          invoicedQuantity: Number(l.invoicedQuantity),
          unitPrice: Number(l.unitPrice),
          taxAmount: Number(l.taxAmount),
          discountAmount: Number(l.discountAmount)
        }))
      };

      const created = await createSupplierInvoice(payload, activeDevRole);
      setSuccessMessage(`فاکتور شماره ${created.invoiceNumber} با موفقیت ثبت شد.`);
      setShowCreateModal(false);
      loadInvoices();
    } catch (err: any) {
      setErrorMessage(err.message || 'خطا در ثبت فاکتور تأمین‌کننده');
    } finally {
      setCreateSubmitting(false);
    }
  };

  // Evaluate 3-Way Match
  const handleEvaluateMatch = async (invoiceId: string) => {
    try {
      const match = await evaluateThreeWayMatch(invoiceId, tolerance, activeDevRole);
      setActiveMatch(match);
      setSuccessMessage(`ارزیابی تطبیق سه‌طرفه انجام شد. وضعیت نتیجه: ${match.matchStatus}`);
      loadMatchingExceptions();
    } catch (err: any) {
      alert(err.message || 'خطا در ارزیابی تطبیق سه‌طرفه');
    }
  };

  // Resolve Match Exception Submit
  const handleResolveSubmit = async () => {
    if (!resolvingMatch || !resolutionNotes.trim()) return;
    setResolveSubmitting(true);
    try {
      await resolveMatchException(resolvingMatch.id, {
        resolutionAction,
        resolutionNotes: resolutionNotes.trim()
      }, activeDevRole);
      setSuccessMessage('مغایرت با موفقیت تعیین تکلیف و تایید مالی گردید.');
      setResolvingMatch(null);
      setResolutionNotes('');
      loadMatchingExceptions();
    } catch (err: any) {
      alert(err.message || 'خطا در رفع مغایرت');
    } finally {
      setResolveSubmitting(false);
    }
  };

  // Fetch Sepidar Candidates
  const handleFetchCandidates = async (inv: SupplierInvoiceDto) => {
    setInvoiceForCandidates(inv);
    setCandidatesLoading(true);
    try {
      const candList = await fetchSepidarCandidates(inv.id, activeDevRole);
      setCandidates(candList);
    } catch (err: any) {
      alert(err.message || 'خطا در واکشی اسناد متناظر سپیدار');
    } finally {
      setCandidatesLoading(false);
    }
  };

  // Link Sepidar Invoice
  const handleLinkCandidate = async (candidate: SepidarCandidateInvoiceDto) => {
    if (!invoiceForCandidates) return;
    if (!confirm(`آیا از اتصال قطعی فاکتور ${invoiceForCandidates.invoiceNumber} به سند سپیدار شماره ${candidate.sepidarVoucherNumber} به مبلغ ${candidate.amount.toLocaleString('fa-IR')} ریال اطمینان دارید؟`)) return;

    try {
      await linkSepidarInvoice({
        supplierInvoiceId: invoiceForCandidates.id,
        sepidarInvoiceId: candidate.sepidarInvoiceId,
        sepidarVoucherNumber: candidate.sepidarVoucherNumber,
        sepidarVoucherDateUtc: candidate.voucherDateUtc,
        sepidarAmount: candidate.amount,
        auditNotes: `تطبیق و اتصال قطعی با ضریب اطمینان ${Math.round(candidate.confidenceScore * 100)}% توسط کاربر مالی`
      }, activeDevRole);
      setSuccessMessage('سند مالی سپیدار با موفقیت به فاکتور متصل شد.');
      setInvoiceForCandidates(null);
      setCandidates([]);
      loadInvoices();
      loadSepidarHistory();
    } catch (err: any) {
      alert(err.message || 'خطا در اتصال سند سپیدار');
    }
  };

  // Unlink Sepidar Invoice Submit
  const handleConfirmUnlink = async () => {
    if (!unlinkItem || !unlinkReason.trim()) return;
    setUnlinkSubmitting(true);
    try {
      await unlinkSepidarInvoice(unlinkItem.id, unlinkReason.trim(), activeDevRole);
      setSuccessMessage('اتصال سند سپیدار لغو و در سیستم ممیزی ثبت گردید.');
      setUnlinkItem(null);
      setUnlinkReason('');
      loadInvoices();
      loadSepidarHistory();
    } catch (err: any) {
      alert(err.message || 'خطا در لغو اتصال سند');
    } finally {
      setUnlinkSubmitting(false);
    }
  };

  // Helper Badge Renderers
  const renderMatchBadge = (status: string) => {
    switch (status) {
      case 'ExactMatch':
        return <span className="badge-status normal" style={{ background: '#059669', color: '#ecfdf5' }}>تطبیق کامل ۱۰۰٪</span>;
      case 'WithinTolerance':
        return <span className="badge-status normal" style={{ background: '#d97706', color: '#fffbeb' }}>در محدوده تلورانس</span>;
      case 'PriceMismatch':
        return <span className="badge-status normal" style={{ background: '#dc2626', color: '#fef2f2' }}>مغایرت نرخ فی</span>;
      case 'QuantityMismatch':
        return <span className="badge-status normal" style={{ background: '#dc2626', color: '#fef2f2' }}>مغایرت تعداد اقلام</span>;
      case 'MissingReceipt':
        return <span className="badge-status normal" style={{ background: '#b91c1c', color: '#fef2f2' }}>عدم ثبت رسید انبار</span>;
      case 'ApprovedWithVariance':
        return <span className="badge-status normal" style={{ background: '#0284c7', color: '#f0f9ff' }}>تایید با مصالحه</span>;
      default:
        return <span className="badge-status normal">{status}</span>;
    }
  };

  const renderConfidenceBadge = (level: string, score: number) => {
    const percent = Math.round(score * 100);
    if (level === 'High') {
      return <span className="badge-status normal" style={{ background: '#059669', color: '#ecfdf5' }}>تطبیق قوی ({percent}٪)</span>;
    }
    if (level === 'Medium') {
      return <span className="badge-status normal" style={{ background: '#d97706', color: '#fffbeb' }}>تطبیق متوسط ({percent}٪)</span>;
    }
    return <span className="badge-status normal" style={{ background: '#dc2626', color: '#fef2f2' }}>نیازمند بررسی دقیق ({percent}٪)</span>;
  };

  return (
    <div className="view-card">
      {/* Header */}
      <div className="view-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Receipt size={26} color="#0284c7" />
            <h2>امور مالی، تطبیق سه‌طرفه و تطبیق اسناد سپیدار (Finance & 3-Way Matching)</h2>
          </div>
          <p style={{ marginTop: '4px', color: 'var(--text-muted)' }}>
            ثبت فاکتورهای تأمین‌کنندگان، تطبیق سه‌طرفه سفارش + رسید کالا + صورتحساب، کنترل هوشمند تلورانس و تطبیق فقط‌خواندنی با اسناد مالی سپیدار
          </p>
        </div>

        <button
          className="btn-primary"
          onClick={() => {
            setInvoiceForm({
              supplierId: '',
              purchaseOrderId: '',
              invoiceNumber: '',
              invoiceDateUtc: new Date().toISOString().split('T')[0],
              currency: 'IRR',
              notes: '',
              lines: [{ materialId: '', invoicedQuantity: 100, unitPrice: 0, taxAmount: 0, discountAmount: 0 }]
            });
            setShowCreateModal(true);
          }}
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
        >
          <Plus size={16} />
          <span>ثبت فاکتور جدید تأمین‌کننده</span>
        </button>
      </div>

      {/* Success Banner */}
      {successMessage && (
        <div style={{
          background: 'rgba(16, 185, 129, 0.12)',
          border: '1px solid rgba(16, 185, 129, 0.3)',
          borderRadius: '8px',
          padding: '12px 16px',
          marginBottom: '16px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          color: '#10b981'
        }}>
          <span>{successMessage}</span>
          <button onClick={() => setSuccessMessage(null)} style={{ background: 'none', border: 'none', color: 'inherit', cursor: 'pointer' }}>×</button>
        </div>
      )}

      {/* Strict Read-Only Sepidar Guarantee Banner */}
      <div style={{
        background: 'rgba(2, 132, 199, 0.08)',
        border: '1px solid rgba(2, 132, 199, 0.25)',
        borderRadius: '10px',
        padding: '12px 16px',
        marginBottom: '20px',
        fontSize: '12px',
        display: 'flex',
        alignItems: 'center',
        gap: '12px'
      }}>
        <ShieldCheck size={24} color="#0284c7" style={{ flexShrink: 0 }} />
        <div>
          <strong style={{ color: '#0284c7' }}>اصل معماری یکپارچه‌سازی مالی سپیدار (Strict Read-Only Access):</strong>
          <p style={{ margin: '2px 0 0', color: 'var(--text-muted)' }}>
            واحد مالی ثبت رسمی اسناد حسابداری و فاکتورها را کماکان مستقیماً در نرم‌افزار سپیدار انجام می‌دهد. ماژول تطبیق CRM صرفاً با دسترسی SELECT اسناد را واکشی نموده و با پیشنهاد تطبیق هوشمند و تأیید قطعی حسابدار، آن‌ها را به اسناد CRM پیوند می‌دهد (بدون هیچ‌گونه عمل INSERT/UPDATE در سپیدار).
          </p>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', marginBottom: '20px', gap: '24px' }}>
        <button
          onClick={() => setActiveTab('invoices')}
          style={{
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'invoices' ? '2px solid #0284c7' : '2px solid transparent',
            color: activeTab === 'invoices' ? '#0284c7' : 'var(--text-muted)',
            fontWeight: activeTab === 'invoices' ? 600 : 400,
            padding: '10px 4px',
            cursor: 'pointer',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <Receipt size={18} />
          <span>فاکتورهای تأمین‌کنندگان ({invoices.length})</span>
        </button>

        <button
          onClick={() => setActiveTab('matching')}
          style={{
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'matching' ? '2px solid #0284c7' : '2px solid transparent',
            color: activeTab === 'matching' ? '#0284c7' : 'var(--text-muted)',
            fontWeight: activeTab === 'matching' ? 600 : 400,
            padding: '10px 4px',
            cursor: 'pointer',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <FileCheck size={18} />
          <span>موتور تطبیق سه‌طرفه و صف مغایرت‌ها ({matchExceptions.length})</span>
        </button>

        <button
          onClick={() => setActiveTab('sepidar')}
          style={{
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'sepidar' ? '2px solid #0284c7' : '2px solid transparent',
            color: activeTab === 'sepidar' ? '#0284c7' : 'var(--text-muted)',
            fontWeight: activeTab === 'sepidar' ? 600 : 400,
            padding: '10px 4px',
            cursor: 'pointer',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <Link2 size={18} />
          <span>تطبیق با اسناد سپیدار ({reconciliationHistory.length})</span>
        </button>
      </div>

      {/* TAB 1: INVOICES LIST */}
      {activeTab === 'invoices' && (
        <div>
          {/* Search bar */}
          <div style={{ display: 'flex', gap: '12px', marginBottom: '16px', alignItems: 'center' }}>
            <div style={{ position: 'relative', flex: 1 }}>
              <Search size={16} style={{ position: 'absolute', right: '12px', top: '12px', color: 'var(--text-muted)' }} />
              <input
                type="text"
                placeholder="جستجو بر اساس شماره فاکتور یا نام تأمین‌کننده..."
                value={searchInvoices}
                onChange={(e) => setSearchInvoices(e.target.value)}
                className="input-field"
                style={{ paddingRight: '36px', width: '100%' }}
              />
            </div>
            <button className="btn-secondary" onClick={loadInvoices} disabled={invoicesLoading}>
              <RefreshCw size={14} className={invoicesLoading ? 'animate-spin' : ''} />
            </button>
          </div>

          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>شماره فاکتور</th>
                  <th>تأمین‌کننده</th>
                  <th>شماره سفارش مرجع</th>
                  <th>تاریخ فاکتور</th>
                  <th>مبلغ فاکتور (ریال)</th>
                  <th>مالیات ارزش افزوده</th>
                  <th>وضعیت سپیدار</th>
                  <th>عملیات مالی</th>
                </tr>
              </thead>
              <tbody>
                {invoicesLoading ? (
                  <tr>
                    <td colSpan={8} style={{ textAlign: 'center', padding: '30px' }}>در حال بارگذاری فاکتورها...</td>
                  </tr>
                ) : invoices.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                      هیچ فاکتوری ثبت نشده است. از دکمه «ثبت فاکتور جدید تأمین‌کننده» استفاده نمایید.
                    </td>
                  </tr>
                ) : (
                  invoices.filter(i => !searchInvoices || i.invoiceNumber.toLowerCase().includes(searchInvoices.toLowerCase()) || i.supplierName.toLowerCase().includes(searchInvoices.toLowerCase())).map(inv => (
                    <tr key={inv.id}>
                      <td style={{ fontWeight: 600, color: '#0284c7' }}>{inv.invoiceNumber}</td>
                      <td>{inv.supplierName}</td>
                      <td>{inv.orderNumber || '—'}</td>
                      <td>{formatJalali(inv.invoiceDateUtc)}</td>
                      <td style={{ fontWeight: 600 }}>{inv.totalAmount.toLocaleString('fa-IR')}</td>
                      <td>{inv.taxTotal.toLocaleString('fa-IR')}</td>
                      <td>
                        {inv.registeredInSepidar ? (
                          <span className="badge-status normal" style={{ background: '#059669', color: '#ecfdf5' }}>
                            متصل به سپیدار ({inv.sepidarVoucherNumber || 'سند رسمی'})
                          </span>
                        ) : (
                          <span className="badge-status normal" style={{ background: '#64748b', color: '#f8fafc' }}>
                            در انتظار تطبیق
                          </span>
                        )}
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px' }}>
                          <button
                            className="btn-primary"
                            onClick={() => handleEvaluateMatch(inv.id)}
                            style={{ padding: '4px 10px', fontSize: '11px', background: '#0284c7' }}
                          >
                            تطبیق ۳ طرفه
                          </button>
                          <button
                            className="btn-secondary"
                            onClick={() => handleFetchCandidates(inv)}
                            style={{ padding: '4px 10px', fontSize: '11px' }}
                          >
                            اسناد سپیدار
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 2: THREE-WAY MATCHING */}
      {activeTab === 'matching' && (
        <div>
          {/* Tolerance Control Bar */}
          <div style={{
            background: 'var(--card-bg, #1e293b)',
            border: '1px solid var(--border)',
            borderRadius: '8px',
            padding: '14px 18px',
            marginBottom: '20px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: '12px'
          }}>
            <div>
              <strong style={{ fontSize: '13px' }}>کنترل آستانه تلورانس مغایرت (Tolerance Percentage):</strong>
              <p style={{ margin: '2px 0 0', fontSize: '12px', color: 'var(--text-muted)' }}>
                مغایرت‌های قیمتی کمتر از این سقف به عنوان "WithinTolerance" تلقی می‌گردند.
              </p>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <input
                type="number"
                min="0"
                max="10"
                step="0.1"
                value={tolerance}
                onChange={(e) => setTolerance(Number(e.target.value))}
                className="input-field"
                style={{ width: '80px', textAlign: 'center' }}
              />
              <span style={{ fontSize: '13px' }}>درصد (٪)</span>
            </div>
          </div>

          {/* Exceptions Table */}
          <h4 style={{ margin: '0 0 10px', fontSize: '14px' }}>صف مغایرت‌های نیازمند اقدام مالی (Exception Queue)</h4>
          <div className="table-container" style={{ marginBottom: '30px' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>شماره فاکتور</th>
                  <th>تأمین‌کننده</th>
                  <th>شماره سفارش</th>
                  <th>شماره رسید انبار</th>
                  <th>وضعیت تطبیق</th>
                  <th>مبلغ کل مغایرت</th>
                  <th>تاریخ ارزیابی</th>
                  <th>وضعیت رفع</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {matchingLoading ? (
                  <tr>
                    <td colSpan={9} style={{ textAlign: 'center', padding: '30px' }}>در حال بارگذاری صف مغایرت‌ها...</td>
                  </tr>
                ) : matchExceptions.length === 0 ? (
                  <tr>
                    <td colSpan={9} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                      هیچ مغایرت بازی در صف وجود ندارد. کلیه فاکتورها کاملاً تطبیق یافته‌اند.
                    </td>
                  </tr>
                ) : (
                  matchExceptions.map(ex => (
                    <tr key={ex.id}>
                      <td style={{ fontWeight: 600 }}>{ex.invoiceNumber}</td>
                      <td>{ex.supplierName}</td>
                      <td>{ex.orderNumber}</td>
                      <td>{ex.receiptNumber || 'فاقد رسید انبار'}</td>
                      <td>{renderMatchBadge(ex.matchStatus)}</td>
                      <td style={{ fontWeight: 600, color: ex.totalVarianceAmount > 0 ? '#ef4444' : 'inherit' }}>
                        {ex.totalVarianceAmount.toLocaleString('fa-IR')} ریال
                      </td>
                      <td>{formatJalali(ex.evaluatedAtUtc)}</td>
                      <td>
                        {ex.isResolved ? (
                          <span className="badge-status normal" style={{ background: '#059669', color: '#ecfdf5' }}>
                            رفع شده ({ex.resolutionAction})
                          </span>
                        ) : (
                          <span className="badge-status normal" style={{ background: '#dc2626', color: '#fef2f2' }}>
                            نیازمند تصمیم مالی
                          </span>
                        )}
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px' }}>
                          <button
                            className="btn-secondary"
                            onClick={() => setActiveMatch(ex)}
                            style={{ padding: '4px 8px', fontSize: '11px' }}
                          >
                            مشاهده ریز
                          </button>
                          {!ex.isResolved && (
                            <button
                              className="btn-primary"
                              onClick={() => { setResolvingMatch(ex); setResolutionNotes(''); }}
                              style={{ padding: '4px 8px', fontSize: '11px', background: '#d97706' }}
                            >
                              تعیین تکلیف
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Active Match Breakdown Drawer */}
          {activeMatch && (
            <div style={{
              background: 'var(--card-bg, #1e293b)',
              border: '1px solid var(--border)',
              borderRadius: '10px',
              padding: '20px',
              marginTop: '20px'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                <div>
                  <h4 style={{ margin: 0, color: '#0284c7' }}>
                    ریز اقلام تطبیق سه‌طرفه: فاکتور {activeMatch.invoiceNumber} با سفارش {activeMatch.orderNumber}
                  </h4>
                  <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                    وضعیت کلی: {renderMatchBadge(activeMatch.matchStatus)} | کل مغایرت: {activeMatch.totalVarianceAmount.toLocaleString('fa-IR')} ریال
                  </span>
                </div>
                <button
                  className="btn-secondary"
                  onClick={() => setActiveMatch(null)}
                  style={{ padding: '4px 8px', fontSize: '12px' }}
                >
                  بستن ریز اقلام
                </button>
              </div>

              <div className="table-container">
                <table className="data-table" style={{ fontSize: '12px' }}>
                  <thead>
                    <tr>
                      <th>ماده اولیه</th>
                      <th>تعداد سفارش</th>
                      <th>تعداد رسید انبار</th>
                      <th>تعداد فاکتور</th>
                      <th>مغایرت تعداد</th>
                      <th>نرخ سفارش (ریال)</th>
                      <th>نرخ فاکتور (ریال)</th>
                      <th>درصد مغایرت نرخ</th>
                      <th>مبلغ کل مغایرت</th>
                      <th>نوع عدم انطباق</th>
                    </tr>
                  </thead>
                  <tbody>
                    {activeMatch.lineVariances.map(lv => (
                      <tr key={lv.id}>
                        <td>{lv.materialName} ({lv.materialCode})</td>
                        <td>{lv.poQuantity.toLocaleString('fa-IR')}</td>
                        <td>{lv.receivedQuantity.toLocaleString('fa-IR')}</td>
                        <td>{lv.invoicedQuantity.toLocaleString('fa-IR')}</td>
                        <td style={{ fontWeight: 600, color: lv.quantityVariance !== 0 ? '#ef4444' : '#10b981' }}>
                          {lv.quantityVariance.toLocaleString('fa-IR')}
                        </td>
                        <td>{lv.poUnitPrice.toLocaleString('fa-IR')}</td>
                        <td>{lv.invoicedUnitPrice.toLocaleString('fa-IR')}</td>
                        <td style={{ fontWeight: 600, color: lv.priceVariancePercent > 0 ? '#ef4444' : '#10b981' }}>
                          {lv.priceVariancePercent}%
                        </td>
                        <td style={{ fontWeight: 600 }}>{lv.totalVariance.toLocaleString('fa-IR')}</td>
                        <td>{renderMatchBadge(lv.mismatchType)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

      {/* TAB 3: SEPIDAR FINANCIAL RECONCILIATION */}
      {activeTab === 'sepidar' && (
        <div>
          {/* Candidates Drawer if selected */}
          {invoiceForCandidates && (
            <div style={{
              background: 'rgba(2, 132, 199, 0.05)',
              border: '1px solid #0284c7',
              borderRadius: '10px',
              padding: '20px',
              marginBottom: '24px'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                <div>
                  <h4 style={{ margin: 0, color: '#0284c7' }}>
                    اسناد مالی متناظر کشف‌شده در سپیدار برای فاکتور {invoiceForCandidates.invoiceNumber}
                  </h4>
                  <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                    تأمین‌کننده: {invoiceForCandidates.supplierName} | مبلغ فاکتور CRM: {invoiceForCandidates.totalAmount.toLocaleString('fa-IR')} ریال
                  </span>
                </div>
                <button
                  className="btn-secondary"
                  onClick={() => setInvoiceForCandidates(null)}
                  style={{ padding: '4px 8px', fontSize: '12px' }}
                >
                  انصراف
                </button>
              </div>

              {candidatesLoading ? (
                <div style={{ textAlign: 'center', padding: '20px' }}>در حال فراخوانی خواندنی اسناد مالی از سپیدار...</div>
              ) : candidates.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '20px', color: 'var(--text-muted)' }}>
                  هیچ سند ثبت‌شده‌ای در بازه زمانی و مبلغ این فاکتور در سپیدار یافت نشد. لطفاً اطمینان حاصل کنید سند در سپیدار ثبت شده باشد.
                </div>
              ) : (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '16px' }}>
                  {candidates.map((cand, idx) => (
                    <div key={idx} style={{
                      background: 'var(--card-bg, #1e293b)',
                      border: '1px solid var(--border)',
                      borderRadius: '8px',
                      padding: '16px',
                      display: 'flex',
                      flexDirection: 'column',
                      justifyContent: 'space-between'
                    }}>
                      <div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                          <strong style={{ fontSize: '14px' }}>سند سپیدار: {cand.sepidarVoucherNumber}</strong>
                          {renderConfidenceBadge(cand.matchConfidenceLevel, cand.confidenceScore)}
                        </div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', lineHeight: 1.6 }}>
                          <div>مبلغ سند: <strong>{cand.amount.toLocaleString('fa-IR')} ریال</strong></div>
                          <div>تاریخ سند: {formatJalali(cand.voucherDateUtc)}</div>
                          <div>نام طرف‌حساب: {cand.supplierName}</div>
                          <div style={{ marginTop: '6px', color: '#0284c7' }}>علت تطبیق: {cand.matchReason}</div>
                        </div>
                      </div>

                      <button
                        className="btn-primary"
                        onClick={() => handleLinkCandidate(cand)}
                        style={{ marginTop: '14px', width: '100%', fontSize: '12px', background: '#059669' }}
                      >
                        تأیید و اتصال قطعی به این سند سپیدار
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* History Table */}
          <h4 style={{ margin: '0 0 10px', fontSize: '14px' }}>تاریخچه تطبیق و اتصال اسناد مالی سپیدار (Reconciliation History & Audit)</h4>
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>شماره فاکتور CRM</th>
                  <th>تأمین‌کننده</th>
                  <th>شماره سند در سپیدار</th>
                  <th>تاریخ سند سپیدار</th>
                  <th>مبلغ سند سپیدار</th>
                  <th>ضریب تطبیق</th>
                  <th>وضعیت اتصال</th>
                  <th>کاربر متصل‌کننده</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {reconciliationHistory.length === 0 ? (
                  <tr>
                    <td colSpan={9} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                      هنوز هیچ سندی به اسناد رسمی سپیدار متصل نشده است.
                    </td>
                  </tr>
                ) : (
                  reconciliationHistory.map(h => (
                    <tr key={h.id}>
                      <td style={{ fontWeight: 600 }}>{h.invoiceNumber}</td>
                      <td>{h.supplierName}</td>
                      <td style={{ color: '#0284c7', fontWeight: 600 }}>{h.sepidarVoucherNumber}</td>
                      <td>{formatJalali(h.sepidarVoucherDateUtc)}</td>
                      <td>{h.sepidarAmount.toLocaleString('fa-IR')} ریال</td>
                      <td>{Math.round(h.candidateConfidenceScore * 100)}٪</td>
                      <td>
                        {h.linkStatus === 'Confirmed' || h.linkStatus === 'LinkedManually' ? (
                          <span className="badge-status normal" style={{ background: '#059669', color: '#ecfdf5' }}>متصل شده</span>
                        ) : (
                          <span className="badge-status normal" style={{ background: '#dc2626', color: '#fef2f2' }}>لغو اتصال (Unlinked)</span>
                        )}
                      </td>
                      <td>{h.linkedBy || 'سیستم'}</td>
                      <td>
                        {h.linkStatus !== 'Unlinked' && (
                          <button
                            className="btn-secondary"
                            onClick={() => { setUnlinkItem(h); setUnlinkReason(''); }}
                            style={{ padding: '4px 8px', fontSize: '11px', color: '#ef4444', borderColor: '#ef4444' }}
                          >
                            لغو اتصال
                          </button>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* RESOLVE EXCEPTION MODAL */}
      {resolvingMatch && (
        <div className="modal-overlay" style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: 'var(--card-bg, #1e293b)',
            borderRadius: '12px',
            width: '100%',
            maxWidth: '520px',
            padding: '24px',
            border: '1px solid var(--border)'
          }}>
            <h3 style={{ margin: '0 0 12px', color: '#d97706' }}>
              رفع و تعیین تکلیف مغایرت تطبیق سه‌طرفه
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              فاکتور: {resolvingMatch.invoiceNumber} | سفارش: {resolvingMatch.orderNumber} | مبلغ مغایرت: {resolvingMatch.totalVarianceAmount.toLocaleString('fa-IR')} ریال
            </p>

            <div style={{ marginTop: '16px', marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>اقدام مالی مصوب *</label>
              <select
                className="input-field"
                value={resolutionAction}
                onChange={(e) => setResolutionAction(e.target.value)}
                style={{ width: '100%' }}
              >
                <option value="ApprovedWithVariance">تایید و پرداخت با مصالحه مابه‌التفاوت (Approved With Variance)</option>
                <option value="CreditNoteRequested">درخواست صدور اعلامیه بستانکاری از تأمین‌کننده (Credit Note)</option>
                <option value="InvoiceRejected">رد فاکتور و درخواست صدور فاکتور اصلاحی (Invoice Rejected)</option>
                <option value="Corrected">اصلاح اطلاعات ثبت شده در سیستم (Corrected)</option>
              </select>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>صورتجلسه و توضیحات ممیزی مالی *</label>
              <textarea
                className="input-field"
                rows={3}
                placeholder="توضیحات مربوط به مصالحه یا علت تایید مغایرت را وارد نمایید..."
                value={resolutionNotes}
                onChange={(e) => setResolutionNotes(e.target.value)}
                required
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                className="btn-secondary"
                onClick={() => setResolvingMatch(null)}
                disabled={resolveSubmitting}
              >
                انصراف
              </button>
              <button
                className="btn-primary"
                onClick={handleResolveSubmit}
                disabled={resolveSubmitting || !resolutionNotes.trim()}
                style={{ background: '#059669', borderColor: '#059669' }}
              >
                {resolveSubmitting ? 'در حال ثبت...' : 'تأیید و اعمال تصمیم مالی'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* UNLINK RECONCILIATION MODAL */}
      {unlinkItem && (
        <div className="modal-overlay" style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: 'var(--card-bg, #1e293b)',
            borderRadius: '12px',
            width: '100%',
            maxWidth: '500px',
            padding: '24px',
            border: '1px solid var(--border)'
          }}>
            <h3 style={{ margin: '0 0 12px', color: '#ef4444' }}>
              لغو اتصال سند سپیدار {unlinkItem.sepidarVoucherNumber}
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              فاکتور CRM: {unlinkItem.invoiceNumber} | لغو اتصال موجب آزاد شدن فاکتور جهت تطبیق مجدد می‌گردد. ثبت علت لغو در سوابق ممیزی الزامی است.
            </p>

            <div style={{ marginTop: '16px', marginBottom: '20px' }}>
              <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>علت لغو اتصال *</label>
              <textarea
                className="input-field"
                rows={3}
                placeholder="علت لغو اتصال (مثلاً ابطال سند در سپیدار یا اشتباه کاربر) را وارد نمایید..."
                value={unlinkReason}
                onChange={(e) => setUnlinkReason(e.target.value)}
                required
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                className="btn-secondary"
                onClick={() => setUnlinkItem(null)}
                disabled={unlinkSubmitting}
              >
                انصراف
              </button>
              <button
                className="btn-primary"
                onClick={handleConfirmUnlink}
                disabled={unlinkSubmitting || !unlinkReason.trim()}
                style={{ background: '#ef4444', borderColor: '#ef4444' }}
              >
                {unlinkSubmitting ? 'در حال ثبت...' : 'تأیید و لغو اتصال'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* CREATE INVOICE MODAL */}
      {showCreateModal && (
        <div className="modal-overlay" style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: 'var(--card-bg, #1e293b)',
            borderRadius: '12px',
            width: '100%',
            maxWidth: '850px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            border: '1px solid var(--border)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', borderBottom: '1px solid var(--border)', paddingBottom: '12px' }}>
              <div>
                <h3 style={{ margin: 0, fontSize: '18px', color: '#0284c7' }}>ثبت فاکتور جدید تأمین‌کننده (Supplier Invoice)</h3>
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>مجهز به الگوریتم کشف و رد فاکتورهای تکراری (Duplicate Invoice Prevention)</span>
              </div>
              <button
                onClick={() => setShowCreateModal(false)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', fontSize: '20px', cursor: 'pointer' }}
              >
                ×
              </button>
            </div>

            {errorMessage && (
              <div style={{ background: 'rgba(239, 68, 68, 0.15)', border: '1px solid rgba(239, 68, 68, 0.3)', color: '#ef4444', padding: '10px 14px', borderRadius: '8px', marginBottom: '16px', fontSize: '13px' }}>
                {errorMessage}
              </div>
            )}

            <form onSubmit={handleCreateInvoice}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '16px', marginBottom: '20px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>تأمین‌کننده *</label>
                  <select
                    className="input-field"
                    value={invoiceForm.supplierId}
                    onChange={(e) => setInvoiceForm({ ...invoiceForm, supplierId: e.target.value })}
                    required
                    style={{ width: '100%' }}
                  >
                    <option value="">-- انتخاب تأمین‌کننده --</option>
                    {suppliers.map(s => (
                      <option key={s.id} value={s.id}>{s.displayName}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>شماره فاکتور تأمین‌کننده *</label>
                  <input
                    type="text"
                    className="input-field"
                    placeholder="مثال: INV-1403-908"
                    value={invoiceForm.invoiceNumber}
                    onChange={(e) => setInvoiceForm({ ...invoiceForm, invoiceNumber: e.target.value })}
                    required
                    style={{ width: '100%' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>سفارش خرید مرتبط (اختیاری)</label>
                  <select
                    className="input-field"
                    value={invoiceForm.purchaseOrderId}
                    onChange={(e) => setInvoiceForm({ ...invoiceForm, purchaseOrderId: e.target.value })}
                    style={{ width: '100%' }}
                  >
                    <option value="">-- فاقد سفارش خرید مستقیم --</option>
                    {orders.map(o => (
                      <option key={o.id} value={o.id}>{o.orderNumber} ({o.supplierName})</option>
                    ))}
                  </select>
                </div>

                <JalaliDatePicker
                  label="تاریخ فاکتور"
                  value={invoiceForm.invoiceDateUtc}
                  onChange={(val) => setInvoiceForm({ ...invoiceForm, invoiceDateUtc: val })}
                  required
                />
              </div>

              {/* Lines Table */}
              <div style={{ marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                  <h4 style={{ margin: 0, fontSize: '14px' }}>اقلام و مبالغ صورتحساب</h4>
                  <button
                    type="button"
                    className="btn-secondary"
                    onClick={() => setInvoiceForm(prev => ({
                      ...prev,
                      lines: [...prev.lines, { materialId: '', invoicedQuantity: 10, unitPrice: 0, taxAmount: 0, discountAmount: 0 }]
                    }))}
                    style={{ padding: '4px 10px', fontSize: '12px' }}
                  >
                    + افزودن ردیف
                  </button>
                </div>

                <div className="table-container" style={{ maxHeight: '220px' }}>
                  <table className="data-table" style={{ fontSize: '12px' }}>
                    <thead>
                      <tr>
                        <th style={{ width: '35%' }}>ماده اولیه</th>
                        <th style={{ width: '15%' }}>مقدار فاکتور شده</th>
                        <th style={{ width: '25%' }}>قیمت واحد (ریال)</th>
                        <th style={{ width: '25%' }}>مالیات ارزش افزوده (ریال)</th>
                      </tr>
                    </thead>
                    <tbody>
                      {invoiceForm.lines.map((l, idx) => (
                        <tr key={idx}>
                          <td>
                            <select
                              className="input-field"
                              value={l.materialId}
                              onChange={(e) => {
                                const updated = [...invoiceForm.lines];
                                updated[idx].materialId = e.target.value;
                                setInvoiceForm({ ...invoiceForm, lines: updated });
                              }}
                              required
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            >
                              <option value="">-- انتخاب کالا --</option>
                              {materials.map(m => (
                                <option key={m.id} value={m.id}>{m.name} ({m.internalCode})</option>
                              ))}
                            </select>
                          </td>
                          <td>
                            <input
                              type="number"
                              min="0.01"
                              step="any"
                              className="input-field"
                              value={l.invoicedQuantity}
                              onChange={(e) => {
                                const updated = [...invoiceForm.lines];
                                updated[idx].invoicedQuantity = Number(e.target.value);
                                setInvoiceForm({ ...invoiceForm, lines: updated });
                              }}
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              min="0"
                              step="any"
                              className="input-field"
                              value={l.unitPrice}
                              onChange={(e) => {
                                const updated = [...invoiceForm.lines];
                                updated[idx].unitPrice = Number(e.target.value);
                                setInvoiceForm({ ...invoiceForm, lines: updated });
                              }}
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              min="0"
                              step="any"
                              className="input-field"
                              value={l.taxAmount}
                              onChange={(e) => {
                                const updated = [...invoiceForm.lines];
                                updated[idx].taxAmount = Number(e.target.value);
                                setInvoiceForm({ ...invoiceForm, lines: updated });
                              }}
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Notes */}
              <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px' }}>یادداشت و ملاحظات مالی</label>
                <textarea
                  className="input-field"
                  rows={2}
                  value={invoiceForm.notes}
                  onChange={(e) => setInvoiceForm({ ...invoiceForm, notes: e.target.value })}
                  style={{ width: '100%' }}
                />
              </div>

              {/* Submit */}
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setShowCreateModal(false)}
                  disabled={createSubmitting}
                >
                  انصراف
                </button>
                <button
                  type="submit"
                  className="btn-primary"
                  disabled={createSubmitting}
                >
                  {createSubmitting ? 'در حال ثبت...' : 'ثبت فاکتور تأمین‌کننده'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
