import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import {
  useMutation,
  useQuery,
  useQueryClient,
  keepPreviousData,
} from "@tanstack/react-query";
import {
  Search,
  Users as UsersIcon,
  ChevronLeft,
  ChevronRight,
  Check,
  Ban,
  RotateCcw,
} from "lucide-react";
import { toast } from "sonner";

import { DashboardNavbar } from "@/components/dashboard/dashboard-navbar";
import { cn } from "@/lib/utils";
import { ApiProblem, apiClient } from "@/lib/api";
import { useSession } from "@/lib/auth/session";
import {
  listUsers,
  assignRole,
  removeRole,
  setUserStatus,
  ASSIGNABLE_ROLES,
  type UserSummary,
} from "@/lib/api/users";

export const Route = createFileRoute("/dashboard/users")({
  component: UsersPage,
});

const PAGE_SIZE = 12;

function errorMessage(error: unknown): string {
  if (error instanceof ApiProblem) return error.message;
  return "Something went wrong. Please try again.";
}

function num(v: string | number | null | undefined): number {
  if (v === null || v === undefined) return 0;
  const n = typeof v === "string" ? Number(v) : v;
  return Number.isFinite(n) ? n : 0;
}

// ---- Role chips for one user ------------------------------------------------------

function RoleChips({
  user,
  isSelf,
  pending,
  onToggle,
}: {
  user: UserSummary;
  isSelf: boolean;
  pending: string | null;
  onToggle: (role: string, grant: boolean) => void;
}) {
  const held = new Set(user.roles);
  return (
    <div className="flex flex-wrap gap-1.5">
      {ASSIGNABLE_ROLES.map((role) => {
        const on = held.has(role.name);
        // A signed-in admin cannot revoke their own System-admin role (mirrors the server guard).
        const lockSelf = isSelf && role.name === "SystemAdministrator" && on;
        const busy = pending === role.name;
        return (
          <button
            key={role.name}
            type="button"
            disabled={busy || lockSelf}
            title={
              lockSelf
                ? "You can't revoke your own System admin role — ask another administrator."
                : on
                  ? `Revoke ${role.label}`
                  : `Grant ${role.label}`
            }
            onClick={() => onToggle(role.name, !on)}
            className={cn(
              "inline-flex items-center gap-1 rounded-full border px-2.5 py-1 font-technical text-[10px] tracking-wide uppercase transition-colors",
              on
                ? "border-accent/40 bg-accent/15 text-accent"
                : "border-white/10 bg-white/[0.02] text-mid-grey hover:border-white/25 hover:text-soft-grey",
              (busy || lockSelf) && "opacity-50",
            )}
          >
            {on && <Check className="size-3" />}
            {role.label}
          </button>
        );
      })}
    </div>
  );
}

// ---- Page -------------------------------------------------------------------------

function UsersPage() {
  const queryClient = useQueryClient();
  const me = useSession((s) => s.user);
  const hydrate = useSession((s) => s.hydrate);

  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  // { userId: role } currently mutating, to disable just that chip.
  const [pending, setPending] = useState<{
    userId: string;
    role: string;
  } | null>(null);

  // Debounce the search box, and reset to page 1 whenever the term changes.
  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(searchInput);
      setPage(1);
    }, 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const users = useQuery({
    queryKey: ["admin-users", search, page],
    queryFn: () => listUsers(search, page, PAGE_SIZE),
    placeholderData: keepPreviousData,
  });

  const role = useMutation({
    mutationFn: ({
      userId,
      roleName,
      grant,
    }: {
      userId: string;
      roleName: string;
      grant: boolean;
    }) => (grant ? assignRole(userId, roleName) : removeRole(userId, roleName)),
    onMutate: (v) => setPending({ userId: v.userId, role: v.roleName }),
    onSettled: () => setPending(null),
    onSuccess: async (_data, v) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-users"] });
      // Changing your own roles only takes effect on a fresh token (permissions are claims-based).
      if (v.userId === me?.id) {
        await apiClient.refresh();
        hydrate();
        toast.success("Your roles changed — session refreshed.");
      } else {
        toast.success(v.grant ? "Role granted" : "Role revoked");
      }
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const status = useMutation({
    mutationFn: ({ userId, next }: { userId: string; next: string }) =>
      setUserStatus(userId, next),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["admin-users"] });
      toast.success("Account status updated");
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const rows = useMemo(() => users.data?.items ?? [], [users.data]);
  const total = num(users.data?.totalCount);
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <>
      <DashboardNavbar title="Users" />

      <main className="flex-1 overflow-y-auto p-6 lg:p-8">
        <div className="mb-6">
          <h2 className="font-heading text-2xl font-bold text-off-white">
            Users &amp; roles
          </h2>
          <p className="mt-1 max-w-2xl font-body text-sm text-soft-grey">
            Grant or revoke a staff member's roles. Roles are additive — a
            person can hold several. Changes take effect on the account's next
            token.
          </p>
        </div>

        <div className="relative mb-4 max-w-md">
          <Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-mid-grey" />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search by name, email or mobile…"
            className="h-11 w-full rounded-xl border border-white/10 bg-white/[0.03] pr-3 pl-10 font-body text-sm text-off-white placeholder:text-mid-grey focus:border-accent/40 focus:outline-none"
          />
        </div>

        {users.isError ? (
          <div className="rounded-2xl border border-destructive/30 bg-destructive/10 p-5 font-body text-sm text-off-white">
            Could not load users. You may not have permission for this view.
          </div>
        ) : (
          <section className="rounded-2xl border border-white/10 bg-white/[0.03] backdrop-blur-md">
            <div className="flex items-center justify-between border-b border-white/5 px-5 py-3">
              <h3 className="font-heading text-sm font-semibold text-off-white">
                Accounts
              </h3>
              <span className="font-technical text-[11px] tracking-wide text-mid-grey uppercase">
                {total.toLocaleString()} total
              </span>
            </div>

            <div className="divide-y divide-white/5">
              {rows.map((user) => (
                <div
                  key={user.id}
                  className="flex flex-col gap-3 px-5 py-4 lg:flex-row lg:items-center lg:justify-between"
                >
                  <div className="flex min-w-0 flex-col">
                    <span className="font-heading text-sm font-semibold text-off-white">
                      {user.name || "—"}
                      {user.id === me?.id && (
                        <span className="ml-2 rounded-full border border-white/10 bg-white/5 px-1.5 py-0.5 font-technical text-[9px] tracking-wide text-mid-grey uppercase">
                          You
                        </span>
                      )}
                    </span>
                    <span className="font-body text-xs text-mid-grey">
                      {[user.email, user.mobileNumber]
                        .filter(Boolean)
                        .join(" · ")}
                      {user.status !== "Active" && (
                        <span className="ml-2 text-amber-300">
                          {user.status}
                        </span>
                      )}
                    </span>
                  </div>

                  <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
                    <RoleChips
                      user={user}
                      isSelf={user.id === me?.id}
                      pending={
                        pending?.userId === user.id ? pending.role : null
                      }
                      onToggle={(roleName, grant) =>
                        role.mutate({ userId: user.id, roleName, grant })
                      }
                    />

                    {user.id !== me?.id &&
                      (user.status === "Active" ? (
                        <button
                          type="button"
                          disabled={status.isPending}
                          onClick={() => {
                            if (
                              window.confirm(
                                `Suspend ${user.name || "this account"}? They won't be able to act until reactivated.`,
                              )
                            ) {
                              status.mutate({
                                userId: user.id,
                                next: "Suspended",
                              });
                            }
                          }}
                          className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-white/10 px-2.5 py-1.5 font-body text-xs text-soft-grey transition-colors hover:border-amber-400/40 hover:text-amber-300 disabled:opacity-40"
                        >
                          <Ban className="size-3.5" /> Suspend
                        </button>
                      ) : (
                        <button
                          type="button"
                          disabled={status.isPending}
                          onClick={() =>
                            status.mutate({ userId: user.id, next: "Active" })
                          }
                          className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-accent/30 bg-accent/10 px-2.5 py-1.5 font-body text-xs text-accent transition-colors hover:bg-accent/15 disabled:opacity-40"
                        >
                          <RotateCcw className="size-3.5" /> Reactivate
                        </button>
                      ))}
                  </div>
                </div>
              ))}

              {rows.length === 0 && !users.isLoading && (
                <div className="flex flex-col items-center gap-3 px-5 py-12 text-center">
                  <UsersIcon className="size-8 text-mid-grey" />
                  <p className="font-body text-sm text-soft-grey">
                    {search
                      ? "No accounts match that search."
                      : "No accounts yet."}
                  </p>
                </div>
              )}
            </div>

            <div className="flex items-center justify-between border-t border-white/5 px-5 py-3">
              <span className="font-body text-xs text-mid-grey">
                Page {page} of {totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((n) => Math.max(1, n - 1))}
                  className="flex items-center gap-1 rounded-lg border border-white/10 px-3 py-1.5 font-body text-xs text-soft-grey transition-colors enabled:hover:bg-white/5 disabled:opacity-40"
                >
                  <ChevronLeft className="size-4" /> Prev
                </button>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((n) => n + 1)}
                  className="flex items-center gap-1 rounded-lg border border-white/10 px-3 py-1.5 font-body text-xs text-soft-grey transition-colors enabled:hover:bg-white/5 disabled:opacity-40"
                >
                  Next <ChevronRight className="size-4" />
                </button>
              </div>
            </div>
          </section>
        )}
      </main>
    </>
  );
}
