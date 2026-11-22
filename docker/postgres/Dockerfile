FROM golang:1.27 AS gosu-builder
ARG GOSU_COMMIT=29b939aa5bd4dc588af765b8248f6e39150d2565
RUN git clone https://github.com/tianon/gosu.git /src/gosu \
    && cd /src/gosu \
    && git checkout --detach "$GOSU_COMMIT" \
    && CGO_ENABLED=0 go build -trimpath -ldflags='-s -w' -o /gosu .

FROM postgres:17
RUN apt-get update \
    && apt-get install --only-upgrade -y --no-install-recommends zlib1g \
    && rm -rf /var/lib/apt/lists/*
COPY --from=gosu-builder /gosu /usr/local/bin/gosu
