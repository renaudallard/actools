using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Threading;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows {
    public static class Toast {
        public static bool OptionFallbackMode = false;

        private static bool _winToasterIsNotAvailable;
        private static Action _defaultAction;

        public static void SetDefaultAction(Action defaultAction) {
            _defaultAction = defaultAction;
        }

        /// <summary>
        /// Show a toast.
        /// </summary>
        /// <param name="title">Ex.: “Something Happened”</param>
        /// <param name="message">Ex.: “This and that. Without dot in the end”</param>
        /// <param name="click">Click action</param>
        public static void Show(string title, string message, Action click = null) {
            Show(title.ToTitle(), message, AppIconService.GetAppIconUri(), click ?? _defaultAction);
        }

        /// <summary>
        /// Show a toast.
        /// </summary>
        /// <param name="title">Ex.: “Something Happened”</param>
        /// <param name="message">Ex.: “This and that. Without dot in the end”</param>
        /// <param name="icon">Uri to some icon</param>
        /// <param name="click">Click action</param>
        public static void Show(string title, string message, [NotNull] Uri icon, Action click = null) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                if (!_winToasterIsNotAvailable && !OptionFallbackMode) {
                    try {
                        ShowWin8Toast(title, message, icon, click);
                        return;
                    } catch {
                        Logging.Warning("Windows 8+ toasts aren’t available");
                        _winToasterIsNotAvailable = true;
                    }
                }

                ShowFallback(title, message, click);
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable UnusedParameter.Local
        private static void ShowWin8Toast(string title, string message, Uri icon, Action click) {
            // ReSharper restore UnusedParameter.Local
#if WIN8SUPPORTED
            FirstFloor.ModernUI.Win8Extension.Toast.Show(new FirstFloor.ModernUI.Win8Extension.ToastParameters(title, message) {
                IconUri = icon,
                ClickCallback = click ?? _defaultAction,
                AppUserModelId = AppShortcut.AppUserModelId,
                BoundToCallingApplication = false
            });
#else
            throw new NotSupportedException();
#endif
        }

        private const int BalloonTimeout = 5000;

        private static void ShowFallback(string title, string message, Action click) {
            try {
                var text = title + Environment.NewLine + message;
                var notifyIcon = new NotifyIcon {
                    Icon = AppIconService.GetTrayIcon(),
                    Text = text.Length > 63 ? text.Substring(0, 63) : text,
                    Visible = true
                };

                if (click != null) {
                    notifyIcon.BalloonTipClicked += (sender, args) => { click.InvokeInMainThreadAsync(); };
                }

                var removed = false;

                void Remove() {
                    if (removed) return;
                    removed = true;
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }

                notifyIcon.ShowBalloonTip(BalloonTimeout, title, message, ToolTipIcon.Info);
                notifyIcon.BalloonTipClosed += (sender, args) => Remove();

                // Wine only ever sends NIN_SELECT, so BalloonTipClosed never arrives and the icon would
                // stay in the tray for good
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(BalloonTimeout + 1000) };
                timer.Tick += (sender, args) => {
                    timer.Stop();
                    Remove();
                };
                timer.Start();
            } catch (Exception e) {
                Logging.Error(e);
            }
        }
    }
}