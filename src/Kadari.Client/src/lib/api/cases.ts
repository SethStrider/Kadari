import { apiClient } from "./client";
import type { components } from "./schema";

/** "Something's wrong" and its cases (CARE/13, CARE/14 C7–C11). Enum values are names. */

export type CaseSummary = components["schemas"]["CaseSummaryDto"];
export type CareCase = components["schemas"]["CaseDto"];
export type CaseCommitment = components["schemas"]["CaseCommitmentDto"];
export type CaseAppointment = components["schemas"]["CaseAppointmentDto"];

export const issueClasses = {
  Fault: "It stopped working or isn't working properly",
  DeadOnArrival: "It never worked — faulty out of the box",
  AccidentalDamage: "It was damaged (dropped, cracked, spilled on)",
} as const;
export type IssueClassName = keyof typeof issueClasses;

export const providerStatuses = {
  WaitingProvider: "Waiting for them",
  WaitingCustomer: "They're waiting for me",
  Collected: "Collected",
  Assessing: "Being assessed",
  RepairUnderway: "Repair underway",
  WaitingPart: "Waiting for a part",
  Ready: "Ready",
} as const;

export const outcomes = {
  Repaired: "Repaired",
  Replaced: "Replaced",
  Refunded: "Refunded",
  Credited: "Store credit",
  NoFaultFound: "No fault found",
  Rejected: "Claim rejected",
  ResolvedOther: "Sorted another way",
} as const;

const base =
  "/api/v1/kadari/customers/{customerId}/assets/{assetId}/care/cases";

export function reportIssue(
  customerId: string,
  assetId: string,
  issueClass: IssueClassName,
  description: string,
  ownerSaysUnsafe: boolean,
) {
  return apiClient.post(base, {
    path: { customerId, assetId },
    body: { issueClass, description, ownerSaysUnsafe },
  });
}

export function getCases(
  customerId: string,
  assetId: string,
  signal?: AbortSignal,
) {
  return apiClient.get(base, { path: { customerId, assetId }, signal });
}

export function getCase(
  customerId: string,
  assetId: string,
  caseId: string,
  signal?: AbortSignal,
) {
  return apiClient.get(`${base}/{caseId}`, {
    path: { customerId, assetId, caseId },
    signal,
  });
}

export function acceptCaseRoute(
  customerId: string,
  assetId: string,
  caseId: string,
) {
  return apiClient.post(`${base}/{caseId}/route/accept`, {
    path: { customerId, assetId, caseId },
  });
}

/**
 * Record that the owner sent the case pack. The idempotency key makes a retried click record one handoff; keep it for
 * the life of the screen.
 */
export async function markCaseSent(
  customerId: string,
  assetId: string,
  caseId: string,
  termsVersion: string,
  idempotencyKey: string,
): Promise<CareCase> {
  const response = await apiClient.fetch(
    `/api/v1/kadari/customers/${customerId}/assets/${assetId}/care/cases/${caseId}/sent`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Idempotency-Key": idempotencyKey,
      },
      body: JSON.stringify({
        acceptedTermsVersion: termsVersion,
        explicitConsent: true,
      }),
    },
  );
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
    } | null;
    throw new Error(
      problem?.detail ??
        `Could not record that it was sent (${response.status}).`,
    );
  }
  return (await response.json()) as CareCase;
}

export function addCaseReference(
  customerId: string,
  assetId: string,
  caseId: string,
  party: string,
  reference: string,
) {
  return apiClient.post(`${base}/{caseId}/references`, {
    path: { customerId, assetId, caseId },
    body: { party, reference },
  });
}

export function recordProviderUpdate(
  customerId: string,
  assetId: string,
  caseId: string,
  status: string,
  note?: string,
) {
  return apiClient.post(`${base}/{caseId}/updates`, {
    path: { customerId, assetId, caseId },
    body: { status, note: note || null },
  });
}

export function addCaseCommitment(
  customerId: string,
  assetId: string,
  caseId: string,
  party: "Provider" | "Owner",
  description: string,
  dueOn: string,
) {
  return apiClient.post(`${base}/{caseId}/commitments`, {
    path: { customerId, assetId, caseId },
    body: { party, description, dueOn },
  });
}

export function keepCaseCommitment(
  customerId: string,
  assetId: string,
  caseId: string,
  commitmentId: string,
) {
  return apiClient.post(`${base}/{caseId}/commitments/{commitmentId}/kept`, {
    path: { customerId, assetId, caseId, commitmentId },
  });
}

export function bookCaseAppointment(
  customerId: string,
  assetId: string,
  caseId: string,
  startsAt: string,
  location?: string,
) {
  return apiClient.post(`${base}/{caseId}/appointments`, {
    path: { customerId, assetId, caseId },
    body: {
      startsAt,
      endsAt: null,
      location: location || null,
      instructions: null,
    },
  });
}

export function settleCaseAppointment(
  customerId: string,
  assetId: string,
  caseId: string,
  appointmentId: string,
  done: boolean,
) {
  return apiClient.post(
    `${base}/{caseId}/appointments/{appointmentId}/settle`,
    {
      path: { customerId, assetId, caseId, appointmentId },
      body: { done },
    },
  );
}

export function resolveCase(
  customerId: string,
  assetId: string,
  caseId: string,
  outcome: string,
  notes?: string,
) {
  return apiClient.post(`${base}/{caseId}/resolve`, {
    path: { customerId, assetId, caseId },
    body: { outcome, notes: notes || null },
  });
}

export function withdrawCase(
  customerId: string,
  assetId: string,
  caseId: string,
) {
  return apiClient.post(`${base}/{caseId}/withdraw`, {
    path: { customerId, assetId, caseId },
  });
}
