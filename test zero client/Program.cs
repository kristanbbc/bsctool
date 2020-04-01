using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test_zero_client
{
    class Program
    {
        static void Main(string[] args)
        {


            using (var client = new RequestSocket())
            {
                client.Connect("tcp://localhost:5555");

                string test = "n334810";
                for (int i = 1; i <= test.Length ; i++)
                {
                    Console.WriteLine("=========================================================================================");
                    Console.WriteLine("Sending {0}", test.Substring(0,i));
                    client.SendFrame(test.Substring(0, i));
                    var message = client.ReceiveFrameString();
                    Console.WriteLine("Received {0}", (JsonConvert.DeserializeObject<object>(message)));
                }
            }
            Console.ReadLine();
        }
    }
}
