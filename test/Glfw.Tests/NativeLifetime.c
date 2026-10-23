// An owned module with no dependencies; load twice to test native reference ownership.
#if defined(_WIN32)
__declspec(dllexport)
#endif
int glfw_test_module_sentinel(void) { return 42; }
