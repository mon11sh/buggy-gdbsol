$ErrorActionPreference = 'Stop'
$gatewayUrl = "http://localhost:8000"

function Assert-Success($stepName, $condition) {
    if ($condition) {
        Write-Host "✅ PASS: $stepName" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $stepName" -ForegroundColor Red
        exit 1
    }
}

Write-Host "--- Phase 5 E2E Gateway Validation ---"

# Wait for gateway to be available
Write-Host "Waiting for API Gateway..."
$retries = 30
while ($retries -gt 0) {
    try {
        $response = Invoke-RestMethod -Uri "$gatewayUrl/" -Method Get -ErrorAction Stop
        if ($response.service -eq "gdb-central-gateway-service") {
            Write-Host "Gateway is ready!"
            break
        }
    } catch {
        # ignore
    }
    Start-Sleep -Seconds 2
    $retries--
}
if ($retries -eq 0) { Write-Host "Gateway failed to start!"; exit 1 }

# 1. Auth Smoke Test
Write-Host "1. Testing Authentication..."
$loginBody = @{
    login_id = "admin"
    password = "Welcome@1"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "$gatewayUrl/auth/api/v1/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
Assert-Success "Admin Login" ($null -ne $loginResponse.access_token)

$token = $loginResponse.access_token
$headers = @{ Authorization = "Bearer $token" }

$tellerBody = @{
    login_id = "teller"
    password = "Welcome@1"
} | ConvertTo-Json

$tellerLoginResponse = Invoke-RestMethod -Uri "$gatewayUrl/auth/api/v1/auth/login" -Method Post -Body $tellerBody -ContentType "application/json"
$tellerToken = $tellerLoginResponse.access_token

$tellerHeaders = @{
    "Authorization" = "Bearer $tellerToken"
}

# 2. Users RBAC Smoke Test
Write-Host "2. Testing Users RBAC..."
$usersResponse = Invoke-RestMethod -Uri "$gatewayUrl/users/api/v1/users" -Method Get -Headers $headers
Assert-Success "Retrieve Users (ADMIN RBAC)" ($usersResponse.users -is [array] -or $usersResponse.users.Length -gt 0)

# 3. Accounts Creation Smoke Test
Write-Host "3. Testing Accounts Workflow..."
$accountBody = @{
    name = "Test Savings"
    pin = "2580"
    date_of_birth = "1990-01-01"
    gender = "Male"
    phone_no = "1234567890"
    aadhar_number = "456789012345"
    account_type = "SAVINGS"
    initial_balance = 5000.00
} | ConvertTo-Json

$accountResponse = Invoke-RestMethod -Uri "$gatewayUrl/accounts/api/v1/accounts/savings" -Method Post -Headers $tellerHeaders -Body $accountBody -ContentType "application/json"
Assert-Success "Create Account" ($accountResponse.account_number -gt 0)
$accountNumber1 = $accountResponse.account_number

$accountBody2 = @{
    name = "Tech Innovations Pvt Ltd"
    pin = "1984"
    company_name = "Digital Solutions Pvt Ltd"
    registration_no = "U74999DL2021PTC345678"
    account_type = "CURRENT"
    website = "http://example.com"
} | ConvertTo-Json
$accountResponse2 = Invoke-RestMethod -Uri "$gatewayUrl/accounts/api/v1/accounts/current" -Method Post -Headers $tellerHeaders -Body $accountBody2 -ContentType "application/json"
$accountNumber2 = $accountResponse2.account_number

# 4. Transactions Smoke Test
Write-Host "4. Testing Transactions Workflow..."

$depositBody = @{
    account_number = $accountNumber1
    amount = 500.00
} | ConvertTo-Json

$depositResponse = Invoke-RestMethod -Uri "$gatewayUrl/transactions/api/v1/transactions/deposit" -Method Post -Headers $tellerHeaders -Body $depositBody -ContentType "application/json"
Assert-Success "Deposit via Gateway" ($depositResponse.transaction_id -ne $null)

$transferBody = @{
    from_account = $accountNumber1
    to_account = $accountNumber2
    amount = 50.00
    pin = "2580"
    transfer_mode = "IMPS"
    description = "Test Transfer"
} | ConvertTo-Json
$transferResponse = Invoke-RestMethod -Uri "$gatewayUrl/transactions/api/v1/transactions/transfer" -Method Post -Headers $tellerHeaders -Body $transferBody -ContentType "application/json"
Assert-Success "Transfer via Gateway" ($transferResponse.transaction_id -ne $null)

# Verify Account 1 Balance
$balanceResponse = Invoke-RestMethod -Uri "$gatewayUrl/accounts/api/v1/accounts/$accountNumber1/balance" -Method Get -Headers $tellerHeaders
# 5000 (initial) + 500 (deposit) - 50 (transfer) = 5450
Assert-Success "Balance Correct" ($balanceResponse.balance -eq 5450.00)

Write-Host "--- ALL TESTS COMPLETED SUCCESSFULLY ---" -ForegroundColor Cyan
