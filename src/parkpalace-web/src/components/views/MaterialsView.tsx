import React, { useEffect, useState } from 'react';
import { 
  Package, 
  Search, 
  Plus, 
  Layers, 
  ChevronRight, 
  ChevronLeft,
  Folder, 
  FolderOpen, 
  ExternalLink, 
  X, 
  AlertCircle,
  Scale,
  CheckCircle2,
  Lock,
  Check,
  Sparkles,
  Edit3,
  Save
} from 'lucide-react';
import { 
  fetchMaterials, 
  fetchMaterialDetail, 
  fetchCategories, 
  fetchCategoryTree, 
  fetchUnits, 
  createMaterial, 
  updateMaterial,
  confirmConversion,
  confirmCategorization,
  createCategory, 
  moveCategory, 
  addUnitConversion 
} from '../../services/api';
import { 
  MaterialDto, 
  MaterialDetailDto, 
  CategoryDto, 
  CategoryTreeDto, 
  UnitOfMeasureDto, 
  PagedResult 
} from '../../types';
import { formatJalali } from '../../utils/jalali';

interface MaterialsViewProps {
  activeDevRole?: string;
  onNavigateToSupplier?: (supplierId: string) => void;
  selectedMaterialId?: string | null;
  onClearSelectedMaterial?: () => void;
}

export const MaterialsView: React.FC<MaterialsViewProps> = ({
  activeDevRole = 'Admin',
  onNavigateToSupplier,
  selectedMaterialId,
  onClearSelectedMaterial,
}) => {
  const [materials, setMaterials] = useState<MaterialDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategoryFilter, setSelectedCategoryFilter] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Catalogs
  const [categories, setCategories] = useState<CategoryDto[]>([]);
  const [categoryTree, setCategoryTree] = useState<CategoryTreeDto[]>([]);
  const [units, setUnits] = useState<UnitOfMeasureDto[]>([]);

  // Modals state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isCategoryModalOpen, setIsCategoryModalOpen] = useState(false);
  const [activeDetailMaterial, setActiveDetailMaterial] = useState<MaterialDetailDto | null>(null);
  const [detailTab, setDetailTab] = useState<'info' | 'conversions' | 'suppliers'>('info');

  // Form states - Create Material
  const [newMatCode, setNewMatCode] = useState('');
  const [newMatName, setNewMatName] = useState('');
  const [newMatCatId, setNewMatCatId] = useState('');
  const [newMatUnitId, setNewMatUnitId] = useState('');
  const [newMatBrand, setNewMatBrand] = useState('');
  const [newMatDesc, setNewMatDesc] = useState('');
  const [newMatSpecs, setNewMatSpecs] = useState('');
  const [newMatMinStock, setNewMatMinStock] = useState<number>(0);
  const [newMatSafetyStock, setNewMatSafetyStock] = useState<number>(0);
  const [newMatLeadTime, setNewMatLeadTime] = useState<number>(1);
  const [formSubmitting, setFormSubmitting] = useState(false);

  // Form states - Edit Material
  const [editName, setEditName] = useState('');
  const [editCategoryId, setEditCategoryId] = useState('');
  const [editBrand, setEditBrand] = useState('');
  const [editPackaging, setEditPackaging] = useState('');
  const [editPackageSize, setEditPackageSize] = useState<number | ''>('');
  const [editPackageSizeUnitId, setEditPackageSizeUnitId] = useState('');
  const [editPurchaseUnitId, setEditPurchaseUnitId] = useState('');
  const [editConsumptionUnitId, setEditConsumptionUnitId] = useState('');
  const [editDesc, setEditDesc] = useState('');
  const [editSpecs, setEditSpecs] = useState('');
  const [editMinStock, setEditMinStock] = useState<number>(0);
  const [editSafetyStock, setEditSafetyStock] = useState<number>(0);
  const [editLeadTime, setEditLeadTime] = useState<number>(1);
  const [isSavingEdit, setIsSavingEdit] = useState(false);
  const [editSuccessMsg, setEditSuccessMsg] = useState<string | null>(null);

  // Form states - Add Conversion
  const [convToUnitId, setConvToUnitId] = useState('');
  const [convFactor, setConvFactor] = useState<number>(1);

  // Form states - Create Category
  const [newCatName, setNewCatName] = useState('');
  const [newCatCode, setNewCatCode] = useState('');
  const [newCatParentId, setNewCatParentId] = useState<string>('');

  // Form states - Move Category
  const [moveSourceCatId, setMoveSourceCatId] = useState('');
  const [moveTargetParentId, setMoveTargetParentId] = useState('');

  // Initial load
  useEffect(() => {
    loadLookups();
  }, [activeDevRole]);

  useEffect(() => {
    loadMaterialsList();
  }, [page, searchTerm, selectedCategoryFilter, activeDevRole]);

  // Handle external selection (e.g. from Supplier view)
  useEffect(() => {
    if (selectedMaterialId) {
      openDetail(selectedMaterialId);
    }
  }, [selectedMaterialId]);

  const loadLookups = async () => {
    try {
      const [cats, tree, uoms] = await Promise.all([
        fetchCategories(activeDevRole),
        fetchCategoryTree(activeDevRole),
        fetchUnits(activeDevRole)
      ]);
      setCategories(cats);
      setCategoryTree(tree);
      setUnits(uoms);
      if (uoms.length > 0 && !newMatUnitId) setNewMatUnitId(uoms[0].id);
      if (cats.length > 0 && !newMatCatId) setNewMatCatId(cats[0].id);
    } catch (err: any) {
      console.error('Failed to load lookups', err);
    }
  };

  const loadMaterialsList = async () => {
    setLoading(true);
    setErrorMsg(null);
    try {
      const res: PagedResult<MaterialDto> = await fetchMaterials({
        search: searchTerm || undefined,
        categoryId: selectedCategoryFilter || undefined,
        page,
        pageSize
      }, activeDevRole);
      setMaterials(res.items);
      setTotalCount(res.totalCount);
    } catch (err: any) {
      setErrorMsg(err.message || 'خطا در بارگذاری لیست مواد اولیه');
    } finally {
      setLoading(false);
    }
  };

  const openDetail = async (materialId: string) => {
    try {
      const detail = await fetchMaterialDetail(materialId, activeDevRole);
      setActiveDetailMaterial(detail);
      setDetailTab('info');
      setEditName(detail.name);
      setEditCategoryId(detail.categoryId);
      setEditBrand(detail.brand || '');
      setEditPackaging(detail.packaging || '');
      setEditPackageSize(detail.packageSize ?? '');
      setEditPackageSizeUnitId(detail.packageSizeUnitId || '');
      setEditPurchaseUnitId(detail.purchaseUnitId || detail.baseUnitId);
      setEditConsumptionUnitId(detail.consumptionUnitId || '');
      setEditDesc(detail.description || '');
      setEditSpecs(detail.specifications || '');
      setEditMinStock(detail.minStock ?? 0);
      setEditSafetyStock(detail.safetyStock ?? 0);
      setEditLeadTime(detail.leadTimeDays ?? 1);
      setEditSuccessMsg(null);

      if (units.length > 0) {
        const otherUnit = units.find(u => u.id !== detail.baseUnitId);
        if (otherUnit) setConvToUnitId(otherUnit.id);
      }
    } catch (err: any) {
      alert(`خطا در دریافت جزئیات ماده: ${err.message}`);
    }
  };

  const closeDetail = () => {
    setActiveDetailMaterial(null);
    setEditSuccessMsg(null);
    if (onClearSelectedMaterial) onClearSelectedMaterial();
  };

  const handleUpdateMaterial = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeDetailMaterial) return;
    if (!editName.trim() || !editCategoryId) {
      alert('نام استاندارد کاتالوگ و دسته‌بندی اجباری هستند.');
      return;
    }
    setIsSavingEdit(true);
    setEditSuccessMsg(null);
    try {
      await updateMaterial(activeDetailMaterial.id, {
        name: editName.trim(),
        categoryId: editCategoryId,
        baseUnitId: activeDetailMaterial.baseUnitId, // Retain existing BaseUnit to protect historical cardex
        brand: editBrand.trim() || undefined,
        packaging: editPackaging.trim() || undefined,
        packageSize: editPackageSize !== '' ? Number(editPackageSize) : undefined,
        packageSizeUnitId: editPackageSizeUnitId || undefined,
        purchaseUnitId: editPurchaseUnitId || undefined,
        consumptionUnitId: editConsumptionUnitId || undefined,
        description: editDesc.trim() || undefined,
        specifications: editSpecs.trim() || undefined,
        minStock: Number(editMinStock),
        safetyStock: Number(editSafetyStock),
        leadTimeDays: Number(editLeadTime),
        isActive: activeDetailMaterial.isActive
      }, activeDevRole);

      const refreshed = await fetchMaterialDetail(activeDetailMaterial.id, activeDevRole);
      setActiveDetailMaterial(refreshed);
      setEditSuccessMsg('اصلاحات کاتالوگ CRM با موفقیت ثبت شد. این تغییرات در همگام‌سازی‌های بعدی سپیدار حفظ خواهد شد.');
      await loadMaterialsList();
    } catch (err: any) {
      alert(`خطا در ذخیره تغییرات کاتالوگ: ${err.message}`);
    } finally {
      setIsSavingEdit(false);
    }
  };

  const handleConfirmCategorization = async () => {
    if (!activeDetailMaterial) return;
    try {
      await confirmCategorization(activeDetailMaterial.id, activeDevRole);
      const refreshed = await fetchMaterialDetail(activeDetailMaterial.id, activeDevRole);
      setActiveDetailMaterial(refreshed);
      setEditSuccessMsg('دسته‌بندی و اطلاعات استاندارد کاتالوگ با موفقیت تأیید شد و در همگام‌سازی‌های بعدی سپیدار حفظ خواهد شد.');
      await loadMaterialsList();
    } catch (err: any) {
      alert(`خطا در تأیید دسته‌بندی: ${err.message}`);
    }
  };

  const handleConfirmConversion = async (conversionId: string) => {
    if (!activeDetailMaterial) return;
    try {
      await confirmConversion(activeDetailMaterial.id, conversionId, activeDevRole);
      const refreshed = await fetchMaterialDetail(activeDetailMaterial.id, activeDevRole);
      setActiveDetailMaterial(refreshed);
      alert('ضریب تبدیل واحد با موفقیت تأیید شد و در محاسبات کاتالوگ، موجودی و سفارش فعال گردید.');
    } catch (err: any) {
      alert(`خطا در تأیید تبدیل واحد: ${err.message}`);
    }
  };

  const handleCreateMaterial = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newMatCode.trim() || !newMatName.trim() || !newMatCatId || !newMatUnitId) {
      alert('لطفاً کد داخلی، نام، دسته‌بندی و واحد پایه را وارد نمایید.');
      return;
    }
    setFormSubmitting(true);
    try {
      await createMaterial({
        internalCode: newMatCode.trim(),
        name: newMatName.trim(),
        categoryId: newMatCatId,
        baseUnitId: newMatUnitId,
        brand: newMatBrand.trim() || undefined,
        description: newMatDesc.trim() || undefined,
        specifications: newMatSpecs.trim() || undefined,
        minStock: Number(newMatMinStock),
        safetyStock: Number(newMatSafetyStock),
        leadTimeDays: Number(newMatLeadTime)
      }, activeDevRole);

      setIsCreateModalOpen(false);
      setNewMatCode('');
      setNewMatName('');
      setNewMatBrand('');
      setNewMatDesc('');
      setNewMatSpecs('');
      await loadMaterialsList();
      alert('ماده اولیه جدید با موفقیت ثبت شد.');
    } catch (err: any) {
      alert(`خطا در ایجاد ماده اولیه: ${err.message}`);
    } finally {
      setFormSubmitting(false);
    }
  };

  const handleAddConversion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeDetailMaterial || !convToUnitId || convFactor <= 0) {
      alert('لطفاً واحد مقصد و ضریب تبدیل معتبر وارد کنید.');
      return;
    }
    try {
      await addUnitConversion(activeDetailMaterial.id, {
        fromUnitId: convToUnitId,
        toUnitId: activeDetailMaterial.baseUnitId,
        factor: Number(convFactor)
      }, activeDevRole);

      // Refresh detail
      const updated = await fetchMaterialDetail(activeDetailMaterial.id, activeDevRole);
      setActiveDetailMaterial(updated);
      alert('ضریب تبدیل واحد با موفقیت ثبت شد.');
    } catch (err: any) {
      alert(`خطا در ثبت تبدیل واحد: ${err.message}`);
    }
  };

  const handleCreateCategory = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newCatName.trim() || !newCatCode.trim()) {
      alert('نام و کد دسته‌بندی اجباری است.');
      return;
    }
    try {
      await createCategory({
        name: newCatName.trim(),
        code: newCatCode.trim(),
        parentId: newCatParentId || undefined
      }, activeDevRole);
      setNewCatName('');
      setNewCatCode('');
      setNewCatParentId('');
      await loadLookups();
      alert('دسته‌بندی جدید با موفقیت ایجاد شد.');
    } catch (err: any) {
      alert(`خطا در ایجاد دسته‌بندی: ${err.message}`);
    }
  };

  const handleMoveCategory = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!moveSourceCatId) {
      alert('دسته‌بندی مبدا را مشخص کنید.');
      return;
    }
    try {
      await moveCategory(moveSourceCatId, moveTargetParentId || null, activeDevRole);
      await loadLookups();
      alert('دسته‌بندی با موفقیت جابجا شد.');
    } catch (err: any) {
      alert(`خطا در جابجایی دسته‌بندی: ${err.message}`);
    }
  };

  // Helper render for category tree
  const renderTreeNodes = (nodes: CategoryTreeDto[], level = 0) => {
    return nodes.map((node) => (
      <div key={node.id} style={{ marginRight: level * 16 }}>
        <div 
          className={`tree-node ${selectedCategoryFilter === node.id ? 'selected' : ''}`}
          onClick={() => {
            setSelectedCategoryFilter(selectedCategoryFilter === node.id ? '' : node.id);
            setPage(1);
          }}
        >
          {node.children && node.children.length > 0 ? (
            <FolderOpen size={16} color="#38bdf8" />
          ) : (
            <Folder size={16} color="var(--text-dim)" />
          )}
          <span style={{ fontWeight: 600 }}>{node.name}</span>
          <span style={{ fontSize: '11px', color: 'var(--text-dim)', marginRight: '6px' }}>({node.code})</span>
          <span className="badge badge-dim" style={{ marginRight: 'auto' }}>
            {node.materialCount} کالا
          </span>
        </div>
        {node.children && node.children.length > 0 && (
          <div className="tree-children">
            {renderTreeNodes(node.children, level + 1)}
          </div>
        )}
      </div>
    ));
  };

  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <div className="view-card">
      {/* Header */}
      <div className="view-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2>کاتالوگ و مشخصات مواد اولیه (Materials)</h2>
          <p>مدیریت جامع درخت دسته‌بندی، واحدهای سنجش، مشخصات فنی آرد، مخمر و سوابق تأمین‌کنندگان</p>
        </div>
        <div style={{ display: 'flex', gap: '10px' }}>
          <button 
            className="btn btn-secondary"
            onClick={() => setIsCategoryModalOpen(true)}
          >
            <Layers size={16} />
            درخت دسته‌بندی‌ها
          </button>
          <button 
            className="btn btn-primary"
            onClick={() => setIsCreateModalOpen(true)}
          >
            <Plus size={16} />
            تعریف ماده اولیه جدید
          </button>
        </div>
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
            placeholder="جستجوی نام، کد کالا یا برند (فارسی / انگلیسی)..."
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

        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          <select 
            className="form-select"
            value={selectedCategoryFilter}
            onChange={(e) => {
              setSelectedCategoryFilter(e.target.value);
              setPage(1);
            }}
            style={{ minWidth: '180px' }}
          >
            <option value="">همه دسته‌بندی‌ها ({categories.length})</option>
            {categories.map(c => {
              const parent = c.parentId ? categories.find(p => p.id === c.parentId) : null;
              return (
                <option key={c.id} value={c.id}>
                  {parent ? `${parent.name} / ${c.name}` : c.name} ({c.code})
                </option>
              );
            })}
          </select>

          {selectedCategoryFilter && (
            <button 
              className="btn btn-outline btn-sm"
              onClick={() => setSelectedCategoryFilter('')}
              title="حذف فیلتر دسته"
            >
              <X size={14} />
              حذف فیلتر
            </button>
          )}
        </div>
      </div>

      {/* Error message */}
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

      {/* Materials Table */}
      <div className="data-table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>کد و شناسه</th>
              <th>نام استاندارد کاتالوگ CRM</th>
              <th>دسته‌بندی کاتالوگ</th>
              <th>برند / بسته‌بندی</th>
              <th>محتویات بسته</th>
              <th>واحدهای زنجیره (خرید / مصرف)</th>
              <th>واحد پایه انبار</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-dim)' }}>
                  در حال بارگذاری اطلاعات کاتالوگ...
                </td>
              </tr>
            ) : materials.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                  ماده اولیه‌ای با این مشخصات یافت نشد.
                </td>
              </tr>
            ) : (
              materials.map((m) => (
                <tr key={m.id} style={m.internalCode.startsWith('TEST-') ? { background: 'rgba(168, 85, 247, 0.05)' } : undefined}>
                  <td>
                    {m.internalCode.startsWith('TEST-') ? (
                      <div>
                        <span style={{ fontFamily: 'monospace', fontWeight: 700, color: '#c084fc' }}>
                          {m.internalCode}
                        </span>
                        <div style={{ marginTop: '2px' }}>
                          <span style={{ 
                            fontSize: '10px', 
                            background: 'rgba(168, 85, 247, 0.2)', 
                            color: '#e9d5ff', 
                            border: '1px solid rgba(168, 85, 247, 0.4)', 
                            borderRadius: '4px', 
                            padding: '1px 6px',
                            fontWeight: 600
                          }}>
                            [داده آزمایشی محلی]
                          </span>
                        </div>
                      </div>
                    ) : (
                      <span style={{ fontFamily: 'monospace', fontWeight: 600, color: '#38bdf8' }}>
                        {m.internalCode}
                      </span>
                    )}
                  </td>
                  <td>
                    <div style={{ fontWeight: 600, color: '#f8fafc' }}>{m.name}</div>
                    {m.sepidarItemTitle && (
                      <div style={{ fontSize: '11px', color: 'var(--text-dim)', marginTop: '2px' }}>
                        سپیدار: {m.sepidarItemTitle}
                      </div>
                    )}
                  </td>
                  <td>
                    <div style={{ fontWeight: 500 }}>{m.categoryName}</div>
                    {m.categorizationStatus === 'NeedsReview' ? (
                      <span className="badge badge-warning" style={{ fontSize: '10px', marginTop: '3px' }}>
                        پیشنهاد هوشمند
                      </span>
                    ) : (m.categorizationStatus === 'ManualConfirmed' || m.isCustomizedByUser) ? (
                      <span className="badge badge-success" style={{ fontSize: '10px', marginTop: '3px' }}>
                        تأیید کاربر (محفوظ در Sync)
                      </span>
                    ) : (
                      <span className="badge badge-dim" style={{ fontSize: '10px', marginTop: '3px' }}>
                        استاندارد
                      </span>
                    )}
                  </td>
                  <td>
                    <span style={{ fontSize: '13px' }}>
                      {m.brand || '-'} {m.packaging ? `/ ${m.packaging}` : ''}
                    </span>
                  </td>
                  <td>
                    {m.packageSize ? (
                      <span className="badge badge-dim" style={{ direction: 'ltr', fontWeight: 600 }}>
                        {m.packageSize} {m.packageSizeUnitName || m.packageSizeUnitCode || ''}
                      </span>
                    ) : (
                      <span style={{ color: 'var(--text-dim)' }}>-</span>
                    )}
                  </td>
                  <td>
                    <div style={{ fontSize: '11px' }}>
                      خرید: <strong style={{ color: '#38bdf8' }}>{m.purchaseUnitName || m.baseUnitName}</strong>
                    </div>
                    <div style={{ fontSize: '11px', marginTop: '2px' }}>
                      مصرف: <strong style={{ color: '#4ade80' }}>{m.consumptionUnitName || m.baseUnitName}</strong>
                    </div>
                  </td>
                  <td>
                    <span className="badge badge-primary">{m.baseUnitName} ({m.baseUnitCode})</span>
                  </td>
                  <td>
                    <button 
                      className="btn btn-secondary btn-sm"
                      onClick={() => openDetail(m.id)}
                      style={{ whiteSpace: 'nowrap' }}
                    >
                      <Edit3 size={13} style={{ marginLeft: '4px' }} />
                      مشاهده و ویرایش
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
        <span>نمایش {materials.length} از {totalCount} ماده اولیه ثبت‌شده</span>
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

      {/* MODAL: Material Detail & Conversions */}
      {activeDetailMaterial && (
        <div className="modal-overlay" onClick={closeDetail}>
          <div className="modal-content large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
                <Package size={22} color="#38bdf8" />
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <h3 style={{ margin: 0 }}>{activeDetailMaterial.name}</h3>
                    {activeDetailMaterial.internalCode.startsWith('TEST-') && (
                      <span style={{ 
                        background: 'rgba(168, 85, 247, 0.2)', 
                        color: '#e9d5ff', 
                        border: '1px solid rgba(168, 85, 247, 0.5)', 
                        borderRadius: '4px', 
                        padding: '2px 8px', 
                        fontSize: '11px', 
                        fontWeight: 600 
                      }}>
                        [داده آزمایشی محلی - مستقل از سپیدار]
                      </span>
                    )}
                  </div>
                  <span style={{ fontSize: '12px', color: 'var(--text-dim)', fontFamily: 'monospace' }}>
                    کد داخلی: {activeDetailMaterial.internalCode} | واحد پایه: {activeDetailMaterial.baseUnitName} ({activeDetailMaterial.baseUnitCode})
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
                  className={`tab-btn ${detailTab === 'info' ? 'active' : ''}`}
                  onClick={() => setDetailTab('info')}
                >
                  مشخصات کاتالوگ استاندارد و انبار
                </button>
                <button 
                  className={`tab-btn ${detailTab === 'conversions' ? 'active' : ''}`}
                  onClick={() => setDetailTab('conversions')}
                >
                  ضرایب تبدیل واحد ({activeDetailMaterial.unitConversions?.length || 0})
                </button>
                <button 
                  className={`tab-btn ${detailTab === 'suppliers' ? 'active' : ''}`}
                  onClick={() => setDetailTab('suppliers')}
                >
                  تأمین‌کنندگان مرتبط ({activeDetailMaterial.suppliers?.length || 0})
                </button>
              </div>

              {/* Tab 1: Info & Standard Catalog */}
              {detailTab === 'info' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
                  
                  {/* Notice for Test Item */}
                  {activeDetailMaterial.internalCode.startsWith('TEST-') && (
                    <div style={{
                      background: 'rgba(168, 85, 247, 0.12)',
                      border: '1px solid rgba(168, 85, 247, 0.35)',
                      borderRadius: '8px',
                      padding: '10px 14px',
                      fontSize: '12px',
                      color: '#d8b4fe',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '8px'
                    }}>
                      <AlertCircle size={16} color="#c084fc" />
                      <span>
                        <strong>رکورد آزمایشی محلی:</strong> این قلم جهت آزمون استقلال بسته‌بندی‌ها و رفتار همگام‌سازی کاتالوگ CRM تعریف شده و با کالای واقعی سپیدار ارتباط عملیاتی ندارد.
                      </span>
                    </div>
                  )}

                  {/* Smart Categorization Proposal Banner */}
                  {(activeDetailMaterial.categorizationStatus === 'NeedsReview' || !activeDetailMaterial.isCustomizedByUser) ? (
                    <div style={{
                      background: 'rgba(234, 179, 8, 0.1)',
                      border: '1px solid rgba(234, 179, 8, 0.35)',
                      borderRadius: '8px',
                      padding: '14px 18px',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      gap: '14px',
                      flexWrap: 'wrap'
                    }}>
                      <div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#facc15', fontWeight: 600, fontSize: '13px' }}>
                          <Sparkles size={16} />
                          <span>پیشنهاد هوشمند دسته‌بندی سیستم</span>
                        </div>
                        <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                          دسته‌بندی پیشنهادی: <strong>{activeDetailMaterial.categoryName}</strong>. می‌توانید پیشنهاد را تأیید کنید یا مشخصات را اصلاح نمایید. برای تغییر دسته‌بندی یا نام نیازی به تعیین ضریب تبدیل نیست.
                        </div>
                      </div>
                      <button 
                        className="btn btn-primary btn-sm"
                        style={{ background: '#eab308', color: '#000', fontWeight: 700, whiteSpace: 'nowrap' }}
                        onClick={handleConfirmCategorization}
                      >
                        <Check size={14} />
                        تأیید پیشنهاد دسته‌بندی
                      </button>
                    </div>
                  ) : (
                    <div style={{
                      background: 'rgba(34, 197, 94, 0.1)',
                      border: '1px solid rgba(34, 197, 94, 0.3)',
                      borderRadius: '8px',
                      padding: '12px 16px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '10px'
                    }}>
                      <CheckCircle2 size={16} color="#4ade80" />
                      <span style={{ fontSize: '12px', color: '#4ade80', fontWeight: 600 }}>
                        مشخصات این کالا توسط کاربر تأیید و شخصی‌سازی شده است و در همگام‌سازی‌های بعدی سپیدار دست‌نخورده حفظ خواهد شد.
                      </span>
                    </div>
                  )}

                  {/* Read-Only Sepidar Original Information Card */}
                  <div style={{
                    background: 'rgba(15, 23, 42, 0.65)',
                    border: '1px solid rgba(56, 189, 248, 0.25)',
                    borderRadius: '8px',
                    padding: '14px 16px'
                  }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
                      <Lock size={15} color="#38bdf8" />
                      <span style={{ fontSize: '13px', fontWeight: 700, color: '#38bdf8' }}>
                        اطلاعات اصلی سیستم سپیدار (فقط خواندنی / Read-Only)
                      </span>
                      <span style={{ fontSize: '11px', color: 'var(--text-dim)', marginRight: 'auto' }}>
                        ثبت‌شده در مبدا حسابداری و انبار سپیدار
                      </span>
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '12px' }}>
                      <div style={{ background: 'var(--bg-main)', padding: '10px 12px', borderRadius: '6px', border: '1px solid var(--border-color)' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>کد کالا در سپیدار:</div>
                        <div style={{ fontSize: '13px', fontWeight: 600, fontFamily: 'monospace', color: '#f8fafc', marginTop: '3px' }}>
                          {activeDetailMaterial.sepidarItemCode || (activeDetailMaterial.internalCode.startsWith('TEST-') ? 'TEST-MAPPING (آزمایشی)' : 'فاقد نگاشت')}
                        </div>
                      </div>
                      <div style={{ background: 'var(--bg-main)', padding: '10px 12px', borderRadius: '6px', border: '1px solid var(--border-color)' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>عنوان کالا در سپیدار:</div>
                        <div style={{ fontSize: '13px', fontWeight: 600, color: '#f8fafc', marginTop: '3px' }}>
                          {activeDetailMaterial.sepidarItemTitle || (activeDetailMaterial.internalCode.startsWith('TEST-') ? 'عنوان آزمایشی مستقل' : activeDetailMaterial.name)}
                        </div>
                      </div>
                      <div style={{ background: 'var(--bg-main)', padding: '10px 12px', borderRadius: '6px', border: '1px solid var(--border-color)' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>واحد سنجش سپیدار:</div>
                        <div style={{ fontSize: '13px', fontWeight: 600, color: '#38bdf8', marginTop: '3px' }}>
                          {activeDetailMaterial.sepidarBaseUnit || activeDetailMaterial.baseUnitName}
                        </div>
                      </div>
                      <div style={{ background: 'var(--bg-main)', padding: '10px 12px', borderRadius: '6px', border: '1px solid var(--border-color)' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-dim)' }}>وضعیت اتصال ERP:</div>
                        <div style={{ fontSize: '12px', fontWeight: 600, color: activeDetailMaterial.sepidarItemCode ? '#4ade80' : '#c084fc', marginTop: '3px' }}>
                          {activeDetailMaterial.sepidarItemCode ? '● متصل به سپیدار' : '◆ کالای مستقل محلی CRM'}
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Editable CRM Standard Catalog Form */}
                  <div style={{
                    background: 'var(--bg-sidebar)',
                    border: '1px solid var(--border-color)',
                    borderRadius: '8px',
                    padding: '16px'
                  }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '14px' }}>
                      <Edit3 size={16} color="#4ade80" />
                      <span style={{ fontSize: '14px', fontWeight: 700, color: '#f8fafc' }}>
                        مشخصات استاندارد کاتالوگ CRM (مشاهده و ویرایش)
                      </span>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px' }}>
                      <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                        <label>نام استاندارد کالا در کاتالوگ CRM:</label>
                        <input 
                          type="text" 
                          className="form-input" 
                          value={editName} 
                          onChange={(e) => setEditName(e.target.value)}
                          placeholder="نام استاندارد کامل..." 
                        />
                      </div>

                      <div className="form-group">
                        <label>دسته و زیردسته کالا:</label>
                        <select 
                          className="form-select"
                          value={editCategoryId}
                          onChange={(e) => setEditCategoryId(e.target.value)}
                        >
                          <option value="">انتخاب دسته‌بندی...</option>
                          {categories.map((c) => {
                            const parent = c.parentId ? categories.find(p => p.id === c.parentId) : null;
                            const label = parent ? `${parent.name} / ${c.name}` : c.name;
                            return (
                              <option key={c.id} value={c.id}>{label} ({c.code})</option>
                            );
                          })}
                        </select>
                      </div>

                      <div className="form-group">
                        <label>برند / کارخانه تولیدی:</label>
                        <input 
                          type="text" 
                          className="form-input" 
                          value={editBrand} 
                          onChange={(e) => setEditBrand(e.target.value)}
                          placeholder="مثلاً: دامداران، کاله، پاک..." 
                        />
                      </div>

                      <div className="form-group">
                        <label>نوع بسته‌بندی:</label>
                        <input 
                          type="text" 
                          className="form-input" 
                          value={editPackaging} 
                          onChange={(e) => setEditPackaging(e.target.value)}
                          placeholder="مثلاً: پاکت، کیسه، بطری، قوطی، کارتن..." 
                        />
                      </div>

                      <div className="form-group">
                        <label>مقدار محتویات بسته و واحد آن:</label>
                        <div style={{ display: 'flex', gap: '8px' }}>
                          <input 
                            type="number" 
                            step="any"
                            className="form-input" 
                            style={{ flex: 1 }}
                            value={editPackageSize} 
                            onChange={(e) => setEditPackageSize(e.target.value ? Number(e.target.value) : '')}
                            placeholder="مثلاً ۱ یا ۲۵۰" 
                          />
                          <select 
                            className="form-select"
                            style={{ flex: 1 }}
                            value={editPackageSizeUnitId}
                            onChange={(e) => setEditPackageSizeUnitId(e.target.value)}
                          >
                            <option value="">واحد محتوا...</option>
                            {units.map((u) => (
                              <option key={u.id} value={u.id}>{u.name} ({u.code})</option>
                            ))}
                          </select>
                        </div>
                      </div>

                      <div className="form-group">
                        <label>واحد شمارش و خرید (Purchase Unit):</label>
                        <select 
                          className="form-select"
                          value={editPurchaseUnitId}
                          onChange={(e) => setEditPurchaseUnitId(e.target.value)}
                        >
                          <option value="">همان واحد پایه ({activeDetailMaterial.baseUnitName})</option>
                          {units.map((u) => (
                            <option key={u.id} value={u.id}>{u.name} ({u.code})</option>
                          ))}
                        </select>
                      </div>

                      <div className="form-group">
                        <label>واحد مصرف در فرمول/تولید (Consumption Unit):</label>
                        <select 
                          className="form-select"
                          value={editConsumptionUnitId}
                          onChange={(e) => setEditConsumptionUnitId(e.target.value)}
                        >
                          <option value="">همان واحد پایه ({activeDetailMaterial.baseUnitName})</option>
                          {units.map((u) => (
                            <option key={u.id} value={u.id}>{u.name} ({u.code})</option>
                          ))}
                        </select>
                      </div>

                      <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                        <label>واحد مبنای کالا در انبار CRM (کاردکس و موجودی):</label>
                        <div style={{
                          background: 'rgba(239, 68, 68, 0.08)',
                          border: '1px solid rgba(239, 68, 68, 0.25)',
                          borderRadius: '6px',
                          padding: '10px 14px',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          flexWrap: 'wrap',
                          gap: '8px'
                        }}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <Lock size={15} color="#f87171" />
                            <span style={{ fontSize: '13px', fontWeight: 600 }}>{activeDetailMaterial.baseUnitName} ({activeDetailMaterial.baseUnitCode})</span>
                          </div>
                          <span style={{ fontSize: '11px', color: '#f87171' }}>
                            تغییر مستقیم واحد مبنا برای کالای دارای سابقه مسدود است و نیاز به فرآیند انتقال تاریخی دارد.
                          </span>
                        </div>
                      </div>

                      <div className="form-group">
                        <label>حداقل موجودی (Min Stock):</label>
                        <input 
                          type="number" 
                          className="form-input" 
                          value={editMinStock} 
                          onChange={(e) => setEditMinStock(Number(e.target.value))} 
                        />
                      </div>

                      <div className="form-group">
                        <label>نقطه سفارش احتیاطی (Safety Stock):</label>
                        <input 
                          type="number" 
                          className="form-input" 
                          value={editSafetyStock} 
                          onChange={(e) => setEditSafetyStock(Number(e.target.value))} 
                        />
                      </div>

                      <div className="form-group">
                        <label>زمان تحویل تأمین‌کننده (Lead Time به روز):</label>
                        <input 
                          type="number" 
                          className="form-input" 
                          value={editLeadTime} 
                          onChange={(e) => setEditLeadTime(Number(e.target.value))} 
                        />
                      </div>

                      <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                        <label>شرح و توضیحات مصرف در تولید:</label>
                        <textarea 
                          className="form-input" 
                          rows={2} 
                          value={editDesc} 
                          onChange={(e) => setEditDesc(e.target.value)}
                          placeholder="توضیحات تکمیلی..."
                        />
                      </div>

                      <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                        <label>مشخصات فنی و استانداردها (Specifications):</label>
                        <textarea 
                          className="form-input" 
                          rows={2} 
                          value={editSpecs} 
                          onChange={(e) => setEditSpecs(e.target.value)}
                          placeholder="مشخصات تخصصی، رطوبت، درصد خلوص، گرماژ و..."
                        />
                      </div>
                    </div>

                    {editSuccessMsg && (
                      <div style={{
                        background: 'rgba(34, 197, 94, 0.15)',
                        color: '#4ade80',
                        border: '1px solid rgba(34, 197, 94, 0.3)',
                        borderRadius: '6px',
                        padding: '10px 14px',
                        marginTop: '14px',
                        fontSize: '12px'
                      }}>
                        {editSuccessMsg}
                      </div>
                    )}

                    <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px' }}>
                      <button 
                        className="btn btn-primary"
                        onClick={handleUpdateMaterial}
                        disabled={isSavingEdit}
                      >
                        <Save size={16} />
                        {isSavingEdit ? 'در حال ذخیره‌سازی...' : 'ذخیره اصلاحات کاتالوگ استاندارد (حفظ در همگام‌سازی)'}
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {/* Tab 2: Conversions */}
              {detailTab === 'conversions' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
                  <div style={{ 
                    background: 'rgba(56, 189, 248, 0.08)',
                    border: '1px solid rgba(56, 189, 248, 0.25)',
                    borderRadius: '8px',
                    padding: '12px 16px',
                    fontSize: '12px', 
                    color: 'var(--text-muted)',
                    lineHeight: '1.6'
                  }}>
                    💡 <strong>قاعده تأیید ضرایب:</strong> تبدیل‌های استخراج‌شده از نام کالا به صورت اولیه «پیشنهاد تأییدنشده» ثبت می‌شوند. تنها پس از بررسی و فشردن دکمه «تأیید و فعال‌سازی»، این ضرایب در محاسبات موجودی، کاردکس و سفارشات سیستم فعال خواهند شد.
                  </div>

                  <div className="data-table-container">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>واحد مبدا</th>
                          <th>ضریب تبدیل</th>
                          <th>واحد پایه (مقصد)</th>
                          <th>فرمول معادل</th>
                          <th>یادداشت / شاهد</th>
                          <th>وضعیت تأیید</th>
                          <th>عملیات</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(!activeDetailMaterial.unitConversions || activeDetailMaterial.unitConversions.length === 0) ? (
                          <tr>
                            <td colSpan={7} style={{ textAlign: 'center', padding: '20px', color: 'var(--text-dim)' }}>
                              ضریب تبدیل واحدی برای این ماده ثبت نشده است.
                            </td>
                          </tr>
                        ) : (
                          activeDetailMaterial.unitConversions.map((conv) => (
                            <tr key={conv.id}>
                              <td>
                                <span className="badge badge-primary">{conv.fromUnitName} ({conv.fromUnitCode})</span>
                              </td>
                              <td style={{ fontWeight: 700, color: '#38bdf8' }}>
                                × {conv.factor}
                              </td>
                              <td>
                                <span className="badge badge-dim">{conv.toUnitName} ({conv.toUnitCode})</span>
                              </td>
                              <td style={{ fontSize: '12px', direction: 'ltr', textAlign: 'right' }}>
                                1 {conv.fromUnitCode} = {conv.factor} {conv.toUnitCode}
                              </td>
                              <td style={{ fontSize: '11px', color: 'var(--text-dim)' }}>
                                {conv.notes || 'استخراج خودکار'}
                              </td>
                              <td>
                                {conv.isConfirmed ? (
                                  <span className="badge badge-success" style={{ fontSize: '11px' }}>
                                    ✅ تأیید شده (فعال)
                                  </span>
                                ) : (
                                  <span className="badge badge-warning" style={{ fontSize: '11px' }}>
                                    ⏳ پیشنهاد اولیه (غیرفعال)
                                  </span>
                                )}
                              </td>
                              <td>
                                {!conv.isConfirmed ? (
                                  <button 
                                    className="btn btn-primary btn-sm"
                                    onClick={() => handleConfirmConversion(conv.id)}
                                    style={{ fontSize: '11px', whiteSpace: 'nowrap' }}
                                  >
                                    <Check size={12} />
                                    تأیید و فعال‌سازی
                                  </button>
                                ) : (
                                  <span style={{ fontSize: '11px', color: 'var(--text-dim)' }}>فعال در محاسبات</span>
                                )}
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Add conversion form */}
                  <form onSubmit={handleAddConversion} style={{ 
                    background: 'var(--bg-sidebar)', 
                    padding: '16px', 
                    borderRadius: '8px', 
                    border: '1px solid var(--border-color)' 
                  }}>
                    <h4 style={{ fontSize: '13px', marginBottom: '12px', color: '#fff' }}>افزودن ضریب تبدیل واحد جدید</h4>
                    <div className="form-row">
                      <div className="form-group">
                        <label>واحد مبدا (خرید/ورودی):</label>
                        <select 
                          className="form-select"
                          value={convToUnitId}
                          onChange={(e) => setConvToUnitId(e.target.value)}
                        >
                          {units.filter(u => u.id !== activeDetailMaterial.baseUnitId).map(u => (
                            <option key={u.id} value={u.id}>{u.name} ({u.code})</option>
                          ))}
                        </select>
                      </div>
                      <div className="form-group">
                        <label>مقدار ضریب نسبت به واحد پایه ({activeDetailMaterial.baseUnitCode}):</label>
                        <input 
                          type="number" 
                          step="0.001" 
                          className="form-input" 
                          value={convFactor} 
                          onChange={(e) => setConvFactor(Number(e.target.value))}
                          placeholder="مثلاً ۵۰ برای کیسه ۵۰ کیلویی"
                        />
                      </div>
                      <div className="form-group" style={{ alignSelf: 'flex-end' }}>
                        <button type="submit" className="btn btn-primary" style={{ width: '100%' }}>
                          <Scale size={15} />
                          ثبت ضریب تبدیل
                        </button>
                      </div>
                    </div>
                  </form>
                </div>
              )}

              {/* Tab 3: Suppliers */}
              {detailTab === 'suppliers' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                  <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    لیست تأمین‌کنندگانی که این ماده اولیه را در کاتالوگ فروش خود دارند به همراه آخرین نرخ استعلامی:
                  </div>

                  <div className="data-table-container">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>نام تأمین‌کننده</th>
                          <th>اولویت</th>
                          <th>وضعیت</th>
                          <th>آخرین قیمت استعلامی</th>
                          <th>واحد قیمت</th>
                          <th>تاریخ استعلام</th>
                          <th>عملیات</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(!activeDetailMaterial.suppliers || activeDetailMaterial.suppliers.length === 0) ? (
                          <tr>
                            <td colSpan={7} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-dim)' }}>
                              هنوز تأمین‌کننده‌ای برای این ماده اولیه ثبت نشده است.
                            </td>
                          </tr>
                        ) : (
                          activeDetailMaterial.suppliers.map((s) => (
                            <tr key={s.supplierId}>
                              <td style={{ fontWeight: 600 }}>{s.supplierName}</td>
                              <td>
                                {s.isPreferred ? (
                                  <span className="badge badge-success">تأمین‌کننده اصلی</span>
                                ) : (
                                  <span className="badge badge-dim">عادی</span>
                                )}
                              </td>
                              <td>
                                <span className="badge badge-primary">{s.status}</span>
                              </td>
                              <td style={{ fontWeight: 700, color: '#34d399' }}>
                                {s.latestPrice != null ? s.latestPrice.toLocaleString('fa-IR') : 'بدون قیمت'}
                              </td>
                              <td>{s.currency || 'ریال'} / {s.priceUnitName || activeDetailMaterial.baseUnitName}</td>
                              <td style={{ fontSize: '12px', color: 'var(--text-dim)' }}>
                                {s.priceEffectiveFrom ? formatJalali(s.priceEffectiveFrom) : '-'}
                              </td>
                              <td>
                                <button 
                                  className="btn btn-outline btn-sm"
                                  onClick={() => {
                                    closeDetail();
                                    if (onNavigateToSupplier) onNavigateToSupplier(s.supplierId);
                                  }}
                                  title="مشاهده پروفایل تأمین‌کننده"
                                >
                                  <ExternalLink size={13} />
                                  پروفایل تأمین‌کننده
                                </button>
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

      {/* MODAL: Create New Material */}
      {isCreateModalOpen && (
        <div className="modal-overlay" onClick={() => setIsCreateModalOpen(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Plus size={20} color="#38bdf8" />
                <h3 style={{ margin: 0 }}>تعریف ماده اولیه جدید</h3>
              </div>
              <button 
                onClick={() => setIsCreateModalOpen(false)}
                style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleCreateMaterial}>
              <div className="modal-body">
                <div className="form-row">
                  <div className="form-group">
                    <label>کد داخلی کالا (یکتا) *:</label>
                    <input 
                      type="text" 
                      className="form-input"
                      placeholder="مثال: MAT-FLOUR-01" 
                      value={newMatCode}
                      onChange={(e) => setNewMatCode(e.target.value)}
                      required
                    />
                  </div>
                  <div className="form-group">
                    <label>نام ماده اولیه *:</label>
                    <input 
                      type="text" 
                      className="form-input"
                      placeholder="مثال: آرد گندم ستاره نان فانتزی" 
                      value={newMatName}
                      onChange={(e) => setNewMatName(e.target.value)}
                      required
                    />
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label>دسته‌بندی کاتالوگ *:</label>
                    <select 
                      className="form-select"
                      value={newMatCatId}
                      onChange={(e) => setNewMatCatId(e.target.value)}
                      required
                    >
                      {categories.map(c => {
                        const parent = c.parentId ? categories.find(p => p.id === c.parentId) : null;
                        const label = parent ? `${parent.name} / ${c.name}` : c.name;
                        return (
                          <option key={c.id} value={c.id}>{label} ({c.code})</option>
                        );
                      })}
                    </select>
                  </div>
                  <div className="form-group">
                    <label>واحد سنجش پایه *:</label>
                    <select 
                      className="form-select"
                      value={newMatUnitId}
                      onChange={(e) => setNewMatUnitId(e.target.value)}
                      required
                    >
                      {units.map(u => (
                        <option key={u.id} value={u.id}>{u.name} ({u.code})</option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label>برند / کارخانه تولیدی:</label>
                    <input 
                      type="text" 
                      className="form-input"
                      placeholder="مثال: کارخانه آرد بیگلری" 
                      value={newMatBrand}
                      onChange={(e) => setNewMatBrand(e.target.value)}
                    />
                  </div>
                  <div className="form-group">
                    <label>زمان تحویل استاندارد (روز):</label>
                    <input 
                      type="number" 
                      className="form-input"
                      value={newMatLeadTime}
                      onChange={(e) => setNewMatLeadTime(Number(e.target.value))}
                      min={0}
                    />
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label>حداقل موجودی مجاز:</label>
                    <input 
                      type="number" 
                      className="form-input"
                      value={newMatMinStock}
                      onChange={(e) => setNewMatMinStock(Number(e.target.value))}
                      min={0}
                    />
                  </div>
                  <div className="form-group">
                    <label>نقطه سفارش احتیاطی (Safety Stock):</label>
                    <input 
                      type="number" 
                      className="form-input"
                      value={newMatSafetyStock}
                      onChange={(e) => setNewMatSafetyStock(Number(e.target.value))}
                      min={0}
                    />
                  </div>
                </div>

                <div className="form-group">
                  <label>شرح و توضیحات مصرف در تولید:</label>
                  <textarea 
                    className="form-textarea"
                    placeholder="توضیحات تکمیلی پیرامون نوع مصرف و دمای نگهداری..."
                    value={newMatDesc}
                    onChange={(e) => setNewMatDesc(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label>مشخصات فنی آزمایشگاهی (Specifications):</label>
                  <textarea 
                    className="form-textarea"
                    placeholder="درصد گلوتن مرطوب: ۲۷٪، خاکستر: ۰.۶۵٪، رطوبت: حداکثر ۱۴٪"
                    value={newMatSpecs}
                    onChange={(e) => setNewMatSpecs(e.target.value)}
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
                  {formSubmitting ? 'در حال ثبت...' : 'ذخیره ماده اولیه'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: Manage Category Tree */}
      {isCategoryModalOpen && (
        <div className="modal-overlay" onClick={() => setIsCategoryModalOpen(false)}>
          <div className="modal-content large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Layers size={20} color="#38bdf8" />
                <h3 style={{ margin: 0 }}>مدیریت درخت دسته‌بندی‌های کاتالوگ</h3>
              </div>
              <button 
                onClick={() => setIsCategoryModalOpen(false)}
                style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div className="modal-body">
              <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: '20px' }}>
                {/* Left: Tree representation */}
                <div>
                  <h4 style={{ fontSize: '13px', marginBottom: '8px', color: '#fff' }}>ساختار درختی فعلی</h4>
                  <div className="tree-container">
                    {categoryTree.length === 0 ? (
                      <div style={{ padding: '20px', textAlign: 'center', color: 'var(--text-dim)' }}>
                        دسته‌بندی‌ای یافت نشد.
                      </div>
                    ) : (
                      renderTreeNodes(categoryTree)
                    )}
                  </div>
                </div>

                {/* Right: Actions */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                  {/* Create Subcategory */}
                  <form onSubmit={handleCreateCategory} style={{ 
                    background: 'var(--bg-sidebar)', 
                    padding: '16px', 
                    borderRadius: '8px', 
                    border: '1px solid var(--border-color)' 
                  }}>
                    <h4 style={{ fontSize: '13px', marginBottom: '10px', color: '#fff' }}>افزودن دسته‌بندی جدید</h4>
                    <div className="form-group" style={{ marginBottom: '10px' }}>
                      <label>دسته‌بندی والد (اختیاری برای ریشه):</label>
                      <select 
                        className="form-select"
                        value={newCatParentId}
                        onChange={(e) => setNewCatParentId(e.target.value)}
                      >
                        <option value="">بدون والد (سطح ریشه Root)</option>
                        {categories.map(c => (
                          <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                        ))}
                      </select>
                    </div>
                    <div className="form-group" style={{ marginBottom: '10px' }}>
                      <label>نام دسته‌بندی *:</label>
                      <input 
                        type="text" 
                        className="form-input" 
                        placeholder="مثلاً انواع مخمر خشک" 
                        value={newCatName}
                        onChange={(e) => setNewCatName(e.target.value)}
                        required
                      />
                    </div>
                    <div className="form-group" style={{ marginBottom: '14px' }}>
                      <label>کد دسته‌بندی *:</label>
                      <input 
                        type="text" 
                        className="form-input" 
                        placeholder="مثلاً YST-DRY" 
                        value={newCatCode}
                        onChange={(e) => setNewCatCode(e.target.value)}
                        required
                      />
                    </div>
                    <button type="submit" className="btn btn-primary btn-sm" style={{ width: '100%' }}>
                      <Plus size={14} />
                      ایجاد دسته‌بندی
                    </button>
                  </form>

                  {/* Move Category */}
                  <form onSubmit={handleMoveCategory} style={{ 
                    background: 'var(--bg-sidebar)', 
                    padding: '16px', 
                    borderRadius: '8px', 
                    border: '1px solid var(--border-color)' 
                  }}>
                    <h4 style={{ fontSize: '13px', marginBottom: '10px', color: '#fff' }}>جابجایی دسته‌بندی در درخت</h4>
                    <div className="form-group" style={{ marginBottom: '10px' }}>
                      <label>دسته‌بندی مورد نظر برای انتقال:</label>
                      <select 
                        className="form-select"
                        value={moveSourceCatId}
                        onChange={(e) => setMoveSourceCatId(e.target.value)}
                        required
                      >
                        <option value="">انتخاب کنید...</option>
                        {categories.map(c => (
                          <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                        ))}
                      </select>
                    </div>
                    <div className="form-group" style={{ marginBottom: '14px' }}>
                      <label>والد جدید:</label>
                      <select 
                        className="form-select"
                        value={moveTargetParentId}
                        onChange={(e) => setMoveTargetParentId(e.target.value)}
                      >
                        <option value="">سطح ریشه Root</option>
                        {categories.map(c => (
                          <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                        ))}
                      </select>
                    </div>
                    <button type="submit" className="btn btn-secondary btn-sm" style={{ width: '100%' }}>
                      انتقال شاخه (با بررسی پیشگیری از چرخه)
                    </button>
                  </form>
                </div>
              </div>
            </div>

            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setIsCategoryModalOpen(false)}>
                بستن
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
