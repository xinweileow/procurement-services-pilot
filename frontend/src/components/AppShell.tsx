import { useState } from 'react';
import type { LucideIcon } from 'lucide-react';
import { Bell, CircleHelp, ChevronsLeft, LogOut } from 'lucide-react';
import etiqaLogo from '../assets/etiqa-logo.png';

export interface NavItem<TView extends string> {
  id: TView;
  label: string;
  icon: LucideIcon;
}

export interface NavSection<TView extends string> {
  label: string;
  items: NavItem<TView>[];
}

interface AppShellProps<TView extends string> {
  sections: NavSection<TView>[];
  activeId: TView;
  onNavigate: (id: TView) => void;
  pageTitle: string;
  children: React.ReactNode;
}

const SIDEBAR_WIDTH = 260;
const SIDEBAR_WIDTH_COLLAPSED = 76;

export function AppShell<TView extends string>({
  sections,
  activeId,
  onNavigate,
  pageTitle,
  children,
}: AppShellProps<TView>) {
  const [collapsed, setCollapsed] = useState(false);
  const width = collapsed ? SIDEBAR_WIDTH_COLLAPSED : SIDEBAR_WIDTH;

  return (
    <div className="bg-canvas min-h-screen">
      <aside
        className="bg-sidebar-gradient fixed top-0 left-0 h-screen z-40 flex flex-col text-white transition-[width] duration-200 overflow-hidden"
        style={{ width }}
      >
        <div className="flex items-center px-5 py-[1.15rem] gap-3">
          {!collapsed && (
            <img
              src={etiqaLogo}
              alt="Etiqa Logo"
              className="w-auto object-contain flex-shrink-0 transition-all duration-200 h-6 max-w-[132px]"
            />
          )}
          {!collapsed && (
            <div className="pl-3 border-l border-white/10 min-w-0">
              <h2 className="text-[0.8125rem] font-bold text-white leading-tight tracking-tight truncate">
                Procurement
              </h2>
              <p className="text-2xs text-sidebar-muted truncate">Finance System</p>
            </div>
          )}
        </div>
        <button
          type="button"
          title="Toggle sidebar"
          onClick={() => setCollapsed((c) => !c)}
          className="mx-3 mb-1 px-2 py-1.5 flex items-center justify-center rounded-md text-sidebar-muted hover:bg-white/[0.06] hover:text-white transition-colors"
        >
          <ChevronsLeft size={16} className={collapsed ? 'rotate-180 transition-transform' : 'transition-transform'} />
        </button>
        <nav className="flex-1 overflow-y-auto overflow-x-hidden px-2.5 pt-1 pb-4" aria-label="Main navigation">
          {sections.map((section) => (
            <div key={section.label}>
              {!collapsed && (
                <p className="px-3 mt-3 mb-1.5 text-2xs font-semibold uppercase tracking-[0.08em] text-sidebar-muted truncate">
                  {section.label}
                </p>
              )}
              {section.items.map((item) => {
                const Icon = item.icon;
                const isActive = item.id === activeId;
                return (
                  <button
                    key={item.id}
                    type="button"
                    title={item.label}
                    onClick={() => onNavigate(item.id)}
                    aria-current={isActive ? 'page' : undefined}
                    className={
                      'relative w-full flex items-center gap-3 mb-0.5 px-3 py-2 rounded-md text-[0.8125rem] font-medium transition-colors text-left ' +
                      (isActive
                        ? 'text-[#FFE166] font-semibold bg-gradient-to-r from-[rgba(255,210,0,0.16)] to-[rgba(255,210,0,0.03)] before:content-[""] before:absolute before:left-[-10px] before:top-[7px] before:bottom-[7px] before:w-[3px] before:bg-accent before:rounded-r-[3px]'
                        : 'text-sidebar-text hover:bg-white/[0.06] hover:text-white')
                    }
                  >
                    <Icon size={18} className="flex-shrink-0" />
                    {!collapsed && <span className="truncate">{item.label}</span>}
                  </button>
                );
              })}
            </div>
          ))}
        </nav>
        <div className="border-t border-white/10 px-3 pt-3.5 pb-4">
          <div className="flex items-center gap-3 px-2 py-1.5 rounded-md text-white">
            <div className="w-[34px] h-[34px] rounded-full bg-gradient-to-br from-accent to-[#F5A623] text-ink flex items-center justify-center text-[0.8125rem] font-bold flex-shrink-0">
              P
            </div>
            {!collapsed && (
              <div className="flex-1 overflow-hidden min-w-0">
                <div className="text-[0.8125rem] font-semibold text-white truncate">Procurement User</div>
                <div className="text-2xs text-sidebar-muted truncate" title="Requestor">
                  Requestor
                </div>
              </div>
            )}
          </div>
          <button
            type="button"
            title="Sign Out"
            className={
              'mt-2 flex items-center gap-2 px-2 py-1.5 rounded-md text-[0.75rem] text-sidebar-muted hover:bg-white/[0.06] hover:text-white transition-colors ' +
              (collapsed ? 'justify-center w-full' : 'w-full')
            }
          >
            <LogOut size={13} />
            {!collapsed && <span>Sign Out</span>}
          </button>
        </div>
      </aside>

      <div className="flex flex-col min-h-screen transition-[margin-left] duration-200" style={{ marginLeft: width }}>
        <header className="itrms-topbar-blur h-16 sticky top-0 z-30 flex items-center justify-between px-7 max-md:px-4 border-b border-line">
          <h1 className="text-[1.0625rem] font-semibold text-ink tracking-tight m-0 truncate">
            {pageTitle}
          </h1>
          <div className="flex items-center gap-3">
            <button
              type="button"
              title="User Guide"
              className="bg-canvas-subtle text-ink-muted hover:bg-surface hover:border-line hover:text-ink border border-transparent rounded-md p-2 transition-colors"
            >
              <CircleHelp size={18} />
            </button>
            <button
              type="button"
              title="Notifications"
              className="bg-canvas-subtle text-ink-muted hover:bg-surface hover:border-line hover:text-ink border border-transparent rounded-md p-2 transition-colors"
            >
              <Bell size={18} />
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-auto p-7 max-md:p-4">
          <div className="animate-rise space-y-5">{children}</div>
        </main>
      </div>
    </div>
  );
}
