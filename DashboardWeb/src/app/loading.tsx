export default function Loading() {
  return (
    <main className="mx-auto grid min-h-screen w-full max-w-[1540px] auto-rows-max content-start gap-5 px-4 py-4 sm:px-6 sm:py-5 lg:px-8">
      <section className="rounded-lg border border-border bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
          <div className="flex items-start gap-3">
            <div className="h-12 w-12 rounded-lg bg-slate-100" />
            <div className="grid gap-3">
              <div className="h-7 w-64 max-w-[65vw] rounded-md bg-slate-100" />
              <div className="flex gap-2">
                <div className="h-7 w-20 rounded-md bg-slate-100" />
                <div className="h-7 w-36 rounded-md bg-slate-100" />
              </div>
              <div className="h-4 w-72 max-w-[70vw] rounded bg-slate-100" />
            </div>
          </div>
          <div className="h-10 w-10 rounded-lg bg-slate-100" />
        </div>
        <div className="mt-4 border-t border-border pt-4">
          <div className="flex flex-wrap gap-2">
            {Array.from({ length: 4 }, (_, index) => (
              <div className="h-9 w-24 rounded-md bg-slate-100" key={index} />
            ))}
          </div>
          <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
            {Array.from({ length: 5 }, (_, index) => (
              <div className="h-10 rounded-lg bg-slate-100" key={index} />
            ))}
          </div>
        </div>
      </section>

      <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => (
          <div className="h-32 rounded-lg border border-border bg-white p-4 shadow-sm" key={index}>
            <div className="h-4 w-28 rounded bg-slate-100" />
            <div className="mt-4 h-6 w-40 rounded bg-slate-100" />
            <div className="mt-3 h-4 w-52 rounded bg-slate-100" />
          </div>
        ))}
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        {Array.from({ length: 4 }, (_, index) => (
          <div className="h-80 rounded-lg border border-border bg-white p-5 shadow-sm" key={index}>
            <div className="h-5 w-44 rounded bg-slate-100" />
            <div className="mt-3 h-4 w-64 max-w-full rounded bg-slate-100" />
            <div className="mt-8 h-52 rounded-lg bg-slate-100" />
          </div>
        ))}
      </section>
    </main>
  );
}
