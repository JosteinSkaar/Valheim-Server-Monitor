using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Valheim_Server_Monitor
{
    public class ProcessStopHandler
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        // Enumerated type for the control messages sent to the handler routine
        enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

        public static void StopProgram(Process proc)
        {
            try {
                proc.StandardError.Close();
                proc.StandardInput.Close();
                proc.StandardOutput.Close();
            } catch (Exception ex) {
                Console.WriteLine($"{ex}");
            }

            //This does not require the console window to be visible.
            Console.WriteLine("Attaching to remote console window");
            if (AttachConsole((uint)proc.Id))
            {
                // Disable Ctrl-C handling for our program
                Console.WriteLine("Sending CTRL Event");
                SetConsoleCtrlHandler(null, true);
                GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);

                //Moved this command up on suggestion from Timothy Jannace (see comments below)
                Console.WriteLine("Free the Attached console");
                FreeConsole();

                // Must wait here. If we don't and re-enable Ctrl-C
                // handling below too fast, we might terminate ourselves.
                Console.WriteLine("Waiting for proc exit");
                proc.WaitForExit(2000);

                if(proc.HasExited)
                    Console.WriteLine($"Proc exited, code: {proc.ExitCode}");
                else
                    Console.WriteLine("Proc failed to close...");


                //Re-enable Ctrl-C handling or any subsequently started
                //programs will inherit the disabled state.
                SetConsoleCtrlHandler(null, false);
            } else {
                Console.ForegroundColor = ConsoleColor.Red;
                int errCode = Marshal.GetLastWin32Error();
                Console.WriteLine($"Failed to attach to remote console window: Error code: {errCode}");
                Exception ex = Marshal.GetExceptionForHR(errCode);
                if(ex != null)
                    Console.WriteLine($"Error: {ex.Message}");
                else
                    Console.WriteLine($"No Exception found...");

                Console.ResetColor();
            }
        }
    }
}
