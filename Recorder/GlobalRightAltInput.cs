using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Recorder;

// Raw Input can receive keyboard events while this form is in the background.
// It identifies right Alt by the keyboard's extended-key flag, without
// intercepting or suppressing keys intended for other applications.
internal sealed class GlobalRightAltInput : IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RimTypeKeyboard = 1;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevRemove = 0x00000001;
    private const ushort RiKeyBreak = 0x0001;
    private const ushort RiKeyE0 = 0x0002;
    private const ushort VkMenu = 0x12;
    private const ushort VkRightMenu = 0xA5;

    private readonly Action _onPressed;
    private bool _isDown;
    private bool _disposed;

    public GlobalRightAltInput(nint window, Action onPressed)
    {
        _onPressed = onPressed;
        var device = new RawInputDevice
        {
            UsagePage = 0x01,
            Usage = 0x06,
            Flags = RidevInputSink,
            Target = window
        };
        if (!RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RawInputDevice>()))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the right Alt shortcut.");
    }

    public void ProcessMessage(ref Message message)
    {
        if (_disposed || message.Msg != WmInput) return;
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint result = GetRawInputData(message.LParam, RidInput, 0, ref size, headerSize);
        if (result == uint.MaxValue || size < headerSize + Marshal.SizeOf<RawKeyboard>()) return;

        nint buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            result = GetRawInputData(message.LParam, RidInput, buffer, ref size, headerSize);
            if (result == uint.MaxValue) return;
            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimTypeKeyboard) return;

            var keyboard = Marshal.PtrToStructure<RawKeyboard>(buffer + (int)headerSize);
            bool rightAlt = keyboard.VirtualKey == VkRightMenu ||
                            (keyboard.VirtualKey == VkMenu && (keyboard.Flags & RiKeyE0) != 0);
            if (!rightAlt) return;

            if ((keyboard.Flags & RiKeyBreak) != 0)
            {
                _isDown = false;
            }
            else if (!_isDown)
            {
                _isDown = true;
                _onPressed();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var device = new RawInputDevice
        {
            UsagePage = 0x01,
            Usage = 0x06,
            Flags = RidevRemove,
            Target = 0
        };
        RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RawInputDevice>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public nint Device;
        public nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices([In] RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(nint rawInput, uint command, nint data, ref uint size, uint headerSize);
}
