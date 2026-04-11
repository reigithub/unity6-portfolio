using System.Net.Sockets;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// ネットワークアドレス取得のユーティリティ。
    /// </summary>
    public static class NetworkAddressHelper
    {
        /// <summary>
        /// ローカルマシンの IPv4 アドレスを返す。
        /// 複数の NIC がある場合は最初に見つかった IPv4 アドレスを返す。
        /// 見つからない場合は null を返す。
        /// </summary>
        /// <returns>IPv4 アドレス文字列。見つからない場合は null。</returns>
        public static string GetLocalIPv4Address()
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }

            return null;
        }
    }
}
