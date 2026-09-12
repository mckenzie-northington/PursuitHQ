'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  courses as coursesApi,
  folders as foldersApi,
  materials as materialsApi,
  notes as notesApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import FilePreview from "@/components/FilePreview";

export default function MaterialsPage() {
  const { id } = useParams();
  const courseId = Number(id);
  const { user, loading } = useAuth();

  const [course, setCourse] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const [trail, setTrail] = useState([{ id: null, name: "All materials" }]);
  const currentFolderId = trail[trail.length - 1].id;

  const [subfolders, setSubfolders] = useState([]);
  const [allFolders, setAllFolders] = useState([]);
  const [files, setFiles] = useState([]);
  const [noteList, setNoteList] = useState([]);
  const [usage, setUsage] = useState(null);

  const [newFolderName, setNewFolderName] = useState("");
  const [showNewFolder, setShowNewFolder] = useState(false);
  const [uploading, setUploading] = useState(false);
  const fileInput = useRef(null);

  // Drag state. dragDepth counts enter/leave events, because dragging over a
  // child element fires dragleave on the parent and would otherwise flicker.
  const [dragging, setDragging] = useState(false);
  const dragDepth = useRef(0);
  const [draggedFile, setDraggedFile] = useState(null);
  const [dropTargetFolder, setDropTargetFolder] = useState(null);

  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const searching = debouncedSearch.trim().length > 0;

  const [previewing, setPreviewing] = useState(null);
  const [movingFile, setMovingFile] = useState(null);

  const [openNote, setOpenNote] = useState(null);
  const [noteDraft, setNoteDraft] = useState({ title: "", content: "" });
  const [savingNote, setSavingNote] = useState(false);

  // Wait for a pause in typing before querying, so a 10-character search is
  // one request instead of ten.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const refresh = useCallback(async () => {
    try {
      const term = debouncedSearch.trim();

      const [f, all, m, n, u] = await Promise.all([
        // While searching, results span the whole course, so the folder
        // listing for the current level is not shown.
        term ? Promise.resolve([]) : foldersApi.list(courseId, currentFolderId),
        foldersApi.listAll(courseId),
        materialsApi.list(courseId, term ? null : currentFolderId, term || null),
        notesApi.list(courseId, term ? null : currentFolderId, term || null),
        materialsApi.usage(),
      ]);
      setSubfolders(f);
      setAllFolders(all);
      setFiles(m);
      setNoteList(n);
      setUsage(u);
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [courseId, currentFolderId, debouncedSearch]);

  useEffect(() => {
    if (loading || !user) return;
    coursesApi.get(courseId).then(setCourse).catch((e) => setError(e.message));
  }, [loading, user, courseId]);

  useEffect(() => {
    if (loading || !user) return;
    refresh();
  }, [loading, user, refresh]);

  // ---------- uploads ----------

  async function uploadFiles(list) {
    const chosen = Array.from(list || []);
    if (chosen.length === 0) return;

    setUploading(true);
    setError("");

    const failures = [];
    for (const file of chosen) {
      try {
        await materialsApi.upload(courseId, file, currentFolderId, null);
      } catch (err) {
        failures.push(`${file.name}: ${err.message}`);
      }
    }

    setUploading(false);
    if (failures.length > 0) setError(failures.join(" · "));
    if (fileInput.current) fileInput.current.value = "";
    await refresh();
  }

  // ---------- drag and drop ----------

  function onDragEnter(e) {
    // Only react to files coming from outside the page, not to a file row
    // being dragged around inside it.
    if (draggedFile) return;
    if (!e.dataTransfer?.types?.includes("Files")) return;
    e.preventDefault();
    dragDepth.current += 1;
    setDragging(true);
  }

  function onDragLeave(e) {
    if (draggedFile) return;
    e.preventDefault();
    dragDepth.current -= 1;
    if (dragDepth.current <= 0) {
      dragDepth.current = 0;
      setDragging(false);
    }
  }

  function onDragOver(e) {
    if (draggedFile) return;
    if (!e.dataTransfer?.types?.includes("Files")) return;
    e.preventDefault();
  }

  async function onDrop(e) {
    if (draggedFile) return;
    e.preventDefault();
    dragDepth.current = 0;
    setDragging(false);
    await uploadFiles(e.dataTransfer.files);
  }

  async function dropOnFolder(folder) {
    if (!draggedFile) return;
    const file = draggedFile;
    setDraggedFile(null);
    setDropTargetFolder(null);

    try {
      await materialsApi.move(courseId, file, folder.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function moveFile(file, folderId) {
    try {
      await materialsApi.move(courseId, file, folderId);
      setMovingFile(null);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  // ---------- folders, notes ----------

  function openFolder(folder) {
    setTrail((t) => [...t, { id: folder.id, name: folder.name }]);
  }

  function jumpTo(index) {
    setTrail((t) => t.slice(0, index + 1));
  }

  async function createFolder(e) {
    e.preventDefault();
    if (!newFolderName.trim()) return;
    try {
      await foldersApi.create(courseId, {
        name: newFolderName.trim(),
        parentFolderId: currentFolderId,
      });
      setNewFolderName("");
      setShowNewFolder(false);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function deleteFolder(folder) {
    if (!confirm(`Delete "${folder.name}" and everything inside it? This cannot be undone.`)) return;
    try {
      await foldersApi.remove(courseId, folder.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function deleteFile(file) {
    if (!confirm(`Delete "${file.fileName}"?`)) return;
    try {
      await materialsApi.remove(courseId, file.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function saveNote(e) {
    e.preventDefault();
    setSavingNote(true);
    try {
      if (openNote === "new") {
        await notesApi.create(courseId, { ...noteDraft, folderId: currentFolderId });
      } else {
        await notesApi.update(courseId, openNote.id, { ...noteDraft, folderId: openNote.folderId });
      }
      setOpenNote(null);
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setSavingNote(false);
    }
  }

  async function editNote(note) {
    try {
      const full = await notesApi.get(courseId, note.id);
      setOpenNote(full);
      setNoteDraft({ title: full.title, content: full.content });
    } catch (err) {
      setError(err.message);
    }
  }

  async function deleteNote(note) {
    if (!confirm(`Delete "${note.title}"?`)) return;
    try {
      await notesApi.remove(courseId, note.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-5xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const isEmpty = subfolders.length === 0 && files.length === 0 && noteList.length === 0;

  return (
    <div
      className="relative mx-auto max-w-5xl px-6 py-10"
      onDragEnter={onDragEnter}
      onDragLeave={onDragLeave}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      {/* Drop overlay for files coming from the desktop */}
      {dragging && (
        <div className="pointer-events-none fixed inset-0 z-40 flex items-center justify-center bg-indigo-600/10 backdrop-blur-[1px]">
          <div className="rounded-2xl border-2 border-dashed border-indigo-500 bg-white px-10 py-8 text-center shadow-lg">
            <p className="text-lg font-semibold text-indigo-700">Drop files to upload</p>
            <p className="mt-1 text-sm text-slate-600">
              into {trail[trail.length - 1].name}
            </p>
          </div>
        </div>
      )}

      <Link href="/courses" className="text-sm font-medium text-indigo-600 hover:underline">
        &larr; Courses
      </Link>

      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{course?.name ?? "Study materials"}</h1>
          <p className="mt-1 text-sm text-slate-600">
            Drag files in to upload. Drag a file onto a folder to move it.
          </p>
        </div>

        {usage && (
          <div className="text-right text-sm text-slate-500">
            <p>{formatBytes(usage.usedBytes)} of {formatBytes(usage.quotaBytes)} used</p>
            <div className="mt-1 h-1.5 w-40 overflow-hidden rounded-full bg-slate-200">
              <div
                className="h-full bg-indigo-500"
                style={{ width: `${Math.min(100, (usage.usedBytes / usage.quotaBytes) * 100)}%` }}
              />
            </div>
          </div>
        )}
      </div>

      <nav className="mt-6 flex flex-wrap items-center gap-1 text-sm">
        {trail.map((crumb, i) => (
          <span key={`${crumb.id}-${i}`} className="flex items-center gap-1">
            {i > 0 && <span className="text-slate-400">/</span>}
            {i === trail.length - 1 ? (
              <span className="font-medium text-slate-900">{crumb.name}</span>
            ) : (
              <button onClick={() => jumpTo(i)} className="text-indigo-600 hover:underline">
                {crumb.name}
              </button>
            )}
          </span>
        ))}
      </nav>

      <div className="mt-4 flex flex-wrap gap-2">
        <button
          onClick={() => fileInput.current?.click()}
          disabled={uploading}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {uploading ? "Uploading..." : "Upload files"}
        </button>
        <input
          ref={fileInput}
          type="file"
          multiple
          onChange={(e) => uploadFiles(e.target.files)}
          className="hidden"
        />

        <button
          onClick={() => setShowNewFolder(!showNewFolder)}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          New folder
        </button>

        <button
          onClick={() => {
            setOpenNote("new");
            setNoteDraft({ title: "", content: "" });
          }}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          New note
        </button>
      </div>

      <p className="mt-2 text-xs text-slate-500">
        PDF, Word, PowerPoint, Excel, text, and images. 25 MB per file.
      </p>

      {showNewFolder && (
        <form onSubmit={createFolder} className="mt-3 flex gap-2">
          <input
            autoFocus
            value={newFolderName}
            onChange={(e) => setNewFolderName(e.target.value)}
            placeholder="Folder name"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />
          <button type="submit" className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">
            Create
          </button>
        </form>
      )}

      <div className="relative mt-5">
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search files and notes in this course..."
          className="w-full rounded-md border border-slate-300 py-2 pl-9 pr-9 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
        />
        <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
          &#128269;
        </span>
        {search && (
          <button
            onClick={() => setSearch("")}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-slate-400 hover:text-slate-700"
            title="Clear search"
          >
            &times;
          </button>
        )}
      </div>

      {searching && (
        <p className="mt-2 text-sm text-slate-600">
          {files.length + noteList.length} result{files.length + noteList.length === 1 ? "" : "s"} for
          &ldquo;{debouncedSearch}&rdquo; across the whole course
        </p>
      )}

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">Dismiss</button>
        </div>
      )}

      {openNote && (
        <form onSubmit={saveNote} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-medium">{openNote === "new" ? "New note" : "Edit note"}</h2>

          <input
            required
            value={noteDraft.title}
            onChange={(e) => setNoteDraft({ ...noteDraft, title: e.target.value })}
            placeholder="Title"
            className="mt-3 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />

          <textarea
            rows={10}
            value={noteDraft.content}
            onChange={(e) => setNoteDraft({ ...noteDraft, content: e.target.value })}
            placeholder="Write your notes here..."
            className="mt-3 w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />

          <div className="mt-4 flex gap-2">
            <button type="submit" disabled={savingNote} className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {savingNote ? "Saving..." : "Save note"}
            </button>
            <button type="button" onClick={() => setOpenNote(null)} className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
              Cancel
            </button>
          </div>
        </form>
      )}

      <div className="mt-6 overflow-hidden rounded-xl border border-slate-200 bg-white">
        {isEmpty ? (
          <div className="px-6 py-16 text-center">
            {searching ? (
              <>
                <p className="text-sm text-slate-600">
                  Nothing matches &ldquo;{debouncedSearch}&rdquo;.
                </p>
                <button
                  onClick={() => setSearch("")}
                  className="mt-2 text-sm font-medium text-indigo-600 hover:underline"
                >
                  Clear search
                </button>
              </>
            ) : (
              <>
                <p className="text-sm text-slate-600">This folder is empty.</p>
                <p className="mt-1 text-sm text-slate-500">Drag files here, or use the buttons above.</p>
              </>
            )}
          </div>
        ) : (
          <ul className="divide-y divide-slate-200">
            {/* Folders - also drop targets for moving files */}
            {subfolders.map((f) => (
              <li
                key={`folder-${f.id}`}
                onDragOver={(e) => {
                  if (!draggedFile) return;
                  e.preventDefault();
                  setDropTargetFolder(f.id);
                }}
                onDragLeave={() => setDropTargetFolder((cur) => (cur === f.id ? null : cur))}
                onDrop={(e) => {
                  if (!draggedFile) return;
                  e.preventDefault();
                  e.stopPropagation();
                  dropOnFolder(f);
                }}
                className={`flex items-center justify-between gap-4 px-4 py-3 transition ${
                  dropTargetFolder === f.id
                    ? "bg-indigo-50 ring-2 ring-inset ring-indigo-400"
                    : "hover:bg-slate-50"
                }`}
              >
                <button onClick={() => openFolder(f)} className="flex min-w-0 items-center gap-3 text-left">
                  <span className="text-lg">📁</span>
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-slate-900">{f.name}</span>
                    <span className="text-xs text-slate-500">
                      {f.subfolderCount} folders · {f.fileCount} files · {f.noteCount} notes
                    </span>
                  </span>
                </button>
                <button onClick={() => deleteFolder(f)} className="shrink-0 text-sm text-slate-400 hover:text-red-600">
                  Delete
                </button>
              </li>
            ))}

            {/* Files - draggable, clickable to preview */}
            {files.map((f) => (
              <li
                key={`file-${f.id}`}
                draggable
                onDragStart={() => setDraggedFile(f)}
                onDragEnd={() => {
                  setDraggedFile(null);
                  setDropTargetFolder(null);
                }}
                className={`flex items-center justify-between gap-4 px-4 py-3 transition hover:bg-slate-50 ${
                  draggedFile?.id === f.id ? "opacity-40" : ""
                }`}
              >
                <button
                  onClick={() => setPreviewing(f)}
                  className="flex min-w-0 cursor-pointer items-center gap-3 text-left"
                  title="Click to preview"
                >
                  <span className="text-lg">{iconFor(f.fileName)}</span>
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-slate-900">{f.fileName}</span>
                    <span className="text-xs text-slate-500">
                      {formatBytes(f.sizeBytes)} · {new Date(f.uploadedAt).toLocaleDateString()}
                      {searching && ` · in ${folderNameFor(f.folderId, allFolders)}`}
                    </span>
                  </span>
                </button>

                <div className="flex shrink-0 items-center gap-3 text-sm">
                  <button onClick={() => setPreviewing(f)} className="font-medium text-indigo-600 hover:underline">
                    Preview
                  </button>

                  {movingFile?.id === f.id ? (
                    <select
                      autoFocus
                      value=""
                      onChange={(e) => {
                        const chosen = e.target.value;
                        if (!chosen) return;
                        moveFile(f, chosen === "root" ? null : Number(chosen));
                      }}
                      onBlur={() => setMovingFile(null)}
                      className="rounded-md border border-slate-300 px-2 py-1 text-sm"
                    >
                      {/*
                        "root" rather than "" for the top level. Both the
                        placeholder and this option used to be value="", so the
                        select was already sitting on that value and choosing it
                        fired no change event at all - moving a file back to the
                        top level silently did nothing.
                      */}
                      <option value="" disabled>Move to...</option>
                      {f.folderId != null && <option value="root">All materials (root)</option>}
                      {allFolders
                        .filter((folder) => folder.id !== f.folderId)
                        .map((folder) => (
                          <option key={folder.id} value={folder.id}>{folder.name}</option>
                        ))}
                    </select>
                  ) : (
                    <button onClick={() => setMovingFile(f)} className="text-slate-500 hover:text-slate-900">
                      Move
                    </button>
                  )}

                  <button
                    onClick={() => materialsApi.download(courseId, f.id, f.fileName)}
                    className="text-slate-500 hover:text-slate-900"
                  >
                    Download
                  </button>
                  <button onClick={() => deleteFile(f)} className="text-slate-400 hover:text-red-600">
                    Delete
                  </button>
                </div>
              </li>
            ))}

            {noteList.map((n) => (
              <li key={`note-${n.id}`} className="flex items-center justify-between gap-4 px-4 py-3 hover:bg-slate-50">
                <button onClick={() => editNote(n)} className="flex min-w-0 items-center gap-3 text-left">
                  <span className="text-lg">📝</span>
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-slate-900">{n.title}</span>
                    <span className="text-xs text-slate-500">
                      Note · updated {new Date(n.updatedAt).toLocaleDateString()}
                      {searching && ` · in ${folderNameFor(n.folderId, allFolders)}`}
                    </span>
                  </span>
                </button>
                <button onClick={() => deleteNote(n)} className="shrink-0 text-sm text-slate-400 hover:text-red-600">
                  Delete
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {previewing && (
        <FilePreview
          courseId={courseId}
          file={previewing}
          onClose={() => setPreviewing(null)}
          onDownload={() => materialsApi.download(courseId, previewing.id, previewing.fileName)}
        />
      )}
    </div>
  );
}

function folderNameFor(folderId, allFolders) {
  if (!folderId) return "All materials";
  return allFolders.find((f) => f.id === folderId)?.name ?? "a folder";
}

function formatBytes(bytes) {
  if (!bytes) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  return `${(bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

function iconFor(fileName) {
  const ext = fileName.split(".").pop()?.toLowerCase();
  if (ext === "pdf") return "📄";
  if (["png", "jpg", "jpeg", "gif"].includes(ext)) return "🖼️";
  if (["docx", "doc"].includes(ext)) return "📃";
  if (["pptx", "ppt"].includes(ext)) return "📊";
  if (["xlsx", "xls", "csv"].includes(ext)) return "📈";
  return "📎";
}
