"""
Front-end half of the time zone work.

Same rules as patch_tz.py: edits in place, refuses to write a file if any
anchor is missing, preserves line endings and the BOM exactly as found.

Run from the repo root:  python patch_tz_fe.py
"""

import sys
from pathlib import Path

WEB = Path("Frontend/pursuithq-web")


def edit(relative, replacements):
    path = WEB / relative
    raw = path.read_bytes()

    bom = raw.startswith(b"\xef\xbb\xbf")
    if bom:
        raw = raw[3:]

    crlf = b"\r\n" in raw
    text = raw.decode("utf-8").replace("\r\n", "\n")

    for old, new in replacements:
        if old not in text:
            sys.exit(f"ANCHOR NOT FOUND in {relative}:\n---\n{old}\n---")
        if text.count(old) != 1:
            sys.exit(f"ANCHOR NOT UNIQUE in {relative} ({text.count(old)}x):\n---\n{old}\n---")
        text = text.replace(old, new)

    out = text.replace("\n", "\r\n").encode("utf-8") if crlf else text.encode("utf-8")
    path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + out)
    print(f"patched {relative}  ({len(replacements)} edits, {'CRLF' if crlf else 'LF'}{', BOM' if bom else ''})")


# ------------------------------------------------------------------------ api
#
# Registration is the one moment where taking the device's word is right: there
# is no preference yet to override. Without this the API's default applies and
# every new account silently lands on Eastern.

edit("lib/api.js", [
    (
        "const API_URL = process.env.NEXT_PUBLIC_API_URL || \"http://localhost:5051\";",

        "import { browserZone } from \"@/lib/timezones\";\n"
        "\n"
        "const API_URL = process.env.NEXT_PUBLIC_API_URL || \"http://localhost:5051\";"
    ),
    (
        "  register: (data) => api.post(\"/api/auth/register\", data),",

        "  /**\n"
        "   * The browser knows the zone and the server cannot guess it, so it rides\n"
        "   * along with every sign-up. Spread second so an explicit choice wins.\n"
        "   */\n"
        "  register: (data) =>\n"
        "    api.post(\"/api/auth/register\", { timeZone: browserZone(), ...data }),\n"
        "\n"
        "  /** Only the zone. updateProfile would blank every field it was not given. */\n"
        "  updateTimeZone: (timeZone) => api.put(\"/api/auth/me/timezone\", { timeZone }),"
    ),
])

# ------------------------------------------------------------------- settings

edit("app/settings/page.js", [
    (
        "import { useTheme } from \"@/components/ThemeProvider\";",

        "import { useTheme } from \"@/components/ThemeProvider\";\n"
        "import TimeZoneSelect from \"@/components/TimeZoneSelect\";\n"
        "import TimeZoneNotice from \"@/components/TimeZoneNotice\";\n"
        "import { zoneLabel, DEFAULT_ZONE } from \"@/lib/timezones\";"
    ),
    (
        "// Where students of a US university actually are. A full tz list is 400 entries\n"
        "// of noise for a feature that only needs to get deadlines right.\n"
        "const TIME_ZONES = [\n"
        "  \"America/New_York\",\n"
        "  \"America/Chicago\",\n"
        "  \"America/Denver\",\n"
        "  \"America/Phoenix\",\n"
        "  \"America/Los_Angeles\",\n"
        "  \"America/Anchorage\",\n"
        "  \"Pacific/Honolulu\",\n"
        "  \"UTC\",\n"
        "];\n"
        "\n",

        ""
    ),
    (
        "          timeZone: me.timeZone || \"America/New_York\",",

        "          timeZone: me.timeZone || DEFAULT_ZONE,"
    ),
    (
        "            <select\n"
        "              value={profile.timeZone}\n"
        "              onChange={(e) => setProfile({ ...profile, timeZone: e.target.value })}\n"
        "              className={field}\n"
        "            >\n"
        "              {TIME_ZONES.map((zone) => (\n"
        "                <option key={zone} value={zone}>\n"
        "                  {zone.replace(\"_\", \" \")}\n"
        "                </option>\n"
        "              ))}\n"
        "            </select>",

        "            <TimeZoneSelect\n"
        "              value={profile.timeZone}\n"
        "              onChange={(zone) => setProfile({ ...profile, timeZone: zone })}\n"
        "              className={field}\n"
        "            />\n"
        "            <p className=\"mt-1 text-xs text-slate-500\">\n"
        "              Due dates and reminder times are read in this zone. It is where your\n"
        "              classes are, which is not always where you are.\n"
        "            </p>"
    ),
    (
        "      <p className=\"mt-1 text-sm text-slate-600\">\n"
        "        Your account and how PursuitHQ looks.\n"
        "      </p>\n",

        "      <p className=\"mt-1 text-sm text-slate-600\">\n"
        "        Your account and how PursuitHQ looks.\n"
        "      </p>\n"
        "\n"
        "      {/* Renders nothing unless the device and the saved zone disagree. */}\n"
        "      <div className=\"mt-6\">\n"
        "        <TimeZoneNotice />\n"
        "      </div>\n"
    ),
    (
        "              Times are in {notify.timeZone.replace(\"_\", \" \")}, taken from your time\n"
        "              zone above. Change it there and reminders follow.",

        "              Times are in {zoneLabel(notify.timeZone)}, taken from your time zone\n"
        "              above. Change it there and reminders follow."
    ),
])

# ---------------------------------------------------------------- assignments
#
# This page works overdue out itself rather than trusting the API's snapshot.
# It was doing that against the browser's clock, which is a different question
# from the one the server now answers - two answers that can disagree is worse
# than one that is wrong, so both sides read the saved zone.

edit("app/assignments/page.js", [
    (
        "import { useAuth } from \"@/components/AuthProvider\";",

        "import { useAuth } from \"@/components/AuthProvider\";\n"
        "import { wallClockNow } from \"@/lib/timezones\";"
    ),
    (
        "function useNow(intervalMs = 60_000) {\n"
        "  const [now, setNow] = useState(() => new Date());\n"
        "\n"
        "  useEffect(() => {\n"
        "    const id = setInterval(() => setNow(new Date()), intervalMs);\n"
        "    return () => clearInterval(id);\n"
        "  }, [intervalMs]);\n"
        "\n"
        "  return now;\n"
        "}",

        "function useNow(timeZone, intervalMs = 60_000) {\n"
        "  const [now, setNow] = useState(() => wallClockNow(timeZone));\n"
        "\n"
        "  useEffect(() => {\n"
        "    setNow(wallClockNow(timeZone));\n"
        "\n"
        "    const id = setInterval(() => setNow(wallClockNow(timeZone)), intervalMs);\n"
        "    return () => clearInterval(id);\n"
        "  }, [timeZone, intervalMs]);\n"
        "\n"
        "  return now;\n"
        "}"
    ),
    (
        "  const now = useNow();",

        "  // Due dates come back without an offset, so they read as wall clock. This\n"
        "  // has to be wall clock in the same zone or the comparison is meaningless.\n"
        "  const now = useNow(user?.timeZone);"
    ),
])

print("\nAdd the three new files, then: npm run dev")
