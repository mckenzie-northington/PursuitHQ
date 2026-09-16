'use client';

import { useEffect, useRef, useState } from "react";

/**
 * A small emoji picker, written rather than installed.
 *
 * The npm pickers are around a megabyte and pull in their own data files and
 * image sprites. This is a few hundred characters of text rendered by the
 * system font, which is what emoji already are - and it means one less
 * dependency to keep current. People can still use their own keyboard picker
 * (Win + .) and type emoji straight into the box; this is for the ones who do
 * not know that shortcut exists.
 */
const GROUPS = [
  {
    name: "Smileys",
    emoji: "🙂",
    items: "😀 😃 😄 😁 😆 😅 🤣 😂 🙂 🙃 😉 😊 😇 🥰 😍 😘 😗 😚 😋 😛 😜 🤪 🤨 🧐 🤓 😎 🥳 😏 😒 😞 😔 😟 😕 🙁 😣 😖 😫 😩 🥺 😢 😭 😤 😠 😡 🤯 😳 🥵 🥶 😱 😨 😰 😥 😓 🤗 🤔 🤭 🤫 😬 🙄 😴 🤤 😪 😵 🤐 🥴 🤢 🤮 🤧 😷 🤒 🤕",
  },
  {
    name: "Gestures",
    emoji: "👍",
    items: "👍 👎 👌 ✌️ 🤞 🤟 🤘 🤙 👈 👉 👆 👇 ☝️ ✋ 🤚 🖐️ 🖖 👋 🤝 🙏 ✊ 👊 🤛 🤜 👏 🙌 👐 🤲 💪 🦾 ✍️ 💅 👀 🧠 🫶",
  },
  {
    name: "People",
    emoji: "🎓",
    items: "🎓 👨‍🎓 👩‍🎓 🧑‍🏫 👨‍💻 👩‍💻 🧑‍🔬 🧑‍⚕️ 🧑‍🍳 🕺 💃 🧘 🏃 🚶 🤦 🤷 🙋 🙆 🙅 💁 🧑‍🤝‍🧑 👥 👤",
  },
  {
    name: "Nature",
    emoji: "🌿",
    items: "🐶 🐱 🐭 🐹 🐰 🦊 🐻 🐼 🐨 🐯 🦁 🐮 🐷 🐵 🐔 🐧 🐦 🦆 🦉 🦋 🐛 🐝 🐢 🐍 🐙 🐬 🐳 🦈 🌸 🌼 🌻 🌹 🌷 🌱 🌿 🍀 🍁 🍂 🌲 🌴 🌵 ⭐ 🌟 ✨ ⚡ 🔥 💧 🌈 ☀️ 🌤️ ☁️ 🌧️ ⛈️ ❄️ ⛄ 🌙",
  },
  {
    name: "Food",
    emoji: "🍕",
    items: "🍏 🍎 🍐 🍊 🍋 🍌 🍉 🍇 🍓 🫐 🍒 🍑 🥭 🍍 🥥 🥝 🍅 🥑 🥦 🥕 🌽 🥔 🍠 🥐 🍞 🥖 🧀 🥚 🍳 🥞 🧇 🥓 🍔 🍟 🍕 🌭 🥪 🌮 🌯 🥗 🍝 🍜 🍲 🍣 🍤 🍚 🍛 🍦 🍩 🍪 🎂 🍰 🧁 🍫 🍬 🍿 ☕ 🍵 🧋 🥤 🧃",
  },
  {
    name: "Activity",
    emoji: "⚽",
    items: "⚽ 🏀 🏈 ⚾ 🥎 🎾 🏐 🏉 🥏 🎱 🏓 🏸 🥅 🏒 🏑 🏏 ⛳ 🏹 🎣 🥊 🥋 🎽 🛹 🛼 🛷 ⛸️ 🎿 ⛷️ 🏂 🏋️ 🤸 🤺 🤾 🏌️ 🏇 🧗 🚴 🚵 🏆 🥇 🥈 🥉 🎖️ 🎬 🎤 🎧 🎼 🎹 🥁 🎸 🎺 🎻 🎲 ♟️ 🎯 🎮 🎨",
  },
  {
    name: "Travel",
    emoji: "✈️",
    items: "🚗 🚕 🚙 🚌 🚎 🏎️ 🚓 🚑 🚒 🚐 🚚 🚛 🚜 🛵 🏍️ 🚲 🛴 🚏 🚉 🚆 🚄 🚇 ✈️ 🛫 🛬 🚀 🛸 🚁 ⛵ 🚤 🛳️ ⚓ 🗺️ 🗿 🗽 🗼 🏰 🏟️ 🎡 🎢 🏖️ 🏝️ 🏔️ ⛰️ 🌋 🏕️ 🏠 🏢 🏫 🏛️ ⛪ 🌃 🌇 🌉",
  },
  {
    name: "Objects",
    emoji: "📚",
    items: "⌚ 📱 💻 ⌨️ 🖥️ 🖨️ 🖱️ 💾 💿 📷 📹 🎥 📞 ☎️ 📟 📠 📺 📻 ⏰ ⏱️ ⌛ 🔋 🔌 💡 🔦 🕯️ 🧯 🛒 💰 💳 💸 🧾 ✉️ 📩 📦 📫 📝 📄 📃 📑 📊 📈 📉 📅 📆 🗓️ 📋 📌 📍 📎 🖇️ 📏 📐 ✂️ 🔒 🔑 🔨 🧰 🔬 🔭 📚 📖 📓 📒 📔 🖊️ ✏️ 🖍️",
  },
  {
    name: "Symbols",
    emoji: "❤️",
    items: "❤️ 🧡 💛 💚 💙 💜 🖤 🤍 🤎 💔 ❣️ 💕 💞 💓 💗 💖 💘 💝 ✅ ☑️ ✔️ ❌ ❎ ➕ ➖ ➗ ❓ ❔ ❗ ❕ 💯 🔔 🔕 ⚠️ 🚫 ♻️ 🔴 🟠 🟡 🟢 🔵 🟣 ⚫ ⚪ 🔺 🔻 ⭕ 🎉 🎊 🎁 🎈 🏁 🚩 🔥 💤 💬 👌",
  },
];

/** The handful offered straight away when reacting, before opening the picker. */
export const QUICK_REACTIONS = ["👍", "❤️", "😂", "🎉", "😮", "😢"];

export default function EmojiPicker({ onPick, onClose, align = "left" }) {
  const [group, setGroup] = useState(0);
  const box = useRef(null);

  // Escape closes it, and so does clicking anywhere outside - both of which
  // people try before looking for a close button.
  useEffect(() => {
    function onKey(e) {
      if (e.key === "Escape") onClose();
    }

    function onDown(e) {
      if (box.current && !box.current.contains(e.target)) onClose();
    }

    document.addEventListener("keydown", onKey);

    // Deferred a tick, or the click that opened this would immediately close it.
    const id = setTimeout(() => document.addEventListener("mousedown", onDown), 0);

    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("mousedown", onDown);
      clearTimeout(id);
    };
  }, [onClose]);

  return (
    <div
      ref={box}
      className={`absolute bottom-full z-30 mb-2 w-72 rounded-xl border border-slate-200 bg-white p-2 shadow-lg ${
        align === "right" ? "right-0" : "left-0"
      }`}
    >
      <div className="flex gap-0.5 border-b border-slate-200 pb-2">
        {GROUPS.map((item, i) => (
          <button
            key={item.name}
            type="button"
            title={item.name}
            onClick={() => setGroup(i)}
            className={`rounded-md px-1.5 py-1 text-base transition ${
              group === i ? "bg-indigo-50" : "hover:bg-slate-100"
            }`}
          >
            {item.emoji}
          </button>
        ))}
      </div>

      <div className="mt-2 grid max-h-48 grid-cols-8 gap-0.5 overflow-y-auto">
        {GROUPS[group].items.split(" ").map((emoji, i) => (
          <button
            key={`${emoji}-${i}`}
            type="button"
            onClick={() => onPick(emoji)}
            className="rounded-md py-1 text-xl transition hover:bg-slate-100"
          >
            {emoji}
          </button>
        ))}
      </div>
    </div>
  );
}
