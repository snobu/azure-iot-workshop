This is a .NET Core fork of Stepan's SNMPToAzure project available at https://github.com/bechynsky/SNMPToAzure.

Configuration is controlled by an `.env` file that you have to create:

```sh
DEVICE_ID=your-device-id
SNMP_COMMUNITY=public
SNMP_HOST=demo.snmplabs.com
SNMP_PORT=161
IOTHUB_CONNSTR=HostName=iothub-instance-name.azure-devices.net;DeviceId=$DEVICE_ID;SharedAccessKey=secret=
```
