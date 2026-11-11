#!/bin/bash

# Builds the solution and starts every service. Run ./tools/local-development/up.sh first.
# Logs of each service are stored in tools/local-development/logs (not commited in git).
# Press Ctrl+C to stop all the services.

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
LOGS="$ROOT/tools/local-development/logs"

PROJECTS=(
  src/Services/Subscriptions/Scattered.Services.Subscriptions
  src/Services/Subscriptions/Scattered.Services.Subscriptions.Consumer
  src/Services/Emails/Scattered.Services.Emails
  src/Services/Products/Scattered.Services.Products.Api.Write
  src/Services/Products/Scattered.Services.Products.Api.Read
  src/Services/Products/Scattered.Services.Products.Consumer
  src/Services/Orders/Scattered.Services.Orders
  src/Services/Orders/Scattered.Services.Orders.Consumer
  src/Api/Public/Scattered.API.Public
  src/Api/Private/Scattered.API.Private
)

cd "$ROOT" || exit 1
mkdir -p "$LOGS"

echo build solution
dotnet build Scattered.slnx --nologo -v q || exit 1

trap 'echo stopping services; kill $(jobs -p) 2>/dev/null' EXIT

for project in "${PROJECTS[@]}"; do
  name=$(basename "$project")
  echo "starting $name"
  dotnet run --no-build --project "$project" > "$LOGS/$name.log" 2>&1 &
done

echo "all services started, logs in $LOGS"
wait
