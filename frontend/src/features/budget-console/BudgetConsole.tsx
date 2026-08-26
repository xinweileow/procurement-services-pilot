import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';
import { PageHeader, Card, inputClass, labelClass, tableClass, theadClass, thClass, thRightClass, trClass, tdClass, tdRightClass, TableWrap } from '../../components/ui';

interface BudgetSummary {
  allocated: number;
  reserved: number;
  actualSpend: number;
  available: number;
  utilisationPercentage: number;
  byCostCentre: Array<{
    costCentre: string;
    capexBalance: number;
    opexBalance: number;
    utilisationPercentage: number;
  }>;
}

function formatMyr(value: number): string {
  return `MYR ${value.toLocaleString()}`;
}

const METRICS: Array<{ key: keyof BudgetSummary; label: string; accent: string; valueClass: string }> = [
  { key: 'allocated', label: 'Allocated Budget', accent: 'bg-accent', valueClass: 'text-ink' },
  { key: 'reserved', label: 'Reserved', accent: 'bg-info', valueClass: 'text-[#1E40AF]' },
  { key: 'actualSpend', label: 'Actual Spend', accent: 'bg-warning', valueClass: 'text-[#92400E]' },
  { key: 'available', label: 'Available Budget', accent: 'bg-success', valueClass: 'text-[#166534]' },
];

export function BudgetConsole() {
  const [fiscalYear, setFiscalYear] = useState<number>(2026);
  const [data, setData] = useState<BudgetSummary | null>(null);

  useEffect(() => {
    apiRequest<BudgetSummary>(`/budgets?fiscalYear=${fiscalYear}`)
      .then(setData)
      .catch(() => {});
  }, [fiscalYear]);

  return (
    <div>
      <PageHeader title="Budget Console" subtitle="Allocations, papers, transfers, FX, and yearly expenses." />

      <div className="mb-5 flex flex-wrap items-end gap-3">
        <div className="w-28">
          <label htmlFor="fySelect" className={labelClass}>
            Fiscal Year
          </label>
          <select id="fySelect" className={inputClass} value={fiscalYear} onChange={(e) => setFiscalYear(Number(e.target.value))}>
            <option value={2024}>FY2024</option>
            <option value={2025}>FY2025</option>
            <option value={2026}>FY2026</option>
            <option value={2027}>FY2027</option>
          </select>
        </div>
      </div>

      {data && (
        <div className="space-y-5">
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
            {METRICS.map((m) => (
              <div
                key={m.key}
                className="relative overflow-hidden bg-surface border border-line rounded-lg p-5 shadow-xs transition-all hover:shadow-md hover:-translate-y-px"
              >
                <span className={`absolute left-0 top-3 bottom-3 w-[3px] rounded-r ${m.accent}`} aria-hidden="true" />
                <div className="pl-2">
                  <div className="text-2xs font-semibold uppercase tracking-wider text-ink-muted mb-2">{m.label}</div>
                  <div className={`text-3xl font-bold leading-none tracking-tight tabular-nums ${m.valueClass}`}>
                    {formatMyr(data[m.key] as number)}
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div className="flex flex-wrap items-center gap-3 px-4 py-2.5 bg-canvas-subtle rounded-lg border border-line text-2xs text-ink-muted">
            <span className="font-medium text-ink">Note:</span>
            <span>Utilisation % = (Reserved + Actual Spend) ÷ Allocated × 100</span>
            <span className="ml-auto font-semibold text-ink tabular-nums">
              Utilisation: {data.utilisationPercentage}%
            </span>
          </div>

          <Card title="Cost Centre Breakdown">
            <TableWrap>
              <table className={tableClass}>
                <thead className={theadClass}>
                  <tr className={trClass}>
                    <th className={thClass}>Cost Centre</th>
                    <th className={thRightClass}>Capex Balance (MYR)</th>
                    <th className={thRightClass}>Opex Balance (MYR)</th>
                    <th className={thRightClass}>Utilisation (%)</th>
                  </tr>
                </thead>
                <tbody>
                  {data.byCostCentre.map((cc) => (
                    <tr key={cc.costCentre} className={trClass}>
                      <td className={`${tdClass} font-medium`}>{cc.costCentre}</td>
                      <td className={tdRightClass}>{cc.capexBalance.toLocaleString()}</td>
                      <td className={tdRightClass}>{cc.opexBalance.toLocaleString()}</td>
                      <td className={tdRightClass}>{cc.utilisationPercentage}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </TableWrap>
          </Card>
        </div>
      )}
    </div>
  );
}
