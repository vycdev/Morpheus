"use client";

import { useEffect, useTransition, type FormEvent, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { hideDashboardLoadingOverlay, showDashboardLoadingOverlay } from "@/components/dashboard/loading-overlay";

type DashboardUpdateFormProps = {
  children: ReactNode;
  className?: string;
};

export function DashboardUpdateForm({ children, className }: DashboardUpdateFormProps) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  useEffect(() => {
    if (!pending) {
      hideDashboardLoadingOverlay();
    }
  }, [pending]);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const form = event.currentTarget;
    const target = new URL(form.action || window.location.href, window.location.href);
    const params = new URLSearchParams();

    for (const [key, value] of new FormData(form)) {
      if (typeof value === "string" && value.trim() !== "") {
        params.set(key, value);
      }
    }

    target.search = params.toString();
    const nextUrl = `${target.pathname}${target.search}`;
    const currentUrl = `${window.location.pathname}${window.location.search}`;

    showDashboardLoadingOverlay("Updating dashboard data", { immediate: true });

    startTransition(() => {
      if (nextUrl === currentUrl) {
        router.refresh();
      } else {
        router.push(nextUrl);
      }
    });
  }

  return (
    <form action="/" className={className} method="get" onSubmit={handleSubmit}>
      {children}
    </form>
  );
}
