// Stub Sentry GDExtension for Android. Returns failure so the engine skips loading it.

typedef int GDExtensionBool;
typedef void* GDExtensionInterfaceGetProcAddress;
typedef void* GDExtensionClassLibraryPtr;
typedef void* GDExtensionInitialization;

GDExtensionBool gdextension_init(
    GDExtensionInterfaceGetProcAddress p_get_proc_address,
    GDExtensionClassLibraryPtr p_library,
    GDExtensionInitialization *r_initialization
) {
    return 0;
}
