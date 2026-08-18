using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.DiscordRpc {
    public class DiscordConnector : IDisposable {
        public static TimeSpan OptionMinReconnectionDelay = TimeSpan.FromSeconds(4d);
        public static TimeSpan OptionMaxReconnectionDelay = TimeSpan.FromMinutes(1d);
        public static bool OptionVerboseMode = false;

        [CanBeNull]
        public static DiscordConnector Instance { get; private set; }

        public static void Initialize([NotNull] string clientId, IDiscordHandler handler = null) {
            Instance?.Dispose();
            Instance = new DiscordConnector(clientId, handler);
            Instance.RunAsync().Ignore();
        }

        [NotNull]
        private readonly string _clientId;
        private readonly int _processId;
        private int? _overrideProcessId;

        [CanBeNull]
        private readonly IDiscordHandler _handler;

        private DiscordConnector(string clientId, IDiscordHandler handler = null) {
            _clientId = clientId;
            _processId = Process.GetCurrentProcess().Id;
            _handler = handler;
        }

        private DiscordConnection _currentConnection;
        private DiscordRichPresence _currentPresence;

        public void Update(DiscordRichPresence presence) {
            _currentPresence = presence;
            if (_currentConnection != null) {
                UpdateSafe(_currentConnection, presence);
            } else {
                Utils.Log("Not yet connected");
            }
        }

        public IDisposable SetAppId(int appId) {
            var oldValue = _overrideProcessId;
            _overrideProcessId = appId;
            return new ActionAsDisposable(async () => {
                if (_currentConnection != null && _currentPresence != null) {
                    try {
                        await _currentConnection.UpdateAsync(_currentPresence, appId);
                    } catch (Exception e) {
                        Utils.Warn(e.ToString());
                    }
                }
                _overrideProcessId = oldValue;
            });
        }

        private async void UpdateSafe(DiscordConnection connection, DiscordRichPresence presence) {
            try {
                await connection.UpdateAsync(presence, _overrideProcessId ?? _processId);
            } catch (Exception e) {
                Utils.Warn(e.ToString());
            }
        }

        // Discord runs outside the prefix under Wine, so its named pipes are never there and reconnection
        // would otherwise write the same two lines to the log every minute for the whole session
        private const int QuietAfterFailures = 4;

        private async Task RunAsync() {
            var delay = OptionMinReconnectionDelay;
            var failures = 0;

            while (!IsDisposed) {
                var quiet = failures >= QuietAfterFailures;

                try {
                    if (!quiet) {
                        Utils.Log("(Re)creating connection…");
                    }

                    using (var connection = new DiscordConnection(_handler)) {
                        await connection.LaunchAsync(_clientId).ConfigureAwait(false);
                        delay = OptionMinReconnectionDelay;
                        failures = 0;

                        _currentConnection = connection;
                        if (_currentPresence != null) {
                            await connection.UpdateAsync(_currentPresence, _overrideProcessId ?? _processId).ConfigureAwait(false);
                        }

                        await connection.ListenAsync().ConfigureAwait(false);
                        if (connection.IsDisposed) continue;
                    }
                } catch (TimeoutException e) {
                    if (!quiet) {
                        Utils.Log(e.Message);
                    }
                } catch (IOException e) {
                    if (!quiet) {
                        Utils.Warn(e.Message);
                    }
                } catch (DiscordException e) {
                    if (!quiet) {
                        Utils.Warn(e.Message);
                    }
                } catch (Exception e) {
                    if (!quiet) {
                        Utils.Warn(e.ToString());
                    }
                }

                if (++failures == QuietAfterFailures) {
                    Utils.Log("Discord is not answering, keeping quiet from now on");
                }

                _currentConnection = null;
                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, OptionMaxReconnectionDelay.TotalSeconds));
            }
        }

        public bool IsDisposed { get; private set; }

        public void Dispose() {
            if (IsDisposed) return;
            Utils.Log("Dispose");
            IsDisposed = true;
            _currentConnection?.Dispose();
        }
    }

    public class DiscordException : Exception {
        public DiscordException(string message) : base(message) { }
    }
}