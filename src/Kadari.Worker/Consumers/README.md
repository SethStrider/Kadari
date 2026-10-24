# Consumers

One public `IConsumer<TEvent>` per integration event in `Kadari.Contracts`, discovered by `AddConsumers` — adding a
consumer needs no DI edit. Consumers live **only** here (enforced by `Kadari.ArchitectureTests`).

A consumer:

- receives through the EF inbox, so a redelivered message (same message id) is skipped;
- does any external call itself — never inside an API request transaction;
- records the outcome by sending an `IUnscopedRequest` command through `ISender`, so the rules stay in the domain;
- honours the `CancellationToken` on `context.CancellationToken`.
