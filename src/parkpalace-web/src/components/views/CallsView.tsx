import React, { useState, useEffect } from 'react';
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
  RefreshCw, 
  ShieldCheck, 
  Radio,
  ExternalLink
} from 'lucide-react';
import { 
  CallRecordDto, 
  CallNoteDto, 
  PartyDto, 
  CreateCallNoteRequest, 
  SimulateCallRequest, 
  CdrReconciliationDto, 
  FollowUpStatus 
} from '../../types';
import { 
  fetchCalls, 
  addCallNote, 
  classifyCall, 
  fetchUnknownCalls, 
  fetchFollowUps, 
  updateFollowUpStatus, 
  getCallRecordingUrl, 
  simulateCall, 
  reconcileCdrBatch, 
  fetchSuppliers 
} from '../../services/api';
import { JalaliDatePicker } from '../JalaliDatePicker';
import { formatJalali, formatJalaliDateTime, dateOnlyToIsoUtc } from '../../utils/jalali';

interface CallsViewProps {
  devRole?: string;
  onNavigateToSupplier?: (supplierId: string) => void;
}

export const CallsView: React.FC<CallsViewProps> = ({ 
  devRole = 'Procurement', 
  onNavigateToSupplier 
}) => {
  const [activeTab, setActiveTab] = useState<'log' | 'unknown' | 'followups' | 'cdr' | 'simulator'>('log');
  
  // Call Log state
  const [calls, setCalls] = useState<CallRecordDto[]>([]);
  const [isLoadingCalls, setIsLoadingCalls] = useState(false);
  const [searchNumber, setSearchNumber] = useState('');
  const [filterDirection, setFilterDirection] = useState<string>('all');
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [filterExtension, setFilterExtension] = useState<string>('');
  
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
    setIsLoadingCalls(true);
    try {
      const params: any = { take: 50 };
      if (filterExtension) params.extension = filterExtension;
      if (filterDirection !== 'all') params.direction = parseInt(filterDirection, 10);
      if (filterStatus !== 'all') params.status = parseInt(filterStatus, 10);
      const data = await fetchCalls(params, devRole);
      setCalls(data);
    } catch (err: any) {
      console.error(err);
    } finally {
      setIsLoadingCalls(false);
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

  useEffect(() => {
    loadCalls();
    loadFollowUps();
    loadSuppliers();
  }, [devRole, filterDirection, filterStatus, filterExtension]);

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
    if (!classifyingCall || !selectedSupplierId) return;
    setIsClassifying(true);
    try {
      await classifyCall(classifyingCall.id, { partyId: selectedSupplierId }, devRole);
      setClassifyingCall(null);
      setSelectedSupplierId('');
      loadUnknownCalls();
      loadCalls();
    } catch (err: any) {
      alert(err.message || 'خطا در انتساب تأمین‌کننده');
    } finally {
      setIsClassifying(false);
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
      c.callerNumber.toLowerCase().includes(term) ||
      (c.callerName && c.callerName.toLowerCase().includes(term)) ||
      (c.partyName && c.partyName.toLowerCase().includes(term))
    );
  });

  const getStatusBadge = (status: number) => {
    switch (status) {
      case 0:
        return <span className="badge badge-warning">زنگ خوردن</span>;
      case 1:
        return <span className="badge badge-info">پاسخ داده شد</span>;
      case 2:
        return <span className="badge badge-danger">بی‌پاسخ</span>;
      case 3:
        return <span className="badge badge-danger">اشغال</span>;
      case 4:
        return <span className="badge badge-danger">ناموفق</span>;
      case 5:
        return <span className="badge badge-primary">انتقال‌یافته</span>;
      case 6:
        return <span className="badge badge-success">پایان‌یافته</span>;
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
          style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
        >
          <AlertCircle size={16} />
          <span>تماس‌های ناشناس ({unknownCalls.length})</span>
        </button>

        <button
          className={`tab-btn ${activeTab === 'followups' ? 'active' : ''}`}
          onClick={() => setActiveTab('followups')}
          style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
        >
          <CheckCircle size={16} />
          <span>وظایف پیگیری ({followUps.length})</span>
        </button>

        <button
          className={`tab-btn ${activeTab === 'cdr' ? 'active' : ''}`}
          onClick={() => setActiveTab('cdr')}
          style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
        >
          <ShieldCheck size={16} />
          <span>تطبیق اسناد CDR/CEL</span>
        </button>

        <button
          className={`tab-btn ${activeTab === 'simulator' ? 'active' : ''}`}
          onClick={() => setActiveTab('simulator')}
          style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
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
              value={filterDirection}
              onChange={e => setFilterDirection(e.target.value)}
              style={{
                background: '#0f172a',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                padding: '8px 12px',
                borderRadius: '8px',
                fontSize: '0.85rem'
              }}
            >
              <option value="all">همه جهت‌ها</option>
              <option value="0">ورودی (Incoming)</option>
              <option value="1">خروجی (Outgoing)</option>
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
              <option value="1">پاسخ داده شده</option>
              <option value="6">تکمیل شده</option>
              <option value="2">بی‌پاسخ (Missed)</option>
              <option value="3">اشغال (Busy)</option>
              <option value="5">انتقال‌یافته</option>
            </select>

            <input
              type="text"
              placeholder="فیلتر داخلی (۱۰۱...)"
              value={filterExtension}
              onChange={e => setFilterExtension(e.target.value)}
              style={{
                width: '120px',
                background: '#0f172a',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                padding: '8px 12px',
                borderRadius: '8px',
                fontSize: '0.85rem'
              }}
            />

            <button
              onClick={loadCalls}
              className="btn btn-secondary"
              style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
              title="بارگذاری مجدد"
            >
              <RefreshCw size={14} />
              <span>تازه‌سازی</span>
            </button>
          </div>

          {/* Table */}
          <div className="table-responsive">
            <table className="data-table">
              <thead>
                <tr>
                  <th style={{ width: '40px' }}>جهت</th>
                  <th>شماره تماس</th>
                  <th>تأمین‌کننده / مخاطب</th>
                  <th>داخلی</th>
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
                    <td colSpan={10} style={{ textAlign: 'center', padding: '24px', color: '#94a3b8' }}>
                      در حال بارگذاری لاگ تماس‌ها...
                    </td>
                  </tr>
                ) : filteredCalls.length === 0 ? (
                  <tr>
                    <td colSpan={10} style={{ textAlign: 'center', padding: '24px', color: '#94a3b8' }}>
                      هیچ رکوردی برای نمایش یافت نشد.
                    </td>
                  </tr>
                ) : (
                  filteredCalls.map(c => (
                    <tr key={c.id}>
                      <td style={{ textAlign: 'center' }}>
                        {c.direction === 0 ? (
                          <span title="تماس ورودی">
                            <PhoneIncoming size={16} color="#4ade80" />
                          </span>
                        ) : (
                          <span title="تماس خروجی">
                            <PhoneOutgoing size={16} color="#38bdf8" />
                          </span>
                        )}
                      </td>
                      <td style={{ fontFamily: 'monospace', fontWeight: 600 }}>{c.callerNumber}</td>
                      <td>
                        {c.partyName ? (
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
                      <td>{c.extension || '-'}</td>
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
                        ) : (
                          <span style={{ fontSize: '0.75rem', color: '#64748b' }}>فاقد ضبط</span>
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
                  ))
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
              شماره‌هایی که در سیستم CRM به هیچ تأمین‌کننده‌ای متصل نیستند. با انتساب یک شماره به تأمین‌کننده، کلیه سوابق تماس‌های گذشته با این شماره نیز به‌صورت عطف‌به‌ماسبق به‌روزرسانی خواهند شد.
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
                        انتساب به تأمین‌کننده
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
                <div style={{ fontWeight: 700, fontFamily: 'monospace' }}>{selectedCall.callerNumber}</div>
              </div>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>تأمین‌کننده:</span>
                <div style={{ fontWeight: 600 }}>{selectedCall.partyName || 'ناشناس'}</div>
              </div>
              <div>
                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>جهت تماس:</span>
                <div>{selectedCall.direction === 0 ? 'ورودی' : 'خروجی'}</div>
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
            </div>

            {/* Recording Audio Player */}
            {selectedCall.hasRecording && (
              <div style={{ marginBottom: '16px', background: '#0f172a', padding: '12px', borderRadius: '8px' }}>
                <span style={{ fontSize: '0.8rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '8px' }}>
                  <Volume2 size={16} color="#4ade80" />
                  <span>پخش مکالمه ضبط شده (استریم امن API):</span>
                </span>
                <audio 
                  controls 
                  src={getCallRecordingUrl(selectedCall.id)}
                  style={{ width: '100%', height: '36px' }}
                />
              </div>
            )}

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

      {/* Classify Supplier Modal */}
      {classifyingCall && (
        <div className="modal-backdrop" onClick={() => setClassifyingCall(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()} style={{ maxWidth: '480px' }}>
            <div className="modal-header">
              <h3>انتساب شماره ناشناس به تأمین‌کننده</h3>
              <button onClick={() => setClassifyingCall(null)} className="close-btn"><X size={18} /></button>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <div style={{ fontSize: '0.85rem', color: '#94a3b8' }}>شماره تماس گیرنده:</div>
              <div style={{ fontSize: '1.1rem', fontWeight: 700, fontFamily: 'monospace', color: '#38bdf8', marginTop: '2px' }}>
                {classifyingCall.callerNumber}
              </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', color: '#cbd5e1', marginBottom: '6px' }}>
                تأمین‌کننده مرتبط را انتخاب کنید:
              </label>
              <select
                value={selectedSupplierId}
                onChange={e => setSelectedSupplierId(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '8px',
                  background: '#0f172a',
                  border: '1px solid rgba(255, 255, 255, 0.1)',
                  color: '#f8fafc',
                  fontSize: '0.85rem'
                }}
              >
                <option value="">-- انتخاب تأمین‌کننده از لیست --</option>
                {suppliersList.map(s => (
                  <option key={s.id} value={s.id}>
                    {s.displayName} {s.nationalIdentifier ? `(${s.nationalIdentifier})` : ''}
                  </option>
                ))}
              </select>
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
                disabled={isClassifying || !selectedSupplierId}
                className="btn btn-primary"
              >
                {isClassifying ? 'در حال ثبت...' : 'تأیید و اتصال به پرونده'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
