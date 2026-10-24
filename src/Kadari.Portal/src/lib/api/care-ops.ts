import { apiClient } from "./client";
import type { components } from "./schema";

/**
 * Care Ops (CARE/11): the queue of exceptions KADARI Care cannot safely finish on its own — unknown routes, safety
 * reviews, failed registrations, rule conflicts, stalled cases. Needs the CareOpsReview permission (Customer
 * Success). Enum values travel by name.
 */

export type OpsWorkItem = components["schemas"]["OpsWorkItemDto"];
export type OpsWorkItemDetail = components["schemas"]["OpsWorkItemDetailDto"];

export const WORK_TYPES: Record<string, string> = {
  SafetyReview: "Safety review",
  RouteUnknown: "Unknown route",
  RegistrationFailed: "Registration failed",
  RuleConflict: "Rule conflict",
  CaseStalled: "Case stalled",
};

export const RESOLUTIONS: Record<string, string> = {
  ReusableKnowledge: "Reusable registry knowledge",
  CaseSpecific: "Case-specific",
  PolicyReview: "Needs a policy review",
  ProductBug: "Product bug",
};

export const ROUTE_TYPES: Record<string, string> = {
  RetailerReturn: "Return to the store",
  RetailerCredit: "Store credit or swap",
  OemDirect: "Manufacturer directly",
  WarrantyAdmin: "Warranty administrator",
  AuthorisedService: "Authorised service centre",
  Onsite: "Technician on site",
  CarryIn: "Carry-in service centre",
  Collection: "Collection / courier",
  ProtectionProvider: "Protection plan provider",
};

export const CHANNEL_TYPES = [
  "WebPage",
  "Email",
  "Phone",
  "WhatsApp",
  "InStore",
] as const;

export function getOpsQueue(resolved: boolean, workType?: string) {
  return apiClient.get("/api/v1/kadari/care-ops/work-items", {
    query: { resolved, workType: workType || undefined },
  });
}

export function getOpsWorkItem(workItemId: string) {
  return apiClient.get("/api/v1/kadari/care-ops/work-items/{workItemId}", {
    path: { workItemId },
  });
}

export function takeOpsWorkItem(workItemId: string) {
  return apiClient.post(
    "/api/v1/kadari/care-ops/work-items/{workItemId}/take",
    {
      path: { workItemId },
    },
  );
}

export function resolveOpsWorkItem(
  workItemId: string,
  resolution: string,
  notes: string,
) {
  return apiClient.post(
    "/api/v1/kadari/care-ops/work-items/{workItemId}/resolve",
    {
      path: { workItemId },
      body: { resolution, notes },
    },
  );
}

export interface CaseRoute {
  routeType: string;
  targetName: string;
  channelType: string;
  channelValue: string | null;
  instructions: string;
  resolution: string;
  notes: string;
}

export function setCaseRoute(workItemId: string, route: CaseRoute) {
  return apiClient.post(
    "/api/v1/kadari/care-ops/work-items/{workItemId}/route",
    {
      path: { workItemId },
      body: route,
    },
  );
}
