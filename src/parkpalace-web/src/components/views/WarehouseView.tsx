import React, { useEffect, useState } from 'react';
import {
  RefreshCw,
  Search,
  ExternalLink,
  ChevronLeft,
  ChevronRight,
  ArrowDownLeft,
  ArrowUpRight,
  TrendingUp,
  Settings,
  Truck,
  FileText,
  X,
  Save,
  ShieldCheck,
  Activity,
  Plus,
  PackageCheck,
  AlertCircle
} from 'lucide-react';
import {
  fetchWarehouse,
  fetchInventoryItems,
  fetchInventoryItemDetail,
  fetchInventoryMovements,
  fetchInventoryConsumption,
  fetchSyncStatus,
  triggerInventorySync,
  updateReplenishmentPolicy,
  fetchCategories,
  fetchGoodsReceipts,
  createGoodsReceipt,
  fetchOrders
} from '../../services/api';
import {
  WarehouseDto,
  InventoryItemDto,
  InventoryItemDetailDto,
  InventoryMovementDto,
  ConsumptionSummaryDto,
  SyncStatusDto,
  CategoryDto,
  UpdateReplenishmentPolicyRequest,
  GoodsReceiptDto,
  CreateGoodsReceiptRequest,
  PurchaseOrderDto
} from '../../types';

interface WarehouseViewProps {
  activeDevRole?: string;
  onNavigateToMaterial?: (materialId: string) => void;
  onNavigateToSupplier?: (supplierId: string) => void;
}

export const WarehouseView: React.FC<WarehouseViewProps> = ({
  activeDevRole = 'Admin',
  onNavigateToMaterial,
  onNavigateToSupplier
}) => {
  // State
  const [warehouse, setWarehouse] = useState<WarehouseDto | null>(null);
  const [syncStatus, setSyncStatus] = useState<SyncStatusDto | null>(null);
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 15;

  // Filters
  const [search, setSearch] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');
  const [onlyCritical, setOnlyCritical] = useState(false);

  // Detail Modal / Drawer
  const [selectedItem, setSelectedItem] = useState<InventoryItemDetailDto | null>(null);
  const [movements, setMovements] = useState<InventoryMovementDto[]>([]);
  const [consumption, setConsumption] = useState<ConsumptionSummaryDto | null>(null);
  const [activeTab, setActiveTab] = useState<'kardex' | 'consumption' | 'policy' | 'suppliers'>('kardex');

  // Policy Form State
  const [policyForm, setPolicyForm] = useState<UpdateReplenishmentPolicyRequest>({
    reviewPeriodDays: 7,
    leadTimeDays: 3,
    safetyStock: 0,
    minOrderQuantity: 0,
    packageMultiple: 1,
    notes: ''
  });
  const [savingPolicy, setSavingPolicy] = useState(false);
  const [policySuccessMsg, setPolicySuccessMsg] = useState('');

  // Sync Action State
  const [syncing, setSyncing] = useState(false);
  const [syncMessage, setSyncMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Phase 5: Goods Receipts Tab State
  const [mainTab, setMainTab] = useState<'inventory' | 'receipts'>('inventory');
  const [receipts, setReceipts] = useState<GoodsReceiptDto[]>([]);
  const [receiptsLoading, setReceiptsLoading] = useState(false);
  const [showReceiptModal, setShowReceiptModal] = useState(false);
  const [issuedOrders, setIssuedOrders] = useState<PurchaseOrderDto[]>([]);
  const [selectedPo, setSelectedPo] = useState<PurchaseOrderDto | null>(null);
  const [receiptForm, setReceiptForm] = useState<{
    purchaseOrderId: string;
    carrierName: string;
    deliveryNoteNumber: string;
    vehiclePlateNumber: string;
    notes: string;
    lines: Array<{
      purchaseOrderLineId: string;
      materialId: string;
      materialName: string;
      orderedQuantity: number;
      alreadyReceivedQuantity: number;
      deliveredQuantity: number;
      acceptedQuantity: number;
      rejectedQuantity: number;
      qcStatus: string;
      rejectionReason: string;
      storageLocation: string;
    }>;
  }>({
    purchaseOrderId: '',
    carrierName: '',
    deliveryNoteNumber: '',
    vehiclePlateNumber: '',
    notes: '',
    lines: []
  });
  const [receiptSubmitting, setReceiptSubmitting] = useState(false);
  const [receiptErrorMessage, setReceiptErrorMessage] = useState<string | null>(null);
  const [receiptSuccessMessage, setReceiptSuccessMessage] = useState<string | null>(null);

  // Check role authorization for sync (Admin, Procurement)
  const canSync = ['Admin', 'Procurement'].includes(activeDevRole);
  const canEditPolicy = ['Admin', 'Procurement'].includes(activeDevRole);

  // Load Warehouse & Status
  const loadOverview = async () => {
    try {
      const [wh, st] = await Promise.all([
        fetchWarehouse(activeDevRole),
        fetchSyncStatus(activeDevRole)
      ]);
      setWarehouse(wh);
      setSyncStatus(st);
    } catch (err) {
      console.error('Failed to load warehouse or sync status', err);
    }
  };

  // Load Items
  const loadItems = async () => {
    setLoading(true);
    try {
      const res = await fetchInventoryItems(page, pageSize, selectedCategory || undefined, search || undefined, activeDevRole);
      let filtered = res.items;
      if (onlyCritical) {
        filtered = filtered.filter(i => i.stockStatus === 'Critical' || i.stockStatus === 'OutOfStock');
      }
      setItems(filtered);
      setTotalCount(res.totalCount);
    } catch (err) {
      console.error('Failed to load inventory items', err);
    } finally {
      setLoading(false);
    }
  };

  // Load Categories for filter
  useEffect(() => {
    fetchCategories(activeDevRole).then(setCategories).catch(console.error);
    loadOverview();
  }, [activeDevRole]);

  useEffect(() => {
    loadItems();
  }, [page, selectedCategory, search, onlyCritical, activeDevRole]);

  // Handle Manual Sync
  const handleTriggerSync = async () => {
    if (!canSync) return;
    setSyncing(true);
    setSyncMessage('در حال اتصال ایمن به سپیدار و همگام‌سازی مانده و گردش کالا...');
    try {
      const job = await triggerInventorySync('Incremental', activeDevRole);
      setSyncMessage(`همگام‌سازی با موفقیت انجام شد: ${job.itemsProcessed} کالا بررسی و ${job.snapshotsUpdated} موجودی به‌روزرسانی شد.`);
      await loadOverview();
      await loadItems();
    } catch (err: any) {
      setSyncMessage(`خطا در همگام‌سازی: ${err.message}`);
    } finally {
      setSyncing(false);
    }
  };

  // Open Item Detail Modal
  const handleOpenDetail = async (materialId: string) => {
    setActiveTab('kardex');
    setPolicySuccessMsg('');
    try {
      const [detail, movs, cons] = await Promise.all([
        fetchInventoryItemDetail(materialId, activeDevRole),
        fetchInventoryMovements(materialId, 1, 30, activeDevRole),
        fetchInventoryConsumption(materialId, activeDevRole)
      ]);
      setSelectedItem(detail);
      setMovements(movs.items);
      setConsumption(cons);

      // Populate policy form
      if (detail.currentPolicy) {
        setPolicyForm({
          reviewPeriodDays: detail.currentPolicy.reviewPeriodDays || 7,
          leadTimeDays: detail.currentPolicy.leadTimeDays || 3,
          safetyStock: detail.currentPolicy.safetyStock || 0,
          minOrderQuantity: detail.currentPolicy.minOrderQuantity || 0,
          packageMultiple: detail.currentPolicy.packageMultiple || 1,
          preferredSupplierId: detail.currentPolicy.preferredSupplierId,
          notes: detail.currentPolicy.notes || ''
        });
      } else {
        setPolicyForm({
          reviewPeriodDays: detail.reviewPeriodDays || 7,
          leadTimeDays: detail.leadTimeDays || 3,
          safetyStock: detail.safetyStock || 0,
          minOrderQuantity: 0,
          packageMultiple: 1,
          notes: ''
        });
      }
    } catch (err) {
      console.error('Failed to load item detail', err);
    }
  };

  // Save Policy
  const handleSavePolicy = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem || !canEditPolicy) return;
    setSavingPolicy(true);
    setPolicySuccessMsg('');
    try {
      await updateReplenishmentPolicy(selectedItem.materialId, policyForm, activeDevRole);
      setPolicySuccessMsg('خط‌مشی سفارش‌دهی با موفقیت در پایگاه داده CRM ذخیره شد.');
      // Refresh item detail
      const updated = await fetchInventoryItemDetail(selectedItem.materialId, activeDevRole);
      setSelectedItem(updated);
      loadItems();
    } catch (err: any) {
      alert(`خطا در ذخیره خط‌مشی: ${err.message}`);
    } finally {
      setSavingPolicy(false);
    }
  };

  // Phase 5: Load Goods Receipts
  const loadReceipts = async () => {
    setReceiptsLoading(true);
    try {
      const data = await fetchGoodsReceipts(undefined, activeDevRole);
      setReceipts(data);
    } catch (err) {
      console.error('Failed to load goods receipts', err);
    } finally {
      setReceiptsLoading(false);
    }
  };

  const loadIssuedOrders = async () => {
    try {
      const res = await fetchOrders({ pageSize: 100 }, activeDevRole);
      // Filter orders that can receive goods (Issued or PartiallyReceived)
      const open = res.items.filter(o => o.status === 'Issued' || o.status === 'PartiallyReceived');
      setIssuedOrders(open);
    } catch (err) {
      console.error('Failed to load orders for receipt', err);
    }
  };

  useEffect(() => {
    if (mainTab === 'receipts') {
      loadReceipts();
      loadIssuedOrders();
    }
  }, [mainTab, activeDevRole]);

  const handleSelectPoForReceipt = (orderId: string) => {
    const po = issuedOrders.find(o => o.id === orderId) || null;
    setSelectedPo(po);
    if (!po) {
      setReceiptForm(prev => ({ ...prev, purchaseOrderId: '', lines: [] }));
      return;
    }

    setReceiptForm({
      purchaseOrderId: po.id,
      carrierName: '',
      deliveryNoteNumber: '',
      vehiclePlateNumber: '',
      notes: '',
      lines: po.lines.map(line => {
        const remaining = Math.max(0, line.orderedQuantity - line.receivedQuantity);
        return {
          purchaseOrderLineId: line.id,
          materialId: line.materialId,
          materialName: line.materialNameSnapshot || line.materialName,
          orderedQuantity: line.orderedQuantity,
          alreadyReceivedQuantity: line.receivedQuantity,
          deliveredQuantity: remaining,
          acceptedQuantity: remaining,
          rejectedQuantity: 0,
          qcStatus: 'Passed',
          rejectionReason: '',
          storageLocation: 'انبار مرکزی - قفسه مواد اولیه'
        };
      })
    });
  };

  const handleCreateReceiptSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!receiptForm.purchaseOrderId) {
      setReceiptErrorMessage('انتخاب سفارش خرید الزامی است');
      return;
    }
    if (receiptForm.lines.some(l => l.acceptedQuantity <= 0 && l.rejectedQuantity <= 0)) {
      setReceiptErrorMessage('حداقل یکی از اقلام باید دارای مقدار تحویل‌شده باشد');
      return;
    }

    setReceiptSubmitting(true);
    setReceiptErrorMessage(null);
    try {
      const payload: CreateGoodsReceiptRequest = {
        purchaseOrderId: receiptForm.purchaseOrderId,
        carrierName: receiptForm.carrierName || undefined,
        deliveryNoteNumber: receiptForm.deliveryNoteNumber || undefined,
        vehiclePlateNumber: receiptForm.vehiclePlateNumber || undefined,
        notes: receiptForm.notes || undefined,
        lines: receiptForm.lines.map(l => ({
          purchaseOrderLineId: l.purchaseOrderLineId,
          deliveredQuantity: Number(l.deliveredQuantity),
          acceptedQuantity: Number(l.acceptedQuantity),
          rejectedQuantity: Number(l.rejectedQuantity),
          qcStatus: l.qcStatus,
          rejectionReason: l.rejectionReason || undefined,
          storageLocation: l.storageLocation || undefined
        }))
      };

      const res = await createGoodsReceipt(payload, activeDevRole);
      setReceiptSuccessMessage(`رسید انبار شماره ${res.receiptNumber} با موفقیت ثبت شد.`);
      setShowReceiptModal(false);
      loadReceipts();
      loadOverview();
      loadItems();
    } catch (err: any) {
      setReceiptErrorMessage(err.message || 'خطا در ثبت رسید انبار');
    } finally {
      setReceiptSubmitting(false);
    }
  };

  return (
    <div className="view-card">
      {/* View Header */}
      <div className="view-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h2>انبار واحد نانوایی و کنترل موجودی (Inventory & Single Warehouse)</h2>
            <span className={`badge-status ${warehouse?.freshnessStatus === 'Fresh' ? 'normal' : 'stale'}`}>
              {warehouse?.freshnessStatus === 'Fresh' ? 'موجودی به‌روز' : 'نیازمند همگام‌سازی'}
            </span>
          </div>
          <p style={{ marginTop: '4px', color: 'var(--text-muted)' }}>
            نظارت بر سطح موجودی قابل تخصیص، کاردکس گردش انبار سپیدار، تحلیل مصرف واقعی روزانه و مدیریت خط‌مشی سفارش‌دهی
          </p>
        </div>

        {/* Sync Controls */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button
            className="btn-primary"
            onClick={handleTriggerSync}
            disabled={syncing || !canSync}
            title={canSync ? 'اجرای همگام‌سازی دستی با سرور سپیدار' : 'شما مجوز همگام‌سازی با سپیدار را ندارید'}
          >
            <RefreshCw size={15} className={syncing ? 'animate-spin' : ''} />
            {syncing ? 'در حال همگام‌سازی...' : 'همگام‌سازی دستی با سپیدار'}
          </button>
        </div>
      </div>

      {/* Sync Banner Notice if any */}
      {syncMessage && (
        <div style={{
          background: 'var(--bg-card-hover)',
          border: '1px solid var(--border-accent)',
          borderRadius: '8px',
          padding: '10px 16px',
          marginBottom: '16px',
          fontSize: '13px',
          color: syncMessage.includes('خطا') ? '#f87171' : '#38bdf8',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span>{syncMessage}</span>
          <button onClick={() => setSyncMessage(null)} style={{ background: 'none', border: 'none', color: 'inherit', cursor: 'pointer' }}>
            <X size={14} />
          </button>
        </div>
      )}

      {/* Main Tab Switcher */}
      <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', marginBottom: '20px', gap: '24px' }}>
        <button
          onClick={() => setMainTab('inventory')}
          style={{
            background: 'none',
            border: 'none',
            borderBottom: mainTab === 'inventory' ? '2px solid #0284c7' : '2px solid transparent',
            color: mainTab === 'inventory' ? '#0284c7' : 'var(--text-muted)',
            fontWeight: mainTab === 'inventory' ? 600 : 400,
            padding: '10px 4px',
            cursor: 'pointer',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <Truck size={18} />
          <span>انبار واحد و کنترل موجودی (Inventory & Stock)</span>
        </button>

        <button
          onClick={() => setMainTab('receipts')}
          style={{
            background: 'none',
            border: 'none',
            borderBottom: mainTab === 'receipts' ? '2px solid #0284c7' : '2px solid transparent',
            color: mainTab === 'receipts' ? '#0284c7' : 'var(--text-muted)',
            fontWeight: mainTab === 'receipts' ? 600 : 400,
            padding: '10px 4px',
            cursor: 'pointer',
            fontSize: '14px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <PackageCheck size={18} />
          <span>رسیدهای کالای انبار و کنترل کیفی (Goods Receipts & QC) ({receipts.length})</span>
        </button>
      </div>

      {mainTab === 'inventory' && (
        <>
          {/* KPI Cards */}
          <div className="kpi-grid">
            <div className="kpi-card primary">
              <span className="kpi-lbl">کد و عنوان انبار فعال</span>
              <span className="kpi-val" style={{ fontSize: '18px' }}>{warehouse?.code || 'WH-MAIN'}</span>
              <span className="kpi-sub">{warehouse?.name || 'انبار مرکزی کارخانه نان'}</span>
            </div>

        <div className="kpi-card success">
          <span className="kpi-lbl">کل اقلام ماده اولیه در انبار</span>
          <span className="kpi-val">{warehouse?.totalItemsCount ?? 0}</span>
          <span className="kpi-sub">نگاشت‌شده با کاتالوگ CRM</span>
        </div>

        <div className="kpi-card danger">
          <span className="kpi-lbl">اقلام کسری یا زیر نقطه سفارش</span>
          <span className="kpi-val">{warehouse?.criticalItemsCount ?? 0}</span>
          <span className="kpi-sub">نیازمند اقدام فوری واحد خرید</span>
        </div>

        <div className="kpi-card warning">
          <span className="kpi-lbl">آخرین همگام‌سازی با سپیدار</span>
          <span className="kpi-val" style={{ fontSize: '15px' }}>
            {syncStatus?.lastSuccessfulSyncUtc 
              ? new Date(syncStatus.lastSuccessfulSyncUtc).toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' }) 
              : 'نامشخص'}
          </span>
          <span className="kpi-sub">
            {syncStatus?.lastSuccessfulSyncUtc 
              ? new Date(syncStatus.lastSuccessfulSyncUtc).toLocaleDateString('fa-IR') 
              : 'هنوز همگام نشده'}
          </span>
        </div>
      </div>

      {/* Safety ERP & Read-Only Architecture Badge */}
      <div style={{
        background: 'rgba(2, 132, 199, 0.08)',
        border: '1px solid rgba(2, 132, 199, 0.25)',
        borderRadius: '10px',
        padding: '12px 18px',
        marginBottom: '20px',
        display: 'flex',
        alignItems: 'center',
        gap: '12px'
      }}>
        <ShieldCheck size={22} color="#38bdf8" style={{ flexShrink: 0 }} />
        <div style={{ fontSize: '12px', lineHeight: 1.6 }}>
          <strong style={{ color: '#38bdf8' }}>پروتکل ایمنی یکپارچه‌سازی سپیدار:</strong>{' '}
          اتصال به SQL Server سپیدار صرفاً با لاگینِ اختصاصی <code>crm_reader</code> و با دستورات کاملاً گزینشی (SELECT-Only) برقرار است. هیچ‌گونه تراکنش نوشتنی، ویرایشی یا تعریف اسکیما روی سپیدار انجام نمی‌شود و دیتابیس CRM کاملاً مستقل عمل می‌کند.
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div style={{ display: 'flex', gap: '12px', marginBottom: '16px', flexWrap: 'wrap', alignItems: 'center' }}>
        <div style={{ position: 'relative', flex: 1, minWidth: '220px' }}>
          <Search size={16} color="var(--text-muted)" style={{ position: 'absolute', right: '12px', top: '10px' }} />
          <input
            type="text"
            className="form-input"
            style={{ width: '100%', paddingRight: '36px' }}
            placeholder="جستجو با کد یا عنوان کالا..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          />
        </div>

        <div style={{ minWidth: '180px' }}>
          <select
            className="form-select"
            style={{ width: '100%' }}
            value={selectedCategory}
            onChange={(e) => { setSelectedCategory(e.target.value); setPage(1); }}
          >
            <option value="">همه گروه‌های کالا</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>

        <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '13px', color: 'var(--text-muted)' }}>
          <input
            type="checkbox"
            checked={onlyCritical}
            onChange={(e) => { setOnlyCritical(e.target.checked); setPage(1); }}
          />
          فقط اقلام دارای کسری (زیر نقطه اطمینان)
        </label>
      </div>

      {/* Inventory Items Table */}
      <div style={{ overflowX: 'auto', background: 'var(--bg-sidebar)', borderRadius: '10px', border: '1px solid var(--border-color)' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'right', fontSize: '13px' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)', background: 'var(--bg-card)' }}>
              <th style={{ padding: '12px 16px' }}>کد کالا</th>
              <th style={{ padding: '12px 16px' }}>نام ماده اولیه</th>
              <th style={{ padding: '12px 16px' }}>دسته‌بندی</th>
              <th style={{ padding: '12px 16px' }}>موجودی فیزیکی</th>
              <th style={{ padding: '12px 16px' }}>قابل تخصیص</th>
              <th style={{ padding: '12px 16px' }}>ذخیره اطمینان</th>
              <th style={{ padding: '12px 16px' }}>مصرف روزانه (۷ روز)</th>
              <th style={{ padding: '12px 16px' }}>مصرف روزانه (۳۰ روز)</th>
              <th style={{ padding: '12px 16px' }}>وضعیت</th>
              <th style={{ padding: '12px 16px', textAlign: 'center' }}>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={10} style={{ padding: '30px', textAlign: 'center', color: 'var(--text-muted)' }}>
                  در حال بارگذاری لیست موجودی انبار...
                </td>
              </tr>
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={10} style={{ padding: '30px', textAlign: 'center', color: 'var(--text-muted)' }}>
                  موردی با شرایط فیلتر انتخاب‌شده یافت نشد.
                </td>
              </tr>
            ) : (
              items.map((item) => {
                const isCritical = item.stockStatus === 'Critical' || item.stockStatus === 'OutOfStock';
                return (
                  <tr
                    key={item.materialId}
                    style={{
                      borderBottom: '1px solid var(--border-color)',
                      transition: 'background 0.15s ease'
                    }}
                  >
                    <td style={{ padding: '12px 16px', fontFamily: 'monospace', color: '#38bdf8' }}>
                      {item.internalCode}
                    </td>
                    <td style={{ padding: '12px 16px', fontWeight: 600 }}>
                      {item.name}
                    </td>
                    <td style={{ padding: '12px 16px', color: 'var(--text-muted)' }}>
                      {item.categoryName}
                    </td>
                    <td style={{ padding: '12px 16px', fontWeight: 700, color: '#fff' }}>
                      {item.onHandQuantity.toLocaleString('fa-IR')} <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>{item.unitName}</span>
                    </td>
                    <td style={{ padding: '12px 16px', color: item.reservableQuantity > 0 ? '#34d399' : '#f87171' }}>
                      {item.reservableQuantity.toLocaleString('fa-IR')}
                    </td>
                    <td style={{ padding: '12px 16px', color: 'var(--text-muted)' }}>
                      {item.safetyStock.toLocaleString('fa-IR')}
                    </td>
                    <td style={{ padding: '12px 16px' }}>
                      {item.dailyConsumption7d > 0 ? `${item.dailyConsumption7d.toLocaleString('fa-IR')} / روز` : '-'}
                    </td>
                    <td style={{ padding: '12px 16px' }}>
                      {item.dailyConsumption30d > 0 ? `${item.dailyConsumption30d.toLocaleString('fa-IR')} / روز` : '-'}
                    </td>
                    <td style={{ padding: '12px 16px' }}>
                      <span className={`badge-status ${isCritical ? 'critical' : item.isStale ? 'stale' : 'normal'}`}>
                        {item.stockStatus === 'Critical' ? 'کسری بحرانی' :
                         item.stockStatus === 'OutOfStock' ? 'اتمام موجودی' :
                         item.isStale ? 'داده قدیمی' : 'نرمال'}
                      </span>
                    </td>
                    <td style={{ padding: '12px 16px', textAlign: 'center' }}>
                      <button
                        className="btn-secondary"
                        onClick={() => handleOpenDetail(item.materialId)}
                        style={{ fontSize: '11px', padding: '5px 10px' }}
                      >
                        <Activity size={13} />
                        کاردکس و تنظیمات
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination Controls */}
      <div className="pagination">
        <span>نمایش {items.length} از {totalCount} ماده اولیه در انبار</span>
        <div className="pagination-controls">
          <button
            className="btn-secondary"
            disabled={page <= 1}
            onClick={() => setPage(p => Math.max(1, p - 1))}
          >
            <ChevronRight size={14} /> قبلی
          </button>
          <span style={{ display: 'flex', alignItems: 'center', padding: '0 8px', color: 'var(--text-main)' }}>
            صفحه {page}
          </span>
          <button
            className="btn-secondary"
            disabled={items.length < pageSize}
            onClick={() => setPage(p => p + 1)}
          >
            بعدی <ChevronLeft size={14} />
          </button>
        </div>
      </div>
    </>
  )}

  {/* ================= Phase 5: Goods Receipts & QC Tab ================= */}
  {mainTab === 'receipts' && (
    <div>
      {/* Receipts Header & Actions */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h3 style={{ margin: 0, fontSize: '16px', color: '#0284c7' }}>رسیدهای کالای انبار، تحویل فیزیکی و بازرسی کیفیت (Goods Receipts & QC)</h3>
          <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>ثبت ورود محموله‌ها بر مبنای سفارشات خرید صادر شده تدارکات</span>
        </div>

        <button
          className="btn-primary"
          onClick={() => {
            setSelectedPo(null);
            setReceiptForm({
              purchaseOrderId: '',
              carrierName: '',
              deliveryNoteNumber: '',
              vehiclePlateNumber: '',
              notes: '',
              lines: []
            });
            setShowReceiptModal(true);
          }}
          style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
        >
          <Plus size={16} />
          <span>ثبت رسید کالای جدید (New Goods Receipt)</span>
        </button>
      </div>

      {/* Success Notification */}
      {receiptSuccessMessage && (
        <div style={{
          background: 'rgba(16, 185, 129, 0.12)',
          border: '1px solid rgba(16, 185, 129, 0.3)',
          borderRadius: '8px',
          padding: '10px 16px',
          marginBottom: '16px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          color: '#10b981',
          fontSize: '13px'
        }}>
          <span>{receiptSuccessMessage}</span>
          <button onClick={() => setReceiptSuccessMessage(null)} style={{ background: 'none', border: 'none', color: 'inherit', cursor: 'pointer' }}>×</button>
        </div>
      )}

      {/* Double Counting Protection Notice */}
      <div style={{
        background: 'rgba(2, 132, 199, 0.08)',
        border: '1px solid rgba(2, 132, 199, 0.25)',
        borderRadius: '8px',
        padding: '12px 16px',
        marginBottom: '20px',
        fontSize: '12px',
        display: 'flex',
        alignItems: 'center',
        gap: '10px'
      }}>
        <ShieldCheck size={20} color="#0284c7" style={{ flexShrink: 0 }} />
        <div>
          <strong style={{ color: '#0284c7' }}>جلوگیری از شمارش مضاعف موجودی (Stock Double-Counting Prevention):</strong>
          <p style={{ margin: '2px 0 0', color: 'var(--text-muted)' }}>
            رسید کالا در CRM برای کنترل فیزیکی، ثبت نتایج بازرسی کیفیت (QC) و تطبیق سه‌طرفه صورتحساب استفاده می‌شود. موجودی رسمی انبار از طریق همگام‌سازی رسیدهای رسمی سپیدار وارد سیستم می‌گردد تا تداخلی ایجاد نشود.
          </p>
        </div>
      </div>

      {/* Receipts Table */}
      <div className="table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>شماره رسید انبار</th>
              <th>شماره سفارش خرید</th>
              <th>شماره بارنامه / حواله</th>
              <th>تحویل‌گیرنده</th>
              <th>تاریخ رسید</th>
              <th>وضعیت رسید</th>
              <th>تعداد اقلام</th>
              <th>کنترل کیفی (QC)</th>
            </tr>
          </thead>
          <tbody>
            {receiptsLoading ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '30px' }}>در حال بارگذاری رسیدهای انبار...</td>
              </tr>
            ) : receipts.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                  هیچ رسیدی ثبت نشده است. با کلیک بر روی «ثبت رسید کالای جدید»، اولین تحویل را ثبت نمایید.
                </td>
              </tr>
            ) : (
              receipts.map(r => (
                <tr key={r.id}>
                  <td style={{ fontWeight: 600, color: '#0284c7' }}>{r.receiptNumber}</td>
                  <td>{r.orderNumber}</td>
                  <td>{r.deliveryNoteNumber || '—'}</td>
                  <td>{r.receivedByUsername}</td>
                  <td>{new Date(r.receiptDateUtc).toLocaleDateString('fa-IR')}</td>
                  <td>
                    {r.status === 'Accepted' && <span className="badge-status normal" style={{ background: '#059669', color: '#ecfdf5' }}>پذیرش کامل</span>}
                    {r.status === 'PartiallyAccepted' && <span className="badge-status normal" style={{ background: '#d97706', color: '#fffbeb' }}>پذیرش مشروط/جزئی</span>}
                    {r.status === 'Rejected' && <span className="badge-status normal" style={{ background: '#dc2626', color: '#fef2f2' }}>مردود شده</span>}
                  </td>
                  <td>{r.lines.length} قلم کالا</td>
                  <td>
                    {r.lines.every(l => l.qcStatus === 'Passed') ? (
                      <span style={{ color: '#10b981', fontSize: '12px', fontWeight: 600 }}>تأیید کیفی ۱۰۰٪</span>
                    ) : (
                      <span style={{ color: '#f59e0b', fontSize: '12px', fontWeight: 600 }}>دارای مغایرت کیفی</span>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* CREATE RECEIPT MODAL */}
      {showReceiptModal && (
        <div className="modal-backdrop">
          <div className="modal-content large" style={{ maxWidth: '900px' }}>
            <div className="modal-header">
              <div>
                <h3 style={{ margin: 0, color: '#0284c7' }}>ثبت رسید کالای جدید و کنترل کیفی (Goods Receipt Note)</h3>
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                  رهگیری تحویل جزئی یا کامل بر مبنای سفارشات خرید فعال {selectedPo ? `(تأمین‌کننده: ${selectedPo.supplierName})` : ''}
                </span>
              </div>
              <button
                onClick={() => setShowReceiptModal(false)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            {receiptErrorMessage && (
              <div style={{ background: 'rgba(239, 68, 68, 0.15)', border: '1px solid rgba(239, 68, 68, 0.3)', color: '#ef4444', padding: '10px 14px', borderRadius: '8px', margin: '14px 20px 0', fontSize: '13px' }}>
                {receiptErrorMessage}
              </div>
            )}

            <form onSubmit={handleCreateReceiptSubmit} style={{ padding: '20px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '20px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>سفارش خرید مرتبط *</label>
                  <select
                    className="input-field"
                    value={receiptForm.purchaseOrderId}
                    onChange={(e) => handleSelectPoForReceipt(e.target.value)}
                    required
                    style={{ width: '100%' }}
                  >
                    <option value="">-- انتخاب سفارش خرید صادر شده --</option>
                    {issuedOrders.map(o => (
                      <option key={o.id} value={o.id}>{o.orderNumber} - {o.supplierName}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>شماره بارنامه / حواله راننده</label>
                  <input
                    type="text"
                    className="input-field"
                    placeholder="مثال: BOL-1403-889"
                    value={receiptForm.deliveryNoteNumber}
                    onChange={(e) => setReceiptForm({ ...receiptForm, deliveryNoteNumber: e.target.value })}
                    style={{ width: '100%' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>نام شرکت حمل / راننده</label>
                  <input
                    type="text"
                    className="input-field"
                    value={receiptForm.carrierName}
                    onChange={(e) => setReceiptForm({ ...receiptForm, carrierName: e.target.value })}
                    style={{ width: '100%' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px', fontWeight: 600 }}>شماره پلاک خودرو</label>
                  <input
                    type="text"
                    className="input-field"
                    placeholder="مثال: ایران ۲۱ - ۴۵۶ ج ۷۸"
                    value={receiptForm.vehiclePlateNumber}
                    onChange={(e) => setReceiptForm({ ...receiptForm, vehiclePlateNumber: e.target.value })}
                    style={{ width: '100%' }}
                  />
                </div>
              </div>

              {/* Over-receipt alert */}
              <div style={{
                background: 'rgba(217, 119, 6, 0.1)',
                border: '1px solid rgba(217, 119, 6, 0.3)',
                borderRadius: '8px',
                padding: '10px 14px',
                marginBottom: '16px',
                fontSize: '12px',
                color: '#d97706',
                display: 'flex',
                alignItems: 'center',
                gap: '8px'
              }}>
                <AlertCircle size={16} />
                <span>حفاظت از بیش‌دریافتی (Over-Receipt Protection): سقف پذیرش اقلام حداکثر تا ۱۰۵٪ مقدار سفارش مجاز است.</span>
              </div>

              {/* Lines Table */}
              {receiptForm.lines.length > 0 && (
                <div style={{ marginBottom: '20px' }}>
                  <h4 style={{ margin: '0 0 10px', fontSize: '14px' }}>اقلام تحویلی محموله و نتایج کنترل کیفی (QC)</h4>
                  <div className="table-container" style={{ maxHeight: '250px' }}>
                    <table className="data-table" style={{ fontSize: '12px' }}>
                      <thead>
                        <tr>
                          <th>ماده اولیه</th>
                          <th>سفارش داده شده</th>
                          <th>قبلاً دریافت شده</th>
                          <th>مقدار تحویلی</th>
                          <th>مقدار پذیرش شده</th>
                          <th>مقدار مرجوعی</th>
                          <th>وضعیت QC</th>
                          <th>علت مرجوعی / توضیحات</th>
                        </tr>
                      </thead>
                      <tbody>
                        {receiptForm.lines.map((line, idx) => (
                          <tr key={idx}>
                            <td><strong>{line.materialName}</strong></td>
                            <td>{line.orderedQuantity.toLocaleString('fa-IR')}</td>
                            <td>{line.alreadyReceivedQuantity.toLocaleString('fa-IR')}</td>
                            <td>
                              <input
                                type="number"
                                min="0"
                                step="any"
                                className="input-field"
                                value={line.deliveredQuantity}
                                onChange={(e) => {
                                  const updated = [...receiptForm.lines];
                                  updated[idx].deliveredQuantity = Number(e.target.value);
                                  setReceiptForm({ ...receiptForm, lines: updated });
                                }}
                                style={{ width: '80px', padding: '4px 6px', fontSize: '12px' }}
                              />
                            </td>
                            <td>
                              <input
                                type="number"
                                min="0"
                                step="any"
                                className="input-field"
                                value={line.acceptedQuantity}
                                onChange={(e) => {
                                  const updated = [...receiptForm.lines];
                                  updated[idx].acceptedQuantity = Number(e.target.value);
                                  setReceiptForm({ ...receiptForm, lines: updated });
                                }}
                                style={{ width: '80px', padding: '4px 6px', fontSize: '12px', borderColor: '#10b981' }}
                              />
                            </td>
                            <td>
                              <input
                                type="number"
                                min="0"
                                step="any"
                                className="input-field"
                                value={line.rejectedQuantity}
                                onChange={(e) => {
                                  const updated = [...receiptForm.lines];
                                  updated[idx].rejectedQuantity = Number(e.target.value);
                                  setReceiptForm({ ...receiptForm, lines: updated });
                                }}
                                style={{ width: '70px', padding: '4px 6px', fontSize: '12px', borderColor: line.rejectedQuantity > 0 ? '#ef4444' : undefined }}
                              />
                            </td>
                            <td>
                              <select
                                className="input-field"
                                value={line.qcStatus}
                                onChange={(e) => {
                                  const updated = [...receiptForm.lines];
                                  updated[idx].qcStatus = e.target.value;
                                  setReceiptForm({ ...receiptForm, lines: updated });
                                }}
                                style={{ width: '100px', padding: '4px 6px', fontSize: '12px' }}
                              >
                                <option value="Passed">پذیرش کامل (Passed)</option>
                                <option value="Conditional">پذیرش مشروط (Conditional)</option>
                                <option value="Rejected">مردود (Rejected)</option>
                              </select>
                            </td>
                            <td>
                              <input
                                type="text"
                                className="input-field"
                                placeholder="دلیل مرجوعی یا محل تخلیه..."
                                value={line.rejectionReason}
                                onChange={(e) => {
                                  const updated = [...receiptForm.lines];
                                  updated[idx].rejectionReason = e.target.value;
                                  setReceiptForm({ ...receiptForm, lines: updated });
                                }}
                                style={{ width: '140px', padding: '4px 6px', fontSize: '12px' }}
                              />
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* Notes */}
              <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', fontSize: '12px', marginBottom: '6px' }}>ملاحظات انبارداری و توضیحات بازرسی</label>
                <textarea
                  className="input-field"
                  rows={2}
                  value={receiptForm.notes}
                  onChange={(e) => setReceiptForm({ ...receiptForm, notes: e.target.value })}
                  style={{ width: '100%' }}
                />
              </div>

              {/* Actions */}
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setShowReceiptModal(false)}
                  disabled={receiptSubmitting}
                >
                  انصراف
                </button>
                <button
                  type="submit"
                  className="btn-primary"
                  disabled={receiptSubmitting || receiptForm.lines.length === 0}
                  style={{ background: '#059669', borderColor: '#059669' }}
                >
                  {receiptSubmitting ? 'در حال ثبت رسید...' : 'تأیید و ثبت رسید انبار'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )}

      {/* ================= Item Detail & Kardex Modal ================= */}
      {selectedItem && (
        <div className="modal-backdrop">
          <div className="modal-content large" style={{ maxWidth: '950px' }}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <Activity size={20} color="#38bdf8" />
                <div>
                  <h3>کاردکس و شناسنامه انبار: {selectedItem.name}</h3>
                  <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                    کد کالا: {selectedItem.internalCode} | دسته: {selectedItem.categoryName} | واحد پایه: {selectedItem.unitName}
                  </span>
                </div>
              </div>
              <button
                onClick={() => setSelectedItem(null)}
                style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div className="modal-body">
              {/* Snapshot Quick Stats Banner */}
              <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))',
                gap: '12px',
                background: 'var(--bg-sidebar)',
                padding: '14px',
                borderRadius: '8px',
                border: '1px solid var(--border-color)'
              }}>
                <div>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>موجودی در انبار:</span>
                  <div style={{ fontSize: '17px', fontWeight: 700, color: '#fff' }}>
                    {selectedItem.onHandQuantity.toLocaleString('fa-IR')} {selectedItem.unitName}
                  </div>
                </div>
                <div>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>تخصیص‌یافته/رزرو:</span>
                  <div style={{ fontSize: '17px', fontWeight: 700, color: '#f59e0b' }}>
                    {selectedItem.reservedQuantity.toLocaleString('fa-IR')} {selectedItem.unitName}
                  </div>
                </div>
                <div>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>موجودی آزاد قابل‌تخصیص:</span>
                  <div style={{ fontSize: '17px', fontWeight: 700, color: '#34d399' }}>
                    {selectedItem.reservableQuantity.toLocaleString('fa-IR')} {selectedItem.unitName}
                  </div>
                </div>
                <div>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>ذخیره اطمینان:</span>
                  <div style={{ fontSize: '17px', fontWeight: 700, color: 'var(--text-main)' }}>
                    {selectedItem.safetyStock.toLocaleString('fa-IR')} {selectedItem.unitName}
                  </div>
                </div>
                <div>
                  <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>مصرف روزانه (۳۰ روز):</span>
                  <div style={{ fontSize: '17px', fontWeight: 700, color: '#38bdf8' }}>
                    {selectedItem.dailyConsumption30d.toLocaleString('fa-IR')} / روز
                  </div>
                </div>
              </div>

              {/* Tabs Bar */}
              <div className="tabs-header">
                <button
                  className={`tab-btn ${activeTab === 'kardex' ? 'active' : ''}`}
                  onClick={() => setActiveTab('kardex')}
                >
                  <FileText size={14} style={{ display: 'inline', marginLeft: '6px' }} />
                  کاردکس گردش کالا (Movements)
                </button>
                <button
                  className={`tab-btn ${activeTab === 'consumption' ? 'active' : ''}`}
                  onClick={() => setActiveTab('consumption')}
                >
                  <TrendingUp size={14} style={{ display: 'inline', marginLeft: '6px' }} />
                  تحلیل آماری مصرف واقعی
                </button>
                <button
                  className={`tab-btn ${activeTab === 'policy' ? 'active' : ''}`}
                  onClick={() => setActiveTab('policy')}
                >
                  <Settings size={14} style={{ display: 'inline', marginLeft: '6px' }} />
                  خط‌مشی سفارش‌دهی (Replenishment Policy)
                </button>
                <button
                  className={`tab-btn ${activeTab === 'suppliers' ? 'active' : ''}`}
                  onClick={() => setActiveTab('suppliers')}
                >
                  <Truck size={14} style={{ display: 'inline', marginLeft: '6px' }} />
                  تأمین‌کنندگان مرتبط ({selectedItem.suppliers.length})
                </button>
              </div>

              {/* Tab 1: Kardex Table */}
              {activeTab === 'kardex' && (
                <div>
                  <div style={{ maxHeight: '350px', overflowY: 'auto', border: '1px solid var(--border-color)', borderRadius: '8px' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'right', fontSize: '12px' }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                          <th style={{ padding: '10px 12px' }}>شماره برگه</th>
                          <th style={{ padding: '10px 12px' }}>نوع تراکنش</th>
                          <th style={{ padding: '10px 12px' }}>مقدار</th>
                          <th style={{ padding: '10px 12px' }}>تاریخ تراکنش</th>
                          <th style={{ padding: '10px 12px' }}>وضعیت سند</th>
                          <th style={{ padding: '10px 12px' }}>سند مرجع</th>
                        </tr>
                      </thead>
                      <tbody>
                        {movements.length === 0 ? (
                          <tr>
                            <td colSpan={6} style={{ padding: '24px', textAlign: 'center', color: 'var(--text-muted)' }}>
                              هیچ تراکنش ثبتی برای این کالا در دوره جاری یافت نشد.
                            </td>
                          </tr>
                        ) : (
                          movements.map((m) => {
                            const isOutflow = m.movementType.includes('Issue') || m.movementType.includes('Consumption');
                            return (
                              <tr key={m.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                                <td style={{ padding: '8px 12px', fontFamily: 'monospace' }}>{m.voucherNumber}</td>
                                <td style={{ padding: '8px 12px' }}>
                                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                                    {isOutflow ? <ArrowUpRight size={13} color="#f87171" /> : <ArrowDownLeft size={13} color="#34d399" />}
                                    {m.movementType === 'ProductionConsumption' ? 'حواله مصرف تولید' :
                                     m.movementType === 'Receipt' ? 'رسید ورود به انبار' :
                                     m.movementType === 'TransferOut' ? 'حواله انتقال خروجی' :
                                     m.movementType === 'TransferIn' ? 'رسید انتقال ورودی' :
                                     m.movementType === 'Return' ? 'برگشت از تولید' : m.movementType}
                                  </span>
                                </td>
                                <td style={{ padding: '8px 12px', fontWeight: 600, color: isOutflow ? '#f87171' : '#34d399' }}>
                                  {isOutflow ? '-' : '+'}{m.quantity.toLocaleString('fa-IR')} {m.unitName}
                                </td>
                                <td style={{ padding: '8px 12px', color: 'var(--text-muted)' }}>
                                  {new Date(m.transactionDateUtc).toLocaleDateString('fa-IR')}
                                </td>
                                <td style={{ padding: '8px 12px' }}>
                                  {m.isCancelled ? (
                                    <span style={{ color: '#ef4444', fontWeight: 600 }}>ابطال‌شده</span>
                                  ) : (
                                    <span style={{ color: '#10b981' }}>{m.postingStatus === 'Posted' ? 'قطعی‌شده' : 'پیش‌نویس'}</span>
                                  )}
                                </td>
                                <td style={{ padding: '8px 12px', color: 'var(--text-dim)' }}>
                                  {m.referenceDocument || '-'}
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

              {/* Tab 2: Consumption Breakdown */}
              {activeTab === 'consumption' && (
                <div>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '16px' }}>
                    <div className="kpi-card">
                      <span className="kpi-lbl">مصرف بازه ۷ روز اخیر</span>
                      <span className="kpi-val" style={{ color: '#38bdf8' }}>
                        {consumption?.consumption7d.toLocaleString('fa-IR') ?? 0} {consumption?.baseUnit}
                      </span>
                      <span className="kpi-sub">
                        میانگین: {consumption?.averageDaily7d.toLocaleString('fa-IR') ?? 0} در روز ({consumption?.activeDays7d ?? 0} روز با تولید)
                      </span>
                    </div>

                    <div className="kpi-card">
                      <span className="kpi-lbl">مصرف بازه ۱۴ روز اخیر</span>
                      <span className="kpi-val" style={{ color: '#38bdf8' }}>
                        {consumption?.consumption14d.toLocaleString('fa-IR') ?? 0} {consumption?.baseUnit}
                      </span>
                      <span className="kpi-sub">
                        میانگین: {consumption?.averageDaily14d.toLocaleString('fa-IR') ?? 0} در روز ({consumption?.activeDays14d ?? 0} روز با تولید)
                      </span>
                    </div>

                    <div className="kpi-card">
                      <span className="kpi-lbl">مصرف بازه ۳۰ روز اخیر</span>
                      <span className="kpi-val" style={{ color: '#38bdf8' }}>
                        {consumption?.consumption30d.toLocaleString('fa-IR') ?? 0} {consumption?.baseUnit}
                      </span>
                      <span className="kpi-sub">
                        میانگین مبنا: {consumption?.averageDaily30d.toLocaleString('fa-IR') ?? 0} در روز ({consumption?.activeDays30d ?? 0} روز با تولید)
                      </span>
                    </div>
                  </div>

                  <p style={{ fontSize: '12px', color: 'var(--text-muted)', lineHeight: 1.6 }}>
                    نکته محاسباتی: در محاسبه مصرف واقعی، فقط حواله‌های مصرف قطعی در فرآیند تولید لحاظ شده و اسناد ابطال‌شده، انتقالات داخلی بین انبارها و تعدیلات ریالی لحاظ نمی‌گردند.
                  </p>
                </div>
              )}

              {/* Tab 3: Replenishment Policy Form */}
              {activeTab === 'policy' && (
                <form onSubmit={handleSavePolicy}>
                  {policySuccessMsg && (
                    <div style={{ background: 'rgba(16, 185, 129, 0.15)', border: '1px solid #10b981', color: '#34d399', padding: '10px 14px', borderRadius: '8px', fontSize: '13px', marginBottom: '14px' }}>
                      {policySuccessMsg}
                    </div>
                  )}

                  <div className="form-row">
                    <div className="form-group">
                      <label>دوره بازنگری سفارش (Review Period - روز):</label>
                      <input
                        type="number"
                        min="1"
                        className="form-input"
                        value={policyForm.reviewPeriodDays}
                        disabled={!canEditPolicy}
                        onChange={(e) => setPolicyForm({ ...policyForm, reviewPeriodDays: parseInt(e.target.value) || 1 })}
                      />
                    </div>

                    <div className="form-group">
                      <label>زمان تحویل تأمین‌کننده (Lead Time - روز):</label>
                      <input
                        type="number"
                        min="0"
                        className="form-input"
                        value={policyForm.leadTimeDays}
                        disabled={!canEditPolicy}
                        onChange={(e) => setPolicyForm({ ...policyForm, leadTimeDays: parseInt(e.target.value) || 0 })}
                      />
                    </div>

                    <div className="form-group">
                      <label>ذخیره اطمینان (Safety Stock - {selectedItem.unitName}):</label>
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        className="form-input"
                        value={policyForm.safetyStock}
                        disabled={!canEditPolicy}
                        onChange={(e) => setPolicyForm({ ...policyForm, safetyStock: parseFloat(e.target.value) || 0 })}
                      />
                    </div>
                  </div>

                  <div className="form-row" style={{ marginTop: '14px' }}>
                    <div className="form-group">
                      <label>حداقل سفارش (MOQ - {selectedItem.unitName}):</label>
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        className="form-input"
                        value={policyForm.minOrderQuantity}
                        disabled={!canEditPolicy}
                        onChange={(e) => setPolicyForm({ ...policyForm, minOrderQuantity: parseFloat(e.target.value) || 0 })}
                      />
                    </div>

                    <div className="form-group">
                      <label>مضرب بسته یا کیسه (Package Multiple):</label>
                      <input
                        type="number"
                        min="1"
                        step="0.01"
                        className="form-input"
                        value={policyForm.packageMultiple || 1}
                        disabled={!canEditPolicy}
                        onChange={(e) => setPolicyForm({ ...policyForm, packageMultiple: parseFloat(e.target.value) || 1 })}
                      />
                    </div>

                    <div className="form-group">
                      <label>تأمین‌کننده پیش‌فرض:</label>
                      <select
                        className="form-select"
                        value={policyForm.preferredSupplierId || ''}
                        disabled={!canEditPolicy}
                        onChange={(e) => setPolicyForm({ ...policyForm, preferredSupplierId: e.target.value || undefined })}
                      >
                        <option value="">انتخاب تأمین‌کننده...</option>
                        {selectedItem.suppliers.map((s) => (
                          <option key={s.supplierId} value={s.supplierId}>{s.supplierName} {s.isPreferred ? '(پیش‌فرض کاتالوگ)' : ''}</option>
                        ))}
                      </select>
                    </div>
                  </div>

                  <div className="form-group" style={{ marginTop: '14px' }}>
                    <label>توضیحات و الزامات سفارش‌دهی:</label>
                    <textarea
                      className="form-textarea"
                      rows={2}
                      value={policyForm.notes || ''}
                      disabled={!canEditPolicy}
                      onChange={(e) => setPolicyForm({ ...policyForm, notes: e.target.value })}
                      placeholder="توضیحات خاص کارخانه آرد، ظرفیت باربری یا حداقل کرایه حمل..."
                    />
                  </div>

                  {canEditPolicy && (
                    <div style={{ marginTop: '16px', display: 'flex', justifyContent: 'flex-end' }}>
                      <button type="submit" className="btn-primary" disabled={savingPolicy}>
                        <Save size={15} />
                        {savingPolicy ? 'در حال ذخیره...' : 'ذخیره خط‌مشی سفارش‌دهی'}
                      </button>
                    </div>
                  )}
                </form>
              )}

              {/* Tab 4: Suppliers List */}
              {activeTab === 'suppliers' && (
                <div>
                  {selectedItem.suppliers.length === 0 ? (
                    <p style={{ color: 'var(--text-muted)', fontSize: '13px', padding: '16px 0' }}>
                      هنوز هیچ تأمین‌کننده‌ای به این ماده اولیه متصل نشده است.
                    </p>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      {selectedItem.suppliers.map((s) => (
                        <div
                          key={s.supplierId}
                          style={{
                            background: 'var(--bg-card)',
                            border: '1px solid var(--border-color)',
                            borderRadius: '8px',
                            padding: '12px 16px',
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center'
                          }}
                        >
                          <div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                              <strong style={{ fontSize: '14px' }}>{s.supplierName}</strong>
                              {s.isPreferred && (
                                <span className="badge-status normal">تأمین‌کننده برگزیده</span>
                              )}
                            </div>
                            <span style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
                              وضعیت تأمین: {s.status === 'Active' ? 'فعال' : s.status}
                            </span>
                          </div>

                          {onNavigateToSupplier && (
                            <button
                              className="btn-secondary"
                              onClick={() => {
                                setSelectedItem(null);
                                onNavigateToSupplier(s.supplierId);
                              }}
                            >
                              <ExternalLink size={13} />
                              مشاهده پروفایل تأمین‌کننده
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>

            <div className="modal-footer" style={{ justifyContent: 'space-between' }}>
              <div>
                {onNavigateToMaterial && (
                  <button
                    className="btn-secondary"
                    onClick={() => {
                      const id = selectedItem.materialId;
                      setSelectedItem(null);
                      onNavigateToMaterial(id);
                    }}
                  >
                    <ExternalLink size={13} />
                    مشاهده مشخصات کامل در کاتالوگ مواد اولیه
                  </button>
                )}
              </div>
              <button className="btn-secondary" onClick={() => setSelectedItem(null)}>
                بستن
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
