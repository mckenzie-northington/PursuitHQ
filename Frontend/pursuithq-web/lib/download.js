/**
 * Saves generated text to the student's computer.
 *
 * Everything the study tools produce is already in the browser, so this needs
 * no round trip to the API - the text is wrapped in a blob and handed to a
 * throwaway link. The object URL is released straight after, or the blob stays
 * in memory for the life of the page.
 */
export function downloadText(text, fileName, type = "text/markdown") {
  const blob = new Blob([text], { type: `${type};charset=utf-8` });
  const url = URL.createObjectURL(blob);

  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;

  document.body.appendChild(link);
  link.click();
  link.remove();

  URL.revokeObjectURL(url);
}

/** Turns a title into something every filesystem will accept. */
export function safeFileName(title, extension) {
  const cleaned = (title || "untitled")
    .replace(/[\\/:*?"<>|]/g, "")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, 80);

  return `${cleaned || "untitled"}.${extension}`;
}
