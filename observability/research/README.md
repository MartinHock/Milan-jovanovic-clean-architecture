# Experimental observability security hardening

Research only. Do not merge into the upstream-oriented template automatically. PR #43 remains separate. Prefer official fixed images when available.

## Rebuild procedure (Linux amd64 Docker)

Clone https://github.com/prometheus/prometheus.git or https://github.com/grafana/tempo.git into a disposable directory. Check out the exact commit in the corresponding source-revision.txt. From that source checkout run `git apply /absolute/path/to/dependencies.patch`. Use the corresponding Dockerfile with the source checkout as build context (`docker build -f /absolute/path/to/Dockerfile -t <local-tag> .`). The Dockerfiles pin builder and runtime images. Tempo uses module mode rather than upstream vendor mode to consume the patched dependencies; transitive changes are captured in the patch.

For Prometheus, the build additionally requires prometheus-web-ui-3.16.0.tar.gz in the source root. The locally used archive SHA256 is 000a45231f6dd22a5703218aad3ee11da125c8a44d1824a27670416c07531001. Its upstream provenance and independent clean rebuild remain unverified; this is a recorded recipe, not yet a certified reproducible build. Promtool version metadata was incomplete.

## Observed validation (2026-11-24/25)

- Prometheus: milan/prometheus:3.16.0-xnet0.62.0; build successful; Trivy 0.70.0 HIGH/CRITICAL zero; config check, readiness, collector scrape and query UI HTTP response successful.
- Tempo: milan/tempo:3.1.1-patched; build successful; go-jose 4.1.6 and x/net 0.62.0 verified in binary; Trivy 0.70.0 HIGH/CRITICAL zero; readiness and OTLP collector-to-Tempo trace retrieval successful (trace bb7cfa05666b444694a693158f4ee241). Scheduler no-jobs messages and Grafana access remain open.
- Collector and Loki: prior scans zero HIGH/CRITICAL. Grafana 13.3.1: 20 HIGH, zero CRITICAL; remediation pending.

Scan each local image with Trivy using `image --scanners vuln --severity HIGH,CRITICAL --exit-code 1 <local-tag>` and a current vulnerability database. No ignore rules. Findings are point-in-time observations, not a security guarantee.