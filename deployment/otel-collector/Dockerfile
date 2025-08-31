FROM golang:1.25-alpine AS builder

RUN apk --no-cache add git

ENV GOPROXY=direct
ENV GOSUMDB=off

RUN go install go.opentelemetry.io/collector/cmd/builder@latest

WORKDIR /build

COPY builder-config.yaml .

RUN builder --config=builder-config.yaml

FROM alpine:latest

RUN apk --no-cache add ca-certificates

COPY --from=builder /build/otelcol-custom/otelcol-custom /otelcol-custom

RUN chmod +x /otelcol-custom

EXPOSE 4317 4318 8888 8889

ENTRYPOINT ["/otelcol-custom"]

CMD ["--config=/etc/otel-collector-config.yaml"]