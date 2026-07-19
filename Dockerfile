# Hack the Claude Status Bar — Railway container: ASP.NET Core host.
# Copyright (C) 2026 Stephen Shanafelt
# SPDX-License-Identifier: GPL-3.0-only
#
# Stage 1 publishes StatusBar.Server (which embeds the Blazor WASM site);
# stage 2 runs it on the aspnet runtime image. Railway auto-detects this
# Dockerfile and injects PORT at runtime; Program.cs binds it.
#
# The template-submission queue expects a persistent volume mounted at /data
# (Railway → service → Volumes) with SUBMISSIONS_PATH=/data/submissions.jsonl
# and SUBMIT_ADMIN_TOKEN set in the service variables.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/StatusBar.Server -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "StatusBar.Server.dll"]
