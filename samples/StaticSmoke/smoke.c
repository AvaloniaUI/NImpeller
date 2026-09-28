// Links against the internalized libimpeller.a next to definitions that clash with Impeller's
// internal copies of libc++, libc++abi, Skia's libpng and HarfBuzz. Exits 0 when Impeller works.
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

uint32_t ImpellerGetVersion(void);
void* ImpellerPathBuilderNew(void);
void ImpellerPathBuilderRelease(void*);

static int clashes;
void* _Znwm(unsigned long size) { clashes++; return malloc(size); }
int __cxa_guard_acquire(uint64_t* guard) { clashes++; return !*(char*)guard; }
void __cxa_guard_release(uint64_t* guard) { clashes++; *(char*)guard = 1; }
void __cxa_guard_abort(uint64_t* guard) { clashes++; }
uint32_t skia_png_get_uint_32(const uint8_t* buf) { clashes++; return 0; }
void* hb_buffer_create(void) { clashes++; return NULL; }

int main(void) {
  uint32_t version = ImpellerGetVersion();
  void* builder = ImpellerPathBuilderNew();
  if (!builder) {
    printf("FAIL: ImpellerPathBuilderNew returned NULL\n");
    return 1;
  }
  ImpellerPathBuilderRelease(builder);
  if (clashes) {
    printf("FAIL: Impeller called %d consumer definitions\n", clashes);
    return 1;
  }
  printf("OK: Impeller version 0x%x\n", version);
  return 0;
}
