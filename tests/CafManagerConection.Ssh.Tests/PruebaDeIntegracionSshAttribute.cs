namespace CafManagerConection.Ssh.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PruebaDeIntegracionSshAttribute : FactAttribute
{
    public PruebaDeIntegracionSshAttribute() => Skip = ServidorDePrueba.MotivoDeOmision();
}

/// <summary>Necesita además el usuario cuyo <c>sudo</c> pide contraseña.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PruebaDeSudoConContrasenaAttribute : FactAttribute
{
    public PruebaDeSudoConContrasenaAttribute() =>
        Skip = ServidorDePrueba.MotivoDeOmisionDelSudoConContrasena();
}
