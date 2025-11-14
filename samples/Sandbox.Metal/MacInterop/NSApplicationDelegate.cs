using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.ObjectiveCCore;

namespace Sandbox.MacInterop
{
    [SupportedOSPlatform("macos")]
    public class NSApplicationDelegate
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OnApplicationWillFinishLaunchingDelegate(IntPtr id, IntPtr cmd, IntPtr notification);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OnApplicationDidFinishLaunchingDelegate(IntPtr id, IntPtr cmd, IntPtr notification);

        private OnApplicationWillFinishLaunchingDelegate _onApplicationWillFinishLaunching;
        private OnApplicationDidFinishLaunchingDelegate _onApplicationDidFinishLaunching;

        public Action<NSNotification> OnApplicationWillFinishLaunching = null!;
        public Action<NSNotification> OnApplicationDidFinishLaunching = null!;

        public IntPtr NativePtr;

        public unsafe NSApplicationDelegate(NSApplication application)
        {
            var name = Utf8StringMarshaller.ConvertToUnmanaged("AppDelegate");
            var types = Utf8StringMarshaller.ConvertToUnmanaged("v@:#");

            _onApplicationWillFinishLaunching = (_, _, notif) => OnApplicationWillFinishLaunching?.Invoke(new NSNotification(notif));
            var onApplicationWillFinishLaunchingPtr = Marshal.GetFunctionPointerForDelegate(_onApplicationWillFinishLaunching);

            _onApplicationDidFinishLaunching = (_, _, notif) => OnApplicationDidFinishLaunching?.Invoke(new NSNotification(notif));
            var onDidFinishLaunchingPtr = Marshal.GetFunctionPointerForDelegate(_onApplicationDidFinishLaunching);

            var appDelegateClass = ObjectiveC.objc_allocateClassPair(new ObjectiveCClass("NSObject"), (char*)name, 0);

            ObjectiveC.class_addMethod(appDelegateClass, "applicationWillFinishLaunching:", onApplicationWillFinishLaunchingPtr, (char*)types);
            ObjectiveC.class_addMethod(appDelegateClass, "applicationDidFinishLaunching:", onDidFinishLaunchingPtr, (char*)types);

            ObjectiveC.objc_registerClassPair(appDelegateClass);

            NativePtr = new ObjectiveCClass(appDelegateClass).AllocInit();
        }
    }
}
