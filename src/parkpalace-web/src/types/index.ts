export type AppRole = 'Admin' | 'Procurement' | 'Manager' | 'Warehouse' | 'Finance';

export interface MeResponse {
  id?: string;
  username: string;
  displayName: string;
  email?: string;
  extension?: string;
  adSid?: string;
  isAuthenticated: boolean;
  roles: string[];
  permissions: string[];
  activeRole?: string;
  availableRoles?: string[];
}

export interface UserDto {
  id: string;
  username: string;
  displayName: string;
  email?: string;
  adObjectSid?: string;
  isEnabled: boolean;
  lastLoginAtUtc?: string;
  roles: string[];
  extension?: string;
  deviceType?: string;
  department?: string;
  jobTitle?: string;
  adDepartment?: string;
  adTitle?: string;
  telephonyGroupId?: string;
  telephonyGroupName?: string;
  isSupervisor?: boolean;
}

export interface UserAccessPreviewDto {
  id: string;
  username: string;
  displayName: string;
  extension?: string;
  activeRole: string;
  roles: string[];
  availableRoles: string[];
  permissions: string[];
}

export interface UpdateUserRequest {
  roles?: string[];
  extension?: string;
  deviceType?: string;
  department?: string;
  jobTitle?: string;
  telephonyGroupId?: string;
  isSupervisor?: boolean;
}

export interface AdUserDirectoryDto {
  samAccountName: string;
  displayName: string;
  email?: string;
  title?: string;
  department?: string;
  telephoneNumber?: string;
  ipPhone?: string;
  mobile?: string;
  sid: string;
  isDisabled: boolean;
  isImportedInCrm: boolean;
  existingUserId?: string;
  existingRoles: string[];
  existingExtension?: string;
}

export interface UpdateUserRolesRequest {
  roles: string[];
}

export interface UpdateUserExtensionRequest {
  extension?: string;
  deviceType?: string;
  description?: string;
}

export interface UpdateUserStatusRequest {
  isEnabled: boolean;
}

export interface AvailableExtensionDto {
  extension: string;
  displayName: string;
  deviceType: string;
  isAssigned: boolean;
  assignedUserId?: string;
  assignedUsername?: string;
  assignedDisplayName?: string;
  holderName?: string;
  status: string;
}

export interface SyncAdUsersRequest {
  samAccountNames?: string[];
  defaultRole?: string;
  overwriteExistingExtensions?: boolean;
}

export interface HealthResponse {
  status: string;
  service: string;
  version: string;
  serverTimeUtc: string;
  databaseConnected: boolean;
  sepidarIntegrationStatus: string;
}

export interface RoleDto {
  id: string;
  name: string;
  displayName?: string;
  description: string;
  permissions: string[];
  isSystemRole?: boolean;
}

export interface PermissionDto {
  id: string;
  code: string;
  category: string;
  description: string;
}

export interface CreateRoleRequest {
  name: string;
  displayName: string;
  description?: string;
  permissions: string[];
}

export interface UpdateRoleRequest {
  displayName: string;
  description?: string;
  permissions?: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface DirectoryPartyListItem {
  id: string;
  displayName: string;
  crmTitle?: string;
  sepidarTitle?: string;
  sepidarCode?: string;
  sepidarPartyRef?: string;
  isSupplier: boolean;
  isVendor?: boolean;
  isCustomer?: boolean;
  hasCrmProfile?: boolean;
  sepidarMappingVerified: boolean;
  financialCalculationVerified?: boolean;
  category?: string;
}

// Category
export interface CategoryDto {
  id: string;
  parentId?: string;
  name: string;
  code: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  materialCount: number;
  childrenCount: number;
}

export interface CategoryTreeDto {
  id: string;
  parentId?: string;
  name: string;
  code: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  materialCount: number;
  children: CategoryTreeDto[];
}

// Unit Of Measure
export interface UnitOfMeasureDto {
  id: string;
  code: string;
  name: string;
  precision: number;
  dimension: string;
  isActive: boolean;
}

// Material
export interface MaterialDto {
  id: string;
  internalCode: string;
  name: string;
  categoryId: string;
  categoryName: string;
  baseUnitId: string;
  baseUnitName: string;
  baseUnitCode: string;
  description?: string;
  specifications?: string;
  brand?: string;
  packaging?: string;
  packageSize?: number;
  packageSizeUnitId?: string;
  packageSizeUnitName?: string;
  packageSizeUnitCode?: string;
  purchaseUnitId?: string;
  purchaseUnitName?: string;
  purchaseUnitCode?: string;
  consumptionUnitId?: string;
  consumptionUnitName?: string;
  consumptionUnitCode?: string;
  productFamily?: string;
  categorizationStatus?: string;
  isCustomizedByUser?: boolean;
  standardizationNotes?: string;
  sepidarItemCode?: string;
  sepidarItemTitle?: string;
  sepidarBaseUnit?: string;
  minStock?: number;
  safetyStock?: number;
  leadTimeDays?: number;
  isActive: boolean;
  supplierCount: number;
  conversionCount: number;
}

export interface MaterialUnitConversionDto {
  id: string;
  materialId: string;
  fromUnitId: string;
  fromUnitName: string;
  fromUnitCode: string;
  toUnitId: string;
  toUnitName: string;
  toUnitCode: string;
  factor: number;
  isConfirmed: boolean;
  notes?: string;
}

export interface SupplierMaterialSummaryDto {
  supplierId: string;
  supplierName: string;
  isPreferred: boolean;
  status: string;
  latestPrice?: number;
  currency?: string;
  priceUnitName?: string;
  priceEffectiveFrom?: string;
}

export interface MaterialDetailDto extends MaterialDto {
  unitConversions: MaterialUnitConversionDto[];
  suppliers: SupplierMaterialSummaryDto[];
}

// Supplier & Party
export type PartyType = 1 | 2; // 1 = Individual, 2 = LegalEntity
export type PartyCategory = 1 | 2 | 3 | 4; // 1 = Supplier, 2 = ServiceProvider, 3 = Customer, 4 = OtherCounterparty

export interface PartyDto {
  id: string;
  partyType: PartyType;
  category?: PartyCategory;
  categoryLabel?: string;
  displayName: string;
  crmTitle?: string;
  sepidarCode?: string;
  sepidarTitle?: string;
  sepidarPartyRef?: string;
  isActive: boolean;
  notes?: string;
  firstName?: string;
  lastName?: string;
  shopName?: string;
  nationalId?: string;
  legalName?: string;
  nationalIdentifier?: string;
  registrationNumber?: string;
  economicCode?: string;
  assignedDepartment?: string;
  assignedGroupId?: string;
  assignedGroupName?: string;
  assignedUserId?: string;
  assignedUserName?: string;
  primaryPhone?: string;
  primaryContactName?: string;
  primaryAddressCity?: string;
  primaryAddressProvince?: string;
  primaryAddressText?: string;
  primaryAddressPostalCode?: string;
  suppliedMaterialsCount: number;
  purchaseOrdersCount?: number;
  isSepidarLinked?: boolean;
  canDelete?: boolean;
}

export interface PartyContactDto {
  id: string;
  partyId: string;
  personName: string;
  position?: string;
  isPrimary: boolean;
  isActive: boolean;
  phones: PartyPhoneDto[];
}

export interface PartyPhoneDto {
  id: string;
  partyId: string;
  contactId?: string;
  contactName?: string;
  phoneType: string;
  originalValue: string;
  normalizedValue: string;
  isPrimary: boolean;
}

export interface PartyAddressDto {
  id: string;
  partyId: string;
  addressType: string;
  province: string;
  city: string;
  addressText: string;
  postalCode?: string;
  isPrimary: boolean;
}

export interface SupplierPriceHistoryDto {
  id: string;
  supplierMaterialId: string;
  price: number;
  currency: string;
  unitId: string;
  unitName: string;
  effectiveFrom: string;
  effectiveTo?: string;
  recordedAt: string;
  source?: string;
  notes?: string;
}

export interface SupplierMaterialDto {
  id: string;
  partyId: string;
  supplierName: string;
  materialId: string;
  materialName: string;
  materialCode: string;
  isPreferred: boolean;
  status: string;
  defaultLeadTimeDays?: number;
  minimumOrderQty?: number;
  purchaseUnitId?: string;
  purchaseUnitName?: string;
  notes?: string;
  priceHistories: SupplierPriceHistoryDto[];
}

export interface PartyDetailDto extends PartyDto {
  contacts: PartyContactDto[];
  phones: PartyPhoneDto[];
  addresses: PartyAddressDto[];
  suppliedMaterials: SupplierMaterialDto[];
}

// Phase 4: Inventory & Warehouse Types
export interface WarehouseDto {
  id: string;
  code: string;
  name: string;
  description?: string;
  sepidarWarehouseRef?: string;
  isActive: boolean;
  totalItemsCount: number;
  criticalItemsCount: number;
  staleSnapshotsCount: number;
  lastSyncUtc?: string;
  freshnessStatus: string;
}

export interface InventoryItemDto {
  materialId: string;
  internalCode: string;
  name: string;
  categoryName: string;
  unitName: string;
  unitCode: string;
  onHandQuantity: number;
  reservableQuantity: number;
  safetyStock: number;
  dailyConsumption7d: number;
  dailyConsumption30d: number;
  isStale: boolean;
  isUnitMismatch?: boolean;
  lastSyncUtc: string;
  stockStatus: string; // 'Normal' | 'Critical' | 'OutOfStock' | 'Stale' | 'UnitMismatch'
}

export interface SupplierMaterialSummaryDto {
  supplierId: string;
  supplierName: string;
  isPreferred: boolean;
  status: string;
}

export interface InventoryItemDetailDto {
  materialId: string;
  internalCode: string;
  name: string;
  categoryName: string;
  unitName: string;
  unitCode: string;
  onHandQuantity: number;
  reservableQuantity: number;
  reservedQuantity: number;
  incomingQuantity: number;
  safetyStock: number;
  minStock: number;
  leadTimeDays: number;
  reviewPeriodDays: number;
  dailyConsumption7d: number;
  dailyConsumption30d: number;
  isStale: boolean;
  lastSnapshotUtc: string;
  stockStatus: string;
  sepidarItemId?: string;
  suppliers: SupplierMaterialSummaryDto[];
  currentPolicy?: ReplenishmentPolicyDto;
}

export interface InventoryMovementDto {
  id: string;
  voucherNumber: string;
  movementType: string;
  quantity: number;
  unitName: string;
  unitCode: string;
  transactionDateUtc: string;
  postingStatus: string;
  isCancelled: boolean;
  referenceDocument?: string;
  importedAtUtc: string;
}

export interface ConsumptionSummaryDto {
  materialId: string;
  materialName: string;
  baseUnit: string;
  consumption7d: number;
  averageDaily7d: number;
  activeDays7d: number;
  consumption14d: number;
  averageDaily14d: number;
  activeDays14d: number;
  consumption30d: number;
  averageDaily30d: number;
  activeDays30d: number;
  calculatedAtUtc: string;
}

export interface SyncStatusDto {
  lastSuccessfulSyncUtc?: string;
  lastJobStatus?: string;
  lastJobStartedUtc?: string;
  lastJobCompletedUtc?: string;
  lastErrorMessage?: string;
  isStale: boolean;
  freshnessStatus: string;
  checkpointVoucherRef?: string;
  totalSyncedItems: number;
}

export interface SyncJobDto {
  id: string;
  jobType: string;
  startedAtUtc: string;
  completedAtUtc?: string;
  status: string;
  itemsProcessed: number;
  movementsProcessed: number;
  snapshotsUpdated: number;
  errorMessage?: string;
}

export interface AlternativeSupplierDto {
  supplierId: string;
  supplierName: string;
  latestPrice?: number;
  currency?: string;
  status: string;
}

export interface PurchaseRecommendationDto {
  id: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  baseUnit: string;
  usableOnHand: number;
  incomingOrders: number;
  reservedQuantity: number;
  inventoryPosition: number;
  averageDailyConsumption: number;
  leadTimeDays: number;
  reviewPeriodDays: number;
  safetyStock: number;
  targetStock: number;
  suggestedPurchaseQuantity: number;
  minOrderQuantity: number;
  packageMultiple?: number;
  preferredSupplierId?: string;
  preferredSupplierName?: string;
  preferredSupplierPrice?: number;
  recommendationExplanation: string;
  hasStaleInventoryWarning: boolean;
  status: string;
  generatedAtUtc: string;
  alternativeSuppliers: AlternativeSupplierDto[];
  reservableOnHand?: number | null;
  dataWarning?: string;
  isDefinitive?: boolean;
  requiresManualReview?: boolean;
  reserveStatusDescription?: string;
}

export interface ReplenishmentPolicyDto {
  materialId: string;
  materialName: string;
  reviewPeriodDays: number;
  leadTimeDays: number;
  safetyStock: number;
  minOrderQuantity: number;
  packageMultiple?: number;
  preferredSupplierId?: string;
  preferredSupplierName?: string;
  notes?: string;
}

export interface UpdateReplenishmentPolicyRequest {
  reviewPeriodDays: number;
  leadTimeDays: number;
  safetyStock: number;
  minOrderQuantity: number;
  packageMultiple?: number;
  preferredSupplierId?: string;
  notes?: string;
}

// Phase 5: Procurement, Receipts, Invoices, 3-Way Matching & Sepidar Reconciliation
export interface PurchaseOrderLineDto {
  id: string;
  lineNumber: number;
  materialId: string;
  materialCode: string;
  materialName: string;
  orderedQuantity: number;
  unitId: string;
  unitName: string;
  unitCode: string;
  unitPrice: number;
  taxRate: number;
  taxAmount: number;
  discountAmount: number;
  lineTotal: number;
  receivedQuantity: number;
  invoicedQuantity: number;
  materialCodeSnapshot?: string;
  materialNameSnapshot?: string;
  unitCodeSnapshot?: string;
  unitNameSnapshot?: string;
  supplierNameSnapshot?: string;
  notes?: string;
}

export interface PurchaseOrderAuditDto {
  id: string;
  action: string;
  performedBy: string;
  timestampUtc: string;
  detailsJson?: string;
}

export interface PurchaseOrderDto {
  id: string;
  orderNumber: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  status: string;
  currency: string;
  exchangeRate: number;
  subTotal: number;
  taxTotal: number;
  discountTotal: number;
  grandTotal: number;
  issuedByUsername?: string;
  issuedAtUtc?: string;
  expectedDeliveryDateUtc?: string;
  paymentTerms?: string;
  deliveryTerms?: string;
  notes?: string;
  concurrencyToken: string;
  createdAtUtc: string;
  createdBy: string;
  lineCount: number;
  totalOrderedQuantity: number;
  totalReceivedQuantity: number;
  totalInvoicedQuantity: number;
  lines: PurchaseOrderLineDto[];
  audits: PurchaseOrderAuditDto[];
}

export interface CreatePurchaseOrderLineRequest {
  materialId: string;
  orderedQuantity: number;
  unitId?: string;
  unitPrice: number;
  taxRate?: number;
  discountAmount?: number;
  notes?: string;
}

export interface CreatePurchaseOrderRequest {
  supplierId: string;
  warehouseId?: string;
  expectedDeliveryDateUtc?: string;
  paymentTerms?: string;
  deliveryTerms?: string;
  notes?: string;
  directIssue: boolean;
  manualReviewConfirmed?: boolean;
  lines: CreatePurchaseOrderLineRequest[];
}

export interface CancelPurchaseOrderRequest {
  reason: string;
  concurrencyToken: string;
}

export interface GoodsReceiptLineDto {
  id: string;
  purchaseOrderLineId: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  deliveredQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  unitName: string;
  unitCode: string;
  qcStatus: string;
  rejectionReason?: string;
  batchNumber?: string;
  expiryDateUtc?: string;
  storageLocation?: string;
  notes?: string;
}

export interface GoodsReceiptDto {
  id: string;
  receiptNumber: string;
  purchaseOrderId: string;
  orderNumber: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  receivedByUsername: string;
  receiptDateUtc: string;
  carrierName?: string;
  deliveryNoteNumber?: string;
  vehiclePlateNumber?: string;
  status: string;
  sepidarMovementRef?: string;
  notes?: string;
  lines: GoodsReceiptLineDto[];
}

export interface CreateGoodsReceiptLineRequest {
  purchaseOrderLineId: string;
  deliveredQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity?: number;
  qcStatus: string;
  rejectionReason?: string;
  batchNumber?: string;
  expiryDateUtc?: string;
  storageLocation?: string;
  notes?: string;
}

export interface CreateGoodsReceiptRequest {
  purchaseOrderId: string;
  warehouseId?: string;
  carrierName?: string;
  deliveryNoteNumber?: string;
  vehiclePlateNumber?: string;
  sepidarMovementRef?: string;
  notes?: string;
  lines: CreateGoodsReceiptLineRequest[];
}

export interface SupplierInvoiceLineDto {
  id: string;
  purchaseOrderLineId?: string;
  goodsReceiptLineId?: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  invoicedQuantity: number;
  unitName: string;
  unitCode: string;
  unitPrice: number;
  taxAmount: number;
  discountAmount: number;
  lineTotal: number;
  description?: string;
}

export interface SupplierInvoiceDto {
  id: string;
  invoiceNumber: string;
  supplierId: string;
  supplierName: string;
  purchaseOrderId?: string;
  orderNumber?: string;
  invoiceDateUtc: string;
  dueDateUtc?: string;
  receivedDateUtc: string;
  currency: string;
  subTotal: number;
  taxTotal: number;
  discountTotal: number;
  totalAmount: number;
  status: string;
  registeredInSepidar: boolean;
  sepidarInvoiceRef?: string;
  sepidarVoucherNumber?: string;
  attachmentFileName?: string;
  notes?: string;
  lines: SupplierInvoiceLineDto[];
}

export interface CreateSupplierInvoiceLineRequest {
  purchaseOrderLineId?: string;
  goodsReceiptLineId?: string;
  materialId: string;
  invoicedQuantity: number;
  unitId?: string;
  unitPrice: number;
  taxAmount?: number;
  discountAmount?: number;
  description?: string;
}

export interface CreateSupplierInvoiceRequest {
  invoiceNumber: string;
  supplierId: string;
  purchaseOrderId?: string;
  invoiceDateUtc: string;
  dueDateUtc?: string;
  currency?: string;
  taxTotal?: number;
  discountTotal?: number;
  notes?: string;
  lines: CreateSupplierInvoiceLineRequest[];
}

export interface ThreeWayMatchLineVarianceDto {
  id: string;
  purchaseOrderLineId?: string;
  goodsReceiptLineId?: string;
  supplierInvoiceLineId?: string;
  materialId: string;
  materialCode: string;
  materialName: string;
  poQuantity: number;
  receivedQuantity: number;
  invoicedQuantity: number;
  quantityVariance: number;
  poUnitPrice: number;
  invoicedUnitPrice: number;
  priceVariance: number;
  priceVariancePercent: number;
  poLineTotal: number;
  invoicedLineTotal: number;
  totalVariance: number;
  mismatchType: string;
  notes?: string;
}

export interface ThreeWayMatchResultDto {
  id: string;
  purchaseOrderId: string;
  orderNumber: string;
  goodsReceiptId?: string;
  receiptNumber?: string;
  supplierInvoiceId: string;
  invoiceNumber: string;
  supplierName: string;
  evaluatedAtUtc: string;
  evaluatedBy: string;
  matchStatus: string;
  totalVarianceAmount: number;
  tolerancePercentageApplied: number;
  isResolved: boolean;
  resolutionAction?: string;
  resolvedBy?: string;
  resolvedAtUtc?: string;
  resolutionNotes?: string;
  lineVariances: ThreeWayMatchLineVarianceDto[];
}

export interface ResolveMatchingRequest {
  resolutionAction: string;
  resolutionNotes: string;
}

export interface SepidarCandidateInvoiceDto {
  sepidarInvoiceId: string;
  sepidarVoucherNumber: string;
  voucherDateUtc: string;
  supplierName: string;
  supplierNationalCode?: string;
  amount: number;
  currency: string;
  confidenceScore: number;
  matchConfidenceLevel: string;
  matchReason: string;
}

export interface SepidarFinancialReconciliationDto {
  id: string;
  supplierInvoiceId: string;
  invoiceNumber: string;
  supplierName: string;
  sepidarInvoiceId: string;
  sepidarVoucherNumber: string;
  sepidarVoucherDateUtc: string;
  sepidarAmount: number;
  candidateConfidenceScore: number;
  linkStatus: string;
  linkedBy?: string;
  linkedAtUtc?: string;
  auditNotes?: string;
}

export interface LinkSepidarInvoiceRequest {
  supplierInvoiceId: string;
  sepidarInvoiceId: string;
  sepidarVoucherNumber: string;
  sepidarVoucherDateUtc: string;
  sepidarAmount: number;
  auditNotes?: string;
}

export interface UnlinkPayload {
  auditNotes?: string;
}

// Phase 6: Telephony, Calls & PBX
export type CallDirection = 0 | 1 | 2; // 0: Incoming, 1: Outgoing, 2: Internal
export type CallScope = 0 | 1 | 2; // 0: Unknown, 1: Trunk, 2: Internal
export type CallClassificationFilter = 'all' | 'inbound_trunk' | 'outbound_trunk' | 'internal_inbound' | 'internal_outbound';
export type CallStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6; // 0: Ringing, 1: Answered, 2: Missed, 3: Busy, 4: Failed, 5: Transferred, 6: Completed
export type CallReconciliationStatus = 0 | 1 | 2 | 3 | 4; // 0: Pending, 1: Reconciled, 2: Uncorrelated, 3: Ambiguous, 4: ManualReview
export type FollowUpStatus = 0 | 1 | 2; // 0: Pending, 1: Completed, 2: Cancelled

export interface CallRecordDto {
  id: string;
  uniqueId: string;
  linkedId: string;
  direction: CallDirection;
  scope?: CallScope;
  status: CallStatus;
  callerNumber: string;
  normalizedCallerNumber: string;
  callerName?: string;
  destinationNumber: string;
  internalCallerName?: string;
  internalDestinationName?: string;
  extension?: string;
  channel?: string;
  destinationChannel?: string;
  startTimeUtc: string;
  answerTimeUtc?: string;
  endTimeUtc?: string;
  durationSeconds?: number;
  billableSeconds?: number;
  partyId?: string;
  partyName?: string;
  partyCode?: string;
  partyCategory?: PartyCategory;
  contactId?: string;
  contactName?: string;
  contactPosition?: string;
  assignedDepartment?: string;
  assignedGroupId?: string;
  assignedGroupName?: string;
  assignedUserId?: string;
  assignedUserName?: string;
  isUnknownCaller: boolean;
  matchConfidence: string;
  recordingFileName?: string;
  hasRecording: boolean;
  recordingDurationSeconds?: number;
  recordingFileSize?: number;
  cdrId?: string;
  reconciliationStatus: CallReconciliationStatus;
  disconnectionCause?: string;
  notesCount: number;
  notes: CallNoteDto[];
  audits: CallAuditDto[];
}

export interface CallNoteDto {
  id: string;
  callRecordId: string;
  userId?: string;
  username: string;
  noteText: string;
  outcome: string;
  followUpRequired: boolean;
  followUpDueDateUtc?: string;
  followUpAssignedTo?: string;
  followUpStatus: FollowUpStatus;
  createdAtUtc: string;
}

export interface CallAuditDto {
  id: string;
  callRecordId: string;
  action: string;
  performedBy: string;
  timestampUtc: string;
  detailsJson?: string;
}

export interface UserExtensionDto {
  id: string;
  userId?: string;
  username: string;
  extension: string;
  deviceType: string;
  isActive: boolean;
  description?: string;
}

export interface PartyMatchDto {
  partyId: string;
  partyName: string;
  partyCode: string;
  partyType?: PartyType;
  category?: PartyCategory;
  categoryLabel?: string;
  contactId?: string;
  contactName?: string;
  contactRole?: string;
  phoneNumber: string;
  matchType: string;
}

export interface RecentCallSummaryDto {
  id: string;
  direction: CallDirection;
  status: CallStatus;
  startTimeUtc: string;
  durationSeconds?: number;
  extension?: string;
  hasRecording: boolean;
  noteSummary?: string;
}

export interface CallerPopupDto {
  callId: string;
  uniqueId: string;
  linkedId: string;
  direction: CallDirection;
  status: CallStatus;
  callerNumber: string;
  normalizedCallerNumber: string;
  callerName?: string;
  destinationNumber: string;
  extension?: string;
  timestampUtc: string;
  matchStatus: string;
  matchedParties: PartyMatchDto[];
  recentCalls: RecentCallSummaryDto[];
}

export interface CreateCallNoteRequest {
  noteText: string;
  outcome: string;
  followUpRequired: boolean;
  followUpDueDateUtc?: string;
  followUpAssignedTo?: string;
}

export interface ClassifyCallRequest {
  partyId?: string;
  contactId?: string;
  newContactName?: string;
  newContactPosition?: string;
  createNewParty?: boolean;
  newPartyType?: number;
  newPartyName?: string;
  newPartyCity?: string;
  createStandaloneContact?: boolean;
  standaloneName?: string;
  standalonePosition?: string;
  partyCategory?: PartyCategory;
  assignedDepartment?: string;
  assignedGroupId?: string;
  assignedUserId?: string;
}

export interface UpdateCallerAssociationRequest {
  partyId?: string;
  contactId?: string;
  assignedDepartment?: string;
  assignedGroupId?: string;
  assignedUserId?: string;
  reason?: string;
}

export interface SepidarAccountMovementDto {
  voucherItemId: string;
  voucherNumber: string;
  voucherDateUtc: string;
  debit: number;
  credit: number;
  description: string;
  cumulativeBalance: number;
}

export interface SepidarPartyFinancialHistoryDto {
  isConnected: boolean;
  isAvailable: boolean;
  partyRef?: string;
  sepidarCode?: string;
  sepidarTitle?: string;
  fiscalPeriod: string;
  fiscalYear?: number;
  currency: string;
  retrievedAtUtc: string;
  openingBalance: number;
  totalDebit: number;
  totalCredit: number;
  balance: number;
  netBalance?: number;
  balanceType: string;
  movements: SepidarAccountMovementDto[];
  statusMessage?: string;
  message?: string;
  unverifiedDependencyDetails?: string;
}

export interface SimulateCallRequest {
  callerNumber: string;
  destinationNumber: string;
  extension: string;
  callerName?: string;
  callType: string;
  durationSeconds: number;
}

export interface CdrReconciliationDto {
  uniqueId: string;
  linkedId?: string;
  callerNumber: string;
  destinationNumber: string;
  startTimeUtc: string;
  answerTimeUtc?: string;
  endTimeUtc: string;
  durationSeconds: number;
  billableSeconds: number;
  disposition: string;
  recordingFile?: string;
}

export type ViewType = 
  | 'dashboard'
  | 'executiveDashboard'
  | 'materials'
  | 'suppliers'
  | 'sepidarParties'
  | 'contacts'
  | 'orders'
  | 'warehouse'
  | 'finance'
  | 'financeDashboard'
  | 'calls'
  | 'admin';

// ================= Phase 7: Reporting, Analytics & Role Dashboards =================

export type DashboardRole = 'Procurement' | 'Manager' | 'Warehouse' | 'Finance' | 'Admin';

export enum DataFreshnessStatus {
  LiveVerified = 1,
  CachedSync = 2,
  Stale = 3,
  Simulated = 4,
  Unavailable = 5
}

export interface ReportFilterParams {
  startDateUtc?: string;
  endDateUtc?: string;
  categoryId?: string;
  supplierId?: string;
  warehouseId?: string;
  status?: string;
  searchTerm?: string;
  page?: number;
  pageSize?: number;
}

export interface ProcurementSummaryReportDto {
  totalPurchaseVolume: number;
  totalOrdersCount: number;
  activeOrdersCount: number;
  completedOrdersCount: number;
  cancelledOrdersCount: number;
  averageOrderValue: number;
  orderFulfillmentRate: number;
  onTimeDeliveryRate: number;
  criticalStockCount: number;
  lowStockCount: number;
  activeRecommendationsCount: number;
  recommendationsEstimatedValue: number;
  dataFreshness: DataFreshnessStatus;
  freshnessWarning?: string;
  generatedAtUtc: string;
}

export interface PurchaseOrderTrendItemDto {
  period: string;
  dateUtc: string;
  jalaliDate: string;
  orderCount: number;
  totalAmount: number;
  receivedAmount: number;
  fulfilledCount: number;
}

export interface SupplierPerformanceReportItemDto {
  supplierId: string;
  supplierName: string;
  totalOrdersCount: number;
  totalSpend: number;
  averageDeliveryLeadTimeDays: number;
  onTimeDeliveryRate: number;
  fulfillmentRate: number;
  partialReceiptRate: number;
  qualityAcceptanceRate: number;
  totalCallsCount: number;
  lastContactDateUtc?: string;
}

export interface MaterialConsumptionReportItemDto {
  materialId: string;
  materialCode: string;
  materialName: string;
  categoryName: string;
  unitName: string;
  totalConsumedQuantity: number;
  averageDailyConsumption: number;
  currentStock: number;
  coverageDays: number;
  inventoryRiskStatus: string;
  lastMovementDateUtc?: string;
}

export interface InventoryHealthItemDto {
  materialId: string;
  materialCode: string;
  materialName: string;
  categoryName: string;
  unitName: string;
  currentOnHand: number;
  safetyStock: number;
  reorderPoint: number;
  lastUnitPrice: number;
  totalStockValue: number;
  coverageDays: number;
  stockStatus: string;
  isStale: boolean;
  lastMovementDateUtc?: string;
}

export interface InventoryHealthReportDto {
  totalSkus: number;
  totalInventoryValue: number;
  criticalStockCount: number;
  lowStockCount: number;
  healthyStockCount: number;
  overstockedCount: number;
  staleInventoryCount: number;
  lastSyncDateUtc?: string;
  syncStatus: string;
  dataFreshness: DataFreshnessStatus;
  freshnessWarning?: string;
  items: InventoryHealthItemDto[];
  totalCount: number;
}

export interface FinanceExceptionItemDto {
  matchResultId: string;
  purchaseOrderId: string;
  orderNumber: string;
  supplierInvoiceId: string;
  invoiceNumber: string;
  supplierName: string;
  invoiceDateUtc: string;
  invoicedAmount: number;
  receivedValue: number;
  varianceAmount: number;
  variancePercent: number;
  matchStatus: string;
  isResolved: boolean;
  resolutionAction?: string;
  resolutionNotes?: string;
  resolvedBy?: string;
  resolvedAtUtc?: string;
  hasSepidarLink: boolean;
  sepidarVoucherNumber?: string;
}

export interface FinanceReconciliationReportDto {
  totalInvoicedAmount: number;
  totalMatchedAmount: number;
  totalVarianceAmount: number;
  matchSuccessRate: number;
  openExceptionsCount: number;
  priceMismatchCount: number;
  quantityMismatchCount: number;
  missingReceiptCount: number;
  sepidarBacklogCount: number;
  sepidarLinkedCount: number;
  exceptions: FinanceExceptionItemDto[];
  totalExceptionsCount: number;
}

export interface TelephonyActivityReportDto {
  totalCalls: number;
  incomingCalls: number;
  outgoingCalls: number;
  answeredCalls: number;
  missedCalls: number;
  missedCallRate: number;
  averageCallDurationSeconds: number;
  supplierIdentifiedCalls: number;
  unknownCallerCalls: number;
  pendingFollowUpsCount: number;
  overdueFollowUpsCount: number;
  pbxConnectionStatus: string;
  dataFreshness: DataFreshnessStatus;
  freshnessWarning?: string;
}

export interface SepidarAdapterHealthDto {
  adapterName: string;
  adapterType: string;
  isLiveConnected: boolean;
  status: string;
  lastSyncUtc?: string;
  freshnessNote: string;
}

export interface IssabelAdapterHealthDto {
  adapterName: string;
  adapterType: string;
  isLiveConnected: boolean;
  status: string;
  eventsProcessed: number;
  reconnectionRetries: number;
  freshnessNote: string;
}

export interface InventorySyncHealthDto {
  lastRunUtc?: string;
  lastRunStatus: string;
  itemsProcessed: number;
  errorsCount: number;
  isStale: boolean;
  elapsedSinceLastSync: string;
}

export interface IntegrationHealthReportDto {
  apiHealth: string;
  databaseHealth: string;
  databaseLatencyMs: number;
  sepidarAdapter: SepidarAdapterHealthDto;
  issabelAdapter: IssabelAdapterHealthDto;
  inventorySync: InventorySyncHealthDto;
  freshnessWarnings: string[];
  checkedAtUtc: string;
}

export interface DashboardActionItemDto {
  id: string;
  title: string;
  subtitle: string;
  severity: 'Normal' | 'Warning' | 'Critical';
  targetView: string;
  timestampUtc: string;
  jalaliDate: string;
}

export type FinancialFigureStatusName = 'Verified' | 'Unverified' | 'Blocked' | 'Unavailable' | 'NoData' | 'Zero';

export interface FinancialFigureDto {
  code: string;
  title: string;
  amount?: number | null;
  status: FinancialFigureStatusName | number;
  reason: string;
}

export interface FinancialDashboardDto {
  generatedAtUtc: string;
  sepidarReadAttempted: boolean;
  freshness: string;
  freshnessDetail: string;
  lastSuccessfulFinancialReadUtc?: string | null;
  catalogFiscalYear?: {
    fiscalYearId: number;
    title: string;
    startDate: string;
    endDate: string;
    evidence: string;
    liveRead: boolean;
  } | null;
  comparisonStatus?: string | null;
  currentPeriodFromUtc?: string | null;
  currentPeriodToUtc?: string | null;
  previousPeriodFromUtc?: string | null;
  previousPeriodToUtc?: string | null;
  fiscalYears?: Array<{ fiscalYearId: number; title: string; startDate: string; endDate: string }>;
  accountScope?: Array<{ code: string; title: string; side: string }>;
  cards: FinancialFigureDto[];
  monthlyComparison: FinancialFigureDto[];
  monthly?: Array<{ monthStart: string; side: string; net: number; previousPeriod: boolean }>;
  dueDates: FinancialFigureDto;
  largestBalances: FinancialFigureDto;
  topParties?: Array<{
    code: string;
    title: string;
    net: number;
    side: string;
    partyId?: string | null;
    crmName?: string | null;
  }>;
  sepidarSearchHits?: Array<{
    code: string;
    title: string;
    partyRef?: string | null;
    partyId?: string | null;
    crmName?: string | null;
    mappingVerified: boolean;
  }>;
  sepidarSearchMessage?: string | null;
  needsReview: Array<{
    partyId: string;
    displayName: string;
    category: string;
    issue: string;
    sepidarCode?: string | null;
    sepidarPartyRef?: string | null;
  }>;
  reviewSampleCapped: boolean;
  unmappedPartyCount: number;
  openFollowUps: Array<{
    noteId: string;
    partyId?: string | null;
    partyName?: string | null;
    owner?: string | null;
    dueDateUtc?: string | null;
    noteText: string;
    source: string;
  }>;
  activeFilters: Array<{ key: string; label: string }>;
}

export interface ExecutiveKpiCardDto {
  contractId: string;
  code: string;
  title: string;
  amount?: number | null;
  numericValue?: number | null;
  unit?: string | null;
  status: FinancialFigureStatusName | number;
  reason: string;
  source: string;
  requiredPermission: string;
  drillTarget?: string | null;
  drillQuery?: string | null;
  previousPeriodAmount?: number | null;
  previousPeriodNote?: string | null;
  lastSuccessfulReadUtc?: string | null;
  validationCause?: string | null;
}

export interface ExecutiveDashboardDto {
  generatedAtUtc: string;
  viewer: {
    hasPartyLedgerView: boolean;
    canViewOps: boolean;
    roles: string[];
    permissionsNote: string;
  };
  slicers: {
    fiscalYears: Array<{ fiscalYearId: number; title: string; startDate: string; endDate: string }>;
    activeFiscalYearId?: number | null;
    items: Array<{
      key: string;
      label: string;
      enabled: boolean;
      disabledReason?: string | null;
      appliesTo?: string | null;
    }>;
  };
  freshness: {
    lastSuccessfulReadUtc?: string | null;
    fiscalYearLabel?: string | null;
    financeFreshness: string;
    financeFreshnessDetail: string;
    completenessFlags: string[];
    opsFreshnessBadge: string;
  };
  cards: ExecutiveKpiCardDto[];
  actionItems: DashboardActionItemDto[];
  monthlyTrend: NonNullable<FinancialDashboardDto['monthly']>;
  topDebtors: NonNullable<FinancialDashboardDto['topParties']>;
  topCreditors: NonNullable<FinancialDashboardDto['topParties']>;
  needsReview: FinancialDashboardDto['needsReview'];
  openFollowUps: FinancialDashboardDto['openFollowUps'];
  topSuppliersOps: SupplierPerformanceReportItemDto[];
  criticalMaterials: MaterialConsumptionReportItemDto[];
  drillLinks: Record<string, string>;
  comparison?: {
    currentFromUtc?: string | null;
    currentToUtc?: string | null;
    previousFromUtc?: string | null;
    previousToUtc?: string | null;
    statusNote?: string | null;
    hasEquivalentPreviousPeriod: boolean;
  };
  financeAccessNote?: string | null;
}

export interface PartyLedgerDto {
  partyId: string;
  displayName: string;
  sepidarPartyRef?: string | null;
  sepidarCode?: string | null;
  mappingVerified: boolean;
  documentsAuthorized: boolean;
  openingBalance: FinancialFigureDto;
  totalDebit: FinancialFigureDto;
  totalCredit: FinancialFigureDto;
  receipts: FinancialFigureDto;
  payments: FinancialFigureDto;
  closingBalance: FinancialFigureDto;
  linesStatus: FinancialFigureStatusName | number;
  linesReason: string;
  page: number;
  pageSize: number;
  totalLineCount?: number | null;
  lines: Array<{
    sepidarReference?: string | null;
    dateUtc?: string | null;
    documentNumber?: string | null;
    documentType?: string | null;
    description?: string | null;
    debit?: number | null;
    credit?: number | null;
    runningBalance?: number | null;
  }>;
  contacts: Array<{ id: string; personName: string; position?: string | null }>;
  calls: Array<{ id: string; startTimeUtc: string; callerNumber: string; destinationNumber: string; callerName?: string | null }>;
  followUps: FinancialDashboardDto['openFollowUps'];
}

export interface RoleDashboardDto {
  role: string;
  generatedAtUtc: string;
  dataFreshness: DataFreshnessStatus;
  freshnessBadge: string;
  summaryMetrics: Record<string, any>;
  trendItems: PurchaseOrderTrendItemDto[];
  topSuppliers: SupplierPerformanceReportItemDto[];
  criticalMaterials: MaterialConsumptionReportItemDto[];
  actionItems: DashboardActionItemDto[];
}

export interface ExportRequestDto {
  reportType: string;
  format: 'csv' | 'xlsx';
  startDateUtc?: string;
  endDateUtc?: string;
  categoryId?: string;
  supplierId?: string;
  warehouseId?: string;
  status?: string;
  searchTerm?: string;
}

export interface TelephonyGroupMemberDto {
  id: string;
  groupId: string;
  extension: string;
  displayName: string;
  userId?: string;
  username?: string;
  isSupervisor: boolean;
  notes?: string;
  joinedAtUtc: string;
}

export interface TelephonyGroupDto {
  id: string;
  name: string;
  code?: string;
  description?: string;
  isActive: boolean;
  supervisorUserId?: string;
  supervisorExtension?: string;
  supervisorDisplayName?: string;
  membersCount: number;
  members: TelephonyGroupMemberDto[];
  createdAtUtc: string;
}

export interface CreateTelephonyGroupRequest {
  name: string;
  code?: string;
  description?: string;
  supervisorUserId?: string;
  supervisorExtension?: string;
  supervisorDisplayName?: string;
}

export interface UpdateTelephonyGroupRequest {
  name: string;
  description?: string;
  isActive?: boolean;
}

export interface AddGroupMemberRequest {
  extension: string;
  displayName?: string;
  userId?: string;
  username?: string;
  isSupervisor?: boolean;
  notes?: string;
}

export interface SetGroupSupervisorRequest {
  /** Empty string clears the supervisor. */
  extension?: string;
  userId?: string;
  displayName?: string;
}


