import DashboardPage from "@/app/page";

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

  return (
    <DashboardPage
      searchParams={{
        ...resolvedSearchParams,
        guildId: resolvedParams.guildId,
        scope: "server",
      }}
    />
  );
}
