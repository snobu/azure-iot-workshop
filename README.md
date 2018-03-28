# Microsoft Azure IoT Workshop

## Orientation

```sh
.
├── code
│   ├── iot-device-x509selfsigned    # Provision devices with x509 certs
│   │   └── certs    # <-- Place your selfsigned.pfx here (pub + priv key)
│   ├── iot-spycam    # Implements device methods and file upload
│   ├── python-sb-client    # Python Service Bus client sample
│   ├── routing-with-iothub    # Demonstrates the use of routing in IoT Hub
│   └── snmp-to-mqtt    # Connect a SNMP device to IoT Hub over MQTT
├── decks    # PowerPoint decks
└── packet-capture    # A raw packet capture for HTTP, AMQP and MQTT after TLS decryption
```

The hands-on lab is available at https://github.com/snobu/azureiothol201.

Unless otherwise noted, all C# code in this repo runs on .NET Core.
