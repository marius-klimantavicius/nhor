// Cross-compile with pristine MinGW headers for i686 and x86_64. This checks
// header aliases and ABI contracts; compiling it is NOT Windows execution.
#include <windows.h>
#define SAME(name, type) _Static_assert(__builtin_types_compatible_p(__typeof__(&(name)), type), #name " signature")
typedef ULONG_PTR (WINAPI *GetClassPtr)(HWND, int);
typedef LONG_PTR (WINAPI *GetWindowPtr)(HWND, int);
typedef LONG_PTR (WINAPI *SetWindowPtr)(HWND, int, LONG_PTR);
typedef UINT_PTR (WINAPI *SetTimerPtr)(HWND, UINT_PTR, UINT, TIMERPROC);
SAME(GetClassLongPtrW, GetClassPtr);
SAME(GetWindowLongPtrW, GetWindowPtr);
SAME(SetWindowLongPtrW, SetWindowPtr);
SAME(SetTimer, SetTimerPtr);
_Static_assert(sizeof(ULONG_PTR) == sizeof(void*), "class return width");
_Static_assert(sizeof(LONG_PTR) == sizeof(void*), "window return/argument width");
GetClassPtr class_export = GetClassLongPtrW;
GetWindowPtr window_export = GetWindowLongPtrW;
SetWindowPtr set_export = SetWindowLongPtrW;
SetTimerPtr timer_export = SetTimer;
