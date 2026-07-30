FROM golang:1.22-alpine AS builder

WORKDIR /app

COPY main.go .
COPY swagger.yaml .

RUN CGO_ENABLED=0 GOOS=linux go build -ldflags="-s -w" -o sensor-mock main.go

FROM alpine:3.19

RUN apk --no-cache add ca-certificates

WORKDIR /root/

COPY --from=builder /app/sensor-mock .

EXPOSE 8085

ENTRYPOINT ["./sensor-mock"]