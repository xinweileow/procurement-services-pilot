import { useState } from 'react';
import { RequestorIntakeWizard } from './features/requestor-intake/RequestorIntakeWizard';
import { WorkspaceDashboard } from './features/workspace-dashboard/WorkspaceDashboard';
import { RequisitionsList } from './features/requisitions-list/RequisitionsList';

type View = 'dashboard' | 'intake' | 'requisitions';

function App() {
  const [view, setView] = useState<View>('dashboard');

  return (
    <div>
      <header>
        <h1>Etiqa Procurement System</h1>
        <nav aria-label="Main navigation">
          <button type="button" onClick={() => setView('dashboard')}>Dashboard</button>
          <button type="button" onClick={() => setView('intake')}>New Request (Intake)</button>
          <button type="button" onClick={() => setView('requisitions')}>Requisitions</button>
        </nav>
      </header>

      <main style={{ marginTop: '1.5rem' }}>
        {view === 'dashboard' && (
          <WorkspaceDashboard
            onNavigateToIntake={() => setView('intake')}
            onNavigateToRequisitions={() => setView('requisitions')}
          />
        )}
        {view === 'intake' && <RequestorIntakeWizard />}
        {view === 'requisitions' && <RequisitionsList />}
      </main>
    </div>
  );
}

export default App;
