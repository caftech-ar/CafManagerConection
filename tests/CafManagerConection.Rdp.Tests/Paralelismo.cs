using Xunit;

// Dos hilos STA registrando la misma clase de ventana de Win32 a la vez fallan con
// ERROR_CLASS_ALREADY_EXISTS (0x80070582); serializar el ensamblado es lo único que lo arregla.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
