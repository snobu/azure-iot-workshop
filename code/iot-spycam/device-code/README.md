# IOT-SPYCAM

## Capture webcam image and upload to Blob storage via IoT Hub.

Add an `.env` file with the device connection string:

```bash
IOTHUB_CONNSTR=HostName=IOTHUB_INSTANCE_NAME.azure-devices.net;DeviceId=botnet;SharedAccessKey=123TheSecretKey321=
```

`npm start` or `node app.js` to start listening to Azure IoT Hub for direct methods.

Currently implemented device methods:

* `capture` - Starts capture, on a loop. 
* `stop` - Stops capture
* `reboot` - Invoke `reboot` shell command on device
* `halt` - Invoke `halt -p` shell command on device

Node 8+ required, could work with 6+, but that's on you.

Needs `fswebcam` on Linux and `imagesnap` on macOS. See comments in `app.js` for more.
