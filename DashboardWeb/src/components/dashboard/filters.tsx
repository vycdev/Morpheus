import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { DashboardGuildSummary } from "@/lib/types";

type DashboardFiltersProps = {
  guilds: DashboardGuildSummary[];
  selectedGuildId?: number;
  days: number;
};

export function DashboardFilters({ guilds, selectedGuildId, days }: DashboardFiltersProps) {
  return (
    <form className="flex flex-wrap items-end gap-3" method="get">
      <label className="grid gap-1 text-sm">
        <span className="font-medium text-muted">Guild</span>
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

      <Button type="submit">
        <RefreshCw className="h-4 w-4" aria-hidden="true" />
        Apply
      </Button>
    </form>
  );
}
