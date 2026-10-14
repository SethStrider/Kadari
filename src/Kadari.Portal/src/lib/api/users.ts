import { apiClient } from "./client";
import type { components } from "./schema";

/**
 * Staff user administration (PRS-001 §3): list accounts and grant/revoke roles. Roles are additive — these
 * are set operations, never a replace. The role travels as a route segment by NAME (the API binds it by
 * name, not ordinal), so a client passes the role name string.
 */

export type UserSummary = components["schemas"]["UserSummaryDto"];

/**
 * The roles an administrator assigns from the portal. PublicVisitor is implicit (never granted); Customer is
 * established by the platform on sign-up. These are the operational/staff roles worth toggling by hand.
 */
export const ASSIGNABLE_ROLES = [
  { name: "SystemAdministrator", label: "System admin" },
  { name: "CustomerSuccess", label: "Customer success" },
  { name: "Valuer", label: "Valuer" },
  { name: "Insurer", label: "Insurer" },
] as const;

export type AssignableRole = (typeof ASSIGNABLE_ROLES)[number]["name"];

export function listUsers(search: string, page: number, pageSize: number) {
  // Page/PageSize/Descending are required by PageRequest binding; Search is optional.
  return apiClient.get("/api/v1/admin/users", {
    query: {
      Page: page,
      PageSize: pageSize,
      Descending: true,
      Search: search.trim() ? search.trim() : undefined,
    },
  });
}

export function assignRole(userId: string, role: string) {
  return apiClient.put("/api/v1/admin/users/{id}/roles/{role}", {
    path: { id: userId, role },
  });
}

export function removeRole(userId: string, role: string) {
  return apiClient.delete("/api/v1/admin/users/{id}/roles/{role}", {
    path: { id: userId, role },
  });
}

/** Suspend / lock / re-activate an account. Status travels by NAME in the route (Active|Suspended|Locked). */
export function setUserStatus(userId: string, status: string) {
  return apiClient.put("/api/v1/admin/users/{id}/status/{status}", {
    path: { id: userId, status },
  });
}
