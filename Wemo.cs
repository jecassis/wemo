using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Wemo
{
    public class Program
    {
        private static readonly HttpClient Client = new HttpClient();

        public static async Task Main(string[] args) 
        {
            String IPADDR = "";
            String PORT = "49153";
            String TARGETSTATUS = "1";

            Client.DefaultRequestHeaders.Add("SOAPAction", "\"urn:Belkin:service:basicevent:1#SetBinaryState\"");
            String soapContent = String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                <s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
                    <s:Body>
                        <u:SetBinaryState xmlns:u=""urn:Belkin:service:basicevent:1"">
                            <BinaryState>{0}</BinaryState>
                        </u:SetBinaryState>
                    </s:Body>
                </s:Envelope>", TARGETSTATUS);
            var content = new StringContent(soapContent, Encoding.UTF8, "text/xml");

            String url = "http://" + IPADDR + ":" + PORT + "/upnp/control/basicevent1";
            var response = await Client.PostAsync(url, content);
            Console.WriteLine(response);

            return;
        }
    }
}