import { AuthProvider } from "@/components/AuthProvider";
import NavBar from "@/components/NavBar";
import "./globals.css";

export const metadata = {
  title: "PursuitHQ",
  description: "Track your courses, assignments, internships, and career goals.",
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-slate-50 text-slate-900 antialiased">
        <AuthProvider>
          <NavBar />
          <main>{children}</main>
        </AuthProvider>
      </body>
    </html>
  );
}
