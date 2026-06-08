"use client";

import Link from "next/link";
import type React from "react";
import { showDashboardLoadingOverlay } from "@/components/dashboard/loading-overlay";
import { cn } from "@/lib/utils";

type DashboardNavLinkProps = {
  href?: string;
  children: React.ReactNode;
  className?: string;
  disabled?: boolean;
  "aria-current"?: React.AriaAttributes["aria-current"];
};

export function DashboardNavLink({
  href,
  children,
  className,
  disabled = false,
  "aria-current": ariaCurrent,
}: DashboardNavLinkProps) {
  if (disabled || !href) {
    return (
      <span
        aria-current={ariaCurrent}
        aria-disabled="true"
        className={cn("inline-flex cursor-not-allowed items-center gap-2 opacity-50", className)}
      >
        {children}
      </span>
    );
  }

  const linkHref = href;

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

    const target = new URL(linkHref, window.location.href);
    const current = new URL(window.location.href);
    if (target.pathname === current.pathname && target.search === current.search) {
      return;
    }

    window.requestAnimationFrame(() => {
      showDashboardLoadingOverlay("Loading dashboard data");
    });
  }

  return (
    <Link
      aria-current={ariaCurrent}
      className={cn("inline-flex items-center gap-2", className)}
      href={linkHref}
      onClick={handleClick}
    >
      {children}
    </Link>
  );
}
