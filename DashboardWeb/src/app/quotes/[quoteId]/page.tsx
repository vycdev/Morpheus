import Link from "next/link";
import {
  ArrowLeft,
  CalendarClock,
  Quote as QuoteIcon,
  Server,
  ShieldCheck,
  Users,
} from "lucide-react";
import { ThemeToggle } from "@/components/dashboard/theme-toggle";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getQuoteDetails } from "@/lib/dashboard-api";
import type { DashboardQuoteApprovalRequestItem, DashboardQuoteVoteItem } from "@/lib/types";
import { formatInteger, formatRelativeDate } from "@/lib/utils";

type SearchParams = Record<string, string | string[] | undefined>;

export default async function QuoteDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ quoteId: string }> | { quoteId: string };
  searchParams?: Promise<SearchParams> | SearchParams;
}) {
  const resolvedParams = await Promise.resolve(params);
  const resolvedSearchParams = await Promise.resolve(searchParams ?? {});
  const quoteId = Number.parseInt(resolvedParams.quoteId, 10);
  const quote = Number.isFinite(quoteId) ? await getQuoteDetails(quoteId) : null;
  const days = getParam(resolvedSearchParams.days) ?? "30";
  const startDate = getParam(resolvedSearchParams.startDate);
  const endDate = getParam(resolvedSearchParams.endDate);
  const quotesDashboardHref = `/?${buildQuery({ scope: "global", view: "quotes", days, startDate, endDate })}`;

  if (!quote) {
    return (
      <main className="mx-auto grid min-h-screen w-full max-w-5xl content-start gap-4 px-4 py-6 sm:px-6">
        <Card>
          <CardHeader>
            <CardTitle>Quote Not Found</CardTitle>
            <CardDescription>The quote may not exist, or the dashboard API may be unavailable.</CardDescription>
          </CardHeader>
          <CardContent>
            <ActionLink href={quotesDashboardHref} icon={ArrowLeft} label="Back to quotes dashboard" />
          </CardContent>
        </Card>
      </main>
    );
  }

  const serverHref = `/servers/${quote.guildId}?${buildQuery({ scope: "server", guildId: String(quote.guildId), view: "quotes", days, startDate, endDate })}`;
  const authorHref = `/users/${quote.userId}?${buildQuery({ scope: "user", userId: String(quote.userId), view: "quotes", days, startDate, endDate })}`;
  const status = quote.removed ? "Removed" : quote.approved ? "Approved" : "Pending";
  const statusVariant = quote.removed ? "danger" : quote.approved ? "success" : "warning";

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-6xl content-start gap-5 px-4 py-5 sm:px-6 lg:px-8">
      <section className="rounded-lg border border-border bg-card p-4 shadow-sm">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex min-w-0 items-start gap-3">
            <div className="grid h-11 w-11 shrink-0 place-items-center rounded-lg bg-rose-50 text-rose-700">
              <QuoteIcon className="h-5 w-5" aria-hidden />
            </div>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-xl font-semibold leading-tight text-foreground sm:text-2xl">Quote #{quote.id}</h1>
                <Badge variant={statusVariant}>{status}</Badge>
              </div>
              <p className="mt-1 text-sm text-muted">
                Quote detail, author context, score activity, and moderation history.
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
            <CardTitle>Quote Content</CardTitle>
            <CardDescription>Original quote text and primary navigation targets.</CardDescription>
          </div>
          <CalendarClock className="h-5 w-5 text-muted" aria-hidden />
        </CardHeader>
        <CardContent className="grid gap-4">
          <blockquote className="rounded-lg border border-border bg-slate-50 p-5 text-base leading-7 text-foreground">
            {quote.content}
          </blockquote>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Metric label="Score" value={`${quote.totalScore >= 0 ? "+" : ""}${formatInteger(quote.totalScore)}`} />
            <Metric label="Created" value={formatRelativeDate(quote.insertedAtUtc)} />
            <Metric label="Author" value={quote.author} href={authorHref} />
            <Metric label="Server" value={quote.guildName} href={serverHref} />
            <Metric label="Score voters" value={formatInteger(quote.voters.length)} />
            <Metric label="Approval requests" value={formatInteger(quote.approvalRequests.length)} />
            <Metric label="Status" value={status} />
          </div>
          <div className="flex flex-wrap gap-2">
            <ActionLink href={authorHref} icon={Users} label="Open author profile" />
            <ActionLink href={serverHref} icon={Server} label="Open server quotes" />
          </div>
        </CardContent>
      </Card>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.85fr_1.15fr]">
        <Card>
          <CardHeader>
            <div>
              <CardTitle>Voter Activity</CardTitle>
              <CardDescription>Users who scored this quote.</CardDescription>
            </div>
            <Users className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <VoteList voters={quote.voters} days={days} endDate={endDate} startDate={startDate} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div>
              <CardTitle>Approval Requests</CardTitle>
              <CardDescription>Add and remove approval history for this quote.</CardDescription>
            </div>
            <ShieldCheck className="h-5 w-5 text-muted" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <ApprovalList days={days} endDate={endDate} requests={quote.approvalRequests} startDate={startDate} />
          </CardContent>
        </Card>
      </section>
    </main>
  );
}

function Metric({ href, label, value }: { href?: string; label: string; value: string }) {
  const content = (
    <div className="rounded-lg border border-border bg-white p-3 transition-colors hover:border-primary">
      <div className="text-xs font-medium uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 truncate text-sm font-semibold text-foreground">{value}</div>
    </div>
  );

  return href ? <Link className="hover:text-primary" href={href}>{content}</Link> : content;
}

function VoteList({
  days,
  endDate,
  startDate,
  voters,
}: {
  days: string;
  endDate?: string;
  startDate?: string;
  voters: DashboardQuoteVoteItem[];
}) {
  if (voters.length === 0) {
    return <EmptyPanel label="No score votes found" />;
  }

  return (
    <div className="grid gap-2">
      {voters.map((voter) => {
        const profileHref = userProfileHref(voter.userId, days, startDate, endDate);

        return (
          <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={`${voter.userId}-${voter.rank}`}>
            <span className="text-xs font-semibold text-muted">#{voter.rank}</span>
            <div className="min-w-0">
              {profileHref ? (
                <Link
                  className="truncate text-sm font-semibold text-foreground hover:text-primary"
                  href={profileHref}
                >
                  {voter.username}
                </Link>
              ) : (
                <div className="truncate text-sm font-semibold text-foreground">{voter.username}</div>
              )}
              <div className="text-xs text-muted">
                {formatInteger(voter.positiveVotes)} up / {formatInteger(voter.negativeVotes)} down
                {voter.lastVotedAtUtc ? ` - ${formatRelativeDate(voter.lastVotedAtUtc)}` : ""}
              </div>
            </div>
            <Badge variant={voter.score >= 0 ? "success" : "danger"}>{voter.score >= 0 ? "+" : ""}{formatInteger(voter.score)}</Badge>
          </div>
        );
      })}
    </div>
  );
}

function ApprovalList({
  days,
  endDate,
  requests,
  startDate,
}: {
  days: string;
  endDate?: string;
  requests: DashboardQuoteApprovalRequestItem[];
  startDate?: string;
}) {
  if (requests.length === 0) {
    return <EmptyPanel label="No approval requests found" />;
  }

  return (
    <div className="grid gap-3">
      {requests.map((request) => (
        <Link
          className="rounded-lg border border-border bg-white p-3 transition-colors hover:border-primary"
          href={`/quote-approvals/${request.id}?${buildQuery({ days, startDate, endDate })}`}
          key={request.id}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-foreground">{request.type} request #{request.id}</div>
              <div className="text-xs text-muted">{formatInteger(request.currentApprovals)} / {formatInteger(request.requiredApprovals)} approvals</div>
            </div>
            <Badge variant={request.expired ? "danger" : request.status === "Approved" ? "success" : "warning"}>{request.status}</Badge>
          </div>
          <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-100">
            <div className="h-full rounded-full bg-primary" style={{ width: `${Math.max(3, request.completionPercent)}%` }} />
          </div>
          <div className="mt-2 text-xs text-muted">
            Opened {formatRelativeDate(request.insertedAtUtc)} - {request.expired ? "expired" : "expires"} {formatRelativeDate(request.expiresAtUtc)}
          </div>
        </Link>
      ))}
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

function EmptyPanel({ label }: { label: string }) {
  return (
    <div className="grid min-h-28 place-items-center rounded-lg border border-dashed border-border bg-slate-50 p-5 text-center">
      <div>
        <QuoteIcon className="mx-auto h-5 w-5 text-muted" aria-hidden />
        <div className="mt-2 text-sm font-medium text-foreground">{label}</div>
        <div className="mt-1 text-xs text-muted">This quote has no matching records yet.</div>
      </div>
    </div>
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

function userProfileHref(userId: number, days: string, startDate?: string, endDate?: string) {
  if (!Number.isSafeInteger(userId) || userId <= 0) {
    return null;
  }

  return `/users/${userId}?${buildQuery({ scope: "user", userId, view: "quotes", days, startDate, endDate })}`;
}

function getParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
