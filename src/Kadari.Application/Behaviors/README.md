# Pipeline behaviours

These wrap **every command** as it passes through MediatR. They are the machinery a call site does *not*
show: an endpoint reads `sender.Send(command)`, but validation, a transaction, and the domain-event
cascade all run here. Registered once in `Application/DependencyInjection.AddApplication` — you never wire
them per handler.

Order is **outer → inner** (registration order):

| Order | Behaviour | What it does | Applies to |
| --- | --- | --- | --- |
| 1 (outer) | `ValidationBehavior` | Runs the command's FluentValidation validator and **fails fast** — before any transaction opens. | Commands with a validator |
| 2 (inner) | `TransactionalDispatchBehavior` | Opens one transaction (`IUnitOfWork`), runs the handler, then **drains domain events** raised by aggregates (iteratively, bounded by `MaxCascades`), then a final `SaveChanges` + commit. | `IBaseCommand` only |

Consequences worth knowing before you write a handler:

- **One command = one transaction.** The handler's writes, the entire synchronous reaction cascade, and any
  integration events a translator buffers to the outbox commit **atomically — both or neither**.
- **Queries skip all of this** — no validator behaviour opens a transaction for an `IQuery`, and reads
  raise no domain events.
- **Retry re-runs the whole delegate.** Postgres' retrying execution strategy re-executes the command + its
  cascade on a transient error. DB state is safe (each attempt rolls back), but **external I/O inline in a
  handler (payment / notification / WhatsApp send) can re-fire** — move side-effects to post-commit
  integration events (outbox → worker). See `CLAUDE.md §6a`.

For the end-to-end picture see [`SOLUTION.md`](../../../SOLUTION.md) → *Life of a command*.
