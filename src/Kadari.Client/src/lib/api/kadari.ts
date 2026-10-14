import { apiClient } from "./client";
import { ApiProblem, type ApiProblemBody } from "./problem";
import type { components } from "./schema";
import { apiBaseUrl, authHeaders } from "./raw";
import type { DetectedObject } from "../vision/types";

/**
 * Kadari API calls. The snap upload is multipart, which the typed {@link ApiClient} (JSON-only) does not
 * do, so these use `fetch` directly — reusing the same base URL and bearer token. Once the Kadari
 * endpoints are added to `openapi/v1.json`, the ledger read can move onto the typed client; the multipart
 * upload stays here.
 */

export interface AnalyzeSnapResponse {
  snapId: string;
  enriched: boolean;
  assetIds: string[];
}

export interface LedgerAsset {
  assetId: string;
  displayName: string;
  category: string;
  assurance: string;
  bestValueMinor: number | null;
  status: string;
}

export interface LedgerRoom {
  room: string;
  roomTotalMinor: number;
  assets: LedgerAsset[];
}

export interface Ledger {
  customerId: string;
  assetCount: number;
  recordedReplacementExposureMinor: number;
  currency: string;
  completenessPercent: number;
  rooms: LedgerRoom[];
}

/** Upload a captured still plus its on-device detections; the server enriches and registers the assets. */
export async function analyzeSnap(
  customerId: string,
  image: Blob,
  detections: DetectedObject[],
  roomLabel: string | undefined,
  signal?: AbortSignal,
): Promise<AnalyzeSnapResponse> {
  const form = new FormData();
  form.append("image", image, "snap.jpg");
  form.append("detections", JSON.stringify(detections));
  if (roomLabel) {
    form.append("roomLabel", roomLabel);
  }

  const response = await fetch(
    `${apiBaseUrl()}/api/v1/kadari/customers/${customerId}/snaps`,
    {
      method: "POST",
      headers: authHeaders(), // no Content-Type — the browser sets the multipart boundary
      credentials: "include",
      body: form,
      signal,
    },
  );

  if (!response.ok) {
    // Structured, so the capture screen can tell a missing consent (`consent-required`) from a failure.
    const body = (await response
      .json()
      .catch(() => null)) as ApiProblemBody | null;
    throw new ApiProblem(
      body ?? {
        type: "about:blank",
        title: `Snap analysis failed (${response.status}).`,
        status: response.status,
      },
    );
  }
  return (await response.json()) as AnalyzeSnapResponse;
}

export async function getLedger(
  customerId: string,
  signal?: AbortSignal,
): Promise<Ledger> {
  const response = await fetch(
    `${apiBaseUrl()}/api/v1/kadari/customers/${customerId}/ledger`,
    {
      method: "GET",
      headers: { Accept: "application/json", ...authHeaders() },
      credentials: "include",
      signal,
    },
  );

  if (!response.ok) {
    throw new Error(`Failed to load the ledger (${response.status}).`);
  }
  return (await response.json()) as Ledger;
}

/** The insurer white-label for this customer's app — nulls when they are consumer-direct. */
export function getBranding(customerId: string, signal?: AbortSignal) {
  return apiClient.get("/api/v1/kadari/customers/{customerId}/branding", {
    path: { customerId },
    signal,
  });
}

export type KadariConsent = components["schemas"]["KadariConsentDto"];

/** The API binds this enum by ordinal (no string-enum converter); `scope` comes back as the name. */
export const consentScopes = {
  AssetImageProcessing: 0,
  CrossBorderAiEnrichment: 1,
} as const;
export type ConsentScopeName = keyof typeof consentScopes;

export function getConsents(customerId: string, signal?: AbortSignal) {
  return apiClient.get("/api/v1/kadari/customers/{customerId}/consents", {
    path: { customerId },
    signal,
  });
}

export function grantConsent(customerId: string, scope: ConsentScopeName) {
  return apiClient.post("/api/v1/kadari/customers/{customerId}/consents", {
    path: { customerId },
    body: { scope: consentScopes[scope] },
  });
}

export function withdrawConsent(customerId: string, scope: ConsentScopeName) {
  return apiClient.post(
    "/api/v1/kadari/customers/{customerId}/consents/withdraw",
    { path: { customerId }, body: { scope: consentScopes[scope] } },
  );
}

export type AssetDetail = components["schemas"]["AssetDetailDto"];
export type AssetPassport = components["schemas"]["AssetPassportDto"];

export function getAsset(
  customerId: string,
  assetId: string,
  signal?: AbortSignal,
) {
  return apiClient.get(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}",
    {
      path: { customerId, assetId },
      signal,
    },
  );
}

/** The asset's passport, or null while none has been minted (the API answers 404 until then). */
export async function getAssetPassport(
  customerId: string,
  assetId: string,
  signal?: AbortSignal,
): Promise<AssetPassport | null> {
  try {
    return await apiClient.get(
      "/api/v1/kadari/customers/{customerId}/assets/{assetId}/passport",
      { path: { customerId, assetId }, signal },
    );
  } catch (error) {
    if (error instanceof ApiProblem && error.status === 404) return null;
    throw error;
  }
}

export function mintAssetPassport(customerId: string, assetId: string) {
  return apiClient.post(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}/passport",
    { path: { customerId, assetId } },
  );
}

export interface OwnershipProof {
  receipt?: File;
  serialNumber?: string;
  amountMinor?: number;
  currency?: string;
}

/** Multipart (a receipt file), so — like the snap upload — this bypasses the JSON-only typed client. */
export async function recordOwnershipProof(
  customerId: string,
  assetId: string,
  proof: OwnershipProof,
): Promise<{ assetId: string; assurance: string }> {
  const form = new FormData();
  if (proof.receipt) form.append("receipt", proof.receipt);
  if (proof.serialNumber) form.append("serialNumber", proof.serialNumber);
  if (proof.amountMinor !== undefined) {
    form.append("amountMinor", String(proof.amountMinor));
    form.append("currency", proof.currency ?? "ZAR");
  }

  const response = await fetch(
    `${apiBaseUrl()}/api/v1/kadari/customers/${customerId}/assets/${assetId}/proof`,
    {
      method: "POST",
      headers: authHeaders(),
      credentials: "include",
      body: form,
    },
  );

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
    } | null;
    throw new Error(
      problem?.detail ?? `Could not record the proof (${response.status}).`,
    );
  }
  return (await response.json()) as { assetId: string; assurance: string };
}

export type AssetPassportVerification =
  components["schemas"]["AssetPassportVerificationDto"];

/** Public, no-login check of an Asset Passport by its AP- id; null when no such passport exists. */
export async function verifyAssetPassport(
  verificationId: string,
  signal?: AbortSignal,
): Promise<AssetPassportVerification | null> {
  try {
    return await apiClient.get(
      "/api/v1/verify/asset-passports/{verificationId}",
      { path: { verificationId }, signal, anonymous: true },
    );
  } catch (error) {
    if (error instanceof ApiProblem && error.status === 404) return null;
    throw error;
  }
}

/** The public page a passport's QR code and share link open. */
export function assetPassportVerifyUrl(verificationId: string): string {
  return `${window.location.origin}/kadari/verify/${encodeURIComponent(verificationId)}`;
}
