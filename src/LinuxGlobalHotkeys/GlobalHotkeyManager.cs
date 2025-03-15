using System.Runtime.InteropServices;

namespace LinuxGlobalHotkeys
{
    public class GlobalHotkeyManager : IDisposable
    {
        // X11 constants
        private const uint AnyModifier = 1 << 15;

        // X11 display and window handles
        private IntPtr display;
        private IntPtr rootWindow;

        // Cancellation token source for clean shutdown
        private CancellationTokenSource cts;
        private Task listenerTask;

        // Dictionary of registered shortcuts and their callbacks
        private Dictionary<ShortcutKey, Action> shortcuts = new Dictionary<ShortcutKey, Action>();
        private Dictionary<ShortcutKey, bool> shortcutStates = new Dictionary<ShortcutKey, bool>();

        // X11 P/Invoke declarations
        [DllImport("libX11.so.6")]
        private static extern IntPtr XOpenDisplay(string display);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XStringToKeysym(string keysym);

        [DllImport("libX11.so.6")]
        private static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XQueryKeymap(IntPtr display, [Out] byte[] keys);

        /// <summary>
        /// Initializes a new instance of the GlobalHotkeyManager
        /// </summary>
        public GlobalHotkeyManager()
        {
            // Open X display
            display = XOpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to open X11 display. Make sure X11/XWayland is running.");
            }

            // Get the root window
            rootWindow = XDefaultRootWindow(display);

            // Start the listener task
            cts = new CancellationTokenSource();
            listenerTask = StartKeyStateListener(cts.Token);
        }

        /// <summary>
        /// Registers a global shortcut
        /// </summary>
        /// <param name="shortcutString">Shortcut in format "Modifier+Key" (e.g., "Alt+Q", "Ctrl+S")</param>
        /// <param name="callback">Action to execute when shortcut is triggered</param>
        /// <returns>True if registration was successful</returns>
        public bool RegisterShortcut(string shortcutString, Action callback)
        {
            try
            {
                // Parse the shortcut string
                string[] parts = shortcutString.Split('+');
                if (parts.Length != 2)
                {
                    Console.WriteLine($"Invalid shortcut format: {shortcutString}. Use 'Modifier+Key' format.");
                    return false;
                }

                string modifierName = parts[0].Trim();
                string keyName = parts[1].Trim();

                // Convert modifier name to mask
                uint modifierMask = GetModifierMask(modifierName);
                if (modifierMask == 0)
                {
                    Console.WriteLine($"Unknown modifier: {modifierName}");
                    return false;
                }

                // Convert key string to keycode
                IntPtr keysym = XStringToKeysym(keyName.ToLower());
                byte keycode = XKeysymToKeycode(display, keysym);
                if (keycode == 0)
                {
                    Console.WriteLine($"Unknown key: {keyName}");
                    return false;
                }

                // Create shortcut key
                ShortcutKey shortcutKey = new ShortcutKey(keycode, modifierMask, modifierName, keyName);

                // Register the shortcut
                shortcuts[shortcutKey] = callback;
                shortcutStates[shortcutKey] = false;

                Console.WriteLine($"Registered shortcut: {modifierName}+{keyName.ToUpper()} (keycode: {keycode})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering shortcut: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unregisters a global shortcut
        /// </summary>
        /// <param name="shortcutString">Shortcut in format "Modifier+Key" (e.g., "Alt+Q", "Ctrl+S")</param>
        /// <returns>True if unregistration was successful</returns>
        public bool UnregisterShortcut(string shortcutString)
        {
            try
            {
                // Parse the shortcut string
                string[] parts = shortcutString.Split('+');
                if (parts.Length != 2)
                {
                    Console.WriteLine($"Invalid shortcut format: {shortcutString}. Use 'Modifier+Key' format.");
                    return false;
                }

                string modifierName = parts[0].Trim();
                string keyName = parts[1].Trim();

                // Convert modifier name to mask
                uint modifierMask = GetModifierMask(modifierName);
                if (modifierMask == 0)
                {
                    Console.WriteLine($"Unknown modifier: {modifierName}");
                    return false;
                }

                // Convert key string to keycode
                IntPtr keysym = XStringToKeysym(keyName.ToLower());
                byte keycode = XKeysymToKeycode(display, keysym);

                // Create shortcut key
                ShortcutKey shortcutKey = new ShortcutKey(keycode, modifierMask, modifierName, keyName);

                // Unregister the shortcut
                if (shortcuts.ContainsKey(shortcutKey))
                {
                    shortcuts.Remove(shortcutKey);
                    shortcutStates.Remove(shortcutKey);
                    Console.WriteLine($"Unregistered shortcut: {modifierName}+{keyName.ToUpper()}");
                    return true;
                }

                Console.WriteLine($"Shortcut not found: {modifierName}+{keyName.ToUpper()}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error unregistering shortcut: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts a task that polls the keyboard state to detect shortcuts
        /// </summary>
        private Task StartKeyStateListener(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                Console.WriteLine("Global shortcut listener started");

                byte[] keymap = new byte[32]; // 256 bits = 32 bytes for all key states
                Dictionary<uint, byte> modifierKeycodes = new Dictionary<uint, byte>
                {
                    { 1, 50 }, // Shift (left)
                    { 4, 37 }, // Control (left)
                    { 8, 64 }, // Alt (left)
                    { 64, 133 } // Super/Windows (left)
                };

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Get the current keyboard state
                    XQueryKeymap(display, keymap);

                    // Check each registered shortcut
                    foreach (var shortcut in shortcuts.Keys)
                    {
                        // Get the modifier keycode
                        byte modifierKeycode = modifierKeycodes.ContainsKey(shortcut.ModifierMask)
                            ? modifierKeycodes[shortcut.ModifierMask]
                            : (byte)0;

                        // Check if modifier is pressed
                        bool isModifierPressed = modifierKeycode > 0 && IsKeyPressed(keymap, modifierKeycode);

                        // Check if key is pressed
                        bool isKeyPressed = IsKeyPressed(keymap, shortcut.Keycode);

                        // If shortcut is pressed and we haven't triggered yet
                        if (isModifierPressed && isKeyPressed && !shortcutStates[shortcut])
                        {
                            shortcutStates[shortcut] = true;

                            // Execute the callback on a separate thread to avoid blocking the listener
                            Task.Run(() =>
                            {
                                try
                                {
                                    shortcuts[shortcut]?.Invoke();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error executing shortcut callback: {ex.Message}");
                                }
                            });

                            Console.WriteLine(
                                $"Shortcut triggered: {shortcut.ModifierName}+{shortcut.KeyName.ToUpper()}");
                        }
                        // If either key is released, reset the trigger state
                        else if ((!isModifierPressed || !isKeyPressed) && shortcutStates[shortcut])
                        {
                            shortcutStates[shortcut] = false;
                            Console.WriteLine(
                                $"Shortcut released: {shortcut.ModifierName}+{shortcut.KeyName.ToUpper()}");
                        }
                    }

                    // Small delay to prevent CPU hogging
                    Thread.Sleep(50);
                }

                Console.WriteLine("Global shortcut listener stopped");
            }, cancellationToken);
        }

        /// <summary>
        /// Checks if a key is pressed in the keymap
        /// </summary>
        private bool IsKeyPressed(byte[] keymap, byte keycode)
        {
            int byteIndex = keycode / 8;
            int bitIndex = keycode % 8;

            if (byteIndex >= keymap.Length)
                return false;

            return (keymap[byteIndex] & (1 << bitIndex)) != 0;
        }

        /// <summary>
        /// Gets the modifier mask from a modifier name
        /// </summary>
        private uint GetModifierMask(string modifierName)
        {
            switch (modifierName.ToLower())
            {
                case "shift": return 1;
                case "control":
                case "ctrl": return 4;
                case "alt": return 8;
                case "super":
                case "meta":
                case "win":
                case "windows": return 64;
                default: return 0;
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            // Cancel the listener task
            cts?.Cancel();

            try
            {
                // Wait for the task to complete
                listenerTask?.Wait(1000);
            }
            catch (AggregateException)
            {
                // Task was cancelled, this is expected
            }

            // Close the display
            if (display != IntPtr.Zero)
            {
                XCloseDisplay(display);
                display = IntPtr.Zero;
            }

            // Dispose cancellation token source
            cts?.Dispose();
            cts = null;
        }

        /// <summary>
        /// Represents a shortcut key combination
        /// </summary>
        private class ShortcutKey : IEquatable<ShortcutKey>
        {
            public byte Keycode { get; }
            public uint ModifierMask { get; }
            public string ModifierName { get; }
            public string KeyName { get; }

            public ShortcutKey(byte keycode, uint modifierMask, string modifierName, string keyName)
            {
                Keycode = keycode;
                ModifierMask = modifierMask;
                ModifierName = modifierName;
                KeyName = keyName;
            }

            public bool Equals(ShortcutKey other)
            {
                if (other == null)
                    return false;

                return Keycode == other.Keycode && ModifierMask == other.ModifierMask;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as ShortcutKey);
            }

            public override int GetHashCode()
            {
                return Keycode.GetHashCode() ^ ModifierMask.GetHashCode();
            }
        }
    }
}