import type { ViewType } from '../types';

export type DrillParams = Record<string, string>;

/** Parse drill query string such as "filter=overdue" or "side=receivables". */
export function parseDrillQuery(query?: string | null): DrillParams {
  if (!query || !query.trim()) return {};
  try {
    const params = new URLSearchParams(query);
    const out: DrillParams = {};
    params.forEach((value, key) => {
      if (key) out[key] = value;
    });
    return out;
  } catch {
    return {};
  }
}

/**
 * Read drill params intended for the given view from URL (?view=&drill=) or sessionStorage.
 * Clears the one-shot sessionStorage entry after a matching consume so stale drills do not linger.
 * Leaves URL params intact so the applied filter remains visible/shareable.
 */
export function consumeDrillForView(view: ViewType): DrillParams {
  try {
    const url = new URL(window.location.href);
    const urlView = url.searchParams.get('view');
    const urlDrill = url.searchParams.get('drill');
    if (urlView === view && urlDrill) {
      return parseDrillQuery(urlDrill);
    }
  } catch { /* ignore */ }

  try {
    const raw = sessionStorage.getItem('parkpalace_drill');
    if (!raw) return {};
    const parsed = JSON.parse(raw) as { view?: string; query?: string };
    if (parsed.view === view && parsed.query) {
      sessionStorage.removeItem('parkpalace_drill');
      return parseDrillQuery(parsed.query);
    }
  } catch { /* ignore */ }

  return {};
}

export function drillBannerLabel(params: DrillParams): string | null {
  const filter = params.filter;
  const side = params.side;
  if (filter === 'overdue') return 'فیلتر از داشبورد تصمیم: سفارش‌های خرید سررسیدگذشته';
  if (filter === 'active') return 'فیلتر از داشبورد تصمیم: سفارش‌های خرید فعال (صادر / دریافت جزئی)';
  if (filter === 'critical') return 'فیلتر از داشبورد تصمیم: موجودی بحرانی';
  if (filter === 'open-exceptions') return 'فیلتر از داشبورد تصمیم: مغایرت‌های فاکتور باز';
  if (filter === 'overdue-followups') return 'فیلتر از داشبورد تصمیم: پیگیری‌های سررسیدگذشته';
  if (side === 'receivables') return 'تمرکز از داشبورد تصمیم: مطالبات تجاری';
  if (side === 'payables') return 'تمرکز از داشبورد تصمیم: بدهی تجاری';
  return filter || side ? `فیلتر از داشبورد تصمیم: ${filter || side}` : null;
}
