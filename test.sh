#!/bin/bash

# Flow Boundary Test Script - Simple Version
# Generates orders over time to demonstrate dashboard metrics

ORDER_SERVICE_URL="http://localhost:5001/orders"
DURATION=300  # Run for 5 minutes
CYCLE_TIME=60 # 60 second cycles

echo "🚀 Starting Flow Boundary Load Test"
echo "📊 Dashboard: http://localhost:3000"
echo "📝 Duration: ${DURATION} seconds"
echo ""

# Function to generate a single order
generate_order() {
    local product_id=$1
    local amount=$((RANDOM % 200 + 10))
    
    if curl -s -X POST "$ORDER_SERVICE_URL" \
        -H "Content-Type: application/json" \
        -d "{\"amount\": $amount, \"productId\": \"$product_id\"}" \
        >/dev/null 2>&1; then
        echo "✅ Order created: $product_id (\$$amount)"
        return 0
    else
        echo "❌ Failed to create order: $product_id"
        return 1
    fi
}

# Main load generation
start_time=$(date +%s)
request_count=0

echo "📈 Load pattern: 2req/s → 4req/s → 6req/s → 8req/s → back to 2req/s (cycles)"
echo "⏱️  Starting load generation..."
echo ""

while true; do
    current_time=$(date +%s)
    elapsed=$((current_time - start_time))
    
    # Exit if duration reached
    if [ $elapsed -ge $DURATION ]; then
        break
    fi
    
    # Calculate current rate based on time
    cycle_pos=$((elapsed % CYCLE_TIME))
    if [ $cycle_pos -lt 15 ]; then
        rate=2
    elif [ $cycle_pos -lt 30 ]; then
        rate=4
    elif [ $cycle_pos -lt 45 ]; then
        rate=6
    else
        rate=8
    fi
    
    # Generate orders for this period
    echo "⚡ Rate: ${rate}req/s (${elapsed}s elapsed)"
    
    period_start=$(date +%s)
    orders_this_period=0
    
    while [ $orders_this_period -lt $rate ] && [ $(date +%s) -eq $period_start ]; do
        product_id="prod-$(printf "%04d" $((request_count + 1)))"
        
        # Generate order in background
        generate_order "$product_id" &
        
        request_count=$((request_count + 1))
        orders_this_period=$((orders_this_period + 1))
        
        # Space out requests within the second
        if [ $orders_this_period -lt $rate ]; then
            sleep 0.2
        fi
    done
    
    # Wait for the rest of the second
    while [ $(date +%s) -eq $period_start ]; do
        sleep 0.1
    done
    
    # Print progress every 15 seconds
    if [ $((elapsed % 15)) -eq 0 ] && [ $elapsed -gt 0 ]; then
        echo "⏳ Progress: ${elapsed}/${DURATION}s | Total orders: ${request_count}"
    fi
done

# Wait for background jobs
echo ""
echo "⏳ Waiting for remaining requests to complete..."
wait

final_time=$(date +%s)
total_elapsed=$((final_time - start_time))

echo ""
echo "🎉 Load test completed!"
echo "📊 Final Stats:"
echo "   • Duration: ${total_elapsed} seconds"
echo "   • Total Orders: ${request_count}"

if [ $total_elapsed -gt 0 ]; then
    avg_rate=$((request_count / total_elapsed))
    echo "   • Average Rate: ${avg_rate}req/s"
fi

echo ""
echo "📈 View results in Grafana:"
echo "   • Dashboard: http://localhost:3000"
echo "   • Username: admin"
echo "   • Password: admin"
echo ""
echo "🔍 Other monitoring:"
echo "   • Prometheus: http://localhost:9090"
echo "   • Jaeger: http://localhost:16686"
echo "   • Raw metrics: http://localhost:8889/metrics"
echo ""
echo "💡 Look for these patterns in the dashboard:"
echo "   • Request rate stepping up: 2→4→6→8req/s"
echo "   • Flow latencies around 400-500ms"
echo "   • ~10% error rate from payment failures"
echo ""
echo "🔄 Run './test.sh' again to generate more data!"