using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Game.Unity.Server
{
    /// <summary>
    /// TCP ヘルスプローブ。バックグラウンドスレッドで TCP 接続を受け入れ、
    /// 即座に閉じる。接続成功 = サーバー正常稼動。
    /// GCE ヘルスチェック、Docker HEALTHCHECK で使用。
    /// </summary>
    public sealed class TcpHealthProbe : IDisposable
    {
        private readonly int _port;
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public TcpHealthProbe(int port)
        {
            _port = port;
        }

        /// <summary>
        /// ヘルスプローブを開始する。バックグラウンドスレッドで TCP リスナーを起動。
        /// </summary>
        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _thread = new Thread(ListenLoop)
            {
                Name = "TcpHealthProbe",
                IsBackground = true
            };
            _thread.Start();

            Debug.Log($"[TcpHealthProbe] Listening on TCP port {_port}");
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    // AcceptTcpClient はブロッキング呼び出し
                    // _listener.Stop() で SocketException が発生してループを抜ける
                    using (TcpClient client = _listener.AcceptTcpClient())
                    {
                        client.Close();
                    }
                }
                catch (SocketException)
                {
                    // Stop() によるリスナー停止時の正常な例外
                    if (!_running)
                    {
                        break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // リスナーが破棄された場合
                    break;
                }
            }
        }

        /// <summary>
        /// ヘルスプローブを停止する。
        /// </summary>
        public void Dispose()
        {
            _running = false;

            try
            {
                _listener?.Stop();
            }
            catch (Exception)
            {
                // 停止時の例外は無視
            }

            // スレッド終了を待機（最大 2 秒）
            if (_thread != null && _thread.IsAlive)
            {
                _thread.Join(2000);
            }

            _listener = null;
            _thread = null;

            Debug.Log("[TcpHealthProbe] Stopped");
        }
    }
}
