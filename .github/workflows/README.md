# GitHub Actions workflows

Three workflows automate the build and deploy of Vakansio.

## `ci.yml` — Continuous Integration

Runs on every pull request and every push to `main`. Two parallel jobs:

- **backend** — `dotnet restore && dotnet build && dotnet test` on the full
  solution. Test results uploaded as artefacts (retained 14 days).
- **frontend** — `npm ci && npm run lint && npm run build` for the Vite/React
  SPA. Build artefact uploaded (retained 7 days).

CI is the gate for the two deploy workflows below: a push to `main` only
triggers them if CI passed.

## `deploy-api.yml` — Backend deploy

On push to `main` (after CI green), or manual dispatch.

1. Builds the production Docker image using the multi-stage `Dockerfile`.
2. Exports as `image.tar.gz`.
3. SCPs the tarball to the EC2 host using the SSH key in repo secrets.
4. SSH executes `docker load` + `docker compose up -d api` against
   `docker-compose.production.yml`.
5. Verifies `/health` returns 200 within 60 s (otherwise the run fails).

**Required secrets**:

| Secret | Value |
|---|---|
| `EC2_HOST` | Public DNS or Elastic IP of the API host |
| `EC2_USER` | SSH user (typically `ec2-user`) |
| `EC2_SSH_KEY` | Private key body, PEM format |
| `EC2_DEPLOY_PATH` | Directory on EC2 containing `docker-compose.production.yml` |

## `deploy-frontend.yml` — Frontend deploy

On push to `main` (after CI green), or manual dispatch.

1. Builds the Vite SPA (`npm run build` → `frontend/dist/`).
2. Syncs hashed assets to S3 with a one-year immutable cache.
3. Uploads `index.html` with `no-cache` so deploys are immediately visible.
4. Invalidates CloudFront `/index.html` and `/` so the edge serves the new
   version on the next request.

**Required secrets / variables**:

| Secret | Value |
|---|---|
| `AWS_ACCESS_KEY_ID` | IAM user with `s3:PutObject` + `cloudfront:CreateInvalidation` |
| `AWS_SECRET_ACCESS_KEY` | — |
| `AWS_REGION` | `eu-central-1` |
| `S3_BUCKET` | Frontend bucket (e.g. `vacancies-frontend-prod`) |
| `CLOUDFRONT_DISTRIBUTION_ID` | CloudFront distribution backing the SPA |

**Optional variables**:

| Variable | Value |
|---|---|
| `VITE_API_URL` | Public API base URL embedded in the SPA at build time (defaults to relative `/api`). |
| `VITE_SENTRY_DSN` | Sentry browser DSN. When unset the Sentry SDK skips init and the frontend ships without error reporting (graceful no-op). |

## Sentry error monitoring

The backend reads its DSN from SSM at `/vacancies/prod/Sentry/Dsn`
(SecureString); the frontend reads its DSN from the build-time variable
`VITE_SENTRY_DSN`. When either is missing the corresponding Sentry SDK
auto-disables — same graceful-fallback pattern as `IScoreCalibrator` and
`LangSmithTracer`. To enable:

1. Create a Sentry project for `vakansio` (free tier, 5k errors/month).
2. Copy the **DSN** from Project → Settings → Client Keys.
3. Add it to SSM: `aws ssm put-parameter --name /vacancies/prod/Sentry/Dsn --type SecureString --value '...'`.
4. Add it to repo variables: Settings → Variables → Actions → New variable → `VITE_SENTRY_DSN`.
5. Restart the API container and trigger a frontend deploy.

## Adding secrets

Repository → Settings → Secrets and variables → Actions → **New repository secret**.

For multi-line secrets such as `EC2_SSH_KEY`, paste the full PEM body including
the `-----BEGIN OPENSSH PRIVATE KEY-----` header and the trailing newline.

## Manual dispatch

Both deploy workflows support `workflow_dispatch`, so you can re-deploy from
the Actions tab without pushing a commit (useful for rotating SSM parameters
or rolling a container with a fresh config).
