# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage — compile the Vite/React SPA with pnpm.
# ---------------------------------------------------------------------------
FROM node:24-alpine AS build
WORKDIR /app

# Node tries each registry address for only 250 ms before giving up (happy eyeballs). On a slow or
# high-latency link every attempt times out and corepack/pnpm fail with ETIMEDOUT; give it 2 s.
ENV NODE_OPTIONS=--network-family-autoselection-attempt-timeout=2000

# pnpm is provided via corepack; pin the version to match the lockfile tooling.
RUN corepack enable && corepack prepare pnpm@11.5.2 --activate

# Install dependencies first so this layer is cached until the lockfile changes.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

# Copy the rest of the source and produce the production build in /app/dist.
# (build = `tsr generate && tsc -b && vite build`, so the generated route tree
#  is recreated from scratch — no need to copy it in.)
COPY . .

# Same-origin API base: the runtime nginx reverse-proxies /api → the Web API, so the bundle must call a
# relative /api/... rather than an absolute origin. An explicit empty value means "same origin" (see
# resolveBaseUrl); leaving it unset would make the client throw at load. Overridable as a build arg.
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# Demo helpers (the "Load sample household" button). Only the local AppHost passes "true"; a production build
# leaves it unset, so the button is not compiled in. The API endpoint it calls exists only in Development too.
ARG VITE_DEMO_TOOLS=""
ENV VITE_DEMO_TOOLS=$VITE_DEMO_TOOLS

RUN pnpm build

# ---------------------------------------------------------------------------
# Runtime stage — serve the static bundle with nginx.
# ---------------------------------------------------------------------------
# Unprivileged nginx (GL-20): the master process runs as uid 101, not root, and listens on 8080. Pinned to the
# stable line so a rebuild cannot silently pick up a new major.
FROM nginxinc/nginx-unprivileged:1.28-alpine AS runtime

# nginx's entrypoint runs envsubst over *.template, filling ${API_URL} at start.
# Aspire injects API_URL (see AppHost.cs); the default keeps standalone runs valid.
ENV API_URL=http://webapi:8080
COPY nginx.conf.template /etc/nginx/templates/default.conf.template

COPY --from=build --chown=101:101 /app/dist /usr/share/nginx/html

USER 101

EXPOSE 8080
