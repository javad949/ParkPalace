import React, { useState, useEffect, useRef } from 'react';
import { 
  Headphones, 
  PhoneIncoming, 
  PhoneOutgoing, 
  Play, 
  Volume2, 
  Building2, 
  Clock, 
  CheckCircle, 
  AlertCircle, 
  Search, 
  Check, 
  X, 
  ShieldCheck, 
  Radio,
  ExternalLink,
  DollarSign,
  RefreshCw,
  Edit2
} from 'lucide-react';
import { 
  CallRecordDto, 
  CallNoteDto, 
  PartyDto, 
  UserExtensionDto,
  CreateCallNoteRequest, 
  ClassifyCallRequest,
  UpdateCallerAssociationRequest,
  SepidarPartyFinancialHistoryDto,
  SimulateCallRequest, 
  CdrReconciliationDto, 
  FollowUpStatus 
} from '../../types';
import { 
  fetchCalls, 
  fetchUserExtensions,
  fetchEmployeePicker,
  fetchExtensionPicker,
  addCallNote, 
  classifyCall, 
  updateCallerAssociation,
  fetchPartyFinancialHistory,
  fetchUnknownCalls, 
  fetchFollowUps, 
  updateFollowUpStatus, 
  getCallRecordingUrl, 
  simulateCall, 
  reconcileCdrBatch, 
  fetchSuppliers,
  type EmployeePickerItem,
  type ExtensionPickerItem
} from '../../services/api';
import { JalaliDatePicker } from '../JalaliDatePicker';
import { formatJalali, formatJalaliDateTime, dateOnlyToIsoUtc } from '../../utils/jalali';
import { consumeDrillForView, drillBannerLabel } from '../../utils/drillNavigation';
import { getCallPerspective } from '../../utils/callPresentation';

interface CallsViewProps {
  devRole?: string;
  previewUserId?: string;
  viewerExtension?: string;
  onNavigateToSupplier?: (supplierId: string) => void;
}

export const CallsView: React.FC<CallsViewProps> = ({ 
  devRole = 'Procurement', 
  previewUserId = '',
  viewerExtension,
  onNavigateToSupplier 
}) => {
  const [activeTab, setActiveTab] = useState<'log' | 'unknown' | 'followups' | 'cdr' | 'simulator'>('log');
  const [drillNote, setDrillNote] = useState<string | null>(null);
  
  // Call Log state with 5 required classification filters
  const [calls, setCalls] = useState<CallRecordDto[]>([]);
  const [totalCallsCount, setTotalCallsCount] = useState<number>(0);
  const [isLoadingCalls, setIsLoadingCalls] = useState(false);
  const [callsError, setCallsError] = useState('');
  const callsRequestId = useRef(0);
  const [searchNumber, setSearchNumber] = useState('');
  const [filterClassification, setFilterClassification] = useState<string>('all');
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [filterExtension, setFilterExtension] = useState<string>('');
  const [filterEmployeeId, setFilterEmployeeId] = useState<string>('');
  const showExtensionColumn = (devRole === 'Admin' || devRole === 'Manager' || devRole === 'OrganizationManager') && !previewUserId;
  const showEmployeeFilter = (devRole === 'Manager' || devRole === 'OrganizationManager' || devRole === 'Admin') && !previewUserId;
  const showAdvancedTabs = devRole !== 'None' && !previewUserId;
  const [availableExtensions, setAvailableExtensions] = useState<UserExtensionDto[]>([]);
  const [extensionPicker, setExtensionPicker] = useState<ExtensionPickerItem[]>([]);
  const [employeePicker, setEmployeePicker] = useState<EmployeePickerItem[]>([]);
  const [filterStartDate, setFilterStartDate] = useState<string>('');
  const [filterEndDate, setFilterEndDate] = useState<string>('');
  
  // Selected Call for Modal / Detail
  const [selectedCall, setSelectedCall] = useState<CallRecordDto | null>(null);
  const [newNoteText, setNewNoteText] = useState('');
  const [newNoteOutcome, setNewNoteOutcome] = useState('PriceInquiry');
  const [newNoteFollowUp, setNewNoteFollowUp] = useState(false);
  const [newNoteDueDate, setNewNoteDueDate] = useState('');
  const [isSubmittingNote, setIsSubmittingNote] = useState(false);

  // Unknown Callers & Classification
  const [unknownCalls, setUnknownCalls] = useState<CallRecordDto[]>([]);
  const [classifyingCall, setClassifyingCall] = useState<CallRecordDto | null>(null);
  const [suppliersList, setSuppliersList] = useState<PartyDto[]>([]);
  const [selectedSupplierId, setSelectedSupplierId] = useState<string>('');
  const [isClassifying, setIsClassifying] = useState(false);

  // 4 Caller Identification Modes
  const [classifyMode, setClassifyMode] = useState<'link_existing' | 'standalone_contact' | 'representative' | 'new_party'>('link_existing');
  const [standaloneName, setStandaloneName] = useState('');
  const [standalonePosition, setStandalonePosition] = useState('');
  const [partyCategory, setPartyCategory] = useState<number>(4);
  const [newPartyName, setNewPartyName] = useState('');
  const [newPartyType, setNewPartyType] = useState<number>(2); // 2: LegalEntity, 1: Individual
  const [newContactName, setNewContactName] = useState('');
  const [newContactPosition, setNewContactPosition] = useState('');
  const [repName, setRepName] = useState('');
  const [repPosition, setRepPosition] = useState('');
  const [assignedDepartment, setAssignedDepartment] = useState('');

  // Caller Association Correction State
  const [editingAssociationCall, setEditingAssociationCall] = useState<CallRecordDto | null>(null);
  const [reassignPartyId, setReassignPartyId] = useState<string>('');
  const [reassignDept, setReassignDept] = useState<string>('');
  const [reassignReason, setReassignReason] = useState<string>('');
  const [isSavingAssociation, setIsSavingAssociation] = useState(false);

  // Sepidar Financial History State
  const [financialPartyId, setFinancialPartyId] = useState<string | null>(null);
  const [financialPartyName, setFinancialPartyName] = useState<string>('');
  const [financialHistory, setFinancialHistory] = useState<SepidarPartyFinancialHistoryDto | null>(null);
  const [loadingFinancial, setLoadingFinancial] = useState(false);
  const [financialError, setFinancialError] = useState<string | null>(null);

  // Follow-ups state
  const [followUps, setFollowUps] = useState<CallNoteDto[]>([]);
  const [_isLoadingFollowUps, setIsLoadingFollowUps] = useState(false);

  // Simulator state
  const [simCaller, setSimCaller] = useState('09121112233');
  const [simExtension, setSimExtension] = useState('101');
  const [simType, setSimType] = useState('incoming');
  const [simDuration, setSimDuration] = useState(30);
  const [simStatusMsg, setSimStatusMsg] = useState('');
  const [isSimulating, setIsSimulating] = useState(false);

  // CDR Batch State
  const [cdrResultMsg, setCdrResultMsg] = useState('');
  const [isReconcilingCdr, setIsReconcilingCdr] = useState(false);

  const loadCalls = async () => {
    const requestId = ++callsRequestId.current;
    setIsLoadingCalls(true);
    setCallsError('');
    try {
      const params: any = { take: 50 };
      if (previewUserId) params.previewUserId = previewUserId;
      if (filterExtension) params.extension = filterExtension;
      if (filterEmployeeId) params.employeeId = filterEmployeeId;
      if (filterClassification !== 'all') params.classification = filterClassification;
      if (filterStatus !== 'all') params.status = parseInt(filterStatus, 10);
      if (filterStartDate) params.fromDate = dateOnlyToIsoUtc(filterStartDate);
      if (filterEndDate) params.toDate = dateOnlyToIsoUtc(filterEndDate);

      const data = await fetchCalls(params, devRole);
      if (requestId !== callsRequestId.current) return;
      setCalls(data);
      if (typeof data.totalCount === 'number') {
        setTotalCallsCount(data.totalCount);
      } else {
        setTotalCallsCount(data.length);
      }
    } catch (err: any) {
      if (requestId !== callsRequestId.current) return;
      console.error(err);
      setCalls([]);
      setTotalCallsCount(0);
      setCallsError(err.message || 'دریافت تماس‌ها ناموفق بود.');
    } finally {
      if (requestId === callsRequestId.current) setIsLoadingCalls(false);
    }
  };

  const loadExtensions = async () => {
    try {
      const [exts, picker, employees] = await Promise.all([
        fetchUserExtensions(devRole).catch(() => [] as UserExtensionDto[]),
        fetchExtensionPicker({ includeInactive: true }, devRole).catch(() => [] as ExtensionPickerItem[]),
        showEmployeeFilter
          ? fetchEmployeePicker({ includeInactive: true }, devRole).catch(() => [] as EmployeePickerItem[])
          : Promise.resolve([] as EmployeePickerItem[])
      ]);
      setAvailableExtensions(exts);
      setExtensionPicker(picker);
      setEmployeePicker(employees);
    } catch (err) {
      console.error('Failed to load user extensions:', err);
    }
  };

  const loadUnknownCalls = async () => {
    try {
      const data = await fetchUnknownCalls(devRole);
      setUnknownCalls(data);
    } catch (err) {
      console.error(err);
    }
  };

  const loadFollowUps = async () => {
    setIsLoadingFollowUps(true);
    try {
      const data = await fetchFollowUps(devRole);
      setFollowUps(data);
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoadingFollowUps(false);
    }
  };

  const loadSuppliers = async () => {
    try {
      const res = await fetchSuppliers({ page: 1, pageSize: 100 }, devRole);
      setSuppliersList(res.items);
    } catch (err) {
      console.error(err);
    }
  };

  const drillAppliedRef = useRef(false);
  useEffect(() => {
    callsRequestId.current++;
    setCalls([]);
    setTotalCallsCount(0);
    setFilterExtension('');
    setFilterEmployeeId('');
    if (!drillAppliedRef.current) {
      drillAppliedRef.current = true;
      const drill = consumeDrillForView('calls');
      if (drill.filter === 'overdue-followups') {
        setActiveTab('followups');
        setDrillNote(drillBannerLabel(drill));
        return;
      }
    }
    setActiveTab('log');
  }, [previewUserId, devRole]);

  useEffect(() => {
    loadExtensions();
    loadFollowUps();
    loadSuppliers();
  }, [devRole]);

  useEffect(() => {
    loadCalls();
  }, [devRole, previewUserId, filterClassification, filterStatus, filterExtension, filterEmployeeId, filterStartDate, filterEndDate]);

  useEffect(() => {
    if (activeTab === 'unknown') {
      loadUnknownCalls();
    } else if (activeTab === 'followups') {
      loadFollowUps();
    }
  }, [activeTab]);

  const handleAddNote = async () => {
    if (!selectedCall || !newNoteText.trim()) return;
    setIsSubmittingNote(true);
    try {
      const req: CreateCallNoteRequest = {
        noteText: newNoteText.trim(),
        outcome: newNoteOutcome,
        followUpRequired: newNoteFollowUp,
        followUpDueDateUtc: newNoteFollowUp && newNoteDueDate ? dateOnlyToIsoUtc(newNoteDueDate) : undefined,
        followUpAssignedTo: devRole.toLowerCase()
      };
      const note = await addCallNote(selectedCall.id, req, devRole);
      setSelectedCall({
        ...selectedCall,
        notes: [note, ...selectedCall.notes],
        notesCount: selectedCall.notesCount + 1
      });
      setNewNoteText('');
      setNewNoteFollowUp(false);
      loadCalls();
      loadFollowUps();
    } catch (err: any) {
      alert(err.message || 'خطا در افزودن یادداشت');
    } finally {
      setIsSubmittingNote(false);
    }
  };

  const handleClassify = async () => {
    if (!classifyingCall) return;
    setIsClassifying(true);
    try {
      const req: ClassifyCallRequest = {
        assignedDepartment: assignedDepartment.trim() || undefined
      };

      if (classifyMode === 'link_existing') {
        if (!selectedSupplierId) {
          alert('لطفاً یک طرف حساب را انتخاب کنید.');
          setIsClassifying(false);
          return;
        }
        req.partyId = selectedSupplierId;
      } else if (classifyMode === 'standalone_contact') {
        if (!standaloneName.trim()) {
          alert('لطفاً نام مخاطب حقیقی را وارد کنید.');
          setIsClassifying(false);
          return;
        }
        req.createStandaloneContact = true;
        req.standaloneName = standaloneName.trim();
        req.standalonePosition = standalonePosition.trim() || undefined;
      } else if (classifyMode === 'representative') {
        if (!selectedSupplierId || !repName.trim()) {
          alert('لطفاً طرف حساب و نام نماینده را وارد کنید.');
          setIsClassifying(false);
          return;
        }
        req.partyId = selectedSupplierId;
        req.newContactName = repName.trim();
        req.newContactPosition = repPosition.trim() || undefined;
      } else if (classifyMode === 'new_party') {
        if (!newPartyName.trim()) {
          alert('لطفاً نام طرف حساب جدید را وارد کنید.');
          setIsClassifying(false);
          return;
        }
        req.createNewParty = true;
        req.newPartyName = newPartyName.trim();
        req.newPartyType = newPartyType;
        req.partyCategory = partyCategory as any;
        req.newContactName = newContactName.trim() || undefined;
        req.newContactPosition = newContactPosition.trim() || undefined;
      }

      await classifyCall(classifyingCall.id, req, devRole);
      setClassifyingCall(null);
      setSelectedSupplierId('');
      setStandaloneName('');
      setNewPartyName('');
      setRepName('');
      setAssignedDepartment('');
      loadUnknownCalls();
      loadCalls();
    } catch (err: any) {
      alert(err.message || 'خطا در انتساب و شناسایی تماس‌گیرنده');
    } finally {
      setIsClassifying(false);
    }
  };

  const handleSaveCallerAssociation = async () => {
    if (!editingAssociationCall) return;
    setIsSavingAssociation(true);
    try {
      const req: UpdateCallerAssociationRequest = {
        partyId: reassignPartyId || undefined,
        assignedDepartment: reassignDept.trim() || undefined,
        reason: reassignReason.trim() || 'اصلاح انتساب توسط کاربر'
      };
      const updated = await updateCallerAssociation(editingAssociationCall.id, req, devRole);
      setEditingAssociationCall(null);
      setReassignReason('');
      if (selectedCall?.id === updated.id) {
        setSelectedCall(updated);
      }
      loadCalls();
      loadUnknownCalls();
    } catch (err: any) {
      alert(err.message || 'خطا در اصلاح انتساب تماس');
    } finally {
      setIsSavingAssociation(false);
    }
  };

  const handleOpenFinancialHistory = async (partyId: string, partyName: string) => {
    setFinancialPartyId(partyId);
    setFinancialPartyName(partyName);
    setLoadingFinancial(true);
    setFinancialError(null);
    try {
      const data = await fetchPartyFinancialHistory(partyId, undefined, devRole);
      setFinancialHistory(data);
    } catch (err: any) {
      setFinancialError(err.message || 'خطا در دریافت اطلاعات مالی از سپیدار');
    } finally {
      setLoadingFinancial(false);
    }
  };

  const handleUpdateFollowUp = async (noteId: string, status: FollowUpStatus) => {
    try {
      await updateFollowUpStatus(noteId, status, devRole);
      loadFollowUps();
    } catch (err: any) {
      alert(err.message || 'خطا در به‌روزرسانی پیگیری');
    }
  };

  const handleSimulate = async () => {
    setIsSimulating(true);
    setSimStatusMsg('');
    try {
      const req: SimulateCallRequest = {
        callerNumber: simCaller,
        destinationNumber: simExtension,
        extension: simExtension,
        callType: simType,
        durationSeconds: simDuration
      };
      await simulateCall(req, devRole);
      setSimStatusMsg(`تماس شبیه‌سازی شده (${simType}) با موفقیت به AMI ارسال شد.`);
      setTimeout(() => {
        loadCalls();
      }, 1000);
    } catch (err: any) {
      setSimStatusMsg(`خطا: ${err.message}`);
    } finally {
      setIsSimulating(false);
    }
  };

  const handleRunCdrReconcile = async () => {
    setIsReconcilingCdr(true);
    setCdrResultMsg('');
    try {
      // Simulate verified Asterisk CDR records batch
      const now = new Date();
      const batch: CdrReconciliationDto[] = [
        {
          uniqueId: `${Math.floor(now.getTime() / 1000)}.701`,
          callerNumber: '09121112233',
          destinationNumber: '101',
          startTimeUtc: new Date(now.getTime() - 120000).toISOString(),
          answerTimeUtc: new Date(now.getTime() - 110000).toISOString(),
          endTimeUtc: new Date(now.getTime() - 40000).toISOString(),
          durationSeconds: 80,
          billableSeconds: 70,
          disposition: 'ANSWERED',
          recordingFile: `q-${Math.floor(now.getTime() / 1000)}.701.wav`
        },
        {
          uniqueId: `${Math.floor(now.getTime() / 1000)}.702`,
          callerNumber: '09359998877',
          destinationNumber: '102',
          startTimeUtc: new Date(now.getTime() - 300000).toISOString(),
          endTimeUtc: new Date(now.getTime() - 280000).toISOString(),
          durationSeconds: 20,
          billableSeconds: 0,
          disposition: 'NO ANSWER'
        }
      ];
      const res = await reconcileCdrBatch(batch, devRole);
      setCdrResultMsg(`تطبیق با موفقیت انجام شد: ${res.reconciledCount} رکورد CDR در پایگاه داده CRM ثبت و تصدیق گردید.`);
      loadCalls();
    } catch (err: any) {
      setCdrResultMsg(`خطا در تطبیق CDR: ${err.message}`);
    } finally {
      setIsReconcilingCdr(false);
    }
  };

  const filteredCalls = calls.filter(c => {
    if (!searchNumber) return true;
    const term = searchNumber.trim().toLowerCase();
    return (
      (c.callerNumber || '').toLowerCase().includes(term) ||
      (c.destinationNumber || '').toLowerCase().includes(term) ||
      getCallPerspective(c, viewerExtension).number.toLowerCase().includes(term) ||
      (c.callerName && c.callerName.toLowerCase().includes(term)) ||
      (c.internalCallerName && c.internalCallerName.toLowerCase().includes(term)) ||
      (c.partyName && c.partyName.toLowerCase().includes(term))
    );
  });

  const getStatusBadge = (status: number) => {
    switch (status) {
      case 0:
        return <span className="badge badge-warning">در حال تماس</span>;
      case 1:
      case 5:
      case 6:
        return <span className="badge badge-success">پاسخ داده‌شده</span>;
      case 2:
      case 3:
      case 4:
        return <span className="badge badge-danger">بی‌پاسخ</span>;
      default:
        return <span className="badge badge-secondary">نامشخص</span>;
    }
  };

  return (
    <div className="view-container">
      {/* View Header */}
      <div className="view-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2 style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Headphones size={26} color="#38bdf8" />
            <span>مدیریت مرکز تماس ایزابل و پرونده تأمین‌کنندگان</span>
          </h2>
          <p style={{ color: '#94a3b8', marginTop: '4px' }}>
            دریافت رویدادهای زنده Asterisk AMI، پاپ‌آپ آنی تماس، شناسایی تأمین‌کننده، ثبت یادداشت، پیگیری و شنود امن مکالمات
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span style={{ 
            display: 'inline-flex', 
            alignItems: 'center', 
            gap: '6px', 
            padding: '4px 10px', 
            borderRadius: '20px', 
            fontSize: '0.75rem', 
            background: 'rgba(34, 197, 94, 0.1)', 
            color: '#4ade80', 
            border: '1px solid rgba(34, 197, 94, 0.3)' 
          }}>
            <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#4ade80', display: 'inline-block' }} />
            <span>آداپتور آزمایشی AMI متصل است</span>
          </span>
        </div>
      </div>

      {drillNote && (
        <div role="status" style={{
          marginBottom: 14,
          padding: '10px 12px',
          borderRadius: 10,
          border: '1px solid rgba(56, 189, 248, 0.35)',
          background: 'rgba(14, 165, 233, 0.08)',
          color: '#7dd3fc',
          fontSize: '0.85rem'
        }}>
          {drillNote}
        </div>
      )}

      {/* Tabs Bar */}
      <div className="tabs-header" style={{ display: 'flex', gap: '8px', borderBottom: '1px solid rgba(255, 255, 255, 0.08)', marginBottom: '20px' }}>
        <button
          className={`tab-btn ${activeTab === 'log' ? 'active' : ''}`}
          onClick={() => setActiveTab('log')}
          style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
        >
          <Clock size={16} />
          <span>دفتر تماس‌ها (Call Log)</span>
        </button>

        <button
          className={`tab-btn ${activeTab === 'unknown' ? 'active' : ''}`}
          onClick={() => setActiveTab('unknown')}
          style={{ display: showAdvancedTabs ? 'flex' : 'none', alignItems: 'center', gap: '6px' }}
        >
          <AlertCircle size={16} />
          <span>تماس‌های ناشناس ({unknownCalls.length})</span>
        </button>

        <button
          className={`tab-btn ${activeTab === 'followups' ? 'active' : ''}`}
          onClick={() => setActiveTab('followups')}
          style={{ display: showAdvancedTabs ? 'flex' : 'none', alignItems: 'center', gap: '6px' }}
        >
          <CheckCircle size={16} />
          <span>وظایف پیگیری ({followUps.length})</span>
        </button>

        <button
          className={`tab-btn ${activeTab === 'cdr' ? 'active' : ''}`}
          onClick={() => setActiveTab('cdr')}
          style={{ display: showAdvancedTabs ? 'flex' : 'none', alignItems: 'center', gap: '6px' }}
        >
          <ShieldCheck size={16} />
          <span>تطبیق اسناد CDR/CEL</span>
        </button>

        <button
          className={`tab-btn ${activeTab === 'simulator' ? 'active' : ''}`}
          onClick={() => setActiveTab('simulator')}
          style={{ display: showAdvancedTabs ? 'flex' : 'none', alignItems: 'center', gap: '6px' }}
        >
          <Radio size={16} />
          <span>شبیه‌ساز و تست تماس (PBX Simulator)</span>
        </button>
      </div>

      {/* Tab 1: Call Log */}
      {activeTab === 'log' && (
        <div className="card">
          {/* Filter Bar */}
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '12px', alignItems: 'center', marginBottom: '16px' }}>
            <div style={{ position: 'relative', flex: 1, minWidth: '220px' }}>
              <Search size={16} style={{ position: 'absolute', right: '12px', top: '10px', color: '#94a3b8' }} />
              <input
                type="text"
                placeholder="جستجو بر اساس شماره، نام یا شرکت تأمین‌کننده..."
                value={searchNumber}
                onChange={e => setSearchNumber(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 36px 8px 12px',
                  borderRadius: '8px',
                  background: '#0f172a',
                  border: '1px solid rgba(255, 255, 255, 0.1)',
                  color: '#f8fafc',
                  fontSize: '0.85rem'
                }}
              />
            </div>

            <select
              value={filterClassification}
              onChange={e => setFilterClassification(e.target.value)}
              style={{
                background: '#0f172a',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                padding: '8px 12px',
                borderRadius: '8px',
                fontSize: '0.85rem'
              }}
            >
              <option value="all">۱. همه تماس‌های مجاز</option>
              <option value="inbound_trunk">۲. تماس‌های ورودی ترانک (SIP/91002628)</option>
              <option value="outbound_trunk">۳. تماس‌های خروجی ترانک (SIP/91002628)</option>
              <option value="internal_inbound">۴. تماس‌های ورودی داخلی</option>
              <option value="internal_outbound">۵. تماس‌های خروجی داخلی</option>
            </select>

            <select
              value={filterStatus}
              onChange={e => setFilterStatus(e.target.value)}
              style={{
                background: '#0f172a',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                padding: '8px 12px',
                borderRadius: '8px',
                fontSize: '0.85rem'
              }}
            >
              <option value="all">همه وضعیت‌ها</option>
              <option value="1">پاسخ داده‌شده</option>
              <option value="2">بی‌پاسخ</option>
            </select>

            {/* Extension selector: number + CRM display name */}
            {showExtensionColumn && <select
              value={filterExtension}
              onChange={e => setFilterExtension(e.target.value)}
              style={{
                minWidth: '180px',
                background: '#0f172a',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                padding: '8px 12px',
                borderRadius: '8px',
                fontSize: '0.85rem'
              }}
            >
              <option value="">همه داخلی‌های مجاز</option>
              {(extensionPicker.length > 0 ? extensionPicker : availableExtensions.map(e => ({
                extension: e.extension,
                displayName: e.username || e.extension,
                isActive: e.isActive
              }))).map(e => (
                <option key={e.extension} value={e.extension}>
                  {e.extension} — {e.displayName}{('isActive' in e && e.isActive === false) ? ' (غیرفعال)' : ''}
                </option>
              ))}
            </select>}

            {showEmployeeFilter && <select
              value={filterEmployeeId}
              onChange={e => setFilterEmployeeId(e.target.value)}
              style={{
                minWidth: '200px',
                background: '#0f172a',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                padding: '8px 12px',
                borderRadius: '8px',
                fontSize: '0.85rem'
              }}
            >
              <option value="">همه کارمندان مجاز</option>
              {employeePicker.map(e => (
                <option key={e.employeeId} value={e.employeeId}>
                  {e.displayName} — {e.statusLabel}
                </option>
              ))}
            </select>}

            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <JalaliDatePicker
                value={filterStartDate}
                onChange={setFilterStartDate}
                placeholder="از تاریخ"
              />
              <JalaliDatePicker
                value={filterEndDate}
                onChange={setFilterEndDate}
                placeholder="تا تاریخ"
              />
            </div>

            <button
              onClick={loadCalls}
              className="btn btn-secondary"
              style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
              title="بارگذاری مجدد"
            >
              <RefreshCw size={14} />
              <span>تازه‌سازی</span>
            </button>

            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.85rem', color: '#94a3b8', marginRight: 'auto' }}>
              <span>تعداد کل نتایج:</span>
              <span style={{ fontWeight: 700, color: '#38bdf8', background: 'rgba(56, 189, 248, 0.1)', padding: '2px 8px', borderRadius: '12px' }}>
                {totalCallsCount}
              </span>
            </div>
          </div>

          {/* Table */}
          {callsError && <div role="alert" style={{ color: '#f87171', marginBottom: '8px' }}>{callsError}</div>}
          <div className="table-responsive">
            <table className="data-table">
              <thead>
                <tr>
                  <th style={{ width: '40px' }}>جهت</th>
                  <th>شماره / مخاطب</th>
                  <th>تأمین‌کننده / مخاطب</th>
                  {showExtensionColumn && <th>داخلی</th>}
                  <th>زمان تماس</th>
                  <th>مدت</th>
                  <th>وضعیت</th>
                  <th>فایل صوتی</th>
                  <th>یادداشت‌ها</th>
                  <th style={{ width: '90px' }}>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {isLoadingCalls ? (
                  <tr>
                    <td colSpan={showExtensionColumn ? 10 : 9} style={{ textAlign: 'center', padding: '24px', color: '#94a3b8' }}>
                      در حال بارگذاری لاگ تماس‌ها...
                    </td>
                  </tr>
                ) : filteredCalls.length === 0 ? (
                  <tr>
                    <td colSpan={showExtensionColumn ? 10 : 9} style={{ textAlign: 'center', padding: '24px', color: '#94a3b8' }}>
                      هیچ رکوردی برای نمایش یافت نشد.
                    </td>
                  </tr>
                ) : (
                  filteredCalls.map(c => {
                    const isInternal = c.scope === 2 || c.direction === 2;
                    const isTrunk = c.scope === 1;
                    const isUnknownScope = !isInternal && !isTrunk;
                    const perspective = getCallPerspective(c, viewerExtension);
                    return (
                      <tr key={c.id}>
                        <td style={{ textAlign: 'center' }}>
                          {isInternal ? (
                            <span title={perspective.direction}>
                              {perspective.outgoing
                                ? <PhoneOutgoing size={16} color="#c084fc" />
                                : <PhoneIncoming size={16} color="#a855f7" />}
                            </span>
                          ) : isTrunk ? (
                            c.direction === 0 ? (
                              <span title="تماس ورودی ترانک SIP/91002628">
                                <PhoneIncoming size={16} color="#4ade80" />
                              </span>
                            ) : (
                              <span title="تماس خروجی ترانک SIP/91002628">
                                <PhoneOutgoing size={16} color="#38bdf8" />
                              </span>
                            )
                          ) : (
                            <span title={`حوزه نامشخص / ترانک متفرقه (${c.direction === 0 ? 'ورودی' : 'خروجی'})`}>
                              <AlertCircle size={16} color="#f59e0b" />
                            </span>
                          )}
                        </td>
                        <td style={{ fontFamily: 'monospace', fontWeight: 600 }}>
                          <span dir="ltr" title={perspective.direction}>{perspective.number || 'نامشخص'}</span>
                        </td>
                        <td>
                          {isInternal ? (
                            <span style={{
                              fontSize: '0.75rem', 
                              color: '#c084fc', 
                              background: 'rgba(168, 85, 247, 0.12)', 
                              padding: '2px 8px', 
                              borderRadius: '4px',
                              border: '1px solid rgba(168, 85, 247, 0.25)' 
                            }}>
                              تماس داخلی (<strong style={{ color: '#fbbf24', fontWeight: 800 }}>{perspective.name?.trim() || 'بدون پرونده تأمین‌کننده'}</strong>)
                            </span>
                          ) : isUnknownScope ? (
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '3px' }}>
                              <span style={{ 
                                fontSize: '0.72rem', 
                                color: '#f59e0b', 
                                background: 'rgba(245, 158, 11, 0.12)', 
                                padding: '2px 6px', 
                                borderRadius: '4px',
                                border: '1px solid rgba(245, 158, 11, 0.25)',
                                width: 'fit-content'
                              }}>
                                حوزه نامشخص / سایر ترانک‌ها
                              </span>
                              {c.partyName ? (
                                <div style={{ fontSize: '0.8rem', color: '#cbd5e1' }}>{c.partyName}</div>
                              ) : (
                                <span style={{ color: '#94a3b8', fontSize: '0.75rem' }}>طرف تماس نامشخص</span>
                              )}
                            </div>
                          ) : c.partyName ? (
                            <div>
                              <div 
                                onClick={() => c.partyId && onNavigateToSupplier && onNavigateToSupplier(c.partyId)}
                                style={{ 
                                  fontWeight: 600, 
                                  color: '#f8fafc', 
                                  display: 'flex', 
                                  alignItems: 'center', 
                                  gap: '4px',
                                  cursor: onNavigateToSupplier ? 'pointer' : 'default'
                                }}
                              >
                                <Building2 size={13} color="#38bdf8" />
                                <span>{c.partyName}</span>
                                {onNavigateToSupplier && <ExternalLink size={11} color="#38bdf8" />}
                              </div>
                              {c.contactName && (
                                <div style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
                                  {c.contactName} {c.contactPosition ? `(${c.contactPosition})` : ''}
                                </div>
                              )}
                            </div>
                          ) : (
                            <span style={{ color: '#94a3b8', fontSize: '0.8rem' }}>ناشناس</span>
                          )}
                        </td>
                        {showExtensionColumn && <td>
                          {isInternal ? (
                            <span dir="ltr" style={{ fontFamily: 'monospace' }}>{c.callerNumber} → {c.destinationNumber}</span>
                          ) : (c.extension || '-')}
                        </td>}
                      <td style={{ fontSize: '0.8rem' }}>
                        {formatJalaliDateTime(c.startTimeUtc)}
                      </td>
                      <td style={{ fontSize: '0.8rem' }}>
                        {c.durationSeconds ? `${c.durationSeconds} ثانیه` : '-'}
                      </td>
                      <td>{getStatusBadge(c.status)}</td>
                      <td>
                        {c.hasRecording ? (
                          <audio 
                            controls 
                            src={getCallRecordingUrl(c.id)}
                            style={{ height: '30px', maxWidth: '170px' }}
                            preload="none"
                          />
                        ) : (c.recordingFileName && c.recordingFileName.trim() !== '') ? (
                          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                            <audio 
                              controls 
                              src={getCallRecordingUrl(c.id)}
                              style={{ height: '30px', maxWidth: '150px' }}
                              preload="none"
                            />
                            <span 
                              title={`نام فایل در CDR ثبت شده است (${c.recordingFileName})؛ دریافت از سرور هنگام پخش آزمایش می‌شود.`}
                              style={{ 
                                fontSize: '0.7rem', 
                                padding: '2px 6px', 
                                borderRadius: '4px', 
                                background: 'rgba(56, 189, 248, 0.15)', 
                                color: '#38bdf8',
                                whiteSpace: 'nowrap'
                              }}
                            >
                              در انتظار دریافت
                            </span>
                          </div>
                        ) : (
                          <span title="نام فایل صوتی هنوز از CDR ایزابل به CRM نرسیده است" style={{ fontSize: '0.75rem', color: '#94a3b8' }}>ضبط تأیید نشده</span>
                        )}
                      </td>
                      <td>
                        <span style={{ 
                          fontSize: '0.75rem', 
                          padding: '2px 8px', 
                          borderRadius: '12px', 
                          background: c.notesCount > 0 ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255, 255, 255, 0.05)',
                          color: c.notesCount > 0 ? '#38bdf8' : '#94a3b8'
                        }}>
                          {c.notesCount} یادداشت
                        </span>
                      </td>
                      <td>
                        <button
                          onClick={() => setSelectedCall(c)}
                          className="btn btn-sm btn-secondary"
                          style={{ fontSize: '0.75rem' }}
                        >
                          جزئیات
                        </button>
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

      {/* Tab 2: Unknown Callers */}
      {activeTab === 'unknown' && (
        <div className="card">
          <div style={{ marginBottom: '16px' }}>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>صف تماس‌های ناشناس نیازمند شناسایی</h3>
            <p style={{ color: '#94a3b8', fontSize: '0.85rem' }}>
              شماره‌ای که هنوز به مخاطب یا پرونده‌ای وصل نشده است. شناسایی، مخاطب می‌سازد یا به شرکت موجود وصل می‌کند و تأمین‌کننده نمی‌سازد مگر نوع پرونده را خودتان تأمین‌کننده انتخاب کنید.
            </p>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>شماره تلفن</th>
                <th>شماره نرمال‌شده</th>
                <th>داخلی مخاطب</th>
                <th>تاریخ و ساعت تماس</th>
                <th>مدت مکالمه</th>
                <th>عملیات انتساب</th>
              </tr>
            </thead>
            <tbody>
              {unknownCalls.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '24px', color: '#94a3b8' }}>
                    تماس ناشناسی در صف وجود ندارد.
                  </td>
                </tr>
              ) : (
                unknownCalls.map(c => (
                  <tr key={c.id}>
                    <td style={{ fontFamily: 'monospace', fontWeight: 700 }}>{c.callerNumber}</td>
                    <td style={{ fontFamily: 'monospace', color: '#94a3b8' }}>{c.normalizedCallerNumber}</td>
                    <td>{c.extension || '-'}</td>
                    <td>{formatJalaliDateTime(c.startTimeUtc)}</td>
                    <td>{c.durationSeconds ? `${c.durationSeconds} ثانیه` : '-'}</td>
                    <td>
                      <button
                        onClick={() => {
                          setClassifyingCall(c);
                          setSelectedSupplierId('');
                        }}
                        className="btn btn-sm btn-primary"
                        style={{ fontSize: '0.8rem' }}
                      >
                        شناسایی مخاطب
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 3: Follow-up Tasks */}
      {activeTab === 'followups' && (
        <div className="card">
          <div style={{ marginBottom: '16px' }}>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>وظایف پیگیری و اقدامات بعدی تماس‌ها</h3>
            <p style={{ color: '#94a3b8', fontSize: '0.85rem' }}>
              لیست تماس‌هایی که کارشناس تدارکات یا ناظر، اقدام بعدی یا استعلام قیمت و تحویل بار را برای آن‌ها زمان‌بندی کرده است.
            </p>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>موضوع / نتیجه</th>
                <th>متن یادداشت و توافقات</th>
                <th>مسئول پیگیری</th>
                <th>مهلت اقدام (Due Date)</th>
                <th>وضعیت</th>
                <th style={{ width: '130px' }}>اقدام</th>
              </tr>
            </thead>
            <tbody>
              {followUps.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '24px', color: '#94a3b8' }}>
                    هیچ وظیفه پیگیری در انتظار برای اقدام ثبت نشده است.
                  </td>
                </tr>
              ) : (
                followUps.map(fu => (
                  <tr key={fu.id}>
                    <td>
                      <span className="badge badge-info">{fu.outcome}</span>
                    </td>
                    <td>{fu.noteText}</td>
                    <td>{fu.followUpAssignedTo || fu.username}</td>
                    <td style={{ fontSize: '0.85rem', color: '#facc15' }}>
                      {fu.followUpDueDateUtc ? formatJalali(fu.followUpDueDateUtc) : 'نامشخص'}
                    </td>
                    <td>
                      <span className="badge badge-warning">در انتظار انجام</span>
                    </td>
                    <td>
                      <div style={{ display: 'flex', gap: '4px' }}>
                        <button
                          onClick={() => handleUpdateFollowUp(fu.id, 1)}
                          className="btn btn-sm btn-success"
                          title="تکمیل شد"
                          style={{ padding: '3px 8px', fontSize: '0.75rem' }}
                        >
                          <Check size={13} />
                        </button>
                        <button
                          onClick={() => handleUpdateFollowUp(fu.id, 2)}
                          className="btn btn-sm btn-danger"
                          title="لغو پیگیری"
                          style={{ padding: '3px 8px', fontSize: '0.75rem' }}
                        >
                          <X size={13} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 4: CDR/CEL Reconciliation */}
      {activeTab === 'cdr' && (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '16px' }}>
            <div>
              <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>تطبیق اسناد قطعی CDR و CEL ایزابل</h3>
              <p style={{ color: '#94a3b8', fontSize: '0.85rem' }}>
                مکانیزم تطبیق دوگانه (Live AMI + Finalized CDR) جهت همگام‌سازی زمان دقیق مکالمه، وضعیت نهایی قطع تماس و انتساب قطعی فایل ضبط.
              </p>
            </div>
            <button
              onClick={handleRunCdrReconcile}
              disabled={isReconcilingCdr}
              className="btn btn-primary"
              style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
            >
              <RefreshCw size={15} />
              <span>{isReconcilingCdr ? 'در حال اجرای تطبیق...' : 'اجرای بچ تطبیق CDR'}</span>
            </button>
          </div>

          {cdrResultMsg && (
            <div style={{ 
              padding: '12px', 
              borderRadius: '8px', 
              marginBottom: '16px',
              background: cdrResultMsg.startsWith('خطا') ? 'rgba(239, 68, 68, 0.15)' : 'rgba(34, 197, 94, 0.15)',
              border: `1px solid ${cdrResultMsg.startsWith('خطا') ? 'rgba(239, 68, 68, 0.3)' : 'rgba(34, 197, 94, 0.3)'}`,
              color: cdrResultMsg.startsWith('خطا') ? '#fca5a5' : '#86efac',
              fontSize: '0.85rem'
            }}>
              {cdrResultMsg}
            </div>
          )}

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '16px', marginTop: '16px' }}>
            <div style={{ background: '#0f172a', padding: '16px', borderRadius: '10px', border: '1px solid rgba(255,255,255,0.08)' }}>
              <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>رویکرد تطبیق تماس‌ها</div>
              <div style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc', marginTop: '4px' }}>شناسه یکتا (Uniqueid / Linkedid)</div>
              <p style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '8px' }}>
                انطباق بر اساس کلیدهای استاندارد استریسک بدون تکیه به زمان تقریبی سیستم
              </p>
            </div>

            <div style={{ background: '#0f172a', padding: '16px', borderRadius: '10px', border: '1px solid rgba(255,255,255,0.08)' }}>
              <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>انتقال و سناریوهای چندشاخه (Transfers)</div>
              <div style={{ fontSize: '1rem', fontWeight: 600, color: '#4ade80', marginTop: '4px' }}>پشتیبانی کامل از Linkedid</div>
              <p style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '8px' }}>
                حفظ شاخه‌های تماس منتقل‌شده از داخلی ۱۰۱ به ۱۰۲ با ثبت مستقل و پیوند مشترک
              </p>
            </div>

            <div style={{ background: '#0f172a', padding: '16px', borderRadius: '10px', border: '1px solid rgba(255,255,255,0.08)' }}>
              <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>امنیت و فایل‌های ضبط</div>
              <div style={{ fontSize: '1rem', fontWeight: 600, color: '#38bdf8', marginTop: '4px' }}>Path Traversal Defense</div>
              <p style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '8px' }}>
                استریم محافظت‌شده از طریق API کنترل دسترسی بدون افشای مسیر خام سرور
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Tab 5: Simulator */}
      {activeTab === 'simulator' && (
        <div className="card">
          <div style={{ marginBottom: '16px' }}>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 600 }}>شبیه‌ساز ترافیک تلفنی ایزابل (Deterministic Asterisk AMI Mock)</h3>
            <p style={{ color: '#94a3b8', fontSize: '0.85rem' }}>
              تست سناریوهای مختلف ورود تماس، ظاهر شدن پاپ‌آپ لحظه‌ای SignalR، تماس‌های بی‌پاسخ، اشغال و انتقال مکالمه بدون نیاز به دسترسی فیزیکی به سرور PBX.
            </p>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '16px', maxWidth: '800px' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.8rem', color: '#94a3b8', marginBottom: '6px' }}>شماره تماس گیرنده (Caller):</label>
              <input
                type="text"
                value={simCaller}
                onChange={e => setSimCaller(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '8px',
                  background: '#0f172a',
                  border: '1px solid rgba(255,255,255,0.1)',
                  color: '#f8fafc',
                  fontFamily: 'monospace'
                }}
              />
              <span style={{ fontSize: '0.75rem', color: '#64748b' }}>برای تست تأمین‌کننده منطبق از شماره 09121112233 یا شماره‌های موجود استفاده کنید.</span>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8rem', color: '#94a3b8', marginBottom: '6px' }}>داخلی مقصد (Extension):</label>
              <input
                type="text"
                value={simExtension}
                onChange={e => setSimExtension(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '8px',
                  background: '#0f172a',
                  border: '1px solid rgba(255,255,255,0.1)',
                  color: '#f8fafc',
                  fontFamily: 'monospace'
                }}
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8rem', color: '#94a3b8', marginBottom: '6px' }}>نوع رویداد تلفنی (Scenario):</label>
              <select
                value={simType}
                onChange={e => setSimType(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '8px',
                  background: '#0f172a',
                  border: '1px solid rgba(255,255,255,0.1)',
                  color: '#f8fafc'
                }}
              >
                <option value="incoming">تماس ورودی موفق (Answered & Completed)</option>
                <option value="missed">تماس ورودی بی‌پاسخ (Missed Call)</option>
                <option value="busy">خط اشغال (Busy Signal)</option>
                <option value="outgoing">تماس خروجی از داخلی به تأمین‌کننده</option>
                <option value="transfer">انتقال تماس بین دو داخلی (Call Transfer)</option>
              </select>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8rem', color: '#94a3b8', marginBottom: '6px' }}>مدت مکالمه (ثانیه):</label>
              <input
                type="number"
                value={simDuration}
                onChange={e => setSimDuration(parseInt(e.target.value, 10))}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '8px',
                  background: '#0f172a',
                  border: '1px solid rgba(255,255,255,0.1)',
                  color: '#f8fafc'
                }}
              />
            </div>
          </div>

          <div style={{ marginTop: '20px' }}>
            <button
              onClick={handleSimulate}
              disabled={isSimulating}
              className="btn btn-primary"
              style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '10px 20px' }}
            >
              <Play size={16} />
              <span>{isSimulating ? 'در حال ارسال رویداد AMI...' : 'شروع شبیه‌سازی تماس'}</span>
            </button>
          </div>

          {simStatusMsg && (
            <div style={{ 
              marginTop: '16px', 
              padding: '12px', 
              borderRadius: '8px',
              background: simStatusMsg.startsWith('خطا') ? 'rgba(239, 68, 68, 0.15)' : 'rgba(56, 189, 248, 0.15)',
              border: `1px solid ${simStatusMsg.startsWith('خطا') ? 'rgba(239, 68, 68, 0.3)' : 'rgba(56, 189, 248, 0.3)'}`,
              color: simStatusMsg.startsWith('خطا') ? '#fca5a5' : '#7dd3fc',
              fontSize: '0.85rem'
            }}>
              {simStatusMsg}
            </div>
          )}
        </div>
      )}

      {/* Call Detail & Notes Modal */}
      {selectedCall && (
        <div className="modal-backdrop" onClick={() => setSelectedCall(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ maxWidth: '650px', maxHeight: '90vh', overflowY: 'auto' }}>
            <div className="modal-header">
              <h3 style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Headphones size={20} color="#38bdf8" />
                <span>جزئیات تماس و پرونده مکالمه</span>
              </h3>
              <button onClick={() => setSelectedCall(null)} className="close-btn"><X size={18} /></button>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px', background: '#0f172a', padding: '12px', borderRadius: '8px' }}>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>شماره تماس:</span>
                <div style={{ fontWeight: 700, fontFamily: 'monospace' }} dir="ltr">
                  {getCallPerspective(selectedCall, viewerExtension).number || 'نامشخص'}
                </div>
              </div>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>{selectedCall.scope === 2 || selectedCall.direction === 2 ? 'کارمند طرف تماس:' : 'تأمین‌کننده:'}</span>
                <div style={{ fontWeight: 600 }}>{selectedCall.scope === 2 || selectedCall.direction === 2 ? <>تماس داخلی (<strong style={{ color: '#fbbf24', fontWeight: 800 }}>{getCallPerspective(selectedCall, viewerExtension).name?.trim() || 'بدون پرونده تأمین‌کننده'}</strong>)</> : (selectedCall.partyName || 'ناشناس')}</div>
              </div>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>جهت تماس:</span>
                <div>{getCallPerspective(selectedCall, viewerExtension).direction}</div>
              </div>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>وضعیت تماس:</span>
                <div>{getStatusBadge(selectedCall.status)}</div>
              </div>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>زمان آغاز:</span>
                <div style={{ fontSize: '0.85rem' }}>{formatJalaliDateTime(selectedCall.startTimeUtc)}</div>
              </div>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>مدت مکالمه:</span>
                <div>{selectedCall.durationSeconds ? `${selectedCall.durationSeconds} ثانیه` : '-'}</div>
              </div>
              <div style={{ gridColumn: 'span 2' }}>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>شناسه سیستمی ایزابل (Uniqueid / Linkedid):</span>
                <div style={{ fontFamily: 'monospace', fontSize: '0.75rem', color: '#64748b' }}>
                  {selectedCall.uniqueId} {selectedCall.linkedId ? `| پیوند: ${selectedCall.linkedId}` : ''}
                </div>
              </div>

              <div style={{ display: 'flex', gap: '8px', gridColumn: 'span 2', marginTop: '6px' }}>
                <button
                  className="btn btn-sm btn-secondary"
                  onClick={() => {
                    setEditingAssociationCall(selectedCall);
                    setReassignPartyId(selectedCall.partyId || '');
                    setReassignDept(selectedCall.assignedDepartment || '');
                    setReassignReason('');
                  }}
                  style={{ display: 'inline-flex', alignItems: 'center', gap: '5px', fontSize: '0.78rem' }}
                >
                  <Edit2 size={13} />
                  <span>اصلاح انتساب طرف‌حساب</span>
                </button>

                {(devRole === 'Finance' || devRole === 'Admin') && selectedCall.partyId && (
                  <button
                    className="btn btn-sm"
                    onClick={() => handleOpenFinancialHistory(selectedCall.partyId!, selectedCall.partyName || 'طرف حساب')}
                    style={{
                      background: 'rgba(34, 197, 94, 0.15)',
                      border: '1px solid rgba(34, 197, 94, 0.35)',
                      color: '#4ade80',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '5px',
                      fontSize: '0.78rem'
                    }}
                  >
                    <DollarSign size={13} />
                    <span>گردش مالی در سپیدار</span>
                  </button>
                )}
              </div>
            </div>

            {/* Recording Audio Player & Status */}
            <div style={{ marginBottom: '16px', background: '#0f172a', padding: '12px', borderRadius: '8px', border: '1px solid rgba(255,255,255,0.06)' }}>
              <span style={{ fontSize: '0.8rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '8px' }}>
                <Volume2 size={16} color={selectedCall.hasRecording ? "#4ade80" : (selectedCall.recordingFileName ? "#38bdf8" : "#94a3b8")} />
                <span>وضعیت ضبط مکالمه:</span>
              </span>
              {selectedCall.hasRecording ? (
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#4ade80', marginBottom: '6px' }}>
                    فایل صوتی محلی آماده پخش است ({selectedCall.recordingFileName})
                  </div>
                  <audio 
                    controls 
                    src={getCallRecordingUrl(selectedCall.id)}
                    style={{ width: '100%', height: '36px' }}
                    preload="metadata"
                  />
                </div>
              ) : (selectedCall.recordingFileName && selectedCall.recordingFileName.trim() !== '') ? (
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#38bdf8', marginBottom: '6px' }}>
                    نام فایل ضبط در CDR ثبت شده است ({selectedCall.recordingFileName}). با آغاز پخش، دریافت از سرور آزمایش می‌شود.
                  </div>
                  <audio 
                    controls 
                    src={getCallRecordingUrl(selectedCall.id)}
                    style={{ width: '100%', height: '36px' }}
                    preload="none"
                  />
                </div>
              ) : selectedCall.status === 0 || selectedCall.status === 1 ? (
                <div style={{ fontSize: '0.8rem', color: '#facc15' }}>
                  مکالمه در جریان است و فایل صوتی پس از پایان مکالمه آماده خواهد شد.
                </div>
              ) : (
                <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
                  وضعیت ضبط هنوز تأیید نشده است؛ نام فایل صوتی این تماس از CDR ایزابل به CRM نرسیده است.
                </div>
              )}
            </div>

            {/* Existing Notes */}
            <div style={{ marginBottom: '16px' }}>
              <h4 style={{ fontSize: '0.9rem', fontWeight: 600, marginBottom: '8px' }}>یادداشت‌ها و پیگیری‌های ثبت شده:</h4>
              {selectedCall.notes && selectedCall.notes.length > 0 ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  {selectedCall.notes.map(n => (
                    <div key={n.id} style={{ background: '#0f172a', padding: '10px', borderRadius: '8px', border: '1px solid rgba(255,255,255,0.06)' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: '#94a3b8', marginBottom: '4px' }}>
                        <span>ثبت توسط: {n.username}</span>
                        <span>{formatJalaliDateTime(n.createdAtUtc)}</span>
                      </div>
                      <div style={{ fontSize: '0.85rem', color: '#f8fafc' }}>{n.noteText}</div>
                      {n.followUpRequired && (
                        <div style={{ marginTop: '6px', fontSize: '0.75rem', color: '#facc15' }}>
                          اقدام بعدی: {n.outcome} | مهلت: {n.followUpDueDateUtc ? formatJalali(n.followUpDueDateUtc) : 'نامشخص'}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <p style={{ fontSize: '0.8rem', color: '#64748b' }}>هنوز یادداشتی برای این تماس ثبت نشده است.</p>
              )}
            </div>

            {/* Add New Note */}
            <div style={{ background: '#0f172a', padding: '12px', borderRadius: '8px' }}>
              <h4 style={{ fontSize: '0.85rem', fontWeight: 600, marginBottom: '8px' }}>ثبت یادداشت جدید:</h4>
              <textarea
                value={newNoteText}
                onChange={e => setNewNoteText(e.target.value)}
                placeholder="متن یادداشت، نتیجه مذاکره، شرایط اعلامی تأمین‌کننده..."
                rows={3}
                style={{
                  width: '100%',
                  background: '#1e293b',
                  border: '1px solid rgba(255,255,255,0.1)',
                  borderRadius: '6px',
                  color: '#f8fafc',
                  padding: '8px',
                  fontSize: '0.85rem',
                  resize: 'none',
                  boxSizing: 'border-box'
                }}
              />

              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '12px', alignItems: 'center', marginTop: '8px' }}>
                <select
                  value={newNoteOutcome}
                  onChange={e => setNewNoteOutcome(e.target.value)}
                  style={{
                    background: '#1e293b',
                    color: '#f8fafc',
                    border: '1px solid rgba(255,255,255,0.1)',
                    borderRadius: '6px',
                    fontSize: '0.8rem',
                    padding: '6px'
                  }}
                >
                  <option value="PriceInquiry">استعلام قیمت مواد</option>
                  <option value="OrderPlacement">ثبت یا صدور سفارش</option>
                  <option value="DeliveryFollowup">پیگیری تحویل و بارگیری</option>
                  <option value="Complaint">شکایت یا مغایرت کیفی</option>
                  <option value="General">مذاکره عمومی</option>
                </select>

                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.8rem', color: '#cbd5e1', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={newNoteFollowUp}
                    onChange={e => setNewNoteFollowUp(e.target.checked)}
                    style={{ accentColor: '#38bdf8' }}
                  />
                  <span>نیاز به پیگیری دارد</span>
                </label>

                {newNoteFollowUp && (
                  <div style={{ minWidth: '170px' }}>
                    <JalaliDatePicker
                      value={newNoteDueDate}
                      onChange={val => setNewNoteDueDate(val)}
                      placeholder="مهلت پیگیری..."
                    />
                  </div>
                )}

                <button
                  onClick={handleAddNote}
                  disabled={isSubmittingNote || !newNoteText.trim()}
                  className="btn btn-sm btn-primary"
                  style={{ marginRight: 'auto' }}
                >
                  {isSubmittingNote ? 'در حال ثبت...' : 'افزودن یادداشت'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Generalized Caller Identification Modal */}
      {classifyingCall && (
        <div className="modal-backdrop" onClick={() => setClassifyingCall(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ maxWidth: '560px' }}>
            <div className="modal-header">
              <h3>شناسایی و انتساب هویت تماس‌گیرنده</h3>
              <button onClick={() => setClassifyingCall(null)} className="close-btn"><X size={18} /></button>
            </div>

            <div style={{ marginBottom: '14px', background: '#0f172a', padding: '10px 14px', borderRadius: '8px' }}>
              <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>شماره تماس‌گیرنده:</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'monospace', color: '#38bdf8', marginTop: '2px' }}>
                {classifyingCall.callerNumber}
              </div>
            </div>

            {/* 4 Workflow Options Tabs */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '6px', marginBottom: '16px' }}>
              {[
                { id: 'link_existing', label: 'اتصال به طرف حساب' },
                { id: 'standalone_contact', label: 'مخاطب مستقل' },
                { id: 'representative', label: 'نماینده شرکت' },
                { id: 'new_party', label: 'طرف حساب جدید' }
              ].map(opt => (
                <button
                  key={opt.id}
                  onClick={() => setClassifyMode(opt.id as any)}
                  style={{
                    padding: '8px 4px',
                    fontSize: '0.75rem',
                    fontWeight: 600,
                    borderRadius: '6px',
                    cursor: 'pointer',
                    background: classifyMode === opt.id ? '#38bdf8' : 'rgba(255,255,255,0.05)',
                    color: classifyMode === opt.id ? '#0f172a' : '#94a3b8',
                    border: 'none',
                    textAlign: 'center'
                  }}
                >
                  {opt.label}
                </button>
              ))}
            </div>

            {/* Option A: Link Existing Party */}
            {classifyMode === 'link_existing' && (
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '6px' }}>
                  طرف حساب موجود را انتخاب کنید:
                </label>
                <select
                  value={selectedSupplierId}
                  onChange={e => setSelectedSupplierId(e.target.value)}
                  className="input-field"
                  style={{ width: '100%' }}
                >
                  <option value="">-- انتخاب طرف حساب از سیستم --</option>
                  {suppliersList.map(s => (
                    <option key={s.id} value={s.id}>
                      {s.displayName} {s.sepidarCode ? `[سپیدار: ${s.sepidarCode}]` : ''}
                    </option>
                  ))}
                </select>
              </div>
            )}

            {/* Option B: Standalone Contact */}
            {classifyMode === 'standalone_contact' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>نام و نام خانوادگی مخاطب:</label>
                  <input
                    type="text"
                    value={standaloneName}
                    onChange={e => setStandaloneName(e.target.value)}
                    placeholder="مثال: مهندس احمدی"
                    className="input-field"
                    style={{ width: '100%' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>سمت / حوزه فعالیت:</label>
                  <input
                    type="text"
                    value={standalonePosition}
                    onChange={e => setStandalonePosition(e.target.value)}
                    placeholder="مثال: مشاور فنی، بازرس"
                    className="input-field"
                    style={{ width: '100%' }}
                  />
                </div>
              </div>
            )}

            {/* Option C: Representative of Existing Party */}
            {classifyMode === 'representative' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>شرکت / طرف حساب:</label>
                  <select
                    value={selectedSupplierId}
                    onChange={e => setSelectedSupplierId(e.target.value)}
                    className="input-field"
                    style={{ width: '100%' }}
                  >
                    <option value="">-- انتخاب طرف حساب --</option>
                    {suppliersList.map(s => (
                      <option key={s.id} value={s.id}>
                        {s.displayName}
                      </option>
                    ))}
                  </select>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>نام نماینده:</label>
                    <input
                      type="text"
                      value={repName}
                      onChange={e => setRepName(e.target.value)}
                      placeholder="نام و نام خانوادگی"
                      className="input-field"
                      style={{ width: '100%' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>سمت نماینده:</label>
                    <input
                      type="text"
                      value={repPosition}
                      onChange={e => setRepPosition(e.target.value)}
                      placeholder="مثال: کارشناس فنی، مدیر فروش"
                      className="input-field"
                      style={{ width: '100%' }}
                    />
                  </div>
                </div>
              </div>
            )}

            {/* Option D: Brand New CRM Party */}
            {classifyMode === 'new_party' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>نام طرف حساب جدید:</label>
                  <input
                    type="text"
                    value={newPartyName}
                    onChange={e => setNewPartyName(e.target.value)}
                    placeholder="نام شرکت یا شخص"
                    className="input-field"
                    style={{ width: '100%' }}
                  />
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>ماهیت:</label>
                    <select
                      value={newPartyType}
                      onChange={e => setNewPartyType(Number(e.target.value))}
                      className="input-field"
                      style={{ width: '100%' }}
                    >
                      <option value={2}>شخص حقوقی (شرکت)</option>
                      <option value={1}>شخص حقیقی</option>
                    </select>
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>دسته‌بندی تجاری:</label>
                    <select
                      value={partyCategory}
                      onChange={e => setPartyCategory(Number(e.target.value))}
                      className="input-field"
                      style={{ width: '100%' }}
                    >
                      <option value={1}>تأمین‌کننده (Supplier)</option>
                      <option value={2}>خدماتی / پیمانکار (Service Provider)</option>
                      <option value={3}>مشتری (Customer)</option>
                      <option value={4}>سایر (Other)</option>
                    </select>
                  </div>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>نام رابط / مخاطب:</label>
                    <input
                      type="text"
                      value={newContactName}
                      onChange={e => setNewContactName(e.target.value)}
                      placeholder="اختیاری"
                      className="input-field"
                      style={{ width: '100%' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>سمت مخاطب:</label>
                    <input
                      type="text"
                      value={newContactPosition}
                      onChange={e => setNewContactPosition(e.target.value)}
                      placeholder="اختیاری"
                      className="input-field"
                      style={{ width: '100%' }}
                    />
                  </div>
                </div>
              </div>
            )}

            {/* Follow-up Assignment Field */}
            <div style={{ marginBottom: '16px', background: 'rgba(255,255,255,0.02)', padding: '10px', borderRadius: '6px', border: '1px solid rgba(255,255,255,0.06)' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>
                واحد یا دپارتمان مسئول پیگیری:
              </label>
              <input
                type="text"
                value={assignedDepartment}
                onChange={e => setAssignedDepartment(e.target.value)}
                placeholder="مثال: تدارکات، مالی، بازرگانی، اداری"
                className="input-field"
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button
                onClick={() => setClassifyingCall(null)}
                className="btn btn-secondary"
              >
                انصراف
              </button>
              <button
                onClick={handleClassify}
                disabled={isClassifying}
                className="btn btn-primary"
              >
                {isClassifying ? 'در حال ثبت...' : 'تأیید و اتصال به پرونده'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Caller Association Modal */}
      {editingAssociationCall && (
        <div className="modal-backdrop" onClick={() => setEditingAssociationCall(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ maxWidth: '480px' }}>
            <div className="modal-header">
              <h3>اصلاح انتساب تماس‌گیرنده (با ثبت ردپا)</h3>
              <button onClick={() => setEditingAssociationCall(null)} className="close-btn"><X size={18} /></button>
            </div>

            <div style={{ marginBottom: '14px', background: '#0f172a', padding: '10px 14px', borderRadius: '8px' }}>
              <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>شماره تماس: {editingAssociationCall.callerNumber}</div>
              <div style={{ fontSize: '0.85rem', color: '#e2e8f0', marginTop: '2px' }}>
                طرف حساب فعلی: <strong>{editingAssociationCall.partyName || 'نامشخص'}</strong>
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '16px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>
                  طرف حساب جدید:
                </label>
                <select
                  value={reassignPartyId}
                  onChange={e => setReassignPartyId(e.target.value)}
                  className="input-field"
                  style={{ width: '100%' }}
                >
                  <option value="">-- حذف انتساب (ناشناس شود) --</option>
                  {suppliersList.map(s => (
                    <option key={s.id} value={s.id}>
                      {s.displayName} {s.sepidarCode ? `[${s.sepidarCode}]` : ''}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>
                  واحد مسئول پیگیری:
                </label>
                <input
                  type="text"
                  value={reassignDept}
                  onChange={e => setReassignDept(e.target.value)}
                  placeholder="مثال: مالی، تدارکات"
                  className="input-field"
                  style={{ width: '100%' }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '4px' }}>
                  علت اصلاح انتساب (جهت درج در ردپای حسابرسی):
                </label>
                <textarea
                  value={reassignReason}
                  onChange={e => setReassignReason(e.target.value)}
                  placeholder="مثال: شماره به اشتباه به شرکت قبلی متصل شده بود..."
                  rows={2}
                  className="input-field"
                  style={{ width: '100%', resize: 'none' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button
                onClick={() => setEditingAssociationCall(null)}
                className="btn btn-secondary"
                disabled={isSavingAssociation}
              >
                انصراف
              </button>
              <button
                onClick={handleSaveCallerAssociation}
                disabled={isSavingAssociation}
                className="btn btn-primary"
              >
                {isSavingAssociation ? 'در حال ذخیره...' : 'ذخیره اصلاحیه'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Sepidar Financial History Modal */}
      {financialPartyId && (
        <div className="modal-backdrop" onClick={() => setFinancialPartyId(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ maxWidth: '780px', maxHeight: '90vh', overflowY: 'auto' }}>
            <div className="modal-header">
              <h3 style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <DollarSign size={20} color="#4ade80" />
                <span>گردش مالی طرف حساب در سپیدار سیستم</span>
              </h3>
              <button onClick={() => setFinancialPartyId(null)} className="close-btn"><X size={18} /></button>
            </div>

            <div style={{ marginBottom: '14px', background: '#0f172a', padding: '12px', borderRadius: '8px', border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <div style={{ fontWeight: 700, fontSize: '1rem', color: '#f8fafc' }}>{financialPartyName}</div>
                  {financialHistory?.sepidarCode && (
                    <div style={{ fontSize: '0.8rem', color: '#38bdf8', fontFamily: 'monospace' }}>
                      کد تفصیلی در سپیدار: {financialHistory.sepidarCode}
                    </div>
                  )}
                </div>
                {financialHistory?.retrievedAtUtc && (
                  <div style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
                    زمان استعلام: {formatJalaliDateTime(financialHistory.retrievedAtUtc)}
                  </div>
                )}
              </div>
            </div>

            {loadingFinancial ? (
              <div style={{ textAlign: 'center', padding: '40px', color: '#94a3b8' }}>
                در حال استعلام امن اطلاعات مالی از سرور سپیدار...
              </div>
            ) : financialError ? (
              <div style={{ background: 'rgba(239, 68, 68, 0.1)', border: '1px solid rgba(239, 68, 68, 0.3)', color: '#f87171', padding: '14px', borderRadius: '8px', fontSize: '0.88rem' }}>
                {financialError}
              </div>
            ) : financialHistory && !financialHistory.isConnected ? (
              <div style={{ background: 'rgba(234, 179, 8, 0.1)', border: '1px solid rgba(234, 179, 8, 0.3)', color: '#facc15', padding: '14px', borderRadius: '8px', fontSize: '0.88rem' }}>
                <strong>عدم دسترسی به پایگاه داده سپیدار:</strong>
                <p style={{ marginTop: '4px', margin: 0 }}>{financialHistory.statusMessage || financialHistory.message || 'اتصال زنده به سرور حسابداری سپیدار پیکربندی نشده است. اطلاعات مالی فرضی تولید نمی‌شود.'}</p>
              </div>
            ) : financialHistory && !financialHistory.isAvailable ? (
              <div style={{ background: 'rgba(234, 179, 8, 0.08)', border: '1px solid rgba(234, 179, 8, 0.25)', color: '#facc15', padding: '16px', borderRadius: '8px', fontSize: '0.88rem' }}>
                <div style={{ fontWeight: 700, marginBottom: '6px', color: '#fde047' }}>
                  اطلاعات مالی در حال حاضر در دسترس نیست (عدم تایید قراردادهای منبع):
                </div>
                <p style={{ margin: '0 0 10px 0', lineHeight: 1.6, color: '#fef08a' }}>
                  {financialHistory.statusMessage || financialHistory.message || 'قراردادهای حسابداری و پایگاه داده سپیدار برای این سامانه هنوز به تایید نهایی نرسیده‌اند.'}
                </p>
                {financialHistory.unverifiedDependencyDetails && (
                  <div style={{ background: '#090d16', border: '1px solid rgba(255,255,255,0.08)', padding: '10px', borderRadius: '6px', fontSize: '0.8rem', color: '#94a3b8', whiteSpace: 'pre-wrap', lineHeight: 1.5 }}>
                    <div style={{ fontWeight: 600, color: '#cbd5e1', marginBottom: '4px' }}>جزییات نیازمندی‌های باز منبع سپیدار:</div>
                    {financialHistory.unverifiedDependencyDetails}
                  </div>
                )}
                <div style={{ marginTop: '10px', fontSize: '0.78rem', color: '#94a3b8' }}>
                  * به منظور حفظ یکپارچگی مالی و انطباق با اصل شفافیت، اطلاعات مالی تخمینی یا مانده‌های فرضی نمایش داده نمی‌شوند.
                </div>
              </div>
            ) : financialHistory ? (
              <div>
                {/* Period info bar */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px', background: 'rgba(255,255,255,0.03)', padding: '8px 12px', borderRadius: '6px', fontSize: '0.82rem' }}>
                  <div>
                    <span style={{ color: '#94a3b8' }}>دوره مالی: </span>
                    <strong style={{ color: '#f8fafc' }}>{financialHistory.fiscalPeriod}</strong>
                  </div>
                  <div>
                    <span style={{ color: '#94a3b8' }}>واحد پول: </span>
                    <strong style={{ color: '#f8fafc' }}>{financialHistory.currency === 'IRR' ? 'ریال' : financialHistory.currency}</strong>
                  </div>
                </div>

                {/* Financial Balances Summary */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '10px', marginBottom: '16px' }}>
                  <div style={{ background: 'rgba(148, 163, 184, 0.08)', padding: '10px', borderRadius: '8px', border: '1px solid rgba(148, 163, 184, 0.2)' }}>
                    <div style={{ fontSize: '0.72rem', color: '#94a3b8' }}>تراز افتتاحیه:</div>
                    <div style={{ fontSize: '1rem', fontWeight: 700, color: '#cbd5e1', fontFamily: 'monospace', marginTop: '2px' }}>
                      {financialHistory.openingBalance.toLocaleString()}
                    </div>
                  </div>
                  <div style={{ background: 'rgba(56, 189, 248, 0.08)', padding: '10px', borderRadius: '8px', border: '1px solid rgba(56, 189, 248, 0.2)' }}>
                    <div style={{ fontSize: '0.72rem', color: '#94a3b8' }}>گردش بدهکار دوره:</div>
                    <div style={{ fontSize: '1rem', fontWeight: 700, color: '#38bdf8', fontFamily: 'monospace', marginTop: '2px' }}>
                      {financialHistory.totalDebit.toLocaleString()}
                    </div>
                  </div>
                  <div style={{ background: 'rgba(234, 179, 8, 0.08)', padding: '10px', borderRadius: '8px', border: '1px solid rgba(234, 179, 8, 0.2)' }}>
                    <div style={{ fontSize: '0.72rem', color: '#94a3b8' }}>گردش بستانکار دوره:</div>
                    <div style={{ fontSize: '1rem', fontWeight: 700, color: '#facc15', fontFamily: 'monospace', marginTop: '2px' }}>
                      {financialHistory.totalCredit.toLocaleString()}
                    </div>
                  </div>
                  <div style={{ background: financialHistory.balance >= 0 ? 'rgba(34, 197, 94, 0.08)' : 'rgba(239, 68, 68, 0.08)', padding: '10px', borderRadius: '8px', border: `1px solid ${financialHistory.balance >= 0 ? 'rgba(34, 197, 94, 0.2)' : 'rgba(239, 68, 68, 0.2)'}` }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: '0.72rem', color: '#94a3b8' }}>مانده نهایی:</span>
                      <span style={{ fontSize: '0.68rem', padding: '1px 6px', borderRadius: '4px', background: financialHistory.balance >= 0 ? 'rgba(34, 197, 94, 0.2)' : 'rgba(239, 68, 68, 0.2)', color: financialHistory.balance >= 0 ? '#4ade80' : '#f87171' }}>
                        {financialHistory.balanceType}
                      </span>
                    </div>
                    <div style={{ fontSize: '1rem', fontWeight: 700, color: financialHistory.balance >= 0 ? '#4ade80' : '#f87171', fontFamily: 'monospace', marginTop: '2px' }}>
                      {financialHistory.balance.toLocaleString()}
                    </div>
                  </div>
                </div>

                {/* Movements Table */}
                <div style={{ overflowX: 'auto', maxHeight: '320px' }}>
                  <table className="data-table" style={{ width: '100%', fontSize: '0.8rem', textAlign: 'right' }}>
                    <thead>
                      <tr>
                        <th>شماره سند</th>
                        <th>تاریخ سند</th>
                        <th>شرح آرتیکل</th>
                        <th>بدهکار</th>
                        <th>بستانکار</th>
                        <th>مانده تجمعی</th>
                      </tr>
                    </thead>
                    <tbody>
                      {financialHistory.movements.length === 0 ? (
                        <tr>
                          <td colSpan={6} style={{ textAlign: 'center', padding: '20px', color: '#94a3b8' }}>
                            گردش حسابی برای این طرف حساب در دوره مالی ثبت نشده است.
                          </td>
                        </tr>
                      ) : (
                        financialHistory.movements.map((m, idx) => (
                          <tr key={idx}>
                            <td style={{ fontFamily: 'monospace', fontWeight: 600 }}>{m.voucherNumber}</td>
                            <td>{m.voucherDateUtc ? formatJalali(m.voucherDateUtc) : '-'}</td>
                            <td style={{ maxWidth: '240px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={m.description}>
                              {m.description || '-'}
                            </td>
                            <td style={{ fontFamily: 'monospace', color: m.debit > 0 ? '#38bdf8' : '#64748b' }}>
                              {m.debit > 0 ? m.debit.toLocaleString() : '-'}
                            </td>
                            <td style={{ fontFamily: 'monospace', color: m.credit > 0 ? '#facc15' : '#64748b' }}>
                              {m.credit > 0 ? m.credit.toLocaleString() : '-'}
                            </td>
                            <td style={{ fontFamily: 'monospace', fontWeight: 700, color: m.cumulativeBalance >= 0 ? '#4ade80' : '#f87171' }}>
                              {m.cumulativeBalance.toLocaleString()}
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : null}

            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px' }}>
              <button onClick={() => setFinancialPartyId(null)} className="btn btn-secondary">
                بستن
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
