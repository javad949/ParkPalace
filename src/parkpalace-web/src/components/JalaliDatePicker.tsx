import React, { useState, useRef, useEffect } from 'react';
import { Calendar as CalendarIcon, ChevronRight, ChevronLeft, X, CalendarCheck } from 'lucide-react';
import {
  PERSIAN_MONTH_NAMES,
  PERSIAN_WEEKDAY_SHORT,
  toPersianDigits,
  isoToJalali,
  jalaliToIsoDate,
  getJalaliMonthDays,
  getJalaliMonthFirstDayOfWeek,
  getTodayJalali,
  parseJalaliInput
} from '../utils/jalali';

export interface JalaliDatePickerProps {
  value?: string | null; // ISO "YYYY-MM-DD" or timestamp
  onChange: (isoDate: string) => void;
  label?: string;
  placeholder?: string;
  disabled?: boolean;
  required?: boolean;
  clearable?: boolean;
  minDate?: string;
  maxDate?: string;
  style?: React.CSSProperties;
  className?: string;
  id?: string;
}

export const JalaliDatePicker: React.FC<JalaliDatePickerProps> = ({
  value,
  onChange,
  label,
  placeholder = 'انتخاب تاریخ شمسی...',
  disabled = false,
  required = false,
  clearable = true,
  style,
  className = '',
  id
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Active Jalali date parsed from value
  const activeJalali = isoToJalali(value);
  const today = getTodayJalali();

  // Calendar navigation state (viewed year and month)
  const [viewYear, setViewYear] = useState<number>(activeJalali?.jy || today.jy);
  const [viewMonth, setViewMonth] = useState<number>(activeJalali?.jm || today.jm);

  // Local text input representation
  const [textInput, setTextInput] = useState<string>('');

  // Update viewed month/year and text input when value changes
  useEffect(() => {
    if (activeJalali) {
      setViewYear(activeJalali.jy);
      setViewMonth(activeJalali.jm);
      const m = activeJalali.jm.toString().padStart(2, '0');
      const d = activeJalali.jd.toString().padStart(2, '0');
      setTextInput(toPersianDigits(`${activeJalali.jy}/${m}/${d}`));
    } else {
      setTextInput('');
    }
  }, [value]);

  // Click outside listener
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  // Handle month navigation
  const handlePrevMonth = () => {
    if (viewMonth === 1) {
      setViewMonth(12);
      setViewYear(prev => prev - 1);
    } else {
      setViewMonth(prev => prev - 1);
    }
  };

  const handleNextMonth = () => {
    if (viewMonth === 12) {
      setViewMonth(1);
      setViewYear(prev => prev + 1);
    } else {
      setViewMonth(prev => prev + 1);
    }
  };

  const handleSelectDay = (day: number) => {
    const iso = jalaliToIsoDate(viewYear, viewMonth, day);
    onChange(iso);
    setIsOpen(false);
  };

  const handleGoToToday = () => {
    setViewYear(today.jy);
    setViewMonth(today.jm);
    handleSelectDay(today.jd);
  };

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation();
    onChange('');
    setTextInput('');
  };

  const handleManualInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setTextInput(val);
    const parsed = parseJalaliInput(val);
    if (parsed) {
      const iso = jalaliToIsoDate(parsed.jy, parsed.jm, parsed.jd);
      onChange(iso);
      setViewYear(parsed.jy);
      setViewMonth(parsed.jm);
    }
  };

  // Build calendar matrix
  const daysInCurrentMonth = getJalaliMonthDays(viewYear, viewMonth);
  const firstDayOfWeek = getJalaliMonthFirstDayOfWeek(viewYear, viewMonth); // 0 (Shanbe) to 6 (Jome)

  // Previous month trailing days
  const prevMonth = viewMonth === 1 ? 12 : viewMonth - 1;
  const prevYear = viewMonth === 1 ? viewYear - 1 : viewYear;
  const daysInPrevMonth = getJalaliMonthDays(prevYear, prevMonth);

  const prevMonthDays: number[] = [];
  for (let i = firstDayOfWeek - 1; i >= 0; i--) {
    prevMonthDays.push(daysInPrevMonth - i);
  }

  // Current month days
  const currentMonthDays: number[] = [];
  for (let d = 1; d <= daysInCurrentMonth; d++) {
    currentMonthDays.push(d);
  }

  // Next month leading days (to fill 35 or 42 grid)
  const totalCells = (prevMonthDays.length + currentMonthDays.length) > 35 ? 42 : 35;
  const nextMonthDaysCount = totalCells - (prevMonthDays.length + currentMonthDays.length);
  const nextMonthDays: number[] = [];
  for (let d = 1; d <= nextMonthDaysCount; d++) {
    nextMonthDays.push(d);
  }

  // Year options for dropdown (1380 to 1420)
  const yearOptions: number[] = [];
  for (let y = 1380; y <= 1420; y++) {
    yearOptions.push(y);
  }

  return (
    <div
      ref={containerRef}
      className={`jalali-picker-wrapper ${className}`}
      style={{ position: 'relative', width: '100%', ...style }}
    >
      {label && (
        <label
          htmlFor={id}
          style={{
            display: 'block',
            fontSize: '13px',
            fontWeight: 600,
            color: 'var(--text-main)',
            marginBottom: '6px'
          }}
        >
          {label} {required && <span style={{ color: '#ef4444' }}>*</span>}
        </label>
      )}

      {/* Input Box */}
      <div
        className="jalali-input-box"
        onClick={() => !disabled && setIsOpen(prev => !prev)}
        style={{
          display: 'flex',
          alignItems: 'center',
          background: 'var(--bg-sidebar, #0f172a)',
          border: isOpen ? '1px solid var(--primary, #0284c7)' : '1px solid var(--border-color, #1e293b)',
          boxShadow: isOpen ? '0 0 0 2px var(--primary-light, rgba(2, 132, 199, 0.25))' : 'none',
          borderRadius: '8px',
          padding: '8px 12px',
          cursor: disabled ? 'not-allowed' : 'pointer',
          opacity: disabled ? 0.6 : 1,
          transition: 'all 0.2s ease',
          height: '40px'
        }}
      >
        <CalendarIcon
          size={16}
          color={isOpen ? 'var(--primary, #0284c7)' : 'var(--text-muted, #94a3b8)'}
          style={{ marginLeft: '10px', flexShrink: 0 }}
        />

        <input
          id={id}
          type="text"
          disabled={disabled}
          placeholder={placeholder}
          value={textInput}
          onChange={handleManualInput}
          onClick={(e) => {
            e.stopPropagation();
            if (!disabled && !isOpen) setIsOpen(true);
          }}
          style={{
            background: 'transparent',
            border: 'none',
            outline: 'none',
            color: 'var(--text-main, #f8fafc)',
            fontFamily: 'var(--font-family)',
            fontSize: '13.5px',
            width: '100%',
            cursor: disabled ? 'not-allowed' : 'text'
          }}
        />

        {clearable && value && !disabled && (
          <button
            type="button"
            onClick={handleClear}
            title="پاک کردن تاریخ"
            style={{
              background: 'none',
              border: 'none',
              color: 'var(--text-dim, #64748b)',
              cursor: 'pointer',
              padding: '2px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              borderRadius: '50%',
              marginRight: '4px'
            }}
          >
            <X size={14} />
          </button>
        )}
      </div>

      {/* Calendar Dropdown Popup */}
      {isOpen && (
        <div
          className="jalali-dropdown-popup"
          style={{
            position: 'absolute',
            top: 'calc(100% + 6px)',
            right: 0,
            zIndex: 1050,
            background: 'var(--bg-card, #131c31)',
            border: '1px solid var(--border-accent, #334155)',
            borderRadius: '12px',
            boxShadow: '0 12px 32px rgba(0, 0, 0, 0.45)',
            padding: '16px',
            width: '290px',
            userSelect: 'none'
          }}
        >
          {/* Header: Month & Year Selectors & Arrows */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: '14px',
              gap: '6px'
            }}
          >
            {/* Prev month arrow (in RTL, Right arrow goes backward) */}
            <button
              type="button"
              onClick={handlePrevMonth}
              title="ماه قبل"
              className="btn-icon"
              style={{
                background: 'rgba(255, 255, 255, 0.05)',
                border: '1px solid var(--border-color, #1e293b)',
                borderRadius: '6px',
                color: 'var(--text-main)',
                width: '28px',
                height: '28px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                cursor: 'pointer'
              }}
            >
              <ChevronRight size={16} />
            </button>

            {/* Month & Year Controls */}
            <div style={{ display: 'flex', gap: '6px', flex: 1, justifyContent: 'center' }}>
              <select
                value={viewMonth}
                onChange={e => setViewMonth(Number(e.target.value))}
                style={{
                  background: 'var(--bg-sidebar, #0f172a)',
                  border: '1px solid var(--border-color, #1e293b)',
                  borderRadius: '6px',
                  color: 'var(--text-main)',
                  padding: '3px 8px',
                  fontSize: '12.5px',
                  fontFamily: 'var(--font-family)',
                  cursor: 'pointer',
                  outline: 'none'
                }}
              >
                {PERSIAN_MONTH_NAMES.map((m, idx) => (
                  <option key={idx} value={idx + 1}>
                    {m}
                  </option>
                ))}
              </select>

              <select
                value={viewYear}
                onChange={e => setViewYear(Number(e.target.value))}
                style={{
                  background: 'var(--bg-sidebar, #0f172a)',
                  border: '1px solid var(--border-color, #1e293b)',
                  borderRadius: '6px',
                  color: 'var(--text-main)',
                  padding: '3px 8px',
                  fontSize: '12.5px',
                  fontFamily: 'var(--font-family)',
                  cursor: 'pointer',
                  outline: 'none'
                }}
              >
                {yearOptions.map(y => (
                  <option key={y} value={y}>
                    {toPersianDigits(y)}
                  </option>
                ))}
              </select>
            </div>

            {/* Next month arrow */}
            <button
              type="button"
              onClick={handleNextMonth}
              title="ماه بعد"
              className="btn-icon"
              style={{
                background: 'rgba(255, 255, 255, 0.05)',
                border: '1px solid var(--border-color, #1e293b)',
                borderRadius: '6px',
                color: 'var(--text-main)',
                width: '28px',
                height: '28px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                cursor: 'pointer'
              }}
            >
              <ChevronLeft size={16} />
            </button>
          </div>

          {/* Weekday headers (شنبه تا جمعه) */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(7, 1fr)',
              textAlign: 'center',
              fontSize: '12px',
              fontWeight: 600,
              color: 'var(--text-muted, #94a3b8)',
              marginBottom: '8px',
              paddingBottom: '6px',
              borderBottom: '1px solid var(--border-color, #1e293b)'
            }}
          >
            {PERSIAN_WEEKDAY_SHORT.map((w, idx) => (
              <div
                key={idx}
                style={{
                  color: idx === 6 ? '#f87171' : 'var(--text-muted, #94a3b8)'
                }}
              >
                {w}
              </div>
            ))}
          </div>

          {/* Days Grid */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(7, 1fr)',
              gap: '3px'
            }}
          >
            {/* Trailing days of previous month */}
            {prevMonthDays.map((d, idx) => (
              <div
                key={`prev-${idx}`}
                style={{
                  height: '32px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '12px',
                  color: 'rgba(148, 163, 184, 0.3)',
                  cursor: 'default'
                }}
              >
                {toPersianDigits(d)}
              </div>
            ))}

            {/* Current month days */}
            {currentMonthDays.map(d => {
              const isSelected =
                activeJalali &&
                activeJalali.jy === viewYear &&
                activeJalali.jm === viewMonth &&
                activeJalali.jd === d;

              const isToday =
                today.jy === viewYear && today.jm === viewMonth && today.jd === d;

              const dayOfWeek = (firstDayOfWeek + d - 1) % 7;
              const isFriday = dayOfWeek === 6;

              return (
                <button
                  key={`day-${d}`}
                  type="button"
                  onClick={() => handleSelectDay(d)}
                  style={{
                    height: '32px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: '12.5px',
                    fontWeight: isSelected || isToday ? 700 : 500,
                    borderRadius: '6px',
                    cursor: 'pointer',
                    border: isToday && !isSelected ? '1px solid var(--primary, #0284c7)' : '1px solid transparent',
                    background: isSelected
                      ? 'var(--primary, #0284c7)'
                      : 'transparent',
                    color: isSelected
                      ? '#ffffff'
                      : isFriday
                      ? '#f87171'
                      : 'var(--text-main, #f8fafc)',
                    transition: 'all 0.15s ease'
                  }}
                  onMouseEnter={e => {
                    if (!isSelected) {
                      e.currentTarget.style.backgroundColor = 'var(--bg-card-hover, #1e2942)';
                    }
                  }}
                  onMouseLeave={e => {
                    if (!isSelected) {
                      e.currentTarget.style.backgroundColor = 'transparent';
                    }
                  }}
                >
                  {toPersianDigits(d)}
                </button>
              );
            })}

            {/* Leading days of next month */}
            {nextMonthDays.map((d, idx) => (
              <div
                key={`next-${idx}`}
                style={{
                  height: '32px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '12px',
                  color: 'rgba(148, 163, 184, 0.3)',
                  cursor: 'default'
                }}
              >
                {toPersianDigits(d)}
              </div>
            ))}
          </div>

          {/* Footer with Today Action */}
          <div
            style={{
              marginTop: '12px',
              paddingTop: '8px',
              borderTop: '1px solid var(--border-color, #1e293b)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between'
            }}
          >
            <button
              type="button"
              onClick={handleGoToToday}
              style={{
                background: 'transparent',
                border: 'none',
                color: 'var(--primary, #0284c7)',
                fontSize: '12px',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '4px',
                padding: '2px 6px',
                borderRadius: '4px'
              }}
            >
              <CalendarCheck size={14} />
              <span>انتخاب امروز ({toPersianDigits(today.jd)} {PERSIAN_MONTH_NAMES[today.jm - 1]})</span>
            </button>

            <button
              type="button"
              onClick={() => setIsOpen(false)}
              style={{
                background: 'transparent',
                border: 'none',
                color: 'var(--text-muted, #94a3b8)',
                fontSize: '12px',
                cursor: 'pointer'
              }}
            >
              بستن
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
