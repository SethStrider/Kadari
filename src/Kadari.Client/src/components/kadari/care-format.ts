const longDate = new Intl.DateTimeFormat("en-ZA", { dateStyle: "long" });

/** A Care date (yyyy-MM-dd) the way an owner reads it — "14 March 2028" — never only a countdown (CARE/05 §5). */
export function formatCareDate(date: string | null | undefined) {
  return date ? longDate.format(new Date(`${date}T00:00:00`)) : null;
}
