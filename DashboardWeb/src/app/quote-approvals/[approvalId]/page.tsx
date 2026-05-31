import Link from "next/link";
import { ShieldCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getQuoteApprovalDetails } from "@/lib/dashboard-api";
import type { DashboardQuoteApprovalRequestItem } from "@/lib/types";
import { formatInteger, formatRelativeDate } from "@/lib/utils";

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
  const approvalId = Number.parseInt(resolvedParams.approvalId, 10);
  const days = Number.parseInt(getParam(resolvedSearchParams.days) ?? "365", 10);
  const request = Number.isFinite(approvalId) ? await getQuoteApprovalDetails(approvalId) : null;

  if (!request) {
    return (
      <main className="mx-auto grid min-h-screen w-full max-w-5xl content-start gap-4 px-4 py-6">
        <Card>
          <CardHeader>
            <CardTitle>Approval Request Not Found</CardTitle>
            <CardDescription>The request may not exist, or the dashboard API may be unavailable.</CardDescription>
          </CardHeader>
          <CardContent>
            <Link className="text-sm font-medium text-primary" href="/?view=quotes">
              Back to quotes dashboard
            </Link>
          </CardContent>
        </Card>
      </main>
    );
  }

  return (
    <main className="mx-auto grid min-h-screen w-full max-w-5xl content-start gap-4 px-4 py-6">
      <Card>
        <CardHeader>
          <div>
            <CardTitle>Approval Request #{request.id}</CardTitle>
            <CardDescription>Approval status, quote preview, expiry, and moderation links.</CardDescription>
          </div>
          <Badge variant={request.expired ? "danger" : request.status === "Approved" ? "success" : "warning"}>{request.status}</Badge>
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
          <div className="h-2 overflow-hidden rounded-full bg-slate-100">
            <div className="h-full rounded-full bg-primary" style={{ width: `${Math.max(3, request.completionPercent)}%` }} />
          </div>
          <div className="flex flex-wrap gap-3 text-sm">
            <Link className="font-medium text-primary" href={`/quotes/${request.quoteId}`}>
              Quote detail
            </Link>
            <Link className="font-medium text-primary" href={`/servers/${request.guildId}?scope=server&view=quotes`}>
              {request.guildName}
            </Link>
            <Link className="font-medium text-primary" href="/?view=quotes">
              Quotes dashboard
            </Link>
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
        <div>Author: <span className="font-medium text-foreground">{request.author}</span></div>
        <div>Completion: <span className="font-medium text-foreground">{request.completionPercent.toFixed(1)}%</span></div>
        <div>Completed: <span className="font-medium text-foreground">{formatRelativeDate(request.completedAtUtc)}</span></div>
      </CardContent>
    </Card>
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

function getParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
