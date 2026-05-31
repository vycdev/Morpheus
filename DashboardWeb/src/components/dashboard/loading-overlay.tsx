"use client";

import { useEffect, useRef, useState, type MutableRefObject } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { Loader2 } from "lucide-react";

const loadingStartEvent = "morpheus-dashboard-loading:start";
const loadingEndEvent = "morpheus-dashboard-loading:end";
const showDelayMs = 120;
const maxVisibleMs = 20000;

export function showDashboardLoadingOverlay(
  message = "Loading dashboard data",
  options: { immediate?: boolean } = {},
) {
  window.dispatchEvent(new CustomEvent(loadingStartEvent, { detail: { message, immediate: options.immediate } }));
}

export function hideDashboardLoadingOverlay() {
  window.dispatchEvent(new Event(loadingEndEvent));
}

export function DashboardLoadingOverlay() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [visible, setVisible] = useState(false);
  const [message, setMessage] = useState("Loading dashboard data");
  const showTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const maxTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => {
    return () => {
      clearTimer(showTimer);
      clearTimer(maxTimer);
    };
  }, []);

  useEffect(() => {
    hideNow();
  }, [pathname, searchParams]);

  useEffect(() => {
    function show(event: Event) {
      const customEvent = event as CustomEvent<{ message?: string; immediate?: boolean }>;
      setMessage(customEvent.detail?.message ?? "Loading dashboard data");
      clearTimer(showTimer);
      clearTimer(maxTimer);

      if (customEvent.detail?.immediate) {
        setVisible(true);
        maxTimer.current = setTimeout(hideNow, maxVisibleMs);
        return;
      }

      showTimer.current = setTimeout(() => {
        setVisible(true);
        maxTimer.current = setTimeout(hideNow, maxVisibleMs);
      }, showDelayMs);
    }

    window.addEventListener(loadingStartEvent, show);
    window.addEventListener(loadingEndEvent, hideNow);

    return () => {
      window.removeEventListener(loadingStartEvent, show);
      window.removeEventListener(loadingEndEvent, hideNow);
    };
  }, []);

  function hideNow() {
    clearTimer(showTimer);
    clearTimer(maxTimer);
    setVisible(false);
  }

  if (!visible) {
    return null;
  }

  return (
    <div
      aria-busy="true"
      aria-live="polite"
      className="fixed inset-0 z-[100] grid place-items-center bg-background/70 px-4 backdrop-blur-[2px]"
      role="status"
    >
      <div className="absolute inset-x-0 top-0 h-1 overflow-hidden bg-transparent">
        <div className="h-full w-1/3 animate-[dashboard-loading-bar_1.15s_ease-in-out_infinite] rounded-r-full bg-primary" />
      </div>
      <div className="flex min-h-14 min-w-72 items-center justify-center gap-3 rounded-lg border border-border bg-white px-5 py-4 text-sm font-medium text-foreground shadow-2xl">
        <Loader2 className="h-5 w-5 animate-spin text-primary" aria-hidden="true" />
        {message}
      </div>
    </div>
  );
}

function clearTimer(timer: MutableRefObject<ReturnType<typeof setTimeout> | undefined>) {
  if (timer.current) {
    clearTimeout(timer.current);
    timer.current = undefined;
  }
}
