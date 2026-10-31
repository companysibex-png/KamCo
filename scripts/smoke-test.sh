#!/bin/bash
# KamCo API smoke test — exercises all endpoints and prints PASS/FAIL per step.
BASE="http://localhost:5000/api"
PASS=0; FAIL=0

check() { # $1=name $2=expected_status $3=actual_status
  if [ "$2" = "$3" ]; then PASS=$((PASS+1)); echo "PASS  [$3] $1";
  else FAIL=$((FAIL+1)); echo "FAIL  $1 — expected $2, got $3"; fi
}

J() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)" 2>/dev/null; }

echo "=== 1. Public catalog ==="
S=$(curl -s -o /tmp/p.json -w '%{http_code}' "$BASE/products")
check "GET /products (public)" 200 "$S"
TOTAL=$(J "['totalCount']" < /tmp/p.json); echo "      products seeded: $TOTAL"

S=$(curl -s -o /tmp/s.json -w '%{http_code}' "$BASE/products?search=keyboard")
check "GET /products?search=keyboard" 200 "$S"
N=$(J "['totalCount']" < /tmp/s.json); echo "      search hits: $N"

S=$(curl -s -o /tmp/s2.json -w '%{http_code}' "$BASE/products?categoryId=1&sortBy=price_asc")
check "GET /products?categoryId=1&sortBy=price_asc" 200 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' "$BASE/categories")
check "GET /categories (public)" 200 "$S"

echo "=== 2. Auth ==="
S=$(curl -s -o /tmp/admin.json -w '%{http_code}' -X POST "$BASE/auth/login" -H 'Content-Type: application/json' -d '{"email":"admin@kamco.com","password":"Admin@123"}')
check "POST /auth/login (admin)" 200 "$S"
ADMIN_TOKEN=$(J "['token']" < /tmp/admin.json)
echo "      admin role: $(J "['role']" < /tmp/admin.json)"

S=$(curl -s -o /tmp/sara.json -w '%{http_code}' -X POST "$BASE/auth/login" -H 'Content-Type: application/json' -d '{"email":"sara@kamco.com","password":"User@123"}')
check "POST /auth/login (customer)" 200 "$S"
SARA_TOKEN=$(J "['token']" < /tmp/sara.json)

S=$(curl -s -o /tmp/reg.json -w '%{http_code}' -X POST "$BASE/auth/register" -H 'Content-Type: application/json' -d '{"fullName":"Ali Test","email":"ali@test.com","password":"Test@123"}')
check "POST /auth/register (new user)" 200 "$S"
ALI_TOKEN=$(J "['token']" < /tmp/reg.json)

S=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/auth/register" -H 'Content-Type: application/json' -d '{"fullName":"Ali Dup","email":"ali@test.com","password":"Test@123"}')
check "POST /auth/register (duplicate email -> 409)" 409 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/auth/login" -H 'Content-Type: application/json' -d '{"email":"ali@test.com","password":"WRONG"}')
check "POST /auth/login (wrong password -> 400)" 400 "$S"

echo "=== 3. Category & product management (Admin only) ==="
S=$(curl -s -o /tmp/cat.json -w '%{http_code}' -X POST "$BASE/categories" -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' -d '{"name":"Sports","description":"Sports gear"}')
check "POST /categories (admin)" 201 "$S"
SPORTS_ID=$(J "['id']" < /tmp/cat.json)

S=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/categories" -H "Authorization: Bearer $SARA_TOKEN" -H 'Content-Type: application/json' -d '{"name":"Hack"}')
check "POST /categories as normal user -> 403" 403 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/products" -H 'Content-Type: application/json' -d '{"name":"X","price":1,"stockQuantity":1,"categoryId":1}')
check "POST /products without token -> 401" 401 "$S"

S=$(curl -s -o /tmp/prod.json -w '%{http_code}' -X POST "$BASE/products" -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' -d '{"name":"Yoga Mat Pro","description":"6mm non-slip mat","price":24.9,"stockQuantity":50,"categoryId":'$SPORTS_ID'}')
check "POST /products (admin)" 201 "$S"
MAT_ID=$(J "['id']" < /tmp/prod.json); echo "      new product id: $MAT_ID"

S=$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$BASE/products/$MAT_ID" -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' -d '{"name":"Yoga Mat Pro v2","description":"6mm","price":26.9,"stockQuantity":45,"categoryId":'$SPORTS_ID',"isActive":true}')
check "PUT /products/{id} (admin update)" 200 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' "$BASE/products/$MAT_ID")
check "GET /products/{id} after update" 200 "$S"
echo "      name now: $(curl -s "$BASE/products/$MAT_ID" | J "['name']")"

echo "=== 4. Orders ==="
S=$(curl -s -o /tmp/order.json -w '%{http_code}' -X POST "$BASE/orders" -H "Authorization: Bearer $SARA_TOKEN" -H 'Content-Type: application/json' -d '{"items":[{"productId":1,"quantity":2},{"productId":4,"quantity":1}]}')
check "POST /orders (customer)" 201 "$S"
echo "      order total: $(J "['totalAmount']" < /tmp/order.json) (expect 89.99*2 + 32.99 = 212.97)"

S=$(curl -s -o /dev/null -w '%{http_code}' "$BASE/orders/my" -H "Authorization: Bearer $SARA_TOKEN")
check "GET /orders/my (customer)" 200 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' "$BASE/orders" -H "Authorization: Bearer $SARA_TOKEN")
check "GET /orders as customer (admin-only) -> 403" 403 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' "$BASE/orders/1" -H "Authorization: Bearer $ALI_TOKEN")
check "GET /orders/1 of another user -> 403" 403 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' -X PATCH "$BASE/orders/1/status" -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' -d '{"status":"Paid"}')
check "PATCH /orders/1/status -> Paid (admin)" 200 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/orders" -H "Authorization: Bearer $SARA_TOKEN" -H 'Content-Type: application/json' -d '{"items":[{"productId":1,"quantity":99999}]}')
check "POST /orders exceeding stock -> 400" 400 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' -X PATCH "$BASE/orders/1/status" -H "Authorization: Bearer $SARA_TOKEN" -H 'Content-Type: application/json' -d '{"status":"Shipped"}')
check "PATCH /orders/1/status as customer -> 403" 403 "$S"

echo "=== 5. Delete flows ==="
S=$(curl -s -o /dev/null -w '%{http_code}' -X DELETE "$BASE/products/$MAT_ID" -H "Authorization: Bearer $ADMIN_TOKEN")
check "DELETE /products/{id} (admin)" 204 "$S"

S=$(curl -s -o /dev/null -w '%{http_code}' -X DELETE "$BASE/categories/1" -H "Authorization: Bearer $ADMIN_TOKEN")
check "DELETE non-empty category -> 400" 400 "$S"

echo ""
echo "RESULT: $PASS passed, $FAIL failed"
