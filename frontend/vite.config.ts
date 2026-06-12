import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
//
// v6.7.8 — server-side proxy so `npm run dev` on localhost:5173 transparently
// reaches the prod backend at api.vakansio.online. The browser sees only
// same-origin requests to localhost:5173/api/*; Vite proxies them server-side
// to https://api.vakansio.online/api/*. Two consequences:
//   - No CORS preflight: prod's Cors:AllowedOrigins SSM doesn't need to list
//     http://localhost:5173.
//   - The frontend dev iteration loop (hot reload, source maps, instant edits)
//     runs against real prod data + real prod scoring — same vacancies, same
//     ScoringCache, same VacancyAnalysisJson. localhost view matches the
//     CloudFront demo URL byte-for-byte.
// Authentication uses Bearer tokens in the Authorization header (see
// src/api/client.ts:19-25) — localStorage is per-origin, so a fresh login on
// localhost:5173 obtains a separate JWT scoped to that origin.
//
// To roll back to the local .NET backend, restore `frontend/.env.development`
// to `VITE_API_BASE_URL=http://localhost:5180/api` and remove `server.proxy`.
export default defineConfig({
    plugins: [react()],
    // LOCAL DEV MODE — talks directly to local .NET API at http://localhost:5180/api
    // via VITE_API_BASE_URL in .env.development. No prod proxy.
    // To switch back to prod, restore the `server.proxy` block (see git history).
})
