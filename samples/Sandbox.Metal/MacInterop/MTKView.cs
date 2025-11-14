using System.Runtime.Versioning;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;
using SharpMetal.QuartzCore;

namespace Sandbox.MacInterop
{
    [SupportedOSPlatform("macos")]
    public class MTKView
    {
        public IntPtr NativePtr;
        public static implicit operator IntPtr(MTKView mtkView) => mtkView.NativePtr;

        public MTKView(IntPtr ptr)
        {
            NativePtr = ptr;
        }

        public MTKView(NSRect frameRect, MTLDevice device)
        {
            var ptr = new ObjectiveCClass("MTKView").Alloc();
            NativePtr = ObjectiveC.IntPtr_objc_msgSend(ptr, "initWithFrame:device:", frameRect, device);
        }

        public MTLPixelFormat ColorPixelFormat
        {
            set => ObjectiveC.objc_msgSend(NativePtr, new Selector("setColorPixelFormat:atIndex:"), value, 0);
        }

        public IntPtr Colorspace
        {
            get => ObjectiveC.IntPtr_objc_msgSend(NativePtr, new Selector("colorspace"));
            set => ObjectiveC.objc_msgSend(NativePtr, new Selector("setColorspace:"), value);
        }

        public MTLClearColor ClearColor
        {
            set => ObjectiveC.objc_msgSend(NativePtr, new Selector("setClearColor:"), value);
        }

        public MTLPixelFormat DepthStencilPixelFormat
        {
            set => ObjectiveC.objc_msgSend(NativePtr, new Selector("setDepthStencilPixelFormat:"), value);
        }

        public MTKViewDelegate Delegate
        {
            set => ObjectiveC.objc_msgSend(NativePtr, "setDelegate:", value);
        }
        
        public CAMetalDrawable CurrentDrawable
        {
            get => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "currentDrawable"));
        }

        public MTLRenderPassDescriptor CurrentRenderPassDescriptor
        {
            get => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "currentRenderPassDescriptor"));
        }

        public MTL4RenderPassDescriptor CurrentMTL4RenderPassDescriptor
        {
            get => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "currentMTL4RenderPassDescriptor"));
        }
    }
}
