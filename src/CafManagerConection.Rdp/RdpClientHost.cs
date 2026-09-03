using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CafManagerConection.Rdp;

/// <summary>Aloja el control ActiveX del cliente RDP de Windows dentro de WinForms.</summary>
// Escrito a mano: la tarea de MSBuild de COMReference/aximp sólo existe en .NET Framework y con dotnet build falla con MSB4803.
[SupportedOSPlatform("windows")]
public sealed partial class RdpClientHost : AxHost
{
    // El ProgID registrado es MsTscAx.MsTscAx.N y no MsRdpClientNNotSafeForScripting, que es el nombre de la coclase. Windows 11 llega a la 13.
    private static readonly string[] ProgIds =
    [
        "MsTscAx.MsTscAx.13",
        "MsTscAx.MsTscAx.12",
        "MsTscAx.MsTscAx.11",
        "MsTscAx.MsTscAx.10",
        "MsTscAx.MsTscAx.9",
        "MsTscAx.MsTscAx.8",
        "MsTscAx.MsTscAx.7",
    ];

    private object? _ocx;
    private bool _comReleased;

    public RdpClientHost()
        : base(ResolveClsid())
    {
    }

    public static bool IsAvailable => TryResolveClsid(out _);

    internal static string ResolvedClsid => TryResolveClsid(out var clsid) ? clsid : string.Empty;

    internal static bool CanCreate(Guid clsid) => SePuedeCrear(clsid);

    private static string ResolveClsid()
    {
        if (TryResolveClsid(out var clsid))
        {
            return clsid;
        }

        throw new NotSupportedException(
            "No se encontró el cliente RDP de Windows en este equipo. " +
            "Es un componente del sistema operativo y debería estar presente en Windows 11.");
    }

    private static string? _clsidResuelto;

    // Que el ProgID resuelva no basta: en Windows 11, MsTscAx.MsTscAx.13 está registrado pero la fábrica devuelve CLASS_E_CLASSNOTAVAILABLE y la 12 sí funciona.
    private static bool TryResolveClsid(out string clsid)
    {
        if (_clsidResuelto is { } cacheado)
        {
            clsid = cacheado;
            return cacheado.Length > 0;
        }

        foreach (var progId in ProgIds)
        {
            var tipo = Type.GetTypeFromProgID(progId, throwOnError: false);

            if (tipo is null)
            {
                continue;
            }

            if (!SePuedeCrear(tipo.GUID))
            {
                continue;
            }

            clsid = tipo.GUID.ToString("B");
            _clsidResuelto = clsid;
            return true;
        }

        _clsidResuelto = string.Empty;
        clsid = string.Empty;
        return false;
    }

    private const uint ClsCtxInprocServer = 1;

    private static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        in Guid clsid, nint exterior, uint contexto, in Guid iid, out nint instancia);

    // CoCreateInstance y no Activator.CreateInstance: liberar el envoltorio administrado con ReleaseComObject dejaba el siguiente control con InvalidComObjectException.
    private static bool SePuedeCrear(Guid clsid)
    {
        var iid = IidIUnknown;

        if (CoCreateInstance(clsid, nint.Zero, ClsCtxInprocServer, iid, out var puntero) < 0)
        {
            return false;
        }

        if (puntero != nint.Zero)
        {
            Marshal.Release(puntero);
        }

        return true;
    }

    private object Ocx => _ocx ??= GetOcx()
        ?? throw new InvalidOperationException(
            "El control RDP todavía no se creó. Hay que agregarlo a un contenedor antes " +
            "de configurarlo.");

    protected override void AttachInterfaces() => _ocx = GetOcx();

    internal void Set(string property, object? value) =>
        Ocx.GetType().InvokeMember(
            property, BindingFlags.SetProperty, null, Ocx, [value]);

    internal T? Get<T>(string property)
    {
        var valor = Ocx.GetType().InvokeMember(
            property, BindingFlags.GetProperty, null, Ocx, null);

        return valor is T tipado ? tipado : default;
    }

    /// <summary>Fija la propiedad del control si esta versión la expone. <see cref="TrySetOn"/> con el AxHost como destino no llega al objeto COM.</summary>
    internal bool TrySet(string property, object? value) => TrySetOn(Ocx, property, value);

    internal object? GetObject(string property) =>
        Ocx.GetType().InvokeMember(property, BindingFlags.GetProperty, null, Ocx, null);

    internal object? Invoke(string method, params object?[] args) =>
        Ocx.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, Ocx, args);

    internal static void SetOn(object target, string property, object? value) =>
        target.GetType().InvokeMember(
            property, BindingFlags.SetProperty, null, target, [value]);

    /// <summary>Fija la propiedad si existe en esta versión del control, y no hace nada si no.</summary>
    internal static bool TrySetOn(object target, string property, object? value)
    {
        try
        {
            SetOn(target, property, value);
            return true;
        }
        catch (MissingMemberException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
    }

    // Desde .NET 8, AxHost.Dispose dejó de soltar el objeto COM de forma determinística (dotnet/winforms#12056).
    public void ReleaseCom()
    {
        if (_comReleased)
        {
            return;
        }

        _comReleased = true;

        if (_ocx is { } ocx && Marshal.IsComObject(ocx))
        {
            try
            {
                Marshal.FinalReleaseComObject(ocx);
            }
            catch (ArgumentException)
            {
            }
        }

        _ocx = null;
    }

    // Primero el desmontaje de AxHost y después soltar el COM: al revés, InPlaceDeactivate lanza InvalidComObjectException al cerrar con una sesión RDP abierta.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            ReleaseCom();
        }
    }
}
