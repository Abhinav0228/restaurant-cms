# Restaurant CMS web app

Angular 22: standalone components, signals, zoneless change detection.

| Command | What it does |
|---|---|
| `npm start` | Dev server on http://localhost:4200, talking to the API on :5080 |
| `npm run start:mock` | Dev server with the in-browser mock API (no backend needed) |
| `npm run build` | Production build into `dist/` |
| `npm run test:ci` | Unit tests (Vitest) |
| `npm run e2e` | End-to-end tests (Playwright, desktop + phone, against the mock API) |

## Where things go

```
src/app/
  core/                app-wide singletons (config, http, auth, tenant, realtime, theme, ui, mock)
  shared/              models, presentational components, pipes, utils — no feature imports
  layout/              the staff portal frame
  features/<feature>/
    data-access/       <feature>.api.ts and, for shared state, <feature>.store.ts
    pages/<page>/      routed page: <page>.page.ts + .html + .scss
    ui/<component>/    pieces used by this feature's pages
    <feature>.routes.ts
```

Rules of thumb:

- Pages talk to their feature's `data-access` services, never to `HttpClient` directly.
- Use the path aliases `@core/*`, `@shared/*`, `@features/*`, `@layout/*` and `@env` across folders, and relative imports within a feature.
- A new endpoint needs a method on the feature's API service and a matching route in `core/mock/routes/<feature>.mock.ts`, so the mock (and the e2e tests) keep working.
- Guest pages live under `/r/:tenant`. Build their links with `TenantStore.link(...)` so they stay inside the restaurant.

See [`../docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) for the full picture.
