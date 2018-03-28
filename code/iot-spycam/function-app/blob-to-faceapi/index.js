const axios = require('axios');
let serviceUrl = 'https://westus.api.cognitive.microsoft.com/face/v1.0/detect?' +
    'returnFaceId=true&returnFaceAttributes=emotion';
let serviceKey = process.env.FACE_API_KEY;

module.exports = async function (context, myBlob) {
    context.log("Processing blob \n Name:", context.bindingData.name, "\n Blob Size:", myBlob.length, "Bytes");
    // Add "dataType": "binary" to function.json
    // myBlob is passed in as Buffer
    try {
        let res = await axios({
            method: 'POST',
            url: serviceUrl,
            headers: {
                'Content-Type': 'application/octet-stream',
                'Content-Length': myBlob.length,
                'Ocp-Apim-Subscription-Key': serviceKey
            },
            data: myBlob
        });
        context.log(JSON.stringify(res.data, null, 4));
        context.log('FACES: ', res.data.length);
        context.bindings.outputEventHubMessage = JSON.stringify({
            deviceId: 'botnet',
            faces: res.data.length
        });
    }
    catch (e) {
        let shortError = {
            status: e.response.status,
            statusText: e.response.statusText,
            body: e.response.data.error
        };
        context.log(shortError);
        
        context.done();
    }
};