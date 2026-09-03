using Xunit;

// Varias clases crean un TerminalControl en su propio hilo STA; en paralelo compiten por el
// registro de la clase de ventana de WinForms. Mismo defecto ya diagnosticado para las pruebas
// de RDP (0x80070582, ERROR_CLASS_ALREADY_EXISTS).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
