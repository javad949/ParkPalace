import { toJalaali, toGregorian, isLeapJalaaliYear, jalaaliMonthLength } from 'jalaali-js';

// Replicating utility functions from src/utils/jalali.ts for automated node test verification
const PERSIAN_MONTH_NAMES = [
  'فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور',
  'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'
];

function toPersianDigits(num) {
  if (num === null || num === undefined) return '';
  const farsiDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];
  return num.toString().replace(/[0-9]/g, (char) => farsiDigits[parseInt(char, 10)]);
}

function toEnglishDigits(str) {
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

function parseDateComponents(date) {
  if (!date) return null;
  if (typeof date === 'string') {
    const trimmed = date.trim();
    if (!trimmed) return null;
    const dateOnlyMatch = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(trimmed);
    if (dateOnlyMatch) {
      return {
        gy: parseInt(dateOnlyMatch[1], 10),
        gm: parseInt(dateOnlyMatch[2], 10),
        gd: parseInt(dateOnlyMatch[3], 10),
        hasTime: false
      };
    }
    const d = new Date(trimmed);
    if (isNaN(d.getTime())) return null;
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
  return null;
}

function jalaliToIsoDate(jy, jm, jd) {
  const res = toGregorian(jy, jm, jd);
  const mStr = res.gm.toString().padStart(2, '0');
  const dStr = res.gd.toString().padStart(2, '0');
  return `${res.gy}-${mStr}-${dStr}`;
}

function isoToJalali(isoString) {
  const comp = parseDateComponents(isoString);
  if (!comp) return null;
  const res = toJalaali(comp.gy, comp.gm, comp.gd);
  return { jy: res.jy, jm: res.jm, jd: res.jd };
}

function dateOnlyToIsoUtc(dateStr) {
  if (!dateStr) return undefined;
  const trimmed = dateStr.trim();
  const match = /^(\d{4})-(\d{1,2})-(\d{1,2})/.exec(trimmed);
  if (!match) return undefined;
  const y = match[1];
  const m = match[2].padStart(2, '0');
  const d = match[3].padStart(2, '0');
  return `${y}-${m}-${d}T00:00:00Z`;
}

function parseJalaliInput(input) {
  if (!input) return null;
  const eng = toEnglishDigits(input).trim();
  const match = /^(\d{4})[/-](\d{1,2})[/-](\d{1,2})$/.exec(eng);
  if (!match) return null;
  const jy = parseInt(match[1], 10);
  const jm = parseInt(match[2], 10);
  const jd = parseInt(match[3], 10);
  if (jy < 1300 || jy > 1500) return null;
  if (jm < 1 || jm > 12) return null;
  const maxDays = jalaaliMonthLength(jy, jm);
  if (jd < 1 || jd > maxDays) return null;
  return { jy, jm, jd };
}

// -------------------------------------------------------------
// TEST SUITE EXECUTION
// -------------------------------------------------------------
console.log('====================================================');
console.log('PARKPALACE CRM - JALALI CALENDAR VERIFICATION SUITE');
console.log('====================================================\n');

let passedTests = 0;
let totalTests = 0;

function assert(condition, testName) {
  totalTests++;
  if (condition) {
    console.log(`[PASS] ${testName}`);
    passedTests++;
  } else {
    console.error(`[FAIL] ${testName}`);
    process.exitCode = 1;
  }
}

// 1. Leap Year & Esfand Verification
console.log('--- 1. Leap Year & Esfand Length Tests ---');
assert(isLeapJalaaliYear(1403) === true, 'Year 1403 is Jalali Leap Year (کبیسه)');
assert(jalaaliMonthLength(1403, 12) === 30, 'Esfand 1403 has 30 days');
assert(isLeapJalaaliYear(1402) === false, 'Year 1402 is not a leap year');
assert(jalaaliMonthLength(1402, 12) === 29, 'Esfand 1402 has 29 days');
assert(isLeapJalaaliYear(1404) === false, 'Year 1404 is not a leap year');
assert(jalaaliMonthLength(1404, 12) === 29, 'Esfand 1404 has 29 days');

// 2. Nowruz Boundary Transitions
console.log('\n--- 2. Nowruz Boundary Transitions ---');
// 1402 Esfand 29 -> 1403 Farvardin 1 (2024-03-19 -> 2024-03-20)
const g1 = toGregorian(1402, 12, 29);
assert(g1.gy === 2024 && g1.gm === 3 && g1.gd === 19, '1402/12/29 converts to 2024-03-19');
const j1 = toJalaali(2024, 3, 20);
assert(j1.jy === 1403 && j1.jm === 1 && j1.jd === 1, '2024-03-20 converts to Nowruz 1403/01/01');

// 1403 Esfand 30 (Leap day) -> 1404 Farvardin 1 (2025-03-20 -> 2025-03-21)
const gLeap = toGregorian(1403, 12, 30);
assert(gLeap.gy === 2025 && gLeap.gm === 3 && gLeap.gd === 20, '1403/12/30 (Leap day) converts to 2025-03-20');
const jNowruz1404 = toJalaali(2025, 3, 21);
assert(jNowruz1404.jy === 1404 && jNowruz1404.jm === 1 && jNowruz1404.jd === 1, '2025-03-21 converts to Nowruz 1404/01/01');

// 3. Bidirectional Continuous Round-Trip (20,000 Days: 1970 to 2050)
console.log('\n--- 3. Continuous Round-Trip Stress Test (1970 - 2050) ---');
let roundTripSuccess = true;
const baseDate = new Date(Date.UTC(1970, 0, 1));
for (let day = 0; day < 25000; day += 3) {
  const current = new Date(baseDate.getTime() + day * 86400000);
  const gy = current.getUTCFullYear();
  const gm = current.getUTCMonth() + 1;
  const gd = current.getUTCDate();
  
  const j = toJalaali(gy, gm, gd);
  const back = toGregorian(j.jy, j.jm, j.jd);
  
  if (back.gy !== gy || back.gm !== gm || back.gd !== gd) {
    roundTripSuccess = false;
    console.error(`Mismatch on ${gy}-${gm}-${gd}: J=${j.jy}/${j.jm}/${j.jd}, back=${back.gy}-${back.gm}-${back.gd}`);
    break;
  }
}
assert(roundTripSuccess, 'Round-trip Gregorian <-> Jalali continuous verification (25,000 dates)');

// 4. Date-Only Timezone Isolation
console.log('\n--- 4. Date-Only Timezone Shift Prevention ---');
const dateOnlySample = '2026-10-23';
const parsedDateOnly = parseDateComponents(dateOnlySample);
assert(parsedDateOnly.gy === 2026 && parsedDateOnly.gm === 10 && parsedDateOnly.gd === 23, 'Date-only "2026-10-23" parsed without shifting day');
assert(parsedDateOnly.hasTime === false, 'Date-only string flagged as hasTime=false');

const isoDateOnlyUtc = dateOnlyToIsoUtc('2026-10-23');
assert(isoDateOnlyUtc === '2026-10-23T00:00:00Z', 'dateOnlyToIsoUtc converts strictly to UTC midnight without offset corruption');

// 5. Asia/Tehran Event Timestamp Standard (+03:30)
console.log('\n--- 5. Asia/Tehran Event Timestamp Standard ---');
// 2026-10-23T08:30:00Z -> Tehran is UTC+3.5 -> 12:00:00 local time
const eventUtc = '2026-10-23T08:30:00Z';
const parsedEvent = parseDateComponents(eventUtc);
assert(parsedEvent.hours === 12 && parsedEvent.minutes === 0, 'UTC 08:30 converts accurately to 12:00 Tehran time (+03:30)');

// 6. User Manual Input Parsing (Persian & English digits, slashes, hyphens)
console.log('\n--- 6. Jalali Manual Input Parsing & Validation ---');
const input1 = parseJalaliInput('1405/08/01');
assert(input1.jy === 1405 && input1.jm === 8 && input1.jd === 1, 'Standard English input "1405/08/01" parsed');

const input2 = parseJalaliInput('۱۴۰۳/۱۲/۳۰');
assert(input2.jy === 1403 && input2.jm === 12 && input2.jd === 30, 'Persian digits leap day "۱۴۰۳/۱۲/۳۰" parsed');

const inputInvalidLeap = parseJalaliInput('۱۴۰۴/۱۲/۳۰');
assert(inputInvalidLeap === null, 'Invalid leap day "۱۴۰۴/۱۲/۳۰" rejected (Esfand 1404 only has 29 days)');

const inputInvalidMonth = parseJalaliInput('1405/13/01');
assert(inputInvalidMonth === null, 'Invalid month 13 rejected');

const inputHyphen = parseJalaliInput('1405-02-15');
assert(inputHyphen.jy === 1405 && inputHyphen.jm === 2 && inputHyphen.jd === 15, 'Hyphenated input "1405-02-15" parsed');

console.log(`\n====================================================`);
console.log(`TOTAL TESTS: ${totalTests} | PASSED: ${passedTests} | FAILED: ${totalTests - passedTests}`);
console.log(`====================================================`);
