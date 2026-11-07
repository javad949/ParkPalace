import React, { useEffect, useState } from 'react';
import {
  RefreshCw,
  Search,
  Plus,
  ShoppingCart,
  FileText,
  Clock,
  ShieldCheck,
  Send,
  Trash2,
  AlertTriangle,
  Eye
} from 'lucide-react';
import { 
  fetchRecommendations, 
  fetchOrders, 
  fetchOrder, 
  createOrder, 
  issueOrder, 
  cancelOrder,
  fetchMaterials,
  fetchSuppliers
} from '../../services/api';
import { 
  PurchaseRecommendationDto, 
  PurchaseOrderDto, 
  CreatePurchaseOrderRequest,
  MaterialDto,
  PartyDto
} from '../../types';
import { JalaliDatePicker } from '../JalaliDatePicker';
import { formatJalali, formatJalaliDateTime, dateOnlyToIsoUtc } from '../../utils/jalali';
import { consumeDrillForView, drillBannerLabel } from '../../utils/drillNavigation';

interface OrdersViewProps {
  activeDevRole?: string;
  onNavigateToMaterial?: (materialId: string) => void;
  onNavigateToSupplier?: (supplierId: string) => void;
}

type OrdersListFilter = 'all' | 'overdue' | 'active';

export const OrdersView: React.FC<OrdersViewProps> = ({
  activeDevRole = 'Procurement',
  onNavigateToMaterial: _onNavigateToMaterial,
  onNavigateToSupplier
}) => {
  const [activeTab, setActiveTab] = useState<'orders' | 'recommendations'>('orders');

  // Orders State
  const [orders, setOrders] = useState<PurchaseOrderDto[]>([]);
  const [ordersLoading, setOrdersLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState('');
  const [listFilter, setListFilter] = useState<OrdersListFilter>('all');
  const [drillNote, setDrillNote] = useState<string | null>(null);
  const [searchOrders, setSearchOrders] = useState('');
  const [selectedOrder, setSelectedOrder] = useState<PurchaseOrderDto | null>(null);

  // Recommendations State
  const [recommendations, setRecommendations] = useState<PurchaseRecommendationDto[]>([]);
  const [recLoading, setRecLoading] = useState(false);
  const [searchRec, setSearchRec] = useState('');
  const [onlyActionable, setOnlyActionable] = useState(false);

  // New PO Modal State
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [materialsList, setMaterialsList] = useState<MaterialDto[]>([]);
  const [suppliersList, setSuppliersList] = useState<PartyDto[]>([]);
  const [formData, setFormData] = useState<{
    supplierId: string;
    expectedDeliveryDateUtc: string;
    paymentTerms: string;
    deliveryTerms: string;
    notes: string;
    directIssue: boolean;
    manualReviewConfirmed: boolean;
    hasUnverifiedReserve: boolean;
    lines: Array<{
      materialId: string;
      orderedQuantity: number;
      unitPrice: number;
      taxRate: number;
      discountAmount: number;
    }>;
  }>({
    supplierId: '',
    expectedDeliveryDateUtc: '',
    paymentTerms: 'نقدی ۳۰ روزه',
    deliveryTerms: 'تحویل در انبار مرکزی تهران',
    notes: '',
    directIssue: true, // Procurement direct PO issuance without manager approval
    manualReviewConfirmed: false,
    hasUnverifiedReserve: false,
    lines: [
      { materialId: '', orderedQuantity: 100, unitPrice: 0, taxRate: 0.10, discountAmount: 0 }
    ]
  });
  const [createSubmitting, setCreateSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Cancel Modal State
  const [orderToCancel, setOrderToCancel] = useState<PurchaseOrderDto | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelSubmitting, setCancelSubmitting] = useState(false);

  // Load Data
  const loadOrders = async () => {
    setOrdersLoading(true);
    try {
      if (listFilter === 'overdue' || listFilter === 'active') {
        const [issued, partial] = await Promise.all([
          fetchOrders({ status: 'Issued', pageSize: 100 }, activeDevRole),
          fetchOrders({ status: 'PartiallyReceived', pageSize: 100 }, activeDevRole)
        ]);
        let items = [...issued.items, ...partial.items];
        if (listFilter === 'overdue') {
          const now = Date.now();
          items = items.filter((o) =>
            !!o.expectedDeliveryDateUtc && new Date(o.expectedDeliveryDateUtc).getTime() < now
          );
        }
        setOrders(items);
      } else {
        const res = await fetchOrders({ status: statusFilter || undefined, pageSize: 50 }, activeDevRole);
        setOrders(res.items);
      }
    } catch (err: any) {
      console.error('Failed to load orders', err);
    } finally {
      setOrdersLoading(false);
    }
  };

  const loadRecommendations = async () => {
    setRecLoading(true);
    try {
      const data = await fetchRecommendations(activeDevRole);
      setRecommendations(data);
    } catch (err) {
      console.error('Failed to load purchase recommendations', err);
    } finally {
      setRecLoading(false);
    }
  };

  const loadDependencies = async () => {
    try {
      const [matsRes, suppRes] = await Promise.all([
        fetchMaterials({ pageSize: 100 }, activeDevRole),
        fetchSuppliers({ pageSize: 100 }, activeDevRole)
      ]);
      setMaterialsList(matsRes.items);
      setSuppliersList(suppRes.items);
    } catch (err) {
      console.error('Failed to load materials/suppliers', err);
    }
  };

  useEffect(() => {
    const drill = consumeDrillForView('orders');
    if (drill.filter === 'overdue' || drill.filter === 'active') {
      setListFilter(drill.filter);
      setStatusFilter('');
      setActiveTab('orders');
      setDrillNote(drillBannerLabel(drill));
    }
  }, []);

  useEffect(() => {
    loadOrders();
    loadRecommendations();
    loadDependencies();
  }, [activeDevRole, statusFilter, listFilter]);

  // Open Order Details
  const handleOpenDetails = async (orderId: string) => {
    try {
      const detailed = await fetchOrder(orderId, activeDevRole);
      setSelectedOrder(detailed);
    } catch (err: any) {
      alert(err.message || 'خطا در بارگذاری جزئیات سفارش');
    }
  };

  // Issue Order
  const handleIssueOrder = async (orderId: string) => {
    if (!confirm('آیا از صدور قطعی این سفارش خرید توسط واحد تدارکات اطمینان دارید؟')) return;
    try {
      await issueOrder(orderId, activeDevRole);
      setSuccessMessage('سفارش خرید با موفقیت صادر گردید و اسنپ‌شات اقلام قفل شد.');
      loadOrders();
      if (selectedOrder?.id === orderId) {
        handleOpenDetails(orderId);
      }
    } catch (err: any) {
      alert(err.message || 'خطا در صدور سفارش');
    }
  };

  // Cancel Order
  const handleConfirmCancel = async () => {
    if (!orderToCancel || !cancelReason.trim()) return;
    setCancelSubmitting(true);
    try {
      await cancelOrder(orderToCancel.id, {
        reason: cancelReason,
        concurrencyToken: orderToCancel.concurrencyToken
      }, activeDevRole);
      setSuccessMessage('سفارش خرید با موفقیت ابطال گردید.');
      setOrderToCancel(null);
      setCancelReason('');
      loadOrders();
    } catch (err: any) {
      alert(err.message || 'خطا در ابطال سفارش');
    } finally {
      setCancelSubmitting(false);
    }
  };

  // Open Create PO Pre-filled from Recommendation
  const handleCreateFromRecommendation = (rec: PurchaseRecommendationDto) => {
    const isUnverified = rec.requiresManualReview || !rec.isDefinitive;
    setFormData({
      supplierId: rec.preferredSupplierId || '',
      expectedDeliveryDateUtc: '',
      paymentTerms: 'نقدی ۳۰ روزه',
      deliveryTerms: 'تحویل در انبار مرکزی تهران',
      notes: isUnverified 
        ? `[بررسی دستی الزامی است: وضعیت رزرو در سپیدار نامعلوم است] پیشنهاد خرید دوره‌ای (MOQ: ${rec.minOrderQuantity} ${rec.baseUnit})`
        : `صادر شده خودکار بر اساس پیشنهاد هوشمند خرید دوره ای (MOQ: ${rec.minOrderQuantity} ${rec.baseUnit})`,
      directIssue: !isUnverified, // BLOCKED from automatic direct issuance if unverified reserve!
      manualReviewConfirmed: false,
      hasUnverifiedReserve: isUnverified,
      lines: [
        {
          materialId: rec.materialId,
          orderedQuantity: rec.suggestedPurchaseQuantity > 0 ? rec.suggestedPurchaseQuantity : rec.minOrderQuantity,
          unitPrice: rec.preferredSupplierPrice || 0,
          taxRate: 0.10,
          discountAmount: 0
        }
      ]
    });
    setActiveTab('orders');
    setShowCreateModal(true);
  };

  // Add Line
  const handleAddLine = () => {
    setFormData(prev => ({
      ...prev,
      lines: [...prev.lines, { materialId: '', orderedQuantity: 10, unitPrice: 0, taxRate: 0.10, discountAmount: 0 }]
    }));
  };

  // Remove Line
  const handleRemoveLine = (idx: number) => {
    if (formData.lines.length <= 1) return;
    setFormData(prev => ({
      ...prev,
      lines: prev.lines.filter((_, i) => i !== idx)
    }));
  };

  // Update Line
  const handleUpdateLine = (idx: number, field: string, value: any) => {
    setFormData(prev => {
      const updated = [...prev.lines];
      updated[idx] = { ...updated[idx], [field]: value };
      return { ...prev, lines: updated };
    });
  };

  // Submit Create PO
  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.supplierId) {
      setErrorMessage('انتخاب تأمین‌کننده الزامی است');
      return;
    }
    if (formData.lines.some(l => !l.materialId || l.orderedQuantity <= 0)) {
      setErrorMessage('کلیه ردیف‌ها باید دارای ماده اولیه معتبر و مقدار بیشتر از صفر باشند');
      return;
    }

    setCreateSubmitting(true);
    setErrorMessage(null);
    try {
      const payload: CreatePurchaseOrderRequest = {
        supplierId: formData.supplierId,
        expectedDeliveryDateUtc: dateOnlyToIsoUtc(formData.expectedDeliveryDateUtc),
        paymentTerms: formData.paymentTerms,
        deliveryTerms: formData.deliveryTerms,
        notes: formData.notes,
        directIssue: formData.directIssue,
        manualReviewConfirmed: formData.manualReviewConfirmed,
        lines: formData.lines.map(l => ({
          materialId: l.materialId,
          orderedQuantity: Number(l.orderedQuantity),
          unitPrice: Number(l.unitPrice),
          taxRate: Number(l.taxRate),
          discountAmount: Number(l.discountAmount)
        }))
      };

      const created = await createOrder(payload, activeDevRole);
      setSuccessMessage(
        formData.directIssue 
          ? `سفارش شماره ${created.orderNumber} با موفقیت توسط تدارکات صادر گردید (بدون نیاز به تایید مدیر).`
          : `پیش‌نویس سفارش شماره ${created.orderNumber} با موفقیت ثبت شد.`
      );
      setShowCreateModal(false);
      loadOrders();
    } catch (err: any) {
      setErrorMessage(err.message || 'خطا در ثبت سفارش خرید');
    } finally {
      setCreateSubmitting(false);
    }
  };

  // Status Badge Helper
  const renderStatusBadge = (status: string) => {
    switch (status) {
      case 'Draft':
        return <span className="badge-status normal" style={{ background: '#475569', color: '#f8fafc' }}>پیش‌نویس</span>;
      case 'Issued':
        return <span className="badge-status normal" style={{ background: '#0284c7', color: '#f0f9ff' }}>صادر شده (تدارکات)</span>;
      case 'PartiallyReceived':
        return <span className="badge-status normal" style={{ background: '#d97706', color: '#fffbeb' }}>دریافت جزئی</span>;
      case 'Received':
        return <span className="badge-status normal" style={{ background: '#059669', color: '#ecfdf5' }}>کامل دریافت شد</span>;
      case 'Completed':
        return <span className="badge-status normal" style={{ background: '#16a34a', color: '#f0fdf4' }}>مختومه و تسویه</span>;
      case 'Cancelled':
        return <span className="badge-status normal" style={{ background: '#dc2626', color: '#fef2f2' }}>ابطال شده</span>;
      default:
        return <span className="badge-status normal">{status}</span>;
    }
  };

  // Filtered Orders
  const filteredOrders = orders.filter(o => {
    if (searchOrders) {
      const q = searchOrders.toLowerCase();
      return (
        o.orderNumber.toLowerCase().includes(q) ||
        o.supplierName.toLowerCase().includes(q) ||
        (o.notes && o.notes.toLowerCase().includes(q))
      );
    }
    return true;
  });

  // Filtered Recommendations
  const filteredRecs = recommendations.filter(r => {
    if (onlyActionable && r.suggestedPurchaseQuantity <= 0) return false;
    if (searchRec) {
      const q = searchRec.toLowerCase();
      return (
        r.materialName.toLowerCase().includes(q) ||
        r.materialCode.toLowerCase().includes(q) ||
        (r.preferredSupplierName && r.preferredSupplierName.toLowerCase().includes(q))
      );
    }
    return true;
  });

  return (
    <div className="view-card">
      {/* Top Header */}
      <div className="view-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <ShoppingCart size={26} color="#0284c7" />
            <h2>مدیریت سفارشات خرید و تدارکات مستقیم (Procurement Orders)</h2>
          </div>
          <p style={{ marginTop: '4px', color: 'var(--text-muted)' }}>
            صدور مستقیم سفارش خرید توسط واحد تدارکات بدون نیاز به تأیید مدیر (SRS FR-05)، قفل اسنپ‌شات اقلام و قیمت‌ها، و اتصال به پیشنهادات خرید روزانه
          </p>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            className="btn-primary"
            onClick={() => {
              setFormData({
                supplierId: '',
                expectedDeliveryDateUtc: '',
                paymentTerms: 'نقدی ۳۰ روزه',
                deliveryTerms: 'تحویل در انبار مرکزی تهران',
                notes: '',
                directIssue: true,
                manualReviewConfirmed: false,
                hasUnverifiedReserve: false,
                lines: [{ materialId: '', orderedQuantity: 100, unitPrice: 0, taxRate: 0.10, discountAmount: 0 }]
              });
              setShowCreateModal(true);
            }}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          >
            <Plus size={16} />
            <span>صدور سفارش مستقیم جدید</span>
          </button>
        </div>
      </div>

      {/* Success Notification */}
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

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', marginBottom: '20px', gap: '24px' }}>
        <button
          onClick={() => setActiveTab('orders')}
          style={{
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'orders' ? '2px solid #0284c7' : '2px solid transparent',
            color: activeTab === 'orders' ? '#0284c7' : 'var(--text-muted)',
            fontWeight: activeTab === 'orders' ? 600 : 400,
            padding: '10px 4px',
            cursor: 'pointer',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <FileText size={18} />
          <span>سفارشات خرید صادر شده ({orders.length})</span>
        </button>

        <button
          onClick={() => setActiveTab('recommendations')}
          style={{
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'recommendations' ? '2px solid #0284c7' : '2px solid transparent',
            color: activeTab === 'recommendations' ? '#0284c7' : 'var(--text-muted)',
            fontWeight: activeTab === 'recommendations' ? 600 : 400,
            padding: '10px 4px',
            cursor: 'pointer',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <Clock size={18} />
          <span>پیشنهادات هوشمند خرید روزانه ({recommendations.filter(r => r.suggestedPurchaseQuantity > 0).length})</span>
        </button>
      </div>

      {/* TAB 1: ORDERS LIST */}
      {activeTab === 'orders' && (
        <div>
          {drillNote && (
            <div role="status" style={{
              marginBottom: 12,
              padding: '10px 12px',
              borderRadius: 10,
              border: '1px solid rgba(56, 189, 248, 0.35)',
              background: 'rgba(14, 165, 233, 0.08)',
              color: '#7dd3fc',
              fontSize: '0.85rem',
              display: 'flex',
              justifyContent: 'space-between',
              gap: 12,
              alignItems: 'center',
              flexWrap: 'wrap'
            }}>
              <span>{drillNote} — {orders.length} ردیف</span>
              <button
                type="button"
                className="btn-secondary"
                style={{ fontSize: '0.75rem', padding: '4px 10px' }}
                onClick={() => { setListFilter('all'); setDrillNote(null); }}
              >
                پاک کردن فیلتر داشبورد
              </button>
            </div>
          )}
          {/* Filters Bar */}
          <div style={{ display: 'flex', gap: '12px', marginBottom: '16px', flexWrap: 'wrap', alignItems: 'center' }}>
            <div style={{ position: 'relative', flex: 1, minWidth: '240px' }}>
              <Search size={16} style={{ position: 'absolute', right: '12px', top: '12px', color: 'var(--text-muted)' }} />
              <input
                type="text"
                placeholder="جستجو بر اساس شماره سفارش، نام تأمین‌کننده یا توضیحات..."
                value={searchOrders}
                onChange={(e) => setSearchOrders(e.target.value)}
                className="input-field"
                style={{ paddingRight: '36px', width: '100%' }}
              />
            </div>

            <select
              value={statusFilter}
              onChange={(e) => {
                setListFilter('all');
                setDrillNote(null);
                setStatusFilter(e.target.value);
              }}
              className="input-field"
              style={{ width: '180px' }}
              disabled={listFilter !== 'all'}
            >
              <option value="">همه وضعیت‌ها</option>
              <option value="Draft">پیش‌نویس</option>
              <option value="Issued">صادر شده</option>
              <option value="PartiallyReceived">دریافت جزئی</option>
              <option value="Received">کامل دریافت شد</option>
              <option value="Completed">مختومه</option>
              <option value="Cancelled">ابطال شده</option>
            </select>

            <button
              className="btn-secondary"
              onClick={loadOrders}
              disabled={ordersLoading}
              style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <RefreshCw size={14} className={ordersLoading ? 'animate-spin' : ''} />
              <span>بروزرسانی</span>
            </button>
          </div>

          {/* Orders Table */}
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>شماره سفارش</th>
                  <th>تأمین‌کننده</th>
                  <th>وضعیت</th>
                  <th>تاریخ صدور</th>
                  <th>تعداد اقلام</th>
                  <th>مبلغ کل (ریال)</th>
                  <th>صادرکننده</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {ordersLoading ? (
                  <tr>
                    <td colSpan={8} style={{ textAlign: 'center', padding: '30px' }}>در حال بارگذاری سفارشات...</td>
                  </tr>
                ) : filteredOrders.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                      هیچ سفارشی یافت نشد. با کلیک بر روی "صدور سفارش مستقیم جدید"، اولین سفارش را صادر کنید.
                    </td>
                  </tr>
                ) : (
                  filteredOrders.map(order => (
                    <tr key={order.id}>
                      <td style={{ fontWeight: 600, color: '#0284c7' }}>{order.orderNumber}</td>
                      <td>
                        <button
                          onClick={() => onNavigateToSupplier?.(order.supplierId)}
                          style={{ background: 'none', border: 'none', color: 'inherit', textDecoration: 'underline', cursor: 'pointer', textAlign: 'right' }}
                        >
                          {order.supplierName}
                        </button>
                      </td>
                      <td>{renderStatusBadge(order.status)}</td>
                      <td>{formatJalali(order.issuedAtUtc)}</td>
                      <td>{order.lineCount} ردیف</td>
                      <td style={{ fontWeight: 600 }}>{order.grandTotal.toLocaleString('fa-IR')}</td>
                      <td>{order.issuedByUsername || order.createdBy}</td>
                      <td>
                        <div style={{ display: 'flex', gap: '8px' }}>
                          <button
                            className="btn-secondary"
                            onClick={() => handleOpenDetails(order.id)}
                            style={{ padding: '4px 10px', fontSize: '12px' }}
                          >
                            جزئیات و اسنپ‌شات
                          </button>

                          {order.status === 'Draft' && (
                            <button
                              className="btn-primary"
                              onClick={() => handleIssueOrder(order.id)}
                              style={{ padding: '4px 10px', fontSize: '12px', background: '#0284c7' }}
                            >
                              صدور قطعی
                            </button>
                          )}

                          {order.status !== 'Cancelled' && order.status !== 'Completed' && (
                            <button
                              className="btn-secondary"
                              onClick={() => setOrderToCancel(order)}
                              style={{ padding: '4px 10px', fontSize: '12px', color: '#ef4444', borderColor: '#ef4444' }}
                            >
                              ابطال
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
        </div>
      )}

      {/* TAB 2: RECOMMENDATIONS */}
      {activeTab === 'recommendations' && (
        <div>
          <div style={{
            background: 'rgba(2, 132, 199, 0.08)',
            border: '1px solid rgba(2, 132, 199, 0.25)',
            borderRadius: '10px',
            padding: '14px 18px',
            marginBottom: '16px',
            fontSize: '12px',
            lineHeight: 1.7
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600, color: '#0284c7' }}>
              <ShieldCheck size={16} />
              <span>تسهیل صدور سفارش خرید: تبدیل با یک کلیک از پیشنهاد روزانه به سفارش مستقیم تدارکات</span>
            </div>
            <p style={{ marginTop: '4px', color: 'var(--text-muted)' }}>
              با کلیک روی گزینه «صدور سفارش مستقیم» در کنار هر قلم، فرم ثبت سفارش با اطلاعات ماده اولیه، تأمین‌کننده مرجع و مقدار بهینه‌سازی شده پیش‌فرض تکمیل می‌گردد.
            </p>
          </div>

          {/* Rec Filter */}
          <div style={{ display: 'flex', gap: '12px', marginBottom: '16px', flexWrap: 'wrap', alignItems: 'center' }}>
            <div style={{ position: 'relative', flex: 1, minWidth: '240px' }}>
              <Search size={16} style={{ position: 'absolute', right: '12px', top: '12px', color: 'var(--text-muted)' }} />
              <input
                type="text"
                placeholder="جستجو در پیشنهادات بر اساس نام ماده اولیه یا تأمین‌کننده..."
                value={searchRec}
                onChange={(e) => setSearchRec(e.target.value)}
                className="input-field"
                style={{ paddingRight: '36px', width: '100%' }}
              />
            </div>

            <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '13px' }}>
              <input
                type="checkbox"
                checked={onlyActionable}
                onChange={(e) => setOnlyActionable(e.target.checked)}
              />
              <span>فقط اقلام دارای نیاز سفارش خرید ({recommendations.filter(r => r.suggestedPurchaseQuantity > 0).length})</span>
            </label>

            <button
              className="btn-secondary"
              onClick={loadRecommendations}
              disabled={recLoading}
              style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <RefreshCw size={14} className={recLoading ? 'animate-spin' : ''} />
              <span>محاسبه مجدد</span>
            </button>
          </div>

          {/* Recommendations Table */}
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>کد ماده</th>
                  <th>شرح ماده اولیه</th>
                  <th>وضعیت قطعیت</th>
                  <th>موجودی آزاد</th>
                  <th>مصرف روزانه</th>
                  <th>نقطه سفارش (s)</th>
                  <th>سقف انبار (S)</th>
                  <th>پیشنهاد خرید</th>
                  <th>تأمین‌کننده مرجع</th>
                  <th>عملیات صدور</th>
                </tr>
              </thead>
              <tbody>
                {recLoading ? (
                  <tr>
                    <td colSpan={10} style={{ textAlign: 'center', padding: '30px' }}>در حال محاسبه الگوریتم دوره‌ای R, s, S...</td>
                  </tr>
                ) : filteredRecs.length === 0 ? (
                  <tr>
                    <td colSpan={10} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                      هیچ موردی مطابق فیلتر یافت نشد.
                    </td>
                  </tr>
                ) : (
                  filteredRecs.map(r => {
                    const isUnverified = r.requiresManualReview || !r.isDefinitive;
                    return (
                      <tr key={r.id} style={{ background: r.suggestedPurchaseQuantity > 0 ? 'rgba(2, 132, 199, 0.03)' : undefined }}>
                        <td style={{ fontWeight: 600 }}>{r.materialCode}</td>
                        <td>
                          <div>{r.materialName}</div>
                          {r.dataWarning && (
                            <div style={{ fontSize: '11px', color: '#ea580c', marginTop: '2px', display: 'flex', alignItems: 'center', gap: '3px' }}>
                              <AlertTriangle size={11} />
                              <span>{r.dataWarning}</span>
                            </div>
                          )}
                        </td>
                        <td>
                          {isUnverified ? (
                            <span style={{
                              background: 'rgba(234, 88, 12, 0.1)',
                              color: '#c2410c',
                              border: '1px solid rgba(234, 88, 12, 0.3)',
                              padding: '2px 8px',
                              borderRadius: '4px',
                              fontSize: '11px',
                              fontWeight: 600,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '4px'
                            }}>
                              <AlertTriangle size={12} />
                              <span>غیرقطعی (بررسی دستی)</span>
                            </span>
                          ) : (
                            <span style={{
                              background: 'rgba(22, 163, 74, 0.1)',
                              color: '#16a34a',
                              border: '1px solid rgba(22, 163, 74, 0.3)',
                              padding: '2px 8px',
                              borderRadius: '4px',
                              fontSize: '11px',
                              fontWeight: 600
                            }}>
                              قطعی
                            </span>
                          )}
                        </td>
                        <td>
                          {isUnverified || r.reservableOnHand === null ? (
                            <div>
                              <span style={{ color: '#ea580c', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                                <AlertTriangle size={12} />
                                <span>نامعلوم (استعلام دستی)</span>
                              </span>
                              <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                فیزیکی: {r.usableOnHand.toLocaleString('fa-IR')} {r.baseUnit}
                              </div>
                            </div>
                          ) : (
                            <span>{r.usableOnHand.toLocaleString('fa-IR')} {r.baseUnit}</span>
                          )}
                        </td>
                        <td>{r.averageDailyConsumption.toLocaleString('fa-IR')} {r.baseUnit}</td>
                        <td>{r.safetyStock.toLocaleString('fa-IR')}</td>
                        <td>{r.targetStock.toLocaleString('fa-IR')}</td>
                        <td style={{ fontWeight: 700, color: r.suggestedPurchaseQuantity > 0 ? '#0284c7' : 'var(--text-muted)' }}>
                          {r.suggestedPurchaseQuantity > 0 ? `${r.suggestedPurchaseQuantity.toLocaleString('fa-IR')} ${r.baseUnit}` : 'بی‌نیاز'}
                        </td>
                        <td>{r.preferredSupplierName || 'نامشخص'}</td>
                        <td>
                          {isUnverified ? (
                            <button
                              className="btn-secondary"
                              onClick={() => handleCreateFromRecommendation(r)}
                              style={{
                                padding: '4px 10px',
                                fontSize: '11px',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px',
                                borderColor: '#ea580c',
                                color: '#c2410c',
                                background: 'rgba(234, 88, 12, 0.06)'
                              }}
                              title="صدور خودکار مسدود است؛ صدور صرفاً پس از بررسی دستی مجاز است"
                            >
                              <Eye size={12} />
                              <span>بررسی و صدور دستی</span>
                            </button>
                          ) : (
                            <button
                              className="btn-primary"
                              onClick={() => handleCreateFromRecommendation(r)}
                              style={{ padding: '4px 12px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}
                            >
                              <Send size={12} />
                              <span>صدور سفارش</span>
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* CREATE ORDER MODAL */}
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
                <h3 style={{ margin: 0, fontSize: '18px', color: '#0284c7' }}>صدور مستقیم سفارش خرید توسط تدارکات (Direct Purchase Order)</h3>
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>مطابق استاندارد FR-05 بدون نیاز به فرآیند تأیید مدیر</span>
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

            <form onSubmit={handleCreateSubmit}>
              {/* Header Fields */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '16px', marginBottom: '20px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>تأمین‌کننده *</label>
                  <select
                    className="input-field"
                    value={formData.supplierId}
                    onChange={(e) => setFormData({ ...formData, supplierId: e.target.value })}
                    required
                    style={{ width: '100%' }}
                  >
                    <option value="">-- انتخاب تأمین‌کننده --</option>
                    {suppliersList.map(s => (
                      <option key={s.id} value={s.id}>{s.displayName}</option>
                    ))}
                  </select>
                </div>

                <JalaliDatePicker
                  label="تاریخ تحویل مورد انتظار"
                  value={formData.expectedDeliveryDateUtc}
                  onChange={(val) => setFormData({ ...formData, expectedDeliveryDateUtc: val })}
                  placeholder="انتخاب تاریخ تحویل شمسی..."
                />

                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>شرایط پرداخت</label>
                  <input
                    type="text"
                    className="input-field"
                    value={formData.paymentTerms}
                    onChange={(e) => setFormData({ ...formData, paymentTerms: e.target.value })}
                    style={{ width: '100%' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>شرایط تحویل</label>
                  <input
                    type="text"
                    className="input-field"
                    value={formData.deliveryTerms}
                    onChange={(e) => setFormData({ ...formData, deliveryTerms: e.target.value })}
                    style={{ width: '100%' }}
                  />
                </div>
              </div>

              {/* Unverified Reserve Manual Review Warning Banner */}
              {formData.hasUnverifiedReserve && (
                <div style={{
                  background: 'rgba(234, 88, 12, 0.08)',
                  border: '1px solid rgba(234, 88, 12, 0.35)',
                  padding: '12px 16px',
                  borderRadius: '8px',
                  marginBottom: '16px'
                }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#ea580c', fontWeight: 700, fontSize: '13px', marginBottom: '4px' }}>
                    <AlertTriangle size={16} />
                    <span>هشدار کنترل بازرگانی: وضعیت رزرو نامعلوم در سپیدار</span>
                  </div>
                  <p style={{ margin: '0 0 10px', fontSize: '12px', color: 'var(--text-muted)', lineHeight: '1.6' }}>
                    اقلام این سفارش دارای وضعیت رزرو نامعلوم در سپیدار هستند و پیشنهاد خرید غیرقطعی اعلام شده است. صدور مستقیم خودکار مسدود گردیده و سفارش به‌صورت پیش‌نویس (جهت بررسی کارشناسی) ثبت خواهد شد، مگر آنکه کارشناس خرید پس از تطبیق دستی کاردکس، تعهد کالاها را بررسی کرده و صدور مستقیم را صریحاً تأیید نماید.
                  </p>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '12px', fontWeight: 600, color: '#c2410c' }}>
                    <input
                      type="checkbox"
                      checked={formData.manualReviewConfirmed}
                      onChange={(e) => {
                        const checked = e.target.checked;
                        setFormData({
                          ...formData,
                          manualReviewConfirmed: checked,
                          directIssue: checked
                        });
                      }}
                    />
                    <span>اینجانب بررسی دستی کاردکس و موجودی آزاد را در سپیدار انجام داده و صدور مستقیم را تأیید می‌نمایم.</span>
                  </label>
                </div>
              )}

              {/* Direct Issue Authorization Toggle */}
              <div style={{
                background: 'rgba(2, 132, 199, 0.08)',
                border: '1px solid rgba(2, 132, 199, 0.25)',
                padding: '12px 16px',
                borderRadius: '8px',
                marginBottom: '20px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between'
              }}>
                <div>
                  <strong style={{ color: '#0284c7', fontSize: '13px' }}>صدور مستقیم و بدون واسطه (Direct PO Issuance):</strong>
                  <p style={{ margin: '2px 0 0', fontSize: '12px', color: 'var(--text-muted)' }}>
                    با انتخاب این گزینه، وضعیت سفارش مستقیماً «Issued» شده و اسنپ‌شات مشخصات و قیمت‌ها قفل می‌گردد.
                  </p>
                  {formData.hasUnverifiedReserve && !formData.manualReviewConfirmed && (
                    <p style={{ margin: '4px 0 0', fontSize: '11px', color: '#ea580c', fontWeight: 600 }}>
                      ⚠️ به دلیل وضعیت رزرو نامعلوم مسدود است — ابتدا تأیید بررسی دستی در کادر بالا الزامی است.
                    </p>
                  )}
                </div>
                <label style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  cursor: (formData.hasUnverifiedReserve && !formData.manualReviewConfirmed) ? 'not-allowed' : 'pointer',
                  fontWeight: 600,
                  opacity: (formData.hasUnverifiedReserve && !formData.manualReviewConfirmed) ? 0.6 : 1
                }}>
                  <input
                    type="checkbox"
                    disabled={formData.hasUnverifiedReserve && !formData.manualReviewConfirmed}
                    checked={formData.directIssue}
                    onChange={(e) => setFormData({ ...formData, directIssue: e.target.checked })}
                  />
                  <span>صدور آنی توسط تدارکات</span>
                </label>
              </div>

              {/* Lines Table */}
              <div style={{ marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                  <h4 style={{ margin: 0, fontSize: '14px' }}>اقلام سفارش خرید</h4>
                  <button
                    type="button"
                    className="btn-secondary"
                    onClick={handleAddLine}
                    style={{ padding: '4px 10px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}
                  >
                    <Plus size={14} />
                    <span>افزودن ردیف</span>
                  </button>
                </div>

                <div className="table-container" style={{ maxHeight: '250px' }}>
                  <table className="data-table" style={{ fontSize: '12px' }}>
                    <thead>
                      <tr>
                        <th style={{ width: '35%' }}>ماده اولیه</th>
                        <th style={{ width: '15%' }}>مقدار سفارش</th>
                        <th style={{ width: '20%' }}>قیمت واحد (ریال)</th>
                        <th style={{ width: '15%' }}>نرخ مالیات</th>
                        <th style={{ width: '15%' }}>عملیات</th>
                      </tr>
                    </thead>
                    <tbody>
                      {formData.lines.map((line, idx) => (
                        <tr key={idx}>
                          <td>
                            <select
                              className="input-field"
                              value={line.materialId}
                              onChange={(e) => handleUpdateLine(idx, 'materialId', e.target.value)}
                              required
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            >
                              <option value="">-- انتخاب کالا --</option>
                              {materialsList.map(m => (
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
                              value={line.orderedQuantity}
                              onChange={(e) => handleUpdateLine(idx, 'orderedQuantity', Number(e.target.value))}
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              min="0"
                              step="any"
                              className="input-field"
                              value={line.unitPrice}
                              onChange={(e) => handleUpdateLine(idx, 'unitPrice', Number(e.target.value))}
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            />
                          </td>
                          <td>
                            <select
                              className="input-field"
                              value={line.taxRate}
                              onChange={(e) => handleUpdateLine(idx, 'taxRate', Number(e.target.value))}
                              style={{ width: '100%', fontSize: '12px', padding: '4px 8px' }}
                            >
                              <option value={0.10}>۱۰٪ مالیات ارزش افزوده</option>
                              <option value={0.0}>معاف از مالیات (۰٪)</option>
                            </select>
                          </td>
                          <td>
                            <button
                              type="button"
                              onClick={() => handleRemoveLine(idx)}
                              disabled={formData.lines.length <= 1}
                              style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer' }}
                            >
                              <Trash2 size={16} />
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Notes */}
              <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px' }}>توضیحات و دستورالعمل‌های تحویل</label>
                <textarea
                  className="input-field"
                  rows={2}
                  value={formData.notes}
                  onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                  style={{ width: '100%' }}
                />
              </div>

              {/* Actions */}
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
                  {createSubmitting ? 'در حال ثبت...' : formData.directIssue ? 'تأیید و صدور مستقیم سفارش' : 'ذخیره به عنوان پیش‌نویس'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* CANCEL MODAL */}
      {orderToCancel && (
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
            <h3 style={{ margin: '0 0 12px', color: '#ef4444' }}>ابطال سفارش خرید {orderToCancel.orderNumber}</h3>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)', lineHeight: 1.6 }}>
              توجه: مطابق قوانین سیستم، سفارشاتی که دارای رسید قطعی انبار یا فاکتور ثبت‌شده باشند قابل ابطال نیستند. ثبت علت ابطال در تاریخچه ثبت تغییرات (Audit History) الزامی است.
            </p>

            <div style={{ marginTop: '16px', marginBottom: '20px' }}>
              <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>علت ابطال سفارش *</label>
              <textarea
                className="input-field"
                rows={3}
                placeholder="علت ابطال سفارش را با ذکر جزئیات وارد نمایید..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                required
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                className="btn-secondary"
                onClick={() => { setOrderToCancel(null); setCancelReason(''); }}
                disabled={cancelSubmitting}
              >
                انصراف
              </button>
              <button
                className="btn-primary"
                onClick={handleConfirmCancel}
                disabled={cancelSubmitting || !cancelReason.trim()}
                style={{ background: '#ef4444', borderColor: '#ef4444' }}
              >
                {cancelSubmitting ? 'در حال ابطال...' : 'تأیید و ابطال سفارش'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* DETAILS & SNAPSHOTS DRAWER / MODAL */}
      {selectedOrder && (
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
            maxWidth: '900px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            border: '1px solid var(--border)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid var(--border)', paddingBottom: '12px' }}>
              <div>
                <h3 style={{ margin: 0, fontSize: '18px', color: '#0284c7' }}>
                  جزئیات و اسنپ‌شات سفارش {selectedOrder.orderNumber}
                </h3>
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                  تأمین‌کننده: {selectedOrder.supplierName} | وضعیت: {renderStatusBadge(selectedOrder.status)}
                </span>
              </div>
              <button
                onClick={() => setSelectedOrder(null)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', fontSize: '20px', cursor: 'pointer' }}
              >
                ×
              </button>
            </div>

            {/* Snapshot Guarantee Alert */}
            <div style={{
              background: 'rgba(16, 185, 129, 0.08)',
              border: '1px solid rgba(16, 185, 129, 0.25)',
              borderRadius: '8px',
              padding: '12px 16px',
              marginBottom: '20px',
              fontSize: '12px',
              display: 'flex',
              alignItems: 'center',
              gap: '10px'
            }}>
              <ShieldCheck size={20} color="#10b981" />
              <div>
                <strong style={{ color: '#10b981' }}>تضمین جامعیت و اسنپ‌شات تغییرناپذیر (Immutable Snapshots):</strong>
                <p style={{ margin: '2px 0 0', color: 'var(--text-muted)' }}>
                  با صدور این سفارش، نام تأمین‌کننده، شرح اقلام، کد کالا، نرخ واحد، تخفیف و مالیات در لحظه صدور فریز شده و هرگونه تغییر بعدی در کاتالوگ هیچ تأثیری بر این سفارش نخواهد داشت.
                </p>
              </div>
            </div>

            {/* Lines with Snapshots Table */}
            <h4 style={{ margin: '0 0 10px', fontSize: '14px' }}>اقلام سفارش و مقادیر اسنپ‌شات شده</h4>
            <div className="table-container" style={{ marginBottom: '20px' }}>
              <table className="data-table" style={{ fontSize: '12px' }}>
                <thead>
                  <tr>
                    <th>ردیف</th>
                    <th>نام کالا (اسنپ‌شات)</th>
                    <th>مقدار سفارش</th>
                    <th>واحد</th>
                    <th>قیمت واحد (ریال)</th>
                    <th>مالیات</th>
                    <th>مبلغ ردیف</th>
                    <th>دریافت شده</th>
                    <th>فاکتور شده</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedOrder.lines.map(line => (
                    <tr key={line.id}>
                      <td>{line.lineNumber}</td>
                      <td>
                        <strong>{line.materialNameSnapshot || line.materialName}</strong>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>کد: {line.materialCodeSnapshot || line.materialCode}</div>
                      </td>
                      <td>{line.orderedQuantity.toLocaleString('fa-IR')}</td>
                      <td>{line.unitNameSnapshot || line.unitName}</td>
                      <td>{line.unitPrice.toLocaleString('fa-IR')}</td>
                      <td>{line.taxAmount.toLocaleString('fa-IR')}</td>
                      <td style={{ fontWeight: 600 }}>{line.lineTotal.toLocaleString('fa-IR')}</td>
                      <td style={{ color: line.receivedQuantity >= line.orderedQuantity ? '#10b981' : '#f59e0b', fontWeight: 600 }}>
                        {line.receivedQuantity.toLocaleString('fa-IR')}
                      </td>
                      <td>{line.invoicedQuantity.toLocaleString('fa-IR')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Audits History */}
            <h4 style={{ margin: '0 0 10px', fontSize: '14px' }}>تاریخچه رویدادها و ممیزی (Audit Trail)</h4>
            <div className="table-container" style={{ maxHeight: '200px' }}>
              <table className="data-table" style={{ fontSize: '12px' }}>
                <thead>
                  <tr>
                    <th>زمان (UTC)</th>
                    <th>اقدام</th>
                    <th>کاربر انجام‌دهنده</th>
                    <th>جزئیات</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedOrder.audits.length === 0 ? (
                    <tr>
                      <td colSpan={4} style={{ textAlign: 'center', color: 'var(--text-muted)' }}>رویدادی ثبت نشده است.</td>
                    </tr>
                  ) : (
                    selectedOrder.audits.map(audit => (
                      <tr key={audit.id}>
                        <td>{formatJalaliDateTime(audit.timestampUtc)}</td>
                        <td style={{ fontWeight: 600, color: '#0284c7' }}>{audit.action}</td>
                        <td>{audit.performedBy}</td>
                        <td>{audit.detailsJson || '—'}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '20px' }}>
              <button className="btn-secondary" onClick={() => setSelectedOrder(null)}>بستن</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
