import { AuthProvider } from "@/components/AuthProvider";
import { ThemeProvider } from "@/components/ThemeProvider";
import NavBar from "@/components/NavBar";
import "./globals.css";

export const metadata = {
  title: "PursuitHQ",
  description: "Track your courses, assignments, internships, and career goals.",
};

/**
 * Applies the saved theme before the page paints.
 *
 * React cannot do this: its first render happens after the browser has already
 * drawn, so a dark-mode user would get a white flash on every page load. This
 * runs synchronously in <head>, before anything is painted. It is wrapped in
 * try/catch because localStorage throws outright in some privacy modes, and a
 * theme preference is not worth a blank page.
 */
const themeScript = `
(function () {
  try {
    var saved = localStorage.getItem("pursuithq_theme") || "system";
    var dark = saved === "dark" ||
      (saved === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
    if (dark) document.documentElement.classList.add("dark");
  } catch (e) {}
})();
`;

export default function RootLayout({ children }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body className="min-h-screen bg-slate-50 text-slate-900 antialiased">
        <ThemeProvider>
          <AuthProvider>
            <NavBar />
            <main>{children}</main>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
