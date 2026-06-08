import { DashboardDateRangePicker } from "@/components/dashboard/date-range-picker";
import { DashboardNavLink } from "@/components/dashboard/nav-link";
import { SearchableSelect, type SearchableSelectOption } from "@/components/dashboard/searchable-select";
import { DashboardSubmitButton } from "@/components/dashboard/submit-button";
import { DashboardUpdateForm } from "@/components/dashboard/update-form";
import type {
  DashboardChannelOption,
  DashboardGuildSummary,
  DashboardUserOption,
} from "@/lib/types";
import { cn } from "@/lib/utils";

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

type DashboardFiltersProps = {
  guilds: DashboardGuildSummary[];
  users: DashboardUserOption[];
  channels: DashboardChannelOption[];
  selectedGuildId?: number;
  selectedUserId?: number;
  selectedChannelId?: string;
  days: number;
  startDate: string;
  endDate: string;
  scope: DashboardScope;
  sortDirection: string;
  minActivity: number;
  view: DashboardView;
};

export function DashboardFilters({
  guilds,
  users,
  channels,
  selectedGuildId,
  selectedUserId,
  selectedChannelId,
  days,
  startDate,
  endDate,
  scope,
  sortDirection,
  minActivity,
  view,
}: DashboardFiltersProps) {
  const isGlobal = scope === "global";
  const hasSelectedUserOption = selectedUserId ? users.some((user) => user.userId === selectedUserId) : true;
  const hasSelectedChannelOption = selectedChannelId
    ? channels.some((channel) => channel.discordId === selectedChannelId)
    : true;
  const guildOptions: SearchableSelectOption[] = guilds.map((guild) => ({
    value: String(guild.id),
    label: guild.name,
    description: guild.discordId,
  }));
  const userOptions: SearchableSelectOption[] = [
    ...(selectedUserId && !hasSelectedUserOption
      ? [{ value: String(selectedUserId), label: `User #${selectedUserId}` }]
      : []),
    ...users.map((user) => ({
      value: String(user.userId),
      label: user.username,
      description: user.discordId,
    })),
  ];
  const channelOptions: SearchableSelectOption[] = [
    ...(selectedChannelId && !hasSelectedChannelOption
      ? [{ value: selectedChannelId, label: `Channel ${selectedChannelId}` }]
      : []),
    ...channels.map((channel) => ({
      value: channel.discordId,
      label: channel.name,
      description: channel.discordId,
    })),
  ];

  const defaultGuildId = selectedGuildId ?? guilds[0]?.id;
  const defaultUserId = selectedUserId ?? users[0]?.userId;
  const defaultChannelId = selectedChannelId ?? channels[0]?.discordId;
  const scopeTabs: Array<{ label: string; scope: DashboardScope; href: string; disabled?: boolean }> = [
    { label: "Global", scope: "global", href: dashboardHref({ scope: "global", days, startDate, endDate }) },
    {
      label: "Server",
      scope: "server",
      href: dashboardHref({ scope: "server", guildId: defaultGuildId, days, startDate, endDate }),
      disabled: !defaultGuildId,
    },
    {
      label: "User",
      scope: "user",
      href: dashboardHref({ scope: "user", userId: defaultUserId, guildId: defaultGuildId, days, startDate, endDate }),
      disabled: !defaultUserId,
    },
    {
      label: "Channel",
      scope: "channel",
      href: dashboardHref({ scope: "channel", channelId: defaultChannelId, guildId: defaultGuildId, days, startDate, endDate }),
      disabled: !defaultChannelId,
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <nav aria-label="Dashboard scope" className="flex w-full max-w-full flex-wrap items-center gap-1 rounded-lg border border-border bg-slate-50 p-1 sm:w-fit sm:self-start">
        {scopeTabs.map((tab) => {
          const active = tab.scope === scope;
          const className = cn(
            "inline-flex h-9 flex-1 items-center justify-center rounded-md px-3 text-sm font-medium transition-colors sm:flex-none",
            active
              ? "bg-primary text-primary-foreground"
              : "text-muted hover:bg-card hover:text-foreground",
          );

          return (
            <DashboardNavLink
              aria-current={active ? "page" : undefined}
              className={className}
              disabled={tab.disabled}
              href={tab.href}
              key={tab.scope}
            >
              {tab.label}
            </DashboardNavLink>
          );
        })}
      </nav>

      <DashboardUpdateForm className="grid grid-cols-1 items-end gap-3 rounded-lg border border-border bg-slate-50 p-3 sm:grid-cols-2 xl:flex xl:flex-wrap">
        <input name="scope" type="hidden" defaultValue={scope} />
        <input name="view" type="hidden" defaultValue={view} />

        {!isGlobal && (
          <SearchableSelect
            emptyOptionLabel={scope === "server" ? "Choose server" : "All servers"}
            label="Server"
            name="guildId"
            options={guildOptions}
            pageSize={8}
            placeholder="Choose server"
            value={selectedGuildId?.toString() ?? ""}
          />
        )}

        {scope === "user" && (
          <SearchableSelect
            disabled={users.length === 0 && !selectedUserId}
            emptyOptionLabel="Choose user"
            label="User"
            name="userId"
            options={userOptions}
            pageSize={8}
            placeholder="Choose user"
            value={selectedUserId?.toString() ?? ""}
          />
        )}

        {scope === "channel" && (
          <SearchableSelect
            disabled={channels.length === 0 && !selectedChannelId}
            emptyOptionLabel="Choose channel"
            label="Channel"
            name="channelId"
            options={channelOptions}
            pageSize={8}
            placeholder="Choose channel"
            value={selectedChannelId ?? ""}
          />
        )}

        <DashboardDateRangePicker days={days} endDate={endDate} startDate={startDate} />

        {!isGlobal && (
          <>
            <label className="grid gap-1 text-sm">
              <span className="font-medium text-muted">Rank order</span>
              <select
                className="h-10 min-w-36 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
                defaultValue={sortDirection}
                name="sortDirection"
              >
                <option value="desc">High first</option>
                <option value="asc">Low first</option>
              </select>
            </label>

            <label className="grid gap-1 text-sm">
              <span className="font-medium text-muted">Minimum messages</span>
              <input
                className="h-10 min-w-32 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
                aria-label="Minimum messages required for leaderboard rows"
                defaultValue={minActivity}
                min={0}
                name="minActivity"
                title="Hide rows with fewer messages than this value."
                type="number"
              />
            </label>
          </>
        )}

        <DashboardSubmitButton />
      </DashboardUpdateForm>
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
}: {
  scope: DashboardScope;
  guildId?: number;
  userId?: number;
  channelId?: string;
  days: number;
  startDate?: string;
  endDate?: string;
}) {
  const params = new URLSearchParams({ scope, days: String(days) });
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

  const path = isServerPage ? `/servers/${guildId}` : isUserPage ? `/users/${userId}` : "/";
  return `${path}?${params.toString()}`;
}
