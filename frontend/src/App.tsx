import { useState } from 'react';
import { RequestorIntakeWizard } from './features/requestor-intake/RequestorIntakeWizard';
import { WorkspaceDashboard } from './features/workspace-dashboard/WorkspaceDashboard';
import { RequisitionsList } from './features/requisitions-list/RequisitionsList';
import { BudgetConsole } from './features/budget-console/BudgetConsole';
import { RequisitionFinalisation } from './features/requisition-finalisation/RequisitionFinalisation';
import { ApprovalInbox } from './features/approval-inbox/ApprovalInbox';
import { ProcurementTriage } from './features/procurement-triage/ProcurementTriage';

type View = 'dashboard' | 'intake' | 'requisitions' | 'budget' | 'finalisation' | 'inbox' | 'triage';

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
          <button type="button" onClick={() => setView('budget')}>Budget Console</button>
          <button type="button" onClick={() => setView('finalisation')}>Requisition Finalisation</button>
          <button type="button" onClick={() => setView('inbox')}>Approval Inbox</button>
          <button type="button" onClick={() => setView('triage')}>Procurement Triage</button>
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
        {view === 'budget' && <BudgetConsole />}
        {view === 'finalisation' && <RequisitionFinalisation />}
        {view === 'inbox' && <ApprovalInbox />}
        {view === 'triage' && <ProcurementTriage />}
      </main>
    </div>
  );
}

export default App;
