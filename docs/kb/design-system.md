# Design System — Etiqa Finance System (ITRMS) reference

Procurement is a module inside the same Etiqa Finance System (internal codename **ITRMS**, `eisp-silver.etiqa.com/itrms/*`) that Budget, Requisition Finalisation, Approval Inbox etc. already ship in. This doc is the literal, pixel-level reference for that system, extracted from three saved production pages checked into `docs/reference/`:

- `docs/reference/Finance System.html` — Dashboard (`/itrms/dashboard`)
- `docs/reference/Finance -Budget.html` — Budget Console (`/itrms/budget`)
- `docs/reference/Finance - Requisition Finalisation.html` — Finance Commitment Console (`/itrms/finance-console`)

These are full "Save As → Webpage, Complete" captures with the built Tailwind stylesheet inline, so class names and computed colors below are read directly off the shipped CSS, not guessed. **This supersedes the placeholder `--bg`/`--panel`/`--accent` palette described in `ui_ux.md`'s "From the eProcurement Prototype" section** wherever the two conflict — that prototype is a design mockup that was never built against this stack; the pages here are what's actually in production. The prototype is still the only source for wizard-specific components (stepper, evidence upload, callout, tooltip) that don't appear in these three pages — see `ui_ux.md` for those.

The procurement frontend (`frontend/`) must be re-skinned to match this system exactly, not to a generic Vite scaffold — a Procurement user should not be able to tell, from the shell alone, that they've left the Finance System.

## Stack

- **Tailwind CSS v3.4.x** utility classes (`@import url(https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap)` + Tailwind preflight + generated utilities). No component library (no MUI/Chakra) — everything is hand-built Tailwind + `lucide` icons.
- **Font:** Inter (400/500/600/700/800 weights), loaded from Google Fonts.
- **Icons:** [lucide](https://lucide.dev) SVGs (`lucide-react` is the idiomatic React equivalent), stroke-width 2, typically 14–18px in content, 16–18px in nav.
- Numeric values (amounts, percentages, IDs) use the `tabular-nums` utility so columns of numbers align.

## Color tokens

Extend the frontend's Tailwind theme with these exact colors (all read from the shipped `.bg-*`/`.text-*`/`.border-*` rules). Use kebab-case token names so `bg-canvas`, `text-ink-muted`, `border-line`, etc. work as literal Tailwind classes, matching the reference markup 1:1.

| Token | Hex | Usage |
|---|---|---|
| `canvas` | `#F9F9F9` | page background (outside cards) |
| `canvas-subtle` | `#F1F4F8` | subtle fills — icon chips, note banners, progress-bar track, table hover |
| `surface` | `#FFFFFF` | card/panel background |
| `surface-2` | `#FAFBFC` | table header background |
| `ink` | `#0F172A` | primary text |
| `ink-muted` | `#64748B` | secondary text, labels, table header text |
| `ink-subtle` | `#94A3B8` | placeholder text, chart axis labels |
| `ink-inverse` | `#0F172A` | text on the accent-colored active pagination pill (dark text on yellow, not white) |
| `line` | `#E2E8F0` | default borders/dividers |
| `line-strong` | *(darker than `line`, hover state)* | input/card hover border — approximate `#CBD5E1` if not otherwise specified |
| `accent` | `#FFD200` | Etiqa brand yellow — active nav item, active tab underline, primary metric accent bar, active pagination pill, links (`text-accent`) |
| `accent-hover` | `#F5C400` | hover state for accent text/links |
| `accent-soft` | `#FFF6CC` | accent tint background |
| `info` | `#2563EB` | blue — informational metric accent, "Reserved" figures |
| `info-soft` | `#DBEAFE` | blue tint background (e.g. quick-action tile icon chips) |
| `success` | `#16A34A` | green — positive status, "Available Budget", Finalise button |
| `success-soft` | `#DCFCE7` | green tint background |
| `warning` | `#F59E0B` | amber — caution status, "Actual Spend" |
| `warning-soft` | `#FEF3C7` | amber tint background |
| `danger` | `#DC2626` | red — negative/error status |
| `danger-soft` | `#FEE2E2` | red tint background |

**Sidebar (dark, separate palette — not derived from the tokens above):**

| Token | Value | Usage |
|---|---|---|
| sidebar gradient | `linear-gradient(rgb(11,18,32) 0%, rgb(17,26,46) 100%)` | sidebar background |
| `sidebar-text` | `#CBD5E1` | default nav item text |
| `sidebar-muted` | `#7C8AA3` | section labels, secondary sidebar text |
| active nav item | `text-[#FFE166]` on `bg-gradient-to-r from-[rgba(255,210,0,0.16)] to-[rgba(255,210,0,0.03)]`, plus a 3px accent-colored left rail (`before:` pseudo-element) | current-page nav highlight |
| hover nav item | `hover:bg-white/[0.06] hover:text-white` | |

Status badges (pills) reuse the soft/solid token pairs above by convention: neutral/awaiting states use `bg-canvas-subtle text-ink-muted`; success uses `bg-success text-white` (solid, e.g. the Finalise button) or `bg-success-soft text-success` (soft pill, for status text); same pattern for warning/danger.

## Layout shell

**Sidebar** — fixed, `w-[260px]`, full viewport height, dark gradient background, `text-white` base:
- Header block: app logo image + `pl-3 border-l border-white/10` divider + two-line title (`Finance System` bold 13px / subtitle 11px muted) — swap for `Procurement` / a procurement-specific subtitle.
- Collapse toggle button (chevrons-left icon) directly under the header.
- Scrollable `<nav>` with grouped sections, each a `<p>` uppercase 11px tracked label (`Workspace`, `Finance`, `Administration`, `ePV` in the reference) followed by `<a>`/`<button>` nav items: icon (18px lucide) + label, `px-3 py-2 rounded-md text-[13px] font-medium`. Items with sub-navigation render as a `<button>` with a trailing `chevron-right` instead of an `<a>`.
- Footer block (`border-t border-white/10`): user identity chip (initial-letter avatar circle, `bg-gradient-to-br from-accent to-[#F5A623]`, employee ID + role) linking to profile, plus a Sign Out button below it.
- A **Procurement** section belongs in this same sidebar (as its own grouped section, sibling to Workspace/Finance/Administration), not as a separate top-level app shell — the user reaches procurement screens without leaving this nav.

**Topbar** — `h-16 sticky top-0`, `border-b border-line`, page `<h1>` (17px semibold) on the left, a right-aligned icon-button cluster (User Guide `circle-help`, Notifications `bell` with unread dot) each `bg-canvas-subtle border border-transparent rounded-md p-2`, hover → `bg-surface border-line`.

**Main content** — `ml-[260px]` (offset for the fixed sidebar), `p-7` (`max-md:p-4`), page content wrapped in `animate-rise space-y-5`. Page heading pattern repeats the topbar title as a larger `<h1>` (22px bold) with an optional muted subtitle paragraph directly under it, `mb-5`.

## Component patterns

**Metric card** (two variants seen):
1. Dashboard style — plain `bg-surface border border-line rounded-md px-4 py-3`: uppercase 11px muted label, then a `text-xl font-bold tabular-nums` value, optional muted caption line.
2. Budget-console style (richer) — `bg-surface border border-line rounded-lg p-5 shadow-xs hover:shadow-md hover:-translate-y-px transition-all`, a 3px colored accent bar pinned to the left edge (`absolute left-0 top-3 bottom-3 w-[3px] rounded-r bg-{accent|info|warning|success}`), content padded `pl-2`: uppercase 11px label, `text-3xl font-bold tabular-nums` value (color-matched to the accent, e.g. `text-[#166534]` for a green card), muted caption, and a `w-9 h-9 rounded-md bg-canvas-subtle` icon chip on the right.

**Card / panel** (the workhorse container — dashboard tiles, tables, chart panels all sit inside one): `bg-surface border border-line rounded-lg shadow-xs hover:shadow-sm transition-shadow`. Header row: `flex items-center justify-between px-[1.15rem] py-[0.9rem] border-b border-line text-[0.9375rem] font-semibold`, with an inline lucide icon (14px) before the title and an optional right-aligned "see more" link (`text-xs text-ink-muted hover:text-accent-hover`, trailing `arrow-right` icon). Body: `p-[1.15rem]`.

**Quick-action tile** (dashboard shortcuts row): `<a>` styled `flex items-center gap-2.5 px-4 py-3 bg-surface border border-line rounded-md hover:border-info hover:shadow-sm`, an icon chip (`w-9 h-9 rounded-md bg-info-soft text-info`) + title/subtitle stack, title turns `text-info` on hover via `group-hover`.

**Tabs**: `flex items-center gap-1 border-b border-line`, each tab a `<button>` `inline-flex items-center gap-1.5 px-3.5 py-2 text-[0.8125rem] font-medium border-b-2 -mb-px transition-colors`; active = `border-accent text-ink`, inactive = `border-transparent text-ink-muted hover:text-ink`. Icon (14px) precedes the label.

**Table**: `w-full text-sm text-ink align-middle` inside an `overflow-x-auto` wrapper. `<thead>` on `bg-surface-2`; `<th>` = `text-left px-4 py-3 text-2xs font-semibold uppercase tracking-wider text-ink-muted border-b border-line whitespace-nowrap` (numeric columns add `text-right`). `<tr>` = `border-b border-line last:border-0 hover:bg-canvas-subtle transition-colors`; `<td>` = `px-4 py-3`. A secondary detail line under a primary cell value uses `text-2xs text-ink-muted` (e.g. reference code + "Step 2/4" under a request title).

**Status pill/badge**: `inline-flex items-center font-medium text-2xs px-[0.6rem] py-[0.3rem] rounded-full tracking-wide`, neutral default `bg-canvas-subtle text-ink-muted`; count badges (e.g. "Ready to Finalise · 1") are circular: `inline-flex items-center justify-center min-w-[22px] h-5 px-1.5 text-2xs font-bold rounded-full bg-warning text-ink`.

**Buttons** (row actions, seen on requisition cards): ghost/outline default `border border-line rounded px-2 py-1.5 text-2xs font-medium hover:bg-surface-raised`; primary/affirmative `bg-success text-white hover:bg-success/90`; both support `disabled:opacity-50`. A leading 12px icon is standard.

**Filter row**: `flex flex-wrap items-end gap-3`, each filter a labeled `<select>`/`<input>` — label `text-2xs text-ink-muted block mb-1`, control `block w-full text-sm bg-surface border border-line rounded-md px-3 py-2 hover:border-line-strong focus:border-accent focus:outline-none focus-visible:shadow-ring disabled:bg-canvas-subtle`. Search inputs add a leading `search` icon absolutely positioned inside the field (`pl-8`).

**Pagination**: `<nav aria-label="Pagination">` with `Previous page`/`Next page` icon buttons (`chevron-left`/`chevron-right`, disabled via native `disabled` + `aria-label`) and numbered page buttons; current page = `bg-accent text-ink-inverse font-semibold`, others = `text-ink-muted hover:bg-surface-raised hover:text-ink`. Always paired with a page-size `<select>` (20/50/100) on the left and a "Showing X to Y of Z entries" caption on the right, in a 3-column `grid-cols-[1fr_auto_1fr]` footer row.

**Progress / utilisation bar**: `h-2` (or `h-2.5`) `bg-canvas-subtle rounded-full overflow-hidden` track, filled `h-full rounded-full` inner div with inline `width: N%` and an inline `background` color chosen by threshold (green when healthy, amber/red as utilisation rises — exact thresholds aren't captured in the reference markup, treat as an open question for the procurement budget-linkage screens).

**Line/area chart**: hand-rolled inline `<svg>` (no charting library) — gridlines + axis labels drawn as `<line>`/`<text>`, series as `<polyline>` (dashed for planned/reserved, solid for actuals), with a gradient-filled `<polygon>` under the actuals line and a text legend below using small colored line/swatch icons. Reasonable to replace with a lightweight charting lib in the real implementation as long as the visual language (thin lines, muted gridlines, `tabular-nums` axis labels, legend-under-chart) matches.

## Responsive behavior

- Metric/tile grids: `grid-cols-1` → `sm:grid-cols-2` → `lg:grid-cols-4` (dashboard quick actions) or `xl:grid-cols-4` (budget metrics).
- Two-column detail grids collapse to one column below `lg`.
- Topbar/main padding drops from `p-7`/`px-7` to `p-4`/`px-4` at `max-md`.

## Applying this to Procurement

1. Add Tailwind CSS v3 to `frontend/` (not currently configured — see `frontend/src/index.css`, still the default Vite scaffold) and register the color tokens above as theme extensions.
2. Load Inter the same way (Google Fonts `@import`/`<link>`), and switch to `lucide-react` for icons.
3. Rebuild the app shell (`App.tsx`) as sidebar + topbar + main, matching the structure above, with a **Procurement** nav section (Dashboard, Intake/New Request, Requisitions, Approval Inbox, Procurement Triage, Supplier Status, Due Diligence, Budget Console, Requisition Finalisation — i.e. the existing `View` union in `App.tsx`) sitting alongside where Workspace/Finance/Administration/ePV would be in the full Finance System, since procurement is reached from the same shell.
4. Re-skin each feature screen to the card/table/tab/badge/pagination/filter patterns above as its ticket comes up — don't do a one-shot mass rewrite of every feature file; match this system incrementally the way any other loop-eng ticket would, but never introduce a competing visual language.
