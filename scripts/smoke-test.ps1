<#
  SmartInventoryManagementAPI - Smoke Test
  Usage (API pehle dotnet run se chalu honi chahiye):
    powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1
    powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 -BaseUrl https://localhost:7014
  Note: ye script development DB mein test data (users, products, orders) bana deti hai.
#>
param(
    [string]$BaseUrl = "https://localhost:7014",
    [string]$AdminEmail = "admin@smartinventory.local",
    [string]$AdminPassword = "Admin@12345"
)

# ---------- Dev certificate (Windows PowerShell 5.1 ke liye) ----------
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
}

# ---------- Helpers ----------
$script:Pass = 0
$script:Fail = 0

function Section([string]$Title) {
    Write-Host ""
    Write-Host "== $Title ==" -ForegroundColor Cyan
}

function Check([string]$Name, [bool]$Condition, [string]$Detail = "") {
    if ($Condition) {
        Write-Host "[PASS] $Name" -ForegroundColor Green
        $script:Pass++
    }
    else {
        Write-Host "[FAIL] $Name $Detail" -ForegroundColor Red
        $script:Fail++
    }
}

function Call([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null) {
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Method          = $Method
        Uri             = "$BaseUrl$Path"
        Headers         = $headers
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
        $params.ContentType = "application/json"
    }
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $params.SkipCertificateCheck = $true
        $params.SkipHttpErrorCheck = $true
    }

    try {
        $r = Invoke-WebRequest @params
        $status = [int]$r.StatusCode
        $content = $r.Content
    }
    catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $status = [int]$resp.StatusCode
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
    }

    $json = $null
    if ($content) { try { $json = $content | ConvertFrom-Json } catch { } }
    [pscustomobject]@{ Status = $status; Json = $json; Raw = $content }
}

function ExpectStatus([string]$Name, $Response, [int]$Expected) {
    $raw = ""
    if ($Response.Raw) { $raw = $Response.Raw.Substring(0, [Math]::Min(200, $Response.Raw.Length)) }
    Check $Name ($Response.Status -eq $Expected) "(expected $Expected, got $($Response.Status)) $raw"
}

$run = Get-Date -Format "MMddHHmmss"
$pw = "Test@1234"

# =====================================================================
Section "AUTH"
# =====================================================================
$admin = Call POST "/api/Auth/login" @{ email = $AdminEmail; password = $AdminPassword }
if ($admin.Status -ne 200) {
    Write-Host "Admin login fail ($($admin.Status)). API chal rahi hai? Admin seed hua? (user-secrets check karo)" -ForegroundColor Red
    exit 1
}
$adminToken = $admin.Json.token
$adminId = $admin.Json.user.id
Check "Admin login, role = Admin" ($admin.Json.user.role -eq "Admin")

$r = Call GET "/api/Products"
ExpectStatus "Bina token GET /api/Products -> 401" $r 401

$staffEmail = "staff$run@test.com"
$r = Call POST "/api/Auth/register" @{ fullName = "Staff $run"; email = $staffEmail; password = $pw }
ExpectStatus "Register staff -> 200" $r 200
Check "Registered role = Staff" ($r.Json.role -eq "Staff")
$staffId = $r.Json.id

$r = Call POST "/api/Auth/register" @{ fullName = "Staff $run"; email = $staffEmail; password = $pw }
ExpectStatus "Duplicate email -> 400" $r 400

$r = Call POST "/api/Auth/register" @{ fullName = "Weak"; email = "weak$run@test.com"; password = "abc" }
ExpectStatus "Weak password -> 400" $r 400

$r = Call POST "/api/Auth/login" @{ email = $staffEmail; password = $pw }
ExpectStatus "Staff login -> 200" $r 200
$staffToken = $r.Json.token

$r = Call POST "/api/Auth/login" @{ email = $staffEmail; password = "Wrong@1234" }
ExpectStatus "Galat password login -> 401" $r 401

$r = Call GET "/api/Auth/me"
ExpectStatus "/me bina token -> 401" $r 401

$r = Call GET "/api/Auth/me" $null $staffToken
ExpectStatus "/me staff token -> 200" $r 200
Check "/me email match" ($r.Json.email -eq $staffEmail)

# =====================================================================
Section "ROLES"
# =====================================================================
$r = Call POST "/api/Products" @{ name = "X"; sku = "x-$run"; price = 10; costPrice = 5; reorderLevel = 1; categoryId = 1 } $staffToken
ExpectStatus "Staff POST product -> 403" $r 403

$r = Call GET "/api/Reports/dashboard" $null $staffToken
ExpectStatus "Staff reports -> 403" $r 403

$r = Call GET "/api/Users" $null $staffToken
ExpectStatus "Staff GET users -> 403" $r 403

$r = Call GET "/api/Users" $null $adminToken
ExpectStatus "Admin GET users -> 200" $r 200

$mgrEmail = "manager$run@test.com"
$r = Call POST "/api/Users" @{ fullName = "Manager $run"; email = $mgrEmail; password = $pw; roleId = 2 } $adminToken
ExpectStatus "Admin creates Manager -> 200" $r 200
Check "Created role = Manager" ($r.Json.role -eq "Manager")

$r = Call POST "/api/Auth/login" @{ email = $mgrEmail; password = $pw }
ExpectStatus "Manager login -> 200" $r 200
$mgrToken = $r.Json.token

$r = Call GET "/api/Reports/dashboard" $null $mgrToken
ExpectStatus "Manager reports -> 200" $r 200

$r = Call GET "/api/Users" $null $mgrToken
ExpectStatus "Manager GET users -> 403" $r 403

# =====================================================================
Section "CATEGORIES & PRODUCTS"
# =====================================================================
$r = Call POST "/api/Categories" @{ name = "Cat $run"; description = "smoke test" } $adminToken
ExpectStatus "Create category -> 201" $r 201
$catId = $r.Json.id

$r = Call POST "/api/Categories" @{ name = ""; description = "x" } $adminToken
ExpectStatus "Empty category name -> 400" $r 400

$r = Call GET "/api/Categories" $null $staffToken
ExpectStatus "Staff GET categories -> 200" $r 200

$sku = "sku-$run"
$r = Call POST "/api/Products" @{ name = "Mouse $run"; sku = $sku; description = "test"; price = 2500; costPrice = 1800; reorderLevel = 10; categoryId = $catId } $adminToken
ExpectStatus "Create product -> 201" $r 201
Check "SKU uppercase" ($r.Json.sku -ceq $sku.ToUpper())
Check "Initial quantity = 0" ($r.Json.quantityOnHand -eq 0)
$pid1 = $r.Json.id

$r = Call POST "/api/Products" @{ name = "Dup"; sku = $sku; price = 10; costPrice = 5; reorderLevel = 1; categoryId = $catId } $adminToken
ExpectStatus "Duplicate SKU -> 400" $r 400

$r = Call POST "/api/Products" @{ name = "NoCat"; sku = "nc-$run"; price = 10; costPrice = 5; reorderLevel = 1; categoryId = 999999 } $adminToken
ExpectStatus "Invalid category -> 404" $r 404

$r = Call POST "/api/Products" @{ name = "Neg"; sku = "neg-$run"; price = -5; costPrice = 5; reorderLevel = 1; categoryId = $catId } $adminToken
ExpectStatus "Negative price -> 400" $r 400

$r = Call POST "/api/Products" @{ name = "Keyboard $run"; sku = "kb-$run"; price = 3500; costPrice = 2500; reorderLevel = 5; categoryId = $catId } $mgrToken
ExpectStatus "Manager create product -> 201" $r 201

$r = Call GET "/api/Products/$pid1" $null $staffToken
ExpectStatus "GET product by id -> 200" $r 200

$r = Call GET "/api/Products/999999" $null $staffToken
ExpectStatus "GET unknown product -> 404" $r 404

$r = Call PUT "/api/Products/$pid1" @{ name = "Mouse Pro $run"; description = "updated"; price = 3000; costPrice = 2000; reorderLevel = 10; isActive = $true; categoryId = $catId } $adminToken
ExpectStatus "Update product -> 200" $r 200
$price = [decimal]$r.Json.price
Check "Price updated to 3000" ($price -eq 3000)

$r = Call GET "/api/Products?search=$sku" $null $staffToken
ExpectStatus "Search products -> 200" $r 200
Check "Search finds the product" ($r.Json.totalCount -ge 1)

$r = Call GET "/api/Products?pageSize=5000" $null $staffToken
Check "pageSize clamped to 100" ($r.Json.pageSize -eq 100)

$r = Call GET "/api/Products?pageSize=1&page=1" $null $staffToken
Check "pageSize=1 returns max 1 item" (@($r.Json.items).Count -le 1)
Check "totalPages present" ($r.Json.totalPages -ge 1)

$r = Call GET "/api/Products?categoryId=$catId" $null $staffToken
Check "Filter by category" ($r.Json.totalCount -ge 2)

# =====================================================================
Section "INVENTORY"
# =====================================================================
$r = Call GET "/api/Inventory/$pid1" $null $staffToken
ExpectStatus "GET inventory -> 200" $r 200
Check "Quantity = 0 and low stock" (($r.Json.quantityOnHand -eq 0) -and ($r.Json.isLowStock -eq $true))

$r = Call POST "/api/Inventory/stock-in" @{ productId = $pid1; quantity = 50; reference = "PO-$run"; note = "initial" } $staffToken
ExpectStatus "Staff stock-in -> 200" $r 200
Check "Quantity = 50" ($r.Json.quantityOnHand -eq 50)

$r = Call POST "/api/Inventory/stock-out" @{ productId = $pid1; quantity = 10; reference = "SALE-$run" } $staffToken
ExpectStatus "Staff stock-out -> 200" $r 200
Check "Quantity = 40" ($r.Json.quantityOnHand -eq 40)

$r = Call POST "/api/Inventory/stock-out" @{ productId = $pid1; quantity = 99999 } $staffToken
ExpectStatus "Insufficient stock -> 400" $r 400

$r = Call POST "/api/Inventory/stock-in" @{ productId = $pid1; quantity = 0 } $staffToken
ExpectStatus "Zero quantity -> 400" $r 400

$r = Call GET "/api/Inventory/999999" $null $staffToken
ExpectStatus "Unknown inventory -> 404" $r 404

$r = Call POST "/api/Inventory/adjust" @{ productId = $pid1; newQuantity = 5; note = "count" } $staffToken
ExpectStatus "Staff adjust -> 403" $r 403

$r = Call POST "/api/Inventory/adjust" @{ productId = $pid1; newQuantity = -1 } $adminToken
ExpectStatus "Adjust negative -> 400" $r 400

$r = Call POST "/api/Inventory/adjust" @{ productId = $pid1; newQuantity = 5; note = "stock count" } $adminToken
ExpectStatus "Admin adjust to 5 -> 200" $r 200
Check "Low stock flag true" ($r.Json.isLowStock -eq $true)

$r = Call GET "/api/Inventory/low-stock" $null $staffToken
ExpectStatus "Low-stock list -> 200" $r 200
Check "Product in low-stock list" (@($r.Json | Where-Object { $_.productId -eq $pid1 }).Count -eq 1)

$r = Call GET "/api/Inventory/movements?productId=$pid1" $null $mgrToken
ExpectStatus "Manager movements -> 200" $r 200
Check "At least 3 movements" (@($r.Json).Count -ge 3)

$r = Call GET "/api/Inventory/movements?productId=$pid1" $null $staffToken
ExpectStatus "Staff movements -> 403" $r 403

$r = Call POST "/api/Inventory/adjust" @{ productId = $pid1; newQuantity = 100; note = "prepare for orders" } $adminToken
ExpectStatus "Adjust to 100 -> 200" $r 200

# =====================================================================
Section "ORDERS"
# =====================================================================
$r = Call POST "/api/Orders" @{ customerName = "Ali Khan"; customerEmail = "ali@test.com"; items = @(@{ productId = $pid1; quantity = 3 }) } $staffToken
ExpectStatus "Staff creates order -> 201" $r 201
Check "Status = Pending" ($r.Json.status -eq "Pending")
Check "Total = 3 x price" ([decimal]$r.Json.totalAmount -eq (3 * $price))
$o1 = $r.Json.id

$r = Call GET "/api/Inventory/$pid1" $null $staffToken
Check "Stock reduced to 97" ($r.Json.quantityOnHand -eq 97)

$r = Call POST "/api/Orders" @{ customerName = "Big"; items = @(@{ productId = $pid1; quantity = 99999 }) } $staffToken
ExpectStatus "Insufficient stock order -> 400" $r 400

$r = Call GET "/api/Inventory/$pid1" $null $staffToken
Check "Stock unchanged after failed order (all-or-nothing)" ($r.Json.quantityOnHand -eq 97)

$r = Call POST "/api/Orders" @{ customerName = "Empty"; items = @() } $staffToken
ExpectStatus "Empty items -> 400" $r 400

$r = Call POST "/api/Orders" @{ customerName = "Ghost"; items = @(@{ productId = 999999; quantity = 1 }) } $staffToken
ExpectStatus "Unknown product order -> 404" $r 404

$r = Call GET "/api/Orders/999999" $null $staffToken
ExpectStatus "Unknown order -> 404" $r 404

$r = Call POST "/api/Orders/$o1/confirm" $null $staffToken
ExpectStatus "Staff confirm -> 403" $r 403

$r = Call POST "/api/Orders/$o1/ship" $null $adminToken
ExpectStatus "Ship before confirm -> 400" $r 400

$r = Call POST "/api/Orders/$o1/confirm" $null $adminToken
ExpectStatus "Confirm -> 200" $r 200
Check "Status = Confirmed" ($r.Json.status -eq "Confirmed")

$r = Call POST "/api/Orders/$o1/ship" $null $mgrToken
ExpectStatus "Manager ship -> 200" $r 200

$r = Call POST "/api/Orders/$o1/deliver" $null $mgrToken
ExpectStatus "Deliver -> 200" $r 200
Check "Status = Delivered" ($r.Json.status -eq "Delivered")

$r = Call POST "/api/Orders/$o1/cancel" $null $adminToken
ExpectStatus "Cancel delivered order -> 400" $r 400

$r = Call GET "/api/Orders?status=Delivered&search=Ali" $null $staffToken
ExpectStatus "Filter orders -> 200" $r 200
Check "Delivered order found" (@($r.Json.items | Where-Object { $_.id -eq $o1 }).Count -eq 1)

# =====================================================================
Section "PAYMENTS & CANCEL"
# =====================================================================
$r = Call POST "/api/Orders" @{ customerName = "Sara"; items = @(@{ productId = $pid1; quantity = 5 }) } $staffToken
ExpectStatus "Second order -> 201" $r 201
$o2 = $r.Json.id
$total2 = [decimal]$r.Json.totalAmount

$r = Call GET "/api/Inventory/$pid1" $null $staffToken
Check "Stock = 92" ($r.Json.quantityOnHand -eq 92)

$r = Call GET "/api/Orders/$o2/payments" $null $staffToken
ExpectStatus "GET payments -> 200" $r 200
Check "State = Unpaid" ($r.Json.paymentState -eq "Unpaid")

$r = Call POST "/api/Orders/$o2/payments" @{ amount = 1000; method = "Cash" } $staffToken
ExpectStatus "Partial payment -> 200" $r 200
Check "State = PartiallyPaid" ($r.Json.paymentState -eq "PartiallyPaid")

$r = Call POST "/api/Orders/$o2/payments" @{ amount = ($total2 + 1); method = "Card" } $staffToken
ExpectStatus "Overpayment -> 400" $r 400

$r = Call POST "/api/Orders/$o2/payments" @{ amount = 0; method = "Cash" } $staffToken
ExpectStatus "Zero payment -> 400" $r 400

$r = Call POST "/api/Orders/$o2/payments" @{ amount = ($total2 - 1000); method = "JazzCash" } $staffToken
ExpectStatus "Remaining payment -> 200" $r 200
Check "State = Paid, balance 0" (($r.Json.paymentState -eq "Paid") -and ([decimal]$r.Json.balanceDue -eq 0))

$r = Call POST "/api/Orders/$o2/cancel" $null $adminToken
ExpectStatus "Cancel order -> 200" $r 200
Check "Status = Cancelled" ($r.Json.status -eq "Cancelled")

$r = Call GET "/api/Inventory/$pid1" $null $staffToken
Check "Stock restored to 97" ($r.Json.quantityOnHand -eq 97)

$r = Call GET "/api/Orders/$o2/payments" $null $staffToken
Check "Payments refunded" ($r.Json.paymentState -eq "Refunded")

$r = Call POST "/api/Orders/$o2/payments" @{ amount = 100; method = "Cash" } $staffToken
ExpectStatus "Payment on cancelled order -> 400" $r 400

$r = Call GET "/api/Inventory/movements?productId=$pid1&take=50" $null $adminToken
Check "Return movement exists" (@($r.Json | Where-Object { $_.type -eq "Return" }).Count -ge 1)

$r = Call POST "/api/Orders/999999/payments" @{ amount = 100; method = "Cash" } $staffToken
ExpectStatus "Payment on unknown order -> 404" $r 404

# =====================================================================
Section "REPORTS"
# =====================================================================
$r = Call GET "/api/Reports/dashboard" $null $adminToken
ExpectStatus "Dashboard -> 200" $r 200
Check "Dashboard has products" ($r.Json.totalProducts -ge 1)

$r = Call GET "/api/Reports/sales-summary" $null $adminToken
ExpectStatus "Sales summary -> 200" $r 200
Check "Order count >= 1" ($r.Json.orderCount -ge 1)

$r = Call GET "/api/Reports/top-products?take=3" $null $mgrToken
ExpectStatus "Top products -> 200" $r 200
Check "Top products max 3" (@($r.Json).Count -le 3)

$r = Call GET "/api/Reports/sales-summary?from=2026-11-10&to=2026-11-01" $null $adminToken
ExpectStatus "Invalid date range -> 400" $r 400

$r = Call GET "/api/Reports/sales-summary?from=2024-01-01&to=2026-11-01" $null $adminToken
ExpectStatus "Range over 1 year -> 400" $r 400

# =====================================================================
Section "USER MANAGEMENT & CLEANUP"
# =====================================================================
$r = Call PUT "/api/Users/$adminId/role" @{ roleId = 2 } $adminToken
ExpectStatus "Admin cannot change own role -> 400" $r 400

$r = Call PUT "/api/Users/$adminId/active" @{ isActive = $false } $adminToken
ExpectStatus "Admin cannot deactivate self -> 400" $r 400

$r = Call PUT "/api/Users/$staffId/role" @{ roleId = 2 } $adminToken
ExpectStatus "Admin changes staff role -> 200" $r 200

$r = Call PUT "/api/Users/$staffId/active" @{ isActive = $false } $adminToken
ExpectStatus "Admin deactivates staff -> 200" $r 200

$r = Call POST "/api/Auth/login" @{ email = $staffEmail; password = $pw }
ExpectStatus "Deactivated user login -> 401" $r 401

$r = Call DELETE "/api/Products/$pid1" $null $mgrToken
ExpectStatus "Manager delete product -> 403" $r 403

$r = Call DELETE "/api/Products/$pid1" $null $adminToken
ExpectStatus "Admin delete product -> 204" $r 204

$r = Call GET "/api/Products/$pid1" $null $adminToken
Check "Soft deleted (isActive = false)" ($r.Json.isActive -eq $false)

$r = Call GET "/api/Products?search=$sku" $null $adminToken
Check "Deleted product hidden from list" ($r.Json.totalCount -eq 0)

# ---------- Summary ----------
Write-Host ""
$total = $script:Pass + $script:Fail
$color = if ($script:Fail -eq 0) { "Green" } else { "Yellow" }
Write-Host "RESULT: $($script:Pass) / $total passed, $($script:Fail) failed" -ForegroundColor $color
if ($script:Fail -gt 0) { exit 1 }
