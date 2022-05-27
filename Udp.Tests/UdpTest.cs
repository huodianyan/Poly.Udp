using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Poly.Udp.Tests
{
    [TestClass]
    public partial class UdpTest
    {
        private string address = "127.0.0.1";
        private int port = 27000;
        private PolyUdpServer server;
        private PolyUdpClient client;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
        }
        [ClassCleanup]
        public static void ClassCleanup()
        {
        }
        [TestInitialize]
        public void TestInitialize()
        {
        }
        [TestCleanup]
        public void TestCleanup()
        {
        }

        [TestMethod]
        public async Task ConnectTest()
        {
            var tcs = new TaskCompletionSource<bool>();

            server = new PolyUdpServer();
            server.Start(address, port);
            server.OnConnectEvent += (connId) => Console.WriteLine($"OnServerConnect: {connId}");
            server.OnDisconnectEvent += (connId) => Console.WriteLine($"OnServerDisconnect: {connId}");
            server.OnReceiveEvent += (connId, segment, method) =>
            {
                var text = Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
                //Assert.AreEqual("Hello", text);
                Console.WriteLine($"OnServerRecieve: {connId}, {text}");
                server.Send(connId, $"Resp: {text}");
            };

            client = new PolyUdpClient();
            client.Connect(address, port);
            client.OnConnectEvent += (connId) => { Console.WriteLine($"OnClientConnect: {connId}"); };
            client.OnDisconnectEvent += (connId) => Console.WriteLine($"OnClientDisconnect: {connId}");
            client.OnReceiveEvent += (connId, segment, method) =>
            {
                var text = Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
                //Assert.AreEqual("World", text);
                Console.WriteLine($"OnClientRecieve: {connId}, {text}");
                //tcs.SetResult(true);
            };

            //var thread = new Thread(() => SendLoop());
            //sendThread.IsBackground = true;
            //sendThread.Start();

            //while (!Console.KeyAvailable)
            //{
            //    server.PollEvents();
            //    client.PollEvents();
            //    await Task.Delay(50);
            //}

            var ticks = 0;
            while (ticks++ < 100)
            {
                //Console.WriteLine($"Tick: {ticks}");
                //if (tcs.Task.IsCompleted) break;
                server.PollEvents();
                client.PollEvents();
                if(client.IsConnected)
                {
                    client.Send(0, $"Hello[{ticks}]");
                }

                await Task.Delay(50);
            }

            client.Disconnect();
            server.Stop();
            //await tcs.Task;

            //client.Disconnect();
            //Assert.IsFalse(client.IsConnected);
            //client = null;

            ////server.OnConnectEvent -= OnServerConnect;
            ////server.OnDisconnectEvent -= OnServerDisconnect;
            ////server.OnRecieveEvent -= OnServerRecieve;
            //server.Stop();
            //Assert.IsFalse(server.IsStarted);
            //server = null;
        }
    }
}