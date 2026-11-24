FROM golang:1.27.4@sha256:205c7636d74e0ffbe3d007162df7d74f859f5a8c8322dfb9f907977686c126cf AS build
WORKDIR /src
COPY . .
RUN go mod verify && go list -mod=readonly -m github.com/go-jose/go-jose/v4 golang.org/x/net
RUN CGO_ENABLED=0 GOAMD64=v2 go build -mod=readonly -ldflags '-w -X main.Version=3.1.1-patched -X main.Revision=9db7df79d2af105a90d96484e6403e1fda0d011e' -o /out/tempo ./cmd/tempo && go version -m /out/tempo
FROM grafana/tempo:3.1.1@sha256:64e2fbd56d9c2b3412a4b430a58ea59e91a670b1e75f920a0c702213ad145044
COPY --from=build /out/tempo /tempo