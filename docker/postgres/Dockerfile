FROM postgres:17
RUN apt-get update \
    && apt-get install --only-upgrade -y --no-install-recommends zlib1g \
    && rm -rf /var/lib/apt/lists/*
