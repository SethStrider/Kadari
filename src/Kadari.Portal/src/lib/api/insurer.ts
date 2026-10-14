import { apiClient } from "./client";

/**
 * Insurer-console reads, typed against the generated schema. The signed-in insurer's own context (which
 * tenant they belong to + the white-label branding the console themes itself with), the tenant's whole-cohort
 * portfolio summary, and the paged asset list behind it. All go through the one typed {@link apiClient}.
 */

export function getInsurerContext() {
  return apiClient.get("/api/v1/kadari/insurer/context");
}

export function getTenantPortfolio(tenantId: string) {
  return apiClient.get(
    "/api/v1/kadari/insurer/tenants/{tenantId}/portfolio",
    { path: { tenantId } },
  );
}

export function listTenantAssets(
  tenantId: string,
  page: number,
  pageSize: number,
) {
  return apiClient.get("/api/v1/kadari/insurer/tenants/{tenantId}/assets", {
    path: { tenantId },
    query: { Page: page, PageSize: pageSize },
  });
}

export type InsurerContext = Awaited<ReturnType<typeof getInsurerContext>>;
export type TenantBranding = NonNullable<InsurerContext["branding"]>;
export type TenantPortfolio = Awaited<ReturnType<typeof getTenantPortfolio>>;
export type TenantAssetsPage = Awaited<ReturnType<typeof listTenantAssets>>;
export type TenantAssetRow = TenantAssetsPage["items"][number];
