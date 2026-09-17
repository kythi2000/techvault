# TechVault Web

The public Next.js frontend for the TechVault digital technology archive. Its current scope implements Frontend Phases 1–4: foundation, public catalog browse, the device detail/specification vertical slice, and brand/category taxonomy pages. See [the frontend specification](../../docs/FRONTEND_SPEC.md).

## Run locally

Start the migrated and seeded API at `http://localhost:5078`, then run:

```powershell
cd apps/web
npm install
npm run dev
```

Open `http://localhost:3000`. Server Components use `TECHVAULT_API_URL`, which defaults to `http://localhost:5078`.

Copy `.env.example` in this directory to `.env.local` when local values need to be overridden:

```dotenv
TECHVAULT_API_URL=http://localhost:5078
NEXT_PUBLIC_SITE_URL=http://localhost:3000
```

`TECHVAULT_API_URL` is server-only. Never expose admin credentials with a `NEXT_PUBLIC_` prefix.

## Quality checks

```powershell
npm run lint
npm run typecheck
npm test
npm run build
npm run test:smoke
```

The web build does not require a live API. Pages use request-time server rendering and show a deliberate unavailable state when the catalog cannot be reached. `test:smoke` starts a temporary contract-compatible API and the production Next.js server; run it after `build`.
