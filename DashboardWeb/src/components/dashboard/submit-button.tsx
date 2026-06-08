"use client";

import { useFormStatus } from "react-dom";
import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";

export function DashboardSubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button aria-busy={pending} disabled={pending} type="submit">
      <RefreshCw className="h-4 w-4" aria-hidden="true" />
      Update
    </Button>
  );
}
