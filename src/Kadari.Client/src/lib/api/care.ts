import { apiClient } from "./client";
import { ApiProblem } from "./problem";
import type { components } from "./schema";

/** KADARI Care on an asset (CARE/13): the card, coverage, purchase details and the asset's history. */

export type CareSummary = components["schemas"]["CareSummaryDto"];
export type CoverageDetail = components["schemas"]["CoverageDetailDto"];
export type CoverageRecord = components["schemas"]["CoverageRecordDto"];
export type AssetHistory = components["schemas"]["AssetHistoryDto"];

/** The Care categories, by the name the API takes (enums bind by name on the Care routes). */
export const careCategories = {
  MajorAppliance: "Large appliance (fridge, washer, stove)",
  SmallAppliance: "Small appliance (kettle, toaster, vacuum)",
  TvAndAudio: "TV or audio",
  Mobile: "Phone or tablet",
  Computing: "Laptop or computer",
  PowerTools: "Power tool",
  PortablePower: "Portable power (inverter, power station)",
  Other: "Something else",
} as const;
export type CareCategoryName = keyof typeof careCategories;

/** The Care card, or null when Care is switched off on this deployment (the routes answer 404 then). */
export async function getCareSummary(
  customerId: string,
  assetId: string,
  signal?: AbortSignal,
): Promise<CareSummary | null> {
  try {
    return await apiClient.get(
      "/api/v1/kadari/customers/{customerId}/assets/{assetId}/care",
      { path: { customerId, assetId }, signal },
    );
  } catch (error) {
    if (error instanceof ApiProblem && error.status === 404) return null;
    throw error;
  }
}

export function enableCare(
  customerId: string,
  assetId: string,
  category: CareCategoryName,
) {
  return apiClient.post(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}/care",
    { path: { customerId, assetId }, body: { category } },
  );
}

export function evaluateCare(customerId: string, assetId: string) {
  return apiClient.post(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}/care/evaluate",
    { path: { customerId, assetId } },
  );
}

export function getCoverage(
  customerId: string,
  assetId: string,
  signal?: AbortSignal,
) {
  return apiClient.get(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}/care/coverage",
    { path: { customerId, assetId }, signal },
  );
}

export interface PurchaseDetails {
  brand?: string;
  model?: string;
  retailer?: string;
  /** yyyy-MM-dd */
  purchasedOn?: string;
}

export function recordPurchaseDetails(
  customerId: string,
  assetId: string,
  details: PurchaseDetails,
) {
  return apiClient.post(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}/purchase",
    {
      path: { customerId, assetId },
      body: {
        brand: details.brand ?? null,
        model: details.model ?? null,
        retailer: details.retailer ?? null,
        purchasedOn: details.purchasedOn ?? null,
      },
    },
  );
}

export function getAssetHistory(
  customerId: string,
  assetId: string,
  signal?: AbortSignal,
) {
  return apiClient.get(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}/history",
    { path: { customerId, assetId }, signal },
  );
}

export type PurchaseProposal = components["schemas"]["PurchaseProposalDto"];

/** Read the asset's stored receipt with AI. A proposal only — nothing is saved until the owner confirms it. */
export function readReceipt(customerId: string, assetId: string) {
  return apiClient.post(
    "/api/v1/kadari/customers/{customerId}/assets/{assetId}/purchase/read-receipt",
    { path: { customerId, assetId } },
  );
}

/** Read a photo of the rating plate or serial label with AI. The photo is not stored. */
export async function readProductLabel(
  customerId: string,
  assetId: string,
  photo: File,
): Promise<PurchaseProposal> {
  const form = new FormData();
  form.append("photo", photo);
  const response = await apiClient.fetch(
    `/api/v1/kadari/customers/${customerId}/assets/${assetId}/purchase/read-label`,
    { method: "POST", body: form },
  );
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
    } | null;
    throw new Error(
      problem?.detail ?? `Could not read the label (${response.status}).`,
    );
  }
  return (await response.json()) as PurchaseProposal;
}

/** Plain words for why KADARI could not read a document. */
export const unavailableReasons: Record<string, string> = {
  NotConfigured:
    "Reading documents isn't switched on yet. Type the details instead.",
  NoCrossBorderConsent:
    "Reading documents uses an AI service outside South Africa. Allow it in your settings, or type the details.",
  QuotaReached:
    "You've used today's document reading. Type the details, or try again tomorrow.",
  NoReceipt: "Add a receipt below first, then KADARI can read it.",
  UnsupportedDocument:
    "KADARI can't read this kind of file. Type the details instead.",
  NothingRead:
    "KADARI couldn't read anything useful. Type the details instead.",
};
