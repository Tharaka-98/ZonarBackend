using System.Net;
using System.Net.Sockets;

namespace Zonar.Api.Infrastructure;

public static class NetworkHelpers
{
    /// <summary>
    /// Connects to IPv4 addresses first, with a short timeout per address.
    /// .NET tries addresses one by one; on networks where IPv6 is advertised but not routed
    /// (common on home Wi-Fi), an IPv6-first attempt can hang until the request times out.
    /// </summary>
    public static async ValueTask<Stream> ConnectPreferIPv4Async(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        Exception? lastError = null;

        foreach (var ip in addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetworkV6 ? 1 : 0))
        {
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                await socket.ConnectAsync(ip, context.DnsEndPoint.Port, timeout.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                socket.Dispose();
                lastError = ex;
            }
        }

        throw lastError ?? new SocketException((int)SocketError.HostNotFound);
    }
}
