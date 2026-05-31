import DashboardPage from "@/app/page";
import { notFound } from "next/navigation";

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
  const userId = parsePositiveInteger(resolvedParams.userId);

  if (!userId) {
    notFound();
  }

  return (
    <DashboardPage
      searchParams={{
        ...resolvedSearchParams,
        scope: "user",
        userId: String(userId),
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
