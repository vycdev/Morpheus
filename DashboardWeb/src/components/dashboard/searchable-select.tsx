"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import { ChevronDown, ChevronLeft, ChevronRight, Search } from "lucide-react";
import { cn } from "@/lib/utils";

export type SearchableSelectOption = {
  value: string;
  label: string;
  description?: string;
};

type SearchableSelectProps = {
  name: string;
  label: string;
  options: SearchableSelectOption[];
  value?: string;
  emptyOptionLabel?: string;
  placeholder: string;
  disabled?: boolean;
  pageSize?: number;
};

export function SearchableSelect({
  name,
  label,
  options,
  value,
  emptyOptionLabel,
  placeholder,
  disabled = false,
  pageSize = 10,
}: SearchableSelectProps) {
  const rootRef = useRef<HTMLDivElement>(null);
  const searchId = useId();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(0);
  const [selectedValue, setSelectedValue] = useState(value ?? "");

  useEffect(() => {
    setSelectedValue(value ?? "");
  }, [value]);

  useEffect(() => {
    if (!open) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  const allOptions = useMemo(() => {
    const baseOptions = emptyOptionLabel
      ? [{ value: "", label: emptyOptionLabel }, ...options]
      : options;

    return baseOptions;
  }, [emptyOptionLabel, options]);

  const filteredOptions = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    if (!normalizedQuery) {
      return allOptions;
    }

    return allOptions.filter((option) => {
      const labelText = option.label.toLowerCase();
      const valueText = option.value.toLowerCase();
      const descriptionText = option.description?.toLowerCase() ?? "";

      return (
        labelText.includes(normalizedQuery) ||
        valueText.includes(normalizedQuery) ||
        descriptionText.includes(normalizedQuery)
      );
    });
  }, [allOptions, query]);

  const pageCount = Math.max(1, Math.ceil(filteredOptions.length / pageSize));
  const safePage = Math.min(page, pageCount - 1);
  const visibleOptions = filteredOptions.slice(safePage * pageSize, safePage * pageSize + pageSize);
  const selectedOption = allOptions.find((option) => option.value === selectedValue);
  const start = filteredOptions.length === 0 ? 0 : safePage * pageSize + 1;
  const end = Math.min(filteredOptions.length, (safePage + 1) * pageSize);

  useEffect(() => {
    setPage(0);
  }, [query]);

  useEffect(() => {
    if (page > pageCount - 1) {
      setPage(pageCount - 1);
    }
  }, [page, pageCount]);

  function chooseOption(option: SearchableSelectOption) {
    setSelectedValue(option.value);
    setOpen(false);
    setQuery("");
  }

  return (
    <div className="relative grid gap-1 text-sm" ref={rootRef}>
      <span className="font-medium text-muted">{label}</span>
      <input name={name} readOnly type="hidden" value={selectedValue} />
      <button
        aria-expanded={open}
        className="flex h-10 min-w-56 items-center justify-between gap-3 rounded-lg border border-border bg-white px-3 text-left text-sm text-foreground shadow-sm outline-none transition-colors hover:border-primary focus:border-primary disabled:cursor-not-allowed disabled:opacity-50"
        disabled={disabled}
        onClick={() => setOpen((current) => !current)}
        type="button"
      >
        <span className={cn("min-w-0 truncate", !selectedOption && "text-muted")}>
          {selectedOption?.label ?? placeholder}
        </span>
        <ChevronDown className="h-4 w-4 shrink-0 text-muted" aria-hidden="true" />
      </button>

      {open && (
        <div className="absolute left-0 top-full z-50 mt-2 w-[min(28rem,calc(100vw-2rem))] rounded-lg border border-border bg-white p-2 shadow-xl">
          <label className="relative block" htmlFor={searchId}>
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" aria-hidden="true" />
            <input
              autoComplete="off"
              autoFocus
              className="h-10 w-full rounded-lg border border-border bg-white pl-9 pr-3 text-sm text-foreground outline-none focus:border-primary"
              id={searchId}
              onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                }
              }}
              placeholder={`Search ${label.toLowerCase()}`}
              type="search"
              value={query}
            />
          </label>

          <div className="mt-2 grid gap-1">
            {visibleOptions.length === 0 ? (
              <div className="rounded-md border border-dashed border-border p-4 text-center text-sm text-muted">
                No matches
              </div>
            ) : (
              visibleOptions.map((option) => {
                const selected = option.value === selectedValue;

                return (
                  <button
                    className={cn(
                      "grid min-h-10 rounded-md px-3 py-2 text-left transition-colors hover:bg-slate-50",
                      selected && "bg-blue-50 text-foreground",
                    )}
                    key={`${name}-${option.value || "empty"}`}
                    onClick={() => chooseOption(option)}
                    type="button"
                  >
                    <span className="truncate text-sm font-medium text-foreground">{option.label}</span>
                    {option.description && (
                      <span className="truncate text-xs text-muted">{option.description}</span>
                    )}
                  </button>
                );
              })
            )}
          </div>

          <div className="mt-2 flex items-center justify-between gap-3 border-t border-border pt-2 text-xs text-muted">
            <span>
              {start}-{end} of {filteredOptions.length}
            </span>
            <div className="flex items-center gap-1">
              <button
                className="inline-flex h-8 w-8 items-center justify-center rounded-md border border-border bg-white text-foreground disabled:cursor-not-allowed disabled:opacity-45"
                disabled={safePage === 0}
                onClick={() => setPage((current) => Math.max(0, current - 1))}
                type="button"
              >
                <ChevronLeft className="h-4 w-4" aria-hidden="true" />
                <span className="sr-only">Previous page</span>
              </button>
              <span className="min-w-16 text-center">
                {safePage + 1} / {pageCount}
              </span>
              <button
                className="inline-flex h-8 w-8 items-center justify-center rounded-md border border-border bg-white text-foreground disabled:cursor-not-allowed disabled:opacity-45"
                disabled={safePage >= pageCount - 1}
                onClick={() => setPage((current) => Math.min(pageCount - 1, current + 1))}
                type="button"
              >
                <ChevronRight className="h-4 w-4" aria-hidden="true" />
                <span className="sr-only">Next page</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
