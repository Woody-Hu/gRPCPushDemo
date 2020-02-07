using gRPCMessage.Client;
using System;
using System.Threading;

namespace Sample.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            var source = new CancellationTokenSource();
            var client = new FanoutConsumerClient("https://localhost:5001/", s => Console.WriteLine($"receive {s}"), source.Token);
            while (!client.Healthy)
            {

            }

            Console.WriteLine("wating for messages");
            Console.ReadKey();
        }
    }
}
