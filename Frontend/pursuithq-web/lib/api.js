// ---------------------------------------------------------------------------
// The single place the website talks to the API.
//
// Every page calls these helpers instead of using fetch directly, so the token
// handling and error handling live in one file rather than being repeated.
// ---------------------------------------------------------------------------

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5051";

const TOKEN_KEY = "pursuithq_token";
const USER_KEY = "pursuithq_user";

// --- token storage ---------------------------------------------------------
// NOTE: localStorage is convenient but readable by any script on the page.
// Before PursuitHQ opens to other people, this should move to an httpOnly
// cookie (see docs/Security.md). Fine for local development.

export function getToken() {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function setSession(token, user) {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function getStoredUser() {
  if (typeof window === "undefined") return null;
  const raw = localStorage.getItem(USER_KEY);
  return raw ? JSON.parse(raw) : null;
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

// --- the request helper ----------------------------------------------------

export class ApiError extends Error {
  constructor(message, status, details) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

async function request(path, { method = "GET", body } = {}) {
  const headers = { "Content-Type": "application/json" };

  const token = getToken();
  if (token) {
    // This is what proves who you are to the API on every request.
    headers.Authorization = `Bearer ${token}`;
  }

  let res;
  try {
    res = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new ApiError(
      "Could not reach the API. Is it running on " + API_URL + "?",
      0
    );
  }

  if (res.status === 204) return null;

  let data = null;
  const text = await res.text();
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  if (!res.ok) {
    // The API returns { error, message, details } - see docs/ApiDesign.md
    const message = data?.message || `Request failed (${res.status})`;
    throw new ApiError(message, res.status, data?.details);
  }

  return data;
}

export const api = {
  get: (path) => request(path),
  post: (path, body) => request(path, { method: "POST", body }),
  put: (path, body) => request(path, { method: "PUT", body }),
  del: (path) => request(path, { method: "DELETE" }),
};

// --- feature helpers -------------------------------------------------------

export const auth = {
  register: (data) => api.post("/api/auth/register", data),
  login: (data) => api.post("/api/auth/login", data),
  me: () => api.get("/api/auth/me"),
};

export const courses = {
  list: (semester) =>
    api.get(semester ? `/api/courses?semester=${encodeURIComponent(semester)}` : "/api/courses"),
  get: (id) => api.get(`/api/courses/${id}`),
  create: (data) => api.post("/api/courses", data),
  update: (id, data) => api.put(`/api/courses/${id}`, data),
  remove: (id) => api.del(`/api/courses/${id}`),
  schedules: {
    create: (courseId, data) => api.post(`/api/courses/${courseId}/schedules`, data),
    remove: (courseId, id) => api.del(`/api/courses/${courseId}/schedules/${id}`),
  },
};

export const assignments = {
  list: (params = {}) => {
    const q = new URLSearchParams();
    if (params.courseId) q.set("courseId", params.courseId);
    if (params.status !== undefined && params.status !== "") q.set("status", params.status);
    const qs = q.toString();
    return api.get(qs ? `/api/assignments?${qs}` : "/api/assignments");
  },
  create: (data) => api.post("/api/assignments", data),
  update: (id, data) => api.put(`/api/assignments/${id}`, data),
  remove: (id) => api.del(`/api/assignments/${id}`),
};
