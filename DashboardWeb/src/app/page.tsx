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
  ActivityComparisonChart,
  ActivityDistributionBars,
  ActivityDistributionDonut,
  ActivityScatterChart,
  ActivityTreemap,
  CalendarActivityHeatmap,
  ChannelActivityHeatmap,
  ChannelHourHeatmap,
  ActivityInsightChart,
  ButtonScoreTimelineChart,
  ButtonGameChart,
  CategoryBars,
  CumulativeXpChart,
  DonutChart,
  EconomyFlowChart,
  EconomyHeatmap,
  EconomyPoolChart,
  EconomyStackedVolumeChart,
  GlobalActivityLineChart,
  LevelProgressionGraph,
  LogErrorWarningLineChart,
  LogTimelineChart,
  MessageLengthHistogramChart,
  MessageLengthBoxPlotChart,
  MessageLengthTrendChart,
  MessagesOverTimeChart,
  MoneyFlowView,
  MoneySupplyLineChart,
  ParetoActivityChart,
  QuoteAuthorBarChart,
  QuoteCreationTimelineChart,
  QuoteScoreTrendChart,
  QuoteServerComparisonChart,
  ReminderVolumeChart,
  ReminderTimelineChart,
  ServerDayHeatmap,
  StackedServerActivityChart,
  StockActivityPriceComparisonChart,
  StockTradeVolumeChart,
  TemporaryBanTimelineChart,
  TransactionTimelineChart,
  UserRankTimelineChart,
  UserRankMovementChart,
  XpPerDayChart,
} from "@/components/dashboard/insight-charts";
import { ThemeToggle } from "@/components/dashboard/theme-toggle";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getDashboardData } from "@/lib/dashboard-api";
import type {
  DashboardChannelActivity,
  DashboardDrilldownData,
  DashboardEconomyActor,
  DashboardEconomyEventItem,
  DashboardEconomyInsights,
  DashboardActivityAnalytics,
  DashboardActivityLeaderboardSet,
  DashboardGlobalChannelActivity,
  DashboardGlobalOverviewResponse,
  DashboardGlobalServerActivity,
  DashboardGlobalUserActivity,
  DashboardGlobalWealthUser,
  DashboardGuildSettingsSummary,
  DashboardGuildSummary,
  DashboardInsightsResponse,
  DashboardLeaderboardItem,
  DashboardButtonGameGap,
  DashboardButtonGameInsights,
  DashboardButtonGameScoreEntry,
  DashboardButtonGameServer,
  DashboardButtonGameUser,
  DashboardLogItem,
  DashboardLogInsights,
  DashboardModerationStats,
  DashboardOperationsInsights,
  DashboardPopularQuote,
  DashboardQuoteApprovalRequestItem,
  DashboardQuoteAuthorSummary,
  DashboardQuoteCandidate,
  DashboardQuoteInsights,
  DashboardQuoteItem,
  DashboardQuoteManagementItem,
  DashboardQuoteRankedItem,
  DashboardQuoteSetupSummary,
  DashboardQuoteVoteItem,
  DashboardRankedUserMetric,
  DashboardRecentEntity,
  DashboardRecentQuote,
  DashboardRecentStock,
  DashboardReactionRoleUsage,
  DashboardReminderItem,
  DashboardReminderStats,
  DashboardServerInsights,
  DashboardServerConfigurationScorecard,
  DashboardServerSetupIssue,
  DashboardStockMover,
  DashboardStockHoldingItem,
  DashboardStockHoldingSummary,
  DashboardStockMarketInsights,
  DashboardStockTableItem,
  DashboardTimeBucket,
  DashboardUserContribution,
  DashboardUserOutcomeStats,
  DashboardUserProfileInsights,
  DashboardUserRankSnapshot,
  DashboardUserReminderTimelineItem,
  DashboardUserServerLevel,
  DashboardUserStockHolding,
  DashboardUserTransactionItem,
  DashboardUserTrend,
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
  const requestedChannelId = parseOptionalDiscordId(getParam(params.channelId));
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
  const userOptions = insights?.filterOptions.users ?? data.filterOptions.users;
  const channelOptions = insights?.filterOptions.channels ?? data.filterOptions.channels;
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
  const globalHrefBase = { scope: "global" as const, days, startDate, endDate };

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-[1540px] auto-rows-max content-start gap-5 px-4 py-4 sm:px-6 sm:py-5 lg:px-8">
      <section className="rounded-lg border border-border bg-card shadow-sm">
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
        <DashboardStatusBanner
          detail={data.error ?? "The dashboard API did not respond."}
          title="Demo data active"
        />
      )}

      {data.drilldownError && (
        <DashboardStatusBanner
          detail={data.drilldownError}
          title="Scoped data unavailable"
        />
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

      <DashboardViewContext
        activeView={dashboardView}
        dataMode={data.usingDemoData ? "Demo data" : "Live API"}
        dateWindowLabel={dateWindowLabel}
        minActivity={minActivity}
        scope={scope}
        scopeLabel={scopeLabel}
      />

      <DashboardAnswerStrip
        activeView={dashboardView}
        days={days}
        endDate={endDate}
        guildId={guildId}
        channelId={channelId}
        global={global}
        insights={insights}
        scope={scope}
        startDate={startDate}
        userId={userId}
      />

      {!drilldownActive && dashboardView === "summary" && (
      <section id="global-overview" className="grid gap-5">
        <DashboardMetricGroup
          description="Core participation signals across every known Morpheus server."
          id="global-community"
          title="Community footprint"
        >
          <StatCard icon={Server} label="Servers" value={formatInteger(totals.totalServers)} meta="tracked globally" tone="blue" href={dashboardHref({ ...globalHrefBase, view: "servers" })} />
          <StatCard icon={Users} label="Known users" value={formatInteger(totals.totalKnownUsers)} meta="unique Discord users" tone="cyan" href={dashboardHref({ ...globalHrefBase, view: "users" })} />
          <StatCard icon={MessageSquareText} label="Messages" value={formatCompactNumber(totals.totalTrackedMessages)} meta={`${formatCompactNumber(totals.latestDayMessages)} in latest day`} tone="green" href={dashboardHref({ ...globalHrefBase, view: "activity" })} />
          <StatCard icon={TrendingUp} label="XP generated" value={formatCompactNumber(totals.totalXpGenerated)} meta={`${formatCompactNumber(totals.latestDayXpGenerated)} in latest day`} tone="green" href={dashboardHref({ ...globalHrefBase, view: "activity" })} />
        </DashboardMetricGroup>

        <DashboardMetricGroup
          description="Quote culture and moderation pressure that may need human follow-through."
          id="global-culture"
          title="Culture and moderation"
        >
          <StatCard icon={Quote} label="Quotes" value={formatInteger(totals.totalQuotes)} meta={`${formatInteger(totals.totalApprovedQuotes)} approved`} tone="rose" href={dashboardHref({ ...globalHrefBase, view: "quotes" })} />
          <StatCard icon={ShieldCheck} label="Approved quotes" value={formatInteger(totals.totalApprovedQuotes)} meta={`${formatInteger(totals.pendingQuotes)} pending quotes`} tone="rose" href={dashboardHref({ ...globalHrefBase, view: "quotes" })} />
          <StatCard icon={AlertTriangle} label="Quote approvals" value={formatInteger(totals.pendingQuoteApprovals)} meta="approval messages open" tone="amber" href={dashboardHref({ ...globalHrefBase, view: "quotes" })} />
          <StatCard icon={AlertTriangle} label="Warnings/errors" value={formatInteger(totals.recentWarningsOrErrors)} meta="last 24 hours" tone={totals.recentWarningsOrErrors > 0 ? "rose" : "green"} href={dashboardHref({ ...globalHrefBase, view: "operations" })} />
        </DashboardMetricGroup>

        <DashboardMetricGroup
          description="Wallet supply, net worth, shared reserves, and economy event volume."
          id="global-economy"
          title="Economy health"
        >
          <StatCard icon={Wallet} label="Economy balance" value={formatCurrency(totals.totalEconomyBalance)} meta="cash in wallets" tone="amber" href={dashboardHref({ ...globalHrefBase, view: "economy" })} />
          <StatCard icon={Banknote} label="Net worth" value={formatCurrency(totals.totalEstimatedNetWorth)} meta="cash plus portfolios" tone="amber" href={dashboardHref({ ...globalHrefBase, view: "economy" })} />
          <StatCard icon={Database} label="UBI pool" value={formatCurrency(totals.ubiPoolSize)} meta="community reserve" tone="cyan" href={dashboardHref({ ...globalHrefBase, view: "economy" })} />
          <StatCard icon={Activity} label="Transactions" value={formatCompactNumber(totals.totalTransactions)} meta="economy event log" tone="slate" href={dashboardHref({ ...globalHrefBase, view: "economy" })} />
        </DashboardMetricGroup>

        <DashboardMetricGroup
          columnsClassName="md:grid-cols-2 xl:grid-cols-3"
          description="Interactive queues and game systems that affect daily bot operations."
          id="global-games"
          title="Games and queues"
        >
          <StatCard icon={Gamepad2} label="Slots vault" value={formatCurrency(totals.slotsVaultSize)} meta="jackpot backing" tone="blue" href={dashboardHref({ ...globalHrefBase, view: "economy" })} />
          <StatCard icon={Gamepad2} label="Button presses" value={formatCompactNumber(totals.totalButtonPresses)} meta="all-time presses" tone="cyan" href={dashboardHref({ ...globalHrefBase, view: "operations" })} />
          <StatCard icon={Bell} label="Reminders" value={formatInteger(totals.activeReminders)} meta="active queue" tone="blue" href={dashboardHref({ ...globalHrefBase, view: "operations" })} />
        </DashboardMetricGroup>
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

      {!drilldownActive && dashboardView === "activity" && insights?.activityAnalytics && (
        <ActivityAnalyticsSection
          activity={insights.activity}
          analytics={insights.activityAnalytics}
          days={days}
          scopeLabel="Global"
        />
      )}

      {!drilldownActive && dashboardView === "economy" && !insights && (
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

      {!drilldownActive && dashboardView === "economy" && !insights && (
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
        insights ? (
          <StockAnalyticsSection days={days} endDate={endDate} insights={insights.stocks} startDate={startDate} />
        ) : (
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
        )
      )}

      {!drilldownActive && dashboardView === "quotes" && (
        insights ? (
          <QuoteAnalyticsSection
            days={days}
            endDate={endDate}
            insights={insights.quotes}
            scope={scope}
            startDate={startDate}
          />
        ) : (
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
        )
      )}

      {!drilldownActive && dashboardView === "activity" && (
      <section className="grid grid-cols-1 gap-4">
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
      </section>
      )}

      {!drilldownActive && dashboardView === "summary" && (
      <section className="grid grid-cols-1 gap-4">
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
        insights ? (
          <EconomyAnalyticsSection days={days} endDate={endDate} insights={insights.economy} startDate={startDate} />
        ) : (
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
        )
      )}

      {!drilldownActive && dashboardView === "operations" && (
        insights ? (
          <OperationsAnalyticsSection buttonGame={insights.buttonGame} operations={insights.operations} />
        ) : (
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
        )
      )}

      {drilldownActive && (
        <section className="grid grid-cols-1 gap-4" aria-label="Scoped dashboard view">
          {insights && drilldown ? (
            <>

      {dashboardView === "summary" && scope === "server" && insights.server ? (
        <ServerDashboardSummary
          days={days}
          drilldown={drilldown}
          endDate={endDate}
          insights={insights}
          server={insights.server}
          startDate={startDate}
        />
      ) : dashboardView === "summary" && scope === "user" && insights.userProfile ? (
        <UserDashboardSummary
          days={days}
          endDate={endDate}
          guildId={guildId}
          profile={insights.userProfile}
          startDate={startDate}
        />
      ) : dashboardView === "summary" && (
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
      <ActivityAnalyticsSection
        activity={insights.activity}
        analytics={insights.activityAnalytics}
        days={days}
        scopeLabel={scope === "server" ? "Server" : scope === "channel" ? "Channel" : "User"}
      />
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
        <LeaderboardCard days={days} endDate={endDate} title="XP Leaders" metric="XP" items={drilldown.xpLeaderboard.items} startDate={startDate} />
        <LeaderboardCard days={days} endDate={endDate} title="Message Leaders" metric="messages" items={drilldown.messageLeaderboard.items} startDate={startDate} />
      </section>
        </>
      )}

      {dashboardView === "quotes" && (
        <QuoteAnalyticsSection
          days={days}
          endDate={endDate}
          guildId={guildId}
          insights={insights.quotes}
          scope={scope}
          startDate={startDate}
          userId={userId}
        />
      )}

      {dashboardView === "economy" && (
        <EconomyAnalyticsSection days={days} endDate={endDate} insights={insights.economy} startDate={startDate} />
      )}

      {dashboardView === "stocks" && (
        <StockAnalyticsSection days={days} endDate={endDate} insights={insights.stocks} startDate={startDate} />
      )}

      {dashboardView === "operations" && (
        <OperationsAnalyticsSection buttonGame={insights.buttonGame} operations={insights.operations} />
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

function UserDashboardSummary({
  days,
  endDate,
  guildId,
  profile,
  startDate,
}: {
  days: number;
  endDate: string;
  guildId?: number;
  profile: DashboardUserProfileInsights;
  startDate: string;
}) {
  const totals = profile.totals;
  const profileHrefBase = {
    scope: "user" as const,
    userId: profile.identity.userId,
    guildId,
    days,
    startDate,
    endDate,
  };
  const serverContribution = profile.serverContribution.map((item) => ({ label: item.label, value: item.messages }));
  const channelContribution = profile.channelContribution.map((item) => ({ label: item.label, value: item.messages }));
  const portfolioBreakdown = profile.stockHoldings.map((holding) => ({ label: holding.name, value: holding.value }));

  return (
    <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.25fr_0.75fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>{profile.identity.username}</CardTitle>
              <CardDescription>Known as {profile.identity.discordId} since {formatRelativeDate(profile.identity.insertedAtUtc)}.</CardDescription>
            </div>
            <Badge variant={profile.identity.levelUpMessages ? "success" : "muted"}>Level messages {yesNo(profile.identity.levelUpMessages)}</Badge>
          </CardHeader>
          <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <MetricPill label="Most active server" value={totals.mostActiveServer} />
            <MetricPill label="Most active channel" value={totals.mostActiveChannel} />
            <MetricPill label="Known servers" value={formatInteger(totals.knownServers)} />
            <MetricPill label="Known channels" value={formatInteger(totals.knownChannels)} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Rank Position</CardTitle>
              <CardDescription>Global and per-server XP rank based on known level records.</CardDescription>
            </div>
            <Badge variant="muted">{rankLabel(profile.globalRank)}</Badge>
          </CardHeader>
          <CardContent>
            <UserRankSnapshotList ranks={[profile.globalRank, ...profile.serverRanks.slice(0, 4)]} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard icon={TrendingUp} label="Total XP" value={formatCompactNumber(totals.totalXp)} meta={`Level ${formatInteger(totals.globalLevel)}`} tone="green" href={dashboardHref({ ...profileHrefBase, view: "activity" })} />
        <StatCard icon={MessageSquareText} label="Messages" value={formatCompactNumber(totals.totalMessages)} meta={`${totals.averageMessageLength.toFixed(1)} chars avg`} tone="cyan" href={dashboardHref({ ...profileHrefBase, view: "activity" })} />
        <StatCard icon={Gauge} label="XP per message" value={totals.xpPerMessage.toFixed(1)} meta={`${totals.messageLengthMovingAverage.toFixed(1)} chars moving avg`} tone="blue" />
        <StatCard icon={Quote} label="Quote impact" value={formatInteger(totals.quoteScoresReceived)} meta={`${formatInteger(totals.quoteContributions)} quotes, ${formatInteger(totals.quoteVotesGiven)} votes`} tone="rose" href={dashboardHref({ ...profileHrefBase, view: "quotes" })} />
        <StatCard icon={Wallet} label="Balance" value={formatCurrency(totals.economyBalance)} meta={`${formatCurrency(totals.estimatedNetWorth)} net worth`} tone="amber" href={dashboardHref({ ...profileHrefBase, view: "economy" })} />
        <StatCard icon={Banknote} label="Portfolio" value={formatCurrency(totals.portfolioValue)} meta={`${formatCurrency(profile.economyPerformance.unrealizedGains)} unrealized`} tone="slate" href={dashboardHref({ ...profileHrefBase, view: "stocks" })} />
        <StatCard icon={Gamepad2} label="Button score" value={formatCompactNumber(totals.buttonScore)} meta={`${formatInteger(profile.buttonGame.presses)} presses`} tone="cyan" href={dashboardHref({ ...profileHrefBase, view: "operations" })} />
        <StatCard icon={Activity} label="Last activity" value={formatRelativeDate(totals.lastActivityAtUtc)} meta={dateWindowSummary(days)} tone="green" />
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>XP Over Time</CardTitle>
              <CardDescription>Daily XP gains and cumulative XP for this profile window.</CardDescription>
            </div>
            <TrendBadge value={profile.activity.trendPercent} />
          </CardHeader>
          <CardContent className="grid gap-4 lg:grid-cols-2">
            <XpPerDayChart points={profile.activity.points} />
            <CumulativeXpChart points={profile.activity.points} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Level Progression</CardTitle>
              <CardDescription>Estimated level path from all-time XP and selected-window gains.</CardDescription>
            </div>
            <Badge variant="muted">Level {formatInteger(totals.globalLevel)}</Badge>
          </CardHeader>
          <CardContent>
            <LevelProgressionGraph points={profile.levelProgression} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Daily Messages</CardTitle>
              <CardDescription>Message cadence, moving activity, and active-day streaks.</CardDescription>
            </div>
            <Badge variant="muted">{formatInteger(profile.activityStreaks.activeDays)} active days</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <MessagesOverTimeChart points={profile.activity.points} />
            <UserStreakPanel streaks={profile.activityStreaks} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Message Length</CardTitle>
              <CardDescription>Average length over time and distribution of message sizes.</CardDescription>
            </div>
            <Badge variant="muted">{totals.messageLengthMovingAverage.toFixed(1)} chars EMA</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <MessageLengthTrendChart points={profile.messageLengthTrend} />
            <MessageLengthHistogramChart buckets={profile.messageLengthHistogram} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Calendar</CardTitle>
              <CardDescription>Daily activity concentration across the selected window.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CalendarActivityHeatmap cells={profile.activityCalendar} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Hour by Weekday</CardTitle>
              <CardDescription>UTC hour habits by weekday for this user.</CardDescription>
            </div>
            <Clock3 className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityHeatmap cells={profile.hourByWeekdayHeatmap} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Best Activity Days</CardTitle>
              <CardDescription>Highest-output days in the current window.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <TimeBucketList rows={profile.bestActivityDays} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Worst Activity Days</CardTitle>
              <CardDescription>Quietest days, including zero-message days.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <TimeBucketList rows={profile.worstActivityDays} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Breakdown</CardTitle>
              <CardDescription>Share of this user's messages by server.</CardDescription>
            </div>
            <Server className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4">
            <DonutChart data={serverContribution} labelKey="label" />
            <UserContributionList contributions={profile.serverContribution} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Channel Breakdown</CardTitle>
              <CardDescription>Top channels by messages and XP contribution.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4">
            <CategoryBars data={channelContribution} />
            <UserContributionList contributions={profile.channelContribution} compact />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Per-Server Levels</CardTitle>
              <CardDescription>All known server levels, ranks, XP, messages, and length moving averages.</CardDescription>
            </div>
            <Users className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <UserServerLevelsTable levels={profile.serverLevels} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Rank Movement</CardTitle>
              <CardDescription>Window-based rank movement where activity history can support it.</CardDescription>
            </div>
            <Badge variant="muted">{rankLabel(profile.globalRank)}</Badge>
          </CardHeader>
          <CardContent>
            <UserRankTimelineChart points={profile.rankMovement} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Author Performance</CardTitle>
              <CardDescription>Contribution count, approval state, received score, and votes cast.</CardDescription>
            </div>
            <Badge variant={profile.quotePerformance.scoreReceived >= 0 ? "success" : "danger"}>
              {profile.quotePerformance.scoreReceived >= 0 ? "+" : ""}{formatInteger(profile.quotePerformance.scoreReceived)}
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <div className="grid gap-3 sm:grid-cols-4">
              <MetricPill label="Approved" value={formatInteger(profile.quotePerformance.approved)} />
              <MetricPill label="Pending" value={formatInteger(profile.quotePerformance.pending)} />
              <MetricPill label="Removed" value={formatInteger(profile.quotePerformance.removed)} />
              <MetricPill label="Votes given" value={formatInteger(profile.quotePerformance.votesGiven)} />
            </div>
            <CategoryBars data={profile.quotePerformance.scoreByServer} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Recent Quotes</CardTitle>
              <CardDescription>Newest quote contributions from this user.</CardDescription>
            </div>
            <Quote className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <QuotesList days={days} endDate={endDate} quotes={profile.quotePerformance.recentQuotes} startDate={startDate} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Economy Performance</CardTitle>
              <CardDescription>Wallet flow, fees, realized trading proxy, and game outcomes.</CardDescription>
            </div>
            <Badge variant="muted">{formatCurrency(profile.economyPerformance.transactionVolume)} volume</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <TransactionTimelineChart points={profile.economyPerformance.dailyFlow} />
            <div className="grid gap-3 sm:grid-cols-4">
              <MetricPill label="Fees paid" value={formatCurrency(profile.economyPerformance.feesPaid)} />
              <MetricPill label="Realized P/L proxy" value={formatCurrency(profile.economyPerformance.realizedGains)} />
              <MetricPill label="Unrealized P/L" value={formatCurrency(profile.economyPerformance.unrealizedGains)} />
              <MetricPill label="Trades" value={formatInteger(profile.economyPerformance.trades)} />
            </div>
            <CategoryBars currency data={profile.economyPerformance.transactionTypes} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Portfolio Breakdown</CardTitle>
              <CardDescription>Marked-to-market holdings and unrealized gains.</CardDescription>
            </div>
            <Badge variant="muted">{formatCurrency(profile.economyPerformance.portfolioValue)}</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <DonutChart data={portfolioBreakdown} labelKey="label" />
            <UserStockHoldingsTable holdings={profile.stockHoldings} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Wins and Losses</CardTitle>
              <CardDescription>Slots, robbery, and donation activity in the selected window.</CardDescription>
            </div>
            <Gamepad2 className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-3">
            <UserOutcomeCard title="Slots" outcome={profile.economyPerformance.slots} />
            <UserOutcomeCard title="Robbery" outcome={profile.economyPerformance.robbery} />
            <div className="rounded-lg border border-border bg-slate-50 p-3">
              <div className="text-sm font-semibold text-foreground">Donations</div>
              <div className="mt-2 text-2xl font-semibold text-foreground">{formatCurrency(profile.economyPerformance.donations.total)}</div>
              <div className="mt-1 text-xs text-muted">{formatInteger(profile.economyPerformance.donations.count)} donations</div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Transaction History</CardTitle>
              <CardDescription>Recent transactions and stock trades involving this user.</CardDescription>
            </div>
            <Wallet className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <UserTransactionsList transactions={profile.economyPerformance.recentTransactions} title="Recent" />
            <UserTransactionsList transactions={profile.economyPerformance.tradingHistory} title="Trades" />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button-Game Records</CardTitle>
              <CardDescription>Daily score, best single press, cumulative score timeline.</CardDescription>
            </div>
            <Badge variant="muted">{formatCompactNumber(profile.buttonGame.bestScore)} best</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <ButtonGameChart points={profile.buttonGame.daily} />
            <ButtonScoreTimelineChart points={profile.buttonGame.scoreTimeline} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminder Timeline</CardTitle>
              <CardDescription>Upcoming and overdue reminders created for this user.</CardDescription>
            </div>
            <Bell className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4">
            <ReminderTimelineChart reminders={profile.reminders} />
            <UserReminderTimelineList reminders={profile.reminders} />
          </CardContent>
        </Card>
      </section>
    </>
  );
}

function ServerDashboardSummary({
  days,
  drilldown,
  endDate,
  insights,
  server,
  startDate,
}: {
  days: number;
  drilldown: DashboardDrilldownData;
  endDate: string;
  insights: DashboardInsightsResponse;
  server: DashboardServerInsights;
  startDate: string;
}) {
  const totals = server.totals;
  const guildId = server.identity.guildId;
  const activityPoints = insights.activity.points;
  const calendarCells = activityPoints.map((point) => ({
    dateUtc: point.dateUtc,
    messages: point.messages,
    xp: point.xp,
    activeUsers: point.activeUsers,
  }));
  const channelBars = insights.channels.map((channel) => ({
    label: channel.name,
    value: channel.messages,
  }));
  const quoteFunnel = [
    { label: "Total", value: totals.totalQuotes },
    { label: "Approved", value: totals.approvedQuotes },
    { label: "Pending", value: totals.pendingQuotes },
    { label: "Approval queue", value: totals.pendingQuoteApprovals },
    { label: "Removed", value: totals.removedQuotes },
  ];

  return (
    <>
      <section className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-5">
        <StatCard icon={Server} label="Server" value={server.identity.name} meta={server.identity.discordId} tone="blue" />
        <StatCard icon={Users} label="Known users" value={formatInteger(totals.knownUsers)} meta="tracked in this server" tone="cyan" />
        <StatCard icon={MessageSquareText} label="Tracked messages" value={formatCompactNumber(totals.trackedMessages)} meta={`${formatCompactNumber(insights.activity.messages)} in window`} tone="green" />
        <StatCard icon={TrendingUp} label="Total XP" value={formatCompactNumber(totals.totalXp)} meta={`${formatCompactNumber(insights.activity.xp)} XP in window`} tone="green" />
        <StatCard icon={Quote} label="Total quotes" value={formatInteger(totals.totalQuotes)} meta={`${formatInteger(totals.approvedQuotes)} approved`} tone="rose" href={dashboardHref({ scope: "server", guildId, days, startDate, endDate, view: "quotes" })} />
        <StatCard icon={ShieldCheck} label="Approved quotes" value={formatInteger(totals.approvedQuotes)} meta={`${formatInteger(totals.pendingQuotes)} pending`} tone="rose" href={dashboardHref({ scope: "server", guildId, days, startDate, endDate, view: "quotes" })} />
        <StatCard icon={AlertTriangle} label="Pending approvals" value={formatInteger(totals.pendingQuoteApprovals)} meta="quote approval messages" tone={totals.pendingQuoteApprovals > 0 ? "amber" : "green"} href={dashboardHref({ scope: "server", guildId, days, startDate, endDate, view: "quotes" })} />
        <StatCard icon={Bell} label="Active reminders" value={formatInteger(totals.activeReminders)} meta="server reminder queue" tone="blue" href={dashboardHref({ scope: "server", guildId, days, startDate, endDate, view: "operations" })} />
        <StatCard icon={Gamepad2} label="Button presses" value={formatCompactNumber(totals.buttonPresses)} meta={`${formatCompactNumber(insights.buttonGame.presses)} in window`} tone="cyan" href={dashboardHref({ scope: "server", guildId, days, startDate, endDate, view: "operations" })} />
        <StatCard icon={Banknote} label="Economy activity" value={formatCurrency(totals.economyVolume)} meta={`${formatCompactNumber(totals.economyTransactions)} transactions`} tone="amber" href={dashboardHref({ scope: "server", guildId, days, startDate, endDate, view: "economy" })} />
        <StatCard icon={TrendingUp} label="Stock activity" value={formatInteger(totals.stockActivity)} meta={`${formatCurrency(totals.stockMarketValue)} market value`} tone="slate" href={dashboardHref({ scope: "server", guildId, days, startDate, endDate, view: "stocks" })} />
        <StatCard icon={Activity} label="Last activity" value={formatRelativeDate(totals.lastActivityAtUtc)} meta={dateWindowSummary(days)} tone="green" />
      </section>

      <section className="grid grid-cols-1 items-start gap-4 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Health</CardTitle>
              <CardDescription>Activity, configuration, operations, and engagement score.</CardDescription>
            </div>
            <Badge variant={server.health.score >= 70 ? "success" : server.health.score >= 50 ? "warning" : "danger"}>
              {server.health.label}
            </Badge>
          </CardHeader>
          <CardContent>
            <ServerHealthScorecard server={server} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Configuration</CardTitle>
              <CardDescription>Channels, feature flags, and quote approval policy.</CardDescription>
            </div>
            <Settings className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-4">
            <ServerConfigurationPanel server={server} />
            <ServerConfigurationChecklist server={server} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Activity</CardTitle>
              <CardDescription>Messages and active users in the selected window.</CardDescription>
            </div>
            <TrendBadge value={insights.activity.trendPercent} />
          </CardHeader>
          <CardContent>
            <GlobalActivityLineChart points={activityPoints} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Cumulative XP</CardTitle>
              <CardDescription>XP accumulated by this server over the selected window.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CumulativeXpChart points={activityPoints} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Daily Messages</CardTitle>
              <CardDescription>Tracked message count by day.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <MessagesOverTimeChart points={activityPoints} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>XP Per Day</CardTitle>
              <CardDescription>Daily XP generated from tracked activity.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <XpPerDayChart points={activityPoints} />
          </CardContent>
        </Card>

        <Card className="xl:col-span-2">
          <CardHeader>
            <div>
              <CardTitle>Rolling Activity</CardTitle>
              <CardDescription>Daily messages, seven-day rolling average, and cumulative messages.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityInsightChart points={activityPoints} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Channel Heatmap</CardTitle>
              <CardDescription>Top channel activity by day.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ChannelActivityHeatmap cells={server.channelHeatmap} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Channel Activity</CardTitle>
              <CardDescription>Most active channels by message volume.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CategoryBars data={channelBars} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Hour by Weekday</CardTitle>
              <CardDescription>UTC message concentration across the week.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityHeatmap cells={insights.heatmap} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Calendar Activity</CardTitle>
              <CardDescription>Server activity density by day.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CalendarActivityHeatmap cells={calendarCells} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <LeaderboardCard days={days} endDate={endDate} title="Top Users by XP" metric="XP" items={drilldown.xpLeaderboard.items} startDate={startDate} />
        <LeaderboardCard days={days} endDate={endDate} title="Top Users by Messages" metric="messages" items={drilldown.messageLeaderboard.items} startDate={startDate} />

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Top Average Message Length</CardTitle>
              <CardDescription>Users with the longest average tracked messages.</CardDescription>
            </div>
            <Users className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <RankedMetricList rows={server.topUsersByAverageMessageLength} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>User Rank Movement</CardTitle>
              <CardDescription>XP rank change between the two halves of the selected window.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <UserRankMovementChart rows={server.userRankMovement} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Fastest Rising Users</CardTitle>
              <CardDescription>Message momentum compared with the prior half-window.</CardDescription>
            </div>
            <ArrowUpRight className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <UserTrendList rows={server.fastestRisingUsers} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Dropping</CardTitle>
              <CardDescription>Users with the largest message-volume drop.</CardDescription>
            </div>
            <AlertTriangle className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <UserTrendList rows={server.droppingUsers} negative />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Active Channels</CardTitle>
              <CardDescription>Highest message channels in the selected window.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ChannelsTable channels={insights.channels} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quietest Channels</CardTitle>
              <CardDescription>Lowest message channels that still pass the minimum activity filter.</CardDescription>
            </div>
            <Clock3 className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ChannelsTable channels={server.quietestChannels} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-4">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Best Days</CardTitle>
              <CardDescription>Highest message days.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <TimeBucketList rows={server.bestActivityDays} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Worst Days</CardTitle>
              <CardDescription>Lowest message days.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <TimeBucketList rows={server.worstActivityDays} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Peak Hours</CardTitle>
              <CardDescription>Most active UTC hours.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <TimeBucketList rows={server.peakHours} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Peak Weekdays</CardTitle>
              <CardDescription>Most active days of week.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <TimeBucketList rows={server.peakWeekdays} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Role Percentiles</CardTitle>
              <CardDescription>Tracked users by activity-role percentile band.</CardDescription>
            </div>
            <ShieldCheck className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CategoryBars data={server.activityRoleDistribution} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Approval Funnel</CardTitle>
              <CardDescription>Total, approved, pending, approval queue, and removed quotes.</CardDescription>
            </div>
            <Quote className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CategoryBars data={quoteFunnel} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Activity</CardTitle>
              <CardDescription>Quote status, scores, authors, and pending queue.</CardDescription>
            </div>
            <Badge variant={insights.quotes.pending > 0 ? "warning" : "success"}>
              {formatInteger(insights.quotes.pending)} pending
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <DonutChart data={insights.quotes.statuses} labelKey="status" />
            <QuoteAuthors authors={insights.quotes.authors} days={days} endDate={endDate} startDate={startDate} />
            <QuotesList days={days} emptyLabel="No pending quotes found" endDate={endDate} quotes={insights.quotes.recentPending} startDate={startDate} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Economy Activity</CardTitle>
              <CardDescription>Money flow, transaction mix, and wealth leaders.</CardDescription>
            </div>
            <Badge variant="muted">{formatCurrency(insights.economy.fees)} fees</Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <EconomyFlowChart points={insights.economy.dailyFlow} />
            <CategoryBars currency data={insights.economy.transactionTypes} />
            <WealthTable users={insights.economy.wealthLeaders} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Stock Movers</CardTitle>
              <CardDescription>Server-specific user, channel, and server stock movement.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <StockMoverList title="Winners" movers={insights.stocks.winners} positive />
            <StockMoverList title="Losers" movers={insights.stocks.losers} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Recent Server Logs</CardTitle>
              <CardDescription>Newest logs that mention this server, channel, or selected scope.</CardDescription>
            </div>
            <AlertTriangle className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <LogList logs={insights.operations.recentLogs} />
          </CardContent>
        </Card>
      </section>
    </>
  );
}

function ServerHealthScorecard({ server }: { server: DashboardServerInsights }) {
  const health = server.health;

  return (
    <div className="grid gap-3">
      <div className="grid gap-3 lg:grid-cols-[6.75rem_minmax(0,1fr)]">
        <div className="grid min-h-28 place-items-center rounded-lg border border-border bg-slate-50 px-3 py-4">
          <div className="text-center">
            <div className="text-4xl font-semibold leading-none text-foreground">{health.score}</div>
            <div className="mt-2 text-xs font-medium uppercase tracking-normal text-muted">of 100</div>
          </div>
        </div>
        <div className="grid gap-2 sm:grid-cols-2">
          <ScoreBar label="Activity" value={health.activityScore} />
          <ScoreBar label="Configuration" value={health.configurationScore} />
          <ScoreBar label="Operations" value={health.operationsScore} />
          <ScoreBar label="Engagement" value={health.engagementScore} />
        </div>
      </div>
      <div className="grid gap-2 sm:grid-cols-2">
        {health.notes.map((note) => (
          <div className="rounded-lg border border-border bg-slate-50 px-3 py-2 text-sm text-muted" key={note}>
            {note}
          </div>
        ))}
      </div>
    </div>
  );
}

function ScoreBar({ label, value }: { label: string; value: number }) {
  const safeValue = clamp(value, 0, 100);

  return (
    <div className="rounded-lg border border-border bg-slate-50 p-3">
      <div className="flex items-center justify-between gap-3 text-sm">
        <span className="font-medium text-foreground">{label}</span>
        <span className="text-muted">{value}</span>
      </div>
      <div
        className="mt-2 h-2 overflow-hidden rounded-full border border-border"
        style={{ backgroundColor: "color-mix(in srgb, var(--border) 44%, var(--card))" }}
      >
        <div
          className="h-full rounded-full"
          style={{
            background: "linear-gradient(90deg, var(--primary), var(--accent))",
            width: `${safeValue}%`,
          }}
        />
      </div>
    </div>
  );
}

function ServerConfigurationPanel({ server }: { server: DashboardServerInsights }) {
  const configuration = server.configuration;

  return (
    <div className="grid gap-2 md:grid-cols-2">
      <ConfigRow label="Prefix" value={configuration.prefix} />
      <ConfigRow label="Welcome channel" value={channelStatus(configuration.welcomeChannel)} />
      <ConfigRow label="Pins channel" value={channelStatus(configuration.pinsChannel)} />
      <ConfigRow label="Honeypot channel" value={channelStatus(configuration.honeypotChannel)} />
      <ConfigRow label="Honeypot messages" value={yesNo(configuration.honeypotMessages)} enabled={configuration.honeypotMessages} />
      <ConfigRow label="Level-up messages" value={yesNo(configuration.levelUpMessages)} enabled={configuration.levelUpMessages} />
      <ConfigRow label="Level-up message channel" value={channelStatus(configuration.levelUpMessageChannel)} />
      <ConfigRow label="Level-up quote messages" value={yesNo(configuration.levelUpQuoteMessages)} enabled={configuration.levelUpQuoteMessages} />
      <ConfigRow label="Level-up quote channel" value={channelStatus(configuration.levelUpQuoteChannel)} />
      <ConfigRow label="Quote approval channel" value={channelStatus(configuration.quoteApprovalChannel)} />
      <ConfigRow label="Quote add threshold" value={formatInteger(configuration.quoteAddRequiredApprovals)} />
      <ConfigRow label="Quote remove threshold" value={formatInteger(configuration.quoteRemoveRequiredApprovals)} />
      <ConfigRow label="Global quotes" value={yesNo(configuration.globalQuotes)} enabled={configuration.globalQuotes} />
      <ConfigRow label="Activity roles" value={yesNo(configuration.activityRoles)} enabled={configuration.activityRoles} />
    </div>
  );
}

function ActivityAnalyticsSection({
  activity,
  analytics,
  days,
  scopeLabel,
}: {
  activity: DashboardInsightsResponse["activity"];
  analytics: DashboardActivityAnalytics;
  days: number;
  scopeLabel: string;
}) {
  const latestDaily = analytics.dailyActiveUsers.at(-1);
  const latestWeekly = analytics.weeklyActiveUsers.at(-1);
  const latestMonthly = analytics.monthlyActiveUsers.at(-1);
  const leaderboardGroups = analytics.leaderboards.filter((set) => set.items.length > 0);

  return (
    <>
      <section className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard icon={TrendingUp} label={`${scopeLabel} XP`} value={formatCompactNumber(activity.xp)} meta={`${activity.xpPerMessage.toFixed(1)} XP/message`} tone="green" />
        <StatCard icon={MessageSquareText} label="Messages" value={formatCompactNumber(activity.messages)} meta={`${activity.averageMessageLength.toFixed(1)} chars avg`} tone="cyan" />
        <StatCard icon={Users} label="Daily active" value={formatInteger(latestDaily?.activeUsers ?? activity.activeUsers)} meta={`${formatInteger(latestWeekly?.activeUsers ?? 0)} weekly`} tone="blue" />
        <StatCard icon={Gauge} label="Activity streak" value={`${formatInteger(analytics.activityStreaks.currentStreakDays)}d`} meta={`${formatInteger(analytics.activityStreaks.longestStreakDays)}d best`} tone="slate" />
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Normal vs Rolling</CardTitle>
              <CardDescription>Daily messages, rolling average, and cumulative activity.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityInsightChart points={activity.points} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Daily XP vs Cumulative XP</CardTitle>
              <CardDescription>Daily XP bars and cumulative XP curve.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <CumulativeXpChart points={activity.points} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Comparison Explorer</CardTitle>
              <CardDescription>Users, channels, servers, and adjacent time windows on the same activity axis.</CardDescription>
            </div>
            <Badge variant="muted">{days} days</Badge>
          </CardHeader>
          <CardContent>
            <ActivityComparisonChart metric="messages" series={analytics.comparisonSeries} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>XP Comparison</CardTitle>
              <CardDescription>Daily XP against previous window and the busiest contributors.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityComparisonChart metric="xp" series={analytics.comparisonSeries} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Messages vs XP</CardTitle>
              <CardDescription>Contributor efficiency across users, channels, and servers.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityScatterChart points={analytics.messageCountVsXp} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Active Users</CardTitle>
              <CardDescription>Daily, weekly, and monthly active-user windows.</CardDescription>
            </div>
            <Badge variant="muted">{formatInteger(latestMonthly?.activeUsers ?? 0)} monthly</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <TimeBucketBars rows={analytics.dailyActiveUsers.slice(-14)} label="Daily" />
            <TimeBucketBars rows={analytics.weeklyActiveUsers.slice(-8)} label="Weekly" />
            <TimeBucketBars rows={analytics.monthlyActiveUsers.slice(-6)} label="Monthly" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Best Activity Periods</CardTitle>
              <CardDescription>Highest message days and peak UTC hours.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4">
            <TimeBucketList rows={analytics.bestActivityDays.slice(0, 5)} />
            <TimeBucketBars rows={analytics.peakHours} label="Peak hours" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Risk Periods</CardTitle>
              <CardDescription>Quietest days, weekday rhythm, and streak shape.</CardDescription>
            </div>
            <Clock3 className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4">
            <TimeBucketList rows={analytics.worstActivityDays.slice(0, 5)} />
            <TimeBucketBars rows={analytics.peakWeekdays} label="Weekdays" />
            <UserStreakPanel streaks={analytics.activityStreaks} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>XP Distribution</CardTitle>
              <CardDescription>Share of XP by user, server, and channel.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-5">
            <ActivityDistributionDonut data={analytics.xpByUser} metric="xp" />
            <ActivityDistributionBars data={analytics.xpByServer} metric="xp" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Contribution Breakdown</CardTitle>
              <CardDescription>Long-term message share across the busiest contributors.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4">
            <ActivityTreemap data={analytics.messageShareByUser} />
            <ActivityDistributionBars data={analytics.messageShareByChannel} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Pareto Share</CardTitle>
              <CardDescription>How quickly top users account for message volume.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ParetoActivityChart points={analytics.userContributionPareto} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Channel by Hour</CardTitle>
              <CardDescription>Top channels across UTC hours.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ChannelHourHeatmap cells={analytics.channelHourHeatmap} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server by Day</CardTitle>
              <CardDescription>Daily contribution density for the busiest servers.</CardDescription>
            </div>
            <Server className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ServerDayHeatmap cells={analytics.serverDayHeatmap} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Channel by Day</CardTitle>
              <CardDescription>Daily channel contribution heatmap.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ChannelActivityHeatmap cells={analytics.channelDayHeatmap} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Rank Movement</CardTitle>
              <CardDescription>Window-based rank movement where activity history supports it.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <UserRankMovementChart rows={analytics.rankMovement} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Message Length Trend</CardTitle>
              <CardDescription>Average length over time against message volume.</CardDescription>
            </div>
            <MessageSquareText className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <MessageLengthTrendChart points={analytics.messageLengthTrend} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Length Distribution</CardTitle>
              <CardDescription>Histogram and channel-level box plot for message length.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent className="grid gap-4">
            <MessageLengthHistogramChart buckets={analytics.messageLengthHistogram} />
            <MessageLengthBoxPlotChart points={analytics.messageLengthBoxPlots} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Length vs XP</CardTitle>
              <CardDescription>Average message length plotted against XP.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden />
          </CardHeader>
          <CardContent>
            <ActivityScatterChart points={analytics.averageLengthVsXp} xMetric="averageMessageLength" />
          </CardContent>
        </Card>
      </section>

      {leaderboardGroups.length > 0 && (
        <section className="grid grid-cols-1 gap-4 xl:grid-cols-2 2xl:grid-cols-3">
          {leaderboardGroups.map((set) => (
            <ActivityLeaderboardSetCard key={set.key} set={set} />
          ))}
        </section>
      )}
    </>
  );
}

function TimeBucketBars({ rows, label }: { rows: DashboardTimeBucket[]; label: string }) {
  const max = Math.max(1, ...rows.map((row) => row.messages));

  if (rows.length === 0) {
    return <EmptyRow label={`No ${label.toLowerCase()} data`} />;
  }

  return (
    <div className="grid gap-2">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      {rows.map((row) => (
        <div className="grid gap-1" key={`${label}-${row.label}`}>
          <div className="flex items-center justify-between gap-3 text-xs">
            <span className="truncate font-medium text-foreground">{row.label}</span>
            <span className="shrink-0 text-muted">{formatCompactNumber(row.messages)} - {formatInteger(row.activeUsers)} users</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-slate-100">
            <div className="h-full rounded-full bg-primary" style={{ width: `${Math.max(2, (row.messages / max) * 100)}%` }} />
          </div>
        </div>
      ))}
    </div>
  );
}

function ActivityLeaderboardSetCard({ set }: { set: DashboardActivityLeaderboardSet }) {
  const max = Math.max(1, ...set.items.map((item) => Math.abs(item.value)));

  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>{set.title}</CardTitle>
          <CardDescription>{set.metric.replaceAll("-", " ")} leaderboard for the selected analytics window.</CardDescription>
        </div>
        <Badge variant="muted">{set.unit}</Badge>
      </CardHeader>
      <CardContent>
        <div className="grid gap-2">
          {set.items.length === 0 ? (
            <EmptyRow label="No leaderboard data" />
          ) : (
            set.items.slice(0, 8).map((item) => (
              <div className="rounded-lg border border-border bg-white p-3" key={`${set.key}-${item.entityId}-${item.rank}`}>
                <div className="grid grid-cols-[2.5rem_minmax(0,1fr)_auto] items-center gap-3 text-sm">
                  <span className="font-semibold text-muted">#{item.rank}</span>
                  <div className="min-w-0">
                    <div className="truncate font-semibold text-foreground">{item.label}</div>
                    <div className="text-xs text-muted">
                      {formatCompactNumber(item.messages)} messages - {formatCompactNumber(item.xp)} XP
                      {item.level !== null ? ` - L${item.level}` : ""}
                    </div>
                  </div>
                  <div className="text-right font-semibold text-foreground">
                    {formatLeaderboardValue(item.value, set.unit)}
                  </div>
                </div>
                <div className="mt-2 h-2 overflow-hidden rounded-full bg-slate-100">
                  <div
                    className={cn("h-full rounded-full", item.value < 0 ? "bg-rose-500" : "bg-primary")}
                    style={{ width: `${Math.max(2, (Math.abs(item.value) / max) * 100)}%` }}
                  />
                </div>
                <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
                  <span>{item.averageMessageLength.toFixed(1)} chars</span>
                  <span>{item.xpPerMessage.toFixed(1)} XP/message</span>
                  {item.deltaPercent !== null && <span>{item.deltaPercent > 0 ? "+" : ""}{item.deltaPercent.toFixed(1)}%</span>}
                </div>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function formatLeaderboardValue(value: number, unit: string) {
  if (unit === "%" || unit === "chars" || unit === "levels") {
    return `${value.toFixed(unit === "levels" ? 0 : 1)}${unit === "%" ? "%" : ` ${unit}`}`;
  }

  return `${formatCompactNumber(value)} ${unit}`;
}

function ConfigRow({
  enabled,
  label,
  value,
}: {
  enabled?: boolean;
  label: string;
  value: string;
}) {
  return (
    <div className="flex min-w-0 items-center justify-between gap-3 rounded-lg border border-border bg-white px-3 py-2">
      <span className="min-w-0 truncate text-sm text-muted">{label}</span>
      {typeof enabled === "boolean" ? (
        <Badge variant={enabled ? "success" : "muted"}>{value}</Badge>
      ) : (
        <span className="min-w-0 truncate text-right text-sm font-semibold text-foreground">{value}</span>
      )}
    </div>
  );
}

function ServerConfigurationChecklist({ server }: { server: DashboardServerInsights }) {
  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">Configuration checklist</div>
      {server.configurationChecklist.map((item) => (
        <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={item.label}>
          <Badge variant={item.passed ? "success" : item.severity === "danger" ? "danger" : "warning"}>
            {item.passed ? "OK" : "Check"}
          </Badge>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{item.label}</div>
            <div className="truncate text-xs text-muted">{item.detail}</div>
          </div>
          <span className="text-xs text-muted">{item.severity}</span>
        </div>
      ))}
    </div>
  );
}

function RankedMetricList({ rows }: { rows: DashboardRankedUserMetric[] }) {
  if (rows.length === 0) {
    return <EmptyRow label="No users match the current filters" />;
  }

  return (
    <div className="grid gap-2">
      {rows.map((row) => (
        <div className="grid grid-cols-[3rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={row.userId}>
          <span className="text-sm font-semibold text-muted">#{row.rank}</span>
          <span className="truncate text-sm font-semibold text-foreground">{row.username}</span>
          <span className="text-sm text-muted">{row.value.toFixed(1)} {row.unit}</span>
        </div>
      ))}
    </div>
  );
}

function UserTrendList({
  negative = false,
  rows,
}: {
  negative?: boolean;
  rows: DashboardUserTrend[];
}) {
  if (rows.length === 0) {
    return <EmptyRow label="No trend rows found" />;
  }

  return (
    <div className="grid gap-2">
      {rows.map((row) => (
        <div className="rounded-lg border border-border bg-white p-3" key={row.userId}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">#{row.rank} {row.username}</div>
              <div className="text-xs text-muted">{formatCompactNumber(row.previousMessages)} to {formatCompactNumber(row.recentMessages)} messages</div>
            </div>
            <Badge variant={negative ? "danger" : "success"}>
              {row.delta > 0 ? "+" : ""}{formatCompactNumber(row.delta)}
            </Badge>
          </div>
          <div className="mt-2 text-xs text-muted">{formatPercent(row.deltaPercent)}</div>
        </div>
      ))}
    </div>
  );
}

function TimeBucketList({ rows }: { rows: DashboardTimeBucket[] }) {
  if (rows.length === 0) {
    return <EmptyRow label="No activity found" />;
  }

  return (
    <div className="grid gap-2">
      {rows.map((row) => (
        <div className="rounded-lg border border-border bg-white px-3 py-2" key={`${row.label}-${row.sort}`}>
          <div className="flex items-center justify-between gap-3">
            <span className="truncate text-sm font-semibold text-foreground">{row.label}</span>
            <span className="text-sm font-semibold text-foreground">{formatCompactNumber(row.messages)}</span>
          </div>
          <div className="mt-1 flex items-center justify-between gap-3 text-xs text-muted">
            <span>{formatCompactNumber(row.xp)} XP</span>
            <span>{formatInteger(row.activeUsers)} active users</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function UserStreakPanel({ streaks }: { streaks: DashboardUserProfileInsights["activityStreaks"] }) {
  return (
    <div className="grid gap-3 sm:grid-cols-4">
      <MetricPill label="Current streak" value={`${formatInteger(streaks.currentStreakDays)} days`} />
      <MetricPill label="Longest streak" value={`${formatInteger(streaks.longestStreakDays)} days`} />
      <MetricPill label="Active days" value={formatInteger(streaks.activeDays)} />
      <MetricPill label="Quiet days" value={formatInteger(streaks.quietDays)} />
    </div>
  );
}

function UserRankSnapshotList({ ranks }: { ranks: DashboardUserRankSnapshot[] }) {
  if (ranks.length === 0) {
    return <EmptyRow label="No rank data found" />;
  }

  return (
    <div className="grid gap-2">
      {ranks.map((rank) => (
        <div className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-slate-50 px-3 py-2" key={`${rank.scope}-${rank.guildId ?? "global"}`}>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{rank.scope}</div>
            <div className="text-xs text-muted">{formatCompactNumber(rank.xp)} XP - {formatCompactNumber(rank.messages)} messages</div>
          </div>
          <Badge variant={rank.rank ? "default" : "muted"}>{rankLabel(rank)}</Badge>
        </div>
      ))}
    </div>
  );
}

function UserContributionList({
  compact = false,
  contributions,
}: {
  compact?: boolean;
  contributions: DashboardUserContribution[];
}) {
  if (contributions.length === 0) {
    return <EmptyRow label="No contribution data found" />;
  }

  return (
    <div className="grid gap-2">
      {contributions.slice(0, compact ? 6 : 8).map((contribution) => (
        <div className="rounded-lg border border-border bg-white p-3" key={contribution.id}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{contribution.label}</div>
              <div className="text-xs text-muted">{formatCompactNumber(contribution.xp)} XP</div>
            </div>
            <div className="text-right">
              <div className="text-sm font-semibold text-foreground">{formatCompactNumber(contribution.messages)}</div>
              <div className="text-xs text-muted">{contribution.percent.toFixed(1)}%</div>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

function UserServerLevelsTable({ levels }: { levels: DashboardUserServerLevel[] }) {
  if (levels.length === 0) {
    return <EmptyRow label="No server levels found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[760px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">Server</th>
            <th className="border-b border-border pb-2 text-right">Rank</th>
            <th className="border-b border-border pb-2 text-right">Level</th>
            <th className="border-b border-border pb-2 text-right">XP</th>
            <th className="border-b border-border pb-2 text-right">Messages</th>
            <th className="border-b border-border pb-2 text-right">Avg length</th>
            <th className="border-b border-border pb-2 text-right">Moving avg</th>
          </tr>
        </thead>
        <tbody>
          {levels.map((level) => (
            <tr key={level.guildId}>
              <td className="border-b border-border py-3 pr-3">
                <div className="font-semibold text-foreground">{level.name}</div>
                <div className="text-xs text-muted">{formatRelativeDate(level.lastActivityAtUtc)}</div>
              </td>
              <td className="border-b border-border py-3 text-right">#{formatInteger(level.rank)} / {formatInteger(level.rankPopulation)}</td>
              <td className="border-b border-border py-3 text-right">{formatInteger(level.level)}</td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(level.totalXp)}</td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(level.messages)}</td>
              <td className="border-b border-border py-3 text-right">{level.averageMessageLength.toFixed(1)}</td>
              <td className="border-b border-border py-3 text-right">{level.messageLengthMovingAverage.toFixed(1)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function UserStockHoldingsTable({ holdings }: { holdings: DashboardUserStockHolding[] }) {
  if (holdings.length === 0) {
    return <EmptyRow label="No stock holdings found" />;
  }

  return (
    <div className="grid gap-2">
      {holdings.slice(0, 8).map((holding) => (
        <div className="rounded-lg border border-border bg-white p-3" key={holding.stockId}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{holding.name}</div>
              <div className="text-xs text-muted">{holding.entityType} - {formatCompactNumber(holding.shares)} shares</div>
            </div>
            <Badge variant={holding.unrealizedGain >= 0 ? "success" : "danger"}>
              {holding.unrealizedGain >= 0 ? "+" : ""}{formatCurrency(holding.unrealizedGain)}
            </Badge>
          </div>
          <div className="mt-3 flex items-center justify-between gap-3 text-sm">
            <span className="text-muted">{formatCurrency(holding.price)} price</span>
            <span className="font-semibold text-foreground">{formatCurrency(holding.value)}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function UserOutcomeCard({
  outcome,
  title,
}: {
  outcome: DashboardUserOutcomeStats;
  title: string;
}) {
  return (
    <div className="rounded-lg border border-border bg-slate-50 p-3">
      <div className="text-sm font-semibold text-foreground">{title}</div>
      <div className="mt-2 text-2xl font-semibold text-foreground">{formatCurrency(outcome.net)}</div>
      <div className="mt-1 text-xs text-muted">
        {formatInteger(outcome.wins)} wins - {formatInteger(outcome.losses)} losses
      </div>
      <div className="mt-3 flex items-center justify-between gap-3 text-xs text-muted">
        <span>{formatCurrency(outcome.won)} won</span>
        <span>{formatCurrency(outcome.lost)} lost</span>
      </div>
    </div>
  );
}

function UserTransactionsList({
  title,
  transactions,
}: {
  title: string;
  transactions: DashboardUserTransactionItem[];
}) {
  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">{title}</div>
      {transactions.length === 0 ? (
        <EmptyRow label="No transactions found" />
      ) : (
        transactions.map((transaction) => (
          <div className="rounded-lg border border-border bg-white p-3" key={transaction.id}>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="truncate text-sm font-semibold text-foreground">{transaction.type}</div>
                <div className="text-xs text-muted">
                  {transaction.stockName ?? transaction.counterpartyUsername ?? transaction.direction}
                </div>
              </div>
              <Badge variant={transaction.direction === "Incoming" ? "success" : transaction.direction === "Outgoing" ? "danger" : "muted"}>
                {transaction.direction}
              </Badge>
            </div>
            <div className="mt-3 flex items-center justify-between gap-3 text-sm">
              <span className="text-muted">{formatRelativeDate(transaction.insertedAtUtc)}</span>
              <span className="font-semibold text-foreground">{formatCurrency(transaction.amount)}</span>
            </div>
          </div>
        ))
      )}
    </div>
  );
}

function UserReminderTimelineList({ reminders }: { reminders: DashboardUserReminderTimelineItem[] }) {
  if (reminders.length === 0) {
    return <EmptyRow label="No reminders found" />;
  }

  return (
    <div className="grid gap-2">
      {reminders.slice(0, 8).map((reminder) => (
        <div className="rounded-lg border border-border bg-white p-3" key={reminder.id}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="line-clamp-2 text-sm font-semibold text-foreground">{reminder.text}</div>
              <div className="mt-1 text-xs text-muted">{reminder.guildName} - {reminder.channelId}</div>
            </div>
            <Badge variant={reminder.overdue ? "danger" : "muted"}>{reminder.overdue ? "Overdue" : "Queued"}</Badge>
          </div>
          <div className="mt-3 text-xs text-muted">Due {formatRelativeDate(reminder.dueDateUtc)}</div>
        </div>
      ))}
    </div>
  );
}

function rankLabel(rank: DashboardUserRankSnapshot) {
  return rank.rank ? `#${formatInteger(rank.rank)} / ${formatInteger(rank.population)}` : "Unranked";
}

function channelStatus(channel: { configured: boolean; name: string }) {
  return channel.configured ? channel.name : "Not configured";
}

function dateWindowSummary(days: number) {
  return days === 1 ? "selected day" : `${days} selected days`;
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

function DashboardStatusBanner({
  detail,
  title,
}: {
  detail: string;
  title: string;
}) {
  return (
    <section className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
      <div className="flex items-start gap-3">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
        <div className="min-w-0">
          <div className="font-semibold">{title}</div>
          <div className="mt-1 break-words">{detail}</div>
        </div>
      </div>
    </section>
  );
}

function DashboardMetricGroup({
  children,
  columnsClassName = "md:grid-cols-2 xl:grid-cols-4",
  description,
  id,
  title,
}: {
  children: React.ReactNode;
  columnsClassName?: string;
  description: string;
  id: string;
  title: string;
}) {
  return (
    <section aria-labelledby={id} className="grid gap-3">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-sm font-semibold uppercase tracking-normal text-foreground" id={id}>{title}</h2>
          <p className="mt-1 max-w-3xl text-sm text-muted">{description}</p>
        </div>
      </div>
      <div className={cn("grid grid-cols-1 gap-4", columnsClassName)}>
        {children}
      </div>
    </section>
  );
}

type DashboardViewTab = {
  view: DashboardView;
  label: string;
  icon: React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;
};

const SCOPED_VIEW_TABS: DashboardViewTab[] = [
  { view: "summary", label: "Summary", icon: Gauge },
  { view: "activity", label: "Activity", icon: Activity },
  { view: "users", label: "Users", icon: Users },
  { view: "quotes", label: "Quotes", icon: Quote },
  { view: "economy", label: "Economy", icon: Wallet },
  { view: "stocks", label: "Stocks", icon: TrendingUp },
  { view: "operations", label: "Ops", icon: ShieldCheck },
  { view: "settings", label: "Settings", icon: Settings },
];

const GLOBAL_VIEW_TABS: DashboardViewTab[] = [
  { view: "summary", label: "Summary", icon: Gauge },
  { view: "activity", label: "Activity", icon: Activity },
  { view: "servers", label: "Servers", icon: Server },
  { view: "users", label: "Users", icon: Users },
  { view: "economy", label: "Economy", icon: Wallet },
  { view: "quotes", label: "Quotes", icon: Quote },
  { view: "stocks", label: "Stocks", icon: TrendingUp },
  { view: "operations", label: "Ops", icon: ShieldCheck },
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
    <nav aria-label="Dashboard pages" className="sticky top-3 z-20 -mx-1 overflow-x-auto rounded-lg border border-border bg-card p-2 shadow-sm scrollbar-clean">
      <div className="flex min-w-max items-center gap-2">
        {tabs.map((tab) => {
          const active = tab.view === activeView;
          const Icon = tab.icon;
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
                "inline-flex h-9 items-center gap-2 rounded-md px-3 text-sm font-medium transition-colors",
                active
                  ? "bg-primary text-primary-foreground"
                  : "border border-border bg-white text-muted hover:border-primary hover:text-foreground",
              )}
              href={href}
              key={tab.view}
            >
              <Icon className="h-4 w-4 shrink-0" aria-hidden />
              {tab.label}
            </DashboardNavLink>
          );
        })}
      </div>
    </nav>
  );
}

const DASHBOARD_VIEW_META: Record<DashboardView, {
  title: string;
  description: string;
  icon: React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;
}> = {
  summary: {
    title: "Command Summary",
    description: "Current bot footprint, top signals, and newest records.",
    icon: Gauge,
  },
  activity: {
    title: "Activity and XP",
    description: "Message volume, XP gain, rolling trends, active periods, and contribution shape.",
    icon: Activity,
  },
  servers: {
    title: "Server Performance",
    description: "Server activity leaders, growth, message share, and setup context.",
    icon: Server,
  },
  users: {
    title: "User Analytics",
    description: "User activity, XP leaders, message behavior, wealth, and contribution history.",
    icon: Users,
  },
  quotes: {
    title: "Quotes and Approvals",
    description: "Quote culture, approval queues, author performance, voting, and moderation state.",
    icon: Quote,
  },
  economy: {
    title: "Economy",
    description: "Money supply, wallet distribution, transactions, UBI, slots, and robbery outcomes.",
    icon: Wallet,
  },
  stocks: {
    title: "Stock Market",
    description: "Market movers, holdings, portfolio value, trade volume, and ownership concentration.",
    icon: TrendingUp,
  },
  operations: {
    title: "Operations",
    description: "Reminders, moderation, button game, logs, configuration health, and bot status.",
    icon: ShieldCheck,
  },
  settings: {
    title: "Configuration",
    description: "Server setup, safety gaps, channel configuration, and automation settings.",
    icon: Settings,
  },
};

function DashboardViewContext({
  activeView,
  dataMode,
  dateWindowLabel,
  minActivity,
  scope,
  scopeLabel,
}: {
  activeView: DashboardView;
  dataMode: string;
  dateWindowLabel: string;
  minActivity: number;
  scope: DashboardScope;
  scopeLabel: string;
}) {
  const meta = DASHBOARD_VIEW_META[activeView];
  const Icon = meta.icon;

  return (
    <section className="rounded-lg border border-border bg-card p-4 shadow-sm">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex min-w-0 items-start gap-3">
          <div className="grid h-10 w-10 shrink-0 place-items-center rounded-lg bg-blue-50 text-blue-700">
            <Icon className="h-5 w-5" aria-hidden />
          </div>
          <div className="min-w-0">
            <h2 className="text-lg font-semibold leading-tight text-foreground">{meta.title}</h2>
            <p className="mt-1 max-w-4xl text-sm text-muted">{meta.description}</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Badge variant="muted">{scopeLabel}</Badge>
          <Badge variant="muted">{dateWindowLabel}</Badge>
          <Badge variant={dataMode === "Live API" ? "success" : "warning"}>{dataMode}</Badge>
          {scope !== "global" && <Badge variant="muted">Min {formatInteger(minActivity)} messages</Badge>}
        </div>
      </div>
    </section>
  );
}

function DashboardAnswerStrip({
  activeView,
  channelId,
  days,
  endDate,
  guildId,
  global,
  insights,
  scope,
  startDate,
  userId,
}: {
  activeView: DashboardView;
  channelId?: string;
  days: number;
  endDate: string;
  guildId?: number;
  global: DashboardGlobalOverviewResponse;
  insights?: DashboardInsightsResponse;
  scope: DashboardScope;
  startDate: string;
  userId?: number;
}) {
  const activeUser = global.highlights.mostActiveUsers[0] ?? global.highlights.biggestXpGainers[0];
  const activeServer = global.highlights.mostActiveServersSelectedWindow[0] ?? global.highlights.mostActiveServersThisWeek[0];
  const activeChannel = global.highlights.mostActiveChannels[0];
  const stockMover = global.highlights.biggestStockGainers[0] ?? global.highlights.biggestStockLosers[0];
  const severeLogs = insights?.operations.logs.errors ?? global.totals.recentWarningsOrErrors;
  const pendingWork = (insights?.operations.reminders.overdue ?? 0) +
    (insights?.operations.moderation.overdueTemporaryBans ?? 0) +
    global.totals.pendingQuoteApprovals;
  const cards = [
    {
      question: "Who is active?",
      answer: activeUser ? activeUser.username : "No active users yet",
      detail: activeUser
        ? `${formatCompactNumber(activeUser.messages)} messages, ${formatCompactNumber(activeUser.xp)} XP`
        : "No messages match this window.",
      href: activeUser
        ? dashboardHref({ scope: "user", userId: activeUser.userId, days, startDate, endDate, view: "activity" })
        : dashboardHref({ scope: "global", days, startDate, endDate, view: "users" }),
      tone: "cyan" as const,
    },
    {
      question: "Where is momentum?",
      answer: activeServer?.name ?? activeChannel?.name ?? "No activity concentration",
      detail: activeServer
        ? `${formatCompactNumber(activeServer.messages)} server messages`
        : activeChannel
          ? `${formatCompactNumber(activeChannel.messages)} channel messages`
          : "Server and channel activity are quiet.",
      href: activeServer
        ? dashboardHref({ scope: "server", guildId: activeServer.guildId, days, startDate, endDate, view: "summary" })
        : activeChannel
          ? dashboardHref({ scope: "channel", channelId: activeChannel.discordId, days, startDate, endDate, view: "activity" })
          : dashboardHref({ scope: "global", days, startDate, endDate, view: "activity" }),
      tone: "green" as const,
    },
    {
      question: "Is the economy healthy?",
      answer: formatCurrency(global.totals.totalEstimatedNetWorth),
      detail: `${formatCurrency(global.totals.totalEconomyBalance)} cash, ${formatCurrency(global.totals.ubiPoolSize)} UBI pool`,
      href: dashboardHref({ scope, guildId, userId, channelId, days, startDate, endDate, view: "economy" }),
      tone: "amber" as const,
    },
    {
      question: "What needs attention?",
      answer: pendingWork > 0 || severeLogs > 0 ? `${formatInteger(pendingWork + severeLogs)} signals` : "No urgent signals",
      detail: stockMover
        ? `${stockMover.name} ${stockMover.dailyChangePercent >= 0 ? "up" : "down"} ${Math.abs(stockMover.dailyChangePercent).toFixed(2)}%`
        : `${formatInteger(global.totals.activeReminders)} reminders, ${formatInteger(global.totals.recentWarningsOrErrors)} recent warnings/errors`,
      href: dashboardHref({ scope, guildId, userId, channelId, days, startDate, endDate, view: pendingWork > 0 || severeLogs > 0 ? "operations" : "stocks" }),
      tone: pendingWork > 0 || severeLogs > 0 ? "rose" as const : "slate" as const,
    },
  ];

  return (
    <section aria-label="Dashboard questions" className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
      {cards.map((card) => (
        <Link
          className={cn(
            "group rounded-lg border bg-white p-4 shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary hover:shadow-md focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary",
            activeView === "summary" ? "border-border" : "border-border/80",
          )}
          href={card.href}
          key={card.question}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="text-xs font-semibold uppercase tracking-normal text-muted">{card.question}</div>
              <div className="mt-2 truncate text-base font-semibold text-foreground">{card.answer}</div>
              <div className="mt-1 line-clamp-2 text-sm text-muted">{card.detail}</div>
            </div>
            <span
              className={cn(
                "grid h-8 w-8 shrink-0 place-items-center rounded-lg transition-colors",
                card.tone === "cyan" && "bg-cyan-50 text-cyan-700",
                card.tone === "green" && "bg-emerald-50 text-emerald-700",
                card.tone === "amber" && "bg-amber-50 text-amber-700",
                card.tone === "rose" && "bg-rose-50 text-rose-700",
                card.tone === "slate" && "bg-slate-50 text-muted",
              )}
            >
              <ArrowUpRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5" aria-hidden />
            </span>
          </div>
        </Link>
      ))}
    </section>
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
          <div className="mt-2 break-words text-xl font-semibold leading-tight text-foreground sm:text-2xl">{value}</div>
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
          <div
            className="mt-2 h-2.5 overflow-hidden rounded-full border border-border"
            style={{ backgroundColor: "color-mix(in srgb, var(--border) 44%, var(--card))" }}
          >
            <div
              className="h-full rounded-full"
              style={{
                background: "linear-gradient(90deg, var(--primary), var(--accent))",
                width: `${Math.max(4, (server.messages / max) * 100)}%`,
              }}
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
          href={quoteDetailHref(quote.id, days, startDate, endDate)}
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
            href={quoteDetailHref(quote.id, days, startDate, endDate)}
            key={quote.id}
          >
            <div className="line-clamp-2 text-sm font-medium text-foreground">{quote.content}</div>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted">
              <span>{quote.author}</span>
              <Badge variant={quote.removed ? "danger" : quote.approved ? "success" : "warning"}>
                {quote.removed ? "Removed" : quote.approved ? "Approved" : "Pending"}
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
  days,
  endDate,
  title,
  metric,
  items,
  startDate,
}: {
  days: number;
  endDate: string;
  title: string;
  metric: string;
  items: DashboardLeaderboardItem[];
  startDate: string;
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
              <Link
                className="grid grid-cols-[3rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2 transition-colors hover:border-primary hover:bg-slate-50"
                href={dashboardHref({ scope: "user", userId: item.userId, days, startDate, endDate, view: "summary" })}
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
              </Link>
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

function QuoteAnalyticsSection({
  days,
  endDate,
  guildId,
  insights,
  scope,
  startDate,
  userId,
}: {
  days: number;
  endDate: string;
  guildId?: number;
  insights: DashboardQuoteInsights;
  scope: DashboardScope;
  startDate: string;
  userId?: number;
}) {
  const scoreHistogram = insights.scoreHistogram.map((item) => ({ label: item.label, value: item.count }));
  const approvalTimeHistogram = insights.approvalTimeHistogram.map((item) => ({ label: item.label, value: item.count }));

  return (
    <>
      <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricPill label="Total quotes" value={formatInteger(insights.total)} />
        <MetricPill label="Approved / pending" value={`${formatInteger(insights.approved)} / ${formatInteger(insights.pending)}`} />
        <MetricPill label="Approval completion" value={`${insights.approvalCompletionRate.toFixed(1)}%`} />
        <MetricPill label="Average score" value={insights.averageScore.toFixed(1)} />
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.85fr_1.15fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Culture</CardTitle>
              <CardDescription>Status mix, author power, and score distribution.</CardDescription>
            </div>
            <Badge variant={insights.pending > 0 ? "warning" : "success"}>{formatInteger(insights.pending)} pending</Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <DonutChart data={insights.statuses} labelKey="status" />
            <CategoryBars data={scoreHistogram} />
            <QuoteAuthors authors={insights.authors} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Creation Timeline</CardTitle>
              <CardDescription>Created quotes, status mix, and approval voting over time.</CardDescription>
            </div>
            <Quote className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <QuoteCreationTimelineChart points={insights.creationTimeline} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Approval Funnel</CardTitle>
              <CardDescription>Created, approved, pending, removed, completed, and expired requests.</CardDescription>
            </div>
            <Badge variant={insights.expiredApprovalRequests > 0 ? "danger" : "success"}>
              {formatInteger(insights.expiredApprovalRequests)} expired
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <CategoryBars data={insights.approvalFunnel} />
            <CategoryBars data={approvalTimeHistogram} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Score Trend</CardTitle>
              <CardDescription>Cumulative quote score and voting activity where score history exists.</CardDescription>
            </div>
            <Badge variant="muted">{formatInteger(insights.topVoters.reduce((sum, voter) => sum + voter.votes, 0))} votes</Badge>
          </CardHeader>
          <CardContent>
            <QuoteScoreTrendChart points={insights.scoreTrend} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Approval Activity</CardTitle>
              <CardDescription>Calendar heatmap of approval votes.</CardDescription>
            </div>
            <ShieldCheck className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <CalendarActivityHeatmap cells={insights.approvalActivityCalendar} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Server Quote Comparison</CardTitle>
              <CardDescription>Quote volume, moderation load, and score by server.</CardDescription>
            </div>
            <Server className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <QuoteServerComparisonChart servers={insights.serverSummaries} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Global Quote Usage</CardTitle>
              <CardDescription>Servers and quotes split by global quote configuration.</CardDescription>
            </div>
            <Badge variant="muted">{scope === "global" ? "Global" : scope}</Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <CategoryBars data={insights.globalVsServerUsage} />
            <QuoteSetupList setups={insights.setupSummaries} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Top Quote Authors</CardTitle>
              <CardDescription>Contributor volume compared with received quote score.</CardDescription>
            </div>
            <Users className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <QuoteAuthorBarChart authors={insights.authors} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Management Views</CardTitle>
              <CardDescription>Filtered lists for quote review, expired approvals, and removed quotes.</CardDescription>
            </div>
            <Badge variant="muted">{formatInteger(insights.quoteList.length)} visible</Badge>
          </CardHeader>
          <CardContent>
            <QuoteManagementBoard days={days} endDate={endDate} insights={insights} startDate={startDate} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Highest-Scoring Quotes</CardTitle>
              <CardDescription>Approved quotes with the strongest score.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <QuoteRankedList quotes={insights.highestScoringQuotes} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Lowest-Scoring Quotes</CardTitle>
              <CardDescription>Approved quotes that landed poorly.</CardDescription>
            </div>
            <AlertTriangle className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <QuoteRankedList quotes={insights.lowestScoringQuotes} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Controversial Quotes</CardTitle>
              <CardDescription>Quotes with the most mixed positive and negative voting.</CardDescription>
            </div>
            <Gauge className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <QuoteRankedList quotes={insights.mostControversialQuotes} days={days} endDate={endDate} startDate={startDate} showControversy />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Quote Period Candidates</CardTitle>
              <CardDescription>Quote-of-the-day, week, and month candidates.</CardDescription>
            </div>
            <Clock3 className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <QuoteCandidateGrid
              days={days}
              endDate={endDate}
              groups={[
                { title: "Day", items: insights.quoteOfTheDayCandidates },
                { title: "Week", items: insights.quoteOfTheWeekCandidates },
                { title: "Month", items: insights.quoteOfTheMonthCandidates },
              ]}
              startDate={startDate}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Voter Activity</CardTitle>
              <CardDescription>Quote voters and approval voters.</CardDescription>
            </div>
            <Users className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <QuoteVoteList title="Quote votes" voters={insights.topVoters} />
            <QuoteVoteList title="Approval votes" voters={insights.approvalVoters} />
          </CardContent>
        </Card>
      </section>

      {insights.mostRemovedQuotes.length > 0 && (
        <section className="grid grid-cols-1 gap-4">
          <Card>
            <CardHeader>
              <div>
                <CardTitle>Removed Quote List</CardTitle>
                <CardDescription>Removed quotes with score and voter context.</CardDescription>
              </div>
              <Badge variant="danger">{formatInteger(insights.removed)} removed</Badge>
            </CardHeader>
            <CardContent>
              <QuoteManagementList days={days} emptyLabel="No removed quotes found" endDate={endDate} quotes={insights.removedQuoteList} startDate={startDate} />
            </CardContent>
          </Card>
        </section>
      )}
    </>
  );
}

function QuoteManagementBoard({
  days,
  endDate,
  insights,
  startDate,
}: {
  days: number;
  endDate: string;
  insights: DashboardQuoteInsights;
  startDate: string;
}) {
  return (
    <div className="grid gap-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <MetricPill label="All listed" value={formatInteger(insights.quoteList.length)} />
        <MetricPill label="Pending queue" value={formatInteger(insights.pendingApprovalQueue.length)} />
        <MetricPill label="Expired queue" value={formatInteger(insights.expiredApprovalQueue.length)} />
        <MetricPill label="Removed listed" value={formatInteger(insights.removedQuoteList.length)} />
      </div>
      <QuoteManagementList days={days} endDate={endDate} quotes={insights.quoteList} startDate={startDate} />
      <div className="grid gap-4 lg:grid-cols-2">
        <QuoteApprovalQueue days={days} emptyLabel="No pending approval requests" endDate={endDate} requests={insights.pendingApprovalQueue} startDate={startDate} title="Pending Approval Queue" />
        <QuoteApprovalQueue days={days} emptyLabel="No expired approval requests" endDate={endDate} requests={insights.expiredApprovalQueue} startDate={startDate} title="Expired Approval Queue" />
      </div>
    </div>
  );
}

function QuoteManagementList({
  days,
  emptyLabel = "No quotes found",
  endDate,
  quotes,
  startDate,
}: {
  days: number;
  emptyLabel?: string;
  endDate: string;
  quotes: DashboardQuoteManagementItem[];
  startDate: string;
}) {
  if (quotes.length === 0) {
    return <EmptyRow label={emptyLabel} />;
  }

  return (
    <div className="grid gap-3">
      {quotes.slice(0, 8).map((quote) => (
        <article className="rounded-lg border border-border bg-white p-3" key={quote.id}>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0">
              <Link className="font-semibold text-foreground hover:text-primary" href={quoteDetailHref(quote.id, days, startDate, endDate)}>
                #{quote.id}
              </Link>
              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted">
                <Link className="hover:text-primary" href={dashboardHref({ scope: "server", guildId: quote.guildId, days, startDate, endDate, view: "quotes" })}>
                  {quote.guildName}
                </Link>
                <span>-</span>
                <Link className="hover:text-primary" href={dashboardHref({ scope: "user", userId: quote.userId, days, startDate, endDate, view: "quotes" })}>
                  {quote.author}
                </Link>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={quote.removed ? "danger" : quote.approved ? "success" : "warning"}>
                {quote.removed ? "Removed" : quote.approved ? "Approved" : "Pending"}
              </Badge>
              {quote.pendingApprovals > 0 && <Badge variant="warning">{formatInteger(quote.pendingApprovals)} requests</Badge>}
              <Badge variant={quote.score >= 0 ? "success" : "danger"}>{quote.score >= 0 ? "+" : ""}{formatInteger(quote.score)}</Badge>
            </div>
          </div>
          <p className="mt-2 line-clamp-3 text-sm leading-6 text-foreground">{quote.content}</p>
          <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-muted">
            <span>{formatInteger(quote.scoreVotes)} votes</span>
            <span>-</span>
            <span>created {formatRelativeDate(quote.insertedAtUtc)}</span>
            <span>-</span>
            <span>last vote {formatRelativeDate(quote.lastVoteAtUtc)}</span>
          </div>
        </article>
      ))}
    </div>
  );
}

function QuoteApprovalQueue({
  days,
  emptyLabel,
  endDate,
  requests,
  startDate,
  title,
}: {
  days: number;
  emptyLabel: string;
  endDate: string;
  requests: DashboardQuoteApprovalRequestItem[];
  startDate: string;
  title: string;
}) {
  if (requests.length === 0) {
    return <EmptyRow label={emptyLabel} />;
  }

  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">{title}</div>
      {requests.slice(0, 6).map((request) => (
        <article className="rounded-lg border border-border bg-white p-3" key={request.id}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <Link className="font-semibold text-foreground hover:text-primary" href={approvalDetailHref(request.id, days, startDate, endDate)}>
                Request #{request.id}
              </Link>
              <div className="mt-1 text-xs text-muted">{request.type} request - {request.guildName}</div>
            </div>
            <Badge variant={request.expired ? "danger" : request.status === "Approved" ? "success" : "warning"}>{request.status}</Badge>
          </div>
          <p className="mt-2 line-clamp-2 text-sm leading-6 text-foreground">{request.quoteContent}</p>
          <div className="mt-3 grid gap-2 text-xs text-muted">
            <div className="flex items-center justify-between gap-3">
              <span>{formatInteger(request.currentApprovals)} / {formatInteger(request.requiredApprovals)} approvals</span>
              <span>{request.completionPercent.toFixed(0)}%</span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-slate-100">
              <div className="h-full rounded-full bg-primary" style={{ width: `${Math.max(3, request.completionPercent)}%` }} />
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <span>opened {formatRelativeDate(request.insertedAtUtc)}</span>
              <span>-</span>
              <span>{request.expired ? "expired" : "expires"} {formatRelativeDate(request.expiresAtUtc)}</span>
              <span>-</span>
              <Link className="hover:text-primary" href={quoteDetailHref(request.quoteId, days, startDate, endDate)}>quote detail</Link>
              <span>-</span>
              <Link className="hover:text-primary" href={dashboardHref({ scope: "server", guildId: request.guildId, days, startDate, endDate, view: "quotes" })}>server</Link>
            </div>
          </div>
        </article>
      ))}
    </div>
  );
}

function QuoteRankedList({
  days,
  endDate,
  quotes,
  showControversy = false,
  startDate,
}: {
  days: number;
  endDate: string;
  quotes: DashboardQuoteRankedItem[];
  showControversy?: boolean;
  startDate: string;
}) {
  if (quotes.length === 0) {
    return <EmptyRow label="No ranked quotes found" />;
  }

  return (
    <div className="grid gap-3">
      {quotes.slice(0, 6).map((quote) => (
        <article className="rounded-lg border border-border bg-white p-3" key={quote.id}>
          <div className="flex items-start justify-between gap-3">
            <Link className="text-sm font-semibold text-foreground hover:text-primary" href={quoteDetailHref(quote.id, days, startDate, endDate)}>
              #{quote.rank} Quote {quote.id}
            </Link>
            <Badge variant={quote.score >= 0 ? "success" : "danger"}>{quote.score >= 0 ? "+" : ""}{formatInteger(quote.score)}</Badge>
          </div>
          <p className="mt-2 line-clamp-3 text-sm leading-6 text-foreground">{quote.content}</p>
          <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-muted">
            <Link className="hover:text-primary" href={dashboardHref({ scope: "user", userId: quote.userId, days, startDate, endDate, view: "quotes" })}>{quote.author}</Link>
            <span>-</span>
            <Link className="hover:text-primary" href={dashboardHref({ scope: "server", guildId: quote.guildId, days, startDate, endDate, view: "quotes" })}>{quote.guildName}</Link>
            <span>-</span>
            <span>{formatInteger(quote.totalVotes)} votes</span>
            {showControversy && (
              <>
                <span>-</span>
                <span>{formatInteger(quote.controversyScore)} controversy</span>
              </>
            )}
          </div>
        </article>
      ))}
    </div>
  );
}

function QuoteCandidateGrid({
  days,
  endDate,
  groups,
  startDate,
}: {
  days: number;
  endDate: string;
  groups: Array<{ title: string; items: DashboardQuoteCandidate[] }>;
  startDate: string;
}) {
  return (
    <div className="grid gap-3 md:grid-cols-3">
      {groups.map((group) => (
        <div className="grid gap-2 rounded-lg border border-border bg-slate-50 p-3" key={group.title}>
          <div className="font-semibold text-foreground">{group.title}</div>
          {group.items.length === 0 ? (
            <div className="text-sm text-muted">No candidates</div>
          ) : (
            group.items.slice(0, 3).map((quote) => (
              <Link className="rounded-md bg-white p-2 text-sm hover:text-primary" href={quoteDetailHref(quote.id, days, startDate, endDate)} key={`${group.title}-${quote.id}`}>
                <span className="font-semibold">#{quote.rank}</span> {quote.content}
                <span className="mt-1 block text-xs text-muted">
                  {quote.author} - {quote.score >= 0 ? "+" : ""}{formatInteger(quote.score)} - {formatInteger(quote.votes)} votes
                </span>
              </Link>
            ))
          )}
          {group.items[0] && (
            <Link className="text-xs font-medium text-primary" href={dashboardHref({ scope: "server", guildId: group.items[0].guildId, days, startDate, endDate, view: "quotes" })}>
              {group.items[0].guildName}
            </Link>
          )}
        </div>
      ))}
    </div>
  );
}

function QuoteVoteList({ title, voters }: { title: string; voters: DashboardQuoteVoteItem[] }) {
  if (voters.length === 0) {
    return <EmptyRow label={`No ${title.toLowerCase()} found`} />;
  }

  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">{title}</div>
      {voters.slice(0, 8).map((voter) => (
        <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={`${title}-${voter.userId}`}>
          <span className="text-xs font-semibold text-muted">#{voter.rank}</span>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{voter.username}</div>
            <div className="text-xs text-muted">{formatInteger(voter.positiveVotes)} up / {formatInteger(voter.negativeVotes)} down</div>
          </div>
          <div className="text-right">
            <div className="text-sm font-semibold text-foreground">{formatInteger(voter.votes)}</div>
            <div className="text-xs text-muted">{formatRelativeDate(voter.lastVotedAtUtc)}</div>
          </div>
        </div>
      ))}
    </div>
  );
}

function QuoteSetupList({
  days,
  endDate,
  setups,
  startDate,
}: {
  days: number;
  endDate: string;
  setups: DashboardQuoteSetupSummary[];
  startDate: string;
}) {
  if (setups.length === 0) {
    return <EmptyRow label="No quote setup data found" />;
  }

  return (
    <div className="grid gap-2">
      {setups.slice(0, 6).map((setup) => (
        <Link
          className="grid gap-2 rounded-lg border border-border bg-white p-3 hover:border-primary"
          href={dashboardHref({ scope: "server", guildId: setup.guildId, days, startDate, endDate, view: "settings" })}
          key={setup.guildId}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{setup.name}</div>
              <div className="text-xs text-muted">{setup.issue}</div>
            </div>
            <Badge variant={setup.health === "Healthy" ? "success" : setup.health === "Weak" ? "warning" : "danger"}>{setup.health}</Badge>
          </div>
          <div className="flex flex-wrap gap-2 text-xs text-muted">
            <span>{setup.usesGlobalQuotes ? "Global quotes" : "Server quotes"}</span>
            <span>-</span>
            <span>{setup.approvalChannelConfigured ? "approval channel set" : "missing channel"}</span>
            <span>-</span>
            <span>+{formatInteger(setup.addRequiredApprovals)} / -{formatInteger(setup.removeRequiredApprovals)}</span>
          </div>
        </Link>
      ))}
    </div>
  );
}

function QuoteAuthors({
  authors,
  days,
  endDate,
  startDate,
}: {
  authors: DashboardQuoteAuthorSummary[];
  days: number;
  endDate: string;
  startDate: string;
}) {
  if (authors.length === 0) {
    return <EmptyRow label="No quote authors found" />;
  }

  return (
    <div className="grid gap-2">
      {authors.map((author) => (
        <Link
          className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2 transition-colors hover:border-primary hover:bg-slate-50"
          href={dashboardHref({ scope: "user", userId: author.userId, days, startDate, endDate, view: "quotes" })}
          key={author.userId}
        >
          <span className="truncate text-sm font-semibold text-foreground">{author.username}</span>
          <span className="text-sm text-muted">{formatInteger(author.quotes)} quotes</span>
          <Badge variant={author.score >= 0 ? "success" : "danger"}>{author.score >= 0 ? "+" : ""}{author.score}</Badge>
        </Link>
      ))}
    </div>
  );
}

function QuotesList({
  days,
  emptyLabel = "No quotes found",
  endDate,
  quotes,
  startDate,
}: {
  days?: number;
  emptyLabel?: string;
  endDate?: string;
  quotes: DashboardQuoteItem[];
  startDate?: string;
}) {
  if (quotes.length === 0) {
    return <EmptyRow label={emptyLabel} />;
  }

  return (
    <div className="grid gap-3">
      {quotes.slice(0, 6).map((quote) => (
        <Link
          className="rounded-lg border border-border bg-white p-3 transition-colors hover:border-primary hover:bg-slate-50"
          href={days && startDate && endDate ? quoteDetailHref(quote.id, days, startDate, endDate) : `/quotes/${quote.id}`}
          key={quote.id}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="text-sm font-semibold text-foreground">#{quote.id}</div>
            <Badge variant={quote.removed ? "danger" : quote.approved ? "success" : "warning"}>
              {quote.removed ? "Removed" : quote.approved ? "Approved" : "Pending"}
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
        </Link>
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

function EconomyAnalyticsSection({
  days,
  endDate,
  insights,
  startDate,
}: {
  days: number;
  endDate: string;
  insights: DashboardEconomyInsights;
  startDate: string;
}) {
  return (
    <>
      <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricPill label="Money supply" value={formatCurrency(insights.totalMoneySupply)} />
        <MetricPill label="Average / median balance" value={`${formatCurrency(insights.averageBalance)} / ${formatCurrency(insights.medianBalance)}`} />
        <MetricPill label="Transaction volume" value={formatCurrency(insights.transactionVolume)} />
        <MetricPill label="Fees + taxes" value={`${formatCurrency(insights.fees)} / ${formatCurrency(insights.taxesCollected)}`} />
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Money Supply</CardTitle>
              <CardDescription>Estimated supply, cash, UBI pool, and slots vault over time.</CardDescription>
            </div>
            <Badge variant="muted">{formatCurrency(insights.ubiPoolSize)} UBI</Badge>
          </CardHeader>
          <CardContent>
            <MoneySupplyLineChart points={insights.moneySupplyTrend} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Wealth Shape</CardTitle>
              <CardDescription>Balance distribution and concentration estimates.</CardDescription>
            </div>
            <Wallet className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-5">
            <CategoryBars data={insights.balanceDistribution.map((item) => ({ label: item.label, value: item.count }))} />
            <CategoryBars data={insights.wealthInequality} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Transaction Volume</CardTitle>
              <CardDescription>Stacked volume across transfers, stocks, slots, robbery, fees, and taxes.</CardDescription>
            </div>
            <Badge variant="default">{formatInteger(insights.transactionCount)} tx</Badge>
          </CardHeader>
          <CardContent>
            <EconomyStackedVolumeChart points={insights.transactionVolumeTimeline} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Money Movement</CardTitle>
              <CardDescription>Sources and destinations for economy flow.</CardDescription>
            </div>
            <Wallet className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <MoneyFlowView flows={insights.moneyFlows} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>UBI Pool</CardTitle>
              <CardDescription>Pool balance, donations, and estimated wealth tax impact.</CardDescription>
            </div>
            <Badge variant="success">{formatCurrency(insights.ubiDonations)} donated</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <EconomyPoolChart label="UBI pool" points={insights.ubiPoolTrend} />
            <div className="grid gap-2 sm:grid-cols-2">
              <MetricPill label="Top donors tracked" value={formatInteger(insights.topDonors.length)} />
              <MetricPill label="Wealth tax impact" value={formatCurrency(insights.wealthTaxImpact)} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Robbery</CardTitle>
              <CardDescription>Robbery wins, losses, success rate, and notable outcomes.</CardDescription>
            </div>
            <Badge variant={insights.robberySuccessRate >= 40 ? "success" : "warning"}>{insights.robberySuccessRate.toFixed(1)}%</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <DonutChart data={insights.robberyOutcomes} labelKey="label" />
            <EconomyActorList actors={insights.biggestRobberies} days={days} emptyLabel="No robberies found" endDate={endDate} startDate={startDate} title="Biggest robberies" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Slots</CardTitle>
              <CardDescription>Vault trend, payout ratio, wins, and losses.</CardDescription>
            </div>
            <Badge variant="muted">{formatCurrency(insights.slotsVaultSize)} vault</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <EconomyPoolChart label="Slots vault" points={insights.slotsVaultTrend} />
            <div className="grid gap-2 sm:grid-cols-2">
              <MetricPill label="Payout ratio" value={insights.slotsPayoutRatio.toFixed(2)} />
              <MetricPill label="Wins / losses" value={`${formatInteger(insights.slotsWins)} / ${formatInteger(insights.slotsLosses)}`} />
            </div>
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
            <DonutChart data={insights.transactionTypes} labelKey="label" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Economy Heatmap</CardTitle>
              <CardDescription>Transaction volume by weekday and UTC hour.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <EconomyHeatmap cells={insights.economyHeatmap} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Richest by Cash</CardTitle>
              <CardDescription>Liquid wallet leaders.</CardDescription>
            </div>
            <Banknote className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <WealthTable users={insights.cashLeaders} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Richest by Net Worth</CardTitle>
              <CardDescription>Cash plus marked portfolio value.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <WealthTable users={insights.wealthLeaders} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Top Donors</CardTitle>
              <CardDescription>Users funding the UBI pool.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <EconomyActorList actors={insights.topDonors} days={days} emptyLabel="No donations found" endDate={endDate} startDate={startDate} title="Donors" />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Robbed Users</CardTitle>
              <CardDescription>Targets with the largest robbery losses.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <EconomyActorList actors={insights.mostRobbedUsers} days={days} emptyLabel="No robbed users found" endDate={endDate} startDate={startDate} title="Targets" />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Slots Extremes</CardTitle>
              <CardDescription>Biggest slots wins and losses.</CardDescription>
            </div>
          </CardHeader>
          <CardContent className="grid gap-4">
            <EconomyActorList actors={insights.biggestSlotsWins} days={days} emptyLabel="No slots wins found" endDate={endDate} startDate={startDate} title="Wins" />
            <EconomyActorList actors={insights.biggestSlotsLosses} days={days} emptyLabel="No slots losses found" endDate={endDate} startDate={startDate} title="Losses" />
          </CardContent>
        </Card>
      </section>
    </>
  );
}

function StockAnalyticsSection({
  days,
  endDate,
  insights,
  startDate,
}: {
  days: number;
  endDate: string;
  insights: DashboardStockMarketInsights;
  startDate: string;
}) {
  return (
    <>
      <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricPill label="Listed stocks" value={formatInteger(insights.stocks)} />
        <MetricPill label="Market value" value={formatCurrency(insights.marketValue)} />
        <MetricPill label="Average price" value={formatCurrency(insights.averagePrice)} />
        <MetricPill label="Buy / sell volume" value={`${formatCurrency(insights.buyVolume)} / ${formatCurrency(insights.sellVolume)}`} />
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.75fr_1.25fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Market Overview</CardTitle>
              <CardDescription>Stock mix by entity type and market movement.</CardDescription>
            </div>
            <Badge variant={insights.averageDailyChangePercent >= 0 ? "success" : "danger"}>
              {insights.averageDailyChangePercent >= 0 ? "+" : ""}{insights.averageDailyChangePercent.toFixed(2)}%
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-5">
            <div className="grid gap-2 sm:grid-cols-3">
              <MetricPill label="User" value={formatInteger(insights.userStocks)} />
              <MetricPill label="Server" value={formatInteger(insights.serverStocks)} />
              <MetricPill label="Channel" value={formatInteger(insights.channelStocks)} />
            </div>
            <DonutChart data={insights.entityTypes} labelKey="label" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Trade Volume Timeline</CardTitle>
              <CardDescription>Buy, sell, transfer activity, and trade count.</CardDescription>
            </div>
            <TrendingUp className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <StockTradeVolumeChart points={insights.tradeVolumeTimeline} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Gainers</CardTitle>
              <CardDescription>Best daily stock movement.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <StockMoverList title="Top gainers" movers={insights.winners} positive />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Losers</CardTitle>
              <CardDescription>Worst daily stock movement.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <StockMoverList title="Top losers" movers={insights.losers} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Daily Change Distribution</CardTitle>
              <CardDescription>Market-wide daily change histogram.</CardDescription>
            </div>
          </CardHeader>
          <CardContent className="grid gap-4">
            <CategoryBars data={insights.dailyChangeHistogram.map((item) => ({ label: item.label, value: item.count }))} />
            <CategoryBars data={insights.priceMovement} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity-to-Price Comparison</CardTitle>
              <CardDescription>XP activity compared with price and portfolio value.</CardDescription>
            </div>
            <Activity className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <StockActivityPriceComparisonChart points={insights.activityToPrice} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Ownership Concentration</CardTitle>
              <CardDescription>How much value sits with the largest holders.</CardDescription>
            </div>
            <Users className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-5">
            <CategoryBars data={insights.ownershipConcentration} />
            <DonutChart data={insights.buyVsSell} labelKey="label" />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Valuable Stocks</CardTitle>
              <CardDescription>Highest marked portfolio value.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <StockTable days={days} endDate={endDate} items={insights.mostValuableStocks} startDate={startDate} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Held Stocks</CardTitle>
              <CardDescription>Stocks with the broadest ownership.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <StockTable days={days} endDate={endDate} items={insights.mostHeldStocks} startDate={startDate} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Traded Stocks</CardTitle>
              <CardDescription>Trade volume and transaction count leaders.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <StockTable days={days} endDate={endDate} items={insights.mostTradedStocks} showTrades startDate={startDate} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Portfolio Pie</CardTitle>
              <CardDescription>Portfolio value held by user.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <DonutChart data={insights.holdingsByUser.map((user) => ({ label: user.username, value: user.portfolioValue }))} labelKey="label" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Holdings Table</CardTitle>
              <CardDescription>User holdings, value, ownership share, and unrealized gain.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <StockHoldingsTable holdings={insights.holdingsTable} />
          </CardContent>
        </Card>
      </section>
    </>
  );
}

function EconomyActorList({
  actors,
  days,
  emptyLabel,
  endDate,
  startDate,
  title,
}: {
  actors: DashboardEconomyActor[];
  days: number;
  emptyLabel: string;
  endDate: string;
  startDate: string;
  title: string;
}) {
  if (actors.length === 0) {
    return <EmptyRow label={emptyLabel} />;
  }

  return (
    <div className="grid gap-2">
      <div className="text-sm font-semibold text-foreground">{title}</div>
      {actors.slice(0, 6).map((actor) => (
        <Link
          className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2 transition-colors hover:border-primary hover:bg-slate-50"
          href={dashboardHref({ scope: "user", userId: actor.userId, days, startDate, endDate, view: "economy" })}
          key={`${title}-${actor.rank}-${actor.userId}`}
        >
          <span className="text-xs font-semibold text-muted">#{actor.rank}</span>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{actor.username}</div>
            <div className="text-xs text-muted">{formatInteger(actor.count)} {actor.label}</div>
          </div>
          <div className="text-right text-sm font-semibold text-foreground">{formatCurrency(actor.amount)}</div>
        </Link>
      ))}
    </div>
  );
}

function StockTable({
  days,
  endDate,
  items,
  showTrades = false,
  startDate,
}: {
  days: number;
  endDate: string;
  items: DashboardStockTableItem[];
  showTrades?: boolean;
  startDate: string;
}) {
  if (items.length === 0) {
    return <EmptyRow label="No stocks found" />;
  }

  return (
    <div className="grid gap-2">
      {items.slice(0, 8).map((stock) => {
        const href = stockEntityHref(stock, days, startDate, endDate);

        return (
          <div className="rounded-lg border border-border bg-white p-3" key={`${stock.stockId}-${stock.rank}`}>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                {href ? (
                  <Link className="truncate text-sm font-semibold text-foreground hover:text-primary" href={href}>
                    #{stock.rank} {stock.name}
                  </Link>
                ) : (
                  <div className="truncate text-sm font-semibold text-foreground">#{stock.rank} {stock.name}</div>
                )}
                <div className="text-xs text-muted">{stock.entityType} stock - {formatInteger(stock.holders)} holders</div>
              </div>
              <Badge variant={stock.dailyChangePercent >= 0 ? "success" : "danger"}>
                {stock.dailyChangePercent >= 0 ? "+" : ""}{stock.dailyChangePercent.toFixed(2)}%
              </Badge>
            </div>
            <div className="mt-3 grid grid-cols-3 gap-2 text-xs text-muted">
              <span>{formatCurrency(stock.price)}</span>
              <span>{formatCurrency(stock.holdingValue)}</span>
              <span>{showTrades ? `${formatCurrency(stock.tradeVolume)} traded` : `${formatCompactNumber(stock.sharesHeld)} shares`}</span>
            </div>
          </div>
        );
      })}
    </div>
  );
}

function StockHoldingsTable({ holdings }: { holdings: DashboardStockHoldingItem[] }) {
  if (holdings.length === 0) {
    return <EmptyRow label="No holdings found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[760px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">Holder</th>
            <th className="border-b border-border pb-2">Stock</th>
            <th className="border-b border-border pb-2 text-right">Shares</th>
            <th className="border-b border-border pb-2 text-right">Value</th>
            <th className="border-b border-border pb-2 text-right">Ownership</th>
            <th className="border-b border-border pb-2 text-right">Unrealized</th>
          </tr>
        </thead>
        <tbody>
          {holdings.slice(0, 12).map((holding) => (
            <tr key={`${holding.userId}-${holding.stockId}`}>
              <td className="border-b border-border py-3 pr-3 font-semibold text-foreground">#{holding.rank} {holding.username}</td>
              <td className="border-b border-border py-3 pr-3">
                <div className="font-medium text-foreground">{holding.stockName}</div>
                <div className="text-xs text-muted">{holding.entityType}</div>
              </td>
              <td className="border-b border-border py-3 text-right">{formatCompactNumber(holding.shares)}</td>
              <td className="border-b border-border py-3 text-right">{formatCurrency(holding.value)}</td>
              <td className="border-b border-border py-3 text-right">{holding.ownershipPercent.toFixed(1)}%</td>
              <td className="border-b border-border py-3 text-right">
                <Badge variant={holding.unrealizedGain >= 0 ? "success" : "danger"}>
                  {holding.unrealizedGain >= 0 ? "+" : ""}{formatCurrency(holding.unrealizedGain)}
                </Badge>
              </td>
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

function OperationsAnalyticsSection({
  buttonGame,
  operations,
}: {
  buttonGame: DashboardButtonGameInsights;
  operations: DashboardOperationsInsights;
}) {
  return (
    <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button Game Command Center</CardTitle>
              <CardDescription>Press volume, score records, competition density, and the quietest gaps between presses.</CardDescription>
            </div>
            <Badge variant="muted">{formatRelativeDate(buttonGame.lastPressAtUtc)}</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <ButtonGameChart points={buttonGame.daily} />
            <div className="grid gap-3 sm:grid-cols-5">
              <MetricPill label="Presses" value={formatInteger(buttonGame.presses)} />
              <MetricPill label="Total score" value={formatCompactNumber(buttonGame.score)} />
              <MetricPill label="Highest ever" value={formatCompactNumber(buttonGame.highestScoreEver)} />
              <MetricPill label="Average" value={buttonGame.averageScore.toFixed(1)} />
              <MetricPill label="Median" value={buttonGame.medianScore.toFixed(1)} />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button Score Distribution</CardTitle>
              <CardDescription>Histogram and strongest player totals in the selected window.</CardDescription>
            </div>
            <Gamepad2 className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-4">
            <CategoryBars data={buttonGame.scoreDistribution.map((bucket) => ({ label: bucket.label, value: bucket.count }))} />
            <ButtonLeaders leaders={buttonGame.topUsersByTotalScore} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Top Global Scores</CardTitle>
              <CardDescription>Best individual button presses across the current scope.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ButtonScoreTable scores={buttonGame.topGlobalScores} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Top Server Scores</CardTitle>
              <CardDescription>Best observed press per server.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ButtonScoreTable scores={buttonGame.topServerScores} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Top Individual Scores</CardTitle>
              <CardDescription>Best personal record per user.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ButtonScoreTable scores={buttonGame.topIndividualScores} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button Press Calendar</CardTitle>
              <CardDescription>Daily press density and participating users.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CalendarActivityHeatmap cells={buttonGame.calendarHeatmap} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button Hour Heatmap</CardTitle>
              <CardDescription>Presses by weekday and UTC hour.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ActivityHeatmap cells={buttonGame.hourByWeekdayHeatmap} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button Server Comparison</CardTitle>
              <CardDescription>Most active servers by press count.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CategoryBars data={buttonGame.pressesByServer} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Button User Comparison</CardTitle>
              <CardDescription>Users ranked by press count.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ButtonLeaders leaders={buttonGame.topUsersByPressCount} metric="presses" />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Most Competitive Servers</CardTitle>
              <CardDescription>Presses, active users, score yield, and recency combined.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ButtonServerTable servers={buttonGame.competitiveServers} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.95fr_1.05fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Longest Button Gaps</CardTitle>
              <CardDescription>Longest quiet stretches between button presses.</CardDescription>
            </div>
            <Clock3 className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <ButtonGapTable gaps={buttonGame.longestGaps} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Press Timing Split</CardTitle>
              <CardDescription>Peak hours and weekdays for button-game activity.</CardDescription>
            </div>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <CategoryBars data={buttonGame.pressesByHour} />
            <CategoryBars data={buttonGame.pressesByWeekday} />
          </CardContent>
        </Card>
      </section>

      <ReminderAnalyticsSection reminders={operations.reminders} />
      <ModerationAnalyticsSection moderation={operations.moderation} />
      <LogsAnalyticsSection logs={operations.logs} />
    </>
  );
}

function ReminderAnalyticsSection({ reminders }: { reminders: DashboardReminderStats }) {
  return (
    <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminder Timeline</CardTitle>
              <CardDescription>Reminder creation, due dates, overdue load, and upcoming demand.</CardDescription>
            </div>
            <Badge variant={reminders.overdue > 0 ? "danger" : "success"}>{formatInteger(reminders.pending)} active</Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <ReminderVolumeChart points={reminders.creationTrend} />
            <div className="grid gap-3 sm:grid-cols-4">
              <MetricPill label="Active" value={formatInteger(reminders.pending)} />
              <MetricPill label="Overdue" value={formatInteger(reminders.overdue)} />
              <MetricPill label="Due 24h" value={formatInteger(reminders.dueNext24Hours)} />
              <MetricPill label="Avg lead" value={`${reminders.averageLeadTimeHours.toFixed(1)}h`} />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminder Calendar</CardTitle>
              <CardDescription>Due-date calendar for reminder load.</CardDescription>
            </div>
            <Bell className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <CalendarActivityHeatmap cells={reminders.calendar} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-4">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Upcoming Reminders</CardTitle>
              <CardDescription>Nearest reminder queue.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ReminderList reminders={reminders.upcoming} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminders by Server</CardTitle>
              <CardDescription>Where reminders are concentrated.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CategoryBars data={reminders.byServer} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminder User Leaders</CardTitle>
              <CardDescription>Users with the most active reminders.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CategoryBars data={reminders.byUser} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reminders by Channel</CardTitle>
              <CardDescription>Channel-level reminder queues.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CategoryBars data={reminders.byChannel} />
          </CardContent>
        </Card>
      </section>
    </>
  );
}

function ModerationAnalyticsSection({ moderation }: { moderation: DashboardModerationStats }) {
  return (
    <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Moderation Timeline</CardTitle>
              <CardDescription>Temporary-ban creation, expiry, completion, and overdue pressure.</CardDescription>
            </div>
            <Badge variant={moderation.overdueTemporaryBans > 0 ? "danger" : "success"}>
              {formatInteger(moderation.pendingTemporaryBans)} pending
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <TemporaryBanTimelineChart points={moderation.temporaryBanTimeline} />
            <div className="grid gap-3 sm:grid-cols-4">
              <MetricPill label="Pending bans" value={formatInteger(moderation.pendingTemporaryBans)} />
              <MetricPill label="Overdue bans" value={formatInteger(moderation.overdueTemporaryBans)} />
              <MetricPill label="Completed 30d" value={formatInteger(moderation.completedLast30Days)} />
              <MetricPill label="Reaction items" value={formatInteger(moderation.reactionRoleItems)} />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Moderation Status</CardTitle>
              <CardDescription>Ban queue status, reasons, and reaction-role mode split.</CardDescription>
            </div>
            <ShieldCheck className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <DonutChart data={moderation.banStatus} labelKey="label" />
            <CategoryBars data={moderation.banReasons} />
            <CategoryBars data={moderation.reactionRoleTypes} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Setup Scorecards</CardTitle>
              <CardDescription>Configuration readiness across servers.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ConfigurationScorecards scorecards={moderation.serverScorecards} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Incomplete Server Setup</CardTitle>
              <CardDescription>Missing channels and incomplete enabled features.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <SetupIssueList issues={moderation.incompleteServerSetup} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Risky Configuration</CardTitle>
              <CardDescription>Weak approval thresholds and moderation-sensitive gaps.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <SetupIssueList issues={moderation.riskyConfiguration} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Reaction-Role Usage</CardTitle>
              <CardDescription>Messages, items, and button-vs-emoji usage by server.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ReactionRoleUsageTable usage={moderation.reactionRoleUsage} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Activity Role Distribution</CardTitle>
              <CardDescription>Configured activity-role tiers.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CategoryBars data={moderation.activityRoleDistribution} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Temporary Ban Queue</CardTitle>
              <CardDescription>Pending and overdue ban follow-through.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <ModerationList pending={moderation.pending} />
          </CardContent>
        </Card>
      </section>
    </>
  );
}

function LogsAnalyticsSection({ logs }: { logs: DashboardLogInsights }) {
  return (
    <>
      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Log Health</CardTitle>
              <CardDescription>Warnings, errors, incidents, and severity mix over time.</CardDescription>
            </div>
            <Badge variant={logs.errors + logs.critical > 0 ? "danger" : logs.warnings > 0 ? "warning" : "success"}>
              {formatInteger(logs.total)} logs
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-4">
            <LogErrorWarningLineChart points={logs.timeline} />
            <div className="grid gap-3 sm:grid-cols-4">
              <MetricPill label="Warnings" value={formatInteger(logs.warnings)} />
              <MetricPill label="Errors" value={formatInteger(logs.errors)} />
              <MetricPill label="Critical" value={formatInteger(logs.critical)} />
              <MetricPill label="Latest" value={formatRelativeDate(logs.latestAtUtc)} />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Severity Breakdown</CardTitle>
              <CardDescription>Severity cards, donut, and health indicators.</CardDescription>
            </div>
            <AlertTriangle className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <DonutChart data={logs.severityCounts.map((slice) => ({ label: slice.severity, value: slice.count }))} labelKey="label" />
            <CategoryBars data={logs.healthIndicators} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Logs by Version</CardTitle>
              <CardDescription>Build/version concentration in the selected window.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CategoryBars data={logs.logsByVersion} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Common Log Messages</CardTitle>
              <CardDescription>Repeated message patterns after noisy identifiers are collapsed.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <CategoryBars data={logs.commonMessages} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Recent Incidents</CardTitle>
              <CardDescription>Latest warning, error, and critical records.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <LogList logs={logs.recentIncidents} />
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Error and Warning Volume</CardTitle>
              <CardDescription>Daily total logs, warnings, and errors.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <LogTimelineChart points={logs.timeline} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Recent Logs</CardTitle>
              <CardDescription>Newest operational records captured by Morpheus.</CardDescription>
            </div>
          </CardHeader>
          <CardContent>
            <LogList logs={logs.recent} />
          </CardContent>
        </Card>
      </section>
    </>
  );
}

function ButtonScoreTable({ scores }: { scores: DashboardButtonGameScoreEntry[] }) {
  if (scores.length === 0) {
    return <EmptyRow label="No button score records found" />;
  }

  return (
    <div className="grid gap-2">
      {scores.map((score) => (
        <div className="grid grid-cols-[2.5rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white p-3" key={`${score.pressId}-${score.rank}`}>
          <span className="text-sm font-semibold text-muted">#{score.rank}</span>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{score.username}</div>
            <div className="truncate text-xs text-muted">{score.guildName ?? "Global"} - {formatRelativeDate(score.insertedAtUtc)}</div>
          </div>
          <span className="text-sm font-semibold text-foreground">{formatCompactNumber(score.score)}</span>
        </div>
      ))}
    </div>
  );
}

function ButtonLeaders({
  leaders,
  metric = "score",
}: {
  leaders: DashboardButtonGameUser[];
  metric?: "score" | "presses";
}) {
  if (leaders.length === 0) {
    return <EmptyRow label="No button-game players found" />;
  }

  return (
    <div className="grid gap-2">
      {leaders.map((leader) => (
        <div className="grid grid-cols-[3rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={leader.userId}>
          <span className="text-sm font-semibold text-muted">#{leader.rank}</span>
          <span className="truncate text-sm font-semibold text-foreground">{leader.username}</span>
          <span className="text-sm text-muted">
            {metric === "presses" ? `${formatInteger(leader.presses)} presses` : `${formatCompactNumber(leader.score)} score`}
          </span>
        </div>
      ))}
    </div>
  );
}

function ButtonServerTable({ servers }: { servers: DashboardButtonGameServer[] }) {
  if (servers.length === 0) {
    return <EmptyRow label="No competitive servers found" />;
  }

  return (
    <div className="grid gap-2">
      {servers.map((server) => (
        <div className="rounded-lg border border-border bg-white p-3" key={server.guildId}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">#{server.rank} {server.guildName}</div>
              <div className="text-xs text-muted">{formatInteger(server.activeUsers)} active users - {formatRelativeDate(server.lastPressAtUtc)}</div>
            </div>
            <Badge variant="muted">{server.competitiveScore.toFixed(1)}</Badge>
          </div>
          <div className="mt-3 grid grid-cols-3 gap-2 text-xs text-muted">
            <span>{formatInteger(server.presses)} presses</span>
            <span>{formatCompactNumber(server.score)} score</span>
            <span>{server.averageScore.toFixed(1)} avg</span>
          </div>
        </div>
      ))}
    </div>
  );
}

function ButtonGapTable({ gaps }: { gaps: DashboardButtonGameGap[] }) {
  if (gaps.length === 0) {
    return <EmptyRow label="No button-game gaps found" />;
  }

  return (
    <div className="grid gap-2">
      {gaps.map((gap) => (
        <div className="grid grid-cols-[3rem_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white p-3" key={`${gap.startedAtUtc}-${gap.rank}`}>
          <span className="text-sm font-semibold text-muted">#{gap.rank}</span>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{gap.hours.toFixed(1)} hours</div>
            <div className="truncate text-xs text-muted">{formatRelativeDate(gap.startedAtUtc)} to {formatRelativeDate(gap.endedAtUtc)}</div>
          </div>
          <span className="text-xs text-muted">{formatCompactNumber(gap.previousScore)} to {formatCompactNumber(gap.nextScore)}</span>
        </div>
      ))}
    </div>
  );
}

function ReminderList({
  reminders,
}: {
  reminders: DashboardReminderItem[];
}) {
  return (
    <div className="grid gap-2">
      {reminders.length === 0 ? (
        <EmptyRow label="No upcoming reminders" />
      ) : (
        reminders.map((reminder) => (
          <div className="rounded-lg border border-border bg-white p-3" key={reminder.id}>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="line-clamp-2 text-sm font-medium text-foreground">{reminder.text}</div>
                <div className="mt-1 text-xs text-muted">{reminder.user} - {reminder.server ?? reminder.channelId}</div>
              </div>
              <Badge variant={reminder.overdue ? "danger" : "muted"}>{reminder.overdue ? "Overdue" : "Queued"}</Badge>
            </div>
            <div className="mt-2 text-xs text-muted">Created {formatRelativeDate(reminder.createdAtUtc)} - due {formatRelativeDate(reminder.dueDateUtc)}</div>
          </div>
        ))
      )}
    </div>
  );
}

function ModerationList({
  pending,
}: {
  pending: Array<{ id: number; guildId: string; userId: string; reason: string | null; expiresAtUtc: string; status?: string }>;
}) {
  return (
    <div className="grid gap-2">
      {pending.length === 0 ? (
        <EmptyRow label="No pending temporary bans" />
      ) : (
        pending.map((ban) => (
          <div className="rounded-lg border border-border bg-white p-3" key={ban.id}>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="text-sm font-medium text-foreground">{ban.reason ?? "No reason recorded"}</div>
                <div className="mt-1 text-xs text-muted">{ban.userId} - {ban.guildId}</div>
              </div>
              <Badge variant={ban.status === "Overdue" ? "danger" : "warning"}>{ban.status ?? "Pending"}</Badge>
            </div>
            <div className="mt-2 text-xs text-muted">Expires {formatRelativeDate(ban.expiresAtUtc)}</div>
          </div>
        ))
      )}
    </div>
  );
}

function ConfigurationScorecards({ scorecards }: { scorecards: DashboardServerConfigurationScorecard[] }) {
  if (scorecards.length === 0) {
    return <EmptyRow label="No server configuration scorecards" />;
  }

  return (
    <div className="grid gap-2">
      {scorecards.map((scorecard) => (
        <div className="rounded-lg border border-border bg-white p-3" key={scorecard.guildId}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{scorecard.guildName}</div>
              <div className="text-xs text-muted">{scorecard.passedChecks} passed - {scorecard.failedChecks} failed</div>
            </div>
            <Badge variant={scorecard.score >= 85 ? "success" : scorecard.score >= 70 ? "warning" : "danger"}>
              {scorecard.score}
            </Badge>
          </div>
          <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-100">
            <div className="h-full rounded-full bg-blue-600" style={{ width: `${scorecard.score}%` }} />
          </div>
          <div className="mt-2 text-xs text-muted">{scorecard.notes.join(", ")}</div>
        </div>
      ))}
    </div>
  );
}

function SetupIssueList({ issues }: { issues: DashboardServerSetupIssue[] }) {
  if (issues.length === 0) {
    return <EmptyRow label="No setup issues found" />;
  }

  return (
    <div className="grid gap-2">
      {issues.map((issue) => (
        <div className="rounded-lg border border-border bg-white p-3" key={`${issue.guildId}-${issue.label}-${issue.detail}`}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{issue.label}</div>
              <div className="text-xs text-muted">{issue.guildName}</div>
            </div>
            <Badge variant={issue.severity === "Risk" ? "danger" : "warning"}>{issue.severity}</Badge>
          </div>
          <div className="mt-2 text-xs text-muted">{issue.detail}</div>
        </div>
      ))}
    </div>
  );
}

function ReactionRoleUsageTable({ usage }: { usage: DashboardReactionRoleUsage[] }) {
  if (usage.length === 0) {
    return <EmptyRow label="No reaction-role messages found" />;
  }

  return (
    <div className="scrollbar-clean overflow-x-auto">
      <table className="w-full min-w-[520px] border-separate border-spacing-0 text-sm">
        <thead>
          <tr className="text-left text-xs font-semibold text-muted">
            <th className="border-b border-border pb-2">Server</th>
            <th className="border-b border-border pb-2 text-right">Messages</th>
            <th className="border-b border-border pb-2 text-right">Items</th>
            <th className="border-b border-border pb-2 text-right">Buttons</th>
            <th className="border-b border-border pb-2 text-right">Emoji</th>
          </tr>
        </thead>
        <tbody>
          {usage.map((row) => (
            <tr key={row.guildId}>
              <td className="border-b border-border py-3 pr-3 font-semibold text-foreground">{row.guildName}</td>
              <td className="border-b border-border py-3 text-right">{formatInteger(row.messages)}</td>
              <td className="border-b border-border py-3 text-right">{formatInteger(row.items)}</td>
              <td className="border-b border-border py-3 text-right">{formatInteger(row.buttonMessages)}</td>
              <td className="border-b border-border py-3 text-right">{formatInteger(row.emojiMessages)}</td>
            </tr>
          ))}
        </tbody>
      </table>
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
  const detail = label === "No setup issues found"
    ? "No configuration issues need attention in the selected scope."
    : "Adjust filters or check back after more data is collected.";

  return (
    <div className="grid min-h-32 place-items-center rounded-lg border border-dashed border-border bg-slate-50 p-6 text-center">
      <div>
        <Database className="mx-auto h-6 w-6 text-muted" aria-hidden />
        <div className="mt-3 text-sm font-medium text-foreground">{label}</div>
        <div className="mt-1 text-xs text-muted">{detail}</div>
      </div>
    </div>
  );
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
  const isServerPage = scope === "server" && Boolean(guildId);
  const isUserPage = scope === "user" && Boolean(userId);

  if (startDate) {
    params.set("startDate", startDate);
  }

  if (endDate) {
    params.set("endDate", endDate);
  }

  if (guildId && !isServerPage) {
    params.set("guildId", String(guildId));
  }

  if (userId && !isUserPage) {
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

  const path = isServerPage ? `/servers/${guildId}` : isUserPage ? `/users/${userId}` : "/";
  return `${path}?${params.toString()}`;
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

function quoteDetailHref(quoteId: number, days: number, startDate: string, endDate: string) {
  return `/quotes/${quoteId}?${buildQuery({ days, startDate, endDate })}`;
}

function approvalDetailHref(approvalId: number, days: number, startDate: string, endDate: string) {
  return `/quote-approvals/${approvalId}?${buildQuery({ days, startDate, endDate })}`;
}

function buildQuery(params: Record<string, string | number | undefined>) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") {
      query.set(key, String(value));
    }
  }

  return query.toString();
}

function stockEntityHref(
  stock: Pick<DashboardStockTableItem, "entityType" | "entityId">,
  days: number,
  startDate: string,
  endDate: string,
) {
  if (stock.entityType === "User") {
    return dashboardHref({ scope: "user", userId: stock.entityId, days, startDate, endDate, view: "stocks" });
  }

  if (stock.entityType === "Server" || stock.entityType === "Guild") {
    return dashboardHref({ scope: "server", guildId: stock.entityId, days, startDate, endDate, view: "stocks" });
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
  const trimmed = value?.trim();
  if (!trimmed || !/^[1-9]\d*$/.test(trimmed)) {
    return undefined;
  }

  const parsed = Number(trimmed);
  return Number.isSafeInteger(parsed) ? parsed : undefined;
}

function parseOptionalDiscordId(value: string | undefined) {
  const trimmed = value?.trim();
  return trimmed && /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
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
