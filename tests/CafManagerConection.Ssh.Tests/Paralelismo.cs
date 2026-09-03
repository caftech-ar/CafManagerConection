using Xunit;

// Sin esto, sshd rechaza conexiones concurrentes sin autenticar por `MaxStartups`: fallaban en
// 10 ms (huella de host nula, motivo «Other») en vez de tardar el ~1 s de un saludo SSH real.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
