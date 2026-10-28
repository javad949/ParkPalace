import { 
  HealthResponse, 
  MeResponse, 
  RoleDto,
  CategoryDto,
  CategoryTreeDto,
  UnitOfMeasureDto,
  MaterialDto,
  MaterialDetailDto,
  PagedResult,
  PartyDto,
  PartyDetailDto,
  SupplierMaterialDto,
  SupplierPriceHistoryDto,
  WarehouseDto,
  InventoryItemDto,
  InventoryItemDetailDto,
  InventoryMovementDto,
  ConsumptionSummaryDto,
  SyncStatusDto,
  SyncJobDto,
  PurchaseRecommendationDto,
  ReplenishmentPolicyDto,
  UpdateReplenishmentPolicyRequest,
  PurchaseOrderDto,
  CreatePurchaseOrderRequest,
  CancelPurchaseOrderRequest,
  GoodsReceiptDto,
  CreateGoodsReceiptRequest,
  SupplierInvoiceDto,
  CreateSupplierInvoiceRequest,
  ThreeWayMatchResultDto,
  ResolveMatchingRequest,
  SepidarCandidateInvoiceDto,
  SepidarFinancialReconciliationDto,
  LinkSepidarInvoiceRequest,
  CallRecordDto,
  CallNoteDto,
  UserExtensionDto,
  PartyMatchDto,
  CreateCallNoteRequest,
  ClassifyCallRequest,
  SimulateCallRequest,
  CdrReconciliationDto,
  FollowUpStatus,
  ReportFilterParams,
  ProcurementSummaryReportDto,
  PurchaseOrderTrendItemDto,
  SupplierPerformanceReportItemDto,
  MaterialConsumptionReportItemDto,
  InventoryHealthReportDto,
  FinanceReconciliationReportDto,
  TelephonyActivityReportDto,
  IntegrationHealthReportDto,
  RoleDashboardDto,
  ExportRequestDto
} from '../types';

const API_BASE = '/api';

function getHeaders(devRole?: string): HeadersInit {
  const h: Record<string, string> = { 'Content-Type': 'application/json' };
  const role = devRole || (typeof window !== 'undefined' ? localStorage.getItem('parkpalace_active_role') : null);
  if (role) {
    h['X-Active-Role'] = role;
    h['X-Dev-Role'] = role;
    h['X-Dev-User'] = `sahar\\${role.toLowerCase()}`;
  }
  return h;
}

export async function fetchHealth(): Promise<HealthResponse> {
  const res = await fetch(`${API_BASE}/health`);
  if (!res.ok) throw new Error(`خطا در ارتباط با سرور: ${res.status}`);
  return res.json();
}

export async function fetchMe(devRoleHeader?: string): Promise<MeResponse> {
  const res = await fetch(`${API_BASE}/me`, { headers: getHeaders(devRoleHeader), credentials: 'include' });
  if (res.status === 401) {
    return {
      username: '',
      displayName: 'کاربر احراز هویت نشده',
      isAuthenticated: false,
      roles: [],
      permissions: [],
    };
  }
  if (!res.ok) throw new Error(`خطا در بازیابی اطلاعات کاربر: ${res.status}`);
  return res.json();
}

export async function fetchRoles(devRoleHeader?: string): Promise<RoleDto[]> {
  const res = await fetch(`${API_BASE}/roles`, { headers: getHeaders(devRoleHeader), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در بازیابی نقش‌ها: ${res.status}`);
  return res.json();
}

// ================= Phase 3: Categories & Catalog =================

export async function fetchCategoryTree(devRole?: string): Promise<CategoryTreeDto[]> {
  const res = await fetch(`${API_BASE}/v1/categories/tree`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در دریافت درخت دسته‌بندی‌ها: ${res.status}`);
  return res.json();
}

export async function fetchCategories(devRole?: string): Promise<CategoryDto[]> {
  const res = await fetch(`${API_BASE}/v1/categories`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در دریافت لیست دسته‌بندی‌ها: ${res.status}`);
  return res.json();
}

export async function createCategory(data: { name: string; code: string; description?: string; parentId?: string }, devRole?: string): Promise<CategoryDto> {
  const res = await fetch(`${API_BASE}/v1/categories`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ایجاد دسته‌بندی: ${res.status}`);
  }
  return res.json();
}

export async function moveCategory(id: string, newParentId: string | null, devRole?: string): Promise<void> {
  const res = await fetch(`${API_BASE}/v1/categories/${id}/move`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify({ newParentId })
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در جابجایی دسته‌بندی: ${res.status}`);
  }
}

// ================= Phase 3: Units Of Measure =================

export async function fetchUnits(devRole?: string): Promise<UnitOfMeasureDto[]> {
  const res = await fetch(`${API_BASE}/v1/units`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در دریافت واحدهای سنجش: ${res.status}`);
  return res.json();
}

// ================= Phase 3: Materials =================

export async function fetchMaterials(params?: { search?: string; categoryId?: string; page?: number; pageSize?: number }, devRole?: string): Promise<PagedResult<MaterialDto>> {
  const sp = new URLSearchParams();
  if (params?.search) sp.append('search', params.search);
  if (params?.categoryId) sp.append('categoryId', params.categoryId);
  if (params?.page) sp.append('page', params.page.toString());
  if (params?.pageSize) sp.append('pageSize', params.pageSize.toString());

  const res = await fetch(`${API_BASE}/v1/materials?${sp.toString()}`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در دریافت لیست مواد اولیه: ${res.status}`);
  return res.json();
}

export async function fetchMaterialDetail(id: string, devRole?: string): Promise<MaterialDetailDto> {
  const res = await fetch(`${API_BASE}/v1/materials/${id}`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در دریافت مشخصات ماده اولیه: ${res.status}`);
  return res.json();
}

export async function createMaterial(data: {
  internalCode: string;
  name: string;
  categoryId: string;
  baseUnitId: string;
  brand?: string;
  description?: string;
  specifications?: string;
  minStock?: number;
  safetyStock?: number;
  leadTimeDays?: number;
}, devRole?: string): Promise<MaterialDto> {
  const res = await fetch(`${API_BASE}/v1/materials`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در تعریف ماده اولیه: ${res.status}`);
  }
  return res.json();
}

export async function updateMaterial(id: string, data: {
  name: string;
  categoryId: string;
  baseUnitId: string;
  brand?: string;
  packaging?: string;
  packageSize?: number;
  packageSizeUnitId?: string;
  purchaseUnitId?: string;
  consumptionUnitId?: string;
  productFamily?: string;
  description?: string;
  specifications?: string;
  standardizationNotes?: string;
  minStock?: number;
  safetyStock?: number;
  leadTimeDays?: number;
  isActive?: boolean;
}, devRole?: string): Promise<MaterialDto> {
  const res = await fetch(`${API_BASE}/v1/materials/${id}`, {
    method: 'PUT',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ویرایش کاتالوگ: ${res.status}`);
  }
  return res.json();
}

export async function confirmConversion(materialId: string, conversionId: string, devRole?: string): Promise<void> {
  const res = await fetch(`${API_BASE}/v1/materials/${materialId}/conversions/${conversionId}/confirm`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در تأیید تبدیل واحد: ${res.status}`);
  }
}

export async function confirmCategorization(materialId: string, devRole?: string): Promise<void> {
  const res = await fetch(`${API_BASE}/v1/materials/${materialId}/confirm-categorization`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در تأیید دسته‌بندی کاتالوگ: ${res.status}`);
  }
}

export async function addUnitConversion(materialId: string, data: {
  fromUnitId: string;
  toUnitId: string;
  factor: number;
  isConfirmed?: boolean;
  notes?: string;
}, devRole?: string): Promise<any> {
  const res = await fetch(`${API_BASE}/v1/materials/${materialId}/conversions`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ثبت تبدیل واحد: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 3: Suppliers & Parties =================

export async function fetchSuppliers(params?: { search?: string; partyType?: number; suppliedMaterialId?: string; page?: number; pageSize?: number }, devRole?: string): Promise<PagedResult<PartyDto>> {
  const sp = new URLSearchParams();
  if (params?.search) sp.append('search', params.search);
  if (params?.partyType) sp.append('partyType', params.partyType.toString());
  if (params?.suppliedMaterialId) sp.append('suppliedMaterialId', params.suppliedMaterialId);
  if (params?.page) sp.append('page', params.page.toString());
  if (params?.pageSize) sp.append('pageSize', params.pageSize.toString());

  const res = await fetch(`${API_BASE}/v1/suppliers?${sp.toString()}`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در دریافت لیست تأمین‌کنندگان: ${res.status}`);
  return res.json();
}

export async function fetchSupplierDetail(id: string, devRole?: string): Promise<PartyDetailDto> {
  const res = await fetch(`${API_BASE}/v1/suppliers/${id}`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) throw new Error(`خطا در دریافت اطلاعات تأمین‌کننده: ${res.status}`);
  return res.json();
}

export async function createSupplier(data: {
  partyType: number;
  displayName: string;
  notes?: string;
  firstName?: string;
  lastName?: string;
  shopName?: string;
  nationalId?: string;
  legalName?: string;
  nationalIdentifier?: string;
  registrationNumber?: string;
  economicCode?: string;
  primaryContactName?: string;
  primaryPhone?: string;
  primaryPhoneType?: string;
  primaryAddressCity?: string;
  primaryAddressText?: string;
}, devRole?: string): Promise<PartyDto> {
  const res = await fetch(`${API_BASE}/v1/suppliers`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ثبت تأمین‌کننده: ${res.status}`);
  }
  return res.json();
}

export async function linkSupplierMaterial(supplierId: string, data: {
  materialId: string;
  isPreferred?: boolean;
  status?: string;
  defaultLeadTimeDays?: number;
  minimumOrderQty?: number;
  initialPrice?: number;
  currency?: string;
  priceUnitId?: string;
  priceSource?: string;
}, devRole?: string): Promise<SupplierMaterialDto> {
  const res = await fetch(`${API_BASE}/v1/suppliers/${supplierId}/materials`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در انتساب کالا به تأمین‌کننده: ${res.status}`);
  }
  return res.json();
}

export async function addPriceQuote(supplierId: string, materialId: string, data: {
  price: number;
  currency: string;
  unitId: string;
  effectiveFrom: string;
  effectiveTo?: string;
  source?: string;
  notes?: string;
}, devRole?: string): Promise<SupplierPriceHistoryDto> {
  const res = await fetch(`${API_BASE}/v1/suppliers/${supplierId}/materials/${materialId}/prices`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ثبت قیمت استعلامی: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 4: Inventory & Warehouse =================

export async function fetchWarehouse(devRole?: string): Promise<WarehouseDto> {
  const res = await fetch(`${API_BASE}/v1/inventory/warehouse`, { headers: getHeaders(devRole), credentials: 'include' });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت اطلاعات انبار: ${res.status}`);
  }
  return res.json();
}

export async function fetchInventoryItems(
  page = 1,
  pageSize = 20,
  categoryId?: string,
  search?: string,
  devRole?: string
): Promise<PagedResult<InventoryItemDto>> {
  const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
  if (categoryId) params.append('categoryId', categoryId);
  if (search) params.append('search', search);

  const res = await fetch(`${API_BASE}/v1/inventory/items?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت موجودی انبار: ${res.status}`);
  }
  return res.json();
}

export async function fetchInventoryItemDetail(materialId: string, devRole?: string): Promise<InventoryItemDetailDto> {
  const res = await fetch(`${API_BASE}/v1/inventory/items/${materialId}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت جزئیات موجودی کالا: ${res.status}`);
  }
  return res.json();
}

export async function fetchInventoryMovements(
  materialId: string,
  page = 1,
  pageSize = 20,
  devRole?: string
): Promise<PagedResult<InventoryMovementDto>> {
  const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
  const res = await fetch(`${API_BASE}/v1/inventory/items/${materialId}/movements?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت گردش کاردکس: ${res.status}`);
  }
  return res.json();
}

export async function fetchInventoryConsumption(materialId: string, devRole?: string): Promise<ConsumptionSummaryDto> {
  const res = await fetch(`${API_BASE}/v1/inventory/items/${materialId}/consumption`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت تحلیل مصرف: ${res.status}`);
  }
  return res.json();
}

export async function fetchSyncStatus(devRole?: string): Promise<SyncStatusDto> {
  const res = await fetch(`${API_BASE}/v1/inventory/sync/status`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت وضعیت همگام‌سازی: ${res.status}`);
  }
  return res.json();
}

export async function triggerInventorySync(jobType = 'Incremental', devRole?: string): Promise<SyncJobDto> {
  const res = await fetch(`${API_BASE}/v1/inventory/sync`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify({ jobType })
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در اجرای همگام‌سازی: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 4: Procurement Recommendations & Policies =================

export async function fetchRecommendations(devRole?: string): Promise<PurchaseRecommendationDto[]> {
  const res = await fetch(`${API_BASE}/v1/procurement/recommendations`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت پیشنهادات خرید: ${res.status}`);
  }
  return res.json();
}

export async function fetchMaterialRecommendation(materialId: string, devRole?: string): Promise<PurchaseRecommendationDto> {
  const res = await fetch(`${API_BASE}/v1/procurement/recommendations/${materialId}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت پیشنهاد خرید ماده اولیه: ${res.status}`);
  }
  return res.json();
}

export async function fetchReplenishmentPolicies(devRole?: string): Promise<ReplenishmentPolicyDto[]> {
  const res = await fetch(`${API_BASE}/v1/inventory/replenishment-policies`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت خط‌مشی‌های سفارش‌دهی: ${res.status}`);
  }
  return res.json();
}

export async function updateReplenishmentPolicy(
  materialId: string,
  data: UpdateReplenishmentPolicyRequest,
  devRole?: string
): Promise<ReplenishmentPolicyDto> {
  const res = await fetch(`${API_BASE}/v1/inventory/replenishment-policies/${materialId}`, {
    method: 'PUT',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ذخیره خط‌مشی سفارش‌دهی: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 5: Procurement & Direct PO =================

export async function fetchOrders(params?: { supplierId?: string; status?: string; page?: number; pageSize?: number }, devRole?: string): Promise<PagedResult<PurchaseOrderDto>> {
  const query = new URLSearchParams();
  if (params?.supplierId) query.append('supplierId', params.supplierId);
  if (params?.status) query.append('status', params.status);
  if (params?.page) query.append('page', params.page.toString());
  if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

  const res = await fetch(`${API_BASE}/v1/procurement/orders?${query.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت سفارشات خرید: ${res.status}`);
  }
  return res.json();
}

export async function fetchOrder(id: string, devRole?: string): Promise<PurchaseOrderDto> {
  const res = await fetch(`${API_BASE}/v1/procurement/orders/${id}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت جزئیات سفارش خرید: ${res.status}`);
  }
  return res.json();
}

export async function createOrder(data: CreatePurchaseOrderRequest, devRole?: string): Promise<PurchaseOrderDto> {
  const res = await fetch(`${API_BASE}/v1/procurement/orders`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ایجاد و صدور سفارش خرید: ${res.status}`);
  }
  return res.json();
}

export async function issueOrder(id: string, devRole?: string): Promise<PurchaseOrderDto> {
  const res = await fetch(`${API_BASE}/v1/procurement/orders/${id}/issue`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در صدور سفارش خرید: ${res.status}`);
  }
  return res.json();
}

export async function cancelOrder(id: string, data: CancelPurchaseOrderRequest, devRole?: string): Promise<PurchaseOrderDto> {
  const res = await fetch(`${API_BASE}/v1/procurement/orders/${id}/cancel`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ابطال سفارش خرید: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 5: Goods Receipts =================

export async function fetchGoodsReceipts(purchaseOrderId?: string, devRole?: string): Promise<GoodsReceiptDto[]> {
  const query = purchaseOrderId ? `?purchaseOrderId=${purchaseOrderId}` : '';
  const res = await fetch(`${API_BASE}/v1/warehouse/receipts${query}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت رسیدهای انبار: ${res.status}`);
  }
  return res.json();
}

export async function fetchGoodsReceipt(id: string, devRole?: string): Promise<GoodsReceiptDto> {
  const res = await fetch(`${API_BASE}/v1/warehouse/receipts/${id}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت جزئیات رسید انبار: ${res.status}`);
  }
  return res.json();
}

export async function createGoodsReceipt(data: CreateGoodsReceiptRequest, devRole?: string): Promise<GoodsReceiptDto> {
  const res = await fetch(`${API_BASE}/v1/warehouse/receipts`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ثبت رسید انبار: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 5: Supplier Invoices =================

export async function fetchSupplierInvoices(supplierId?: string, devRole?: string): Promise<SupplierInvoiceDto[]> {
  const query = supplierId ? `?supplierId=${supplierId}` : '';
  const res = await fetch(`${API_BASE}/v1/finance/invoices${query}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت فاکتورهای تأمین‌کنندگان: ${res.status}`);
  }
  return res.json();
}

export async function fetchSupplierInvoice(id: string, devRole?: string): Promise<SupplierInvoiceDto> {
  const res = await fetch(`${API_BASE}/v1/finance/invoices/${id}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت جزئیات فاکتور: ${res.status}`);
  }
  return res.json();
}

export async function createSupplierInvoice(data: CreateSupplierInvoiceRequest, devRole?: string): Promise<SupplierInvoiceDto> {
  const res = await fetch(`${API_BASE}/v1/finance/invoices`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ثبت فاکتور تأمین‌کننده: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 5: Three-Way Matching =================

export async function evaluateThreeWayMatch(invoiceId: string, tolerance: number = 1.0, devRole?: string): Promise<ThreeWayMatchResultDto> {
  const res = await fetch(`${API_BASE}/v1/finance/matching/evaluate/${invoiceId}?tolerance=${tolerance}`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ارزیابی تطبیق سه‌طرفه: ${res.status}`);
  }
  return res.json();
}

export async function fetchMatchResultByInvoice(invoiceId: string, devRole?: string): Promise<ThreeWayMatchResultDto> {
  const res = await fetch(`${API_BASE}/v1/finance/matching/invoice/${invoiceId}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت نتیجه تطبیق: ${res.status}`);
  }
  return res.json();
}

export async function fetchMatchExceptions(devRole?: string): Promise<ThreeWayMatchResultDto[]> {
  const res = await fetch(`${API_BASE}/v1/finance/matching/exceptions`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت صف مغایرت‌ها: ${res.status}`);
  }
  return res.json();
}

export async function resolveMatchException(matchResultId: string, data: ResolveMatchingRequest, devRole?: string): Promise<ThreeWayMatchResultDto> {
  const res = await fetch(`${API_BASE}/v1/finance/matching/resolve/${matchResultId}`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در رفع مغایرت تطبیق: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 5: Sepidar Financial Reconciliation =================

export async function fetchSepidarCandidates(supplierInvoiceId: string, devRole?: string): Promise<SepidarCandidateInvoiceDto[]> {
  const res = await fetch(`${API_BASE}/v1/finance/sepidar-reconciliation/candidates/${supplierInvoiceId}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در یافتن اسناد متناظر سپیدار: ${res.status}`);
  }
  return res.json();
}

export async function linkSepidarInvoice(data: LinkSepidarInvoiceRequest, devRole?: string): Promise<SepidarFinancialReconciliationDto> {
  const res = await fetch(`${API_BASE}/v1/finance/sepidar-reconciliation/link`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در اتصال سند مالی سپیدار: ${res.status}`);
  }
  return res.json();
}

export async function unlinkSepidarInvoice(reconciliationId: string, auditNotes: string, devRole?: string): Promise<SepidarFinancialReconciliationDto> {
  const res = await fetch(`${API_BASE}/v1/finance/sepidar-reconciliation/unlink/${reconciliationId}`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify({ auditNotes })
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در لغو اتصال سند سپیدار: ${res.status}`);
  }
  return res.json();
}

export async function fetchSepidarReconciliationHistory(devRole?: string): Promise<SepidarFinancialReconciliationDto[]> {
  const res = await fetch(`${API_BASE}/v1/finance/sepidar-reconciliation/history`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت تاریخچه تطبیق سپیدار: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 6: Telephony, Calls & PBX =================

export async function fetchCalls(params?: {
  skip?: number;
  take?: number;
  extension?: string;
  direction?: number;
  status?: number;
  unknownOnly?: boolean;
}, devRole?: string): Promise<CallRecordDto[]> {
  const queryParts: string[] = [];
  if (params?.skip !== undefined) queryParts.push(`skip=${params.skip}`);
  if (params?.take !== undefined) queryParts.push(`take=${params.take}`);
  if (params?.extension) queryParts.push(`extension=${encodeURIComponent(params.extension)}`);
  if (params?.direction !== undefined) queryParts.push(`direction=${params.direction}`);
  if (params?.status !== undefined) queryParts.push(`status=${params.status}`);
  if (params?.unknownOnly !== undefined) queryParts.push(`unknownOnly=${params.unknownOnly}`);

  const queryString = queryParts.length ? `?${queryParts.join('&')}` : '';
  const res = await fetch(`${API_BASE}/v1/calls${queryString}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت تاریخچه تماس‌ها: ${res.status}`);
  }
  return res.json();
}

export async function fetchCall(id: string, devRole?: string): Promise<CallRecordDto> {
  const res = await fetch(`${API_BASE}/v1/calls/${id}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت جزئیات تماس: ${res.status}`);
  }
  return res.json();
}

export async function addCallNote(id: string, data: CreateCallNoteRequest, devRole?: string): Promise<CallNoteDto> {
  const res = await fetch(`${API_BASE}/v1/calls/${id}/notes`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ثبت یادداشت تماس: ${res.status}`);
  }
  return res.json();
}

export async function classifyCall(id: string, data: ClassifyCallRequest, devRole?: string): Promise<CallRecordDto> {
  const res = await fetch(`${API_BASE}/v1/calls/${id}/classify`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در انتساب تأمین‌کننده به تماس: ${res.status}`);
  }
  return res.json();
}

export async function matchPartiesByPhone(phone: string, devRole?: string): Promise<PartyMatchDto[]> {
  const res = await fetch(`${API_BASE}/v1/calls/match?phone=${encodeURIComponent(phone)}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در استعلام انطباق شماره: ${res.status}`);
  }
  return res.json();
}

export async function fetchUnknownCalls(devRole?: string): Promise<CallRecordDto[]> {
  const res = await fetch(`${API_BASE}/v1/calls/unknown`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت تماس‌های ناشناس: ${res.status}`);
  }
  return res.json();
}

export async function fetchFollowUps(devRole?: string): Promise<CallNoteDto[]> {
  const res = await fetch(`${API_BASE}/v1/calls/follow-ups`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت پیگیری‌ها: ${res.status}`);
  }
  return res.json();
}

export async function updateFollowUpStatus(noteId: string, status: FollowUpStatus, devRole?: string): Promise<CallNoteDto> {
  const res = await fetch(`${API_BASE}/v1/calls/follow-ups/${noteId}/status?status=${status}`, {
    method: 'PUT',
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در به‌روزرسانی وضعیت پیگیری: ${res.status}`);
  }
  return res.json();
}

export function getCallRecordingUrl(callId: string): string {
  return `${API_BASE}/v1/calls/${callId}/recording`;
}

export async function simulateCall(data: SimulateCallRequest, devRole?: string): Promise<{ message: string }> {
  const res = await fetch(`${API_BASE}/v1/calls/simulate`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در شبیه‌سازی تماس: ${res.status}`);
  }
  return res.json();
}

export async function reconcileCdrBatch(batch: CdrReconciliationDto[], devRole?: string): Promise<{ reconciledCount: number }> {
  const res = await fetch(`${API_BASE}/v1/calls/reconcile-cdr`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(batch)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در تطبیق CDR: ${res.status}`);
  }
  return res.json();
}

export async function fetchUserExtensions(devRole?: string): Promise<UserExtensionDto[]> {
  const res = await fetch(`${API_BASE}/v1/calls/extensions`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در دریافت داخلی‌های کاربران: ${res.status}`);
  }
  return res.json();
}

export async function saveUserExtension(data: UserExtensionDto, devRole?: string): Promise<UserExtensionDto> {
  const res = await fetch(`${API_BASE}/v1/calls/extensions`, {
    method: 'POST',
    headers: getHeaders(devRole),
    credentials: 'include',
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در ذخیره داخلی: ${res.status}`);
  }
  return res.json();
}

// ================= Phase 7: Reporting & Analytics APIs =================

export async function fetchProcurementSummary(filters: ReportFilterParams = {}, devRole?: string): Promise<ProcurementSummaryReportDto> {
  const params = new URLSearchParams();
  if (filters.startDateUtc) params.append('startDateUtc', filters.startDateUtc);
  if (filters.endDateUtc) params.append('endDateUtc', filters.endDateUtc);
  if (filters.supplierId) params.append('supplierId', filters.supplierId);
  if (filters.warehouseId) params.append('warehouseId', filters.warehouseId);

  const res = await fetch(`${API_BASE}/v1/reports/procurement-summary?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی گزارش تدارکات: ${res.status}`);
  }
  return res.json();
}

export async function fetchPurchaseOrderTrends(filters: ReportFilterParams = {}, devRole?: string): Promise<PurchaseOrderTrendItemDto[]> {
  const params = new URLSearchParams();
  if (filters.startDateUtc) params.append('startDateUtc', filters.startDateUtc);
  if (filters.endDateUtc) params.append('endDateUtc', filters.endDateUtc);
  if (filters.supplierId) params.append('supplierId', filters.supplierId);

  const res = await fetch(`${API_BASE}/v1/reports/purchase-order-trends?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی روندهای سفارشات: ${res.status}`);
  }
  return res.json();
}

export async function fetchSupplierPerformance(filters: ReportFilterParams = {}, devRole?: string): Promise<SupplierPerformanceReportItemDto[]> {
  const params = new URLSearchParams();
  if (filters.supplierId) params.append('supplierId', filters.supplierId);

  const res = await fetch(`${API_BASE}/v1/reports/supplier-performance?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی عملکرد تأمین‌کنندگان: ${res.status}`);
  }
  return res.json();
}

export async function fetchMaterialConsumption(filters: ReportFilterParams = {}, devRole?: string): Promise<MaterialConsumptionReportItemDto[]> {
  const params = new URLSearchParams();
  if (filters.categoryId) params.append('categoryId', filters.categoryId);

  const res = await fetch(`${API_BASE}/v1/reports/material-consumption?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی مصرف کالاها: ${res.status}`);
  }
  return res.json();
}

export async function fetchInventoryHealth(filters: ReportFilterParams = {}, devRole?: string): Promise<InventoryHealthReportDto> {
  const params = new URLSearchParams();
  if (filters.categoryId) params.append('categoryId', filters.categoryId);

  const res = await fetch(`${API_BASE}/v1/reports/inventory-health?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی سلامت انبار: ${res.status}`);
  }
  return res.json();
}

export async function fetchFinanceReconciliationReport(filters: ReportFilterParams = {}, devRole?: string): Promise<FinanceReconciliationReportDto> {
  const params = new URLSearchParams();
  if (filters.startDateUtc) params.append('startDateUtc', filters.startDateUtc);
  if (filters.endDateUtc) params.append('endDateUtc', filters.endDateUtc);
  if (filters.supplierId) params.append('supplierId', filters.supplierId);

  const res = await fetch(`${API_BASE}/v1/reports/finance-reconciliation?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی انطباق مالی: ${res.status}`);
  }
  return res.json();
}

export async function fetchTelephonyActivityReport(filters: ReportFilterParams = {}, devRole?: string): Promise<TelephonyActivityReportDto> {
  const params = new URLSearchParams();
  if (filters.startDateUtc) params.append('startDateUtc', filters.startDateUtc);
  if (filters.endDateUtc) params.append('endDateUtc', filters.endDateUtc);

  const res = await fetch(`${API_BASE}/v1/reports/telephony-activity?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی فعالیت‌های تلفنی: ${res.status}`);
  }
  return res.json();
}

export async function fetchIntegrationHealth(devRole?: string): Promise<IntegrationHealthReportDto> {
  const res = await fetch(`${API_BASE}/v1/reports/integration-health`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی سلامت اتصالات: ${res.status}`);
  }
  return res.json();
}

export async function fetchRoleDashboard(role: string, filters: ReportFilterParams = {}, devRole?: string): Promise<RoleDashboardDto> {
  const params = new URLSearchParams();
  if (filters.startDateUtc) params.append('startDateUtc', filters.startDateUtc);
  if (filters.endDateUtc) params.append('endDateUtc', filters.endDateUtc);

  const res = await fetch(`${API_BASE}/v1/reports/role-dashboard/${role}?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در واکشی داشبورد ${role}: ${res.status}`);
  }
  return res.json();
}

export async function exportReport(request: ExportRequestDto, devRole?: string): Promise<void> {
  const params = new URLSearchParams();
  params.append('reportType', request.reportType);
  params.append('format', request.format);
  if (request.startDateUtc) params.append('startDateUtc', request.startDateUtc);
  if (request.endDateUtc) params.append('endDateUtc', request.endDateUtc);
  if (request.supplierId) params.append('supplierId', request.supplierId);
  if (request.categoryId) params.append('categoryId', request.categoryId);

  const res = await fetch(`${API_BASE}/v1/reports/export?${params.toString()}`, {
    headers: getHeaders(devRole),
    credentials: 'include'
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || `خطا در خروجی فایل: ${res.status}`);
  }

  const blob = await res.blob();
  const disposition = res.headers.get('content-disposition');
  let filename = `${request.reportType}.${request.format === 'xlsx' ? 'xls' : 'csv'}`;
  if (disposition && disposition.indexOf('filename=') !== -1) {
    const matches = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(disposition);
    if (matches != null && matches[1]) {
      filename = matches[1].replace(/['"]/g, '');
    }
  }

  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  window.URL.revokeObjectURL(url);
  document.body.removeChild(a);
}

