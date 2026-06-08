"use client";

import { ArrowRight } from "lucide-react";
import { useId, useMemo } from "react";
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
  Scatter,
  ScatterChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from "recharts";
import type {
  DashboardActivityBoxPlotPoint,
  DashboardActivityComparisonSeries,
  DashboardActivityDerivedPoint,
  DashboardActivityDistributionPoint,
  DashboardActivityParetoPoint,
  DashboardActivityScatterPoint,
  DashboardButtonGamePoint,
  DashboardCalendarActivityCell,
  DashboardCategoryValue,
  DashboardChannelHourHeatmapCell,
  DashboardChannelHeatmapCell,
  DashboardEconomyHeatmapCell,
  DashboardEconomyFlowPoint,
  DashboardEconomyStackedPoint,
  DashboardEconomySupplyPoint,
  DashboardHistogramBucket,
  DashboardHeatmapCell,
  DashboardLogPoint,
  DashboardMoneyFlow,
  DashboardQuoteAuthorSummary,
  DashboardQuoteServerSummary,
  DashboardQuoteStatusSlice,
  DashboardQuoteTimelinePoint,
  DashboardReminderPoint,
  DashboardStackedServerActivityPoint,
  DashboardServerDayActivityCell,
  DashboardStockActivityPricePoint,
  DashboardStockTradePoint,
  DashboardTemporaryBanPoint,
  DashboardUserButtonScorePoint,
  DashboardUserLevelPoint,
  DashboardUserMessageLengthPoint,
  DashboardUserRankTimelinePoint,
  DashboardUserReminderTimelineItem,
  DashboardUserRankMovement,
} from "@/lib/types";
import { formatCompactNumber, formatCurrency } from "@/lib/utils";

const chartColors = ["#2563eb", "#0891b2", "#059669", "#d97706", "#e11d48", "#7c3aed"];
const dayLabels = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const shortDateFormatter = new Intl.DateTimeFormat("en", {
  month: "short",
  day: "numeric",
  timeZone: "UTC",
});

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
  const fillId = useChartId("global-cumulative-xp-fill");
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
            <linearGradient id={fillId} x1="0" x2="0" y1="0" y2="1">
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
            fill={`url(#${fillId})`}
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

export function XpPerDayChart({ points }: { points: DashboardActivityDerivedPoint[] }) {
  const hasXp = points.some((point) => point.xp > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    XP: point.xp,
  }));

  return (
    <ChartFrame empty={!hasXp} emptyLabel="No XP in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <BarChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
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
          <Bar dataKey="XP" fill="#059669" isAnimationActive={false} radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function LevelProgressionGraph({ points }: { points: DashboardUserLevelPoint[] }) {
  const hasLevels = points.some((point) => point.level > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Level: point.level,
    XP: point.totalXp,
  }));

  return (
    <ChartFrame empty={!hasLevels} emptyLabel="No level progression in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} width={42} />
          <YAxis
            orientation="right"
            yAxisId="xp"
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={48}
          />
          <Tooltip content={<TooltipContent />} />
          <Line dataKey="Level" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2.4} type="stepAfter" />
          <Area
            dataKey="XP"
            fill="#059669"
            fillOpacity={0.08}
            isAnimationActive={false}
            stroke="#059669"
            strokeWidth={2}
            type="monotone"
            yAxisId="xp"
          />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function MessageLengthTrendChart({ points }: { points: DashboardUserMessageLengthPoint[] }) {
  const hasMessages = points.some((point) => point.messages > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Average: point.averageMessageLength,
    "7d moving": point.movingAverage,
    Messages: point.messages,
  }));

  return (
    <ChartFrame empty={!hasMessages} emptyLabel="No message-length trend in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} width={42} />
          <YAxis
            orientation="right"
            yAxisId="messages"
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Messages" fill="#cbd5e1" isAnimationActive={false} radius={[4, 4, 0, 0]} yAxisId="messages" />
          <Line dataKey="Average" dot={false} isAnimationActive={false} stroke="#0891b2" strokeWidth={2.2} type="monotone" />
          <Line dataKey="7d moving" dot={false} isAnimationActive={false} stroke="#d97706" strokeWidth={2.2} type="monotone" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function MessageLengthHistogramChart({ buckets }: { buckets: DashboardHistogramBucket[] }) {
  const hasBuckets = buckets.some((bucket) => bucket.count > 0);
  const data = buckets.map((bucket) => ({
    Bucket: bucket.label,
    Messages: bucket.count,
  }));

  return (
    <ChartFrame empty={!hasBuckets} emptyLabel="No message-length histogram in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <BarChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="Bucket" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Messages" fill="#2563eb" isAnimationActive={false} radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function UserRankTimelineChart({ points }: { points: DashboardUserRankTimelinePoint[] }) {
  const hasRanks = points.some((point) => point.globalRank || point.serverRank);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    "Global rank": point.globalRank ?? null,
    "Server rank": point.serverRank ?? null,
  }));

  return (
    <ChartFrame empty={!hasRanks} emptyLabel="No rank movement in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <LineChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis reversed tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} width={42} />
          <Tooltip content={<TooltipContent />} />
          <Line connectNulls dataKey="Global rank" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2.3} type="monotone" />
          <Line connectNulls dataKey="Server rank" dot={false} isAnimationActive={false} stroke="#059669" strokeWidth={2.1} type="monotone" />
        </LineChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function TransactionTimelineChart({ points }: { points: DashboardEconomyFlowPoint[] }) {
  return <EconomyFlowChart points={points} />;
}

export function ButtonScoreTimelineChart({ points }: { points: DashboardUserButtonScorePoint[] }) {
  const hasScores = points.some((point) => point.score !== 0 || point.cumulativeScore !== 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.insertedAtUtc),
    Score: point.score,
    Cumulative: point.cumulativeScore,
  }));

  return (
    <ChartFrame empty={!hasScores} emptyLabel="No button-game scores in this window">
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
          <Bar dataKey="Score" fill="#d97706" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Cumulative" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2.2} type="monotone" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function ReminderTimelineChart({ reminders }: { reminders: DashboardUserReminderTimelineItem[] }) {
  const counts = Array.from(
    reminders
      .reduce((map, reminder) => {
        const date = reminder.dueDateUtc.slice(0, 10);
        map.set(date, (map.get(date) ?? 0) + 1);
        return map;
      }, new Map<string, number>())
      .entries(),
  )
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([date, count]) => ({ date: formatShortDate(date), Reminders: count }));

  return (
    <ChartFrame empty={counts.length === 0} emptyLabel="No reminder timeline data">
      <ResponsiveContainer width="100%" height="100%" minHeight={240}>
        <BarChart data={counts} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} width={32} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Reminders" fill="#0891b2" isAnimationActive={false} radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function UserRankMovementChart({ rows }: { rows: DashboardUserRankMovement[] }) {
  const hasMovement = rows.length > 0;
  const data = rows.map((row) => ({
    user: row.username,
    "Rank change": row.rankChange,
    "Current XP": row.currentXp,
  }));

  return (
    <ChartFrame empty={!hasMovement} emptyLabel="No rank movement in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <BarChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="user" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} width={38} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Rank change" isAnimationActive={false} radius={[4, 4, 0, 0]}>
            {data.map((entry) => (
              <Cell
                fill={entry["Rank change"] >= 0 ? "#059669" : "#e11d48"}
                key={entry.user}
              />
            ))}
          </Bar>
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
        <AreaChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
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
            <Area
              dataKey={`server-${guildId}`}
              fill={chartColors[index % chartColors.length]}
              fillOpacity={0.34}
              isAnimationActive={false}
              key={guildId}
              name={guildName}
              stackId="activity"
              stroke={chartColors[index % chartColors.length]}
              strokeWidth={1.5}
              type="monotone"
            />
          ))}
        </AreaChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function CalendarActivityHeatmap({ cells }: { cells: DashboardCalendarActivityCell[] }) {
  const sortedCells = useMemo(
    () => [...cells].sort((a, b) => getUtcTimestamp(a.dateUtc) - getUtcTimestamp(b.dateUtc)),
    [cells],
  );

  if (sortedCells.length === 0) {
    return <EmptyState label="No calendar activity in this window" />;
  }

  const max = maxBy(sortedCells, (cell) => cell.messages);
  const firstDay = getUtcDayOfWeek(sortedCells[0].dateUtc);
  const blanks = Array.from({ length: firstDay }, (_, index) => index);
  const columns = Math.ceil((blanks.length + sortedCells.length) / 7);

  if (sortedCells.length <= 62) {
    const trailingBlanks = Array.from(
      { length: Math.max(0, columns * 7 - blanks.length - sortedCells.length) },
      (_, index) => index,
    );

    return (
      <div className="grid min-h-[240px] content-between gap-3">
        <div className="grid grid-cols-7 gap-1.5 text-xs font-medium text-muted">
          {["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].map((day) => (
            <span className="px-1" key={day}>{day}</span>
          ))}
          {blanks.map((blank) => (
            <span className="min-h-12 rounded-md border border-transparent" key={`blank-${blank}`} />
          ))}
          {sortedCells.map((cell) => {
            const intensity = cell.messages === 0 ? 7 : Math.round(18 + (cell.messages / max) * 58);
            return (
              <span
                className="grid min-h-12 content-between rounded-md border border-border p-2 text-left shadow-sm"
                key={cell.dateUtc}
                style={{
                  backgroundColor: `color-mix(in srgb, var(--primary) ${intensity}%, var(--card))`,
                }}
                title={`${formatShortDate(cell.dateUtc)}: ${cell.messages} messages, ${formatCompactNumber(cell.xp)} XP`}
              >
                <span className="text-xs font-semibold text-foreground">{getUtcDayOfMonth(cell.dateUtc)}</span>
                <span className="justify-self-end text-[11px] font-medium text-muted">
                  {cell.messages > 0 ? formatCompactNumber(cell.messages) : ""}
                </span>
              </span>
            );
          })}
          {trailingBlanks.map((blank) => (
            <span className="min-h-12 rounded-md border border-transparent" key={`trailing-${blank}`} />
          ))}
        </div>
        <div className="flex items-center justify-between gap-3 text-xs text-muted">
          <span>{formatShortDate(sortedCells[0].dateUtc)}</span>
          <span>{formatShortDate(sortedCells[sortedCells.length - 1].dateUtc)}</span>
        </div>
      </div>
    );
  }

  const cellSize = "clamp(0.8rem, 2vw, 1.1rem)";

  return (
    <div className="grid min-h-[240px] content-between gap-3">
      <div className="scrollbar-clean max-w-full overflow-x-auto pb-1">
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

export function MoneySupplyLineChart({ points }: { points: DashboardEconomySupplyPoint[] }) {
  const hasSupply = points.some((point) => point.totalMoneySupply > 0 || point.cashBalance > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    "Money supply": point.totalMoneySupply,
    Cash: point.cashBalance,
    "UBI pool": point.ubiPool,
    "Slots vault": point.slotsVault,
  }));

  return (
    <ChartFrame empty={!hasSupply} emptyLabel="No money supply data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <LineChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={52} />
          <Tooltip content={<TooltipContent currency />} />
          <Line dataKey="Money supply" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2.4} type="monotone" />
          <Line dataKey="Cash" dot={false} isAnimationActive={false} stroke="#059669" strokeWidth={2} type="monotone" />
          <Line dataKey="UBI pool" dot={false} isAnimationActive={false} stroke="#d97706" strokeWidth={2} type="monotone" />
          <Line dataKey="Slots vault" dot={false} isAnimationActive={false} stroke="#7c3aed" strokeWidth={2} type="monotone" />
        </LineChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function EconomyStackedVolumeChart({ points }: { points: DashboardEconomyStackedPoint[] }) {
  const hasVolume = points.some((point) =>
    point.stockBuy > 0 ||
    point.stockSell > 0 ||
    point.transfer > 0 ||
    point.donation > 0 ||
    point.slotsWin > 0 ||
    point.slotsLoss > 0 ||
    point.robberyWin > 0 ||
    point.robberyLoss > 0,
  );
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    "Stock buy": point.stockBuy,
    "Stock sell": point.stockSell,
    Transfer: point.transfer,
    Donation: point.donation,
    "Slots win": point.slotsWin,
    "Slots loss": point.slotsLoss,
    Robbery: point.robberyWin + point.robberyLoss,
    Fees: point.fees,
    Taxes: point.taxes,
  }));

  return (
    <ChartFrame empty={!hasVolume} emptyLabel="No transaction volume in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <AreaChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={48} />
          <Tooltip content={<TooltipContent currency />} />
          {["Stock buy", "Stock sell", "Transfer", "Donation", "Slots win", "Slots loss", "Robbery"].map((key, index) => (
            <Area dataKey={key} fill={chartColors[index % chartColors.length]} fillOpacity={0.18} isAnimationActive={false} key={key} stackId="volume" stroke={chartColors[index % chartColors.length]} type="monotone" />
          ))}
        </AreaChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function EconomyPoolChart({ points, label }: { points: DashboardEconomyFlowPoint[]; label: string }) {
  const hasPoints = points.some((point) => point.inflow > 0 || point.outflow > 0 || point.net > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Inflow: point.inflow,
    Outflow: point.outflow,
    [label]: point.net,
  }));

  return (
    <ChartFrame empty={!hasPoints} emptyLabel={`No ${label.toLowerCase()} data in this window`}>
      <ResponsiveContainer width="100%" height="100%" minHeight={260}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={48} />
          <Tooltip content={<TooltipContent currency />} />
          <Bar dataKey="Inflow" fill="#059669" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Outflow" fill="#e11d48" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey={label} dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2.2} type="monotone" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function EconomyHeatmap({ cells }: { cells: DashboardEconomyHeatmapCell[] }) {
  const max = Math.max(1, ...cells.map((cell) => cell.volume));
  const rows = Array.from(new Set(cells.map((cell) => cell.dayOfWeek))).sort((a, b) => a - b);
  const lookup = new Map(cells.map((cell) => [`${cell.dayOfWeek}-${cell.hourUtc}`, cell]));

  return (
    <div className="grid min-w-0 gap-2">
      <div className="grid grid-cols-[3rem_repeat(24,minmax(0,1fr))] gap-1 text-[10px] text-muted">
        <span />
        {Array.from({ length: 24 }, (_, hour) => <span className="text-center" key={hour}>{hour}</span>)}
      </div>
      {rows.map((day) => {
        const dayLabel = cells.find((cell) => cell.dayOfWeek === day)?.dayLabel ?? String(day);
        return (
          <div className="grid grid-cols-[3rem_repeat(24,minmax(0,1fr))] gap-1" key={day}>
            <span className="self-center text-xs text-muted">{dayLabel}</span>
            {Array.from({ length: 24 }, (_, hour) => {
              const cell = lookup.get(`${day}-${hour}`);
              const opacity = cell ? Math.max(0.08, Math.min(0.9, cell.volume / max)) : 0.04;
              return (
                <div
                  className="aspect-square rounded-sm"
                  key={hour}
                  style={{ backgroundColor: `rgba(37, 99, 235, ${opacity})` }}
                  title={`${dayLabel} ${hour}:00 - ${formatCurrency(cell?.volume ?? 0)} volume`}
                />
              );
            })}
          </div>
        );
      })}
    </div>
  );
}

export function StockTradeVolumeChart({ points }: { points: DashboardStockTradePoint[] }) {
  const hasTrades = points.some((point) => point.buyVolume > 0 || point.sellVolume > 0 || point.transferVolume > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Buy: point.buyVolume,
    Sell: point.sellVolume,
    Transfer: point.transferVolume,
    Trades: point.trades,
  }));

  return (
    <ChartFrame empty={!hasTrades} emptyLabel="No stock trades in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={48} />
          <YAxis orientation="right" yAxisId="trades" tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={42} />
          <Tooltip content={<TooltipContent currency />} />
          <Bar dataKey="Buy" fill="#059669" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Sell" fill="#e11d48" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Transfer" fill="#7c3aed" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Trades" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2} type="monotone" yAxisId="trades" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function StockActivityPriceComparisonChart({ points }: { points: DashboardStockActivityPricePoint[] }) {
  const hasPoints = points.some((point) => point.xp > 0 || point.holdingValue > 0);
  const data = points.map((point) => ({
    name: point.name,
    XP: point.xp,
    Price: point.price,
    Change: point.dailyChangePercent,
    Value: point.holdingValue,
  }));

  return (
    <ChartFrame empty={!hasPoints} emptyLabel="No activity-to-price data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ScatterChart margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" />
          <XAxis dataKey="XP" name="XP" tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} type="number" />
          <YAxis dataKey="Price" name="Price" tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCurrency(Number(value))} tickLine={false} axisLine={false} type="number" width={58} />
          <ZAxis dataKey="Value" range={[60, 420]} />
          <Tooltip content={<TooltipContent currency />} cursor={{ strokeDasharray: "3 3" }} />
          <Scatter data={data} fill="#2563eb" isAnimationActive={false} name="Stocks" />
        </ScatterChart>
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

export function LogErrorWarningLineChart({ points }: { points: DashboardLogPoint[] }) {
  const hasLogs = points.some((point) => point.warnings > 0 || point.errors > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Warnings: point.warnings,
    Errors: point.errors,
  }));

  return (
    <ChartFrame empty={!hasLogs} emptyLabel="No warning or error logs in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={240}>
        <LineChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={42} />
          <Tooltip content={<TooltipContent />} />
          <Line dataKey="Warnings" dot={false} isAnimationActive={false} stroke="#d97706" strokeWidth={2} type="monotone" />
          <Line dataKey="Errors" dot={false} isAnimationActive={false} stroke="#e11d48" strokeWidth={2} type="monotone" />
        </LineChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function ReminderVolumeChart({ points }: { points: DashboardReminderPoint[] }) {
  const hasReminders = points.some((point) => point.created > 0 || point.due > 0 || point.overdue > 0 || point.upcoming > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Created: point.created,
    Due: point.due,
    Overdue: point.overdue,
    Upcoming: point.upcoming,
  }));

  return (
    <ChartFrame empty={!hasReminders} emptyLabel="No reminder data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={250}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={42} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Created" fill="#2563eb" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Due" fill="#0891b2" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Upcoming" dot={false} isAnimationActive={false} stroke="#059669" strokeWidth={2} type="monotone" />
          <Line dataKey="Overdue" dot={false} isAnimationActive={false} stroke="#e11d48" strokeWidth={2} type="monotone" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function TemporaryBanTimelineChart({ points }: { points: DashboardTemporaryBanPoint[] }) {
  const hasBans = points.some((point) => point.created > 0 || point.completed > 0 || point.expiring > 0 || point.overdue > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Created: point.created,
    Completed: point.completed,
    Expiring: point.expiring,
    Overdue: point.overdue,
  }));

  return (
    <ChartFrame empty={!hasBans} emptyLabel="No temporary bans in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={250}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={42} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Created" fill="#7c3aed" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Completed" fill="#059669" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Expiring" dot={false} isAnimationActive={false} stroke="#d97706" strokeWidth={2} type="monotone" />
          <Line dataKey="Overdue" dot={false} isAnimationActive={false} stroke="#e11d48" strokeWidth={2} type="monotone" />
        </ComposedChart>
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

export function QuoteCreationTimelineChart({ points }: { points: DashboardQuoteTimelinePoint[] }) {
  const hasQuotes = points.some((point) => point.created > 0 || point.approvalVotes > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Created: point.created,
    Approved: point.approved,
    Pending: point.pending,
    Removed: point.removed,
    "Approval votes": point.approvalVotes,
  }));

  return (
    <ChartFrame empty={!hasQuotes} emptyLabel="No quote creation data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={42} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Created" fill="#2563eb" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Approved" fill="#059669" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Pending" fill="#d97706" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Bar dataKey="Removed" fill="#e11d48" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Approval votes" dot={false} isAnimationActive={false} stroke="#7c3aed" strokeWidth={2.2} type="monotone" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function QuoteScoreTrendChart({ points }: { points: DashboardQuoteTimelinePoint[] }) {
  const hasScores = points.some((point) => point.score !== 0 || point.scoreVotes > 0);
  const data = points.map((point) => ({
    date: formatShortDate(point.dateUtc),
    Score: point.score,
    Votes: point.scoreVotes,
  }));

  return (
    <ChartFrame empty={!hasScores} emptyLabel="No quote score trend in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="date" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={46} />
          <YAxis orientation="right" yAxisId="votes" tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={42} />
          <Tooltip content={<TooltipContent />} />
          <Area dataKey="Score" fill="#0891b2" fillOpacity={0.12} isAnimationActive={false} stroke="#0891b2" strokeWidth={2.3} type="monotone" />
          <Bar dataKey="Votes" fill="#cbd5e1" isAnimationActive={false} radius={[4, 4, 0, 0]} yAxisId="votes" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function QuoteAuthorBarChart({ authors }: { authors: DashboardQuoteAuthorSummary[] }) {
  const hasAuthors = authors.some((author) => author.quotes > 0 || author.score !== 0);
  const data = authors.slice(0, 10).map((author) => ({
    name: author.username,
    Quotes: author.quotes,
    Score: author.score,
  }));

  return (
    <ChartFrame empty={!hasAuthors} emptyLabel="No quote author data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <BarChart data={data} layout="vertical" margin={{ left: 6, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" horizontal={false} />
          <XAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} type="number" />
          <YAxis dataKey="name" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} type="category" width={88} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Quotes" fill="#2563eb" isAnimationActive={false} radius={[0, 4, 4, 0]} />
          <Bar dataKey="Score" fill="#059669" isAnimationActive={false} radius={[0, 4, 4, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function QuoteServerComparisonChart({ servers }: { servers: DashboardQuoteServerSummary[] }) {
  const hasServers = servers.some((server) => server.total > 0 || server.totalScore !== 0);
  const data = servers.slice(0, 10).map((server) => ({
    name: server.name,
    Approved: server.approved,
    Pending: server.pending,
    Removed: server.removed,
    Score: server.totalScore,
  }));

  return (
    <ChartFrame empty={!hasServers} emptyLabel="No server quote comparison data">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="name" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={42} />
          <YAxis orientation="right" yAxisId="score" tick={{ fill: "#667085", fontSize: 12 }} tickFormatter={(value) => formatCompactNumber(Number(value))} tickLine={false} axisLine={false} width={46} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Approved" fill="#059669" isAnimationActive={false} radius={[4, 4, 0, 0]} stackId="quotes" />
          <Bar dataKey="Pending" fill="#d97706" isAnimationActive={false} radius={[4, 4, 0, 0]} stackId="quotes" />
          <Bar dataKey="Removed" fill="#e11d48" isAnimationActive={false} radius={[4, 4, 0, 0]} stackId="quotes" />
          <Line dataKey="Score" dot={false} isAnimationActive={false} stroke="#2563eb" strokeWidth={2.2} type="monotone" yAxisId="score" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function ActivityComparisonChart({
  series,
  metric = "messages",
}: {
  series: DashboardActivityComparisonSeries[];
  metric?: "messages" | "xp" | "activeUsers";
}) {
  const { data, visibleSeries } = useMemo(() => {
    const nextVisibleSeries = series
      .filter((item) => item.points.some((point) => point[metric] > 0))
      .slice(0, 7)
      .map((item, index) => ({ ...item, dataKey: `series-${index}` }));
    const dates = Array.from(
      new Set(nextVisibleSeries.flatMap((item) => item.points.map((point) => point.dateUtc.slice(0, 10)))),
    ).sort();
    const pointsBySeries = nextVisibleSeries.map(
      (item) => new Map(item.points.map((point) => [point.dateUtc.slice(0, 10), point])),
    );
    const data = dates.map((date) => {
      const row: Record<string, string | number> = { date: formatShortDate(date) };
      nextVisibleSeries.forEach((item, index) => {
        row[item.dataKey] = pointsBySeries[index].get(date)?.[metric] ?? 0;
      });
      return row;
    });

    return { data, visibleSeries: nextVisibleSeries };
  }, [metric, series]);

  return (
    <ChartFrame empty={visibleSeries.length === 0} emptyLabel="No comparison data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={300}>
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
          {visibleSeries.map((item, index) => (
            <Line
              dataKey={item.dataKey}
              dot={false}
              isAnimationActive={false}
              key={item.key}
              name={item.label}
              stroke={chartColors[index % chartColors.length]}
              strokeWidth={item.kind === "time-range" ? 2.6 : 2}
              type="monotone"
            />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function ActivityDistributionDonut({
  data,
  metric = "xp",
}: {
  data: DashboardActivityDistributionPoint[];
  metric?: "xp" | "messages";
}) {
  const chartData = data.slice(0, 8).map((item) => ({
    name: item.label,
    value: metric === "xp" ? item.xp : item.messages,
  }));
  const total = chartData.reduce((sum, item) => sum + item.value, 0);

  if (chartData.length === 0 || total === 0) {
    return <EmptyState label="No distribution data in this window" />;
  }

  return (
    <div className="grid min-h-[220px] min-w-0 grid-cols-[minmax(0,1fr)_auto] items-center gap-3" style={{ minHeight: 220, minWidth: 0 }}>
      <ResponsiveContainer width="100%" height={220} minWidth={0}>
        <PieChart>
          <Tooltip content={<TooltipContent />} />
          <Pie data={chartData} dataKey="value" innerRadius={54} isAnimationActive={false} outerRadius={82} paddingAngle={2} stroke="var(--card)" strokeWidth={2}>
            {chartData.map((entry, index) => (
              <Cell fill={chartColors[index % chartColors.length]} key={entry.name} stroke="var(--card)" strokeWidth={2} />
            ))}
          </Pie>
        </PieChart>
      </ResponsiveContainer>
      <div className="grid min-w-32 gap-2 text-sm">
        {chartData.map((entry, index) => (
          <div className="flex items-center justify-between gap-3" key={entry.name}>
            <span className="flex min-w-0 items-center gap-2 text-muted">
              <span className="h-2.5 w-2.5 shrink-0 rounded-sm" style={{ backgroundColor: chartColors[index % chartColors.length] }} />
              <span className="truncate">{entry.name}</span>
            </span>
            <span className="font-semibold text-foreground">{formatCompactNumber(entry.value)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function ActivityDistributionBars({
  data,
  metric = "messages",
}: {
  data: DashboardActivityDistributionPoint[];
  metric?: "xp" | "messages";
}) {
  const rows = data.slice(0, 10).map((item) => ({
    ...item,
    value: metric === "xp" ? item.xp : item.messages,
  }));
  const max = Math.max(1, ...rows.map((item) => item.value));

  if (rows.length === 0) {
    return <EmptyState label="No contribution data in this window" />;
  }

  return (
    <div className="grid gap-2">
      {rows.map((item, index) => (
        <div className="rounded-lg border border-border bg-white p-3" key={`${item.kind}-${item.id}`}>
          <div className="flex items-center justify-between gap-3 text-sm">
            <span className="truncate font-semibold text-foreground">{item.label}</span>
            <span className="shrink-0 text-muted">{formatCompactNumber(item.value)} - {item.sharePercent.toFixed(1)}%</span>
          </div>
          <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-100">
            <div
              className="h-full rounded-full"
              style={{
                width: `${Math.max(2, (item.value / max) * 100)}%`,
                backgroundColor: chartColors[index % chartColors.length],
              }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}

export function ActivityTreemap({ data }: { data: DashboardActivityDistributionPoint[] }) {
  const rows = data.slice(0, 10);
  const total = rows.reduce((sum, row) => sum + row.messages, 0);

  if (rows.length === 0 || total === 0) {
    return <EmptyState label="No treemap data in this window" />;
  }

  return (
    <div className="grid min-h-[260px] grid-cols-2 gap-2 md:grid-cols-4">
      {rows.map((row, index) => (
        <div
          className="grid content-between rounded-lg border border-border p-3 text-white"
          key={`${row.kind}-${row.id}`}
          style={{
            minHeight: `${Math.max(92, 90 + row.messages / total * 260)}px`,
            backgroundColor: chartColors[index % chartColors.length],
          }}
        >
          <div className="truncate text-sm font-semibold">{row.label}</div>
          <div className="text-xs opacity-90">{formatCompactNumber(row.messages)} messages</div>
        </div>
      ))}
    </div>
  );
}

export function ParetoActivityChart({ points }: { points: DashboardActivityParetoPoint[] }) {
  const hasPoints = points.some((point) => point.value > 0);
  const data = points.slice(0, 10).map((point) => ({
    label: point.label,
    Messages: point.value,
    "Cumulative %": point.cumulativePercent,
  }));

  return (
    <ChartFrame empty={!hasPoints} emptyLabel="No Pareto data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ComposedChart data={data} margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="label" tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} />
          <YAxis
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            width={42}
          />
          <YAxis orientation="right" yAxisId="percent" domain={[0, 100]} tick={{ fill: "#667085", fontSize: 12 }} tickLine={false} axisLine={false} width={38} />
          <Tooltip content={<TooltipContent />} />
          <Bar dataKey="Messages" fill="#0891b2" isAnimationActive={false} radius={[4, 4, 0, 0]} />
          <Line dataKey="Cumulative %" dot={false} isAnimationActive={false} stroke="#d97706" strokeWidth={2.2} type="monotone" yAxisId="percent" />
        </ComposedChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function ActivityScatterChart({
  points,
  xMetric = "messages",
}: {
  points: DashboardActivityScatterPoint[];
  xMetric?: "messages" | "averageMessageLength";
}) {
  const hasPoints = points.some((point) => point.xp > 0 || point.messages > 0);
  const data = points.slice(0, 30).map((point) => ({
    name: point.label,
    kind: point.kind,
    x: xMetric === "messages" ? point.messages : point.averageMessageLength,
    y: point.xp,
    z: Math.max(32, point.messages),
  }));

  return (
    <ChartFrame empty={!hasPoints} emptyLabel="No scatter data in this window">
      <ResponsiveContainer width="100%" height="100%" minHeight={280}>
        <ScatterChart margin={{ left: 4, right: 8, top: 14, bottom: 0 }}>
          <CartesianGrid stroke="#e5e7eb" strokeDasharray="3 3" vertical={false} />
          <XAxis
            dataKey="x"
            name={xMetric === "messages" ? "Messages" : "Avg length"}
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            type="number"
          />
          <YAxis
            dataKey="y"
            name="XP"
            tick={{ fill: "#667085", fontSize: 12 }}
            tickFormatter={(value) => formatCompactNumber(Number(value))}
            tickLine={false}
            axisLine={false}
            type="number"
            width={46}
          />
          <ZAxis dataKey="z" range={[60, 420]} />
          <Tooltip content={<TooltipContent />} cursor={{ strokeDasharray: "3 3" }} />
          <Scatter data={data} fill="#2563eb" isAnimationActive={false} name="Activity" />
        </ScatterChart>
      </ResponsiveContainer>
    </ChartFrame>
  );
}

export function MessageLengthBoxPlotChart({ points }: { points: DashboardActivityBoxPlotPoint[] }) {
  const rows = points.filter((point) => point.count > 0).slice(0, 6);
  const max = Math.max(1, ...rows.map((point) => point.maximum));

  if (rows.length === 0) {
    return <EmptyState label="No message-length box plot data" />;
  }

  return (
    <div className="grid min-h-[260px] gap-4 rounded-lg border border-border bg-white p-4">
      {rows.map((point, index) => (
        <div className="grid grid-cols-[minmax(6rem,8rem)_minmax(0,1fr)_auto] items-center gap-3" key={`${point.kind}-${point.label}`}>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{point.label}</div>
            <div className="text-xs text-muted">{formatCompactNumber(point.count)} messages</div>
          </div>
          <div className="relative h-8 rounded-md bg-slate-100">
            <div
              className="absolute top-1/2 h-px -translate-y-1/2 bg-slate-400"
              style={{ left: `${(point.minimum / max) * 100}%`, right: `${100 - (point.maximum / max) * 100}%` }}
            />
            <div
              className="absolute top-1/2 h-5 -translate-y-1/2 rounded-md border border-white/70"
              style={{
                left: `${(point.q1 / max) * 100}%`,
                width: `${Math.max(2, ((point.q3 - point.q1) / max) * 100)}%`,
                backgroundColor: chartColors[index % chartColors.length],
              }}
            />
            <span
              className="absolute top-1/2 h-7 w-0.5 -translate-y-1/2 bg-foreground"
              style={{ left: `${(point.median / max) * 100}%` }}
            />
          </div>
          <div className="text-right text-xs text-muted">
            <div>{point.median.toFixed(1)} med</div>
            <div>{point.average.toFixed(1)} avg</div>
          </div>
        </div>
      ))}
    </div>
  );
}

export function ChannelHourHeatmap({ cells }: { cells: DashboardChannelHourHeatmapCell[] }) {
  if (cells.length === 0) {
    return <EmptyState label="No channel-hour data in this window" />;
  }

  const channels = Array.from(new Map(cells.map((cell) => [cell.channelId, cell.channelName])).entries());
  const max = Math.max(1, ...cells.map((cell) => cell.messages));
  const lookup = new Map(cells.map((cell) => [`${cell.channelId}-${cell.hourUtc}`, cell]));

  return (
    <div className="scrollbar-clean min-h-[260px] overflow-x-auto pb-1">
      <div className="grid w-max min-w-full gap-1 text-xs" style={{ gridTemplateColumns: "minmax(7rem,9rem) repeat(24, minmax(1.25rem,1fr))" }}>
        <span />
        {Array.from({ length: 24 }, (_, hour) => (
          <span className="text-center text-muted" key={hour}>{hour % 3 === 0 ? hour : ""}</span>
        ))}
        {channels.map(([channelId, channelName]) => (
          <ChannelHourHeatmapRow channelId={channelId} channelName={channelName} key={channelId} lookup={lookup} max={max} />
        ))}
      </div>
    </div>
  );
}

export function ServerDayHeatmap({ cells }: { cells: DashboardServerDayActivityCell[] }) {
  if (cells.length === 0) {
    return <EmptyState label="No server-day data in this window" />;
  }

  const dates = Array.from(new Set(cells.map((cell) => cell.dateUtc.slice(0, 10)))).sort();
  const servers = Array.from(new Map(cells.map((cell) => [cell.guildId, cell.guildName])).entries());
  const max = Math.max(1, ...cells.map((cell) => cell.messages));
  const lookup = new Map(cells.map((cell) => [`${cell.guildId}-${cell.dateUtc.slice(0, 10)}`, cell]));
  const columnWidth = dates.length <= 21 ? "3rem" : dates.length <= 60 ? "2.2rem" : "1.55rem";

  return (
    <div className="scrollbar-clean min-h-[260px] overflow-x-auto pb-1">
      <div className="grid w-max min-w-full gap-1 text-xs" style={{ gridTemplateColumns: `minmax(7rem,9rem) repeat(${dates.length}, ${columnWidth})` }}>
        <span />
        {dates.map((date, index) => (
          <span className="truncate text-center text-muted" key={date}>{dates.length <= 21 || index % 7 === 0 ? formatShortDate(date) : ""}</span>
        ))}
        {servers.map(([guildId, guildName]) => (
          <ServerDayHeatmapRow dates={dates} guildId={guildId} guildName={guildName} key={guildId} lookup={lookup} max={max} />
        ))}
      </div>
    </div>
  );
}

export function ActivityHeatmap({ cells }: { cells: DashboardHeatmapCell[] }) {
  const { labelsByDay, lookup, max } = useMemo(() => {
    const labelsByDay = new Map<number, string>();
    const lookup = new Map<string, DashboardHeatmapCell>();

    for (const cell of cells) {
      labelsByDay.set(cell.dayOfWeek, cell.dayLabel);
      lookup.set(`${cell.dayOfWeek}-${cell.hourUtc}`, cell);
    }

    return {
      labelsByDay,
      lookup,
      max: maxBy(cells, (cell) => cell.messages),
    };
  }, [cells]);

  if (cells.length === 0) {
    return <EmptyState label="No activity heatmap data in this window" />;
  }

  return (
    <div className="grid gap-2 pb-1">
      <div className="grid w-full grid-cols-[2rem_repeat(24,minmax(0,1fr))] gap-1 text-xs text-muted sm:grid-cols-[3rem_repeat(24,minmax(0,1fr))]">
        <span />
        {Array.from({ length: 24 }, (_, hour) => (
          <span className="min-w-0 text-center" key={hour}>
            {hour % 3 === 0 ? hour : ""}
          </span>
        ))}
        {dayLabels.map((fallbackLabel, dayIndex) => (
          <ActivityHeatmapRow
            dayIndex={dayIndex}
            key={dayIndex}
            label={labelsByDay.get(dayIndex) ?? fallbackLabel}
            lookup={lookup}
            max={max}
          />
        ))}
      </div>
    </div>
  );
}

export function ChannelActivityHeatmap({ cells }: { cells: DashboardChannelHeatmapCell[] }) {
  if (cells.length === 0) {
    return <EmptyState label="No channel heatmap data in this window" />;
  }

  const dates = Array.from(new Set(cells.map((cell) => cell.dateUtc.slice(0, 10)))).sort();
  const channelRows = Array.from(
    cells
      .reduce((map, cell) => {
        const row = map.get(cell.channelId) ?? {
          channelId: cell.channelId,
          channelName: cell.channelName,
          messages: 0,
        };
        row.messages += cell.messages;
        map.set(cell.channelId, row);
        return map;
      }, new Map<string, { channelId: string; channelName: string; messages: number }>())
      .values(),
  ).sort((a, b) => b.messages - a.messages);
  const byChannelAndDate = new Map(cells.map((cell) => [`${cell.channelId}-${cell.dateUtc.slice(0, 10)}`, cell]));
  const max = Math.max(1, ...cells.map((cell) => cell.messages));
  const columnWidth = dates.length <= 14 ? "3rem" : dates.length <= 45 ? "2.35rem" : "1.75rem";

  return (
    <div className="scrollbar-clean min-h-[260px] overflow-x-auto pb-1">
      <div
        className="grid w-max min-w-full gap-1 text-xs"
        style={{ gridTemplateColumns: `minmax(7rem, 9rem) repeat(${dates.length}, ${columnWidth})` }}
      >
        <span className="sticky left-0 z-10 bg-white text-muted" />
        {dates.map((date, index) => (
          <span className="truncate text-center text-muted" key={date}>
            {dates.length <= 21 || index % 7 === 0 ? formatShortDate(date) : ""}
          </span>
        ))}
        {channelRows.map((channel) => (
          <ChannelActivityHeatmapRow
            channel={channel}
            dates={dates}
            key={channel.channelId}
            max={max}
            rowLookup={byChannelAndDate}
          />
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

function ChannelHourHeatmapRow({
  channelId,
  channelName,
  lookup,
  max,
}: {
  channelId: string;
  channelName: string;
  lookup: Map<string, DashboardChannelHourHeatmapCell>;
  max: number;
}) {
  return (
    <>
      <span className="sticky left-0 z-10 truncate rounded-[3px] bg-white py-1 pr-2 text-xs font-medium text-foreground">
        {channelName}
      </span>
      {Array.from({ length: 24 }, (_, hour) => {
        const cell = lookup.get(`${channelId}-${hour}`);
        const messages = cell?.messages ?? 0;
        const opacity = messages === 0 ? 0.08 : 0.18 + (messages / max) * 0.72;

        return (
          <span
            className="aspect-square min-h-4 rounded-[3px] border border-border"
            key={`${channelId}-${hour}`}
            style={{ backgroundColor: `rgba(8, 145, 178, ${opacity})` }}
            title={`${channelName} ${hour}:00 UTC: ${messages} messages`}
          />
        );
      })}
    </>
  );
}

function ServerDayHeatmapRow({
  dates,
  guildId,
  guildName,
  lookup,
  max,
}: {
  dates: string[];
  guildId: number;
  guildName: string;
  lookup: Map<string, DashboardServerDayActivityCell>;
  max: number;
}) {
  return (
    <>
      <span className="sticky left-0 z-10 truncate rounded-[3px] bg-white py-1 pr-2 text-xs font-medium text-foreground">
        {guildName}
      </span>
      {dates.map((date) => {
        const cell = lookup.get(`${guildId}-${date}`);
        const messages = cell?.messages ?? 0;
        const opacity = messages === 0 ? 0.08 : 0.18 + (messages / max) * 0.72;

        return (
          <span
            className="aspect-square min-h-4 rounded-[3px] border border-border"
            key={`${guildId}-${date}`}
            style={{ backgroundColor: `rgba(37, 99, 235, ${opacity})` }}
            title={`${guildName} ${date}: ${messages} messages`}
          />
        );
      })}
    </>
  );
}

function ActivityHeatmapRow({
  dayIndex,
  label,
  lookup,
  max,
}: {
  dayIndex: number;
  label: string;
  lookup: Map<string, DashboardHeatmapCell>;
  max: number;
}) {
  return (
    <>
      <span className="flex min-w-0 items-center text-[10px] font-medium sm:text-xs">{label}</span>
      {Array.from({ length: 24 }, (_, hour) => {
        const cell = lookup.get(`${dayIndex}-${hour}`);
        const messages = cell?.messages ?? 0;
        const opacity = messages === 0 ? 0.08 : 0.18 + (messages / max) * 0.72;

        return (
          <span
            className="aspect-square min-h-2 rounded-[3px] border border-border"
            key={`${dayIndex}-${hour}`}
            style={{ backgroundColor: `rgba(8, 145, 178, ${opacity})` }}
            title={`${label} ${hour}:00 UTC: ${messages} messages`}
          />
        );
      })}
    </>
  );
}

function ChannelActivityHeatmapRow({
  channel,
  dates,
  max,
  rowLookup,
}: {
  channel: { channelId: string; channelName: string; messages: number };
  dates: string[];
  max: number;
  rowLookup: Map<string, DashboardChannelHeatmapCell>;
}) {
  return (
    <>
      <span className="sticky left-0 z-10 truncate rounded-[3px] bg-white py-1 pr-2 text-xs font-medium text-foreground">
        {channel.channelName}
      </span>
      {dates.map((date) => {
        const cell = rowLookup.get(`${channel.channelId}-${date}`);
        const messages = cell?.messages ?? 0;
        const opacity = messages === 0 ? 0.08 : 0.18 + (messages / max) * 0.72;

        return (
          <span
            className="aspect-square min-h-4 rounded-[3px] border border-border"
            key={`${channel.channelId}-${date}`}
            style={{ backgroundColor: `rgba(37, 99, 235, ${opacity})` }}
            title={`${channel.channelName} ${date}: ${messages} messages`}
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
  payload?: Array<{ name?: string | number; value?: unknown; color?: string; dataKey?: string | number }>;
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
        {payload.map((item, index) => {
          const name = item.name ?? item.dataKey ?? "Value";

          return (
            <div className="flex items-center gap-2 text-muted" key={`${name}-${index}`}>
              <span className="h-2 w-2 rounded-sm" style={{ backgroundColor: item.color }} />
              <span>{name}</span>
              <span className="font-medium text-foreground">
                {formatTooltipValue(item.value, currency)}
              </span>
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

function formatTooltipValue(value: unknown, currency: boolean) {
  const numericValue = typeof value === "number" ? value : Number(value);

  if (Number.isFinite(numericValue)) {
    return currency ? formatCurrency(numericValue) : formatCompactNumber(numericValue);
  }

  return String(value ?? "");
}

function getUtcTimestamp(value: string) {
  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) ? timestamp : 0;
}

function getUtcDayOfWeek(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 0 : date.getUTCDay();
}

function getUtcDayOfMonth(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.getUTCDate();
}

function maxBy<T>(items: T[], getValue: (item: T) => number) {
  let max = 1;

  for (const item of items) {
    max = Math.max(max, getValue(item));
  }

  return max;
}

function useChartId(prefix: string) {
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, "");
  return `${prefix}-${id}`;
}
