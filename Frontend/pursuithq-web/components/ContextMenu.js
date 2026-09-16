'use client';

import { useEffect, useLayoutEffect, useRef, useState } from "react";

/**
 * A right-click menu, positioned where the click happened.
 *
 * Replaces the controls that used to appear on hover. Hover controls make a
 * message visibly change shape as the pointer crosses it, which is distracting
 * while reading and impossible on a touchscreen - a menu you ask for only
 * appears when you want it.
 *
 * Items are `{ label, onClick, danger }`, or the string "divider". `header` is
 * optional content pinned above them - the reaction row uses it, because
 * choosing an emoji is a different kind of act from choosing a command and
 * reading "React thumbs-up" as a line of text is nobody's idea of a reaction.
 */
export default function ContextMenu({ x, y, items, header, onClose }) {
  const box = useRef(null);
  const [position, setPosition] = useState({ left: x, top: y });

  // Measured after it renders, then nudged back inside the window. A menu
  // opened near the bottom right would otherwise run off the screen with its
  // last item - usually the destructive one - out of reach.
  useLayoutEffect(() => {
    const el = box.current;
    if (!el) return;

    const { width, height } = el.getBoundingClientRect();
    const margin = 8;

    setPosition({
      left: Math.min(x, window.innerWidth - width - margin),
      top: Math.min(y, window.innerHeight - height - margin),
    });
  }, [x, y]);

  useEffect(() => {
    function onKey(e) {
      if (e.key === "Escape") onClose();
    }

    /**
     * Closes only for a press that landed outside the menu.
     *
     * This has to be checked here, and cannot be left to stopPropagation on the
     * menu itself. Next's App Router hands React the whole document to hydrate,
     * so React's own listener and this one are both attached to `document` -
     * and stopPropagation does not stop another listener on the *same* node.
     * The old version therefore closed the menu on mousedown, the button was
     * gone before mouseup, no click event was ever produced, and every item did
     * nothing at all.
     */
    function onPressOutside(e) {
      if (box.current?.contains(e.target)) return;
      onClose();
    }

    // Anything that moves the page underneath makes the position wrong, so the
    // menu closes rather than floating somewhere meaningless.
    document.addEventListener("keydown", onKey);
    document.addEventListener("mousedown", onPressOutside);
    document.addEventListener("scroll", onClose, true);
    window.addEventListener("resize", onClose);

    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("mousedown", onPressOutside);
      document.removeEventListener("scroll", onClose, true);
      window.removeEventListener("resize", onClose);
    };
  }, [onClose]);

  return (
    <div
      ref={box}
      role="menu"
      style={{ left: position.left, top: position.top }}
      className="fixed z-50 min-w-44 rounded-lg border border-slate-200 bg-white py-1 shadow-xl"
    >
      {header && (
        <>
          {header}
          <div className="my-1 border-t border-slate-100" />
        </>
      )}

      {items.filter(Boolean).map((item, i) =>
        item === "divider" ? (
          <div key={`d${i}`} className="my-1 border-t border-slate-100" />
        ) : (
          <button
            key={item.label}
            role="menuitem"
            onClick={() => {
              onClose();
              item.onClick();
            }}
            className={`flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm transition hover:bg-slate-100 ${
              item.danger ? "text-red-600" : "text-slate-700"
            }`}
          >
            {item.emoji && <span className="text-base leading-none">{item.emoji}</span>}
            {item.label}
          </button>
        )
      )}
    </div>
  );
}
