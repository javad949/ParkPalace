import React, { useEffect, useState } from 'react';
import { fetchDirectoryContacts, fetchSepidarParties } from '../../services/api';
import { DirectoryPartyListItem } from '../../types';

type DirectoryMode = 'sepidarParties' | 'contacts';

interface DirectoryRow {
  id: string;
  title: string;
  detail?: string;
}

export const DirectoryView: React.FC<{ mode: DirectoryMode; devRole: string }> = ({ mode, devRole }) => {
  const [rows, setRows] = useState<DirectoryRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    const load = mode === 'sepidarParties'
      ? fetchSepidarParties({ page: 1, pageSize: 50 }, devRole).then((page) =>
          page.items.map((item: DirectoryPartyListItem) => ({
            id: item.id,
            title: item.displayName || item.crmTitle || item.sepidarTitle || 'بدون نام',
            detail: item.sepidarCode,
          })))
      : fetchDirectoryContacts('', devRole).then((data: unknown) => {
          const list = Array.isArray(data) ? data : [];
          return list.map((item: { id?: string; personName?: string; displayName?: string; phone?: string }, index: number) => ({
            id: item.id || String(index),
            title: item.personName || item.displayName || 'بدون نام',
            detail: item.phone,
          }));
        });

    load
      .then((items) => {
        if (!cancelled) setRows(items);
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'بارگذاری انجام نشد.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [mode, devRole]);

  const title = mode === 'sepidarParties' ? 'طرف‌حساب‌های سپیدار' : 'مخاطبان';

  return (
    <section style={{ padding: 16 }}>
      <h2 style={{ marginTop: 0 }}>{title}</h2>
      {loading && <p>در حال بارگذاری...</p>}
      {error && <p role="alert">{error}</p>}
      {!loading && !error && rows.length === 0 && <p>موردی ثبت نشده است.</p>}
      {!loading && !error && rows.length > 0 && (
        <ul>
          {rows.map((row) => (
            <li key={row.id}>
              {row.title}
              {row.detail ? ` — ${row.detail}` : ''}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
};
