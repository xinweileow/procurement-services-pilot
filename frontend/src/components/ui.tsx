import type { ReactNode } from 'react';

export function PageHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div className="flex items-start justify-between gap-4 flex-wrap mb-5">
      <div className="min-w-0">
        <h1 className="text-[1.375rem] font-bold leading-tight tracking-tight text-ink">{title}</h1>
        {subtitle && <p className="text-[0.8125rem] text-ink-muted mt-1">{subtitle}</p>}
      </div>
    </div>
  );
}

interface CardProps {
  title?: ReactNode;
  icon?: ReactNode;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function Card({ title, icon, action, children, className = '' }: CardProps) {
  return (
    <section className={`bg-surface border border-line rounded-lg shadow-xs hover:shadow-sm transition-shadow ${className}`}>
      {title && (
        <div className="flex items-center justify-between gap-3 px-[1.15rem] py-[0.9rem] border-b border-line text-[0.9375rem] font-semibold">
          <span className="inline-flex items-center gap-1.5 min-w-0 truncate">
            {icon}
            {title}
          </span>
          {action && <div className="flex items-center gap-2 flex-shrink-0">{action}</div>}
        </div>
      )}
      <div className="p-[1.15rem]">{children}</div>
    </section>
  );
}

type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'info';

const TONE_CLASSES: Record<Tone, string> = {
  neutral: 'bg-canvas-subtle text-ink-muted',
  success: 'bg-success-soft text-success',
  warning: 'bg-warning-soft text-warning',
  danger: 'bg-danger-soft text-danger',
  info: 'bg-info-soft text-info',
};

export function Badge({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span
      className={`inline-flex items-center font-medium text-2xs px-[0.6rem] py-[0.3rem] rounded-full tracking-wide whitespace-nowrap ${TONE_CLASSES[tone]}`}
    >
      {children}
    </span>
  );
}

export function StatusMessage({ tone = 'info', children }: { tone?: Tone; children: ReactNode }) {
  const toneClass: Record<Tone, string> = {
    neutral: 'bg-canvas-subtle text-ink-muted border-line',
    success: 'bg-success-soft text-success border-success/20',
    warning: 'bg-warning-soft text-warning border-warning/20',
    danger: 'bg-danger-soft text-danger border-danger/20',
    info: 'bg-info-soft text-info border-info/20',
  };
  return (
    <div className={`px-4 py-2.5 rounded-md border text-sm font-medium mb-4 ${toneClass[tone]}`}>{children}</div>
  );
}

export const inputClass =
  'block w-full text-sm text-ink bg-surface border border-line rounded-md px-3 py-2 transition-colors hover:border-line-strong placeholder:text-ink-subtle focus:border-accent focus:outline-none focus-visible:shadow-ring disabled:bg-canvas-subtle disabled:text-ink-muted disabled:cursor-not-allowed';

export const labelClass = 'text-2xs text-ink-muted block mb-1';

export const tableClass = 'w-full text-sm text-ink align-middle';
export const theadClass = 'bg-surface-2';
export const thClass =
  'text-left px-4 py-3 text-2xs font-semibold uppercase tracking-wider text-ink-muted border-b border-line whitespace-nowrap';
export const thRightClass = `${thClass} text-right`;
export const trClass = 'border-b border-line last:border-0 transition-colors hover:bg-canvas-subtle';
export const tdClass = 'px-4 py-3';
export const tdRightClass = `${tdClass} text-right tabular-nums`;

export function TableWrap({ children }: { children: ReactNode }) {
  return <div className="overflow-x-auto">{children}</div>;
}

const BTN_BASE =
  'inline-flex items-center justify-center gap-1.5 text-2xs font-medium rounded px-3 py-1.5 transition-colors disabled:opacity-50 disabled:cursor-not-allowed';

export function ButtonPrimary({ className = '', type = 'button', ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return <button type={type} {...props} className={`${BTN_BASE} bg-success text-white hover:bg-success/90 ${className}`} />;
}

export function ButtonGhost({ className = '', type = 'button', ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type={type}
      {...props}
      className={`${BTN_BASE} border border-line text-ink hover:bg-surface-raised ${className}`}
    />
  );
}

export function ButtonDanger({ className = '', type = 'button', ...props }: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return <button type={type} {...props} className={`${BTN_BASE} bg-danger text-white hover:bg-danger/90 ${className}`} />;
}
