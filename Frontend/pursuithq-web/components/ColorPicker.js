'use client';

import { useState } from "react";
import { EVENT_COLORS } from "@/lib/calendar";

/**
 * A row of color swatches: the built-in colors, then any the student has added
 * of their own, then a + to add another.
 *
 * The built-in colors are always there and cannot be removed - they are the
 * app's, not the student's. Added colors sit beside them, and each carries a
 * small x to delete it.
 *
 * Adding is deliberately two steps - open the picker, then press Add. A native
 * color input fires a change event continuously while you drag around the
 * color wheel, so saving on change would post a dozen half-chosen colors to
 * the API on the way to the one you actually wanted.
 */
export default function ColorPicker({ value, onChange, saved = [], onSave, onRemove }) {
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState(value || "#0ea5e9");

  const current = (value || "").toLowerCase();
  const builtIn = EVENT_COLORS.map((c) => c.toLowerCase());
  const mine = saved.map((c) => c.toLowerCase()).filter((c) => !builtIn.includes(c));

  const draftValue = draft.toLowerCase();
  const alreadyHave = builtIn.includes(draftValue) || mine.includes(draftValue);

  function startAdding() {
    setDraft(value || "#0ea5e9");
    setAdding(true);
  }

  function confirmAdd() {
    onSave?.(draftValue);
    onChange(draftValue);
    setAdding(false);
  }

  return (
    <div className="mt-1.5">
      <div className="flex flex-wrap items-center gap-2">
        {builtIn.map((color) => (
          <Swatch key={color} color={color} selected={current === color} onSelect={onChange} />
        ))}

        {mine.map((color) => (
          <Swatch
            key={color}
            color={color}
            selected={current === color}
            onSelect={onChange}
            onRemove={onRemove}
          />
        ))}

        {onSave && !adding && (
          <button
            type="button"
            onClick={startAdding}
            title="Add a color"
            aria-label="Add a color"
            className="flex h-6 w-6 items-center justify-center rounded-full border border-dashed border-slate-400 text-sm leading-none text-slate-500 transition hover:border-slate-600 hover:text-slate-700"
          >
            +
          </button>
        )}
      </div>

      {adding && (
        <div className="mt-2 flex flex-wrap items-center gap-2 rounded-md border border-slate-200 bg-slate-50 p-2">
          <input
            type="color"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            aria-label="Pick a color"
            className="h-7 w-10 cursor-pointer rounded border border-slate-300 bg-white"
          />
          <span className="font-mono text-xs text-slate-500">{draftValue}</span>

          <button
            type="button"
            onClick={confirmAdd}
            disabled={alreadyHave}
            title={alreadyHave ? "You already have this color" : "Add to your colors"}
            className="rounded-md bg-indigo-600 px-3 py-1 text-xs font-medium text-white transition hover:bg-indigo-700 disabled:opacity-40"
          >
            {alreadyHave ? "Already added" : "Add"}
          </button>
          <button
            type="button"
            onClick={() => setAdding(false)}
            className="rounded-md border border-slate-300 px-3 py-1 text-xs font-medium text-slate-600 transition hover:bg-white"
          >
            Cancel
          </button>
        </div>
      )}
    </div>
  );
}

function Swatch({ color, selected, onSelect, onRemove }) {
  return (
    <span className="group relative inline-flex">
      <button
        type="button"
        onClick={() => onSelect(color)}
        style={{ backgroundColor: color }}
        aria-label={`Use ${color}`}
        aria-pressed={selected}
        className={`h-6 w-6 rounded-full transition ${
          selected ? "ring-2 ring-slate-900 ring-offset-2" : "hover:scale-110"
        }`}
      />

      {onRemove && (
        <button
          type="button"
          onClick={() => onRemove(color)}
          aria-label={`Remove ${color} from your colors`}
          title="Remove this color"
          className="absolute -right-1 -top-1 hidden h-3.5 w-3.5 items-center justify-center rounded-full border border-slate-300 bg-white text-[8px] leading-none text-slate-600 shadow-sm hover:bg-red-50 hover:text-red-600 group-hover:flex"
        >
          &#10005;
        </button>
      )}
    </span>
  );
}
