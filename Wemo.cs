//
// Wemo.cs
//
// Author:
//  Jimmy Cassis <KernelOops@outlook.com>
//
// An application that sends commands to Wemo devices.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;

namespace Wemo
{
    public class Program
    {
        private const string ProgramName = "wemo";
        private static readonly Regex ipRegex = new Regex(@"\A(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\z");
        private static readonly Regex contentRegex = new Regex(@"<(?:BinaryState|SignalStrength|FriendlyName|DimValue)>([\w ]+?)</");
        private const int CommandDelay = 1000;
        private enum Function {
            GETSTATE,
            ON,
            OFF,
            GETNIGHTLIGHT,
            NIGHTLIGHT_ON,
            NIGHTLIGHT_OFF,
            GETSIGNALSTRENGTH,
            GETFRIENDLYNAME,
            CHANGEFRIENDLYNAME,
        };

        private static readonly HttpClient Client = new HttpClient();

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine(String.Format("Usage: {0} [OPTIONS]", ProgramName));
            Console.WriteLine("Sends and receives information from Wemo devices.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private static void ParseOpt(string[] args, List<Function> commands, ref string ip, ref string port, ref string name)
        {
            bool show_help = false;
            string ip_temp = null, port_temp = null, name_temp = null;

            var p = new OptionSet() {
                { "h|help",  "Show this message", v => show_help = v != null },
                { "i|ip=", "Specify the IP address of the target device", v => ip_temp = ipRegex.IsMatch(v) ? v : throw new OptionException("IP address invalid for option '-i'.", "--ip") },
                // Local
                // 1900 - Advertisement and discovery requests (UPnP).
                // Xxxx - Discovery response port is up to the requester source port – random
                // 49152, 49153, 49xxxx - Local web service calls. First two are used most of the time, but numbers can increase.
                // Remote
                // 8080 - HTTP for downloading firmware update files
                // 8443 - HTTPS for web services to cloud
                // 3478 - Port for TURN server for NAT client
                // PORTTEST=$(curl -s $IP:49152 | grep "404")
                // if [ "$PORTTEST" = "" ]
                // 	then
                // 	PORT=49153
                // else
                // 	PORT=49152
                // fi
                { "p|port=", "Specify the port of the target device", v => port_temp = v },
                { "c|command=", "Command to send to target device", v => {
                    if (Regex.Match(v, @"state\z", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.GETSTATE);
                    else if (Regex.Match(v, @"\Aon\z", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.ON);
                    else if (Regex.Match(v, @"\Aoff\z", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.OFF);
                    else if (Regex.Match(v, @"\An(?:ight)?l(?:ight)?\z", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.GETNIGHTLIGHT);
                    else if (Regex.Match(v, @"\An(?:ight)?l(?:ight)?_?on\z", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.NIGHTLIGHT_ON);
                    else if (Regex.Match(v, @"\An(?:ight)?l(?:ight)?_?off\z", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.NIGHTLIGHT_OFF);
                    else if (Regex.Match(v, @"signal", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.GETSIGNALSTRENGTH);
                    else if (Regex.Match(v, @"name", RegexOptions.IgnoreCase).Success)
                        commands.Add(Function.GETFRIENDLYNAME);
                    else if (Regex.Match(v, @"change", RegexOptions.IgnoreCase).Success) {
                        string[] macro = Regex.Split(v, @"[:=]");
                        if (macro.Length != 2 || macro[1] == null)
                            throw new OptionException("Missing name for option '-c change=NAME'. Received option '" + v + "'.", "--command");
                        commands.Add(Function.CHANGEFRIENDLYNAME);
                        name_temp = macro[1];
                    } else
                        throw new OptionException("Unrecognized command '" + v + "' for option '-c'.", "--command");
                } },
            };

            try {
                p.Parse(args);
            } catch (OptionException e) {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `" + ProgramName + " --help' for more information.");
                Environment.Exit(0);
            }

            if (show_help) {
                ShowHelp(p);
                Environment.Exit(0);
            }

            ip = ip_temp;
            port = port_temp ?? port;
            name = name_temp;
        }

        private static (String, String) SelectFunction(Function function, string name)
        {
            String urn = "\"urn:Belkin:service:basicevent:1#";
            String ns = "xmlns:u=\"urn:Belkin:service:basicevent:1\"";
            String action = "";
            String argument = "";
            String value = "";

            // http://IP:PORT/setup.xml events available
            // http://IP:PORT/eventservice.xml services available
            switch (function) {
                case Function.GETSTATE:
                    action = "GetBinaryState";
                    argument = "BinaryState";
                    break;
                case Function.ON:
                    action = "SetBinaryState";
                    argument = "BinaryState";
                    value = "1";
                    break;
                case Function.OFF:
                    action = "SetBinaryState";
                    argument = "BinaryState";
                    value = "0";
                    break;
                case Function.GETNIGHTLIGHT:
                    action = "GetNightLightStatus";
                    argument = "DimValue";
                    break;
                case Function.NIGHTLIGHT_ON:
                    action = "SetNightLightStatus";
                    argument = "DimValue";
                    value = "0";
                    break;
                case Function.NIGHTLIGHT_OFF:
                    action = "SetNightLightStatus";
                    argument = "DimValue";
                    value = "2";
                    break;
                case Function.GETSIGNALSTRENGTH:
                    action = "GetSignalStrength";
                    argument = "SignalStrength";
                    break;
                case Function.GETFRIENDLYNAME:
                    action = "GetFriendlyName";
                    argument = "FriendlyName";
                    break;
                case Function.CHANGEFRIENDLYNAME:
                    action = "ChangeFriendlyName";
                    argument = "FriendlyName";
                    value = name;
                    break;
                default:
                    throw new InvalidEnumArgumentException("Unknown command: " + function.ToString());
            }
            
            return (urn + action + "\"", "<u:" + action + " " + ns + "><" + argument + ">" + value + "</" + argument + "></u:" + action + ">");
        }

        private static string InterpretResult(Function c, string content)
        {
            string result = contentRegex.Match(content).Groups[1].Value;

            switch (c) {
                case Function.GETSTATE:
                case Function.ON:
                case Function.OFF:
                    if (result == "1")
                        result = "On";
                    else if (result == "0")
                        result = "Off";
                    break;
                case Function.GETNIGHTLIGHT:
                case Function.NIGHTLIGHT_ON:
                case Function.NIGHTLIGHT_OFF:
                    if (result == "0")
                        result = "On";
                    else if (result == "1")
                        result = "Dim";
                    else if (result == "2")
                        result = "Off";
                    break;
                case Function.GETSIGNALSTRENGTH:
                case Function.GETFRIENDLYNAME:
                case Function.CHANGEFRIENDLYNAME:
                    break;
                default:
                    throw new InvalidEnumArgumentException("Unknown command: " + c.ToString());
            }

            return result;
        }

        private static async Task SendCommand(string url, Function c, string changeName)
        {
            (string soapHeader, string soapBody) = SelectFunction(c, changeName);

            Client.DefaultRequestHeaders.Clear();
            Client.DefaultRequestHeaders.Add("SOAPAction", soapHeader);
            String soapContent = String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
                        <s:Body>
                            {0}
                        </s:Body>
                    </s:Envelope>", soapBody);
            var response = await Client.PostAsync(url, new StringContent(soapContent, Encoding.UTF8, "text/xml"));
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType.CharSet = "utf-8"; // BUG: https://github.com/dotnet/corefx/issues/5014
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine(String.Format("URL: {0}, Command: {1}, Result: {2}", url, c.ToString(), InterpretResult(c, content)));
            Debug.WriteLine(content);
        }

        public static async Task<int> Main(string[] args)
        {
            List<Function> commands = new List<Function>();
            string ip = "192.168.1.xxx", port = "49153", changeName = null;
            ParseOpt(args, commands, ref ip, ref port, ref changeName);

            string url = "http://" + ip + ":" + port + "/upnp/control/basicevent1";
            foreach (Function c in commands) {
                await SendCommand(url, c, changeName);
                Thread.Sleep(CommandDelay);
            }

            return 0;
        }
    }
}
