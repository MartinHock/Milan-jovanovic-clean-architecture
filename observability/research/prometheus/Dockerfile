FROM golang:1.27.4@sha256:205c7636d74e0ffbe3d007162df7d74f859f5a8c8322dfb9f907977686c126cf AS build
WORKDIR /src
COPY . .
RUN tar -xzf prometheus-web-ui-3.16.0.tar.gz -C web/ui && sed -i 's/\r$//' scripts/compress_assets.sh web/ui/embed.go.tmpl && bash scripts/compress_assets.sh
RUN go mod verify && go list -m golang.org/x/net
RUN CGO_ENABLED=0 go build -mod=readonly -tags netgo,builtinassets -ldflags '-X github.com/prometheus/common/version.Version=3.16.0-patched -X github.com/prometheus/common/version.Revision=e2e28adeee2c5883d000fc82aca6008cfbf7e96d' -o /out/prometheus ./cmd/prometheus && CGO_ENABLED=0 go build -mod=readonly -tags netgo,builtinassets -o /out/promtool ./cmd/promtool && go version -m /out/prometheus && go version -m /out/promtool
FROM prom/prometheus:v3.16.0@sha256:e04987641b57704f408e31f6373ba4138ecf27cb4a165d24aabb6c38fddd0f06
COPY --from=build /out/prometheus /bin/prometheus
COPY --from=build /out/promtool /bin/promtool
