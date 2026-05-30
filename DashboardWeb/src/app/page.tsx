import Image from "next/image";
import type React from "react";
import {
  Activity,
  Banknote,
  Bot,
  Clock3,
  Database,
  MessageSquareText,
  Quote,
  Server,
  ShieldCheck,
  TrendingUp,
  Users,
} from "lucide-react";
import { ActivityChart } from "@/components/dashboard/activity-chart";
import { DashboardFilters } from "@/components/dashboard/filters";
import { ThemeToggle } from "@/components/dashboard/theme-toggle";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getDashboardData } from "@/lib/dashboard-api";
import type {
  DashboardGuildSummary,
  DashboardLeaderboardItem,
  DashboardQuoteItem,
} from "@/lib/types";
import {
  clamp,
  formatCompactNumber,
  formatCurrency,
  formatInteger,
  formatRelativeDate,
} from "@/lib/utils";

export const dynamic = "force-dynamic";

type SearchParams = Record<string, string | string[] | undefined>;

export default async function DashboardPage({
  searchParams,
}: {
  searchParams?: Promise<SearchParams> | SearchParams;
}) {
  const params = await Promise.resolve(searchParams ?? {});
  const days = clamp(parseNumber(getParam(params.days), 30), 1, 90);
  const guildId = parseOptionalNumber(getParam(params.guildId));
  const data = await getDashboardData({ guildId, days });
  const selectedGuild = data.guilds.find((guild) => guild.id === guildId);

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-[1500px] gap-5 px-4 py-4 sm:px-6 lg:px-8">
      <section className="flex flex-col gap-4 rounded-lg border border-border bg-white p-4 shadow-sm lg:flex-row lg:items-center lg:justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <Image
            src="/morpheus.png"
            width={48}
            height={48}
            alt="Morpheus"
            className="h-12 w-12 rounded-lg border border-border object-cover"
            priority
          />
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-xl font-semibold text-foreground sm:text-2xl">Morpheus Dashboard</h1>
              <Badge variant={data.usingDemoData ? "warning" : "success"}>
                {data.usingDemoData ? "Demo data" : "Live API"}
              </Badge>
            </div>
            <p className="mt-1 text-sm text-muted">
              {selectedGuild ? selectedGuild.name : "All guilds"} · generated {formatRelativeDate(data.overview.generatedAtUtc)}
            </p>
          </div>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <DashboardFilters guilds={data.guilds} selectedGuildId={guildId} days={days} />
          <ThemeToggle />
        </div>
      </section>

      {data.usingDemoData && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Showing demo data because the dashboard API did not respond. {data.error}
        </div>
      )}

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <StatCard
          icon={Users}
          label="Tracked users"
          value={formatInteger(data.overview.system.users)}
          meta={`${formatInteger(data.overview.activity.activeUsersLast30Days)} active in 30d`}
          tone="blue"
        />
        <StatCard
          icon={TrendingUp}
          label="Total XP"
          value={formatCompactNumber(data.overview.activity.totalXp)}
          meta={`${formatCompactNumber(data.overview.activity.xpLast30Days)} in this window`}
          tone="green"
        />
        <StatCard
          icon={MessageSquareText}
          label="Messages"
          value={formatCompactNumber(data.overview.activity.totalMessages)}
          meta={`${formatCompactNumber(data.overview.activity.messagesLast30Days)} in 30d`}
          tone="cyan"
        />
        <StatCard
          icon={Banknote}
          label="Economy"
          value={formatCurrency(data.overview.economy.totalBalance + data.overview.economy.stockPortfolioValue)}
          meta={`${formatCurrency(data.overview.economy.stockPortfolioValue)} in stocks`}
          tone="amber"
        />
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.5fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity</CardTitle>
              <CardDescription>
                XP and message flow across the selected {days}-day window.
              </CardDescription>
            </div>
            <Badge variant="muted">
              <Activity className="h-3.5 w-3.5" aria-hidden="true" />
              {formatRelativeDate(data.overview.activity.lastActivityAtUtc)}
            </Badge>
          </CardHeader>
          <CardContent>
            <ActivityChart points={data.activity.points} />
          </CardContent>
        </Card>

        <div className="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle>Bot Health</CardTitle>
              <Badge variant="success">
                <ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" />
                Running
              </Badge>
            </CardHeader>
            <CardContent className="grid gap-3">
              <HealthRow icon={Clock3} label="Uptime" value={formatUptime(data.overview.uptimeSeconds)} />
              <HealthRow icon={Server} label="Guilds" value={formatInteger(data.overview.system.guilds)} />
              <HealthRow icon={Database} label="Logs 24h" value={formatInteger(data.overview.logs.last24Hours)} />
              <HealthRow icon={Quote} label="Quotes pending" value={formatInteger(data.overview.quotes.pending)} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Quotes</CardTitle>
              <Badge variant="default">{formatInteger(data.overview.quotes.totalScores)} score</Badge>
            </CardHeader>
            <CardContent className="grid grid-cols-3 gap-3">
              <MiniStat label="Approved" value={data.overview.quotes.approved} />
              <MiniStat label="Pending" value={data.overview.quotes.pending} />
              <MiniStat label="Removed" value={data.overview.quotes.removed} />
            </CardContent>
          </Card>
        </div>
      </section>

      <section className="grid gap-4 xl:grid-cols-2">
        <LeaderboardCard title="XP Leaders" metric="XP" items={data.xpLeaderboard.items} />
        <LeaderboardCard title="Message Leaders" metric="messages" items={data.messageLeaderboard.items} />
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.1fr_1fr]">
        <GuildsCard guilds={data.guilds} />
        <QuotesCard quotes={data.quotes.items} total={data.quotes.total} />
      </section>
    </main>
  );
}

function StatCard({
  icon: Icon,
  label,
  value,
  meta,
  tone,
}: {
  icon: React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;
  label: string;
  value: string;
  meta: string;
  tone: "blue" | "green" | "cyan" | "amber";
}) {
  const toneClass = {
    blue: "bg-blue-50 text-blue-700",
    green: "bg-emerald-50 text-emerald-700",
    cyan: "bg-cyan-50 text-cyan-700",
    amber: "bg-amber-50 text-amber-700",
  }[tone];

  return (
    <Card>
      <CardContent className="flex h-32 items-center justify-between gap-4">
        <div className="min-w-0">
          <div className="text-sm font-medium text-muted">{label}</div>
          <div className="mt-2 text-3xl font-semibold text-foreground">{value}</div>
          <div className="mt-2 truncate text-sm text-muted">{meta}</div>
        </div>
        <div className={`grid h-11 w-11 shrink-0 place-items-center rounded-lg ${toneClass}`}>
          <Icon className="h-5 w-5" aria-hidden />
        </div>
      </CardContent>
    </Card>
  );
}

function HealthRow({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-slate-50 px-3 py-2">
      <div className="flex items-center gap-2 text-sm text-muted">
        <Icon className="h-4 w-4" aria-hidden />
        <span>{label}</span>
      </div>
      <span className="text-sm font-semibold text-foreground">{value}</span>
    </div>
  );
}

function MiniStat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-border bg-slate-50 p-3">
      <div className="text-xs font-medium text-muted">{label}</div>
      <div className="mt-2 text-xl font-semibold text-foreground">{formatInteger(value)}</div>
    </div>
  );
}

function LeaderboardCard({
  title,
  metric,
  items,
}: {
  title: string;
  metric: string;
  items: DashboardLeaderboardItem[];
}) {
  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>{title}</CardTitle>
          <CardDescription>Top users for the selected window.</CardDescription>
        </div>
        <Bot className="h-5 w-5 text-muted" aria-hidden />
      </CardHeader>
      <CardContent>
        <div className="grid gap-2">
          {items.length === 0 ? (
            <EmptyRow label="No leaderboard data" />
          ) : (
            items.map((item) => (
              <div
                className="grid grid-cols-[3rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2"
                key={`${title}-${item.userId}`}
              >
                <div className="text-sm font-semibold text-muted">#{item.rank}</div>
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-foreground">{item.username}</div>
                  <div className="text-xs text-muted">
                    {item.level !== null ? `Level ${item.level}` : formatRelativeDate(item.lastActivityAtUtc)}
                  </div>
                </div>
                <div className="text-right text-sm font-semibold text-foreground">
                  {formatCompactNumber(item.value)}
                  <span className="ml-1 text-xs font-medium text-muted">{metric}</span>
                </div>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function GuildsCard({ guilds }: { guilds: DashboardGuildSummary[] }) {
  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>Guilds</CardTitle>
          <CardDescription>Tracked servers and all-time activity.</CardDescription>
        </div>
        <Server className="h-5 w-5 text-muted" aria-hidden />
      </CardHeader>
      <CardContent>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[560px] border-separate border-spacing-0 text-sm">
            <thead>
              <tr className="text-left text-xs font-semibold text-muted">
                <th className="border-b border-border pb-2">Name</th>
                <th className="border-b border-border pb-2 text-right">Users</th>
                <th className="border-b border-border pb-2 text-right">Messages</th>
                <th className="border-b border-border pb-2 text-right">XP</th>
                <th className="border-b border-border pb-2 text-right">Quotes</th>
              </tr>
            </thead>
            <tbody>
              {guilds.map((guild) => (
                <tr key={guild.id}>
                  <td className="border-b border-border py-3 pr-3">
                    <div className="font-semibold text-foreground">{guild.name}</div>
                    <div className="text-xs text-muted">{guild.discordId}</div>
                  </td>
                  <td className="border-b border-border py-3 text-right">{formatInteger(guild.trackedUsers)}</td>
                  <td className="border-b border-border py-3 text-right">{formatCompactNumber(guild.messages)}</td>
                  <td className="border-b border-border py-3 text-right">{formatCompactNumber(guild.xp)}</td>
                  <td className="border-b border-border py-3 text-right">{formatInteger(guild.approvedQuotes)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}

function QuotesCard({ quotes, total }: { quotes: DashboardQuoteItem[]; total: number }) {
  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>Top Quotes</CardTitle>
          <CardDescription>{formatInteger(total)} approved quotes in scope.</CardDescription>
        </div>
        <Quote className="h-5 w-5 text-muted" aria-hidden />
      </CardHeader>
      <CardContent>
        <div className="grid gap-3">
          {quotes.length === 0 ? (
            <EmptyRow label="No quotes found" />
          ) : (
            quotes.slice(0, 6).map((quote) => (
              <article className="rounded-lg border border-border bg-white p-3" key={quote.id}>
                <div className="flex items-start justify-between gap-3">
                  <div className="text-sm font-semibold text-foreground">#{quote.id}</div>
                  <Badge variant={quote.score >= 0 ? "success" : "danger"}>{quote.score >= 0 ? "+" : ""}{quote.score}</Badge>
                </div>
                <p className="mt-2 line-clamp-3 text-sm leading-6 text-foreground">{quote.content}</p>
                <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-muted">
                  <span>{quote.author}</span>
                  <span>·</span>
                  <span>{formatRelativeDate(quote.insertedAtUtc)}</span>
                </div>
              </article>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function EmptyRow({ label }: { label: string }) {
  return <div className="rounded-lg border border-dashed border-border p-5 text-center text-sm text-muted">{label}</div>;
}

function parseNumber(value: string | undefined, fallback: number) {
  if (!value) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function parseOptionalNumber(value: string | undefined) {
  if (!value) {
    return undefined;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : undefined;
}

function getParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function formatUptime(seconds: number) {
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (days > 0) {
    return `${days}d ${hours}h`;
  }

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }

  return `${minutes}m`;
}
