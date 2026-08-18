using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Helpers {
    /// <summary>
    /// WPF builds a CultureInfo out of the language id of the current keyboard layout every time a text
    /// field takes keyboard focus, without checking that such a culture exists. Wine reports the user
    /// locale in place of a keyboard layout, and locales without their own LCID, such as en-BE, are
    /// reported as LOCALE_CUSTOM_UNSPECIFIED, which has no culture and takes the app down. This source
    /// does the same lookup and falls back to the current culture when the language id has no culture.
    /// </summary>
    public sealed class SafeInputLanguageSource : IInputLanguageSource {
        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        private static bool _reported;

        public static void Install() {
            InputLanguageManager.Current.RegisterInputLanguageSource(new SafeInputLanguageSource());
        }

        public void Initialize() { }

        public void Uninitialize() { }

        public CultureInfo CurrentInputLanguage {
            get {
                var languageId = (int)(GetKeyboardLayout(0).ToInt64() & 0xffff);
                try {
                    return new CultureInfo(languageId);
                } catch (ArgumentException) {
                    if (!_reported) {
                        _reported = true;
                        Logging.Warning($"Keyboard layout language 0x{languageId:x4} has no culture, using {CultureInfo.CurrentCulture.Name}");
                    }
                    return CultureInfo.CurrentCulture;
                }
            }
            set { }
        }

        public IEnumerable InputLanguageList => new[] { CurrentInputLanguage };
    }
}
