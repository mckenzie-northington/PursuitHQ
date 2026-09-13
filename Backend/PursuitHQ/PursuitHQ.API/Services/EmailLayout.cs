using System.Net;
using System.Text;

namespace PursuitHQ.API.Services
{
    /// <summary>
    /// The shell every PursuitHQ email is rendered into.
    ///
    /// One place, so the footer cannot be forgotten. Every email these reminders
    /// send is about someone's coursework and carries assignment titles and
    /// course names, so every one of them has to say who it is from and offer a
    /// one-click way to stop them. That is not politeness - a reminder people
    /// cannot switch off is spam, whatever it is about.
    ///
    /// Inline styles and a table-free layout on purpose: email clients strip
    /// stylesheets, and anything clever degrades into a mess in Outlook.
    /// </summary>
    public static class EmailLayout
    {
        /// <param name="showPreferences">
        /// False for mail you cannot opt out of - a password reset is sent
        /// because someone asked for it, and offering to "change what you get
        /// emailed about" underneath implies a setting that does not and should
        /// not exist. Reminders are the opposite: those must always carry it.
        /// </param>
        public static string Html(
            string heading, string bodyHtml, string appUrl, bool showPreferences = true)
        {
            var settings = $"{appUrl.TrimEnd('/')}/settings";

            var sb = new StringBuilder();

            sb.Append("<div style=\"font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;")
              .Append("max-width:560px;margin:0 auto;padding:24px;color:#0f172a;line-height:1.5\">");

            sb.Append("<h1 style=\"font-size:18px;margin:0 0 16px\">")
              .Append(WebUtility.HtmlEncode(heading))
              .Append("</h1>");

            sb.Append(bodyHtml);

            sb.Append("<hr style=\"border:none;border-top:1px solid #e2e8f0;margin:28px 0 12px\">");

            sb.Append("<p style=\"font-size:12px;color:#64748b;margin:0\">")
              .Append("Sent by PursuitHQ.");

            if (showPreferences)
            {
                sb.Append(' ')
                  .Append("<a href=\"").Append(settings).Append("\" style=\"color:#4f46e5\">")
                  .Append("Change what you get emailed about</a>.");
            }

            sb.Append("</p>");

            sb.Append("</div>");

            return sb.ToString();
        }

        /// <summary>
        /// The plain-text half. Not optional: some clients show it, some people
        /// prefer it, and a message with no text part is more likely to be
        /// treated as spam.
        /// </summary>
        public static string Text(
            string heading, string body, string appUrl, bool showPreferences = true)
        {
            var sb = new StringBuilder();

            sb.AppendLine(heading);
            sb.AppendLine(new string('-', heading.Length));
            sb.AppendLine();
            sb.AppendLine(body.Trim());
            sb.AppendLine();
            sb.AppendLine("--");
            sb.AppendLine("Sent by PursuitHQ.");

            if (showPreferences)
            {
                sb.AppendLine($"Change what you get emailed about: {appUrl.TrimEnd('/')}/settings");
            }

            return sb.ToString();
        }
    }
}
