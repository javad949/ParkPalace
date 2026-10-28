import React, { useEffect, useState } from 'react';
import { 
  Building, 
  User, 
  Search, 
  Plus, 
  Phone, 
  ExternalLink, 
  ChevronRight, 
  ChevronLeft, 
  X, 
  AlertCircle,
  DollarSign
} from 'lucide-react';
import { 
  fetchSuppliers, 
  fetchSupplierDetail, 
  fetchMaterials, 
  fetchUnits, 
  createSupplier, 
  linkSupplierMaterial, 
  addPriceQuote 
} from '../../services/api';
import { 
  PartyDto, 
  PartyDetailDto, 
  PartyType, 
  MaterialDto, 
  UnitOfMeasureDto, 
  PagedResult,
  SupplierMaterialDto
} from '../../types';
import { JalaliDatePicker } from '../JalaliDatePicker';
import { formatJalali, dateOnlyToIsoUtc } from '../../utils/jalali';

interface SuppliersViewProps {
  activeDevRole?: string;
  onNavigateToMaterial?: (materialId: string) => void;
  selectedSupplierId?: string | null;
  onClearSelectedSupplier?: () => void;
}

export const SuppliersView: React.FC<SuppliersViewProps> = ({
  activeDevRole = 'Admin',
  onNavigateToMaterial,
  selectedSupplierId,
  onClearSelectedSupplier,
}) => {
  const [suppliers, setSuppliers] = useState<PartyDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [searchTerm, setSearchTerm] = useState('');
  const [partyTypeFilter, setPartyTypeFilter] = useState<number | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Lookups
  const [materials, setMaterials] = useState<MaterialDto[]>([]);
  const [units, setUnits] = useState<UnitOfMeasureDto[]>([]);

  // Modals
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [activeSupplierDetail, setActiveSupplierDetail] = useState<PartyDetailDto | null>(null);
  const [detailTab, setDetailTab] = useState<'materials' | 'contacts' | 'addresses'>('materials');

  // Sub-modal: Add Price Quote
  const [priceQuoteMaterial, setPriceQuoteMaterial] = useState<SupplierMaterialDto | null>(null);
  const [quotePrice, setQuotePrice] = useState<number>(0);
  const [quoteCurrency, setQuoteCurrency] = useState('ریال');
  const [quoteUnitId, setQuoteUnitId] = useState('');
  const [quoteSource, setQuoteSource] = useState('استعلام تلفنی');
  const [quoteNotes, setQuoteNotes] = useState('');
  const [quoteEffectiveFrom, setQuoteEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));

  // Sub-form: Link Material
  const [linkMatId, setLinkMatId] = useState('');
  const [linkIsPref, setLinkIsPref] = useState(false);
  const [linkLeadTime, setLinkLeadTime] = useState(1);
  const [linkMinQty, setLinkMinQty] = useState(0);
  const [linkInitialPrice, setLinkInitialPrice] = useState<number | undefined>(undefined);

  // Form states - Create Supplier
  const [createType, setCreateType] = useState<PartyType>(2); // Default Legal
  const [createDisplayName, setCreateDisplayName] = useState('');
  const [createLegalName, setCreateLegalName] = useState('');
  const [createRegNumber, setCreateRegNumber] = useState('');
  const [createNationalIdentifier, setCreateNationalIdentifier] = useState('');
  const [createEconomicCode, setCreateEconomicCode] = useState('');
  const [createFirstName, setCreateFirstName] = useState('');
  const [createLastName, setCreateLastName] = useState('');
  const [createShopName, setCreateShopName] = useState('');
  const [createNationalId, setCreateNationalId] = useState('');
  const [createContactName, setCreateContactName] = useState('');
  const [createPhone, setCreatePhone] = useState('');
  const [createPhoneType, setCreatePhoneType] = useState('همراه');
  const [createCity, setCreateCity] = useState('تهران');
  const [createAddress, setCreateAddress] = useState('');
  const [createNotes, setCreateNotes] = useState('');
  const [formSubmitting, setFormSubmitting] = useState(false);

  useEffect(() => {
    loadLookups();
  }, [activeDevRole]);

  useEffect(() => {
    loadSuppliersList();
  }, [page, searchTerm, partyTypeFilter, activeDevRole]);

  useEffect(() => {
    if (selectedSupplierId) {
      openDetail(selectedSupplierId);
    }
  }, [selectedSupplierId]);

  const loadLookups = async () => {
    try {
      const [matRes, uomList] = await Promise.all([
        fetchMaterials({ pageSize: 100 }, activeDevRole),
        fetchUnits(activeDevRole)
      ]);
      setMaterials(matRes.items);
      setUnits(uomList);
      if (matRes.items.length > 0) setLinkMatId(matRes.items[0].id);
      if (uomList.length > 0) setQuoteUnitId(uomList[0].id);
    } catch (err: any) {
      console.error('Failed to load supplier lookups', err);
    }
  };

  const loadSuppliersList = async () => {
    setLoading(true);
    setErrorMsg(null);
    try {
      const res: PagedResult<PartyDto> = await fetchSuppliers({
        search: searchTerm || undefined,
        partyType: partyTypeFilter,
        page,
        pageSize
      }, activeDevRole);
      setSuppliers(res.items);
      setTotalCount(res.totalCount);
    } catch (err: any) {
      setErrorMsg(err.message || 'خطا در بارگذاری لیست تأمین‌کنندگان');
    } finally {
      setLoading(false);
    }
  };

  const openDetail = async (supplierId: string) => {
    try {
      const detail = await fetchSupplierDetail(supplierId, activeDevRole);
      setActiveSupplierDetail(detail);
      setDetailTab('materials');
    } catch (err: any) {
      alert(`خطا در دریافت مشخصات تأمین‌کننده: ${err.message}`);
    }
  };

  const closeDetail = () => {
    setActiveSupplierDetail(null);
    if (onClearSelectedSupplier) onClearSelectedSupplier();
  };

  const handleCreateSupplier = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!createDisplayName.trim()) {
      alert('لطفاً عنوان نمایشی تأمین‌کننده را وارد کنید.');
      return;
    }
    setFormSubmitting(true);
    try {
      await createSupplier({
        partyType: createType,
        displayName: createDisplayName.trim(),
        notes: createNotes.trim() || undefined,
        legalName: createType === 2 ? (createLegalName.trim() || undefined) : undefined,
        registrationNumber: createType === 2 ? (createRegNumber.trim() || undefined) : undefined,
        nationalIdentifier: createType === 2 ? (createNationalIdentifier.trim() || undefined) : undefined,
        economicCode: createType === 2 ? (createEconomicCode.trim() || undefined) : undefined,
        firstName: createType === 1 ? (createFirstName.trim() || undefined) : undefined,
        lastName: createType === 1 ? (createLastName.trim() || undefined) : undefined,
        shopName: createType === 1 ? (createShopName.trim() || undefined) : undefined,
        nationalId: createType === 1 ? (createNationalId.trim() || undefined) : undefined,
        primaryContactName: createContactName.trim() || undefined,
        primaryPhone: createPhone.trim() || undefined,
        primaryPhoneType: createPhoneType,
        primaryAddressCity: createCity.trim() || undefined,
        primaryAddressText: createAddress.trim() || undefined
      }, activeDevRole);

      setIsCreateModalOpen(false);
      resetCreateForm();
      await loadSuppliersList();
      alert('تأمین‌کننده جدید با موفقیت ثبت شد.');
    } catch (err: any) {
      alert(`خطا در ثبت تأمین‌کننده: ${err.message}`);
    } finally {
      setFormSubmitting(false);
    }
  };

  const resetCreateForm = () => {
    setCreateDisplayName('');
    setCreateLegalName('');
    setCreateRegNumber('');
    setCreateNationalIdentifier('');
    setCreateEconomicCode('');
    setCreateFirstName('');
    setCreateLastName('');
    setCreateShopName('');
    setCreateNationalId('');
    setCreateContactName('');
    setCreatePhone('');
    setCreateAddress('');
    setCreateNotes('');
  };

  const handleLinkMaterial = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeSupplierDetail || !linkMatId) {
      alert('لطفاً ماده اولیه مورد نظر را انتخاب کنید.');
      return;
    }
    try {
      await linkSupplierMaterial(activeSupplierDetail.id, {
        materialId: linkMatId,
        isPreferred: linkIsPref,
        defaultLeadTimeDays: Number(linkLeadTime),
        minimumOrderQty: Number(linkMinQty),
        initialPrice: linkInitialPrice ? Number(linkInitialPrice) : undefined,
        currency: 'ریال',
        priceUnitId: quoteUnitId || undefined,
        priceSource: 'قرارداد اولیه'
      }, activeDevRole);

      // Refresh detail
      const updated = await fetchSupplierDetail(activeSupplierDetail.id, activeDevRole);
      setActiveSupplierDetail(updated);
      alert('کالا با موفقیت به سبد تأمین این شرکت افزوده شد.');
    } catch (err: any) {
      alert(`خطا در انتساب کالا: ${err.message}`);
    }
  };

  const handleAddPriceQuote = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeSupplierDetail || !priceQuoteMaterial || quotePrice <= 0) {
      alert('لطفاً مبلغ معتبر استعلام قیمت را وارد کنید.');
      return;
    }
    try {
      await addPriceQuote(activeSupplierDetail.id, priceQuoteMaterial.materialId, {
        price: Number(quotePrice),
        currency: quoteCurrency,
        unitId: quoteUnitId,
        effectiveFrom: dateOnlyToIsoUtc(quoteEffectiveFrom)!,
        source: quoteSource,
        notes: quoteNotes || undefined
      }, activeDevRole);

      // Refresh detail
      const updated = await fetchSupplierDetail(activeSupplierDetail.id, activeDevRole);
      setActiveSupplierDetail(updated);
      setPriceQuoteMaterial(null);
      setQuotePrice(0);
      setQuoteNotes('');
      alert('نرخ جدید استعلام قیمت با موفقیت ثبت شد.');
    } catch (err: any) {
      alert(`خطا در ثبت استعلام قیمت: ${err.message}`);
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <div className="view-card">
      {/* Header */}
      <div className="view-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>مدیریت تأمین‌کنندگان و مخاطبین (Suppliers & Parties)</h2>
          <p>سامانه تجمیع اطلاعات اشخاص حقوقی و حقیقی، نمایندگان فروش، شماره‌های تماس ایزابل و سوابق استعلام قیمت</p>
        </div>
        <button 
          className="btn btn-primary"
          onClick={() => setIsCreateModalOpen(true)}
        >
          <Plus size={16} />
          ثبت تأمین‌کننده جدید
        </button>
      </div>

      {/* Filter and Search Bar */}
      <div style={{ display: 'flex', gap: '12px', marginBottom: '20px', flexWrap: 'wrap' }}>
        <div style={{
          flex: 1,
          minWidth: '280px',
          background: 'var(--bg-sidebar)',
          border: '1px solid var(--border-color)',
          borderRadius: '8px',
          padding: '8px 14px',
          display: 'flex',
          alignItems: 'center',
          gap: '10px'
        }}>
          <Search size={16} color="var(--text-dim)" />
          <input
            type="text"
            placeholder="جستجوی نام شرکت، شخص، کد اقتصادی یا شماره تلفن نرمال‌شده..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(1);
            }}
            style={{
              background: 'transparent',
              border: 'none',
              color: 'var(--text-main)',
              fontFamily: 'var(--font-family)',
              width: '100%',
              outline: 'none',
              fontSize: '13px'
            }}
          />
          {searchTerm && (
            <button 
              onClick={() => setSearchTerm('')} 
              style={{ background: 'transparent', border: 'none', color: 'var(--text-dim)', cursor: 'pointer' }}
            >
              <X size={14} />
            </button>
          )}
        </div>

        {/* Type toggle buttons */}
        <div style={{ display: 'flex', gap: '4px', background: 'var(--bg-sidebar)', padding: '4px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
          <button 
            className={`btn btn-sm ${partyTypeFilter === undefined ? 'btn-primary' : 'btn-outline'}`}
            onClick={() => { setPartyTypeFilter(undefined); setPage(1); }}
            style={{ border: 'none' }}
          >
            همه
          </button>
          <button 
            className={`btn btn-sm ${partyTypeFilter === 2 ? 'btn-primary' : 'btn-outline'}`}
            onClick={() => { setPartyTypeFilter(2); setPage(1); }}
            style={{ border: 'none' }}
          >
            <Building size={14} />
            حقوقی (شرکت‌ها)
          </button>
          <button 
            className={`btn btn-sm ${partyTypeFilter === 1 ? 'btn-primary' : 'btn-outline'}`}
            onClick={() => { setPartyTypeFilter(1); setPage(1); }}
            style={{ border: 'none' }}
          >
            <User size={14} />
            حقیقی (فروشگاه‌ها)
          </button>
        </div>
      </div>

      {/* Error notification */}
      {errorMsg && (
        <div style={{ 
          background: 'var(--danger-light)', 
          color: '#f87171', 
          border: '1px solid rgba(239, 68, 68, 0.3)', 
          borderRadius: '8px', 
          padding: '12px 16px',
          marginBottom: '16px',
          display: 'flex',
          alignItems: 'center',
          gap: '10px'
        }}>
          <AlertCircle size={18} />
          <span>{errorMsg}</span>
        </div>
      )}

      {/* Suppliers Table */}
      <div className="data-table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>عنوان تجاری / نام</th>
              <th>نوع شخصیت</th>
              <th>نماینده / مخاطب</th>
              <th>شماره تماس اصلی</th>
              <th>شهر</th>
              <th>اقلام تأمینی</th>
              <th>وضعیت</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-dim)' }}>
                  در حال بارگذاری لیست تأمین‌کنندگان...
                </td>
              </tr>
            ) : suppliers.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                  تأمین‌کننده‌ای با این مشخصات یافت نشد.
                </td>
              </tr>
            ) : (
              suppliers.map((s) => (
                <tr key={s.id}>
                  <td>
                    <div style={{ fontWeight: 600 }}>{s.displayName}</div>
                    <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>
                      {s.partyType === 2 
                        ? (s.legalName ? `ثبتی: ${s.legalName}` : (s.economicCode ? `کد اقتصادی: ${s.economicCode}` : ''))
                        : (s.shopName ? `تابلو: ${s.shopName}` : (s.nationalId ? `کد ملی: ${s.nationalId}` : ''))}
                    </div>
                  </td>
                  <td>
                    {s.partyType === 2 ? (
                      <span className="badge badge-primary">
                        <Building size={12} />
                        حقوقی
                      </span>
                    ) : (
                      <span className="badge badge-warning">
                        <User size={12} />
                        حقیقی
                      </span>
                    )}
                  </td>
                  <td>{s.primaryContactName || '-'}</td>
                  <td>
                    {s.primaryPhone ? (
                      <span style={{ 
                        fontFamily: 'monospace', 
                        direction: 'ltr', 
                        display: 'inline-flex', 
                        alignItems: 'center', 
                        gap: '6px',
                        background: 'rgba(56, 189, 248, 0.08)',
                        padding: '2px 8px',
                        borderRadius: '4px'
                      }}>
                        <Phone size={12} color="#38bdf8" />
                        {s.primaryPhone}
                      </span>
                    ) : '-'}
                  </td>
                  <td>{s.primaryAddressCity || '-'}</td>
                  <td>
                    <span className={`badge ${s.suppliedMaterialsCount > 0 ? 'badge-success' : 'badge-dim'}`}>
                      {s.suppliedMaterialsCount} قلم کالا
                    </span>
                  </td>
                  <td>
                    <span className="badge badge-success">فعال</span>
                  </td>
                  <td>
                    <button 
                      className="btn btn-secondary btn-sm"
                      onClick={() => openDetail(s.id)}
                    >
                      مشاهده و قیمت‌ها
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination Bar */}
      <div className="pagination">
        <span>نمایش {suppliers.length} از {totalCount} تأمین‌کننده ثبت‌شده</span>
        <div className="pagination-controls">
          <button 
            className="btn btn-secondary btn-sm" 
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
          >
            <ChevronRight size={14} />
            قبلی
          </button>
          <span style={{ alignSelf: 'center', fontSize: '12px', padding: '0 8px' }}>
            صفحه {page} از {totalPages}
          </span>
          <button 
            className="btn btn-secondary btn-sm" 
            disabled={page >= totalPages}
            onClick={() => setPage(page + 1)}
          >
            بعدی
            <ChevronLeft size={14} />
          </button>
        </div>
      </div>

      {/* MODAL: Supplier Detail Drawer/Dialog */}
      {activeSupplierDetail && (
        <div className="modal-overlay" onClick={closeDetail}>
          <div className="modal-content large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                {activeSupplierDetail.partyType === 2 ? (
                  <Building size={24} color="#38bdf8" />
                ) : (
                  <User size={24} color="#fbbf24" />
                )}
                <div>
                  <h3 style={{ margin: 0 }}>{activeSupplierDetail.displayName}</h3>
                  <span style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
                    {activeSupplierDetail.partyType === 2 
                      ? `شخص حقوقی | شناسه ملی: ${activeSupplierDetail.nationalIdentifier || '-'} | کد اقتصادی: ${activeSupplierDetail.economicCode || '-'}`
                      : `شخص حقیقی | نام: ${activeSupplierDetail.firstName || ''} ${activeSupplierDetail.lastName || ''} | کد ملی: ${activeSupplierDetail.nationalId || '-'}`}
                  </span>
                </div>
              </div>
              <button 
                onClick={closeDetail}
                style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div className="modal-body">
              {/* Tabs */}
              <div className="tabs-header">
                <button 
                  className={`tab-btn ${detailTab === 'materials' ? 'active' : ''}`}
                  onClick={() => setDetailTab('materials')}
                >
                  اقلام کاتالوگ و سوابق قیمت ({activeSupplierDetail.suppliedMaterials?.length || 0})
                </button>
                <button 
                  className={`tab-btn ${detailTab === 'contacts' ? 'active' : ''}`}
                  onClick={() => setDetailTab('contacts')}
                >
                  مخاطبین و شماره‌های ایزابل ({activeSupplierDetail.contacts?.length || 0} نماینده / {activeSupplierDetail.phones?.length || 0} خط)
                </button>
                <button 
                  className={`tab-btn ${detailTab === 'addresses' ? 'active' : ''}`}
                  onClick={() => setDetailTab('addresses')}
                >
                  آدرس‌ها و انبارها ({activeSupplierDetail.addresses?.length || 0})
                </button>
              </div>

              {/* Tab 1: Supplied Materials & Price Quotes */}
              {detailTab === 'materials' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
                  <div className="data-table-container">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>نام ماده اولیه</th>
                          <th>کد کالا</th>
                          <th>اولویت</th>
                          <th>زمان تحویل</th>
                          <th>حداقل سفارش</th>
                          <th>آخرین نرخ استعلامی</th>
                          <th>عملیات</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(!activeSupplierDetail.suppliedMaterials || activeSupplierDetail.suppliedMaterials.length === 0) ? (
                          <tr>
                            <td colSpan={7} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-dim)' }}>
                              هنوز کالایی در سبد تأمین این شرکت تعریف نشده است.
                            </td>
                          </tr>
                        ) : (
                          activeSupplierDetail.suppliedMaterials.map((sm) => {
                            const latestPrice = sm.priceHistories && sm.priceHistories.length > 0
                              ? sm.priceHistories[0]
                              : null;

                            return (
                              <tr key={sm.id}>
                                <td>
                                  <div style={{ fontWeight: 600 }}>{sm.materialName}</div>
                                </td>
                                <td>
                                  <span style={{ fontFamily: 'monospace', color: '#38bdf8' }}>
                                    {sm.materialCode}
                                  </span>
                                </td>
                                <td>
                                  {sm.isPreferred ? (
                                    <span className="badge badge-success">تأمین‌کننده اصلی</span>
                                  ) : (
                                    <span className="badge badge-dim">عادی</span>
                                  )}
                                </td>
                                <td>{sm.defaultLeadTimeDays ?? 1} روز</td>
                                <td>{sm.minimumOrderQty ?? 0}</td>
                                <td>
                                  {latestPrice ? (
                                    <div>
                                      <strong style={{ color: '#34d399' }}>
                                        {latestPrice.price.toLocaleString('fa-IR')} {latestPrice.currency}
                                      </strong>
                                      <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>
                                        هر {latestPrice.unitName} ({formatJalali(latestPrice.effectiveFrom)})
                                      </div>
                                    </div>
                                  ) : (
                                    <span style={{ color: 'var(--text-dim)' }}>بدون استعلام</span>
                                  )}
                                </td>
                                <td>
                                  <div style={{ display: 'flex', gap: '6px' }}>
                                    <button 
                                      className="btn btn-outline btn-sm"
                                      onClick={() => {
                                        setPriceQuoteMaterial(sm);
                                        setQuotePrice(latestPrice?.price || 0);
                                      }}
                                      title="ثبت نرخ جدید استعلام"
                                    >
                                      <DollarSign size={13} />
                                      ثبت قیمت
                                    </button>
                                    <button 
                                      className="btn btn-secondary btn-sm"
                                      onClick={() => {
                                        closeDetail();
                                        if (onNavigateToMaterial) onNavigateToMaterial(sm.materialId);
                                      }}
                                      title="مشاهده کالا در کاتالوگ"
                                    >
                                      <ExternalLink size={13} />
                                    </button>
                                  </div>
                                </td>
                              </tr>
                            );
                          })
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Form to Link a Material */}
                  <form onSubmit={handleLinkMaterial} style={{ 
                    background: 'var(--bg-sidebar)', 
                    padding: '16px', 
                    borderRadius: '8px', 
                    border: '1px solid var(--border-color)' 
                  }}>
                    <h4 style={{ fontSize: '13px', marginBottom: '12px', color: '#fff' }}>افزودن ماده اولیه جدید به سبد تأمین این شرکت</h4>
                    <div className="form-row">
                      <div className="form-group">
                        <label>انتخاب کالا از کاتالوگ:</label>
                        <select 
                          className="form-select"
                          value={linkMatId}
                          onChange={(e) => setLinkMatId(e.target.value)}
                          required
                        >
                          {materials.map(m => (
                            <option key={m.id} value={m.id}>{m.name} ({m.internalCode})</option>
                          ))}
                        </select>
                      </div>
                      <div className="form-group">
                        <label>زمان تحویل (روز):</label>
                        <input 
                          type="number" 
                          className="form-input" 
                          value={linkLeadTime} 
                          onChange={(e) => setLinkLeadTime(Number(e.target.value))}
                          min={0}
                        />
                      </div>
                      <div className="form-group">
                        <label>حداقل تیراژ سفارش:</label>
                        <input 
                          type="number" 
                          className="form-input" 
                          value={linkMinQty} 
                          onChange={(e) => setLinkMinQty(Number(e.target.value))}
                          min={0}
                        />
                      </div>
                      <div className="form-group">
                        <label>قیمت اولیه (اختیاری - ریال):</label>
                        <input 
                          type="number" 
                          className="form-input" 
                          value={linkInitialPrice || ''} 
                          onChange={(e) => setLinkInitialPrice(e.target.value ? Number(e.target.value) : undefined)}
                          placeholder="مثلاً ۱۲۵۰۰۰۰"
                        />
                      </div>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '12px' }}>
                      <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', cursor: 'pointer' }}>
                        <input 
                          type="checkbox" 
                          checked={linkIsPref} 
                          onChange={(e) => setLinkIsPref(e.target.checked)} 
                        />
                        تعیین به عنوان تأمین‌کننده اصلی و دارای اولویت (Preferred Supplier)
                      </label>
                      <button type="submit" className="btn btn-primary btn-sm">
                        <Plus size={14} />
                        انتساب کالا به تأمین‌کننده
                      </button>
                    </div>
                  </form>
                </div>
              )}

              {/* Tab 2: Contacts & Phones (Isabel Screen-Pop ready) */}
              {detailTab === 'contacts' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                  <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    کلیه شماره تلفن‌های ثبت‌شده با استاندارد مخابراتی ایران نرمال‌سازی شده‌اند و هنگام تماس ورودی ایزابل (Asterisk AMI) مستقیماً شناسایی می‌شوند:
                  </div>

                  <div className="data-table-container">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>نام نماینده / مخاطب</th>
                          <th>سمت / موقعیت</th>
                          <th>نوع خط</th>
                          <th>شماره اولیه</th>
                          <th>شماره نرمال‌شده (ایزابل)</th>
                          <th>اصلی</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(!activeSupplierDetail.phones || activeSupplierDetail.phones.length === 0) ? (
                          <tr>
                            <td colSpan={6} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-dim)' }}>
                              شماره تماسی برای این شرکت ثبت نشده است.
                            </td>
                          </tr>
                        ) : (
                          activeSupplierDetail.phones.map((p) => (
                            <tr key={p.id}>
                              <td>{p.contactName || activeSupplierDetail.displayName}</td>
                              <td>-</td>
                              <td>
                                <span className="badge badge-dim">{p.phoneType}</span>
                              </td>
                              <td style={{ fontFamily: 'monospace', direction: 'ltr', textAlign: 'right' }}>
                                {p.originalValue}
                              </td>
                              <td style={{ fontFamily: 'monospace', direction: 'ltr', textAlign: 'right', color: '#38bdf8', fontWeight: 600 }}>
                                {p.normalizedValue}
                              </td>
                              <td>
                                {p.isPrimary ? (
                                  <span className="badge badge-success">پیش‌فرض</span>
                                ) : (
                                  <span className="badge badge-dim">فرعی</span>
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

              {/* Tab 3: Addresses */}
              {detailTab === 'addresses' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                  <div className="data-table-container">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>نوع آدرس</th>
                          <th>استان / شهر</th>
                          <th>نشانی کامل</th>
                          <th>کد پستی</th>
                          <th>اصلی</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(!activeSupplierDetail.addresses || activeSupplierDetail.addresses.length === 0) ? (
                          <tr>
                            <td colSpan={5} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-dim)' }}>
                              آدرسی برای این شرکت ثبت نشده است.
                            </td>
                          </tr>
                        ) : (
                          activeSupplierDetail.addresses.map((a) => (
                            <tr key={a.id}>
                              <td><span className="badge badge-dim">{a.addressType}</span></td>
                              <td>{a.province} - {a.city}</td>
                              <td>{a.addressText}</td>
                              <td style={{ fontFamily: 'monospace' }}>{a.postalCode || '-'}</td>
                              <td>
                                {a.isPrimary ? (
                                  <span className="badge badge-success">پیش‌فرض</span>
                                ) : (
                                  <span className="badge badge-dim">فرعی</span>
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
            </div>

            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={closeDetail}>
                بستن
              </button>
            </div>
          </div>
        </div>
      )}

      {/* SUB-MODAL: Add Price Quote (Append-Only) */}
      {priceQuoteMaterial && activeSupplierDetail && (
        <div className="modal-overlay" onClick={() => setPriceQuoteMaterial(null)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <DollarSign size={20} color="#34d399" />
                <h3 style={{ margin: 0 }}>ثبت استعلام قیمت جدید (Append-Only)</h3>
              </div>
              <button 
                onClick={() => setPriceQuoteMaterial(null)}
                style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleAddPriceQuote}>
              <div className="modal-body">
                <div style={{ 
                  background: 'var(--bg-sidebar)', 
                  padding: '12px', 
                  borderRadius: '8px', 
                  fontSize: '13px',
                  marginBottom: '10px'
                }}>
                  تأمین‌کننده: <strong>{activeSupplierDetail.displayName}</strong> | کالا: <strong>{priceQuoteMaterial.materialName}</strong>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label>مبلغ واحد استعلامی *:</label>
                    <input 
                      type="number" 
                      className="form-input" 
                      value={quotePrice} 
                      onChange={(e) => setQuotePrice(Number(e.target.value))}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label>واحد پولی:</label>
                    <select 
                      className="form-select"
                      value={quoteCurrency}
                      onChange={(e) => setQuoteCurrency(e.target.value)}
                    >
                      <option value="ریال">ریال</option>
                      <option value="تومان">تومان</option>
                    </select>
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label>واحد سنجش مظنه *:</label>
                    <select 
                      className="form-select"
                      value={quoteUnitId}
                      onChange={(e) => setQuoteUnitId(e.target.value)}
                      required
                    >
                      {units.map(u => (
                        <option key={u.id} value={u.id}>{u.name} ({u.code})</option>
                      ))}
                    </select>
                  </div>
                  <div className="form-group">
                    <JalaliDatePicker
                      label="تاریخ استعلام (موثر از):"
                      value={quoteEffectiveFrom}
                      onChange={(val) => setQuoteEffectiveFrom(val)}
                      required
                    />
                  </div>
                </div>

                <div className="form-group">
                  <label>منبع استعلام قیمت:</label>
                  <select 
                    className="form-select"
                    value={quoteSource}
                    onChange={(e) => setQuoteSource(e.target.value)}
                  >
                    <option value="استعلام تلفنی">استعلام تلفنی</option>
                    <option value="پیش‌فاکتور رسمی">پیش‌فاکتور رسمی</option>
                    <option value="فاکتور خرید قطعی">فاکتور خرید قطعی</option>
                    <option value="لیست قیمت کارخانه">لیست قیمت کارخانه</option>
                  </select>
                </div>

                <div className="form-group">
                  <label>یادداشت و شرایط پرداخت:</label>
                  <textarea 
                    className="form-textarea"
                    placeholder="مثلاً نقد پای باسکول، یا چک صیادی ۴۵ روزه..."
                    value={quoteNotes}
                    onChange={(e) => setQuoteNotes(e.target.value)}
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button 
                  type="button" 
                  className="btn btn-secondary"
                  onClick={() => setPriceQuoteMaterial(null)}
                >
                  انصراف
                </button>
                <button type="submit" className="btn btn-primary">
                  ثبت قطعی در سوابق
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: Create New Supplier */}
      {isCreateModalOpen && (
        <div className="modal-overlay" onClick={() => setIsCreateModalOpen(false)}>
          <div className="modal-content large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Plus size={20} color="#38bdf8" />
                <h3 style={{ margin: 0 }}>ثبت تأمین‌کننده و طرف حساب جدید</h3>
              </div>
              <button 
                onClick={() => setIsCreateModalOpen(false)}
                style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleCreateSupplier}>
              <div className="modal-body">
                {/* Party Type selector */}
                <div style={{ display: 'flex', gap: '16px', marginBottom: '8px' }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                    <input 
                      type="radio" 
                      name="partyType" 
                      checked={createType === 2} 
                      onChange={() => setCreateType(2)} 
                    />
                    <strong>شخص حقوقی (کارخانه، شرکت بازرگانی، تعاونی)</strong>
                  </label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                    <input 
                      type="radio" 
                      name="partyType" 
                      checked={createType === 1} 
                      onChange={() => setCreateType(1)} 
                    />
                    <strong>شخص حقیقی (فروشگاه، ویزیتور آزاد، کشاورز)</strong>
                  </label>
                </div>

                <div className="form-group">
                  <label>عنوان نمایشی تأمین‌کننده در سیستم *:</label>
                  <input 
                    type="text" 
                    className="form-input"
                    placeholder={createType === 2 ? "مثال: شرکت صنایع آرد خوشه زرین" : "مثال: فروشگاه نان و ملزومات قنادی کریمی"}
                    value={createDisplayName}
                    onChange={(e) => setCreateDisplayName(e.target.value)}
                    required
                  />
                </div>

                {createType === 2 ? (
                  <>
                    <div className="form-row">
                      <div className="form-group">
                        <label>نام کامل ثبتی شرکت:</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createLegalName}
                          onChange={(e) => setCreateLegalName(e.target.value)}
                        />
                      </div>
                      <div className="form-group">
                        <label>شماره ثبت شرکت:</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createRegNumber}
                          onChange={(e) => setCreateRegNumber(e.target.value)}
                        />
                      </div>
                    </div>
                    <div className="form-row">
                      <div className="form-group">
                        <label>شناسه ملی (۱۱ رقمی):</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createNationalIdentifier}
                          onChange={(e) => setCreateNationalIdentifier(e.target.value)}
                        />
                      </div>
                      <div className="form-group">
                        <label>کد اقتصادی:</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createEconomicCode}
                          onChange={(e) => setCreateEconomicCode(e.target.value)}
                        />
                      </div>
                    </div>
                  </>
                ) : (
                  <>
                    <div className="form-row">
                      <div className="form-group">
                        <label>نام:</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createFirstName}
                          onChange={(e) => setCreateFirstName(e.target.value)}
                        />
                      </div>
                      <div className="form-group">
                        <label>نام خانوادگی:</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createLastName}
                          onChange={(e) => setCreateLastName(e.target.value)}
                        />
                      </div>
                    </div>
                    <div className="form-row">
                      <div className="form-group">
                        <label>نام فروشگاه / تابلو تجاری:</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createShopName}
                          onChange={(e) => setCreateShopName(e.target.value)}
                        />
                      </div>
                      <div className="form-group">
                        <label>کد ملی (۱۰ رقمی):</label>
                        <input 
                          type="text" 
                          className="form-input"
                          value={createNationalId}
                          onChange={(e) => setCreateNationalId(e.target.value)}
                        />
                      </div>
                    </div>
                  </>
                )}

                <div className="form-row">
                  <div className="form-group">
                    <label>نام مخاطب اصلی / نماینده فروش:</label>
                    <input 
                      type="text" 
                      className="form-input"
                      placeholder="مثال: آقای مهندس جلالی (مدیر فروش)"
                      value={createContactName}
                      onChange={(e) => setCreateContactName(e.target.value)}
                    />
                  </div>
                  <div className="form-group">
                    <label>شماره تماس اصلی (نرمال‌سازی خودکار):</label>
                    <input 
                      type="text" 
                      className="form-input"
                      placeholder="مثال: 09121112233 یا 02188776655"
                      value={createPhone}
                      onChange={(e) => setCreatePhone(e.target.value)}
                      style={{ direction: 'ltr', textAlign: 'right' }}
                    />
                  </div>
                  <div className="form-group">
                    <label>نوع خط:</label>
                    <select 
                      className="form-select"
                      value={createPhoneType}
                      onChange={(e) => setCreatePhoneType(e.target.value)}
                    >
                      <option value="همراه">همراه (موبایل)</option>
                      <option value="ثابت">تلفن ثابت</option>
                      <option value="فکس">فکس</option>
                    </select>
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group" style={{ maxWidth: '180px' }}>
                    <label>شهر:</label>
                    <input 
                      type="text" 
                      className="form-input"
                      value={createCity}
                      onChange={(e) => setCreateCity(e.target.value)}
                    />
                  </div>
                  <div className="form-group" style={{ flex: 1 }}>
                    <label>نشانی کامل کارخانه / دفتر فروش:</label>
                    <input 
                      type="text" 
                      className="form-input"
                      placeholder="خیابان، پلاک، طبقه، واحد..."
                      value={createAddress}
                      onChange={(e) => setCreateAddress(e.target.value)}
                    />
                  </div>
                </div>

                <div className="form-group">
                  <label>یادداشت و شرایط همکاری:</label>
                  <textarea 
                    className="form-textarea"
                    placeholder="شرایط تسویه، سابقه همکاری، اعتبار سنجی..."
                    value={createNotes}
                    onChange={(e) => setCreateNotes(e.target.value)}
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button 
                  type="button" 
                  className="btn btn-secondary"
                  onClick={() => setIsCreateModalOpen(false)}
                >
                  انصراف
                </button>
                <button 
                  type="submit" 
                  className="btn btn-primary"
                  disabled={formSubmitting}
                >
                  {formSubmitting ? 'در حال ثبت...' : 'ذخیره تأمین‌کننده'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
