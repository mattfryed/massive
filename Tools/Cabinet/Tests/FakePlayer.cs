using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

internal static class FakePlayer
{
    private static int Main(string[] args)
    {
        string control = args[Array.IndexOf(args, "--control") + 1];
        string root = Path.GetDirectoryName(control);
        File.WriteAllLines(Path.Combine(root, "args-" + Process.GetCurrentProcess().Id + ".txt"), args);
        using (var udp = new UdpClient())
        {
            udp.Connect(IPAddress.Loopback, int.Parse(Environment.GetEnvironmentVariable("MASSIVE_WATCHDOG_PORT")));
            byte[] packet = new byte[24];
            Array.Copy(Guid.ParseExact(Environment.GetEnvironmentVariable("MASSIVE_WATCHDOG_TOKEN"), "N").ToByteArray(), packet, 16);
            long sequence = 0;
            var clock = Stopwatch.StartNew();
            while (true)
            {
                string mode;
                try { mode = File.ReadAllText(control).Trim(); }
                catch (IOException) { Thread.Sleep(20); continue; }
                if (mode == "crash") return 7;
                if (mode == "freeze") Thread.Sleep(Timeout.Infinite);
                if (mode != "silent" && !(mode == "delay" && clock.Elapsed.TotalSeconds < 1.5))
                {
                    if (mode != "repeat" || sequence == 0) sequence++;
                    byte[] number = BitConverter.GetBytes(sequence);
                    Array.Copy(number, 0, packet, 16, 8);
                    if (mode == "wrong") packet[0] ^= 1;
                    udp.Send(packet, packet.Length);
                    if (mode == "wrong") packet[0] ^= 1;
                }
                Thread.Sleep(100);
            }
        }
    }
}
