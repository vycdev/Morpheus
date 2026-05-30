import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import type {
  DashboardChannelOption,
  DashboardGuildSummary,
  DashboardUserOption,
} from "@/lib/types";

type DashboardFiltersProps = {
  guilds: DashboardGuildSummary[];
  users: DashboardUserOption[];
  channels: DashboardChannelOption[];
  selectedGuildId?: number;
  selectedUserId?: number;
  selectedChannelId?: string;
  days: number;
  scope: string;
  sortDirection: string;
  minActivity: number;
};

export function DashboardFilters({
  guilds,
  users,
  channels,
  selectedGuildId,
  selectedUserId,
  selectedChannelId,
  days,
  scope,
  sortDirection,
  minActivity,
}: DashboardFiltersProps) {
  return (
    <form className="grid w-full gap-3 sm:grid-cols-2 lg:w-auto lg:grid-cols-7" method="get">
      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">Scope</span>
        <select
          className="h-10 min-w-32 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
          defaultValue={scope}
          name="scope"
        >
          <option value="global">Global</option>
          <option value="server">Server</option>
          <option value="user">User</option>
          <option value="channel">Channel</option>
        </select>
      </label>

      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">Server</span>
        <select
          className="h-10 min-w-48 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
          defaultValue={selectedGuildId?.toString() ?? ""}
          name="guildId"
        >
          <option value="">All guilds</option>
          {guilds.map((guild) => (
            <option key={guild.id} value={guild.id}>
              {guild.name}
            </option>
          ))}
        </select>
      </label>

      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">User</span>
        <select
          className="h-10 min-w-40 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
          defaultValue={selectedUserId?.toString() ?? ""}
          name="userId"
        >
          <option value="">All users</option>
          {users.map((user) => (
            <option key={user.userId} value={user.userId}>
              {user.username}
            </option>
          ))}
        </select>
      </label>

      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">Channel</span>
        <select
          className="h-10 min-w-40 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
          defaultValue={selectedChannelId ?? ""}
          name="channelId"
        >
          <option value="">All channels</option>
          {channels.map((channel) => (
            <option key={channel.discordId} value={channel.discordId}>
              {channel.name}
            </option>
          ))}
        </select>
      </label>

      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">Window</span>
        <select
          className="h-10 min-w-36 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
          defaultValue={String(days)}
          name="days"
        >
          <option value="7">7 days</option>
          <option value="30">30 days</option>
          <option value="60">60 days</option>
          <option value="90">90 days</option>
        </select>
      </label>

      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">Sort</span>
        <select
          className="h-10 min-w-32 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
          defaultValue={sortDirection}
          name="sortDirection"
        >
          <option value="desc">High first</option>
          <option value="asc">Low first</option>
        </select>
      </label>

      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">Minimum</span>
        <input
          className="h-10 min-w-28 rounded-lg border border-border bg-white px-3 text-sm text-foreground shadow-sm outline-none focus:border-primary"
          defaultValue={minActivity}
          min={0}
          name="minActivity"
          type="number"
        />
      </label>

      <Button className="self-end" type="submit">
        <RefreshCw className="h-4 w-4" aria-hidden="true" />
        Apply
      </Button>
    </form>
  );
}
