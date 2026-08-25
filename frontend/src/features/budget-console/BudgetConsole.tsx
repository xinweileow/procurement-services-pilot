import { useEffect, useState } from 'react';
import { apiRequest } from '../../api/client';

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
      <h2>Budget Console</h2>

      <div>
        <label htmlFor="fySelect">Fiscal Year</label>
        <select id="fySelect" value={fiscalYear} onChange={(e) => setFiscalYear(Number(e.target.value))}>
          <option value={2024}>FY2024</option>
          <option value={2025}>FY2025</option>
          <option value={2026}>FY2026</option>
          <option value={2027}>FY2027</option>
        </select>
      </div>

      {data && (
        <>
          <div style={{ display: 'flex', gap: '1rem', marginTop: '1rem' }}>
            <div style={{ border: '1px solid #ccc', padding: '1rem', borderRadius: '8px' }}>
              <strong>Allocated Budget</strong>
              <div>MYR {data.allocated.toLocaleString()}</div>
            </div>
            <div style={{ border: '1px solid #ccc', padding: '1rem', borderRadius: '8px' }}>
              <strong>Reserved</strong>
              <div>MYR {data.reserved.toLocaleString()}</div>
            </div>
            <div style={{ border: '1px solid #ccc', padding: '1rem', borderRadius: '8px' }}>
              <strong>Actual Spend</strong>
              <div>MYR {data.actualSpend.toLocaleString()}</div>
            </div>
            <div style={{ border: '1px solid #ccc', padding: '1rem', borderRadius: '8px' }}>
              <strong>Available Budget</strong>
              <div>MYR {data.available.toLocaleString()}</div>
            </div>
          </div>

          <p style={{ marginTop: '1rem', fontStyle: 'italic' }}>
            Utilisation: {data.utilisationPercentage}% (formula: (Reserved + Actual Spend) ÷ Allocated × 100)
          </p>

          <h3 style={{ marginTop: '1.5rem' }}>Cost Centre Breakdown</h3>
          <table>
            <thead>
              <tr>
                <th>Cost Centre</th>
                <th>Capex Balance (MYR)</th>
                <th>Opex Balance (MYR)</th>
                <th>Utilisation (%)</th>
              </tr>
            </thead>
            <tbody>
              {data.byCostCentre.map((cc) => (
                <tr key={cc.costCentre}>
                  <td>{cc.costCentre}</td>
                  <td>{cc.capexBalance.toLocaleString()}</td>
                  <td>{cc.opexBalance.toLocaleString()}</td>
                  <td>{cc.utilisationPercentage}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
