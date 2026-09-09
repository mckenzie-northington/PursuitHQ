'use client';

/**
 * The tick box on an assignment.
 *
 * It appears both on colored blocks (inside a calendar chip) and on white
 * (in the day list), so it takes an `onLight` flag rather than guessing.
 *
 * It is a <button>, not an <input type="checkbox">, because it sits inside
 * other clickable things and needs to stop the click from reaching them - an
 * input would still bubble, and ticking a box would also open the event
 * dialog underneath it.
 */
export default function AssignmentCheckbox({ checked, onChange, onLight = true, title }) {
  const base =
    "flex h-4 w-4 shrink-0 items-center justify-center rounded-[3px] border transition";

  const skin = checked
    ? onLight
      ? "border-green-600 bg-green-600 text-white"
      : "border-white/80 bg-white/90 text-slate-800"
    : onLight
    ? "border-slate-400 bg-white hover:border-green-600"
    : "border-white/70 bg-white/20 hover:bg-white/40";

  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={checked}
      title={title ?? (checked ? "Mark as not done" : "Mark as done")}
      onClick={(e) => {
        e.stopPropagation();
        onChange();
      }}
      className={`${base} ${skin}`}
    >
      {checked && (
        <svg viewBox="0 0 12 12" className="h-3 w-3" fill="none" stroke="currentColor" strokeWidth="2.5">
          <path d="M2.5 6.5l2.5 2.5 4.5-5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      )}
    </button>
  );
}
