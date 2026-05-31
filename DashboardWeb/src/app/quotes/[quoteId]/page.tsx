import Link from "next/link";
import { ArrowUpRight, ShieldCheck, Users } from "lucide-react";
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

  if (!quote) {
    return (
      <main className="mx-auto grid min-h-screen w-full max-w-5xl content-start gap-4 px-4 py-6">
        <Card>
          <CardHeader>
            <CardTitle>Quote Not Found</CardTitle>
            <CardDescription>The quote may not exist, or the dashboard API may be unavailable.</CardDescription>
          </CardHeader>
          <CardContent>
            <Link className="text-sm font-medium text-primary" href={`/?view=quotes&days=${days}`}>
              Back to quotes dashboard
            </Link>
          </CardContent>
        </Card>
      </main>
    );
  }

  const query = new URLSearchParams({ scope: "server", guildId: String(quote.guildId), view: "quotes", days });
  if (startDate) {
    query.set("startDate", startDate);
  }
  if (endDate) {
    query.set("endDate", endDate);
  }
  const serverHref = `/servers/${quote.guildId}?${query.toString()}`;
  const authorQuery = new URLSearchParams({ scope: "user", userId: String(quote.userId), view: "quotes", days });
  if (startDate) {
    authorQuery.set("startDate", startDate);
  }
  if (endDate) {
    authorQuery.set("endDate", endDate);
  }

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-6xl content-start gap-4 px-4 py-6">
      <Card>
        <CardHeader>
          <div>
            <CardTitle>Quote #{quote.id}</CardTitle>
            <CardDescription>Quote detail, status, voter activity, and approval history.</CardDescription>
          </div>
          <Badge variant={quote.removed ? "danger" : quote.approved ? "success" : "warning"}>
            {quote.removed ? "Removed" : quote.approved ? "Approved" : "Pending"}
          </Badge>
        </CardHeader>
        <CardContent className="grid gap-4">
          <blockquote className="rounded-lg border border-border bg-slate-50 p-4 text-base leading-7 text-foreground">
            {quote.content}
          </blockquote>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Metric label="Score" value={`${quote.totalScore >= 0 ? "+" : ""}${formatInteger(quote.totalScore)}`} />
            <Metric label="Created" value={formatRelativeDate(quote.insertedAtUtc)} />
            <Metric label="Author" value={quote.author} href={`/users/${quote.userId}?${authorQuery.toString()}`} />
            <Metric label="Server" value={quote.guildName} href={serverHref} />
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
            <VoteList voters={quote.voters} />
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
            <ApprovalList requests={quote.approvalRequests} />
          </CardContent>
        </Card>
      </section>

      <Link className="inline-flex items-center gap-2 text-sm font-medium text-primary" href={`/?view=quotes&days=${days}`}>
        Back to quotes dashboard
        <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
      </Link>
    </main>
  );
}

function Metric({ href, label, value }: { href?: string; label: string; value: string }) {
  const content = (
    <div className="rounded-lg border border-border bg-white p-3">
      <div className="text-xs font-medium uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 truncate text-sm font-semibold text-foreground">{value}</div>
    </div>
  );

  return href ? <Link className="hover:text-primary" href={href}>{content}</Link> : content;
}

function VoteList({ voters }: { voters: DashboardQuoteVoteItem[] }) {
  if (voters.length === 0) {
    return <div className="rounded-lg border border-dashed border-border p-5 text-center text-sm text-muted">No score votes found</div>;
  }

  return (
    <div className="grid gap-2">
      {voters.map((voter) => (
        <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-lg border border-border bg-white px-3 py-2" key={voter.userId}>
          <span className="text-xs font-semibold text-muted">#{voter.rank}</span>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-foreground">{voter.username}</div>
            <div className="text-xs text-muted">{formatInteger(voter.positiveVotes)} up / {formatInteger(voter.negativeVotes)} down</div>
          </div>
          <Badge variant={voter.score >= 0 ? "success" : "danger"}>{voter.score >= 0 ? "+" : ""}{formatInteger(voter.score)}</Badge>
        </div>
      ))}
    </div>
  );
}

function ApprovalList({ requests }: { requests: DashboardQuoteApprovalRequestItem[] }) {
  if (requests.length === 0) {
    return <div className="rounded-lg border border-dashed border-border p-5 text-center text-sm text-muted">No approval requests found</div>;
  }

  return (
    <div className="grid gap-3">
      {requests.map((request) => (
        <Link className="rounded-lg border border-border bg-white p-3 hover:border-primary" href={`/quote-approvals/${request.id}`} key={request.id}>
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

function getParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
