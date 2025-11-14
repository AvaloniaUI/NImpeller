using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using SharpMetal.ObjectiveCCore;

namespace Sandbox.MacInterop
{
    [SupportedOSPlatform("macos")]
    public class NSWindowDelegate
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool WindowShouldCloseDelegate(IntPtr id, IntPtr cmd, IntPtr sender);

        private WindowShouldCloseDelegate _windowShouldClose;

        public Func<IntPtr, bool>? WindowShouldClose;

        public IntPtr NativePtr;

        public unsafe NSWindowDelegate()
        {
            var name = Utf8StringMarshaller.ConvertToUnmanaged("NSWindowDelegate");
            var types = Utf8StringMarshaller.ConvertToUnmanaged("c@:@");

            _windowShouldClose = (_, _, sender) => WindowShouldClose?.Invoke(sender) ?? true;
            var windowShouldClosePtr = Marshal.GetFunctionPointerForDelegate(_windowShouldClose);

            var windowDelegateClass = ObjectiveC.objc_allocateClassPair(new ObjectiveCClass("NSObject"), (char*)name, 0);

            ObjectiveC.class_addMethod(windowDelegateClass, "windowShouldClose:", windowShouldClosePtr, (char*)types);

            ObjectiveC.objc_registerClassPair(windowDelegateClass);

            NativePtr = new ObjectiveCClass(windowDelegateClass).AllocInit();
        }
    }
}
