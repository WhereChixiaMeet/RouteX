using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetProbe;

class TgRestartTests
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static TcpListener Reserve(int port=0){var listener=new TcpListener(IPAddress.Loopback,port);listener.ExclusiveAddressUse=true;listener.Start();return listener;}
    static TgConfig Config(int port){var config=new TgConfig{Host="127.0.0.1",Port=port,Secret="0123456789abcdef0123456789abcdef",PoolSize=0,FallbackCf=false,H2Media=false};config.DcRedirects.Clear();config.CfUserDomains.Add("example.invalid");return config;}
    static void Main()
    {
        try{
            var reservation=Reserve();int port=((IPEndPoint)reservation.LocalEndpoint).Port;var config=Config(port);
            Check(!TgProxy.Start(config)&&!TgProxy.Running&&TgProxy.LastError!="","a foreign listener produces a real bind error");
            using(var foreignClient=new TcpClient()){foreignClient.Connect(IPAddress.Loopback,port);Check(reservation.Pending(),"restart does not kill a foreign listener");using(var accepted=reservation.AcceptTcpClient()){} }
            reservation.Stop();
            Check(TgProxy.Start(config)&&TgProxy.Running&&TgProxy.LastError=="","restart recovers from bind error when the port is freed");
            using(var client=new TcpClient()){
                client.Connect(IPAddress.Loopback,port);SpinWait.SpinUntil(()=>Interlocked.Read(ref TgStats.Active)>0,2000);
                Check(TgProxy.Start(config),"restart closes the old listener and rebinds the same port");
                client.ReceiveTimeout=2000;bool closed=false;try{closed=client.GetStream().ReadByte()==-1;}catch(IOException){closed=true;}Check(closed,"restart closes the previous client connection");
            }
            var starts=Enumerable.Range(0,32).Select(i=>Task.Run(()=>TgProxy.Start(Config(port)))).ToArray();Task.WaitAll(starts);
            Check(starts.All(task=>task.Result),"32 concurrent restart requests serialize without orphan listeners");
            Thread.Sleep(1400);Check(TgProxy.Running&&TgProxy.LastError=="","old accept loops cannot stop or overwrite the new listener");
            TgProxy.Stop();TgProxy.Stop();var released=Reserve(port);released.Stop();Check(true,"Stop is idempotent and releases the port for another listener");
            var interleaved=Enumerable.Range(0,24).Select(i=>Task.Run(()=>{if(i%3==0)TgProxy.Stop();else TgProxy.Start(Config(port));})).ToArray();Task.WaitAll(interleaved);TgProxy.Stop();
            released=Reserve(port);released.Stop();Check(true,"interleaved Start/Stop leaves no listener behind");
            Console.WriteLine("No external network requests: loopback sockets, no DC targets, no warmup pools, custom example.invalid domains.");
        }finally{TgProxy.Stop();}
    }
}
