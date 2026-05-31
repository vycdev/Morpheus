import DashboardPage from "@/app/page";

type SearchParams = Record<string, string | string[] | undefined>;

export default async function UserDashboardPage({
  params,
  searchParams,
}: {
  params: Promise<{ userId: string }> | { userId: string };
  searchParams?: Promise<SearchParams> | SearchParams;
}) {
  const resolvedParams = await Promise.resolve(params);
  const resolvedSearchParams = await Promise.resolve(searchParams ?? {});

  return (
    <DashboardPage
      searchParams={{
        ...resolvedSearchParams,
        scope: "user",
        userId: resolvedParams.userId,
      }}
    />
  );
}
