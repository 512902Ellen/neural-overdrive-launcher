using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CodexBootAnimation;

internal static class SystemAppActivation
{
    internal const string PackageFamily = "OpenAI.Codex_2p2nqsd0c76g0";
    internal const string Aumid = PackageFamily + "!App";
    private static readonly Guid ActivationClass = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    private static readonly Guid ActivationInterface = new("2E941141-7F97-4756-BA1D-9DECDE894A3D");

    public static int Submit(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        int apartment = CoInitializeEx(0, 0);
        const int changedMode = unchecked((int)0x80010106);
        if (apartment != changedMode)
            Marshal.ThrowExceptionForHR(apartment);
        try
        {
            // The out-of-process COM server keeps launch arguments alive beyond the launcher lifetime.
            const uint localServer = 4;
            Marshal.ThrowExceptionForHR(CoCreateInstance(ActivationClass, 0, localServer,
                ActivationInterface, out var manager));
            try
            {
                cancellation.ThrowIfCancellationRequested();
                const uint noErrorUi = 2;
                int result = manager.ActivateApplication(Aumid, null, noErrorUi, out _);
                Marshal.ThrowExceptionForHR(result);
                return result;
            }
            finally
            {
                Marshal.ReleaseComObject(manager);
            }
        }
        finally
        {
            if (apartment >= 0) CoUninitialize();
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint reserved, uint apartment);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IApplicationActivationManager manager);

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint processId);
    }
}
