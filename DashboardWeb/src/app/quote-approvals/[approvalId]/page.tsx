import Link from "next/link";
import { ArrowLeft, ArrowUpRight, CalendarClock, Quote, Server, ShieldCheck } from "lucide-react";
import { ThemeToggle } from "@/components/dashboard/theme-toggle";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getQuoteApprovalDetails } from "@/lib/dashboard-api";
import type { DashboardQuoteApprovalRequestItem } from "@/lib/types";
import { cn, formatInteger, formatRelativeDate } from "@/lib/utils";

type SearchParams = Record<string, string | string[] | undefined>;

export default async function QuoteApprovalDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ approvalId: string }> | { approvalId: string };
  searchParams?: Promise<SearchParams> | SearchParams;
}) {
  const resolvedParams = await Promise.resolve(params);
  const resolvedSearchParams = await Promise.resolve(searchParams ?? {});
  const approvalId = parsePositiveInteger(resolvedParams.approvalId);
  const days = getParam(resolvedSearchParams.days) ?? "365";
  const startDate = getParam(resolvedSearchParams.startDate);
  const endDate = getParam(resolvedSearchParams.endDate);
  const request = approvalId ? await getQuoteApprovalDetails(approvalId) : null;
  const quotesDashboardHref = `/?${buildQuery({ scope: "global", view: "quotes", days, startDate, endDate })}`;

  if (!request) {
    return (
      <main className="mx-auto grid min-h-screen w-full max-w-5xl content-start gap-4 px-4 py-6 sm:px-6">
        <Card>
          <CardHeader>
            <CardTitle>Approval Request Not Found</CardTitle>
            <CardDescription>The request may not exist, or the dashboard API may be unavailable.</CardDescription>
          </CardHeader>
          <CardContent>
            <ActionLink href={quotesDashboardHref} icon={ArrowLeft} label="Back to quotes dashboard" />
          </CardContent>
        </Card>
      </main>
    );
  }

  const quoteHref = `/quotes/${request.quoteId}?${buildQuery({ days, startDate, endDate })}`;
  const serverHref = `/servers/${request.guildId}?${buildQuery({ scope: "server", guildId: request.guildId, view: "quotes", days, startDate, endDate })}`;
  const statusVariant = request.expired ? "danger" : request.status === "Approved" ? "success" : "warning";

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-5xl content-start gap-5 px-4 py-5 sm:px-6 lg:px-8">
      <section className="rounded-lg border border-border bg-card p-4 shadow-sm">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex min-w-0 items-start gap-3">
            <div className="grid h-11 w-11 shrink-0 place-items-center rounded-lg bg-amber-50 text-amber-700">
              <ShieldCheck className="h-5 w-5" aria-hidden />
            </div>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-xl font-semibold leading-tight text-foreground sm:text-2xl">Approval Request #{request.id}</h1>
                <Badge variant={statusVariant}>{request.status}</Badge>
              </div>
              <p className="mt-1 text-sm text-muted">
                Approval progress, quote preview, expiry, and moderation links.
              </p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <ActionLink href={quotesDashboardHref} icon={ArrowLeft} label="Quotes dashboard" />
            <ThemeToggle />
          </div>
        </div>
      </section>

      <Card>
        <CardHeader>
          <div>
            <CardTitle>Approval Progress</CardTitle>
            <CardDescription>Current moderation state and the quote under review.</CardDescription>
          </div>
          <CalendarClock className="h-5 w-5 text-muted" aria-hidden />
        </CardHeader>
        <CardContent className="grid gap-4">
          <blockquote className="rounded-lg border border-border bg-slate-50 p-4 text-base leading-7 text-foreground">
            {request.quoteContent}
          </blockquote>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Metric label="Type" value={request.type} />
            <Metric label="Approvals" value={`${formatInteger(request.currentApprovals)} / ${formatInteger(request.requiredApprovals)}`} />
            <Metric label="Opened" value={formatRelativeDate(request.insertedAtUtc)} />
            <Metric label={request.expired ? "Expired" : "Expires"} value={formatRelativeDate(request.expiresAtUtc)} />
          </div>
          <ProgressMeter completionPercent={request.completionPercent} expired={request.expired} />
          <div className="flex flex-wrap gap-2">
            <ActionLink href={quoteHref} icon={Quote} label="Quote detail" />
            <ActionLink href={serverHref} icon={Server} label={request.guildName} />
            <ActionLink href={quotesDashboardHref} icon={ArrowUpRight} label="Quotes dashboard" />
          </div>
        </CardContent>
      </Card>

      <ApprovalStatusCard request={request} />
    </main>
  );
}

function ApprovalStatusCard({ request }: { request: DashboardQuoteApprovalRequestItem }) {
  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>Moderation State</CardTitle>
          <CardDescription>How close this request is to completion.</CardDescription>
        </div>
        <ShieldCheck className="h-5 w-5 text-muted" aria-hidden="true" />
      </CardHeader>
      <CardContent className="grid gap-3 text-sm text-muted">
        <StateRow label="Author" value={request.author} />
        <StateRow label="Completion" value={`${request.completionPercent.toFixed(1)}%`} />
        <StateRow label="Completed" value={formatRelativeDate(request.completedAtUtc)} />
      </CardContent>
    </Card>
  );
}

function ProgressMeter({
  completionPercent,
  expired,
}: {
  completionPercent: number;
  expired: boolean;
}) {
  const width = Math.max(3, Math.min(100, completionPercent));

  return (
    <div className="rounded-lg border border-border bg-slate-50 p-3">
      <div className="flex items-center justify-between gap-3 text-xs font-medium text-muted">
        <span>Completion</span>
        <span>{completionPercent.toFixed(1)}%</span>
      </div>
      <div className="mt-2 h-2.5 overflow-hidden rounded-full bg-slate-100">
        <div
          className={cn("h-full rounded-full", expired ? "bg-rose-500" : "bg-primary")}
          style={{ width: `${width}%` }}
        />
      </div>
    </div>
  );
}

function StateRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-slate-50 px-3 py-2">
      <span>{label}</span>
      <span className="text-right font-medium text-foreground">{value}</span>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border bg-white p-3">
      <div className="text-xs font-medium uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 truncate text-sm font-semibold text-foreground">{value}</div>
    </div>
  );
}

function ActionLink({
  href,
  icon: Icon,
  label,
}: {
  href: string;
  icon: React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;
  label: string;
}) {
  return (
    <Link
      className="inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-border bg-white px-3 text-sm font-medium text-foreground transition-colors hover:border-primary hover:text-primary"
      href={href}
    >
      <Icon className="h-4 w-4" aria-hidden />
      {label}
    </Link>
  );
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

function getParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parsePositiveInteger(value: string) {
  if (!/^[1-9]\d*$/.test(value)) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isSafeInteger(parsed) ? parsed : undefined;
}
