// Minimal WebGL glue for NImpeller in the browser. Compiled by the .NET wasm build
// (NativeFileReference) and linked into dotnet.native.wasm alongside libimpeller.a.

#include <emscripten/emscripten.h>
#include <emscripten/html5.h>
#include <emscripten/html5_webgl.h>

static EMSCRIPTEN_WEBGL_CONTEXT_HANDLE g_context = 0;

// Creates a WebGL 2 context on the canvas matched by `selector` (e.g. "#canvas"), makes it
// current and sizes the drawing buffer. Returns 1 on success, or the negative EMSCRIPTEN_RESULT.
EMSCRIPTEN_KEEPALIVE
int nimpeller_web_create_context(const char* selector, int width, int height) {
  EmscriptenWebGLContextAttributes attrs;
  emscripten_webgl_init_context_attributes(&attrs);
  attrs.majorVersion = 2;
  attrs.minorVersion = 0;
  attrs.alpha = EM_FALSE;
  attrs.depth = EM_TRUE;
  attrs.stencil = EM_TRUE;
  attrs.antialias = EM_FALSE;
  attrs.premultipliedAlpha = EM_TRUE;
  attrs.preserveDrawingBuffer = EM_FALSE;
  attrs.enableExtensionsByDefault = EM_TRUE;

  emscripten_set_canvas_element_size(selector, width, height);

  g_context = emscripten_webgl_create_context(selector, &attrs);
  if (g_context <= 0) {
    return (int)g_context;
  }
  EMSCRIPTEN_RESULT r = emscripten_webgl_make_context_current(g_context);
  if (r != EMSCRIPTEN_RESULT_SUCCESS) {
    return (int)r;
  }
  return 1;
}

EMSCRIPTEN_KEEPALIVE
int nimpeller_web_resize(const char* selector, int width, int height) {
  return (int)emscripten_set_canvas_element_size(selector, width, height);
}

// Impeller's ImpellerProcAddressCallback target: resolves GLES entrypoints from Emscripten's libGL.
EMSCRIPTEN_KEEPALIVE
void* nimpeller_web_get_proc_address(const char* name) {
  return emscripten_webgl_get_proc_address(name);
}

// --- setjmp/longjmp ABI shim -------------------------------------------------------------------
//
// The .NET 10 Emscripten pack pairs the 3.1.56 runtime libraries with a newer clang (19) that
// lowers setjmp to the __wasm_setjmp/__wasm_setjmp_test helpers introduced in Emscripten 3.1.60,
// but its compiler-rt only ships the older saveSetjmp/testSetjmp pair. libpng/libjpeg inside
// libimpeller.a use setjmp, so provide the new helpers here (verbatim from upstream
// system/lib/compiler-rt/emscripten_setjmp.c). They are layout-compatible with the 3.1.56
// __wasm_longjmp, which throws a {env, val} pair that the catch pad passes back to
// __wasm_setjmp_test.
#include <stdint.h>

struct nimpeller_jmp_buf_impl {
  void* func_invocation_id;
  uint32_t label;
  struct {
    void* env;
    int val;
  } arg;
};

void __wasm_setjmp(void* env, uint32_t label, void* func_invocation_id) {
  struct nimpeller_jmp_buf_impl* jb = env;
  jb->func_invocation_id = func_invocation_id;
  jb->label = label;
}

uint32_t __wasm_setjmp_test(void* env, void* func_invocation_id) {
  struct nimpeller_jmp_buf_impl* jb = env;
  if (jb->func_invocation_id == func_invocation_id) {
    return jb->label;
  }
  return 0;
}
