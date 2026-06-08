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
    const formData = new FormData(form);
    const target = new URL(form.action || window.location.href, window.location.href);
    const params = new URLSearchParams();

    for (const [key, value] of formData) {
      if (typeof value === "string" && value.trim() !== "") {
        params.set(key, value.trim());
      }
    }

    const scope = getFormString(formData, "scope");
    const guildId = parsePositiveInteger(getFormString(formData, "guildId"));
    const userId = parsePositiveInteger(getFormString(formData, "userId"));

    if (scope === "server" && guildId) {
      target.pathname = `/servers/${guildId}`;
      params.delete("guildId");
    } else if (scope === "user" && userId) {
      target.pathname = `/users/${userId}`;
      params.delete("userId");
    } else {
      target.pathname = "/";
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

function getFormString(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function parsePositiveInteger(value: string) {
  if (!/^[1-9]\d*$/.test(value)) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isSafeInteger(parsed) ? parsed : undefined;
}
