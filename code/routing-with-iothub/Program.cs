using System;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;
using System.Threading.Tasks;
using System.Text;

namespace routing_with_iothub
{

    class Program
    {
        // Your DeviceId goes here
        public static string deviceId = "DEVICE_ID";
        // And your device connection string here
        public static string connStr = "HostName=IOTHUB_INSTANCE_NAME.azure-devices.net;DeviceId=DEVICE_ID;SharedAccessKey=SECRET=";
        static async Task Main(string[] args)
        {
            DeviceClient deviceClient =
                DeviceClient.CreateFromConnectionString(connStr, TransportType.Mqtt);
            await deviceClient.OpenAsync();
            Console.WriteLine("Hooked up to Azure IoT Hub.");

            int vibrationReading = -1;
            string severity = "normal";

            for (vibrationReading = 96; vibrationReading < 101; vibrationReading++)
            {
                Console.Write($"Vibration reading: {vibrationReading}");
                if (vibrationReading == 99) Console.Write(" (oh boy)");
                Console.Write("\n");

                if (vibrationReading > 99)
                {
                    severity = "critical";
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"VIBRATION READING CRITICAL: {vibrationReading}\n" +
                        $"SENDING OUT ALERT AT {DateTime.Now}");
                    Console.ResetColor();
                }

                object payload = new
                {
                    deviceId = deviceId,
                    vibrationLevel = vibrationReading
                };

                Message message =
                    new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(payload)));
                message.Properties.Add("severity", severity);

                await deviceClient.SendEventAsync(message);
                await Task.Delay(6000);
            }
        }
    }
}