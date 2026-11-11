#!/bin/bash
# Recent consul versions reject addresses with a scheme (http://...) in the CLI flags,
# so those services are registered through the HTTP API instead.
register_with_scheme() {
  curl -s -X PUT http://localhost:8500/v1/agent/service/register \
    -d "{\"Name\":\"$1\",\"Address\":\"$2\",\"Port\":$3}" && echo "Registered service: $1"
}

# Infrastructure
docker exec -it consul consul services register -name=RabbitMQ -address=localhost
register_with_scheme SecretManager http://localhost 8200
docker exec -it consul consul services register -name=MySql -address=localhost -port=3307
docker exec -it consul consul services register -name=MongoDb -address=localhost -port=27017
docker exec -it consul consul services register -name=Graylog --address=localhost -port=12201
docker exec -it consul consul services register -name=OpenTelemetryCollector --address=localhost -port=4317

# Services
register_with_scheme EmailsApi http://localhost 60120
register_with_scheme ProductsApiWrite https://localhost 60320
register_with_scheme ProductsApiRead https://localhost 60321
register_with_scheme OrdersApi http://localhost 60220
register_with_scheme SubscriptionsApi http://localhost 60420
