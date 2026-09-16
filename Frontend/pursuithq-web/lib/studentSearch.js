/**
 * Filtering an already-loaded list of people by a typed query.
 *
 * Client side on purpose: your connections are all in hand, and a round trip
 * per keystroke to filter a list the browser already holds would be slower and
 * worse. Searching for people you are *not* connected to is a different job and
 * stays on the server, where the privacy rules live.
 */
export function matchesStudent(student, query) {
  const needle = query.trim().toLowerCase();
  if (!needle) return true;

  return [
    student.firstName,
    student.lastName,
    `${student.firstName} ${student.lastName}`,
    student.school,
    student.email,
  ]
    .filter(Boolean)
    .some((field) => field.toLowerCase().includes(needle));
}

export function filterStudents(students, query) {
  return students.filter((student) => matchesStudent(student, query));
}
