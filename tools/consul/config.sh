#!/bin/bash

## Registra en Consul los servicios que se resuelven por nombre.
## Se usa la API HTTP del agente en lugar de `consul services register -address=... -port=...`:
## las versiones actuales de la CLI rechazan una dirección con esquema ("http://localhost")
## porque la interpretan como host:puerto, y el código espera ese esquema para montar la URL.
register() {
    docker exec consul curl -sf -X PUT http://localhost:8500/v1/agent/service/register -d "$1" \
        && echo "Registered service: $(echo "$1" | sed 's/.*"Name":"\([^"]*\)".*/\1/')"
}

# Infrastructure
register '{"Name":"RabbitMQ","Address":"localhost"}'
register '{"Name":"SecretManager","Address":"http://localhost","Port":8200}'
register '{"Name":"MySql","Address":"localhost","Port":3307}'
register '{"Name":"MongoDb","Address":"localhost","Port":27017}'
register '{"Name":"Graylog","Address":"localhost","Port":12201}'
register '{"Name":"OpenTelemetryCollector","Address":"localhost","Port":4317}'

# Services
register '{"Name":"EmailsApi","Address":"http://localhost","Port":60120}'
register '{"Name":"ProductsApiWrite","Address":"https://localhost","Port":60320}'
register '{"Name":"ProductsApiRead","Address":"https://localhost","Port":60321}'
register '{"Name":"OrdersApi","Address":"http://localhost","Port":60220}'
register '{"Name":"SubscriptionsApi","Address":"http://localhost","Port":60420}'
