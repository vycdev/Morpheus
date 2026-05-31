import Image from "next/image";
import Link from "next/link";
import type React from "react";
import {
  Activity,
  ArrowUpRight,
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
import { DashboardNavLink } from "@/components/dashboard/nav-link";
import {
  ActivityHeatmap,
  CalendarActivityHeatmap,
  ActivityInsightChart,
  ButtonGameChart,
  CategoryBars,
  CumulativeXpChart,
  DonutChart,
  EconomyFlowChart,
  GlobalActivityLineChart,
  LogTimelineChart,
  MessagesOverTimeChart,
  MoneyFlowView,
  StackedServerActivityChart,
} from "@/components/dashboard/insight-charts";
import { ThemeToggle } from "@/components/dashboard/theme-toggle";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getDashboardData } from "@/lib/dashboard-api";
import type {
  DashboardChannelActivity,
  DashboardEconomyEventItem,
  DashboardGlobalChannelActivity,
  DashboardGlobalServerActivity,
  DashboardGlobalUserActivity,
  DashboardGlobalWealthUser,
  DashboardGuildSettingsSummary,
  DashboardGuildSummary,
  DashboardLeaderboardItem,
  DashboardLogItem,
  DashboardPopularQuote,
  DashboardQuoteAuthorSummary,
  DashboardQuoteItem,
  DashboardRecentEntity,
  DashboardRecentQuote,
  DashboardRecentStock,
  DashboardStockMover,
  DashboardUserActivitySummary,
  DashboardWealthUser,
} from "@/lib/types";
import {
  cn,
  clamp,
  formatCompactNumber,
  formatCurrency,
  formatInteger,
  formatRelativeDate,
} from "@/lib/utils";

export const dynamic = "force-dynamic";

type SearchParams = Record<string, string | string[] | undefined>;
type DashboardScope = "global" | "server" | "user" | "channel";
type DashboardView =
  | "summary"
  | "activity"
  | "servers"
  | "users"
  | "quotes"
  | "economy"
  | "stocks"
  | "operations"
  | "settings";
type SortDirection = "asc" | "desc";
type DashboardDateWindow = {
  days: number;
  startDate: string;
  endDate: string;
};

const maxDashboardDateWindowDays = 3650;

export default async function DashboardPage({
  searchParams,
}: {
  searchParams?: Promise<SearchParams> | SearchParams;
}) {
  const params = await Promise.resolve(searchParams ?? {});
  const requestedDays = clamp(parseNumber(getParam(params.days), 30), 1, maxDashboardDateWindowDays);
  const dateWindow = normalizeDateWindow(
    requestedDays,
    parseDateParam(getParam(params.startDate)),
    parseDateParam(getParam(params.endDate)),
  );
  const { days, startDate, endDate } = dateWindow;
  const requestedGuildId = parseOptionalNumber(getParam(params.guildId));
  const requestedUserId = parseOptionalNumber(getParam(params.userId));
  const requestedChannelId = parseOptionalString(getParam(params.channelId));
  const requestedScope = parseScope(getParam(params.scope), requestedGuildId, requestedUserId, requestedChannelId);
  const requestedView = parseDashboardView(getParam(params.view));
  const sortDirection = parseSortDirection(getParam(params.sortDirection));
  const minActivity = clamp(parseNumber(getParam(params.minActivity), 1), 0, 100000);
  const filters = normalizeDashboardFilters({
    guildId: requestedGuildId,
    userId: requestedUserId,
    channelId: requestedChannelId,
    days,
    startDate,
    endDate,
    scope: requestedScope,
    sortDirection,
    minActivity,
  });
  const dashboardView = filters.scope !== "global"
    ? requestedView === "servers" ? "summary" : requestedView
    : requestedView === "settings" ? "summary" : requestedView;
  const requestFilters = { ...filters, view: dashboardView };
  const data = await getDashboardData(requestFilters);
  const { guildId, userId, channelId, scope } = filters;
  const drilldown = data.drilldown;
  const insights = drilldown?.insights;
  const userOptions = insights?.filterOptions.users ?? [];
  const channelOptions = insights?.filterOptions.channels ?? [];
  const drilldownActive = scope !== "global";
  const selectedGuild = data.guilds.find((guild) => guild.id === guildId);
  const selectedUser = userOptions.find((user) => user.userId === userId);
  const selectedChannel = channelOptions.find((channel) => channel.discordId === channelId);
  const dateWindowLabel = formatDateWindowLabel(startDate, endDate, days);
  const scopeLabel =
    selectedChannel?.name ??
    selectedUser?.username ??
    selectedGuild?.name ??
    (userId ? `User #${userId}` : channelId ? `Channel ${channelId}` : "All Morpheus data");
  const global = data.globalOverview;
  const totals = global.totals;

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-[1540px] auto-rows-max content-start gap-4 px-4 py-4 sm:px-6 lg:px-8">
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
                Generated {formatRelativeDate(global.generatedAtUtc)} - {dateWindowLabel}
              </p>
            </div>
          </div>
          <div className="flex shrink-0 items-start gap-3">
            <ThemeToggle />
          </div>
        </div>
        <div className="border-t border-border p-3">
          <DashboardFilters
            channels={channelOptions}
            days={days}
            endDate={endDate}
            guilds={data.guilds}
            minActivity={minActivity}
            scope={scope}
            selectedChannelId={channelId}
            selectedGuildId={guildId}
            selectedUserId={userId}
            sortDirection={sortDirection}
            startDate={startDate}
            users={userOptions}
            view={dashboardView}
          />
        </div>
      </section>

      {data.usingDemoData && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Showing demo data because the dashboard API did not respond. {data.error}
        </div>
      )}

      {data.drilldownError && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          The global overview loaded, but scoped drilldown data did not respond. {data.drilldownError}
        </div>
      )}

      <DashboardPageTabs
        activeView={dashboardView}
        channelId={channelId}
        days={days}
        endDate={endDate}
        guildId={guildId}
        minActivity={minActivity}
        scope={scope}
        sortDirection={sortDirection}
        startDate={startDate}
        userId={userId}
      />

      {!drilldownActive && dashboardView === "summary" && (
      <section id="global-overview" className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        <StatCard icon={Server} label="Servers" value={formatInteger(totals.totalServers)} meta="tracked globally" tone="blue" />
        <StatCard icon={Users} label="Known users" value={formatInteger(totals.totalKnownUsers)} meta="unique Discord users" tone="cyan" />
        <StatCard icon={MessageSquareText} label="Messages" value={formatCompactNumber(totals.totalTrackedMessages)} meta={`${formatCompactNumber(totals.latestDayMessages)} in latest day`} tone="green" />
        <StatCard icon={TrendingUp} label="XP generated" value={formatCompactNumber(totals.totalXpGenerated)} meta={`${formatCompactNumber(totals.latestDayXpGenerated)} in latest day`} tone="green" />
        <StatCard icon={Quote} label="Quotes" value={formatInteger(totals.totalQuotes)} meta={`${formatInteger(totals.totalApprovedQuotes)} approved`} tone="rose" />
        <StatCard icon={ShieldCheck} label="Approved quotes" value={formatInteger(totals.totalApprovedQuotes)} meta={`${formatInteger(totals.pendingQuotes)} pending quotes`} tone="rose" />
        <StatCard icon={AlertTriangle} label="Quote approvals" value={formatInteger(totals.pendingQuoteApprovals)} meta="approval messages open" tone="amber" />
        <StatCard icon={Wallet} label="Economy balance" value={formatCurrency(totals.totalEconomyBalance)} meta="cash in wallets" tone="amber" />
        <StatCard icon={Banknote} label="Net worth" value={formatCurrency(totals.totalEstimatedNetWorth)} meta="cash plus portfolios" tone="amber" />
        <StatCard icon={Database} label="UBI pool" value={formatCurrency(totals.ubiPoolSize)} meta="community reserve" tone="cyan" />
        <StatCard icon={Gamepad2} label="Slots vault" value={formatCurrency(totals.slotsVaultSize)} meta="jackpot backing" tone="blue" />
        <StatCard icon={Activity} label="Transactions" value={formatCompactNumber(totals.totalTransactions)} meta="economy event log" tone="slate" />
        <StatCard icon={Gamepad2} label="Button presses" value={formatCompactNumber(totals.totalButtonPresses)} meta="all-time presses" tone="cyan" />
        <StatCard icon={Bell} label="Reminders" value={formatInteger(totals.activeReminders)} meta="active queue" tone="blue" />
        <StatCard icon={AlertTriangle} label="Warnings/errors" value={formatInteger(totals.recentWarningsOrErrors)} meta="last 24 hours" tone={totals.recentWarningsOrErrors > 0 ? "rose" : "green"} />
      </section>
      )}

      {!drilldownActive && dashboardView === "activity" && (
      <section id="activity" className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Global Activity</CardTitle>
              <CardDescription>Messages and active users across every server.</CardDescription>
            </div>
            <Badge variant="muted">{global.days} days</Badge>
          </CardHeader>
          <CardContent>
            <GlobalActivityLineChart points={global.visuals.activity} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Cumulative XP</CardTitle>
              <CardDescription>Global XP accumulation in the current window.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CumulativeXpChart points={global.visuals.activity} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Messages Over Time</CardTitle>
              <CardDescription>Daily tracked message volume for all known servers.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <MessagesOverTimeChart points={global.visuals.activity} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Activity Stack</CardTitle>
              <CardDescription>Top servers contributing to global message activity.</CardDescription>
            </div>
            <Server className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <StackedServerActivityChart points={global.visuals.stackedServerActivity} />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "activity" && (
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Calendar Heatmap</CardTitle>
              <CardDescription>Global activity density by day.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CalendarActivityHeatmap cells={global.visuals.calendarActivity} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Hour by Weekday</CardTitle>
              <CardDescription>UTC activity concentration across the week.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityHeatmap cells={global.visuals.hourByWeekdayActivity} />
          </CardContent>
        </Card>

      </section>
      )}

      {!drilldownActive && dashboardView === "economy" && (
      <section className="grid grid-cols-1 gap-4">
        <Card id="economy">
          <CardHeader>
            <div>
              <CardTitle>Transaction Types</CardTitle>
              <CardDescription>Economy movement by category.</CardDescription>
            </div>
            <Banknote className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <DonutChart data={global.visuals.transactionTypes} labelKey="label" />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "servers" && (
      <section id="servers" className="grid grid-cols-1 gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Active Servers</CardTitle>
              <CardDescription>Today, week, month, and all-time leaders.</CardDescription>
            </div>
            <Server className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ServerActivityWindows
              allTime={global.highlights.mostActiveServersAllTime}
              days={days}
              endDate={endDate}
              month={global.highlights.mostActiveServersThisMonth}
              startDate={startDate}
              today={global.highlights.mostActiveServersToday}
              week={global.highlights.mostActiveServersThisWeek}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Top Servers by Activity</CardTitle>
              <CardDescription>Message share for the selected global window.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <LinkedServerBars servers={global.highlights.mostActiveServersSelectedWindow} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "users" && (
      <section id="users" className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>User Leaderboard</CardTitle>
              <CardDescription>Biggest XP gainers across all servers.</CardDescription>
            </div>
            <Users className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <GlobalUserLeaderboard users={global.highlights.biggestXpGainers} days={days} endDate={endDate} metric="XP" startDate={startDate} valueKey="xp" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Active Users</CardTitle>
              <CardDescription>Highest message contributors in the global window.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <GlobalUserLeaderboard users={global.highlights.mostActiveUsers} days={days} endDate={endDate} metric="messages" startDate={startDate} valueKey="messages" />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "users" && (
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Richest by Balance</CardTitle>
              <CardDescription>Largest liquid wallet balances.</CardDescription>
            </div>
            <Wallet className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <GlobalWealthLeaderboard users={global.highlights.richestUsersByBalance} days={days} endDate={endDate} mode="balance" startDate={startDate} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Richest by Net Worth</CardTitle>
              <CardDescription>Wallet balances plus marked portfolio value.</CardDescription>
            </div>
            <Banknote className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <GlobalWealthLeaderboard users={global.highlights.richestUsersByNetWorth} days={days} endDate={endDate} mode="netWorth" startDate={startDate} />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "stocks" && (
      <section id="stocks" className="grid grid-cols-1 gap-4">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Stock Movers</CardTitle>
              <CardDescription>Largest positive and negative daily stock moves.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <StockMoverList title="Gainers" movers={global.highlights.biggestStockGainers} positive />
            <StockMoverList title="Losers" movers={global.highlights.biggestStockLosers} />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "quotes" && (
      <section className="grid grid-cols-1 gap-4">
        <Card id="quotes">
          <CardHeader>
            <div>
              <CardTitle>Popular Quotes</CardTitle>
              <CardDescription>Top-scored approved quotes across the bot.</CardDescription>
            </div>
            <Quote className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <PopularQuotesList quotes={global.highlights.mostPopularQuotes} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "users" && (
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Active Channels</CardTitle>
              <CardDescription>Channels with the highest global message volume.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <GlobalChannelList channels={global.highlights.mostActiveChannels} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Recently Created</CardTitle>
              <CardDescription>Newest users, servers, quotes, and stocks.</CardDescription>
            </div>
            <Database className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <RecentCreatedGrid
              days={days}
              quotes={global.highlights.recentlyCreatedQuotes}
              servers={global.highlights.recentlyCreatedServers}
              stocks={global.highlights.recentlyCreatedStocks}
              endDate={endDate}
              startDate={startDate}
              users={global.highlights.recentlyCreatedUsers}
            />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "economy" && (
      <section id="logs" className="grid grid-cols-1 gap-4">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Recent Economy Events</CardTitle>
              <CardDescription>Latest transactions, transfers, slots, and stock events.</CardDescription>
            </div>
            <Banknote className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <EconomyEventsFeed events={global.feeds.recentEconomyEvents} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>
      </section>
      )}

      {!drilldownActive && dashboardView === "operations" && (
      <section className="grid grid-cols-1 gap-4">
        <Card id="operations">
          <CardHeader>
            <div>
              <CardTitle>Recent Bot Health Events</CardTitle>
              <CardDescription>Newest warning, error, and critical log entries.</CardDescription>
            </div>
            <AlertTriangle className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <LogList logs={global.feeds.recentBotHealthEvents} />
          </CardContent>
        </Card>
      </section>
      )}

      {drilldownActive && (
        <section className="grid grid-cols-1 gap-4" aria-label="Scoped dashboard view">
          {insights && drilldown ? (
            <>

      {dashboardView === "summary" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.4fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Intelligence</CardTitle>
              <CardDescription>Daily messages, rolling average, and cumulative activity in the selected scope.</CardDescription>
            </div>
            <TrendBadge value={insights.activity.trendPercent} />
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-4">
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
        </>
      )}

      {dashboardView === "activity" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Heatmap</CardTitle>
              <CardDescription>UTC hour and day concentration for message volume.</CardDescription>
            </div>
            <Badge variant="muted">
              <Gauge className="h-3.5 w-3.5" aria-hidden="true" />
              Min {formatInteger(minActivity)} messages
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
            <ActivityChart points={drilldown.activity.points} />
          </CardContent>
        </Card>
      </section>
        </>
      )}

      {dashboardView === "users" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.2fr_0.8fr]">
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

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <LeaderboardCard title="XP Leaders" metric="XP" items={drilldown.xpLeaderboard.items} />
        <LeaderboardCard title="Message Leaders" metric="messages" items={drilldown.messageLeaderboard.items} />
      </section>
        </>
      )}

      {dashboardView === "quotes" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.95fr_1.05fr]">
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

        <div className="grid grid-cols-1 gap-4">
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

        </div>
      </section>
        </>
      )}

      {dashboardView === "economy" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.25fr_0.75fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Economy Flow</CardTitle>
              <CardDescription>Money entering and leaving wallets, with net movement over time.</CardDescription>
            </div>
            <Badge variant="muted">{formatCurrency(insights.economy.fees)} fees</Badge>
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-4">
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

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.8fr_1.2fr]">
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
        </>
      )}

      {dashboardView === "stocks" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.9fr_1.1fr]">
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
          <CardContent className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <StockMoverList title="Winners" movers={insights.stocks.winners} positive />
            <StockMoverList title="Losers" movers={insights.stocks.losers} />
          </CardContent>
        </Card>
      </section>
        </>
      )}

      {dashboardView === "operations" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button Game</CardTitle>
              <CardDescription>Press volume, score yield, rolling engagement, and top players.</CardDescription>
            </div>
            <Badge variant="muted">{formatRelativeDate(insights.buttonGame.lastPressAtUtc)}</Badge>
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-4">
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
          <CardContent className="grid grid-cols-1 gap-4">
            <LogTimelineChart points={insights.operations.logTimeline} />
            <div className="grid gap-3 sm:grid-cols-3">
              <MetricPill label="Overdue reminders" value={formatInteger(insights.operations.reminders.overdue)} />
              <MetricPill label="Due 24h" value={formatInteger(insights.operations.reminders.dueNext24Hours)} />
              <MetricPill label="Temp bans" value={formatInteger(insights.operations.moderation.pendingTemporaryBans)} />
            </div>
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminder and Moderation Queue</CardTitle>
              <CardDescription>Upcoming reminders and temporary ban follow-through.</CardDescription>
            </div>
            <Bell className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-4">
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
        </>
      )}

      {dashboardView === "settings" && (
        <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.9fr_1.1fr]">
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
        </>
      )}
            </>
          ) : (
            <ScopedViewEmptyState error={data.drilldownError} />
          )}
        </section>
      )}
    </main>
  );
}

function ScopedViewEmptyState({ error }: { error?: string }) {
  return (
    <div className="rounded-lg border border-dashed border-border bg-white p-6 text-sm text-muted">
      <div className="font-semibold text-foreground">Scoped metrics did not load</div>
      <p className="mt-2 max-w-2xl">
        {error ?? "The global overview is available, but the selected scoped data is unavailable right now."}
      </p>
    </div>
  );
}

const SCOPED_VIEW_TABS: Array<{ view: DashboardView; label: string }> = [
  { view: "summary", label: "Summary" },
  { view: "activity", label: "Activity" },
  { view: "users", label: "Users" },
  { view: "quotes", label: "Quotes" },
  { view: "economy", label: "Economy" },
  { view: "stocks", label: "Stocks" },
  { view: "operations", label: "Ops" },
  { view: "settings", label: "Settings" },
];

const GLOBAL_VIEW_TABS: Array<{ view: DashboardView; label: string }> = [
  { view: "summary", label: "Summary" },
  { view: "activity", label: "Activity" },
  { view: "servers", label: "Servers" },
  { view: "users", label: "Users" },
  { view: "economy", label: "Economy" },
  { view: "quotes", label: "Quotes" },
  { view: "stocks", label: "Stocks" },
  { view: "operations", label: "Ops" },
];

function DashboardPageTabs({
  activeView,
  channelId,
  days,
  startDate,
  endDate,
  guildId,
  minActivity,
  scope,
  sortDirection,
  userId,
}: {
  activeView: DashboardView;
  channelId?: string;
  days: number;
  startDate: string;
  endDate: string;
  guildId?: number;
  minActivity: number;
  scope: DashboardScope;
  sortDirection: SortDirection;
  userId?: number;
}) {
  const tabs = scope === "global" ? GLOBAL_VIEW_TABS : SCOPED_VIEW_TABS;

  return (
    <nav aria-label="Dashboard pages" className="inline-flex w-fit max-w-full flex-wrap items-center gap-2 justify-self-start rounded-lg border border-border bg-white p-2 shadow-sm">
      {tabs.map((tab) => {
        const active = tab.view === activeView;
        const href = scope === "global"
          ? dashboardHref({ scope: "global", days, startDate, endDate, view: tab.view })
          : dashboardHref({
              channelId,
              days,
              endDate,
              guildId,
              minActivity,
              scope,
              sortDirection,
              startDate,
              userId,
              view: tab.view,
            });

        return (
          <DashboardNavLink
            aria-current={active ? "page" : undefined}
            className={cn(
              "inline-flex h-9 items-center rounded-md px-3 text-sm font-medium transition-colors",
              active
                ? "bg-primary text-primary-foreground"
                : "border border-border bg-white text-muted hover:border-primary hover:text-foreground",
            )}
            href={href}
            key={tab.view}
          >
            {tab.label}
          </DashboardNavLink>
        );
      })}
    </nav>
  );
}

function StatCard({
  icon: Icon,
  label,
  value,
  meta,
  tone,
  href,
}: {
  icon: React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;
  label: string;
  value: string;
  meta: string;
  tone: "blue" | "green" | "cyan" | "amber" | "rose" | "slate";
  href?: string;
}) {
  const toneClass = {
    blue: "bg-blue-50 text-blue-700",
    green: "bg-emerald-50 text-emerald-700",
    cyan: "bg-cyan-50 text-cyan-700",
    amber: "bg-amber-50 text-amber-700",
    rose: "bg-rose-50 text-rose-700",
    slate: "bg-slate-50 text-muted",
  }[tone];

  const card = (
    <Card className={cn("w-full", href && "transition-colors hover:border-primary hover:shadow-md")}>
      <CardContent className="grid h-32 grid-cols-1 items-center gap-4 sm:grid-cols-[minmax(0,1fr)_2.75rem]">
        <div className="min-w-0">
          <div className="text-sm font-medium text-muted">{label}</div>
          <div className="mt-2 text-2xl font-semibold text-foreground">{value}</div>
          <div className="mt-2 text-sm text-muted">{meta}</div>
        </div>
        <div className={`hidden h-11 w-11 shrink-0 place-items-center rounded-lg sm:grid ${toneClass}`}>
          <Icon className="h-5 w-5" aria-hidden />
        </div>
      </CardContent>
    </Card>
  );

  return href ? <Link className="block min-w-0" href={href}>{card}</Link> : card;
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

function ServerActivityWindows({
  today,
  week,
  month,
  allTime,
  days,
  startDate,
  endDate,
}: {
  today: DashboardGlobalServerActivity[];
  week: DashboardGlobalServerActivity[];
  month: DashboardGlobalServerActivity[];
  allTime: DashboardGlobalServerActivity[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  const windows = [
    { label: "Today", servers: today },
    { label: "This week", servers: week },
    { label: "This month", servers: month },
    { label: "All time", servers: allTime },
  ];

  return (
    <div className="grid gap-3 md:grid-cols-2">
      {windows.map((window) => (
        <div className="rounded-lg border border-border bg-slate-50 p-3" key={window.label}>
          <div className="mb-2 text-sm font-semibold text-foreground">{window.label}</div>
          <div className="grid gap-2">
            {window.servers.length === 0 ? (
              <EmptyRow label="No server activity" />
            ) : (
              window.servers.slice(0, 5).map((server) => (
                <Link
                  className="grid grid-cols-[2rem_minmax(0,1fr)_auto] items-center gap-2 rounded-lg border border-border bg-white px-3 py-2 transition-colors hover:bg-slate-50"
                  href={dashboardHref({ scope: "server", guildId: server.guildId, days, startDate, endDate })}
                  key={`${window.label}-${server.guildId}`}
                >
                  <span className="text-sm font-semibold text-muted">#{server.rank}</span>
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-foreground">{server.name}</span>
                    <span className="block text-xs text-muted">{formatInteger(server.activeUsers)} active users</span>
                  </span>
                  <span className="text-right text-sm font-semibold text-foreground">
                    {formatCompactNumber(server.messages)}
                  </span>
                </Link>
              ))
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

function LinkedServerBars({
  servers,
  days,
  startDate,
  endDate,
}: {
  servers: DashboardGlobalServerActivity[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  const max = Math.max(1, ...servers.map((server) => server.messages));

  if (servers.length === 0) {
    return <EmptyRow label="No server activity found" />;
  }

  return (
    <div className="grid gap-3">
      {servers.slice(0, 8).map((server) => (
        <Link
          className="rounded-lg border border-border bg-white p-3 transition-colors hover:bg-slate-50"
          href={dashboardHref({ scope: "server", guildId: server.guildId, days, startDate, endDate })}
          key={server.guildId}
        >
          <div className="flex items-center justify-between gap-3 text-sm">
            <span className="truncate font-semibold text-foreground">#{server.rank} {server.name}</span>
            <span className="shrink-0 text-muted">{formatCompactNumber(server.messages)}</span>
          </div>
          <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-100">
            <div
              className="h-full rounded-full bg-primary"
              style={{ width: `${Math.max(4, (server.messages / max) * 100)}%` }}
            />
          </div>
        </Link>
      ))}
    </div>
  );
}

function GlobalUserLeaderboard({
  users,
  days,
  startDate,
  endDate,
  metric,
  valueKey,
}: {
  users: DashboardGlobalUserActivity[];
  days: number;
  startDate: string;
  endDate: string;
  metric: string;
  valueKey: "xp" | "messages";
}) {
  if (users.length === 0) {
    return <EmptyRow label="No user activity found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[560px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">User</th>
            <th className="border-b border-border pb-2 text-right">Messages</th>
            <th className="border-b border-border pb-2 text-right">XP</th>
            <th className="border-b border-border pb-2 text-right">Level</th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => (
            <tr key={`${metric}-${user.userId}`}>
              <td className="border-b border-border py-3 pr-3">
                <Link
                  className="group flex min-w-0 items-center gap-2 font-semibold text-foreground"
                  href={dashboardHref({ scope: "user", userId: user.userId, days, startDate, endDate })}
                >
                  <span>#{user.rank} {user.username}</span>
                  <ArrowUpRight className="h-3.5 w-3.5 text-muted opacity-0 transition-opacity group-hover:opacity-100" aria-hidden />
                </Link>
                <div className="text-xs text-muted">{formatRelativeDate(user.lastActivityAtUtc)}</div>
              </td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(user.messages)}</td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(user.xp)}</td>
              <td className="border-b border-border py-3 text-right">
                <Badge variant={valueKey === "xp" ? "success" : "muted"}>{formatInteger(user.level)}</Badge>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function GlobalWealthLeaderboard({
  users,
  days,
  startDate,
  endDate,
  mode,
}: {
  users: DashboardGlobalWealthUser[];
  days: number;
  startDate: string;
  endDate: string;
  mode: "balance" | "netWorth";
}) {
  if (users.length === 0) {
    return <EmptyRow label="No wealth data found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[620px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">User</th>
            <th className="border-b border-border pb-2 text-right">Balance</th>
            <th className="border-b border-border pb-2 text-right">Portfolio</th>
            <th className="border-b border-border pb-2 text-right">Net worth</th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => (
            <tr key={`${mode}-${user.userId}`}>
              <td className="border-b border-border py-3 pr-3">
                <Link
                  className="font-semibold text-foreground"
                  href={dashboardHref({ scope: "user", userId: user.userId, days, startDate, endDate })}
                >
                  #{user.rank} {user.username}
                </Link>
              </td>
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

function PopularQuotesList({
  quotes,
  days,
  startDate,
  endDate,
}: {
  quotes: DashboardPopularQuote[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  if (quotes.length === 0) {
    return <EmptyRow label="No popular quotes found" />;
  }

  return (
    <div className="grid gap-3">
      {quotes.slice(0, 8).map((quote) => (
        <Link
          className="rounded-lg border border-border bg-white p-3 transition-colors hover:bg-slate-50"
          href={dashboardHref({ scope: "server", guildId: quote.guildId, days, startDate, endDate })}
          key={quote.id}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="text-sm font-semibold text-foreground">#{quote.rank} Quote {quote.id}</div>
            <Badge variant={quote.score >= 0 ? "success" : "danger"}>{quote.score >= 0 ? "+" : ""}{quote.score}</Badge>
          </div>
          <p className="mt-2 line-clamp-2 text-sm leading-6 text-foreground">{quote.content}</p>
          <div className="mt-2 text-xs text-muted">{quote.author} - {formatRelativeDate(quote.insertedAtUtc)}</div>
        </Link>
      ))}
    </div>
  );
}

function GlobalChannelList({
  channels,
  days,
  startDate,
  endDate,
}: {
  channels: DashboardGlobalChannelActivity[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  if (channels.length === 0) {
    return <EmptyRow label="No channel activity found" />;
  }

  return (
    <div className="grid gap-2">
      {channels.slice(0, 10).map((channel) => (
        <Link
          className="rounded-lg border border-border bg-white p-3 transition-colors hover:bg-slate-50"
          href={dashboardHref({ scope: "channel", channelId: channel.discordId, days, startDate, endDate })}
          key={channel.discordId}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">#{channel.rank} {channel.name}</div>
              <div className="text-xs text-muted">{formatInteger(channel.activeUsers)} active users</div>
            </div>
            <div className="text-right text-sm font-semibold text-foreground">{formatCompactNumber(channel.messages)}</div>
          </div>
          <div className="mt-2 text-xs text-muted">{formatCompactNumber(channel.xp)} XP - {formatRelativeDate(channel.lastActivityAtUtc)}</div>
        </Link>
      ))}
    </div>
  );
}

function RecentCreatedGrid({
  users,
  servers,
  quotes,
  stocks,
  days,
  startDate,
  endDate,
}: {
  users: DashboardRecentEntity[];
  servers: DashboardRecentEntity[];
  quotes: DashboardRecentQuote[];
  stocks: DashboardRecentStock[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  return (
    <div className="grid grid-cols-1 items-start gap-4 md:grid-cols-2">
      <RecentEntityList items={users} title="Users" days={days} endDate={endDate} scope="user" startDate={startDate} />
      <RecentEntityList items={servers} title="Servers" days={days} endDate={endDate} scope="server" startDate={startDate} />
      <RecentQuoteList quotes={quotes} days={days} endDate={endDate} startDate={startDate} />
      <RecentStockList stocks={stocks} days={days} endDate={endDate} startDate={startDate} />
    </div>
  );
}

function RecentEntityList({
  title,
  items,
  scope,
  days,
  startDate,
  endDate,
}: {
  title: string;
  items: DashboardRecentEntity[];
  scope: "user" | "server";
  days: number;
  startDate: string;
  endDate: string;
}) {
  return (
    <div className="grid auto-rows-max content-start gap-2">
      <div className="text-sm font-semibold text-foreground">{title}</div>
      {items.length === 0 ? (
        <EmptyRow label={`No recent ${title.toLowerCase()}`} />
      ) : (
        items.slice(0, 4).map((item) => (
          <Link
            className="grid min-h-[66px] content-center rounded-lg border border-border bg-white p-3 transition-colors hover:bg-slate-50"
            href={
              scope === "user"
                ? dashboardHref({ scope: "user", userId: item.id, days, startDate, endDate })
                : dashboardHref({ scope: "server", guildId: item.id, days, startDate, endDate })
            }
            key={`${title}-${item.id}`}
          >
            <div className="truncate text-sm font-semibold text-foreground">{item.name}</div>
            <div className="mt-1 text-xs text-muted">{formatRelativeDate(item.insertedAtUtc)}</div>
          </Link>
        ))
      )}
    </div>
  );
}

function RecentQuoteList({
  quotes,
  days,
  startDate,
  endDate,
}: {
  quotes: DashboardRecentQuote[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  return (
    <div className="grid auto-rows-max content-start gap-2">
      <div className="text-sm font-semibold text-foreground">Quotes</div>
      {quotes.length === 0 ? (
        <EmptyRow label="No recent quotes" />
      ) : (
        quotes.slice(0, 4).map((quote) => (
          <Link
            className="grid min-h-[66px] content-center rounded-lg border border-border bg-white p-3 transition-colors hover:bg-slate-50"
            href={dashboardHref({ scope: "server", guildId: quote.guildId, days, startDate, endDate })}
            key={quote.id}
          >
            <div className="line-clamp-2 text-sm font-medium text-foreground">{quote.content}</div>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted">
              <span>{quote.author}</span>
              <Badge variant={quote.approved ? "success" : quote.removed ? "danger" : "warning"}>
                {quote.approved ? "Approved" : quote.removed ? "Removed" : "Pending"}
              </Badge>
            </div>
          </Link>
        ))
      )}
    </div>
  );
}

function RecentStockList({
  stocks,
  days,
  startDate,
  endDate,
}: {
  stocks: DashboardRecentStock[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  return (
    <div className="grid auto-rows-max content-start gap-2">
      <div className="text-sm font-semibold text-foreground">Stocks</div>
      {stocks.length === 0 ? (
        <EmptyRow label="No recent stocks" />
      ) : (
        stocks.slice(0, 4).map((stock) => {
          const href = stockDrillHref(stock, days, startDate, endDate);
          const content = (
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-foreground">{stock.name}</div>
                <div className="text-xs text-muted">{stock.entityType} stock</div>
              </div>
              <Badge variant={stock.dailyChangePercent >= 0 ? "success" : "danger"}>
                {stock.dailyChangePercent >= 0 ? "+" : ""}{stock.dailyChangePercent.toFixed(2)}%
              </Badge>
            </div>
          );

          return href ? (
            <Link
              className="grid min-h-[66px] content-center rounded-lg border border-border bg-white p-3 transition-colors hover:bg-slate-50"
              href={href}
              key={stock.stockId}
            >
              {content}
            </Link>
          ) : (
            <div className="grid min-h-[66px] content-center rounded-lg border border-border bg-white p-3" key={stock.stockId}>
              {content}
            </div>
          );
        })
      )}
    </div>
  );
}

function EconomyEventsFeed({
  events,
  days,
  startDate,
  endDate,
}: {
  events: DashboardEconomyEventItem[];
  days: number;
  startDate: string;
  endDate: string;
}) {
  if (events.length === 0) {
    return <EmptyRow label="No recent economy events" />;
  }

  return (
    <div className="grid gap-2">
      {events.map((event) => (
        <Link
          className="rounded-lg border border-border bg-white p-3 transition-colors hover:bg-slate-50"
          href={dashboardHref({ scope: "user", userId: event.userId, days, startDate, endDate })}
          key={event.id}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{event.type}</div>
              <div className="text-xs text-muted">
                {event.user}{event.targetUser ? ` -> ${event.targetUser}` : ""}{event.stockName ? ` - ${event.stockName}` : ""}
              </div>
            </div>
            <div className="text-right text-sm font-semibold text-foreground">{formatCurrency(event.amount)}</div>
          </div>
          <div className="mt-2 flex items-center justify-between gap-3 text-xs text-muted">
            <span>{formatRelativeDate(event.insertedAtUtc)}</span>
            {event.fee > 0 && <span>{formatCurrency(event.fee)} fee</span>}
          </div>
        </Link>
      ))}
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

function dashboardHref({
  scope,
  guildId,
  userId,
  channelId,
  days,
  startDate,
  endDate,
  view,
  minActivity,
  sortDirection,
}: {
  scope: DashboardScope;
  guildId?: number;
  userId?: number;
  channelId?: string;
  days: number;
  startDate?: string;
  endDate?: string;
  view?: DashboardView;
  minActivity?: number;
  sortDirection?: SortDirection;
}) {
  const params = new URLSearchParams({
    scope,
    days: String(days),
  });

  if (startDate) {
    params.set("startDate", startDate);
  }

  if (endDate) {
    params.set("endDate", endDate);
  }

  if (guildId) {
    params.set("guildId", String(guildId));
  }

  if (userId) {
    params.set("userId", String(userId));
  }

  if (channelId) {
    params.set("channelId", channelId);
  }

  if (view && view !== "summary") {
    params.set("view", view);
  }

  if (typeof minActivity === "number" && minActivity !== 1) {
    params.set("minActivity", String(minActivity));
  }

  if (sortDirection === "asc") {
    params.set("sortDirection", sortDirection);
  }

  return `/?${params.toString()}`;
}

function stockDrillHref(stock: DashboardRecentStock, days: number, startDate: string, endDate: string) {
  if (stock.entityType === "User") {
    return dashboardHref({ scope: "user", userId: stock.entityId, days, startDate, endDate });
  }

  if (stock.entityType === "Server") {
    return dashboardHref({ scope: "server", guildId: stock.entityId, days, startDate, endDate });
  }

  return undefined;
}

function normalizeDateWindow(days: number, startDate?: string, endDate?: string): DashboardDateWindow {
  const today = todayUtc();
  let end = endDate ? parseDateString(endDate) : today;
  let start = startDate ? parseDateString(startDate) : addUtcDays(end, -(days - 1));

  if (start > end) {
    [start, end] = [end, start];
  }

  if (end > today) {
    end = today;
  }

  if (start > end) {
    start = end;
  }

  const rangeDays = differenceInUtcDays(start, end) + 1;
  if (rangeDays > maxDashboardDateWindowDays) {
    start = addUtcDays(end, -(maxDashboardDateWindowDays - 1));
  }

  return {
    days: differenceInUtcDays(start, end) + 1,
    startDate: formatDateParam(start),
    endDate: formatDateParam(end),
  };
}

function parseDateParam(value: string | undefined) {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return undefined;
  }

  const date = parseDateString(value);
  return formatDateParam(date) === value ? value : undefined;
}

function parseDateString(value: string) {
  const [year, month, day] = value.split("-").map((part) => Number.parseInt(part, 10));
  return new Date(Date.UTC(year, month - 1, day));
}

function formatDateParam(date: Date) {
  return date.toISOString().slice(0, 10);
}

function todayUtc() {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
}

function addUtcDays(date: Date, days: number) {
  const next = new Date(date);
  next.setUTCDate(next.getUTCDate() + days);
  return next;
}

function differenceInUtcDays(start: Date, end: Date) {
  return Math.round((end.getTime() - start.getTime()) / 86400000);
}

function formatDateWindowLabel(startDate: string, endDate: string, days: number) {
  if (days === 1) {
    return formatDateLabel(startDate);
  }

  return `${formatDateLabel(startDate)} - ${formatDateLabel(endDate)} (${days} days)`;
}

function formatDateLabel(value: string) {
  return new Intl.DateTimeFormat("en", {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  }).format(parseDateString(value));
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

function parseDashboardView(value: string | undefined): DashboardView {
  if (
    value === "activity" ||
    value === "servers" ||
    value === "users" ||
    value === "quotes" ||
    value === "economy" ||
    value === "stocks" ||
    value === "operations" ||
    value === "settings"
  ) {
    return value;
  }

  return "summary";
}

function normalizeDashboardFilters(filters: {
  guildId?: number;
  userId?: number;
  channelId?: string;
  days: number;
  startDate: string;
  endDate: string;
  scope: DashboardScope;
  sortDirection: SortDirection;
  minActivity: number;
}) {
  let scope = filters.scope;

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
