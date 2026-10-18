import React, { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { 
  PhoneIncoming, 
  PhoneOutgoing, 
  Building2, 
  User, 
  Clock, 
  CheckCircle, 
  X, 
  ExternalLink, 
  FileText, 
  AlertCircle 
} from 'lucide-react';
import { CallerPopupDto, PartyMatchDto, CreateCallNoteRequest } from '../types';
import { addCallNote, classifyCall } from '../services/api';

interface CallPopupProps {
  devRole?: string;
  onNavigateToSupplier?: (supplierId: string) => void;
  userExtension?: string;
}

export const CallPopup: React.FC<CallPopupProps> = ({ 
  devRole = 'Procurement', 
  onNavigateToSupplier, 
  userExtension = '101' 
}) => {
  const [popup, setPopup] = useState<CallerPopupDto | null>(null);
  const [selectedParty, setSelectedParty] = useState<PartyMatchDto | null>(null);
  const [noteText, setNoteText] = useState('');
  const [outcome, setOutcome] = useState('PriceInquiry');
  const [followUpRequired, setFollowUpRequired] = useState(false);
  const [followUpDueDate, setFollowUpDueDate] = useState('');
  const [isSubmittingNote, setIsSubmittingNote] = useState(false);
  const [noteSaved, setNoteSaved] = useState(false);
  const [_connectionStatus, setConnectionStatus] = useState<'connected' | 'connecting' | 'disconnected'>('connecting');
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const playChime = () => {
    try {
      const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
      if (!AudioCtx) return;
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
      osc.frequency.setValueAtTime(880, ctx.currentTime + 0.12); // A5
      gain.gain.setValueAtTime(0.12, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.6);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + 0.6);
    } catch {
      // Audio context policy
    }
  };

  useEffect(() => {
    const hubUrl = '/hubs/telephony';
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        headers: {
          'X-Dev-Role': devRole,
          'X-Dev-User': `sahar\\${devRole.toLowerCase()}`
        }
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('ReceiveCallPopup', (data: CallerPopupDto) => {
      setPopup(data);
      setNoteText('');
      setNoteSaved(false);
      if (data.matchedParties && data.matchedParties.length === 1) {
        setSelectedParty(data.matchedParties[0]);
      } else {
        setSelectedParty(null);
      }
      playChime();
    });

    connection.on('CallStatusChanged', (call) => {
      setPopup(prev => {
        if (!prev || prev.uniqueId !== call.uniqueId) return prev;
        return {
          ...prev,
          status: call.status,
          durationSeconds: call.durationSeconds
        };
      });
    });

    connection.on('CallEnded', (uniqueId: string) => {
      setPopup(prev => {
        if (!prev || prev.uniqueId !== uniqueId) return prev;
        return {
          ...prev,
          status: 6 // Completed
        };
      });
    });

    const startConnection = async () => {
      try {
        setConnectionStatus('connecting');
        await connection.start();
        setConnectionStatus('connected');
        if (userExtension) {
          await connection.invoke('RegisterExtension', userExtension);
        }
      } catch (err) {
        setConnectionStatus('disconnected');
      }
    };

    startConnection();

    connection.onreconnected(async () => {
      setConnectionStatus('connected');
      if (userExtension) {
        await connection.invoke('RegisterExtension', userExtension);
      }
    });

    connection.onclose(() => {
      setConnectionStatus('disconnected');
    });

    return () => {
      connection.stop();
    };
  }, [devRole, userExtension]);

  const handleSaveNote = async () => {
    if (!popup || !noteText.trim()) return;
    setIsSubmittingNote(true);
    try {
      const payload: CreateCallNoteRequest = {
        noteText: noteText.trim(),
        outcome,
        followUpRequired,
        followUpDueDateUtc: followUpRequired && followUpDueDate ? new Date(followUpDueDate).toISOString() : undefined,
        followUpAssignedTo: devRole.toLowerCase()
      };
      await addCallNote(popup.callId, payload, devRole);
      setNoteSaved(true);
      setTimeout(() => {
        setNoteSaved(false);
      }, 3000);
    } catch (err: any) {
      alert(err.message || 'خطا در ثبت یادداشت تماس');
    } finally {
      setIsSubmittingNote(false);
    }
  };

  const handleSelectParty = async (party: PartyMatchDto) => {
    if (!popup) return;
    setSelectedParty(party);
    try {
      await classifyCall(popup.callId, { partyId: party.partyId, contactId: party.contactId }, devRole);
    } catch (err) {
      console.error(err);
    }
  };

  if (!popup) {
    return null;
  }

  const isRinging = popup.status === 0;
  const isAnswered = popup.status === 1;
  const isCompleted = popup.status === 6;
  const isMissed = popup.status === 2;

  const statusLabel = isRinging ? 'در حال زنگ خوردن...' :
    isAnswered ? 'مکالمه برقرار است' :
    isMissed ? 'تماس بی‌پاسخ' :
    isCompleted ? 'مکالمه پایان یافت' : 'تماس ایزابل';

  const statusBg = isRinging ? 'rgba(234, 179, 8, 0.15)' :
    isAnswered ? 'rgba(34, 197, 94, 0.15)' :
    isMissed ? 'rgba(239, 68, 68, 0.15)' : 'rgba(56, 189, 248, 0.15)';

  const statusColor = isRinging ? '#facc15' :
    isAnswered ? '#4ade80' :
    isMissed ? '#f87171' : '#38bdf8';

  return (
    <div 
      style={{
        position: 'fixed',
        bottom: '24px',
        left: '24px',
        zIndex: 9999,
        width: '420px',
        maxHeight: '85vh',
        overflowY: 'auto',
        background: 'linear-gradient(135deg, rgba(17, 24, 39, 0.95), rgba(30, 41, 59, 0.95))',
        backdropFilter: 'blur(16px)',
        border: `2px solid ${isRinging ? '#facc15' : 'rgba(56, 189, 248, 0.3)'}`,
        borderRadius: '16px',
        boxShadow: isRinging 
          ? '0 12px 36px rgba(250, 204, 21, 0.25), 0 0 20px rgba(250, 204, 21, 0.2)' 
          : '0 20px 45px rgba(0, 0, 0, 0.6), 0 0 20px rgba(56, 189, 248, 0.15)',
        color: '#f8fafc',
        fontFamily: 'inherit',
        direction: 'rtl',
        padding: '16px',
        animation: 'slideUp 0.3s cubic-bezier(0.16, 1, 0.3, 1)'
      }}
    >
      {/* Top Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <div style={{
            width: '36px',
            height: '36px',
            borderRadius: '10px',
            background: statusBg,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: statusColor,
            animation: isRinging ? 'pulse 1.5s infinite' : 'none'
          }}>
            {popup.direction === 0 ? <PhoneIncoming size={20} /> : <PhoneOutgoing size={20} />}
          </div>
          <div>
            <div style={{ fontSize: '0.85rem', fontWeight: 600, color: statusColor, display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span>{statusLabel}</span>
              {isRinging && <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#facc15', display: 'inline-block' }} />}
            </div>
            <div style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
              داخلی: {popup.extension || userExtension} | {new Date(popup.timestampUtc).toLocaleTimeString('fa-IR')}
            </div>
          </div>
        </div>
        <button 
          onClick={() => setPopup(null)}
          style={{
            background: 'transparent',
            border: 'none',
            color: '#94a3b8',
            cursor: 'pointer',
            padding: '4px',
            borderRadius: '6px'
          }}
          title="بستن پنجره"
        >
          <X size={18} />
        </button>
      </div>

      {/* Caller Info Card */}
      <div style={{
        background: 'rgba(15, 23, 42, 0.6)',
        borderRadius: '12px',
        padding: '12px',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        marginBottom: '12px'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>شماره تماس گیرنده:</span>
          <span style={{ 
            fontFamily: 'monospace', 
            fontSize: '1.05rem', 
            fontWeight: 700, 
            color: '#38bdf8',
            letterSpacing: '0.5px' 
          }}>
            {popup.callerNumber}
          </span>
        </div>

        {/* Identified Supplier */}
        <div style={{ marginTop: '8px', paddingTop: '8px', borderTop: '1px solid rgba(255, 255, 255, 0.06)' }}>
          {selectedParty ? (
            <div>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 600, color: '#f8fafc', fontSize: '0.95rem' }}>
                  <Building2 size={16} color="#38bdf8" />
                  <span>{selectedParty.partyName}</span>
                </div>
                {onNavigateToSupplier && (
                  <button
                    onClick={() => {
                      onNavigateToSupplier(selectedParty.partyId);
                      setPopup(null);
                    }}
                    style={{
                      background: 'rgba(56, 189, 248, 0.1)',
                      border: '1px solid rgba(56, 189, 248, 0.3)',
                      color: '#38bdf8',
                      fontSize: '0.75rem',
                      borderRadius: '6px',
                      padding: '3px 8px',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '4px'
                    }}
                  >
                    <span>پرونده</span>
                    <ExternalLink size={12} />
                  </button>
                )}
              </div>
              {selectedParty.contactName && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#cbd5e1', fontSize: '0.8rem', marginTop: '4px' }}>
                  <User size={13} color="#94a3b8" />
                  <span>نماینده: {selectedParty.contactName} {selectedParty.contactRole ? `(${selectedParty.contactRole})` : ''}</span>
                </div>
              )}
            </div>
          ) : popup.matchedParties && popup.matchedParties.length > 1 ? (
            <div>
              <div style={{ fontSize: '0.8rem', color: '#facc15', display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '6px' }}>
                <AlertCircle size={14} />
                <span>چند تأمین‌کننده با این شماره منطبق هستند:</span>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                {popup.matchedParties.map(p => (
                  <button
                    key={p.partyId}
                    onClick={() => handleSelectParty(p)}
                    style={{
                      background: 'rgba(30, 41, 59, 0.7)',
                      border: '1px solid rgba(255, 255, 255, 0.1)',
                      color: '#f8fafc',
                      fontSize: '0.8rem',
                      padding: '6px 8px',
                      borderRadius: '6px',
                      textAlign: 'right',
                      cursor: 'pointer',
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center'
                    }}
                  >
                    <span>{p.partyName} {p.contactName ? `(${p.contactName})` : ''}</span>
                    <span style={{ fontSize: '0.7rem', color: '#38bdf8' }}>انتخاب</span>
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '6px', color: '#94a3b8', fontSize: '0.85rem' }}>
                <AlertCircle size={15} color="#e2e8f0" />
                <span>شماره ناشناس (ثبت نشده در پرونده تأمین‌کنندگان)</span>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Recent Call History */}
      {popup.recentCalls && popup.recentCalls.length > 0 && (
        <div style={{ marginBottom: '12px' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#94a3b8', marginBottom: '6px', display: 'flex', alignItems: 'center', gap: '4px' }}>
            <Clock size={13} />
            <span>سوابق تماس‌های اخیر با این شماره:</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            {popup.recentCalls.slice(0, 3).map(rc => (
              <div 
                key={rc.id}
                style={{
                  background: 'rgba(15, 23, 42, 0.4)',
                  padding: '6px 8px',
                  borderRadius: '6px',
                  fontSize: '0.75rem',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center'
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  {rc.direction === 0 ? <PhoneIncoming size={12} color="#4ade80" /> : <PhoneOutgoing size={12} color="#38bdf8" />}
                  <span>{new Date(rc.startTimeUtc).toLocaleDateString('fa-IR')}</span>
                  {rc.durationSeconds ? <span>({rc.durationSeconds} ثانیه)</span> : null}
                </div>
                <div style={{ color: '#94a3b8', maxWidth: '160px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {rc.noteSummary || 'بدون یادداشت'}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Quick Call Note & Follow-up */}
      <div style={{
        background: 'rgba(15, 23, 42, 0.4)',
        borderRadius: '10px',
        padding: '10px',
        border: '1px solid rgba(255, 255, 255, 0.06)'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '6px' }}>
          <span style={{ fontSize: '0.78rem', fontWeight: 600, color: '#e2e8f0', display: 'flex', alignItems: 'center', gap: '4px' }}>
            <FileText size={13} />
            <span>ثبت یادداشت و پیگیری فوری:</span>
          </span>
          <select
            value={outcome}
            onChange={e => setOutcome(e.target.value)}
            style={{
              background: '#1e293b',
              color: '#f8fafc',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              borderRadius: '6px',
              fontSize: '0.75rem',
              padding: '2px 6px'
            }}
          >
            <option value="PriceInquiry">استعلام قیمت مواد</option>
            <option value="OrderPlacement">ثبت یا صدور سفارش</option>
            <option value="DeliveryFollowup">پیگیری تحویل و بارگیری</option>
            <option value="Complaint">شکایت یا مغایرت کیفی</option>
            <option value="General">مذاکره عمومی</option>
          </select>
        </div>

        <textarea
          value={noteText}
          onChange={e => setNoteText(e.target.value)}
          placeholder="خلاصه مکالمه، توافقات یا نیازمندی‌های تأمین‌کننده..."
          rows={2}
          style={{
            width: '100%',
            background: '#0f172a',
            border: '1px solid rgba(255, 255, 255, 0.12)',
            borderRadius: '6px',
            color: '#f8fafc',
            fontSize: '0.8rem',
            padding: '6px 8px',
            resize: 'none',
            fontFamily: 'inherit',
            outline: 'none',
            boxSizing: 'border-box'
          }}
        />

        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: '8px' }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.75rem', color: '#cbd5e1', cursor: 'pointer' }}>
            <input 
              type="checkbox"
              checked={followUpRequired}
              onChange={e => setFollowUpRequired(e.target.checked)}
              style={{ accentColor: '#38bdf8' }}
            />
            <span>نیاز به پیگیری بعدی دارد</span>
          </label>

          {followUpRequired && (
            <input
              type="date"
              value={followUpDueDate}
              onChange={e => setFollowUpDueDate(e.target.value)}
              style={{
                background: '#1e293b',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                fontSize: '0.75rem',
                padding: '2px 6px',
                borderRadius: '6px'
              }}
            />
          )}

          <button
            onClick={handleSaveNote}
            disabled={isSubmittingNote || !noteText.trim()}
            style={{
              background: noteSaved ? '#22c55e' : '#0284c7',
              color: '#ffffff',
              border: 'none',
              borderRadius: '6px',
              padding: '4px 12px',
              fontSize: '0.75rem',
              fontWeight: 600,
              cursor: isSubmittingNote || !noteText.trim() ? 'not-allowed' : 'pointer',
              opacity: isSubmittingNote || !noteText.trim() ? 0.6 : 1,
              display: 'flex',
              alignItems: 'center',
              gap: '4px'
            }}
          >
            {noteSaved ? (
              <>
                <CheckCircle size={13} />
                <span>ثبت شد</span>
              </>
            ) : (
              <span>{isSubmittingNote ? 'در حال ثبت...' : 'ذخیره یادداشت'}</span>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
