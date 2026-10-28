/**
 * ParkPalace CRM - Solar Hijri (Jalali / Shamsi) Calendar Engine
 * Accurate, bidirectional, astronomical conversion based on standard algorithm.
 * Preserves date-only ISO strings without timezone shifting.
 * Standardizes event timestamps to Asia/Tehran timezone (+03:30).
 */

import { toJalaali, toGregorian, isLeapJalaaliYear, jalaaliMonthLength } from 'jalaali-js';

export const PERSIAN_MONTH_NAMES = [
  'فروردین',
  'اردیبهشت',
  'خرداد',
  'تیر',
  'مرداد',
  'شهریور',
  'مهر',
  'آبان',
  'آذر',
  'دی',
  'بهمن',
  'اسفند'
] as const;

export const PERSIAN_WEEKDAY_NAMES = [
  'شنبه',
  'یکشنبه',
  'دوشنبه',
  'سه‌شنبه',
  'چهارشنبه',
  'پنج‌شنبه',
  'جمعه'
] as const;

export const PERSIAN_WEEKDAY_SHORT = [
  'ش',
  'ی',
  'د',
  'س',
  'چ',
  'پ',
  'ج'
] as const;

export interface JalaliDate {
  jy: number;
  jm: number;
  jd: number;
}

export interface GregorianDate {
  gy: number;
  gm: number;
  gd: number;
}

/**
 * Converts a Gregorian date (year, month 1-12, day 1-31) to Jalali.
 */
export function gregorianToJalali(gy: number, gm: number, gd: number): [number, number, number] {
  const res = toJalaali(gy, gm, gd);
  return [res.jy, res.jm, res.jd];
}

/**
 * Converts a Jalali date (year, month 1-12, day 1-31) to Gregorian.
 */
export function jalaliToGregorian(jy: number, jm: number, jd: number): [number, number, number] {
  const res = toGregorian(jy, jm, jd);
  return [res.gy, res.gm, res.gd];
}

/**
 * Returns true if the Jalali year is a leap year (کبیسه).
 */
export function isJalaliLeapYear(jy: number): boolean {
  return isLeapJalaaliYear(jy);
}

/**
 * Returns number of days in the specified Jalali month (1-12).
 * Months 1-6: 31 days
 * Months 7-11: 30 days
 * Month 12: 30 days if leap year, 29 days otherwise
 */
export function getJalaliMonthDays(jy: number, jm: number): number {
  return jalaaliMonthLength(jy, jm);
}

/**
 * Returns the weekday index for the 1st day of the given Jalali month.
 * 0 = شنبه (Saturday), 1 = یکشنبه, ..., 6 = جمعه (Friday).
 */
export function getJalaliMonthFirstDayOfWeek(jy: number, jm: number): number {
  const [gy, gm, gd] = jalaliToGregorian(jy, jm, 1);
  const d = new Date(Date.UTC(gy, gm - 1, gd));
  // JS getUTCDay(): 0 = Sunday, 1 = Monday, ..., 6 = Saturday
  // We want: 0 = Saturday, 1 = Sunday, ..., 6 = Friday
  return (d.getUTCDay() + 1) % 7;
}

/**
 * Converts English digits to Persian digits.
 */
export function toPersianDigits(num: number | string | null | undefined): string {
  if (num === null || num === undefined) return '';
  const farsiDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];
  return num
    .toString()
    .replace(/[0-9]/g, (char) => farsiDigits[parseInt(char, 10)]);
}

/**
 * Converts Persian digits to English digits.
 */
export function toEnglishDigits(str: string | null | undefined): string {
  if (!str) return '';
  const farsiDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];
  const arabicDigits = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];
  let res = str.toString();
  for (let i = 0; i < 10; i++) {
    res = res.replace(new RegExp(farsiDigits[i], 'g'), i.toString());
    res = res.replace(new RegExp(arabicDigits[i], 'g'), i.toString());
  }
  return res;
}

/**
 * Safely parses any date representation (date-only ISO "YYYY-MM-DD" or timestamp).
 * CRITICAL RULE: Date-only strings (no 'T') are parsed by calendar components
 * WITHOUT timezone shift, preserving the exact day intended by the user/database.
 */
export function parseDateComponents(date: Date | string | null | undefined): {
  gy: number;
  gm: number;
  gd: number;
  hours?: number;
  minutes?: number;
  hasTime: boolean;
} | null {
  if (!date) return null;

  if (typeof date === 'string') {
    const trimmed = date.trim();
    if (!trimmed) return null;

    // 1. Date-only pattern: "YYYY-MM-DD"
    const dateOnlyMatch = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(trimmed);
    if (dateOnlyMatch) {
      return {
        gy: parseInt(dateOnlyMatch[1], 10),
        gm: parseInt(dateOnlyMatch[2], 10),
        gd: parseInt(dateOnlyMatch[3], 10),
        hasTime: false
      };
    }

    // 2. ISO timestamp with time: convert to Asia/Tehran (+03:30)
    const d = new Date(trimmed);
    if (isNaN(d.getTime())) return null;

    // Convert UTC milliseconds to Asia/Tehran (+3.5 hours = +210 minutes)
    // Using UTC getters on shifted timestamp to prevent client OS local timezone discrepancies
    const tehranOffsetMs = 3.5 * 60 * 60 * 1000;
    const tehranDate = new Date(d.getTime() + tehranOffsetMs);

    return {
      gy: tehranDate.getUTCFullYear(),
      gm: tehranDate.getUTCMonth() + 1,
      gd: tehranDate.getUTCDate(),
      hours: tehranDate.getUTCHours(),
      minutes: tehranDate.getUTCMinutes(),
      hasTime: true
    };
  }

  if (date instanceof Date) {
    if (isNaN(date.getTime())) return null;
    return {
      gy: date.getFullYear(),
      gm: date.getMonth() + 1,
      gd: date.getDate(),
      hours: date.getHours(),
      minutes: date.getMinutes(),
      hasTime: true
    };
  }

  return null;
}

/**
 * Formats a date into Persian text: e.g. "۱ آبان ۱۴۰۵" or with time "۱ آبان ۱۴۰۵ - ۱۱:۴۷"
 */
export function formatJalali(date: Date | string | null | undefined, showTime: boolean = false): string {
  const comp = parseDateComponents(date);
  if (!comp) return '—';

  const [jy, jm, jd] = gregorianToJalali(comp.gy, comp.gm, comp.gd);
  const monthName = PERSIAN_MONTH_NAMES[jm - 1];

  const dateStr = `${toPersianDigits(jd)} ${monthName} ${toPersianDigits(jy)}`;

  if (showTime && comp.hasTime && comp.hours !== undefined && comp.minutes !== undefined) {
    const hours = toPersianDigits(comp.hours.toString().padStart(2, '0'));
    const minutes = toPersianDigits(comp.minutes.toString().padStart(2, '0'));
    return `${dateStr} - ساعت ${hours}:${minutes}`;
  }

  return dateStr;
}

/**
 * Formats a date into standard numeric Jalali string: e.g. "۱۴۰۵/۰۸/۰۱" or "1405/08/01"
 */
export function formatJalaliNumeric(
  date: Date | string | null | undefined,
  persianDigits: boolean = true
): string {
  const comp = parseDateComponents(date);
  if (!comp) return '';

  const [jy, jm, jd] = gregorianToJalali(comp.gy, comp.gm, comp.gd);
  const mStr = jm.toString().padStart(2, '0');
  const dStr = jd.toString().padStart(2, '0');
  const numeric = `${jy}/${mStr}/${dStr}`;

  return persianDigits ? toPersianDigits(numeric) : numeric;
}

/**
 * Formats an event timestamp with Tehran time: e.g. "۱ آبان ۱۴۰۵ - ۱۱:۴۷"
 */
export function formatJalaliDateTime(date: Date | string | null | undefined): string {
  return formatJalali(date, true);
}

/**
 * Formats a date into short Jalali string: e.g. "۱ آبان"
 */
export function formatJalaliShort(date: Date | string | null | undefined): string {
  const comp = parseDateComponents(date);
  if (!comp) return '—';

  const [, jm, jd] = gregorianToJalali(comp.gy, comp.gm, comp.gd);
  const monthName = PERSIAN_MONTH_NAMES[jm - 1];

  return `${toPersianDigits(jd)} ${monthName}`;
}

/**
 * Converts Jalali date components (jy, jm, jd) into Gregorian ISO "YYYY-MM-DD" string
 * for transmission to the backend API.
 */
export function jalaliToIsoDate(jy: number, jm: number, jd: number): string {
  const [gy, gm, gd] = jalaliToGregorian(jy, jm, jd);
  const mStr = gm.toString().padStart(2, '0');
  const dStr = gd.toString().padStart(2, '0');
  return `${gy}-${mStr}-${dStr}`;
}

/**
 * Converts a Gregorian ISO date string ("YYYY-MM-DD" or timestamp) to Jalali components { jy, jm, jd }.
 */
export function isoToJalali(isoString: string | null | undefined): JalaliDate | null {
  const comp = parseDateComponents(isoString);
  if (!comp) return null;
  const [jy, jm, jd] = gregorianToJalali(comp.gy, comp.gm, comp.gd);
  return { jy, jm, jd };
}

/**
 * Parses a user-entered Jalali string (e.g. "1405/08/01" or "۱۴۰۵-۸-۱") into JalaliDate.
 */
export function parseJalaliInput(input: string): JalaliDate | null {
  if (!input) return null;
  const eng = toEnglishDigits(input).trim();
  const match = /^(\d{4})[/-](\d{1,2})[/-](\d{1,2})$/.exec(eng);
  if (!match) return null;

  const jy = parseInt(match[1], 10);
  const jm = parseInt(match[2], 10);
  const jd = parseInt(match[3], 10);

  if (jy < 1300 || jy > 1500) return null;
  if (jm < 1 || jm > 12) return null;
  const maxDays = getJalaliMonthDays(jy, jm);
  if (jd < 1 || jd > maxDays) return null;

  return { jy, jm, jd };
}

/**
 * Returns today's Jalali date components.
 */
export function getTodayJalali(): JalaliDate {
  const now = new Date();
  const [jy, jm, jd] = gregorianToJalali(now.getFullYear(), now.getMonth() + 1, now.getDate());
  return { jy, jm, jd };
}

/**
 * Formats time in Asia/Tehran: e.g. "۱۱:۴۷"
 */
export function formatJalaliTime(date: Date | string | null | undefined): string {
  const comp = parseDateComponents(date);
  if (!comp || comp.hours === undefined || comp.minutes === undefined) return '—';
  const hours = toPersianDigits(comp.hours.toString().padStart(2, '0'));
  const minutes = toPersianDigits(comp.minutes.toString().padStart(2, '0'));
  return `${hours}:${minutes}`;
}

/**
 * Converts a date-only string ("YYYY-MM-DD") to an ISO 8601 UTC string ("YYYY-MM-DDT00:00:00Z")
 * safely without local timezone shifting.
 */
export function dateOnlyToIsoUtc(dateStr: string | null | undefined): string | undefined {
  if (!dateStr) return undefined;
  const trimmed = dateStr.trim();
  const match = /^(\d{4})-(\d{1,2})-(\d{1,2})/.exec(trimmed);
  if (!match) {
    const d = new Date(trimmed);
    return isNaN(d.getTime()) ? undefined : d.toISOString();
  }
  const y = match[1];
  const m = match[2].padStart(2, '0');
  const d = match[3].padStart(2, '0');
  return `${y}-${m}-${d}T00:00:00Z`;
}
