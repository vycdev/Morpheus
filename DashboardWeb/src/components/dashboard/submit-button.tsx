"use client";

import { useEffect } from "react";
import { useFormStatus } from "react-dom";
import { RefreshCw } from "lucide-react";
import { hideDashboardLoadingOverlay, showDashboardLoadingOverlay } from "@/components/dashboard/loading-overlay";
import { Button } from "@/components/ui/button";

export function DashboardSubmitButton() {
  const { pending } = useFormStatus();

  useEffect(() => {
    if (pending) {
      showDashboardLoadingOverlay("Updating dashboard data");
      return;
    }

    hideDashboardLoadingOverlay();
  }, [pending]);

  return (
    <Button aria-busy={pending} disabled={pending} type="submit">
      <RefreshCw className="h-4 w-4" aria-hidden="true" />
      Update
    </Button>
  );
}
