#!/usr/bin/env bash
# =====================================================================
#  端到端冒烟测试：验证登录 / 注册 / 用户页面 / Blazor 资源可用
#  用法：bash scripts/smoke-test.sh [BASE_URL]
#  默认 BASE_URL=http://localhost:8080
# =====================================================================
set -uo pipefail

BASE_URL="${1:-http://localhost:8080}"
JAR="$(mktemp -t dw-cookie-XXXXXX)"
TMP="$(mktemp -d)"
PASS=0
FAIL=0

cleanup() { rm -rf "$JAR" "$TMP"; }
trap cleanup EXIT

ok()   { printf '  \033[32m✔\033[0m %s\n' "$1"; PASS=$((PASS+1)); }
bad()  { printf '  \033[31m✘\033[0m %s\n' "$1"; FAIL=$((FAIL+1)); }
head2() { printf '\n\033[1m%s\033[0m\n' "$1"; }

# 从页面中抓取防伪令牌
token_from() {
  grep -oE 'name="__RequestVerificationToken"[^>]*value="[^"]+"' "$1" \
    | head -1 | sed -E 's/.*value="([^"]+)".*/\1/'
}

# 抓取表单里的 returnUrl / action
field_from() {
  grep -oE "name=\"$2\"[^>]*value=\"[^\"]*\"" "$1" | head -1 | sed -E "s/.*value=\"([^\"]*)\".*/\1/"
}

head2 "1) 首页可访问"
code=$(curl -s -o "$TMP/home.html" -w '%{http_code}' "$BASE_URL/")
[ "$code" = "200" ] && ok "GET / → 200" || bad "GET / → $code"

if grep -q "欢迎使用 Blazor + MVC 用户系统" "$TMP/home.html"; then
  ok "首页标题渲染正确"
else
  bad "首页标题未找到"
fi

if grep -q "stat-card" "$TMP/home.html"; then
  ok "Blazor 仪表盘组件已预渲染（stat-card）"
else
  bad "Blazor 仪表盘组件未预渲染"
fi

head2 "2) Blazor Server 客户端资源"
code=$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/_framework/blazor.web.js")
[ "$code" = "200" ] && ok "GET /_framework/blazor.web.js → 200" || bad "GET /_framework/blazor.web.js → $code"

code=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/_blazor/negotiate?negotiateVersion=1")
case "$code" in
  200|401|404|405) ok "电路端点 /_blazor/negotiate 可达（$code）" ;;
  *) bad "/_blazor/negotiate → $code" ;;
esac

head2 "3) 未登录访问 /Users 应跳转登录页"
code=$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/Users")
if [ "$code" = "302" ] || [ "$code" = "401" ]; then
  ok "GET /Users → $code（受保护）"
else
  bad "GET /Users → $code，预期 302/401"
fi

head2 "4) 使用 admin 登录"
curl -s -c "$JAR" -b "$JAR" -o "$TMP/login.html" "$BASE_URL/Account/Login"
TOKEN=$(token_from "$TMP/login.html")
[ -n "$TOKEN" ] && ok "获取到防伪令牌" || bad "未找到防伪令牌"

code=$(curl -s -c "$JAR" -b "$JAR" -o "$TMP/login-post.html" -w '%{http_code}' \
  -X POST "$BASE_URL/Account/Login" \
  --data-urlencode "UserName=admin" \
  --data-urlencode "Password=Admin@123" \
  --data-urlencode "RememberMe=false" \
  --data-urlencode "__RequestVerificationToken=$TOKEN")
if [ "$code" = "302" ]; then
  ok "POST /Account/Login → 302（登录成功）"
else
  bad "POST /Account/Login → $code，预期 302"
fi

if grep -q "AspNetCore.Identity.Application" "$JAR"; then
  ok "已写入身份认证 Cookie"
else
  bad "未找到身份认证 Cookie"
fi

head2 "5) 登录后访问用户页面"
curl -s -c "$JAR" -b "$JAR" -o "$TMP/users.html" "$BASE_URL/Users"
if grep -q "用户管理" "$TMP/users.html"; then
  ok "GET /Users → 用户管理页渲染成功"
else
  bad "用户管理页渲染失败"
fi

if grep -q "table" "$TMP/users.html" && grep -q "admin" "$TMP/users.html"; then
  ok "用户表格已预渲染且包含 admin 用户"
else
  bad "用户表格内容缺失"
fi

head2 "6) 注册新用户（使用独立会话，避免已登录状态下注册页被重定向）"
JAR2="$(mktemp -t dw-cookie2-XXXXXX)"
curl -s -c "$JAR2" -b "$JAR2" -o "$TMP/reg.html" "$BASE_URL/Account/Register"
TOKEN=$(token_from "$TMP/reg.html")
[ -n "$TOKEN" ] && ok "获取到注册页防伪令牌" || bad "未找到注册页防伪令牌"
NEWUSER="smoke$(date +%H%M%S)"
code=$(curl -s -c "$JAR2" -b "$JAR2" -o "$TMP/reg-post.html" -w '%{http_code}' \
  -X POST "$BASE_URL/Account/Register" \
  --data-urlencode "UserName=$NEWUSER" \
  --data-urlencode "Email=$NEWUSER@example.com" \
  --data-urlencode "FullName=冒烟测试用户" \
  --data-urlencode "Department=QA" \
  --data-urlencode "Password=Smoke@12345" \
  --data-urlencode "ConfirmPassword=Smoke@12345" \
  --data-urlencode "__RequestVerificationToken=$TOKEN")
if [ "$code" = "302" ]; then
  ok "POST /Account/Register → 302（注册成功并自动登录）"
else
  bad "POST /Account/Register → $code，预期 302"
fi

if curl -s -b "$JAR2" "$BASE_URL/Users" | grep -q "$NEWUSER"; then
  ok "新注册用户已出现在用户列表中"
else
  bad "新注册用户未出现在用户列表中"
fi
rm -f "$JAR2"

head2 "7) 退出登录"
curl -s -c "$JAR" -b "$JAR" -o "$TMP/logout.html" "$BASE_URL/"
TOKEN=$(token_from "$TMP/logout.html")
code=$(curl -s -c "$JAR" -b "$JAR" -o /dev/null -w '%{http_code}' \
  -X POST "$BASE_URL/Account/Logout" \
  --data-urlencode "__RequestVerificationToken=$TOKEN")
[ "$code" = "302" ] && ok "POST /Account/Logout → 302" || bad "POST /Account/Logout → $code"

printf '\n\033[1m结果：\033[0m 通过 %d 项，失败 %d 项\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
