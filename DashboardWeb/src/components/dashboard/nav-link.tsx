"use client";

import Link from "next/link";
import type React from "react";
import { showDashboardLoadingOverlay } from "@/components/dashboard/loading-overlay";
import { cn } from "@/lib/utils";

type DashboardNavLinkProps = {
  href: string;
  children: React.ReactNode;
  className?: string;
  "aria-current"?: React.AriaAttributes["aria-current"];
};

export function DashboardNavLink({
  href,
  children,
  className,
  "aria-current": ariaCurrent,
}: DashboardNavLinkProps) {
  function handleClick(event: React.MouseEvent<HTMLAnchorElement>) {
    if (
      event.defaultPrevented ||
      event.button !== 0 ||
      event.metaKey ||
      event.altKey ||
      event.ctrlKey ||
      event.shiftKey
    ) {
      return;
    }

    const target = new URL(href, window.location.href);
    const current = new URL(window.location.href);
    if (target.pathname === current.pathname && target.search === current.search) {
      return;
    }

    showDashboardLoadingOverlay("Loading dashboard data");
  }

  return (
    <Link
      aria-current={ariaCurrent}
      className={cn("inline-flex items-center gap-2", className)}
      href={href}
      onClick={handleClick}
    >
      {children}
    </Link>
  );
}
