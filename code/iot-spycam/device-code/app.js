// read secrets from .env
require('dotenv').config()
const IOTHUB_CONNSTR = process.env.IOTHUB_CONNSTR;

// needs fswebcam on Linux and imagesnap on macOS
// Use imagesnap 0.2.5 from http://iharder.sourceforge.net/current/macosx/imagesnap/
// until this bug gets fixed: https://github.com/rharder/imagesnap/issues/16
const NodeWebcam = require("node-webcam");
const Duplex = require('stream').Duplex;
const colors = require('colors');
const fs = require('fs');
const mqtt = require('azure-iot-device-mqtt').Mqtt;
const clientFromConnectionString = require('azure-iot-device-mqtt').clientFromConnectionString;
const client = clientFromConnectionString(IOTHUB_CONNSTR);
const exec = require('child_process').exec;
const crypto = require('crypto');

let capture = null;
const intervalInMs = 1000 * 15; // snap a pic every 15 sec

// Use Linux framebuffer imageviewer if available
const fbi = '/usr/bin/fbi';
let useFbi = false;
if (fs.existsSync(fbi)) {
    useFbi = true;
}

let webcam = NodeWebcam.create({
    width: 1280,
    height: 720,
    quality: 60,
    delay: 1,
    skip: 20,
    // Save shots in memory
    saveShots: true,
    output: 'jpeg',
    // false = default webcam
    device: false,
    // [location, buffer, base64]
    callbackReturn: 'buffer',
    verbose: false
});

colors.setTheme({
    silly: 'rainbow',
    info: 'green',
    warn: 'yellow',
    debug: 'cyan',
    error: 'red'
});

function bufferToStream(buffer) {  
    let stream = new Duplex();
    stream.push(buffer);
    stream.push(null);

    return stream;
}

function uploadToBlob(blobName, data) {
    let digest = crypto.createHash('md5')
	.update(new Int32Array(data))
	.digest('hex').substring(0, 6);
    let bytes = bufferToStream(data);
    client.uploadToBlob(blobName, bytes, data.length, (err) => {
        if (err) {
            console.error(`Error uploading file: ${err.toString()}`.error);
        } else {
            console.log(`File uploaded.\nMD5 hash: ${digest}`.info);
        }
    });
}

function captureAndUpload(request, response) {
    response.send(200, 'Capture has started.', (err) => {
        if (err) {
            console.error(`An error ocurred when sending a method response:\n ${err.toString()}`.error);
        } else {
            console.log(`Response to method ${request.methodName} sent successfully.`.debug);
        }
    });
    capture = setInterval(() => {
        // Use in-memory /tmp as tmpfs on the Pi. Add this line to /etc/fstab and reboot:
        //     tmpfs /tmp tmpfs nodev,nosuid,size=50M 0 0
        // That's 50 MB of "RAM drive" in /tmp.
        webcam.capture('/tmp/in_memory_image', function (err, data) {
            if (fbi) {
                exec('fbi -T 1 --noverbose -t 2 -1 -a -d /dev/fb0 ' +
                     '/tmp/in_memory_image.jpg; clear', (error, stdout, stderr) => {
                         if (error) { console.error(error) };
                         if (stdout) { console.log(stdout) };
                         if (stderr) { console.error(stderr) };
                     });
            }
            // console.log('Image capture ready. Uploading...'.debug);
            uploadToBlob('faces.jpg', data);
        });
    }, intervalInMs);
}

function stopCapture(request, response) {
    clearInterval(capture);
    console.log('Capture stopped via direct method.'.debug);
    response.send(200, 'Capture has stopped.', (err) => {
        if (err) {
            console.error(`An error ocurred when sending a method response:\n ${err.toString()}`.error);
        } else {
            console.log(`Response to method ${request.methodName} sent successfully.`.debug);
        }
    });
}

function handleReboot(request, response) {
    response.send(200, 'Rebooting device...', (err) => {
        if (err) {
            console.error(`An error ocurred when sending a method response:\n ${err.toString()}`.error);
        } else {
            console.log(`Response to method ${request.methodName} sent successfully.`.debug);
        }
    });
    exec('reboot');
}

function handleHalt(request, response) {
    response.send(200, 'Shutting down device...', (err) => {
        if (err) {
            console.error(`An error ocurred when sending a method response:\n ${err.toString()}`.error);
        } else {
            console.log(`Response to method ${request.methodName} sent successfully.`.debug);
        }
    });
    exec('halt -p');
}

// "Main()"
const deviceId = IOTHUB_CONNSTR.split(';')[1].split('=')[1];
client.open(err => {
    if (err) {
        console.error('Unable to connect to Azure IoT Hub:'.error);
        console.error(err.message.toString().error);
    }
    else {
        console.log('\nDevice ID'.info, deviceId.rainbow, 'connected to Azure IoT Hub\nover MQTT. '.info);
    }
});

// Attach handlers for device methods
client.onDeviceMethod('capture', captureAndUpload);
client.onDeviceMethod('stop', stopCapture);
client.onDeviceMethod('reboot', handleReboot);
client.onDeviceMethod('halt', handleHalt);
