#!/bin/zsh
# Uso: run-local.sh <dir 45064123_App> <puerto_api> <puerto_web> <archivo.db>
set -e
APP=$1; API=$2; WEB=$3; DB=$4
export PATH=$HOME/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 ASPNETCORE_ENVIRONMENT=Development
echo "{\"ApiBaseUrl\": \"http://localhost:$API/\"}" > $APP/src/PSAG.Mantenimiento.Web/wwwroot/appsettings.Development.json
grep -q "appsettings.Development.json" $(git -C $APP rev-parse --path-format=absolute --git-common-dir)/info/exclude 2>/dev/null || echo "45064123_App/src/PSAG.Mantenimiento.Web/wwwroot/appsettings.Development.json" >> $(git -C $APP rev-parse --path-format=absolute --git-common-dir)/info/exclude
LOG=$(dirname $DB)
ConnectionStrings__DefaultConnection="Data Source=$DB" Cors__AllowedOrigins__0="http://localhost:$WEB" \
  dotnet run --project $APP/src/PSAG.Mantenimiento.Api --no-launch-profile --urls http://localhost:$API > $LOG/api-$API.log 2>&1 &
dotnet run --project $APP/src/PSAG.Mantenimiento.Web --no-launch-profile --urls http://localhost:$WEB > $LOG/web-$WEB.log 2>&1 &
for i in {1..90}; do curl -sf -o /dev/null http://localhost:$WEB/ && curl -s -o /dev/null http://localhost:$API/ && break; sleep 2; done
echo "api http://localhost:$API  web http://localhost:$WEB  db $DB"
