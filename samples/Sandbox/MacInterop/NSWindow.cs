using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.ObjectiveCCore;

namespace Sandbox.MacInterop
{
    [SupportedOSPlatform("macos")]
    public class NSWindow
    {
        public IntPtr NativePtr;

        public NSWindow(NSRect rect, ulong styleMask)
        {
            NativePtr = new ObjectiveCClass("NSWindow").Alloc();
            ObjectiveC.objc_msgSend(NativePtr, "initWithContentRect:styleMask:backing:defer:", rect, styleMask, 2, false);
        }

        public NSString Title
        {
            get => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "title"));
            set => ObjectiveC.objc_msgSend(NativePtr, "setTitle:", value);
        }

        public void SetContentView(IntPtr ptr)
        {
            ObjectiveC.objc_msgSend(NativePtr, "setContentView:", ptr);
        }

        public void MakeKeyAndOrderFront()
        {
            ObjectiveC.objc_msgSend(NativePtr, "makeKeyAndOrderFront:", IntPtr.Zero);
        }
    }

    [SupportedOSPlatform("macos")]
    [Flags]
    public enum NSStyleMask : ulong
    {
        Borderless = 0,
        Titled = 1 << 0,
        Closable = 1 << 1,
        Miniaturizable = 1 << 2,
        Resizable = 1 << 3,
        FullScreen = 1 << 14,
        FullSizeContentView = 1 << 15,
        UtilityWindow = 1 << 4,
        DocModalWindow = 1 << 6,
        NonactivatingPanel = 1 << 7,
        HUDWindow = 1 << 13
    }
}
