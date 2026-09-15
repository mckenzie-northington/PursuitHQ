"""
Typeface, size and line spacing for the resume.

No migration: Resume.Content is already a JSON column, so the style rides along
inside it. A resume saved before this existed deserialises with Style null and
picks up the defaults when it opens.

Same rules as the other patch scripts: refuses to write a file if any anchor is
missing, preserves line endings and the BOM.

Run from the repo root:  python patch_fonts.py
"""

import sys
from pathlib import Path

ROOT = Path(".")


def edit(relative, replacements):
    path = ROOT / relative
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


# ------------------------------------------------------------------- back end

edit("Backend/PursuitHQ.API/DTOs/Resumes/ResumeDtos.cs", [
    (
        "        public List<string> Layout { get; set; } = new();\n"
        "    }",

        "        public List<string> Layout { get; set; } = new();\n"
        "\n"
        "        /// <summary>\n"
        "        /// How the sheet is set: typeface, text size, line spacing.\n"
        "        ///\n"
        "        /// Null on every resume saved before this existed, which is why the\n"
        "        /// editor fills it in rather than the database doing it. Kept inside the\n"
        "        /// content JSON rather than given columns of its own because it is only\n"
        "        /// ever read and written whole, alongside the content it formats.\n"
        "        /// </summary>\n"
        "        public ResumeStyleDto? Style { get; set; }\n"
        "    }\n"
        "\n"
        "    /// <summary>\n"
        "    /// Presentation only. Nothing here reaches the AI: the checker is sent the\n"
        "    /// words, and what font they are in is none of its business.\n"
        "    /// </summary>\n"
        "    public class ResumeStyleDto\n"
        "    {\n"
        "        /// <summary>A key from RESUME_FONTS in the website, not a CSS stack.</summary>\n"
        "        [MaxLength(40)]\n"
        "        public string? Font { get; set; }\n"
        "\n"
        "        /// <summary>Points. Range matches what the editor will accept.</summary>\n"
        "        [Range(8, 14)]\n"
        "        public double? Size { get; set; }\n"
        "\n"
        "        [Range(1, 2)]\n"
        "        public double? Leading { get; set; }\n"
        "    }"
    ),
])

# ------------------------------------------------------------------ front end

PAGE = "Frontend/pursuithq-web/app/resume/[id]/page.js"

edit(PAGE, [
    (
        "import { useAuth } from \"@/components/AuthProvider\";",

        "import { useAuth } from \"@/components/AuthProvider\";\n"
        "import FormatBar from \"@/components/resume/FormatBar\";\n"
        "import { styleVars, normaliseStyle } from \"@/lib/resumeFonts\";"
    ),
    (
        "  // An empty layout means this resume predates arranging, not that every\n"
        "  // section was removed - so it gets the default order, not a blank page.\n"
        "  c.layout = layout.length > 0 ? layout : known;\n"
        "\n"
        "  return c;",

        "  // An empty layout means this resume predates arranging, not that every\n"
        "  // section was removed - so it gets the default order, not a blank page.\n"
        "  c.layout = layout.length > 0 ? layout : known;\n"
        "\n"
        "  // Missing on anything written before the format controls existed, and\n"
        "  // checked rather than trusted even when present - see normaliseStyle.\n"
        "  c.style = normaliseStyle(c.style);\n"
        "\n"
        "  return c;"
    ),
    (
        "          <p className=\"print-hide mb-2 text-xs font-medium uppercase tracking-wide text-slate-400\">\n"
        "            Preview\n"
        "          </p>\n"
        "          <Sheet content={content} />",

        "          <p className=\"print-hide mb-2 text-xs font-medium uppercase tracking-wide text-slate-400\">\n"
        "            Preview\n"
        "          </p>\n"
        "\n"
        "          <FormatBar\n"
        "            style={content.style}\n"
        "            onChange={(patch) =>\n"
        "              edit((c) => {\n"
        "                c.style = { ...c.style, ...patch };\n"
        "                return c;\n"
        "              })\n"
        "            }\n"
        "          />\n"
        "\n"
        "          <Sheet content={content} />"
    ),
    (
        "function Sheet({ content }) {\n"
        "  const { contact } = content;",

        "/**\n"
        " * The sheet's own type scale.\n"
        " *\n"
        " * Tailwind's text sizes are fixed rem values, so with them left alone the size\n"
        " * control would move the body text and leave the name, headings and dates\n"
        " * behind. Restating them in em, scoped to the sheet, makes the whole document\n"
        " * scale from one number. Unlayered on purpose: plain CSS outranks anything in\n"
        " * a cascade layer, which is where Tailwind's utilities live.\n"
        " */\n"
        "const SHEET_CSS = `\n"
        ".resume-sheet {\n"
        "  font-family: var(--resume-font);\n"
        "  font-size: var(--resume-size);\n"
        "  line-height: var(--resume-leading);\n"
        "}\n"
        ".resume-sheet .text-2xl { font-size: 1.72em; line-height: 1.2; }\n"
        ".resume-sheet .text-lg  { font-size: 1.15em; line-height: 1.25; }\n"
        ".resume-sheet .text-base { font-size: 1em; line-height: var(--resume-leading); }\n"
        ".resume-sheet .text-sm  { font-size: 0.93em; line-height: var(--resume-leading); }\n"
        ".resume-sheet .text-xs  { font-size: 0.82em; line-height: var(--resume-leading); }\n"
        ".resume-sheet .leading-snug { line-height: var(--resume-leading); }\n"
        "`;\n"
        "\n"
        "function Sheet({ content }) {\n"
        "  const { contact } = content;"
    ),
    (
        "    <div className=\"resume-sheet rounded-xl border border-slate-200 bg-white p-8 text-slate-900 shadow-sm\">",

        "    <div\n"
        "      className=\"resume-sheet rounded-xl border border-slate-200 bg-white p-8 text-slate-900 shadow-sm\"\n"
        "      style={styleVars(content.style)}\n"
        "    >\n"
        "      <style>{SHEET_CSS}</style>\n"
    ),
])

print("\nAdd resumeFonts.js and FormatBar.js, then: dotnet build && npm run dev")
