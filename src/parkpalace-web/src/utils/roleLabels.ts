const ROLE_TITLES_FA: Record<string, string> = {
  Admin: 'مدیر ارشد سیستم',
  Procurement: 'مسئول تدارکات',
  Warehouse: 'سرپرست انبار',
  Finance: 'امور مالی و حسابداری',
  Manager: 'مدیر ناظر بازرگانی',
  OrganizationManager: 'مدیر سازمان',
  None: 'بدون نقش',
};

export function roleTitleFa(role: string): string {
  return ROLE_TITLES_FA[role] ?? role;
}
