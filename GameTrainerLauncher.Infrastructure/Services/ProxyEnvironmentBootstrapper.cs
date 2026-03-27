using System.Net;
using System.Net.Sockets;

namespace GameTrainerLauncher.Infrastructure.Services;

public static class ProxyEnvironmentBootstrapper
{
    private static readonly string[] ProxyKeys =
    {
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "ALL_PROXY",
        "http_proxy",
        "https_proxy",
        "all_proxy"
    };

    private static readonly int[] CommonLocalProxyPorts = { 7890, 7897 };

    public static string? Configure()
    {
        if (HasExplicitProxyConfiguration())
        {
            return null;
        }

        foreach (var port in CommonLocalProxyPorts)
        {
            if (!IsTcpPortOpen("127.0.0.1", port))
            {
                continue;
            }

            var proxyUrl = $"http://127.0.0.1:{port}";
            foreach (var key in ProxyKeys)
            {
                Environment.SetEnvironmentVariable(key, proxyUrl);
            }

            WebRequest.DefaultWebProxy = new WebProxy(proxyUrl);
            return proxyUrl;
        }

        return null;
    }

    private static bool HasExplicitProxyConfiguration()
    {
        return ProxyKeys.Any(key => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)));
    }

    private static bool IsTcpPortOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = connectTask.Wait(TimeSpan.FromMilliseconds(250));
            return completed && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
