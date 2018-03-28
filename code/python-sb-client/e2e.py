#!/usr/bin/env python3

# Fixup these vars first
NAMESPACE='your_service_bus_namespace'
SAS_NAME='shared_access_key_name' # We need Listen, Send, Manage permissions
SAS_KEY='secret'

from azure.servicebus import ServiceBusService, Message, Queue
import time
import sys
from colored import fg, bg, attr

print()
color = bg(26) + fg('white')
color2 = fg(133)
reset = attr('reset')

# Instantiate Service Bus client
bus_service = ServiceBusService(
    service_namespace=NAMESPACE,
    shared_access_key_name=SAS_NAME,
    shared_access_key_value=SAS_KEY)

# Create queue
bus_service.create_queue('python-queue')

# Add message
msg = Message(b'Test Message')
bus_service.send_queue_message('python-queue', msg)
print('Message added to queue python-queue.\n')

# Receive message (peek message)
msg = bus_service.receive_queue_message('python-queue', peek_lock=True)
print('Received message from queue (peek): ');
print(color2 + str(msg.body) + reset)

# Process message
print(color + '\nProcessing: ' + reset, end='', flush=True)
sys.stdout.flush()
if len(msg.body) > 10:
    for i in range(0, 20):
        print(color + '!' + reset, end='', flush=True)
        time.sleep(0.3)
    print(reset)
    print('Processing went fine. Deleting message...')
    # Delete message
    msg.delete()
    print('Message deleted.')

# Delete queue
bus_service.delete_queue('python-queue')
print('\nQueue python-queue deleted.')
