import { useState } from 'react';
import { apiRequest } from '../../api/client';
import type { CatalogueItemResponse } from '../../types/requests';
import { Card, StatusMessage, inputClass, labelClass, tableClass, theadClass, thClass, trClass, tdClass, TableWrap, ButtonPrimary, ButtonGhost } from '../../components/ui';

interface Props {
  onSelectNonCatalogue: () => void;
}

export function CatalogueSearch({ onSelectNonCatalogue }: Props) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<CatalogueItemResponse[]>([]);
  const [searched, setSearched] = useState(false);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    const items = await apiRequest<CatalogueItemResponse[]>(`/catalogue/items?query=${encodeURIComponent(query)}`);
    setResults(items);
    setSearched(true);
  };

  return (
    <Card title="Catalogue Search">
      <p className="text-sm text-ink-muted mb-4">
        Search whether your intended item is available in the approved catalogue before creating a non-catalogue request.
      </p>

      <form onSubmit={handleSearch} className="flex flex-wrap items-end gap-3 mb-4">
        <div className="flex-1 min-w-[240px]">
          <label htmlFor="catalogueQueryInput" className={labelClass}>
            Search catalogue item
          </label>
          <input
            id="catalogueQueryInput"
            type="text"
            className={inputClass}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search by item, service, keyword or supplier"
          />
        </div>
        <ButtonPrimary type="submit" className="py-2 px-4">
          Search
        </ButtonPrimary>
      </form>

      {searched && results.length > 0 && (
        <div role="status" aria-live="polite" className="mb-4">
          <StatusMessage tone="info">
            Catalogue item found. Please proceed using the catalogue purchase flow instead of non-catalogue intake.
          </StatusMessage>
          <TableWrap>
            <table className={tableClass}>
              <thead className={theadClass}>
                <tr className={trClass}>
                  <th className={thClass}>Item</th>
                  <th className={thClass}>Category</th>
                  <th className={thClass}>Supplier</th>
                </tr>
              </thead>
              <tbody>
                {results.map((r, i) => (
                  <tr key={i} className={trClass}>
                    <td className={tdClass}>{r.name}</td>
                    <td className={tdClass}>{r.category}</td>
                    <td className={tdClass}>{r.supplier}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableWrap>
        </div>
      )}

      {searched && results.length === 0 && (
        <div role="status" className="mb-4">
          <StatusMessage tone="neutral">No matching catalogue item found. You may proceed as a Non-Catalogue Request.</StatusMessage>
        </div>
      )}

      <ButtonGhost type="button" onClick={onSelectNonCatalogue} className="py-2 px-4">
        Item not available - proceed as Non-Catalogue
      </ButtonGhost>
    </Card>
  );
}
