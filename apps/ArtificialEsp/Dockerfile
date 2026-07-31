FROM golang:1.22-alpine AS builder

WORKDIR /app

COPY main.go .
COPY swagger.yaml .

RUN go mod init mock-sensor
RUN CGO_ENABLED=0 GOOS=linux go build -ldflags="-s -w" -o sensor-mock main.go

FROM scratch

WORKDIR /

COPY --from=builder /app/sensor-mock /sensor-mock

EXPOSE 8085

ENTRYPOINT ["/sensor-mock"]