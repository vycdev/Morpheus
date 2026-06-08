"use client";

import { useId, useMemo } from "react";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { DashboardActivityPoint } from "@/lib/types";
import { formatCompactNumber } from "@/lib/utils";

const shortDateFormatter = new Intl.DateTimeFormat("en", {
  month: "short",
  day: "numeric",
  timeZone: "UTC",
});

type ActivityChartProps = {
  points: DashboardActivityPoint[];
};

export function ActivityChart({ points }: ActivityChartProps) {
  const chartId = useId().replace(/[^a-zA-Z0-9_-]/g, "");
  const xpFillId = `activity-xp-fill-${chartId}`;
  const data = useMemo(
    () =>
      points.map((point) => ({
        date: formatShortDate(point.dateUtc),
        xp: point.xp,
        messages: point.messages,
      })),
    [points],
  );

  return (
    <div className="grid min-w-0 gap-4 xl:grid-cols-[minmax(0,1.45fr)_minmax(0,1fr)]">
      <div
        className="relative h-[310px] min-h-[310px] w-full min-w-0 overflow-hidden rounded-lg border border-border bg-white p-3"
        style={{ height: 310, minHeight: 310, minWidth: 0 }}
      >
        <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
          <AreaChart data={data} margin={{ left: 6, right: 8, top: 14, bottom: 4 }}>
            <defs>
              <linearGradient id={xpFillId} x1="0" x2="0" y1="0" y2="1">
                <stop offset="5%" stopColor="#2563eb" stopOpacity={0.26} />
                <stop offset="95%" stopColor="#2563eb" stopOpacity={0.03} />
              </linearGradient>
            </defs>
            <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
            <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
            <YAxis
              tick={{ fill: "#667085", fontSize: 12 }}
              tickFormatter={(value) => formatCompactNumber(Number(value))}
              tickLine={false}
              axisLine={false}
              width={42}
            />
            <Tooltip content={<DashboardTooltip />} />
            <Area
              type="monotone"
              dataKey="xp"
              name="XP"
              stroke="#2563eb"
              strokeWidth={2}
              fill={`url(#${xpFillId})`}
              isAnimationActive={false}
              activeDot={{ r: 4 }}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      <div
        className="relative h-[310px] min-h-[310px] w-full min-w-0 overflow-hidden rounded-lg border border-border bg-white p-3"
        style={{ height: 310, minHeight: 310, minWidth: 0 }}
      >
        <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
          <BarChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 4 }}>
            <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
            <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
            <YAxis
              tick={{ fill: "#667085", fontSize: 12 }}
              tickFormatter={(value) => formatCompactNumber(Number(value))}
              tickLine={false}
              axisLine={false}
              width={38}
            />
            <Tooltip content={<DashboardTooltip />} />
            <Bar dataKey="messages" name="Messages" fill="#0891b2" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

function DashboardTooltip({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: Array<{ name?: string | number; value?: unknown; color?: string; dataKey?: string | number }>;
  label?: string;
}) {
  if (!active || !payload?.length) {
    return null;
  }

  return (
    <div className="rounded-lg border border-border bg-white px-3 py-2 text-sm shadow-md">
      <div className="mb-1 font-medium text-foreground">{label}</div>
      <div className="grid gap-1">
        {payload.map((item, index) => {
          const name = item.name ?? item.dataKey ?? "Value";

          return (
            <div key={`${name}-${index}`} className="flex items-center gap-2 text-muted">
              <span className="h-2 w-2 rounded-sm" style={{ backgroundColor: item.color }} />
              <span>{name}</span>
              <span className="font-medium text-foreground">{formatTooltipValue(item.value)}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function formatShortDate(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : shortDateFormatter.format(date);
}

function formatTooltipValue(value: unknown) {
  const numericValue = typeof value === "number" ? value : Number(value);
  return Number.isFinite(numericValue) ? formatCompactNumber(numericValue) : String(value ?? "");
}
