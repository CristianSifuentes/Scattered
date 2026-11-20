#!/bin/bash

## this is the code for the first part of the tutorial. 
## example code for get KeyValue secrets. 
docker exec -it vault vault kv put secret/rabbitmq username=ScatteredAdmin password=ScatteredPass


## this is the code for the rabbitmq integration with vault
docker exec -it vault vault secrets enable rabbitmq

docker exec -it vault vault write rabbitmq/config/connection \
    connection_uri="http://rabbitmq:15672" \
    username="ScatteredAdmin" \
    password="ScatteredPass" 

docker exec -it vault vault write rabbitmq/roles/scattered-role \
    vhosts='{"/":{"write": ".*", "read": ".*"}}'
