import Image from "next/image";
import type React from "react";
import {
  Activity,
  AlertTriangle,
  Banknote,
  Bell,
  Bot,
  Clock3,
  Database,
  Gamepad2,
  Gauge,
  MessageSquareText,
  Quote,
  Server,
  Settings,
  ShieldCheck,
  TrendingUp,
  Users,
  Wallet,
} from "lucide-react";
import { ActivityChart } from "@/components/dashboard/activity-chart";
import { DashboardFilters } from "@/components/dashboard/filters";
import {
  ActivityHeatmap,
  ActivityInsightChart,
  ButtonGameChart,
  CategoryBars,
  DonutChart,
  EconomyFlowChart,
  LogTimelineChart,
  MoneyFlowView,
} from "@/components/dashboard/insight-charts";
import { ThemeToggle } from "@/components/dashboard/theme-toggle";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getDashboardData } from "@/lib/dashboard-api";
import type {
  DashboardChannelActivity,
  DashboardGuildSettingsSummary,
  DashboardGuildSummary,
  DashboardLeaderboardItem,
  DashboardLogItem,
  DashboardQuoteAuthorSummary,
  DashboardQuoteItem,
  DashboardStockMover,
  DashboardUserActivitySummary,
  DashboardWealthUser,
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
type DashboardScope = "global" | "server" | "user" | "channel";
type SortDirection = "asc" | "desc";

export default async function DashboardPage({
  searchParams,
}: {
  searchParams?: Promise<SearchParams> | SearchParams;
}) {
  const params = await Promise.resolve(searchParams ?? {});
  const days = clamp(parseNumber(getParam(params.days), 30), 1, 90);
  const requestedGuildId = parseOptionalNumber(getParam(params.guildId));
  const requestedUserId = parseOptionalNumber(getParam(params.userId));
  const requestedChannelId = parseOptionalString(getParam(params.channelId));
  const requestedScope = parseScope(getParam(params.scope), requestedGuildId, requestedUserId, requestedChannelId);
  const sortDirection = parseSortDirection(getParam(params.sortDirection));
  const minActivity = clamp(parseNumber(getParam(params.minActivity), 1), 0, 100000);
  const filters = normalizeDashboardFilters({
    guildId: requestedGuildId,
    userId: requestedUserId,
    channelId: requestedChannelId,
    days,
    scope: requestedScope,
    sortDirection,
    minActivity,
  });
  const data = await getDashboardData(filters);
  const { guildId, userId, channelId, scope } = filters;
  const selectedGuild = data.guilds.find((guild) => guild.id === guildId);
  const selectedUser = data.insights.filterOptions.users.find((user) => user.userId === userId);
  const selectedChannel = data.insights.filterOptions.channels.find((channel) => channel.discordId === channelId);
  const scopeLabel = selectedChannel?.name ?? selectedUser?.username ?? selectedGuild?.name ?? "All Morpheus data";
  const insights = data.insights;

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-[1540px] gap-5 px-4 py-4 sm:px-6 lg:px-8">
      <section className="rounded-lg border border-border bg-white shadow-sm">
        <div className="flex flex-col gap-4 p-4 xl:flex-row xl:items-center xl:justify-between">
          <div className="flex min-w-0 items-start gap-3">
            <Image
              src="/morpheus.png"
              width={52}
              height={52}
              alt="Morpheus"
              className="h-12 w-12 rounded-lg border border-border object-cover"
              priority
            />
            <div className="min-w-0 flex-1">
              <div className="grid gap-2">
                <h1 className="text-lg font-semibold leading-tight text-foreground sm:text-2xl">Morpheus Command Center</h1>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant={data.usingDemoData ? "warning" : "success"}>
                    {data.usingDemoData ? "Demo data" : "Live API"}
                  </Badge>
                  <Badge variant="muted">{scopeLabel}</Badge>
                </div>
              </div>
              <p className="mt-2 break-words text-sm text-muted">
                Generated {formatRelativeDate(data.overview.generatedAtUtc)} - {days} days
              </p>
            </div>
          </div>
          <div className="flex shrink-0 items-start gap-3">
            <ThemeToggle />
          </div>
        </div>
        <div className="border-t border-border p-4">
          <DashboardFilters
            channels={data.insights.filterOptions.channels}
            days={days}
            guilds={data.guilds}
            minActivity={minActivity}
            scope={scope}
            selectedChannelId={channelId}
            selectedGuildId={guildId}
            selectedUserId={userId}
            sortDirection={sortDirection}
            users={data.insights.filterOptions.users}
          />
        </div>
      </section>

      {data.usingDemoData && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Showing demo data because the dashboard API did not respond. {data.error}
        </div>
      )}

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
        <StatCard
          icon={Users}
          label="Tracked users"
          value={formatInteger(data.overview.system.users)}
          meta={`${formatInteger(insights.activity.activeUsers)} active in scope`}
          tone="blue"
        />
        <StatCard
          icon={MessageSquareText}
          label="Messages"
          value={formatCompactNumber(insights.activity.messages)}
          meta={`${formatPercent(insights.activity.trendPercent)} vs prior half`}
          tone="cyan"
        />
        <StatCard
          icon={TrendingUp}
          label="XP gained"
          value={formatCompactNumber(insights.activity.xp)}
          meta={`${insights.activity.xpPerMessage.toFixed(1)} XP per message`}
          tone="green"
        />
        <StatCard
          icon={Wallet}
          label="Net worth"
          value={formatCurrency(insights.economy.netWorth)}
          meta={`${formatCurrency(insights.economy.transactionVolume)} moved`}
          tone="amber"
        />
        <StatCard
          icon={Quote}
          label="Quote queue"
          value={formatInteger(insights.quotes.pending)}
          meta={`${formatInteger(insights.quotes.pendingApprovalRequests)} approvals open`}
          tone="rose"
        />
        <StatCard
          icon={Database}
          label="Logs"
          value={formatCompactNumber(data.overview.logs.total)}
          meta={`${formatInteger(data.overview.logs.last24Hours)} in 24h`}
          tone="slate"
        />
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.4fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Intelligence</CardTitle>
              <CardDescription>Daily messages, rolling average, and cumulative activity in the selected scope.</CardDescription>
            </div>
            <TrendBadge value={insights.activity.trendPercent} />
          </CardHeader>
          <CardContent className="grid gap-4">
            <ActivityInsightChart points={insights.activity.points} />
            <div className="grid gap-3 md:grid-cols-4">
              <MetricPill label="Avg length" value={`${insights.activity.averageMessageLength.toFixed(1)} chars`} />
              <MetricPill label="Messages/user" value={insights.activity.messagesPerActiveUser.toFixed(1)} />
              <MetricPill label="Peak hour" value={formatHourUtc(insights.activity.peakHourUtc)} />
              <MetricPill label="Channels" value={formatInteger(insights.activity.activeChannels)} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Bot Health</CardTitle>
              <CardDescription>Runtime, API status, and collection freshness.</CardDescription>
            </div>
            <Badge variant="success">
              <ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" />
              Running
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-3">
            <HealthRow icon={Clock3} label="Uptime" value={formatUptime(data.overview.uptimeSeconds)} />
            <HealthRow icon={Server} label="Servers" value={formatInteger(data.overview.system.guilds)} />
            <HealthRow icon={Banknote} label="Stock entities" value={formatInteger(data.overview.system.stocks)} />
            <HealthRow icon={Activity} label="Last activity" value={formatRelativeDate(data.overview.activity.lastActivityAtUtc)} />
            <HealthRow icon={Quote} label="Quote score" value={formatInteger(data.overview.quotes.totalScores)} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Heatmap</CardTitle>
              <CardDescription>UTC hour and day concentration for message volume.</CardDescription>
            </div>
            <Badge variant="muted">
              <Gauge className="h-3.5 w-3.5" aria-hidden="true" />
              {formatInteger(minActivity)} minimum
            </Badge>
          </CardHeader>
          <CardContent>
            <ActivityHeatmap cells={insights.heatmap} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Detail</CardTitle>
              <CardDescription>Daily XP and message volume side by side.</CardDescription>
            </div>
            <Badge variant="muted">{formatRelativeDate(data.overview.activity.lastActivityAtUtc)}</Badge>
          </CardHeader>
          <CardContent>
            <ActivityChart points={data.activity.points} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>User View</CardTitle>
              <CardDescription>Activity, XP, quotes, wallet value, portfolio value, and button-game score.</CardDescription>
            </div>
            <Bot className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <UsersTable users={insights.users} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Channel View</CardTitle>
              <CardDescription>Message density and XP contribution by channel.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <ChannelsTable channels={insights.channels} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-2">
        <LeaderboardCard title="XP Leaders" metric="XP" items={data.xpLeaderboard.items} />
        <LeaderboardCard title="Message Leaders" metric="messages" items={data.messageLeaderboard.items} />
      </section>

      <section className="grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Pipeline</CardTitle>
              <CardDescription>Approval state, score distribution, authors, and pending queue.</CardDescription>
            </div>
            <Badge variant={insights.quotes.pending > 0 ? "warning" : "success"}>
              {formatInteger(insights.quotes.pending)} pending
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <DonutChart data={insights.quotes.statuses} labelKey="status" />
            <CategoryBars data={insights.quotes.scoreHistogram.map((item) => ({ label: item.label, value: item.count }))} />
            <QuoteAuthors authors={insights.quotes.authors} />
          </CardContent>
        </Card>

        <div className="grid gap-4">
          <Card>
            <CardHeader>
              <div>
                <CardTitle>Pending Quote Review</CardTitle>
                <CardDescription>Newest quotes waiting for approval in the selected scope.</CardDescription>
              </div>
              <Quote className="h-5 w-5 text-muted" aria-hidden="true" />
            </CardHeader>
            <CardContent>
              <QuotesList emptyLabel="No pending quotes found" quotes={insights.quotes.recentPending} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <div>
                <CardTitle>Guilds</CardTitle>
                <CardDescription>Tracked servers and all-time collection totals.</CardDescription>
              </div>
              <Server className="h-5 w-5 text-muted" aria-hidden="true" />
            </CardHeader>
            <CardContent>
              <GuildsTable guilds={data.guilds} />
            </CardContent>
          </Card>
        </div>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.25fr_0.75fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Economy Flow</CardTitle>
              <CardDescription>Money entering and leaving wallets, with net movement over time.</CardDescription>
            </div>
            <Badge variant="muted">{formatCurrency(insights.economy.fees)} fees</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <EconomyFlowChart points={insights.economy.dailyFlow} />
            <div className="grid gap-3 md:grid-cols-3">
              <MetricPill label="Cash" value={formatCurrency(insights.economy.cashBalance)} />
              <MetricPill label="Portfolio" value={formatCurrency(insights.economy.portfolioValue)} />
              <MetricPill label="Active traders" value={formatInteger(insights.economy.activeTraders)} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Money Movement</CardTitle>
              <CardDescription>Flow-style view of transaction sources and destinations.</CardDescription>
            </div>
            <Wallet className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <MoneyFlowView flows={insights.economy.moneyFlows} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Transaction Mix</CardTitle>
              <CardDescription>Volume by economy event type.</CardDescription>
            </div>
            <Banknote className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <CategoryBars currency data={insights.economy.transactionTypes} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Wealth Leaderboard</CardTitle>
              <CardDescription>Cash plus marked-to-market portfolio value.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <WealthTable users={insights.economy.wealthLeaders} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Stock Market</CardTitle>
              <CardDescription>Market value, daily movers, and entity coverage.</CardDescription>
            </div>
            <Badge variant="default">{formatInteger(insights.stocks.stocks)} stocks</Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <div className="grid gap-3 sm:grid-cols-2">
              <MetricPill label="Market value" value={formatCurrency(insights.stocks.marketValue)} />
              <MetricPill label="Avg change" value={`${insights.stocks.averageDailyChangePercent.toFixed(2)}%`} />
            </div>
            <DonutChart data={insights.stocks.entityTypes} labelKey="label" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Market Movers</CardTitle>
              <CardDescription>Best and worst daily movement among tracked stock entities.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <StockMoverList title="Winners" movers={insights.stocks.winners} positive />
            <StockMoverList title="Losers" movers={insights.stocks.losers} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button Game</CardTitle>
              <CardDescription>Press volume, score yield, rolling engagement, and top players.</CardDescription>
            </div>
            <Badge variant="muted">{formatRelativeDate(insights.buttonGame.lastPressAtUtc)}</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <ButtonGameChart points={insights.buttonGame.daily} />
            <div className="grid gap-3 sm:grid-cols-3">
              <MetricPill label="Presses" value={formatInteger(insights.buttonGame.presses)} />
              <MetricPill label="Score" value={formatCompactNumber(insights.buttonGame.score)} />
              <MetricPill label="Avg score" value={insights.buttonGame.averageScore.toFixed(1)} />
            </div>
            <ButtonLeaders leaders={insights.buttonGame.leaders} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Operations Timeline</CardTitle>
              <CardDescription>Logs, warning density, errors, reminders, and temporary bans.</CardDescription>
            </div>
            <Badge variant={hasSevereLogs(insights.operations.logSeverities) ? "danger" : "success"}>
              {formatInteger(insights.operations.reminders.pending)} reminders
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <LogTimelineChart points={insights.operations.logTimeline} />
            <div className="grid gap-3 sm:grid-cols-3">
              <MetricPill label="Overdue reminders" value={formatInteger(insights.operations.reminders.overdue)} />
              <MetricPill label="Due 24h" value={formatInteger(insights.operations.reminders.dueNext24Hours)} />
              <MetricPill label="Temp bans" value={formatInteger(insights.operations.moderation.pendingTemporaryBans)} />
            </div>
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminder and Moderation Queue</CardTitle>
              <CardDescription>Upcoming reminders and temporary ban follow-through.</CardDescription>
            </div>
            <Bell className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-4">
            <ReminderList reminders={insights.operations.reminders.upcoming} />
            <ModerationList pending={insights.operations.moderation.pending} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Recent Logs</CardTitle>
              <CardDescription>Latest operational records captured by Morpheus.</CardDescription>
            </div>
            <AlertTriangle className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <LogList logs={insights.operations.recentLogs} />
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Settings</CardTitle>
              <CardDescription>Feature flags and quote approval thresholds by server.</CardDescription>
            </div>
            <Settings className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <SettingsTable settings={insights.settings} />
          </CardContent>
        </Card>
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
  tone: "blue" | "green" | "cyan" | "amber" | "rose" | "slate";
}) {
  const toneClass = {
    blue: "bg-blue-50 text-blue-700",
    green: "bg-emerald-50 text-emerald-700",
    cyan: "bg-cyan-50 text-cyan-700",
    amber: "bg-amber-50 text-amber-700",
    rose: "bg-rose-50 text-rose-700",
    slate: "bg-slate-50 text-muted",
  }[tone];

  return (
    <Card>
      <CardContent className="flex h-32 items-center justify-between gap-4">
        <div className="min-w-0">
          <div className="text-sm font-medium text-muted">{label}</div>
          <div className="mt-2 text-2xl font-semibold text-foreground">{value}</div>
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
      <span className="text-right text-sm font-semibold text-foreground">{value}</span>
    </div>
  );
}

function MetricPill({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border bg-slate-50 p-3">
      <div className="text-xs font-medium uppercase tracking-normal text-muted">{label}</div>
      <div className="mt-2 text-lg font-semibold text-foreground">{value}</div>
    </div>
  );
}

function TrendBadge({ value }: { value: number }) {
  const variant = value > 0 ? "success" : value < 0 ? "danger" : "muted";
  return <Badge variant={variant}>{formatPercent(value)}</Badge>;
}

function hasSevereLogs(logSeverities: Array<{ severity: string }>) {
  return logSeverities.some((item) => item.severity === "Critical" || item.severity === "Error");
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

function UsersTable({ users }: { users: DashboardUserActivitySummary[] }) {
  if (users.length === 0) {
    return <EmptyRow label="No users match the current filters" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[680px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">User</th>
            <th className="border-b border-border pb-2 text-right">Messages</th>
            <th className="border-b border-border pb-2 text-right">XP</th>
            <th className="border-b border-border pb-2 text-right">Level</th>
            <th className="border-b border-border pb-2 text-right">Quotes</th>
            <th className="border-b border-border pb-2 text-right">Net worth</th>
            <th className="border-b border-border pb-2 text-right">Button</th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => (
            <tr key={user.userId}>
              <td className="border-b border-border py-3 pr-3">
                <div className="font-semibold text-foreground">#{user.rank} {user.username}</div>
                <div className="text-xs text-muted">{formatRelativeDate(user.lastActivityAtUtc)}</div>
              </td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(user.messages)}</td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(user.xp)}</td>
              <td className="border-b border-border py-3 text-right">{formatInteger(user.level)}</td>
              <td className="border-b border-border py-3 text-right">{formatInteger(user.quotes)}</td>
              <td className="border-b border-border py-3 text-right">
                {formatCurrency(user.balance + user.stockPortfolioValue)}
              </td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(user.buttonScore)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ChannelsTable({ channels }: { channels: DashboardChannelActivity[] }) {
  if (channels.length === 0) {
    return <EmptyRow label="No channel activity matches the current filters" />;
  }

  return (
    <div className="grid gap-2">
      {channels.map((channel) => (
        <div className="rounded-lg border border-border bg-white p-3" key={channel.discordId}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">#{channel.rank} {channel.name}</div>
              <div className="text-xs text-muted">{formatInteger(channel.activeUsers)} active users</div>
            </div>
            <div className="text-right text-sm font-semibold text-foreground">{formatCompactNumber(channel.messages)}</div>
          </div>
          <div className="mt-3 grid grid-cols-3 gap-2 text-xs text-muted">
            <span>{formatCompactNumber(channel.xp)} XP</span>
            <span>{channel.averageMessageLength.toFixed(1)} chars</span>
            <span>{formatRelativeDate(channel.lastActivityAtUtc)}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function QuoteAuthors({ authors }: { authors: DashboardQuoteAuthorSummary[] }) {
  if (authors.length === 0) {
    return <EmptyRow label="No quote authors found" />;
  }

  return (
    <div className="grid gap-2">
      {authors.map((author) => (
        <div className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={author.userId}>
          <span className="truncate text-sm font-semibold text-foreground">{author.username}</span>
          <span className="text-sm text-muted">{formatInteger(author.quotes)} quotes</span>
          <Badge variant={author.score >= 0 ? "success" : "danger"}>{author.score >= 0 ? "+" : ""}{author.score}</Badge>
        </div>
      ))}
    </div>
  );
}

function QuotesList({
  emptyLabel = "No quotes found",
  quotes,
}: {
  emptyLabel?: string;
  quotes: DashboardQuoteItem[];
}) {
  if (quotes.length === 0) {
    return <EmptyRow label={emptyLabel} />;
  }

  return (
    <div className="grid gap-3">
      {quotes.slice(0, 6).map((quote) => (
        <article className="rounded-lg border border-border bg-white p-3" key={quote.id}>
          <div className="flex items-start justify-between gap-3">
            <div className="text-sm font-semibold text-foreground">#{quote.id}</div>
            <Badge variant={quote.approved ? "success" : quote.removed ? "danger" : "warning"}>
              {quote.approved ? "Approved" : quote.removed ? "Removed" : "Pending"}
            </Badge>
          </div>
          <p className="mt-2 line-clamp-3 text-sm leading-6 text-foreground">{quote.content}</p>
          <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-muted">
            <span>{quote.author}</span>
            <span>-</span>
            <span>{formatRelativeDate(quote.insertedAtUtc)}</span>
            <span>-</span>
            <span>{quote.score >= 0 ? "+" : ""}{quote.score}</span>
          </div>
        </article>
      ))}
    </div>
  );
}

function GuildsTable({ guilds }: { guilds: DashboardGuildSummary[] }) {
  if (guilds.length === 0) {
    return <EmptyRow label="No guilds found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
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
  );
}

function WealthTable({ users }: { users: DashboardWealthUser[] }) {
  if (users.length === 0) {
    return <EmptyRow label="No wallet data found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[620px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">User</th>
            <th className="border-b border-border pb-2 text-right">Cash</th>
            <th className="border-b border-border pb-2 text-right">Portfolio</th>
            <th className="border-b border-border pb-2 text-right">Net worth</th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => (
            <tr key={user.userId}>
              <td className="border-b border-border py-3 pr-3 font-semibold text-foreground">#{user.rank} {user.username}</td>
              <td className="border-b border-border py-3 text-right">{formatCurrency(user.balance)}</td>
              <td className="border-b border-border py-3 text-right">{formatCurrency(user.stockPortfolioValue)}</td>
              <td className="border-b border-border py-3 text-right font-semibold">{formatCurrency(user.netWorth)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function StockMoverList({
  title,
  movers,
  positive = false,
}: {
  title: string;
  movers: DashboardStockMover[];
  positive?: boolean;
}) {
  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">{title}</div>
      {movers.length === 0 ? (
        <EmptyRow label="No stock data" />
      ) : (
        movers.map((stock) => (
          <div className="rounded-lg border border-border bg-white p-3" key={stock.stockId}>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-foreground">{stock.name}</div>
                <div className="text-xs text-muted">{stock.entityType} stock</div>
              </div>
              <Badge variant={positive ? "success" : "danger"}>
                {stock.dailyChangePercent >= 0 ? "+" : ""}{stock.dailyChangePercent.toFixed(2)}%
              </Badge>
            </div>
            <div className="mt-3 flex items-center justify-between gap-3 text-sm">
              <span className="text-muted">{formatCurrency(stock.price)}</span>
              <span className="font-semibold text-foreground">{formatCurrency(stock.holdingValue)}</span>
            </div>
          </div>
        ))
      )}
    </div>
  );
}

function ButtonLeaders({ leaders }: { leaders: Array<{ rank: number; userId: number; username: string; presses: number; score: number }> }) {
  if (leaders.length === 0) {
    return <EmptyRow label="No button-game players found" />;
  }

  return (
    <div className="grid gap-2">
      {leaders.map((leader) => (
        <div className="grid grid-cols-[3rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={leader.userId}>
          <span className="text-sm font-semibold text-muted">#{leader.rank}</span>
          <span className="truncate text-sm font-semibold text-foreground">{leader.username}</span>
          <span className="text-sm text-muted">{formatCompactNumber(leader.score)} score</span>
        </div>
      ))}
    </div>
  );
}

function ReminderList({
  reminders,
}: {
  reminders: Array<{ id: number; channelId: string; text: string; user: string; dueDateUtc: string }>;
}) {
  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">Upcoming reminders</div>
      {reminders.length === 0 ? (
        <EmptyRow label="No upcoming reminders" />
      ) : (
        reminders.map((reminder) => (
          <div className="rounded-lg border border-border bg-white p-3" key={reminder.id}>
            <div className="line-clamp-2 text-sm font-medium text-foreground">{reminder.text}</div>
            <div className="mt-2 text-xs text-muted">{reminder.user} - {formatRelativeDate(reminder.dueDateUtc)}</div>
          </div>
        ))
      )}
    </div>
  );
}

function ModerationList({
  pending,
}: {
  pending: Array<{ id: number; guildId: string; userId: string; reason: string | null; expiresAtUtc: string }>;
}) {
  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">Temporary bans</div>
      {pending.length === 0 ? (
        <EmptyRow label="No pending temporary bans" />
      ) : (
        pending.map((ban) => (
          <div className="rounded-lg border border-border bg-white p-3" key={ban.id}>
            <div className="text-sm font-medium text-foreground">{ban.reason ?? "No reason recorded"}</div>
            <div className="mt-2 text-xs text-muted">{ban.userId} - expires {formatRelativeDate(ban.expiresAtUtc)}</div>
          </div>
        ))
      )}
    </div>
  );
}

function LogList({ logs }: { logs: DashboardLogItem[] }) {
  if (logs.length === 0) {
    return <EmptyRow label="No logs in the selected window" />;
  }

  return (
    <div className="grid gap-2">
      {logs.map((log) => (
        <div className="rounded-lg border border-border bg-white p-3" key={log.id}>
          <div className="flex items-start justify-between gap-3">
            <Badge variant={log.severity === "Error" || log.severity === "Critical" ? "danger" : log.severity === "Warning" ? "warning" : "muted"}>
              {log.severity}
            </Badge>
            <span className="shrink-0 text-xs text-muted">{formatRelativeDate(log.insertedAtUtc)}</span>
          </div>
          <div className="mt-2 line-clamp-3 text-sm text-foreground">{log.message}</div>
          {log.version && <div className="mt-2 text-xs text-muted">v{log.version}</div>}
        </div>
      ))}
    </div>
  );
}

function SettingsTable({ settings }: { settings: DashboardGuildSettingsSummary[] }) {
  if (settings.length === 0) {
    return <EmptyRow label="No server settings found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[780px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">Server</th>
            <th className="border-b border-border pb-2">Prefix</th>
            <th className="border-b border-border pb-2">Levels</th>
            <th className="border-b border-border pb-2">Quotes</th>
            <th className="border-b border-border pb-2">Welcome</th>
            <th className="border-b border-border pb-2">Activity roles</th>
            <th className="border-b border-border pb-2 text-right">Approvals</th>
          </tr>
        </thead>
        <tbody>
          {settings.map((setting) => (
            <tr key={setting.guildId}>
              <td className="border-b border-border py-3 pr-3 font-semibold text-foreground">{setting.guildName}</td>
              <td className="border-b border-border py-3">{setting.prefix}</td>
              <td className="border-b border-border py-3">{yesNo(setting.levelUpMessages)}</td>
              <td className="border-b border-border py-3">{setting.useGlobalQuotes ? "Global" : yesNo(setting.levelUpQuotes)}</td>
              <td className="border-b border-border py-3">{yesNo(setting.welcomeMessages)}</td>
              <td className="border-b border-border py-3">{yesNo(setting.useActivityRoles)}</td>
              <td className="border-b border-border py-3 text-right">
                +{setting.quoteAddRequiredApprovals} / -{setting.quoteRemoveRequiredApprovals}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
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

function parseOptionalString(value: string | undefined) {
  return value && value.trim().length > 0 ? value.trim() : undefined;
}

function parseScope(value: string | undefined, guildId?: number, userId?: number, channelId?: string): DashboardScope {
  if (value === "global" || value === "server" || value === "user" || value === "channel") {
    return value;
  }

  if (channelId) {
    return "channel";
  }

  if (userId) {
    return "user";
  }

  if (guildId) {
    return "server";
  }

  return "global";
}

function normalizeDashboardFilters(filters: {
  guildId?: number;
  userId?: number;
  channelId?: string;
  days: number;
  scope: DashboardScope;
  sortDirection: SortDirection;
  minActivity: number;
}) {
  let scope = filters.scope;

  if (scope === "channel" && !filters.channelId) {
    scope = filters.userId ? "user" : filters.guildId ? "server" : "global";
  }

  if (scope === "user" && !filters.userId) {
    scope = filters.guildId ? "server" : "global";
  }

  if (scope === "server" && !filters.guildId) {
    scope = "global";
  }

  if (scope === "global") {
    return { ...filters, guildId: undefined, userId: undefined, channelId: undefined, scope };
  }

  if (scope === "server") {
    return { ...filters, userId: undefined, channelId: undefined, scope };
  }

  if (scope === "user") {
    return { ...filters, channelId: undefined, scope };
  }

  return { ...filters, userId: undefined, scope };
}

function parseSortDirection(value: string | undefined): SortDirection {
  return value === "asc" ? "asc" : "desc";
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

function formatPercent(value: number) {
  const prefix = value > 0 ? "+" : "";
  return `${prefix}${value.toFixed(1)}%`;
}

function formatHourUtc(hour: number) {
  return `${String(hour).padStart(2, "0")}:00 UTC`;
}

function yesNo(value: boolean) {
  return value ? "On" : "Off";
}
