# Post-deployment smoke test for ClientServer, runnable from a Windows dev machine against a
# Raspberry Pi's IP address, or locally on the device via PowerShell 7+.
#
# Usage:
#   ./smoke-test.ps1 -ClientServerUrl "http://192.168.1.60:8082" -ClientId 1 -MqttHost "192.168.1.60" -MqttPort 1883

param(
    [string]$ClientServerUrl = "http://localhost:8082",
    [int]$ClientId = 1,
    [string]$MqttHost = "localhost",
    [int]$MqttPort = 1883
)

$pass = 0
$fail = 0

function Test-Endpoint {
    param(
        [string]$Description,
        [string]$Url
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -TimeoutSec 10 -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "[PASS] $Description -> HTTP $($response.StatusCode)" -ForegroundColor Green
            $script:pass++
        }
        else {
            Write-Host "[FAIL] $Description -> HTTP $($response.StatusCode)" -ForegroundColor Red
            $script:fail++
        }
    }
    catch {
        Write-Host "[FAIL] $Description -> $($_.Exception.Message)" -ForegroundColor Red
        $script:fail++
    }
}

Write-Host "== ClientServer smoke test =="
Write-Host "Target: $ClientServerUrl (clientId=$ClientId)"
Write-Host ""

Test-Endpoint "ClientServer health" "$ClientServerUrl/api/client-server/health"
Test-Endpoint "Connectivity to main Server" "$ClientServerUrl/api/client-server/server-check"
Test-Endpoint "Controller status" "$ClientServerUrl/api/client-server/controllers/$ClientId/status"
Test-Endpoint "Controller topology" "$ClientServerUrl/api/client-server/controllers/$ClientId/topology"
Test-Endpoint "Controller telemetry (current)" "$ClientServerUrl/api/client-server/controllers/$ClientId/telemetry/current"
Test-Endpoint "Controller configuration snapshot" "$ClientServerUrl/api/client-server/controllers/$ClientId/configuration"

Write-Host ""
Write-Host "== MQTT broker reachability =="
$mqttTest = Test-NetConnection -ComputerName $MqttHost -Port $MqttPort -WarningAction SilentlyContinue
if ($mqttTest.TcpTestSucceeded) {
    Write-Host "[PASS] MQTT broker reachable at ${MqttHost}:${MqttPort}" -ForegroundColor Green
    $pass++
}
else {
    Write-Host "[FAIL] MQTT broker NOT reachable at ${MqttHost}:${MqttPort}" -ForegroundColor Red
    $fail++
}

Write-Host ""
Write-Host "== Summary =="
Write-Host "Passed: $pass, Failed: $fail"

if ($fail -gt 0) {
    exit 1
}
exit 0
