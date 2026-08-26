import { useState } from 'react';
import {
  LayoutDashboard,
  FilePlus,
  Inbox,
  CircleCheck,
  ShieldCheck,
  Building2,
  BadgeCheck,
  DollarSign,
  Gavel,
} from 'lucide-react';
import { AppShell, type NavSection } from './components/AppShell';
import { RequestorIntakeWizard } from './features/requestor-intake/RequestorIntakeWizard';
import { WorkspaceDashboard } from './features/workspace-dashboard/WorkspaceDashboard';
import { RequisitionsList } from './features/requisitions-list/RequisitionsList';
import { BudgetConsole } from './features/budget-console/BudgetConsole';
import { RequisitionFinalisation } from './features/requisition-finalisation/RequisitionFinalisation';
import { ApprovalInbox } from './features/approval-inbox/ApprovalInbox';
import { ProcurementTriage } from './features/procurement-triage/ProcurementTriage';
import { SupplierStatus } from './features/supplier-status/SupplierStatus';
import { DueDiligenceStep } from './features/due-diligence/DueDiligenceStep';
import { RfxEvents } from './features/rfx-events/RfxEvents';

type View =
  | 'dashboard'
  | 'intake'
  | 'requisitions'
  | 'budget'
  | 'finalisation'
  | 'inbox'
  | 'triage'
  | 'supplier'
  | 'duediligence'
  | 'rfxevents';

const PAGE_TITLES: Record<View, string> = {
  dashboard: 'Dashboard',
  intake: 'New Request (Intake)',
  requisitions: 'Requisitions',
  budget: 'Budget Console',
  finalisation: 'Requisition Finalisation',
  inbox: 'Approval Inbox',
  triage: 'Procurement Triage',
  supplier: 'Supplier Status',
  duediligence: 'Due Diligence',
  rfxevents: 'RFx Events',
};

const NAV_SECTIONS: NavSection<View>[] = [
  {
    label: 'Workspace',
    items: [
      { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
      { id: 'intake', label: 'New Request', icon: FilePlus },
      { id: 'requisitions', label: 'Requisitions', icon: Inbox },
      { id: 'inbox', label: 'Approval Inbox', icon: CircleCheck },
    ],
  },
  {
    label: 'Procurement',
    items: [
      { id: 'triage', label: 'Procurement Triage', icon: ShieldCheck },
      { id: 'supplier', label: 'Supplier Status', icon: Building2 },
      { id: 'duediligence', label: 'Due Diligence', icon: BadgeCheck },
    ],
  },
  {
    label: 'Sourcing',
    items: [{ id: 'rfxevents', label: 'RFx Events', icon: Gavel }],
  },
  {
    label: 'Finance',
    items: [
      { id: 'budget', label: 'Budget Console', icon: DollarSign },
      { id: 'finalisation', label: 'Requisition Finalisation', icon: BadgeCheck },
    ],
  },
];

function App() {
  const [view, setView] = useState<View>('dashboard');

  return (
    <AppShell sections={NAV_SECTIONS} activeId={view} onNavigate={setView} pageTitle={PAGE_TITLES[view]}>
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
      {view === 'supplier' && <SupplierStatus />}
      {view === 'duediligence' && <DueDiligenceStep />}
      {view === 'rfxevents' && <RfxEvents />}
    </AppShell>
  );
}

export default App;
