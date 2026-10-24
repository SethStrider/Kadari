import { useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { LifeBuoy } from "lucide-react";
import { toast } from "sonner";

import { DashboardNavbar } from "@/components/dashboard/dashboard-navbar";
import { Button } from "@/components/ui/button";
import { ApiProblem } from "@/lib/api";
import {
  CHANNEL_TYPES,
  RESOLUTIONS,
  ROUTE_TYPES,
  WORK_TYPES,
  getOpsQueue,
  getOpsWorkItem,
  resolveOpsWorkItem,
  setCaseRoute,
  takeOpsWorkItem,
  type OpsWorkItem,
} from "@/lib/api/care-ops";
import { isForbidden, retryOnlyTransient } from "@/lib/api/query";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/dashboard/care-ops")({
  component: CareOpsPage,
});

const fieldClass =
  "h-10 w-full rounded-xl border border-white/10 bg-white/[0.04] px-3 font-body text-sm text-off-white placeholder:text-mid-grey focus:border-trustli-cyan focus:outline-none";

const dateTime = new Intl.DateTimeFormat("en-ZA", {
  dateStyle: "medium",
  timeStyle: "short",
});

function errorMessage(error: unknown): string {
  return error instanceof ApiProblem
    ? error.message
    : "Something went wrong. Please try again.";
}

const severityStyle: Record<string, string> = {
  P0: "bg-red-500/20 text-red-300",
  P1: "bg-amber-500/20 text-amber-300",
  P2: "bg-sky-500/20 text-sky-300",
  P3: "bg-white/10 text-soft-grey",
};

/**
 * Care Ops (CARE/11, CARE/14 C12): work the exception queue by severity — find a route for an unknown-route case,
 * review a safety report, resolve a failed registration — and say what kind of knowledge each resolution produced.
 */
function CareOpsPage() {
  const [resolved, setResolved] = useState(false);
  const [workType, setWorkType] = useState("");
  const [selected, setSelected] = useState<string | null>(null);

  const queue = useQuery({
    queryKey: ["care-ops", resolved, workType],
    queryFn: () => getOpsQueue(resolved, workType),
    retry: retryOnlyTransient,
  });

  return (
    <>
      <DashboardNavbar title="Care Ops" />
      <main className="flex flex-1 flex-col gap-6 p-6 lg:p-8">
        <div className="flex flex-wrap items-center gap-3">
          <select
            className={cn(fieldClass, "w-auto")}
            value={workType}
            onChange={(e) => setWorkType(e.target.value)}
          >
            <option value="">All queues</option>
            {Object.entries(WORK_TYPES).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
          <Button
            variant={resolved ? "secondary" : "primary"}
            size="sm"
            onClick={() => setResolved(false)}
          >
            Open
          </Button>
          <Button
            variant={resolved ? "primary" : "secondary"}
            size="sm"
            onClick={() => setResolved(true)}
          >
            Resolved
          </Button>
        </div>

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
          <section className="flex flex-col gap-2">
            {queue.isPending && (
              <p className="font-body text-sm text-soft-grey">
                Loading the queue…
              </p>
            )}
            {queue.isError && (
              <p className="font-body text-sm text-soft-grey">
                {isForbidden(queue.error)
                  ? "Care Ops needs the Customer success role."
                  : errorMessage(queue.error)}
              </p>
            )}
            {queue.data?.length === 0 && (
              <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] p-6 text-soft-grey">
                <LifeBuoy className="size-5" /> Nothing waiting. Automation is
                handling everything.
              </div>
            )}
            {queue.data?.map((item) => (
              <QueueRow
                key={item.workItemId}
                item={item}
                active={selected === item.workItemId}
                onSelect={() => setSelected(item.workItemId)}
              />
            ))}
          </section>
          {selected && (
            <WorkItemPanel
              key={selected}
              workItemId={selected}
              onDone={() => setSelected(null)}
            />
          )}
        </div>
      </main>
    </>
  );
}

function QueueRow({
  item,
  active,
  onSelect,
}: {
  item: OpsWorkItem;
  active: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={cn(
        "flex flex-col gap-1 rounded-2xl border bg-white/[0.03] p-4 text-left transition-colors hover:border-trustli-cyan",
        active ? "border-trustli-cyan" : "border-white/10",
      )}
    >
      <span className="flex items-center gap-2">
        <span
          className={cn(
            "rounded-full px-2 py-0.5 font-technical text-[11px]",
            severityStyle[item.severity],
          )}
        >
          {item.severity}
        </span>
        <span className="font-heading text-sm font-semibold text-off-white">
          {WORK_TYPES[item.workType] ?? item.workType}
        </span>
        {item.overdue && (
          <span className="font-technical text-[11px] text-red-300">
            OVERDUE
          </span>
        )}
      </span>
      <span className="font-body text-sm text-soft-grey">{item.summary}</span>
      <span className="font-body text-xs text-mid-grey">
        Raised {dateTime.format(new Date(item.raisedAt))}
        {item.assignedTo && " · taken"}
        {item.resolution &&
          ` · ${RESOLUTIONS[item.resolution] ?? item.resolution}`}
      </span>
    </button>
  );
}

function WorkItemPanel({
  workItemId,
  onDone,
}: {
  workItemId: string;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const detail = useQuery({
    queryKey: ["care-ops-item", workItemId],
    queryFn: () => getOpsWorkItem(workItemId),
    retry: retryOnlyTransient,
  });
  const [resolution, setResolution] = useState("CaseSpecific");
  const [notes, setNotes] = useState("");
  const [route, setRoute] = useState({
    routeType: "",
    targetName: "",
    channelType: "WebPage",
    channelValue: "",
    instructions: "",
  });
  const [busy, setBusy] = useState(false);

  async function run(
    action: () => Promise<unknown>,
    success: string,
    close = true,
  ) {
    setBusy(true);
    try {
      await action();
      toast.success(success);
      await queryClient.invalidateQueries({ queryKey: ["care-ops"] });
      await queryClient.invalidateQueries({
        queryKey: ["care-ops-item", workItemId],
      });
      if (close) onDone();
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  if (detail.isPending)
    return <p className="font-body text-sm text-soft-grey">Loading…</p>;
  if (detail.isError)
    return (
      <p className="font-body text-sm text-soft-grey">
        {errorMessage(detail.error)}
      </p>
    );

  const { item, product, case: caseContext, routingModels } = detail.data;
  const open = item.status !== "Resolved";
  const routeTask =
    open &&
    item.workType === "RouteUnknown" &&
    caseContext?.status === "Preparing";

  return (
    <section className="flex flex-col gap-5 rounded-2xl border border-white/10 bg-white/[0.03] p-6">
      <div>
        <h3 className="font-heading text-base font-semibold text-off-white">
          {WORK_TYPES[item.workType] ?? item.workType}
        </h3>
        <p className="mt-1 font-body text-sm text-soft-grey">{item.summary}</p>
      </div>

      {product && (
        <Facts
          title="Product"
          rows={[
            ["Category", product.category],
            ["Brand", product.brand],
            ["Model", product.model],
            ["Bought from", product.retailer],
            ["Purchased", product.purchasedOn],
            ["Cover", product.coverageState],
          ]}
        />
      )}

      {caseContext && (
        <Facts
          title="Case"
          rows={[
            ["Status", caseContext.status],
            ["Problem", caseContext.issueClass],
            ["Cover when reported", caseContext.coverageAtOpen],
            [
              "Route",
              `${caseContext.routeType} (${caseContext.routeConfidence})`,
            ],
            ["Owner's words", caseContext.issueSummary],
          ]}
        />
      )}

      {caseContext && caseContext.timeline.length > 0 && (
        <ol className="flex flex-col gap-1 font-body text-xs text-mid-grey">
          {caseContext.timeline.map((line, i) => (
            <li key={i}>{line}</li>
          ))}
        </ol>
      )}

      {open && !item.assignedTo && (
        <Button
          size="sm"
          variant="secondary"
          className="self-start"
          disabled={busy}
          onClick={() => run(() => takeOpsWorkItem(workItemId), "Taken", false)}
        >
          Take this
        </Button>
      )}

      {routeTask && (
        <form
          className="flex flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            void run(
              () =>
                setCaseRoute(workItemId, {
                  ...route,
                  channelValue: route.channelValue || null,
                  resolution,
                  notes,
                }),
              "Route set — the owner will confirm it",
            );
          }}
        >
          <h4 className="font-heading text-sm font-semibold text-off-white">
            Where it goes
          </h4>
          <select
            className={fieldClass}
            value={route.routeType}
            onChange={(e) => setRoute({ ...route, routeType: e.target.value })}
            required
          >
            <option value="">Route…</option>
            {Object.entries(ROUTE_TYPES).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
          {routingModels.length > 0 && (
            <p className="font-body text-xs text-mid-grey">
              Routing models:{" "}
              {routingModels.map((m) => `${m.code} ${m.name}`).join(" · ")}
            </p>
          )}
          <input
            className={fieldClass}
            placeholder="Who (e.g. Bosch Home Service)"
            value={route.targetName}
            onChange={(e) => setRoute({ ...route, targetName: e.target.value })}
            required
            maxLength={200}
          />
          <div className="grid gap-3 sm:grid-cols-[10rem_1fr]">
            <select
              className={fieldClass}
              value={route.channelType}
              onChange={(e) =>
                setRoute({ ...route, channelType: e.target.value })
              }
            >
              {CHANNEL_TYPES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
            <input
              className={fieldClass}
              placeholder={
                route.channelType === "WebPage"
                  ? "https://… (official page)"
                  : "Address or number"
              }
              value={route.channelValue}
              onChange={(e) =>
                setRoute({ ...route, channelValue: e.target.value })
              }
              disabled={route.channelType === "InStore"}
            />
          </div>
          <textarea
            className={cn(fieldClass, "h-20 py-2")}
            placeholder="What the owner should do"
            value={route.instructions}
            onChange={(e) =>
              setRoute({ ...route, instructions: e.target.value })
            }
            required
            maxLength={1000}
          />
          <ResolutionFields
            resolution={resolution}
            setResolution={setResolution}
            notes={notes}
            setNotes={setNotes}
          />
          <Button
            type="submit"
            size="sm"
            className="self-start"
            disabled={busy}
          >
            Set route
          </Button>
        </form>
      )}

      {open && !routeTask && (
        <form
          className="flex flex-col gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            void run(
              () => resolveOpsWorkItem(workItemId, resolution, notes),
              "Resolved",
            );
          }}
        >
          <ResolutionFields
            resolution={resolution}
            setResolution={setResolution}
            notes={notes}
            setNotes={setNotes}
          />
          <Button
            type="submit"
            size="sm"
            className="self-start"
            disabled={busy || !notes.trim()}
          >
            Resolve
          </Button>
        </form>
      )}

      {!open && item.resolutionNotes && (
        <p className="font-body text-sm text-soft-grey">
          {RESOLUTIONS[item.resolution ?? ""] ?? item.resolution}:{" "}
          {item.resolutionNotes}
        </p>
      )}
    </section>
  );
}

function ResolutionFields({
  resolution,
  setResolution,
  notes,
  setNotes,
}: {
  resolution: string;
  setResolution: (v: string) => void;
  notes: string;
  setNotes: (v: string) => void;
}) {
  return (
    <>
      <select
        className={fieldClass}
        value={resolution}
        onChange={(e) => setResolution(e.target.value)}
      >
        {Object.entries(RESOLUTIONS).map(([k, v]) => (
          <option key={k} value={k}>
            {v}
          </option>
        ))}
      </select>
      <textarea
        className={cn(fieldClass, "h-20 py-2")}
        placeholder="What you found, and the source"
        value={notes}
        onChange={(e) => setNotes(e.target.value)}
        required
        maxLength={1000}
      />
    </>
  );
}

function Facts({
  title,
  rows,
}: {
  title: string;
  rows: [string, string | null | undefined][];
}) {
  return (
    <div>
      <h4 className="font-heading text-sm font-semibold text-off-white">
        {title}
      </h4>
      <dl className="mt-2 grid grid-cols-[9rem_1fr] gap-x-3 gap-y-1 font-body text-sm">
        {rows
          .filter(([, v]) => v)
          .map(([k, v]) => (
            <div key={k} className="contents">
              <dt className="text-mid-grey">{k}</dt>
              <dd className="text-soft-grey">{v}</dd>
            </div>
          ))}
      </dl>
    </div>
  );
}
