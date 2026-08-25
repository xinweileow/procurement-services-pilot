import { useState } from 'react';
import { apiRequest } from '../../api/client';
import type { CatalogueItemResponse } from '../../types/requests';

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
    <section>
      <h2>Catalogue Search</h2>
      <p>Search whether your intended item is available in the approved catalogue before creating a non-catalogue request.</p>

      <form onSubmit={handleSearch}>
        <label htmlFor="catalogueQueryInput">Search catalogue item</label>
        <input
          id="catalogueQueryInput"
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search by item, service, keyword or supplier"
        />
        <button type="submit">Search</button>
      </form>

      {searched && results.length > 0 && (
        <div role="status" aria-live="polite">
          <p>Catalogue item found. Please proceed using the catalogue purchase flow instead of non-catalogue intake.</p>
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Category</th>
                <th>Supplier</th>
              </tr>
            </thead>
            <tbody>
              {results.map((r, i) => (
                <tr key={i}>
                  <td>{r.name}</td>
                  <td>{r.category}</td>
                  <td>{r.supplier}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {searched && results.length === 0 && (
        <div role="status">
          <p>No matching catalogue item found. You may proceed as a Non-Catalogue Request.</p>
        </div>
      )}

      <div style={{ marginTop: '1rem' }}>
        <button type="button" onClick={onSelectNonCatalogue}>
          Item not available - proceed as Non-Catalogue
        </button>
      </div>
    </section>
  );
}
