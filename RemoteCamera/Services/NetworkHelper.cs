using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RemoteCamera
{
    /// <summary>
    /// ネットワークアドレスの確認を行う補助クラス。
    /// </summary>
    internal static class NetworkHelper
    {
        /// <summary>
        /// Tailscale アダプタの IPv4 アドレスを取得する。
        /// </summary>
        /// <returns>Tailscale の IPv4 アドレス。見つからない場合は null。</returns>
        public static string? TryGetTailscaleIpv4Address()
        {
            try
            {
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    var displayName = $"{networkInterface.Name} {networkInterface.Description}";
                    if (displayName.IndexOf("Tailscale", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        if (IPAddress.IsLoopback(unicastAddress.Address))
                        {
                            continue;
                        }

                        return unicastAddress.Address.ToString();
                    }
                }
            }
            catch
            {
                // ネットワークの状態変化で取得に失敗しても、監視画面は止めない。
            }

            return null;
        }
    }
}
