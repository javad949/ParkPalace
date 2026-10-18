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
  Scale
} from 'lucide-react';
import { 
  fetchMaterials, 
  fetchMaterialDetail, 
  fetchCategories, 
  fetchCategoryTree, 
  fetchUnits, 
  createMaterial, 
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
    if (onClearSelectedMaterial) onClearSelectedMaterial();
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
            {categories.map(c => (
              <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
            ))}
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
              <th>کد داخلی</th>
              <th>نام ماده اولیه</th>
              <th>دسته‌بندی</th>
              <th>واحد پایه</th>
              <th>برند / مشخصات</th>
              <th>حداقل / ذخیره احتیاطی</th>
              <th>زمان تأمین</th>
              <th>تأمین‌کنندگان</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={9} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-dim)' }}>
                  در حال بارگذاری اطلاعات کاتالوگ...
                </td>
              </tr>
            ) : materials.length === 0 ? (
              <tr>
                <td colSpan={9} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                  ماده اولیه‌ای با این مشخصات یافت نشد.
                </td>
              </tr>
            ) : (
              materials.map((m) => (
                <tr key={m.id}>
                  <td>
                    <span style={{ fontFamily: 'monospace', fontWeight: 600, color: '#38bdf8' }}>
                      {m.internalCode}
                    </span>
                  </td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{m.name}</div>
                    {m.description && (
                      <div style={{ fontSize: '11px', color: 'var(--text-dim)', maxWidth: '220px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {m.description}
                      </div>
                    )}
                  </td>
                  <td>
                    <span className="badge badge-dim">{m.categoryName}</span>
                  </td>
                  <td>
                    <span className="badge badge-primary">{m.baseUnitName} ({m.baseUnitCode})</span>
                  </td>
                  <td>{m.brand || '-'}</td>
                  <td>
                    <span style={{ fontSize: '12px' }}>
                      {m.minStock ?? 0} / <strong style={{ color: '#fbbf24' }}>{m.safetyStock ?? 0}</strong> {m.baseUnitCode}
                    </span>
                  </td>
                  <td>
                    <span style={{ fontSize: '12px' }}>{m.leadTimeDays ?? 1} روز</span>
                  </td>
                  <td>
                    <span className={`badge ${m.supplierCount > 0 ? 'badge-success' : 'badge-dim'}`}>
                      {m.supplierCount} تأمین‌کننده
                    </span>
                  </td>
                  <td>
                    <button 
                      className="btn btn-secondary btn-sm"
                      onClick={() => openDetail(m.id)}
                    >
                      جزئیات و ضرایب
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
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <Package size={22} color="#38bdf8" />
                <div>
                  <h3 style={{ margin: 0 }}>{activeDetailMaterial.name}</h3>
                  <span style={{ fontSize: '12px', color: 'var(--text-dim)', fontFamily: 'monospace' }}>
                    کد داخلی: {activeDetailMaterial.internalCode} | واحد پایه: {activeDetailMaterial.baseUnitName}
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
                  مشخصات فنی و انبار
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

              {/* Tab 1: Info */}
              {detailTab === 'info' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                  <div className="stats-grid" style={{ marginBottom: 0 }}>
                    <div className="stat-box">
                      <span>حداقل موجودی (Min Stock)</span>
                      <strong>{activeDetailMaterial.minStock ?? 0} {activeDetailMaterial.baseUnitCode}</strong>
                    </div>
                    <div className="stat-box">
                      <span>نقطه سفارش احتیاطی (Safety)</span>
                      <strong style={{ color: '#fbbf24' }}>{activeDetailMaterial.safetyStock ?? 0} {activeDetailMaterial.baseUnitCode}</strong>
                    </div>
                    <div className="stat-box">
                      <span>دوره تحویل استاندارد (Lead Time)</span>
                      <strong>{activeDetailMaterial.leadTimeDays ?? 1} روز</strong>
                    </div>
                  </div>

                  <div className="form-group">
                    <label>دسته‌بندی کاتالوگ:</label>
                    <div style={{ fontSize: '13px', fontWeight: 600 }}>{activeDetailMaterial.categoryName}</div>
                  </div>

                  <div className="form-group">
                    <label>برند / کارخانه تولیدی:</label>
                    <div style={{ fontSize: '13px' }}>{activeDetailMaterial.brand || 'ثبت نشده'}</div>
                  </div>

                  <div className="form-group">
                    <label>شرح و توضیحات مصرف در تولید:</label>
                    <div style={{ fontSize: '13px', color: 'var(--text-muted)', lineHeight: '1.6' }}>
                      {activeDetailMaterial.description || 'توضیحاتی برای این کالا ثبت نشده است.'}
                    </div>
                  </div>

                  <div className="form-group">
                    <label>مشخصات فنی و استانداردها (Specifications):</label>
                    <div style={{ 
                      background: 'var(--bg-sidebar)', 
                      padding: '12px', 
                      borderRadius: '8px', 
                      fontSize: '13px',
                      color: 'var(--text-main)',
                      border: '1px solid var(--border-color)',
                      whiteSpace: 'pre-wrap'
                    }}>
                      {activeDetailMaterial.specifications || 'مشخصات فنی تخصصی (مانند درصد خاکستر، گلوتن، رطوبت یا گرماژ) درج نشده است.'}
                    </div>
                  </div>
                </div>
              )}

              {/* Tab 2: Conversions */}
              {detailTab === 'conversions' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
                  <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    ضرایب تبدیل واحد به شما اجازه می‌دهند خرید را با واحدهای بسته‌بندی یا گونی انجام داده و موجودی انبار را با واحد پایه محاسبه کنید.
                  </div>

                  <div className="data-table-container">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>واحد مبدا</th>
                          <th>ضریب تبدیل</th>
                          <th>واحد پایه (مقصد)</th>
                          <th>فرمول معادل</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(!activeDetailMaterial.unitConversions || activeDetailMaterial.unitConversions.length === 0) ? (
                          <tr>
                            <td colSpan={4} style={{ textAlign: 'center', padding: '20px', color: 'var(--text-dim)' }}>
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
                                {s.priceEffectiveFrom ? new Date(s.priceEffectiveFrom).toLocaleDateString('fa-IR') : '-'}
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
                      {categories.map(c => (
                        <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                      ))}
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
