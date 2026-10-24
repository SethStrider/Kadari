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
