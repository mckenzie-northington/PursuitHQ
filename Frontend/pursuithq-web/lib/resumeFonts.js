/**
 * Typography choices for the resume sheet.
 *
 * System fonts only, on purpose. A resume is a print document: the font that
 * matters is the one that renders when someone opens the PDF, and a stack that
 * resolves on the reader's own machine cannot fail to load, cannot flash, and
 * cannot quietly substitute something else halfway down page one. Every option
 * here is a face that ships with Windows, macOS or common Linux, with named
 * fallbacks in the same style so the shape survives even where the exact face
 * does not.
 *
 * If a modern web font is ever wanted - Inter, Source Serif - that is a real
 * option too, just a different trade: a network request, and a font that has to
 * be embedded in the PDF at export.
 */
export const RESUME_FONTS = [
  {
    id: "system",
    label: "System default",
    group: "Sans serif",
    stack: "ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif",
  },
  {
    id: "arial",
    label: "Arial",
    group: "Sans serif",
    stack: "Arial, 'Helvetica Neue', Helvetica, 'Liberation Sans', sans-serif",
  },
  {
    id: "calibri",
    label: "Calibri",
    group: "Sans serif",
    stack: "Calibri, Carlito, 'Segoe UI', Candara, sans-serif",
  },
  {
    id: "helvetica",
    label: "Helvetica",
    group: "Sans serif",
    stack: "'Helvetica Neue', Helvetica, Arial, 'Liberation Sans', sans-serif",
  },
  {
    id: "tahoma",
    label: "Tahoma",
    group: "Sans serif",
    stack: "Tahoma, Verdana, Geneva, 'DejaVu Sans', sans-serif",
  },
  {
    id: "verdana",
    label: "Verdana",
    group: "Sans serif",
    stack: "Verdana, Geneva, Tahoma, 'DejaVu Sans', sans-serif",
  },
  {
    id: "georgia",
    label: "Georgia",
    group: "Serif",
    stack: "Georgia, 'Times New Roman', Times, 'Liberation Serif', serif",
  },
  {
    id: "garamond",
    label: "Garamond",
    group: "Serif",
    stack: "Garamond, 'EB Garamond', 'Palatino Linotype', Palatino, 'Book Antiqua', serif",
  },
  {
    id: "cambria",
    label: "Cambria",
    group: "Serif",
    stack: "Cambria, Caladea, Georgia, 'Times New Roman', serif",
  },
  {
    id: "times",
    label: "Times New Roman",
    group: "Serif",
    stack: "'Times New Roman', Times, 'Liberation Serif', 'Nimbus Roman', serif",
  },
];

/** Half-point steps, because a resume that nearly fits usually needs half. */
export const RESUME_SIZES = [9, 9.5, 10, 10.5, 11, 11.5, 12];

export const RESUME_LEADING = [
  { value: 1.15, label: "Tight" },
  { value: 1.3, label: "Normal" },
  { value: 1.5, label: "Relaxed" },
];

/**
 * What a resume with no style saved gets.
 *
 * "system" rather than a named face so that every resume written before this
 * existed looks exactly as it did before, and only changes when someone chooses
 * to change it.
 */
export const DEFAULT_RESUME_STYLE = { font: "system", size: 10.5, leading: 1.3 };

export function findFont(id) {
  return RESUME_FONTS.find((font) => font.id === id) ?? RESUME_FONTS[0];
}

/**
 * Cleans up whatever was stored.
 *
 * A style comes out of a JSON column that older records do not have and that
 * nothing stops a future version from writing differently, so every field is
 * checked rather than trusted. A resume must still open if its style is
 * nonsense.
 */
export function normaliseStyle(raw) {
  const style = raw ?? {};

  const size = Number(style.size);
  const leading = Number(style.leading);

  return {
    font: RESUME_FONTS.some((f) => f.id === style.font)
      ? style.font
      : DEFAULT_RESUME_STYLE.font,

    // Clamped rather than matched to the list: a size typed in by hand or left
    // over from an older set of options is fine as long as it is printable.
    size: Number.isFinite(size) ? Math.min(14, Math.max(8, size)) : DEFAULT_RESUME_STYLE.size,

    leading: Number.isFinite(leading)
      ? Math.min(2, Math.max(1, leading))
      : DEFAULT_RESUME_STYLE.leading,
  };
}

/**
 * The style as CSS custom properties for the sheet element.
 *
 * Custom properties rather than direct font-size, because the sheet's headings
 * and small print are sized in em against this one value - so a single control
 * moves the whole document in proportion instead of resizing the body text and
 * leaving the name and dates behind.
 */
export function styleVars(style) {
  const clean = normaliseStyle(style);

  return {
    "--resume-font": findFont(clean.font).stack,
    "--resume-size": `${clean.size}pt`,
    "--resume-leading": String(clean.leading),
  };
}
