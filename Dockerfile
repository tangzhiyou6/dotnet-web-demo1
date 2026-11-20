# syntax=docker/dockerfile:1
# =====================================================================
#  ASP.NET Core 11 (MVC + 内嵌 Blazor Server) —— 多阶段构建
#  目标架构：linux/amd64
# =====================================================================

# ---------- 构建阶段 ----------
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# 先只复制项目文件做还原，充分利用 Docker 层缓存（NuGet 包下载走缓存层）
COPY src/DotnetWebDemo.Web/DotnetWebDemo.Web.csproj src/DotnetWebDemo.Web/
RUN dotnet restore src/DotnetWebDemo.Web/DotnetWebDemo.Web.csproj

# 再复制源码并发布
#
# 注意：此处**不要**加 --no-restore。
# ASP.NET Core 的静态资源清单(StaticWebAssetsManifest)是在还原阶段生成的，
# 上面的分步还原时项目目录里还没有 wwwroot，生成的是不完整的清单，
# 会导致 Microsoft.AspNetCore.App.Internal.Assets 提供的
# wwwroot/_framework/blazor.web.js(Blazor Server 客户端脚本)被漏掉，
# 表现为页面能打开但 Blazor 组件完全失去交互(脚本 404)。
# 保留 publish 的隐式还原即可(NuGet 缓存已就绪，几乎不额外耗时)。
COPY . .
RUN dotnet publish src/DotnetWebDemo.Web/DotnetWebDemo.Web.csproj \
        --configuration Release \
        --output /app/publish \
        /p:UseAppHost=false

# ---------- 运行阶段 ----------
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=Asia/Shanghai

EXPOSE 8080

COPY --from=build /app/publish .

# 预创建 DataProtection 密钥目录并授权给 app 用户，
# 配合 compose 中的命名卷即可在容器重启后保留登录 Cookie
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app/.aspnet

# 以非 root 用户运行（.NET 8+ 镜像内置 app 用户）
USER app

ENTRYPOINT ["dotnet", "DotnetWebDemo.Web.dll"]
