"use client";

import { ArrowRight } from "lucide-react";
import type React from "react";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ComposedChart,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type {
  DashboardActivityDerivedPoint,
  DashboardButtonGamePoint,
  DashboardCalendarActivityCell,
  DashboardCategoryValue,
  DashboardEconomyFlowPoint,
  DashboardHeatmapCell,
  DashboardLogPoint,
  DashboardMoneyFlow,
  DashboardQuoteStatusSlice,
  DashboardStackedServerActivityPoint,
} from "@/lib/types";
import { formatCompactNumber, formatCurrency } from "@/lib/utils";

const chartColors = ["#2563eb", "#0891b2", "#059669", "#d97706", "#e11d48", "#7c3aed"];

export function GlobalActivityLineChart({ points }: { points: DashboardActivityDerivedPoint[] }) {
  const hasActivity = points.some((point) => point.messages > 0 || point.activeUsers > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Messages: point.messages,
    "Active users": point.activeUsers,
  }));

  return (
    <ChartFrame empty={!hasActivity} emptyLabel="No global activity in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <LineChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <Tooltip content={<TooltipContent />} />
          <Line dataKey="Messages" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2.4} type="monotone" />
          <Line dataKey="Active users" dot={false} isAnimationActive={false} stroke="#059669" strokeWidth={2.2} type="monotone" />
        </LineChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function CumulativeXpChart({ points }: { points: DashboardActivityDerivedPoint[] }) {
  const hasXp = points.some((point) => point.cumulativeXp > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    "Cumulative XP": point.cumulativeXp,
  }));

  return (
    <ChartFrame empty={!hasXp} emptyLabel="No XP in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <AreaChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <defs>
            <linearGradient id="global-cumulative-xp-fill" x1="0" x2="0" y1="0" y2="1">
              <stop offset="5%" stopColor="#059669" stopOpacity={0.24} />
              <stop offset="95%" stopColor="#059669" stopOpacity={0.03} />
            </linearGradient>
          </defs>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={46}
          />
          <Tooltip content={<TooltipContent />} />
          <Area
            dataKey="Cumulative XP"
            fill="url(#global-cumulative-xp-fill)"
            isAnimationActive={false}
            stroke="#059669"
            strokeWidth={2.3}
            type="monotone"
          />
        </AreaChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function MessagesOverTimeChart({ points }: { points: DashboardActivityDerivedPoint[] }) {
  const hasMessages = points.some((point) => point.messages > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Messages: point.messages,
  }));

  return (
    <ChartFrame empty={!hasMessages} emptyLabel="No messages in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <BarChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Messages" fill="#0891b2" isAnimationActive={false} radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function StackedServerActivityChart({ points }: { points: DashboardStackedServerActivityPoint[] }) {
  const hasActivity = points.some((point) => point.messages > 0);
  const serverRows = Array.from(
    new Map(points.map((point) => [point.guildId, point.guildName])).entries(),
  ).slice(0, chartColors.length);
  const rowsByDate = new Map<string, Record<string, string | number>>();

  for (const point of points) {
    const dateKey = point.dateUtc.slice(0, 10);
    const row = rowsByDate.get(dateKey) ?? { dateKey, date: formatShortDate(point.dateUtc) };
    row[`server-${point.guildId}`] = point.messages;
    rowsByDate.set(dateKey, row);
  }

  const data = Array.from(rowsByDate.values());

  return (
    <ChartFrame empty={!hasActivity} emptyLabel="No server activity in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={300}>
        <BarChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <Tooltip content={<TooltipContent />} />
          {serverRows.map(([guildId, guildName], index) => (
            <Bar
              dataKey={`server-${guildId}`}
              fill={chartColors[index % chartColors.length]}
              isAnimationActive={false}
              key={guildId}
              name={guildName}
              stackId="activity"
            />
          ))}
        </BarChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function CalendarActivityHeatmap({ cells }: { cells: DashboardCalendarActivityCell[] }) {
  if (cells.length === 0) {
    return <EmptyState label="No calendar activity in this window" />;
  }

  const sortedCells = [...cells].sort((a, b) => new Date(a.dateUtc).getTime() - new Date(b.dateUtc).getTime());
  const max = Math.max(1, ...sortedCells.map((cell) => cell.messages));
  const firstDay = new Date(sortedCells[0].dateUtc).getUTCDay();
  const blanks = Array.from({ length: firstDay }, (_, index) => index);
  const columns = Math.ceil((blanks.length + sortedCells.length) / 7);
  const cellSize = columns <= 6 ? "clamp(1.4rem, 6vw, 2.5rem)" : "clamp(0.8rem, 2vw, 1.1rem)";

  return (
    <div className="grid min-h-[240px] content-between gap-3">
      <div className="max-w-full overflow-x-auto pb-1">
        <div className="grid w-max grid-flow-col grid-rows-7 gap-1.5" style={{ gridAutoColumns: cellSize }}>
          {blanks.map((blank) => (
            <span className="aspect-square w-full rounded-[4px]" key={`blank-${blank}`} />
          ))}
          {sortedCells.map((cell) => {
            const opacity = cell.messages === 0 ? 0.08 : 0.2 + (cell.messages / max) * 0.72;
            return (
              <span
                className="aspect-square w-full rounded-[4px] border border-border"
                key={cell.dateUtc}
                style={{ backgroundColor: `rgba(37, 99, 235, ${opacity})` }}
                title={`${formatShortDate(cell.dateUtc)}: ${cell.messages} messages, ${formatCompactNumber(cell.xp)} XP`}
              />
            );
          })}
        </div>
      </div>
      <div className="flex items-center justify-between gap-3 text-xs text-muted">
        <span>{formatShortDate(sortedCells[0].dateUtc)}</span>
        <span>{formatShortDate(sortedCells[sortedCells.length - 1].dateUtc)}</span>
      </div>
    </div>
  );
}

export function ActivityInsightChart({ points }: { points: DashboardActivityDerivedPoint[] }) {
  const hasActivity = points.some((point) => point.messages > 0 || point.xp > 0 || point.activeUsers > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    messages: point.messages,
    rolling: point.rollingMessages,
    cumulative: point.cumulativeMessages,
  }));

  return (
    <ChartFrame empty={!hasActivity} emptyLabel="No activity in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={300}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <YAxis
            orientation="right"
            yAxisId="cumulative"
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={46}
          />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="messages" name="Messages" fill="#0891b2" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line
            dataKey="rolling"
            isAnimationActive={false}
            name="7d rolling avg"
            stroke="#2563eb"
            strokeWidth={2}
            dot={false}
            type="monotone"
          />
          <Area
            dataKey="cumulative"
            fill="#059669"
            fillOpacity={0.08}
            isAnimationActive={false}
            name="Cumulative"
            stroke="#059669"
            strokeWidth={2}
            type="monotone"
            yAxisId="cumulative"
          />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function EconomyFlowChart({ points }: { points: DashboardEconomyFlowPoint[] }) {
  const hasMovement = points.some((point) => point.inflow !== 0 || point.outflow !== 0 || point.net !== 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Inflow: point.inflow,
    Outflow: point.outflow,
    Net: point.net,
  }));

  return (
    <ChartFrame empty={!hasMovement} emptyLabel="No economy movement in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={48}
          />
          <Tooltip content={<TooltipContent currency />} />
          <Bar dataKey="Inflow" fill="#059669" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Outflow" fill="#e11d48" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Net" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2} type="monotone" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function ButtonGameChart({ points }: { points: DashboardButtonGamePoint[] }) {
  const hasPresses = points.some((point) => point.presses > 0 || point.score !== 0 || point.activeUsers > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Presses: point.presses,
    Rolling: point.rollingPresses,
    Score: point.score,
  }));

  return (
    <ChartFrame empty={!hasPresses} emptyLabel="No button-game presses in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Presses" fill="#d97706" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Rolling" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2} type="monotone" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function LogTimelineChart({ points }: { points: DashboardLogPoint[] }) {
  const hasLogs = points.some((point) => point.total > 0 || point.warnings > 0 || point.errors > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Total: point.total,
    Warnings: point.warnings,
    Errors: point.errors,
  }));

  return (
    <ChartFrame empty={!hasLogs} emptyLabel="No logs in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={240}>
        <BarChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Total" fill="#2563eb" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Warnings" fill="#d97706" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Errors" fill="#e11d48" isAnimationActive={false} radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function DonutChart({
  data,
  labelKey,
}: {
  data: DashboardQuoteStatusSlice[] | DashboardCategoryValue[];
  labelKey: "status" | "label";
}) {
  const chartData = data.map((item) => ({
    name: labelKey === "status" ? (item as DashboardQuoteStatusSlice).status : (item as DashboardCategoryValue).label,
    value: labelKey === "status" ? (item as DashboardQuoteStatusSlice).count : (item as DashboardCategoryValue).value,
  }));
  const total = chartData.reduce((sum, item) => sum + item.value, 0);

  if (chartData.length === 0 || total === 0) {
    return <EmptyState label="No chart data in this window" />;
  }

  return (
    <div className="grid min-h-[220px] min-w-0 grid-cols-[minmax(0,1fr)_auto] items-center gap-3" style={{ minHeight: 220, minWidth: 0 }}>
      <ResponsiveContainer width="100%" height={220} minWidth={0}>
        <PieChart>
          <Tooltip content={<TooltipContent />} />
          <Pie
            data={chartData}
            dataKey="value"
            innerRadius={54}
            isAnimationActive={false}
            outerRadius={82}
            paddingAngle={2}
            stroke="var(--card)"
            strokeWidth={2}
          >
            {chartData.map((entry, index) => (
              <Cell
                fill={chartColors[index % chartColors.length]}
                key={entry.name}
                stroke="var(--card)"
                strokeWidth={2}
              />
            ))}
          </Pie>
        </PieChart>
      </ResponsiveContainer>
      <div className="grid min-w-32 gap-2 text-sm">
        {chartData.map((entry, index) => (
          <div className="flex items-center justify-between gap-3" key={entry.name}>
            <span className="flex min-w-0 items-center gap-2 text-muted">
              <span
                className="h-2.5 w-2.5 shrink-0 rounded-sm"
                style={{ backgroundColor: chartColors[index % chartColors.length] }}
              />
              <span className="truncate">{entry.name}</span>
            </span>
            <span className="font-semibold text-foreground">{formatCompactNumber(entry.value)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function CategoryBars({
  data,
  currency = false,
}: {
  data: DashboardCategoryValue[];
  currency?: boolean;
}) {
  const max = Math.max(1, ...data.map((item) => item.value));

  if (data.length === 0) {
    return <EmptyState label="No categories found" />;
  }

  if (data.every((item) => item.value === 0)) {
    return <EmptyState label="No distribution data in this window" />;
  }

  return (
    <div className="grid gap-3">
      {data.slice(0, 8).map((item, index) => (
        <div className="rounded-lg border border-border bg-slate-50 p-3" key={item.label}>
          <div className="flex items-center justify-between gap-3 text-sm">
            <span className="truncate font-medium text-foreground">{item.label}</span>
            <span className="shrink-0 text-muted">{currency ? formatCurrency(item.value) : formatCompactNumber(item.value)}</span>
          </div>
          <div
            className="mt-2 h-2.5 overflow-hidden rounded-full"
            style={{ backgroundColor: "color-mix(in srgb, var(--border) 48%, transparent)" }}
          >
            <div
              className="h-full rounded-full"
              style={{
                width: item.value > 0 ? `${Math.max(2, (item.value / max) * 100)}%` : "0%",
                backgroundColor: chartColors[index % chartColors.length],
              }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}

export function ActivityHeatmap({ cells }: { cells: DashboardHeatmapCell[] }) {
  const max = Math.max(1, ...cells.map((cell) => cell.messages));
  const byDay = Array.from({ length: 7 }, (_, day) =>
    cells.filter((cell) => cell.dayOfWeek === day).sort((a, b) => a.hourUtc - b.hourUtc),
  );

  return (
    <div className="grid gap-2 pb-1">
      <div className="grid w-full grid-cols-[2rem_repeat(24,minmax(0,1fr))] gap-1 text-xs text-muted sm:grid-cols-[3rem_repeat(24,minmax(0,1fr))]">
        <span />
        {Array.from({ length: 24 }, (_, hour) => (
          <span className="min-w-0 text-center" key={hour}>
            {hour % 3 === 0 ? hour : ""}
          </span>
        ))}
        {byDay.map((dayCells, dayIndex) => (
          <FragmentRow cells={dayCells} dayIndex={dayIndex} key={dayIndex} max={max} />
        ))}
      </div>
    </div>
  );
}

export function MoneyFlowView({ flows }: { flows: DashboardMoneyFlow[] }) {
  const max = Math.max(1, ...flows.map((flow) => flow.value));

  if (flows.length === 0) {
    return <EmptyState label="No money movement in this window" />;
  }

  return (
    <div className="grid gap-3">
      {flows.map((flow, index) => (
        <div className="rounded-lg border border-border bg-white p-3" key={`${flow.source}-${flow.target}`}>
          <div className="flex items-center justify-between gap-3 text-sm">
            <div className="flex min-w-0 items-center gap-2">
              <span className="truncate font-medium text-foreground">{flow.source}</span>
              <ArrowRight className="h-4 w-4 shrink-0 text-muted" aria-hidden="true" />
              <span className="truncate font-medium text-foreground">{flow.target}</span>
            </div>
            <span className="shrink-0 font-semibold text-foreground">{formatCurrency(flow.value)}</span>
          </div>
          <div
            className="mt-3 h-2 overflow-hidden rounded-full"
            style={{ backgroundColor: "color-mix(in srgb, var(--border) 48%, transparent)" }}
          >
            <div
              className="h-full rounded-full"
              style={{
                width: `${Math.max(5, (flow.value / max) * 100)}%`,
                backgroundColor: chartColors[index % chartColors.length],
              }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}

function FragmentRow({
  cells,
  dayIndex,
  max,
}: {
  cells: DashboardHeatmapCell[];
  dayIndex: number;
  max: number;
}) {
  const label = cells[0]?.dayLabel ?? ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][dayIndex];

  return (
    <>
      <span className="flex min-w-0 items-center text-[10px] font-medium sm:text-xs">{label}</span>
      {cells.map((cell) => {
        const opacity = cell.messages === 0 ? 0.08 : 0.18 + (cell.messages / max) * 0.72;
        return (
          <span
            className="aspect-square min-h-2 rounded-[3px] border border-border"
            key={`${cell.dayOfWeek}-${cell.hourUtc}`}
            style={{ backgroundColor: `rgba(8, 145, 178, ${opacity})` }}
            title={`${cell.dayLabel} ${cell.hourUtc}:00 UTC: ${cell.messages} messages`}
          />
        );
      })}
    </>
  );
}

function ChartFrame({
  children,
  empty,
  emptyLabel,
}: {
  children: React.ReactNode;
  empty: boolean;
  emptyLabel: string;
}) {
  return (
    <div
      className="relative h-[320px] min-h-[260px] w-full min-w-0 overflow-hidden rounded-lg border border-border bg-white p-3"
      style={{ height: 320, minHeight: 260, minWidth: 0 }}
    >
      {empty ? <EmptyState label={emptyLabel} /> : children}
    </div>
  );
}

function EmptyState({ label }: { label: string }) {
  return (
    <div className="grid h-full min-h-[180px] place-items-center rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted">
      {label}
    </div>
  );
}

function TooltipContent({
  active,
  payload,
  label,
  currency = false,
}: {
  active?: boolean;
  payload?: Array<{ name: string; value: number; color?: string }>;
  label?: string;
  currency?: boolean;
}) {
  if (!active || !payload?.length) {
    return null;
  }

  return (
    <div className="rounded-lg border border-border bg-white px-3 py-2 text-sm shadow-md">
      <div className="mb-1 font-medium text-foreground">{label}</div>
      <div className="grid gap-1">
        {payload.map((item) => (
          <div className="flex items-center gap-2 text-muted" key={item.name}>
            <span className="h-2 w-2 rounded-sm" style={{ backgroundColor: item.color }} />
            <span>{item.name}</span>
            <span className="font-medium text-foreground">
              {currency ? formatCurrency(item.value) : formatCompactNumber(item.value)}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

function formatShortDate(value: string) {
  return new Intl.DateTimeFormat("en", { month: "short", day: "numeric" }).format(new Date(value));
}
