import { AuthProvider } from "@/components/AuthProvider";
import { ThemeProvider } from "@/components/ThemeProvider";
import NavBar from "@/components/NavBar";
import TourGuide from "@/components/TourGuide";
import "./globals.css";

export const metadata = {
  title: "PursuitHQ",
  description: "Track your courses, assignments, internships, and career goals.",
};

/**
 * Stated rather than inherited.
 *
 * Without width=device-width a phone renders the page at about 980px and then
 * shrinks the picture, so every layout built for a narrow screen is thrown away
 * and all the text arrives too small to read. maximumScale is left alone on
 * purpose: blocking zoom is a real accessibility problem for anyone who needs
 * to enlarge something, and it buys nothing.
 */
export const viewport = {
  width: "device-width",
  initialScale: 1,
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

            {/* Inside AuthProvider, and outside <main>, so it survives moving
                between pages - which is the whole trick: the walkthrough
                navigates, and a card that unmounted on every route change
                could not. */}
            <TourGuide />
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
