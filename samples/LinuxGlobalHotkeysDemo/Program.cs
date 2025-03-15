namespace LinuxGlobalHotkeys.samples;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Global Shortcut Example");
        Console.WriteLine("Press Ctrl+C to exit");

        // Create the hotkey manager
        using (var hotkeyManager = new GlobalHotkeyManager())
        {
            // Register Alt+Q shortcut
            hotkeyManager.RegisterShortcut("Alt+Q", () =>
            {
                Console.WriteLine($"\n=== Alt+Q triggered at {DateTime.Now} ===\n");
                // You can add any action here
            });

            // Register Ctrl+L shortcut
            hotkeyManager.RegisterShortcut("Ctrl+L", () =>
            {
                Console.WriteLine($"\n=== Ctrl+L triggered at {DateTime.Now} ===\n");
                // You can add any action here
            });

            // Wait for Ctrl+C
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                exitEvent.Set();
            };

            Console.WriteLine("Listening for shortcuts. Press Ctrl+C to exit.");
            exitEvent.WaitOne();
        }

        Console.WriteLine("Application exited cleanly.");
    }
}