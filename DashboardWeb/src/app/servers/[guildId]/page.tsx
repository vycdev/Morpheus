import DashboardPage from "@/app/page";
import { notFound } from "next/navigation";

type SearchParams = Record<string, string | string[] | undefined>;

export default async function ServerDashboardPage({
  params,
  searchParams,
}: {
  params: Promise<{ guildId: string }> | { guildId: string };
  searchParams?: Promise<SearchParams> | SearchParams;
}) {
  const resolvedParams = await Promise.resolve(params);
  const resolvedSearchParams = await Promise.resolve(searchParams ?? {});
  const guildId = parsePositiveInteger(resolvedParams.guildId);

  if (!guildId) {
    notFound();
  }

  return (
    <DashboardPage
      searchParams={{
        ...resolvedSearchParams,
        guildId: String(guildId),
        scope: "server",
      }}
    />
  );
}

function parsePositiveInteger(value: string) {
  if (!/^[1-9]\d*$/.test(value)) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isSafeInteger(parsed) ? parsed : undefined;
}
