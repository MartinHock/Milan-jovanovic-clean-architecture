# Container image security assessment — 2026-11-04

Scope: PR #16, commit `e0ac492` before the PostgreSQL loopback-binding change.

## Evidence

- [GitHub Security Audit #8](https://github.com/MartinHock/Milan-jovanovic-clean-architecture/actions/runs/41812298399)
- PostgreSQL image: `postgres:17`; Trivy reports 26 library vulnerabilities (25 HIGH, 1 CRITICAL) and one HIGH secret finding at `/etc/ssl/private/ssl-cert-snakeoil.key`. The CRITICAL finding is `CVE-2025-68121` in Go `crypto/tls` (`stdlib` v1.24.6; fixes reported in 1.24.13 / 1.25.7).
- Grafana image: `grafana/otel-lgtm:0.39.0`; Trivy reports multiple HIGH findings in Go components (including `CVE-2026-111943`, `CVE-2026-78667`, `CVE-2026-78669` and `CVE-2026-97031`). Trivy tables refer to multiple executable targets: individual table counts must **not** be interpreted as an image-wide total.
- Application image and NuGet audits pass. Build and observability E2E pass with LGTM 0.39.0.

## Exposure and controls

- Local `docker-compose.yml` previously published PostgreSQL on all host interfaces (`5432:5432`) with demo credentials. This change restricts publication to `127.0.0.1:5432:5432`. The CI E2E compose file does not publish the database port.
- Grafana UI and OTLP ports are bound to `127.0.0.1` in the local and CI compose configurations.
- Loopback binding reduces network exposure; it does not remove image vulnerabilities, protect against local attackers, or make hard-coded demo credentials suitable for non-development deployments.
- A vulnerability in a bundled Go binary is not proof of a remotely exploitable PostgreSQL server vulnerability. The affected executable and reachable code paths must be established before lowering risk classification.

## Outstanding evidence / release gate

1. Resolve immutable registry image digests for the exact images and platform used by GitHub Actions; capture Trivy version, vulnerability database timestamp and machine-readable JSON/SARIF reports.
2. Map each Go library finding to the actual executable file in the image, its runtime invocation, reachable protocol surface and available upstream fix.
3. Verify whether `ssl-cert-snakeoil.key` is a packaged sample key, how it is used, and whether the same key is shared across deployments. Do not classify it as harmless without checking.
4. Check patched upstream image versions and rescan before considering any narrowly scoped, documented exception.
5. Keep HIGH/CRITICAL blocking enabled. No security waiver or merge is authorized by this assessment.

**Evidence limitation:** The local Docker daemon was unavailable during this assessment; immutable digests and binary-to-CVE mapping were not independently verified. This is an interim triage, not a completed exploitability assessment.

## Local binary verification (2026-11-04)

Docker Engine 29.9.2, Linux/amd64. Local image digests:

- `postgres:17`: `sha256:0027bef26712baaee437a4ea48fdf3d2d2e2bc5f0d81615374408ca320f3c7e3`
- `grafana/otel-lgtm:0.39.0`: `sha256:4c8056c9c05fcb3c8fd93800e85334bfd79b4d0271b47a54a121a64af41fe1fe`

In the PostgreSQL image, `/usr/local/bin/gosu --version` prints `1.19 (go1.24.6 on linux/amd64; gc)`. A byte-string search also identifies `go1.24.6` in `gosu`; PostgreSQL's server executable is a separate file at `/usr/lib/postgresql/17/bin/postgres`. This is a concrete candidate for the Go stdlib CVEs, **not yet an exhaustive mapping of all Trivy findings**. The snakeoil private key exists at `/etc/ssl/private/ssl-cert-snakeoil.key`; its use and exposure remain unverified.

The Grafana image is now locally available. Component-to-binary CVE mapping, attack-path validation and a reproducible local Trivy JSON report remain open. No finding has been waived.

## Local Trivy 0.67.2 JSON scan (2026-11-05)

Reproduction: Trivy Docker image `aquasec/trivy:0.67.2`, `image --scanners vuln --severity HIGH,CRITICAL --ignore-unfixed --format json`, scanning the locally cached Linux/amd64 images. Reports retained locally in untracked `.artifacts/security/` (not committed). Counts are vulnerability *occurrences per target*, not distinct CVE IDs.

- PostgreSQL: 148 occurrences: Debian 13.5 OS target 122 (110 HIGH, 12 CRITICAL); `usr/local/bin/gosu` 26 (25 HIGH, 1 CRITICAL). The earlier GitHub CI result showed zero OS CVEs and a Debian 13.7 target. **Do not equate the local cached image with the exact CI image** without verifying its digest and platform; a mutable `postgres:17` tag can move. The Go findings are specifically mapped by Trivy to `gosu`, invoked by the entrypoint to drop privileges. No remotely reachable vulnerable Go TLS code path has been demonstrated.
- Grafana LGTM: 50 HIGH occurrences across 16 Go binary targets, zero CRITICAL. These include bundled Grafana datasource plugins, `obi`, `tempo`, `tempo-cli`, `tempo-query`, `otelcol-contrib` and `pyroscope`. Presence in an image does not prove runtime activation or exploitability; some binaries are runtime services and must not be automatically exempted.

Before making a waiver or comparing with CI, collect the CI-resolved image digests and vulnerability DB metadata; identify whether each vulnerable binary is executed and whether its affected code path is reachable. The local reports include no secret scanner because this reproduction used `--scanners vuln`; the earlier CI secret finding is not cleared by this scan.
