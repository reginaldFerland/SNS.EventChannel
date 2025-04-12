#!/bin/bash

# Create SNS topic
awslocal sns create-topic --name order-events

echo "LocalStack initialization completed"
