# Hack the Claude Status Bar — Railway/nginx container for the Blazor WASM site.
# Copyright (C) 2026 Stephen Shanafelt
# SPDX-License-Identifier: GPL-3.0-only
#
# Stage 1 publishes the static Blazor WebAssembly output; stage 2 serves it with
# nginx. Railway auto-detects this Dockerfile and injects PORT at runtime.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/StatusBar.Web -c Release -o /app --nologo
# nginx here has no brotli module; the .gz variants are served via gzip_static.
RUN find /app/wwwroot -name '*.br' -delete

FROM nginx:alpine
COPY --from=build /app/wwwroot /usr/share/nginx/html
# The nginx image runs envsubst over templates/*.template at startup, so the
# server can bind Railway's PORT.
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
ENV PORT=80
EXPOSE 80
