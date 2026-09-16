'use client';

import {
  RESUME_FONTS,
  RESUME_SIZES,
  RESUME_LEADING,
  normaliseStyle,
} from "@/lib/resumeFonts";

/**
 * Typeface, size and line spacing for the resume sheet.
 *
 * Sits above the preview rather than in the editor column: these change how the
 * page looks, not what it says, and the only way to judge them is to watch the
 * preview move while you change them.
 */
export default function FormatBar({ style, onChange }) {
  const current = normaliseStyle(style);

  const control =
    "rounded-md border border-slate-300 bg-white px-2 py-1 text-xs text-slate-700 outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  const groups = [...new Set(RESUME_FONTS.map((font) => font.group))];

  return (
    <div className="print-hide mb-2 flex flex-wrap items-center gap-2">
      <label className="sr-only" htmlFor="resume-font">
        Font
      </label>
      <select
        id="resume-font"
        value={current.font}
        onChange={(e) => onChange({ font: e.target.value })}
        className={control}
      >
        {groups.map((group) => (
          <optgroup key={group} label={group}>
            {RESUME_FONTS.filter((font) => font.group === group).map((font) => (
              // Each option previews itself. Naming a typeface tells you almost
              // nothing if you have not seen it set.
              <option key={font.id} value={font.id} style={{ fontFamily: font.stack }}>
                {font.label}
              </option>
            ))}
          </optgroup>
        ))}
      </select>

      <label className="sr-only" htmlFor="resume-size">
        Text size
      </label>
      <select
        id="resume-size"
        value={current.size}
        onChange={(e) => onChange({ size: Number(e.target.value) })}
        className={control}
      >
        {RESUME_SIZES.map((size) => (
          <option key={size} value={size}>
            {size} pt
          </option>
        ))}
      </select>

      <label className="sr-only" htmlFor="resume-leading">
        Line spacing
      </label>
      <select
        id="resume-leading"
        value={current.leading}
        onChange={(e) => onChange({ leading: Number(e.target.value) })}
        className={control}
      >
        {RESUME_LEADING.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label} spacing
          </option>
        ))}
      </select>

      <span className="text-xs text-slate-400">Affects the PDF too</span>
    </div>
  );
}
