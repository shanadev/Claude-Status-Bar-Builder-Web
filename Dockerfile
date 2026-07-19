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
# The CLIENT publish is what runs Blazor's fingerprinting + index.html
# placeholder substitution — the server publish alone would ship a raw
# index.html with a literal #[.{fingerprint}] boot script (dead site).
# Publish both and overlay the client's processed wwwroot onto the server.
RUN dotnet publish src/StatusBar.Web -c Release -o /client --nologo
RUN dotnet publish src/StatusBar.Server -c Release -o /app --nologo
RUN rm -rf /app/wwwroot && cp -r /client/wwwroot /app/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "StatusBar.Server.dll"]
