using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using dotenv.net;
using ColorConsole;


namespace SNMPToAzure
{
    // This clas is for C2D communication
    // Example (gets status of contact #1 on STE2 device http://www.hw-group.com/products/STE2/ste2-wifi-thermometer_en.html):
    // { Method:"get",OID:"1.3.6.1.4.1.21796.4.9.1.1.2.1",Community:"public" }
    class SnmpCommand
    {
        public string Method { get; set; }
        public string OID { get; set; }
        public string Community { get; set; }
    }

    class Program
    {
        private static ConsoleWriter console = new ConsoleWriter();
        private static string deviceId = Environment.GetEnvironmentVariable("DEVICE_ID");
        private static DeviceClient _sendDevice = null;
        private static DeviceClient _receiveDevice = null;
        private static Timer _timer = null;
        private static IPEndPoint _snmpEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 161);
        internal static string _community = Environment.GetEnvironmentVariable("SNMP_COMMUNITY");
        internal static string _method = "get";
        //internal static int _interval = 9000;
        internal static int _interval = 0;
        internal static string _oid = ".1.3.6.1.2.1.25.1.1.0"; // uptime
        // Target device
        internal static string _smmpConnectionString = 
            $"ip={Environment.GetEnvironmentVariable("SNMP_HOST")};" +
            $"port={Environment.GetEnvironmentVariable("SNMP_PORT")}";
        // Azure IoT Hub connection string
        internal static string _connectionIoTHub = Environment.GetEnvironmentVariable("IOTHUB_CONNSTR");

        public static IPAddress GetIPv4Addresses(string host)
        {
            IPAddress ipAddress = null;
            try
            {
                ipAddress = Dns.GetHostEntry(host)
                    .AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unable to Dns.GetHostEntry() on target FQDN: {ex.Message}");
                Console.ResetColor();
                Environment.Exit(1);
            }

            return ipAddress;
        }

        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            // Read secrets from .env as env vars
            DotEnv.Config();          

            // Example: <add name="snmp" connectionString="ip=192.168.88.35;port=161" />
            string connectionSNMP = _smmpConnectionString;

            string[] connectionSNMPParts = connectionSNMP.Split(new char[] {';'});

            if (connectionSNMPParts.Length > 2)
            {
                Console.WriteLine("ERROR: Wrong SNMP connection string");
                Environment.Exit(1);
            }

            foreach (string connectionSNMPPart in connectionSNMPParts)
            {
                if(connectionSNMPPart.StartsWith("ip="))
                {
                    //_snmpEndPoint.Address = IPAddress.Parse(connectionSNMPPart.Substring(3));
                    _snmpEndPoint.Address = GetIPv4Addresses(connectionSNMPPart.Substring(3));
                    console.WriteLine($"Target SNMP device at {_snmpEndPoint.Address}",
                        ConsoleColor.DarkGreen);
                }
                else if (connectionSNMPPart.StartsWith("port="))
                {
                    int snmpPort = 0;

                    if (!int.TryParse(connectionSNMPPart.Substring(5), out snmpPort))
                    {
                        Console.WriteLine("ERROR: Wrong SNMP port");
                        Environment.Exit(2);
                    }

                    _snmpEndPoint.Port = snmpPort;
                }
                else
                {
                    Console.WriteLine("ERROR: Wrong SNMP connection string");
                    Environment.Exit(1);
                }
            }

            // If "interval" value is present in App.config we start Timer
            if (_interval > 0)
            {
                // START: Timer
                _timer = new Timer(_interval);
                _timer.AutoReset = true;
                _timer.Elapsed += _timer_Elapsed;
                _timer.Start();
                // END: Timer
            }

            _sendDevice = DeviceClient.CreateFromConnectionString(_connectionIoTHub, Microsoft.Azure.Devices.Client.TransportType.Amqp);
            _receiveDevice = DeviceClient.CreateFromConnectionString(_connectionIoTHub, Microsoft.Azure.Devices.Client.TransportType.Amqp);

#pragma warning disable 4014
            ReceiveCommandsAsync();
#pragma warning restore 4014

            
            while (true) { }
        }

        private static async void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                Console.WriteLine("\nMaking SNMP call...");
                IList<Variable> results = SnmpCall(_method, _community, _oid);

            if (results.Count < 1)
                {
                    Console.WriteLine("No results");
                    return;
                }
               
                string messageJson = GetJsonMessage(results);
                // prepare message
                Message eventMessage = new Message(Encoding.UTF8.GetBytes(messageJson));                // send message
                await _sendDevice.SendEventAsync(eventMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static string GetJsonMessage(IList<Variable> results)
        {
            object[] resultsToSerialize = new object[results.Count];

            int i = 0;
            foreach (var result in results)
            {
                resultsToSerialize[i++] = new
                {
                    Data = result.Data.ToString(),
                    Id = result.Id.ToString(),
                    TypeCode = result.Data.TypeCode.ToString(),
                    TypeCodeId = result.Data.TypeCode
                };

                console.Write("SNMP response: ", ConsoleColor.Gray);
                console.WriteLine(result.Data, ConsoleColor.Green);
            }

            String message = string.Empty;
            if (i == 1)
            {
                message = JsonConvert.SerializeObject(resultsToSerialize[0],
                    Formatting.Indented);
            }
            else
            {
                message = JsonConvert.SerializeObject(resultsToSerialize,
                    Formatting.Indented);
            }
            console.WriteLine($"Sending telemetry object to IoT Hub as device id {deviceId}:", ConsoleColor.DarkCyan);
            console.WriteLine(message, ConsoleColor.Cyan);

            return message;
        }

        private static IList<Variable> SnmpCall(string method, string community, string oid)
        {
            IList<Variable> results = new List<Variable>();

            switch (method)
            {
                case "get":
                    results = Messenger.Get(VersionCode.V1,
                           _snmpEndPoint,
                           new OctetString(community),
                           new List<Variable> { new Variable(new ObjectIdentifier(oid)) },
                           60000);
                    break;
                case "walk":
                    Messenger.Walk(VersionCode.V1,
                           _snmpEndPoint,
                           new OctetString(community),
                           new ObjectIdentifier(oid),
                           results,
                           60000, WalkMode.WithinSubtree);
                    break;
                default:
                    break;
            }

            return results;
        }

        private static async Task ReceiveCommandsAsync()
        {
            if (_receiveDevice == null)
            {
                Console.WriteLine("you must connect a device before receiving messages");
                return;
            }

            // open device client to prevent device client from closing when no messages are received after some time
            await _receiveDevice.OpenAsync();

            // start the receiving loop, check for message, if there is a message then process the message that is received
            await RunTimedLoopAsync(10, async () =>
            {                
                var receivedMessage = await _receiveDevice.ReceiveAsync();
                if (receivedMessage?.Properties.Count >= 0)
                {
                    StreamReader reader = new StreamReader(receivedMessage.BodyStream);
                    string data = reader.ReadToEnd();

                    // check received message for a command
                    Console.WriteLine("\nProcessing cloud to device message: " + data);

                    try
                    {
                        Dictionary<string, string> c2d = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
                        switch (c2d["Method"])
                        {
                            case "shutdown":
                                console.WriteLine("Got shutdown. Shutting down SNMP device...\n", ConsoleColor.Yellow);
                                break;
                            
                            case "get":
                                try
                                {
                                    SnmpCommand snmpCmd = JsonConvert.DeserializeObject<SnmpCommand>(data);

                                    IList<Variable> results = SnmpCall(snmpCmd.Method, snmpCmd.Community, snmpCmd.OID);

                                    if (results.Count < 1)
                                    {
                                        Console.WriteLine("No results");
                                        return;
                                    }

                                    string messageJson = GetJsonMessage(results);
                                    // prepare message
                                    Message eventMessage = new Message(Encoding.UTF8.GetBytes(messageJson));
                                    // send message
                                    await _sendDevice.SendEventAsync(eventMessage);
                                    
                                    // completing the message to cause the command to show as completed in the portal
                                    await _receiveDevice.CompleteAsync(receivedMessage);
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                                break;
                        }
                        await _receiveDevice.CompleteAsync(receivedMessage);
                        return;;
                    }
                    catch
                    {
                        //
                    }
                }
            });
        }

        private static async Task RunTimedLoopAsync(int runEveryMSeconds, Func<Task> runFunc)
        {
            while (true)
            {
                await runFunc();

                //if canceled let tasks die peacefully in their sleep
                await Task.Delay(TimeSpan.FromMilliseconds(runEveryMSeconds));
            }
        }
    }
}
