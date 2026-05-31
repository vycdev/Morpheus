"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { CalendarDays, ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

const maxWindowDays = 3650;
const weekdayLabels = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
const quickWindows = [
  { label: "7 days", days: 7 },
  { label: "30 days", days: 30 },
  { label: "90 days", days: 90 },
  { label: "1 year", days: 365 },
  { label: "5 years", days: 1825 },
];

type DashboardDateRangePickerProps = {
  days: number;
  startDate: string;
  endDate: string;
};

export function DashboardDateRangePicker({
  days,
  startDate,
  endDate,
}: DashboardDateRangePickerProps) {
  const rootRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [pendingAnchor, setPendingAnchor] = useState<string | null>(null);
  const [range, setRange] = useState(() => clampRange(startDate, endDate));
  const [visibleMonth, setVisibleMonth] = useState(() => startOfMonth(parseDate(range.startDate)));

  useEffect(() => {
    const nextRange = clampRange(startDate, endDate);
    setRange(nextRange);
    setVisibleMonth(startOfMonth(parseDate(nextRange.startDate)));
    setPendingAnchor(null);
  }, [startDate, endDate, days]);

  useEffect(() => {
    if (!open) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  const rangeDays = inclusiveDays(range.startDate, range.endDate);
  const rangeLabel = `${formatShortDate(range.startDate)} - ${formatShortDate(range.endDate)}`;
  const todayString = todayDateString();
  const activeQuickWindow = quickWindows.find((quick) => quick.days === rangeDays && range.endDate === todayString);
  const rightMonth = addMonths(visibleMonth, 1);

  function applyRange(nextStartDate: string, nextEndDate: string) {
    setRange(clampRange(nextStartDate, nextEndDate));
  }

  function applyQuickWindow(windowDays: number) {
    const today = todayDateString();
    const nextStartDate = formatDate(addDays(parseDate(today), -(windowDays - 1)));
    applyRange(nextStartDate, today);
    setVisibleMonth(startOfMonth(parseDate(nextStartDate)));
    setPendingAnchor(null);
  }

  function selectCalendarDate(date: string) {
    if (!pendingAnchor) {
      setPendingAnchor(date);
      applyRange(date, date);
      return;
    }

    applyRange(pendingAnchor, date);
    setPendingAnchor(null);
  }

  return (
    <div className="relative grid gap-1 text-sm" ref={rootRef}>
      <span className="font-medium text-muted">Time window</span>
      <input name="days" readOnly type="hidden" value={rangeDays} />
      <input name="startDate" readOnly type="hidden" value={range.startDate} />
      <input name="endDate" readOnly type="hidden" value={range.endDate} />

      <button
        aria-expanded={open}
        className="flex h-10 min-w-64 items-center justify-between gap-3 rounded-lg border border-border bg-white px-3 text-left text-sm text-foreground shadow-sm outline-none transition-colors hover:border-primary focus:border-primary"
        onClick={() => setOpen((current) => !current)}
        type="button"
      >
        <span className="flex min-w-0 items-center gap-2">
          <CalendarDays className="h-4 w-4 shrink-0 text-muted" aria-hidden="true" />
          <span className="truncate">{activeQuickWindow?.label ?? rangeLabel}</span>
        </span>
        <ChevronDown className="h-4 w-4 shrink-0 text-muted" aria-hidden="true" />
      </button>

      {open && (
        <div className="absolute left-0 top-full z-50 mt-2 w-[min(46rem,calc(100vw-2rem))] rounded-lg border border-border bg-white p-3 shadow-xl">
          <div className="grid gap-3 lg:grid-cols-[9rem_minmax(0,1fr)]">
            <div className="grid auto-rows-max gap-2 border-b border-border pb-3 lg:border-b-0 lg:border-r lg:pb-0 lg:pr-3">
              {quickWindows.map((quick) => {
                const active = quick.days === rangeDays && range.endDate === todayString;

                return (
                  <button
                    className={cn(
                      "h-9 rounded-md px-3 text-left text-sm font-medium transition-colors hover:bg-slate-50",
                      active && "bg-blue-50 text-foreground",
                    )}
                    key={quick.label}
                    onClick={() => applyQuickWindow(quick.days)}
                    type="button"
                  >
                    {quick.label}
                  </button>
                );
              })}
            </div>

            <div className="grid gap-3">
              <div className="grid gap-2 sm:grid-cols-2">
                <DateField
                  label="Start"
                  onChange={(value) => {
                    applyRange(value, range.endDate);
                    setPendingAnchor(null);
                  }}
                  value={range.startDate}
                />
                <DateField
                  label="End"
                  onChange={(value) => {
                    applyRange(range.startDate, value);
                    setPendingAnchor(null);
                  }}
                  value={range.endDate}
                />
              </div>

              <div className="flex items-center justify-between gap-3 border-y border-border py-2">
                <button
                  className="inline-flex h-8 w-8 items-center justify-center rounded-md border border-border bg-white text-foreground transition-colors hover:bg-slate-50"
                  onClick={() => setVisibleMonth((current) => addMonths(current, -1))}
                  type="button"
                >
                  <ChevronLeft className="h-4 w-4" aria-hidden="true" />
                  <span className="sr-only">Previous month</span>
                </button>
                <div className="text-center text-sm font-semibold text-foreground">
                  {formatMonth(visibleMonth)}
                  <span className="hidden sm:inline"> / {formatMonth(rightMonth)}</span>
                </div>
                <button
                  className="inline-flex h-8 w-8 items-center justify-center rounded-md border border-border bg-white text-foreground transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-45"
                  disabled={isSameMonth(visibleMonth, startOfMonth(parseDate(todayString)))}
                  onClick={() => setVisibleMonth((current) => addMonths(current, 1))}
                  type="button"
                >
                  <ChevronRight className="h-4 w-4" aria-hidden="true" />
                  <span className="sr-only">Next month</span>
                </button>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <CalendarMonth
                  month={visibleMonth}
                  onSelect={selectCalendarDate}
                  range={range}
                />
                <CalendarMonth
                  className="hidden sm:block"
                  month={rightMonth}
                  onSelect={selectCalendarDate}
                  range={range}
                />
              </div>

              <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border pt-2 text-xs text-muted">
                <span>{rangeDays} days selected</span>
                <span>{rangeLabel}</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function DateField({
  label,
  onChange,
  value,
}: {
  label: string;
  onChange: (value: string) => void;
  value: string;
}) {
  const [draft, setDraft] = useState(() => formatInputDate(value));

  useEffect(() => {
    setDraft(formatInputDate(value));
  }, [value]);

  function commitDraft() {
    const parsed = parseInputDate(draft);
    if (!parsed) {
      setDraft(formatInputDate(value));
      return;
    }

    onChange(parsed);
  }

  return (
    <label className="grid gap-1">
      <span className="text-xs font-semibold text-muted">{label}</span>
      <span className="relative block">
        <input
          autoComplete="off"
          className="h-10 w-full rounded-lg border border-border bg-white px-3 pr-10 text-sm text-foreground outline-none focus:border-primary"
          inputMode="numeric"
          onBlur={commitDraft}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              commitDraft();
            }
          }}
          placeholder="MM/DD/YYYY"
          type="text"
          value={draft}
        />
        <CalendarDays
          className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-foreground opacity-90"
          aria-hidden="true"
        />
      </span>
    </label>
  );
}

function CalendarMonth({
  className,
  month,
  onSelect,
  range,
}: {
  className?: string;
  month: Date;
  onSelect: (date: string) => void;
  range: { startDate: string; endDate: string };
}) {
  const cells = useMemo(() => buildMonthCells(month), [month]);
  const today = todayDateString();

  return (
    <div className={className}>
      <div className="mb-2 text-center text-sm font-semibold text-foreground">{formatMonth(month)}</div>
      <div className="grid grid-cols-7 gap-1 text-center text-xs font-semibold text-muted">
        {weekdayLabels.map((weekday) => (
          <div className="py-1" key={weekday}>{weekday}</div>
        ))}
      </div>
      <div className="mt-1 grid grid-cols-7 gap-1">
        {cells.map((cell, index) => {
          if (!cell) {
            return <div className="h-9" key={`empty-${index}`} />;
          }

          const disabled = cell.date > today;
          const selectedStart = cell.date === range.startDate;
          const selectedEnd = cell.date === range.endDate;
          const inRange = cell.date > range.startDate && cell.date < range.endDate;

          return (
            <button
              aria-pressed={selectedStart || selectedEnd}
              className={cn(
                "h-9 rounded-md text-sm font-medium text-foreground transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:text-muted disabled:opacity-40",
                inRange && "bg-blue-50",
                (selectedStart || selectedEnd) && "bg-primary text-primary-foreground hover:bg-primary",
              )}
              disabled={disabled}
              key={cell.date}
              onClick={() => onSelect(cell.date)}
              type="button"
            >
              {cell.day}
            </button>
          );
        })}
      </div>
    </div>
  );
}

function buildMonthCells(month: Date) {
  const year = month.getUTCFullYear();
  const monthIndex = month.getUTCMonth();
  const firstDay = new Date(Date.UTC(year, monthIndex, 1)).getUTCDay();
  const daysInMonth = new Date(Date.UTC(year, monthIndex + 1, 0)).getUTCDate();
  const cells: Array<{ date: string; day: number } | null> = [];

  for (let index = 0; index < firstDay; index += 1) {
    cells.push(null);
  }

  for (let day = 1; day <= daysInMonth; day += 1) {
    cells.push({ date: formatDate(new Date(Date.UTC(year, monthIndex, day))), day });
  }

  while (cells.length % 7 !== 0) {
    cells.push(null);
  }

  return cells;
}

function clampRange(startDate: string, endDate: string) {
  let start = parseDate(startDate);
  let end = parseDate(endDate);
  const today = parseDate(todayDateString());

  if (start > end) {
    [start, end] = [end, start];
  }

  if (end > today) {
    end = today;
  }

  if (start > end) {
    start = end;
  }

  if (differenceInDays(start, end) + 1 > maxWindowDays) {
    start = addDays(end, -(maxWindowDays - 1));
  }

  return {
    startDate: formatDate(start),
    endDate: formatDate(end),
  };
}

function parseDate(value: string) {
  const [year, month, day] = value.split("-").map((part) => Number.parseInt(part, 10));
  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return parseDate(todayDateString());
  }

  return new Date(Date.UTC(year, month - 1, day));
}

function parseInputDate(value: string) {
  const trimmed = value.trim();
  const isoMatch = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(trimmed);
  if (isoMatch) {
    return normalizeDateParts(
      Number.parseInt(isoMatch[1], 10),
      Number.parseInt(isoMatch[2], 10),
      Number.parseInt(isoMatch[3], 10),
    );
  }

  const slashMatch = /^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/.exec(trimmed);
  if (!slashMatch) {
    return null;
  }

  return normalizeDateParts(
    Number.parseInt(slashMatch[3], 10),
    Number.parseInt(slashMatch[1], 10),
    Number.parseInt(slashMatch[2], 10),
  );
}

function normalizeDateParts(year: number, month: number, day: number) {
  const date = new Date(Date.UTC(year, month - 1, day));
  if (
    date.getUTCFullYear() !== year ||
    date.getUTCMonth() !== month - 1 ||
    date.getUTCDate() !== day
  ) {
    return null;
  }

  return formatDate(date);
}

function formatInputDate(value: string) {
  const date = parseDate(value);
  const month = String(date.getUTCMonth() + 1).padStart(2, "0");
  const day = String(date.getUTCDate()).padStart(2, "0");
  return `${month}/${day}/${date.getUTCFullYear()}`;
}

function formatDate(date: Date) {
  return date.toISOString().slice(0, 10);
}

function todayDateString() {
  return formatDate(new Date());
}

function startOfMonth(date: Date) {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1));
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setUTCDate(next.getUTCDate() + days);
  return next;
}

function addMonths(date: Date, months: number) {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + months, 1));
}

function differenceInDays(start: Date, end: Date) {
  return Math.round((end.getTime() - start.getTime()) / 86400000);
}

function inclusiveDays(startDate: string, endDate: string) {
  return differenceInDays(parseDate(startDate), parseDate(endDate)) + 1;
}

function formatShortDate(value: string) {
  return new Intl.DateTimeFormat("en", {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  }).format(parseDate(value));
}

function formatMonth(date: Date) {
  return new Intl.DateTimeFormat("en", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(date);
}

function isSameMonth(a: Date, b: Date) {
  return a.getUTCFullYear() === b.getUTCFullYear() && a.getUTCMonth() === b.getUTCMonth();
}
