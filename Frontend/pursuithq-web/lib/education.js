/**
 * Education levels, matching the EducationLevel enum in the API.
 *
 * The numbers are the stored values, so they must line up with the C# enum and
 * must never be renumbered - only added to.
 */
export const EDUCATION_LEVELS = [
  { value: 0, label: "Prefer not to say" },
  { value: 1, label: "High school" },
  { value: 2, label: "College" },
  { value: 3, label: "University" },
  { value: 4, label: "Other" },
];

/** The label for a stored value, or "" for NotSet and anything unrecognised. */
export function educationLabel(value) {
  if (!value) return "";

  return EDUCATION_LEVELS.find((level) => level.value === Number(value))?.label ?? "";
}
