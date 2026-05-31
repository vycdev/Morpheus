import type { Metadata } from "next";
import { Suspense } from "react";
import { DashboardLoadingOverlay } from "@/components/dashboard/loading-overlay";
import "./globals.css";

export const metadata: Metadata = {
  title: "Morpheus Dashboard",
  description: "Operational dashboard for the Morpheus Discord bot",
  icons: {
    icon: "/morpheus.png",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <script
          dangerouslySetInnerHTML={{
            __html: `
              (() => {
                try {
                  const stored = localStorage.getItem("morpheus-dashboard-theme");
                  const theme = stored === "dark" || stored === "light"
                    ? stored
                    : (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
                  document.documentElement.dataset.theme = theme;
                  document.documentElement.classList.toggle("dark", theme === "dark");
                } catch {}
              })();
            `,
          }}
        />
        {children}
        <Suspense fallback={null}>
          <DashboardLoadingOverlay />
        </Suspense>
      </body>
    </html>
  );
}
