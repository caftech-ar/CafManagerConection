namespace CafManagerConection.Domain.Settings;

/// <summary>Los iconos que la aplicación puede dibujar, agrupados por tema.</summary>
/// <remarks>Generado desde Assets/Iconos por build/convertir-iconos.ps1; se edita ahí, no acá.</remarks>
public static class CatalogoDeIconos
{
    /// <summary>Icono con el que cae cualquier clave que el catálogo no reconoce.</summary>
    public const string ClaveDesconocido = "help-circle";

    public static GrupoDeIconos ConexionesYSesion { get; } = new("conexiones-y-sesion", "Conexiones y sesión", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos TerminalYAcceso { get; } = new("terminal-y-acceso", "Terminal y acceso", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos ServidoresYHosts { get; } = new("servidores-y-hosts", "Servidores y hosts", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos EscritorioRemotoYArchivos { get; } = new("escritorio-remoto-y-archivos", "Escritorio remoto y archivos", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos Red { get; } = new("red", "Red", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos InfraestructuraYServicios { get; } = new("infraestructura-y-servicios", "Infraestructura y servicios", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos SeguridadYCredenciales { get; } = new("seguridad-y-credenciales", "Seguridad y credenciales", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos UsuariosYPermisos { get; } = new("usuarios-y-permisos", "Usuarios y permisos", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos AlmacenamientoYBackups { get; } = new("almacenamiento-y-backups", "Almacenamiento y backups", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos MonitoreoYDiagnostico { get; } = new("monitoreo-y-diagnostico", "Monitoreo y diagnóstico", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos EstadosYSeveridad { get; } = new("estados-y-severidad", "Estados y severidad", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos CicloDeVidaYTareas { get; } = new("ciclo-de-vida-y-tareas", "Ciclo de vida y tareas", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos OrganizacionYNavegacion { get; } = new("organizacion-y-navegacion", "Organización y navegación", FamiliaDeIcono.Concepto, true);
    public static GrupoDeIconos Acciones { get; } = new("acciones", "Acciones", FamiliaDeIcono.Concepto, false);
    public static GrupoDeIconos SistemasOperativos { get; } = new("sistemas-operativos", "Sistemas operativos", FamiliaDeIcono.Logo, true);
    public static GrupoDeIconos MotoresDeDatos { get; } = new("motores-de-datos", "Motores de datos", FamiliaDeIcono.Logo, true);
    public static GrupoDeIconos VirtualizacionYContenedores { get; } = new("virtualizacion-y-contenedores", "Virtualización y contenedores", FamiliaDeIcono.Logo, true);
    public static GrupoDeIconos ServiciosYOperacion { get; } = new("servicios-y-operacion", "Servicios y operación", FamiliaDeIcono.Logo, true);
    public static GrupoDeIconos RedYPerimetro { get; } = new("red-y-perimetro", "Red y perímetro", FamiliaDeIcono.Logo, true);
    public static GrupoDeIconos StacksYRepositorios { get; } = new("stacks-y-repositorios", "Stacks y repositorios", FamiliaDeIcono.Logo, true);

    public static IReadOnlyList<GrupoDeIconos> Grupos { get; } =
    [
        ConexionesYSesion,
        TerminalYAcceso,
        ServidoresYHosts,
        EscritorioRemotoYArchivos,
        Red,
        InfraestructuraYServicios,
        SeguridadYCredenciales,
        UsuariosYPermisos,
        AlmacenamientoYBackups,
        MonitoreoYDiagnostico,
        EstadosYSeveridad,
        CicloDeVidaYTareas,
        OrganizacionYNavegacion,
        Acciones,
        SistemasOperativos,
        MotoresDeDatos,
        VirtualizacionYContenedores,
        ServiciosYOperacion,
        RedYPerimetro,
        StacksYRepositorios,
    ];

    public static IReadOnlyList<IconoDelCatalogo> Iconos { get; } =
    [
        // Conexiones y sesión
        new("arrows-transfer-up-down", ConexionesYSesion, "tráfico bidireccional", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/arrows-transfer-up-down.svg")),
        new("external-link", ConexionesYSesion, "abrir en ventana aparte", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/external-link.svg")),
        new("history", ConexionesYSesion, "historial de conexiones", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/history.svg")),
        new("link-off", ConexionesYSesion, "enlace caído", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/link-off.svg")),
        new("link", ConexionesYSesion, "enlace", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/link.svg")),
        new("player-record", ConexionesYSesion, "grabar sesión", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/player-record.svg")),
        new("plug-connected-x", ConexionesYSesion, "caída inesperada", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/plug-connected-x.svg")),
        new("plug-connected", ConexionesYSesion, "conectado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/plug-connected.svg")),
        new("plug-off", ConexionesYSesion, "desconectado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/plug-off.svg")),
        new("plug-x", ConexionesYSesion, "conexión rechazada", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/plug-x.svg")),
        new("plug", ConexionesYSesion, "conexión", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/plug.svg")),
        new("transfer-in", ConexionesYSesion, "sesión entrante", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/transfer-in.svg")),
        new("transfer-out", ConexionesYSesion, "sesión saliente", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/transfer-out.svg")),

        // Terminal y acceso
        new("code", TerminalYAcceso, "script", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/code.svg")),
        new("command", TerminalYAcceso, "comando", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/command.svg")),
        new("fingerprint", TerminalYAcceso, "huella del host", ["huella", "hostkey", "clave del host"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/fingerprint.svg")),
        new("keyboard", TerminalYAcceso, "entrada remota", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/keyboard.svg")),
        new("prompt", TerminalYAcceso, "consola", ["consola", "cmd", "linea de comando"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/prompt.svg")),
        new("sql", TerminalYAcceso, "consulta de base", ["consulta", "query", "base"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/sql.svg")),
        new("terminal-2", TerminalYAcceso, "terminal / shell", ["ssh", "shell", "consola", "bash", "terminal"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/terminal-2.svg")),

        // Servidores y hosts
        new("building-warehouse", ServidoresYHosts, "datacenter", ["datacenter", "cpd", "sitio", "sede"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/building-warehouse.svg")),
        new("cpu", ServidoresYHosts, "procesador", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cpu.svg")),
        new("cube", ServidoresYHosts, "máquina virtual", ["vm", "virtual", "maquina virtual"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cube.svg")),
        new("device-desktop", ServidoresYHosts, "PC / host", ["pc", "escritorio", "workstation", "equipo"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/device-desktop.svg")),
        new("device-laptop", ServidoresYHosts, "notebook", ["portatil", "notebook"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/device-laptop.svg")),
        new("device-sd-card", ServidoresYHosts, "almacenamiento", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/device-sd-card.svg")),
        new("devices-pc", ServidoresYHosts, "equipo completo", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/devices-pc.svg")),
        new("map-pin", ServidoresYHosts, "ubicación física", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/map-pin.svg")),
        new("server-2", ServidoresYHosts, "servidor alternativo", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/server-2.svg")),
        new("server-bolt", ServidoresYHosts, "servidor con actividad", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/server-bolt.svg")),
        new("server-cog", ServidoresYHosts, "configuración del servidor", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/server-cog.svg")),
        new("server-off", ServidoresYHosts, "servidor offline", ["caido", "apagado"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/server-off.svg")),
        new("server", ServidoresYHosts, "servidor", ["host", "maquina", "nodo"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/server.svg")),
        new("stack-3", ServidoresYHosts, "hipervisor / pool", ["hipervisor", "cluster", "pool", "esxi"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/stack-3.svg")),

        // Escritorio remoto y archivos
        new("arrows-diagonal", EscritorioRemotoYArchivos, "redimensionar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/arrows-diagonal.svg")),
        new("arrows-maximize", EscritorioRemotoYArchivos, "pantalla completa", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/arrows-maximize.svg")),
        new("clipboard-copy", EscritorioRemotoYArchivos, "portapapeles compartido", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/clipboard-copy.svg")),
        new("device-desktop-share", EscritorioRemotoYArchivos, "conexión remota", ["rdp", "escritorio remoto", "remoto"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/device-desktop-share.svg")),
        new("file-download", EscritorioRemotoYArchivos, "bajar archivo", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-download.svg")),
        new("file-upload", EscritorioRemotoYArchivos, "subir archivo", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-upload.svg")),
        new("folder-open", EscritorioRemotoYArchivos, "explorador remoto", ["explorador", "archivos"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/folder-open.svg")),
        new("screen-share", EscritorioRemotoYArchivos, "escritorio compartido", ["vnc", "compartir pantalla"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/screen-share.svg")),
        new("transfer", EscritorioRemotoYArchivos, "transferencia de archivos", ["sftp", "scp", "ftp", "transferencia"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/transfer.svg")),
        new("window-maximize", EscritorioRemotoYArchivos, "ventana de sesión", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/window-maximize.svg")),

        // Red
        new("affiliate", Red, "proxy / gateway", ["proxy", "gateway", "reverso"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/affiliate.svg")),
        new("antenna-bars-5", Red, "señal", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/antenna-bars-5.svg")),
        new("arrows-split-2", Red, "balanceador", ["balanceador", "lb", "load balancer"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/arrows-split-2.svg")),
        new("cloud-data-connection", Red, "enlace a nube", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cloud-data-connection.svg")),
        new("cloud-network", Red, "red externa", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cloud-network.svg")),
        new("network-off", Red, "red desconectada", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/network-off.svg")),
        new("network", Red, "red", ["lan", "red"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/network.svg")),
        new("radar", Red, "descubrimiento de hosts", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/radar.svg")),
        new("route", Red, "routing", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/route.svg")),
        new("router", Red, "router", ["ruteador", "gateway"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/router.svg")),
        new("satellite", Red, "enlace satelital / VPN", ["vpn", "tunel", "satelital"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/satellite.svg")),
        new("sitemap", Red, "segmentación / VLAN", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/sitemap.svg")),
        new("switch-3", Red, "switch", ["conmutador", "switch"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/switch-3.svg")),
        new("topology-star-3", Red, "topología", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/topology-star-3.svg")),
        new("wall", Red, "firewall", ["firewall", "fw", "cortafuegos"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/wall.svg")),
        new("wifi-off", Red, "Wi-Fi caído", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/wifi-off.svg")),
        new("wifi", Red, "Wi-Fi", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/wifi.svg")),
        new("world", Red, "Internet / WAN", ["internet", "wan", "publico"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/world.svg")),

        // Infraestructura y servicios
        new("api", InfraestructuraYServicios, "API", ["rest", "endpoint"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/api.svg")),
        new("box", InfraestructuraYServicios, "aplicación / servicio", ["servicio", "aplicacion", "app"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/box.svg")),
        new("cloud-computing", InfraestructuraYServicios, "cómputo en nube", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cloud-computing.svg")),
        new("cloud", InfraestructuraYServicios, "cloud", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cloud.svg")),
        new("container", InfraestructuraYServicios, "contenedor", ["docker", "contenedor", "lxc"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/container.svg")),
        new("database-cog", InfraestructuraYServicios, "configuración de base", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/database-cog.svg")),
        new("database-off", InfraestructuraYServicios, "motor detenido", ["db caida", "motor detenido"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/database-off.svg")),
        new("database-search", InfraestructuraYServicios, "explorar esquema", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/database-search.svg")),
        new("database", InfraestructuraYServicios, "base de datos", ["db", "base", "motor"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/database.svg")),
        new("mail", InfraestructuraYServicios, "servidor de correo", ["correo", "smtp", "mail"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/mail.svg")),
        new("packages", InfraestructuraYServicios, "registro de imágenes", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/packages.svg")),
        new("printer", InfraestructuraYServicios, "cola de impresión", ["impresora", "cola"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/printer.svg")),
        new("stack-2", InfraestructuraYServicios, "stack de servicios", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/stack-2.svg")),
        new("world-www", InfraestructuraYServicios, "servicio web", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/world-www.svg")),

        // Seguridad y credenciales
        new("certificate", SeguridadYCredenciales, "certificado", ["cert", "ssl", "tls", "certificado"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/certificate.svg")),
        new("circle-key", SeguridadYCredenciales, "bóveda de credenciales", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/circle-key.svg")),
        new("cloud-lock", SeguridadYCredenciales, "secreto remoto", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cloud-lock.svg")),
        new("file-certificate", SeguridadYCredenciales, "cadena de certificados", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-certificate.svg")),
        new("key", SeguridadYCredenciales, "clave SSH", ["llave", "clave privada", "ssh", "pem"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/key.svg")),
        new("lock-cog", SeguridadYCredenciales, "política de acceso", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/lock-cog.svg")),
        new("lock-open", SeguridadYCredenciales, "recurso liberado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/lock-open.svg")),
        new("lock", SeguridadYCredenciales, "recurso bloqueado", ["bloqueado", "candado"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/lock.svg")),
        new("password-user", SeguridadYCredenciales, "credencial de usuario", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/password-user.svg")),
        new("password", SeguridadYCredenciales, "contraseña", ["contrasena", "clave", "pass"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/password.svg")),
        new("shield-bolt", SeguridadYCredenciales, "excepción activa", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/shield-bolt.svg")),
        new("shield-check", SeguridadYCredenciales, "política validada", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/shield-check.svg")),
        new("shield-lock", SeguridadYCredenciales, "protegido", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/shield-lock.svg")),
        new("shield-off", SeguridadYCredenciales, "sin protección", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/shield-off.svg")),
        new("shield-search", SeguridadYCredenciales, "auditoría", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/shield-search.svg")),
        new("shield", SeguridadYCredenciales, "seguridad", ["seguridad", "proteccion"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/shield.svg")),

        // Usuarios y permisos
        new("eye-off", UsuariosYPermisos, "ocultar secreto", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/eye-off.svg")),
        new("eye", UsuariosYPermisos, "mostrar secreto", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/eye.svg")),
        new("id-badge-2", UsuariosYPermisos, "identidad", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/id-badge-2.svg")),
        new("license", UsuariosYPermisos, "licencia", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/license.svg")),
        new("user-check", UsuariosYPermisos, "usuario habilitado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/user-check.svg")),
        new("user-cog", UsuariosYPermisos, "perfil / preferencias", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/user-cog.svg")),
        new("user-off", UsuariosYPermisos, "usuario deshabilitado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/user-off.svg")),
        new("user-plus", UsuariosYPermisos, "alta de usuario", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/user-plus.svg")),
        new("user-shield", UsuariosYPermisos, "usuario privilegiado", ["admin", "root", "privilegiado"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/user-shield.svg")),
        new("users-group", UsuariosYPermisos, "grupo", ["equipo", "grupo"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/users-group.svg")),

        // Almacenamiento y backups
        new("archive", AlmacenamientoYBackups, "archivar", ["archivar", "backup"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/archive.svg")),
        new("cloud-download", AlmacenamientoYBackups, "restaurar", ["restaurar", "restore"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cloud-download.svg")),
        new("cloud-upload", AlmacenamientoYBackups, "respaldo a nube", ["respaldo", "backup"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/cloud-upload.svg")),
        new("database-export", AlmacenamientoYBackups, "exportar base", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/database-export.svg")),
        new("database-import", AlmacenamientoYBackups, "importar base", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/database-import.svg")),
        new("device-floppy", AlmacenamientoYBackups, "guardar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/device-floppy.svg")),
        new("file-database", AlmacenamientoYBackups, "volcado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-database.svg")),
        new("file-export", AlmacenamientoYBackups, "exportar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-export.svg")),
        new("file-import", AlmacenamientoYBackups, "importar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-import.svg")),
        new("file-text", AlmacenamientoYBackups, "archivo de texto", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-text.svg")),
        new("file-zip", AlmacenamientoYBackups, "comprimido", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/file-zip.svg")),
        new("folders", AlmacenamientoYBackups, "repositorio", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/folders.svg")),
        new("package", AlmacenamientoYBackups, "paquete / snapshot", ["snapshot", "paquete"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/package.svg")),
        new("photo", AlmacenamientoYBackups, "imagen", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/photo.svg")),

        // Monitoreo y diagnóstico
        new("activity-heartbeat", MonitoreoYDiagnostico, "healthcheck", ["healthcheck", "salud", "latido"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/activity-heartbeat.svg")),
        new("activity", MonitoreoYDiagnostico, "actividad", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/activity.svg")),
        new("bolt", MonitoreoYDiagnostico, "consumo / energía", ["energia", "consumo", "rayo"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/bolt.svg")),
        new("bug", MonitoreoYDiagnostico, "incidente", ["incidente", "error", "falla"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/bug.svg")),
        new("chart-bar", MonitoreoYDiagnostico, "comparativa", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/chart-bar.svg")),
        new("chart-dots", MonitoreoYDiagnostico, "métricas", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/chart-dots.svg")),
        new("chart-histogram", MonitoreoYDiagnostico, "distribución", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/chart-histogram.svg")),
        new("chart-line", MonitoreoYDiagnostico, "serie temporal", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/chart-line.svg")),
        new("clock", MonitoreoYDiagnostico, "uptime", ["uptime", "hora", "tiempo"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/clock.svg")),
        new("dashboard", MonitoreoYDiagnostico, "tablero", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/dashboard.svg")),
        new("gauge", MonitoreoYDiagnostico, "carga", ["carga", "uso", "medidor"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/gauge.svg")),
        new("hourglass", MonitoreoYDiagnostico, "latencia", ["latencia", "espera", "demora"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/hourglass.svg")),
        new("list-details", MonitoreoYDiagnostico, "registro de eventos", ["log", "registro", "bitacora"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/list-details.svg")),
        new("report-analytics", MonitoreoYDiagnostico, "reporte", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/report-analytics.svg")),
        new("scan-eye", MonitoreoYDiagnostico, "inspección", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/scan-eye.svg")),
        new("temperature", MonitoreoYDiagnostico, "temperatura", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/temperature.svg")),

        // Estados y severidad
        new("alert-octagon", EstadosYSeveridad, "crítico", ["critico", "grave"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/alert-octagon.svg")),
        new("alert-triangle", EstadosYSeveridad, "advertencia", ["warning", "aviso", "advertencia"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/alert-triangle.svg")),
        new("bell-off", EstadosYSeveridad, "silenciado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/bell-off.svg")),
        new("bell-ringing", EstadosYSeveridad, "alerta activa", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/bell-ringing.svg")),
        new("bell", EstadosYSeveridad, "notificación", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/bell.svg")),
        new("circle-check", EstadosYSeveridad, "correcto", ["ok", "exito", "correcto", "verde"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/circle-check.svg")),
        new("circle-x", EstadosYSeveridad, "error", ["error", "falla", "rojo"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/circle-x.svg")),
        new("flag", EstadosYSeveridad, "marcado", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/flag.svg")),
        new("help-circle", EstadosYSeveridad, "desconocido", ["desconocido", "ayuda", "interrogacion"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/help-circle.svg")),
        new("info-circle", EstadosYSeveridad, "informativo", ["info", "informacion"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/info-circle.svg")),
        new("refresh-alert", EstadosYSeveridad, "reintentando", ["reintento", "reconectando", "reintentando"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/refresh-alert.svg")),

        // Ciclo de vida y tareas
        new("calendar-time", CicloDeVidaYTareas, "tarea programada", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/calendar-time.svg")),
        new("checklist", CicloDeVidaYTareas, "plan de tareas", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/checklist.svg")),
        new("clock-play", CicloDeVidaYTareas, "ejecución diferida", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/clock-play.svg")),
        new("list-check", CicloDeVidaYTareas, "resultado de tareas", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/list-check.svg")),
        new("player-pause", CicloDeVidaYTareas, "pausar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/player-pause.svg")),
        new("player-play", CicloDeVidaYTareas, "iniciar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/player-play.svg")),
        new("player-stop", CicloDeVidaYTareas, "detener", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/player-stop.svg")),
        new("power", CicloDeVidaYTareas, "encender / apagar", ["encender", "apagar", "on off"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/power.svg")),
        new("refresh", CicloDeVidaYTareas, "refrescar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/refresh.svg")),
        new("reload", CicloDeVidaYTareas, "reiniciar servicio", ["reiniciar", "restart"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/reload.svg")),
        new("rotate-clockwise", CicloDeVidaYTareas, "reintentar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/rotate-clockwise.svg")),
        new("settings-automation", CicloDeVidaYTareas, "automatización", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/settings-automation.svg")),

        // Organización y navegación
        new("bookmark", OrganizacionYNavegacion, "marcador", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/bookmark.svg")),
        new("chevron-down", OrganizacionYNavegacion, "bajar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/chevron-down.svg")),
        new("chevron-right", OrganizacionYNavegacion, "expandir", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/chevron-right.svg")),
        new("chevron-up", OrganizacionYNavegacion, "subir", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/chevron-up.svg")),
        new("dots-vertical", OrganizacionYNavegacion, "más acciones", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/dots-vertical.svg")),
        new("filter", OrganizacionYNavegacion, "filtrar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/filter.svg")),
        new("folder-cog", OrganizacionYNavegacion, "propiedades de carpeta", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/folder-cog.svg")),
        new("folder-plus", OrganizacionYNavegacion, "nueva carpeta", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/folder-plus.svg")),
        new("folder", OrganizacionYNavegacion, "carpeta", ["directorio", "carpeta"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/folder.svg")),
        new("layout-grid", OrganizacionYNavegacion, "vista de tarjetas", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/layout-grid.svg")),
        new("layout-list", OrganizacionYNavegacion, "vista de lista", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/layout-list.svg")),
        new("layout-sidebar", OrganizacionYNavegacion, "panel lateral", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/layout-sidebar.svg")),
        new("menu-2", OrganizacionYNavegacion, "menú", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/menu-2.svg")),
        new("pin", OrganizacionYNavegacion, "fijar", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/pin.svg")),
        new("search", OrganizacionYNavegacion, "buscar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/search.svg")),
        new("sort-ascending", OrganizacionYNavegacion, "ordenar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/sort-ascending.svg")),
        new("star", OrganizacionYNavegacion, "favorito", ["favorito", "destacado"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/star.svg")),
        new("table", OrganizacionYNavegacion, "vista de tabla", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/table.svg")),
        new("tag", OrganizacionYNavegacion, "etiqueta", ["etiqueta", "label"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/tag.svg")),
        new("tags", OrganizacionYNavegacion, "etiquetas", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/tags.svg")),

        // Acciones
        new("adjustments", Acciones, "ajustes avanzados", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/adjustments.svg")),
        new("arrow-back-up", Acciones, "deshacer", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/arrow-back-up.svg")),
        new("check", Acciones, "confirmar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/check.svg")),
        new("copy", Acciones, "duplicar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/copy.svg")),
        new("download", Acciones, "descargar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/download.svg")),
        new("edit", Acciones, "editar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/edit.svg")),
        new("maximize", Acciones, "maximizar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/maximize.svg")),
        new("minimize", Acciones, "minimizar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/minimize.svg")),
        new("plus", Acciones, "nuevo", ["nuevo", "agregar", "crear"], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/plus.svg")),
        new("send", Acciones, "enviar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/send.svg")),
        new("settings", Acciones, "configuración", ["configuracion", "ajustes", "opciones"], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/settings.svg")),
        new("tool", Acciones, "herramientas", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/tool.svg")),
        new("trash", Acciones, "eliminar", ["borrar", "eliminar"], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/trash.svg")),
        new("upload", Acciones, "subir", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/upload.svg")),
        new("x", Acciones, "cancelar", [], ModoDePintado.Trazo, 24, false, Tabler("icons/outline/x.svg")),

        // Sistemas operativos
        new("archlinux", SistemasOperativos, "Arch", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/archlinux.svg")),
        new("brand-android", SistemasOperativos, "Android", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-android.svg")),
        new("brand-apple", SistemasOperativos, "macOS", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-apple.svg")),
        new("brand-debian", SistemasOperativos, "Debian", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-debian.svg")),
        new("brand-redhat", SistemasOperativos, "Red Hat", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-redhat.svg")),
        new("brand-ubuntu", SistemasOperativos, "Ubuntu", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-ubuntu.svg")),
        new("brand-windows", SistemasOperativos, "Windows", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-windows.svg")),
        new("centos", SistemasOperativos, "CentOS", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/centos.svg")),
        new("fedora", SistemasOperativos, "Fedora", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/fedora.svg")),
        new("linux", SistemasOperativos, "Linux", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/linux.svg")),
        new("opensuse", SistemasOperativos, "openSUSE", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/opensuse.svg")),
        new("rockylinux", SistemasOperativos, "Rocky", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/rockylinux.svg")),

        // Motores de datos
        new("brand-elastic", MotoresDeDatos, "Elasticsearch", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-elastic.svg")),
        new("brand-mongodb", MotoresDeDatos, "MongoDB", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-mongodb.svg")),
        new("brand-mysql", MotoresDeDatos, "MySQL", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-mysql.svg")),
        new("mariadb", MotoresDeDatos, "MariaDB", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/mariadb.svg")),
        new("microsoftsqlserver-line", MotoresDeDatos, "Microsoft SQL Server", [], ModoDePintado.Relleno, 128, true, Devicon("icons/microsoftsqlserver/microsoftsqlserver-line.svg")),
        new("oracle-original", MotoresDeDatos, "Oracle", [], ModoDePintado.Relleno, 128, true, Devicon("icons/oracle/oracle-original.svg")),
        new("postgresql", MotoresDeDatos, "PostgreSQL", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/postgresql.svg")),
        new("rabbitmq", MotoresDeDatos, "RabbitMQ", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/rabbitmq.svg")),
        new("redis", MotoresDeDatos, "Redis", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/redis.svg")),
        new("sqlite", MotoresDeDatos, "SQLite", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/sqlite.svg")),

        // Virtualización y contenedores
        new("brand-docker", VirtualizacionYContenedores, "Docker", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-docker.svg")),
        new("kubernetes", VirtualizacionYContenedores, "Kubernetes", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/kubernetes.svg")),
        new("proxmox", VirtualizacionYContenedores, "Proxmox", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/proxmox.svg")),
        new("vagrant", VirtualizacionYContenedores, "Vagrant", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/vagrant.svg")),
        new("vmware", VirtualizacionYContenedores, "VMware", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/vmware.svg")),

        // Servicios y operación
        new("apache", ServiciosYOperacion, "Apache", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/apache.svg")),
        new("apachetomcat", ServiciosYOperacion, "Tomcat", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/apachetomcat.svg")),
        new("brand-ansible", ServiciosYOperacion, "Ansible", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-ansible.svg")),
        new("brand-terraform", ServiciosYOperacion, "Terraform", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-terraform.svg")),
        new("grafana", ServiciosYOperacion, "Grafana", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/grafana.svg")),
        new("jenkins", ServiciosYOperacion, "Jenkins", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/jenkins.svg")),
        new("nginx", ServiciosYOperacion, "nginx", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/nginx.svg")),
        new("prometheus", ServiciosYOperacion, "Prometheus", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/prometheus.svg")),
        new("traefikproxy", ServiciosYOperacion, "Traefik", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/traefikproxy.svg")),

        // Red y perímetro
        new("brand-openvpn", RedYPerimetro, "OpenVPN", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-openvpn.svg")),
        new("cisco", RedYPerimetro, "Cisco", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/cisco.svg")),
        new("fortinet", RedYPerimetro, "Fortinet", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/fortinet.svg")),
        new("mikrotik", RedYPerimetro, "MikroTik", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/mikrotik.svg")),
        new("pfsense", RedYPerimetro, "pfSense", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/pfsense.svg")),
        new("ubiquiti", RedYPerimetro, "Ubiquiti", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/ubiquiti.svg")),
        new("wireguard", RedYPerimetro, "WireGuard", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/wireguard.svg")),

        // Stacks y repositorios
        new("brand-angular", StacksYRepositorios, "Angular", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-angular.svg")),
        new("brand-aws", StacksYRepositorios, "AWS", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-aws.svg")),
        new("brand-azure", StacksYRepositorios, "Azure", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-azure.svg")),
        new("brand-c-sharp", StacksYRepositorios, "C#", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-c-sharp.svg")),
        new("brand-git", StacksYRepositorios, "Git", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-git.svg")),
        new("brand-github", StacksYRepositorios, "GitHub", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-github.svg")),
        new("brand-gitlab", StacksYRepositorios, "GitLab", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-gitlab.svg")),
        new("brand-golang", StacksYRepositorios, "Go", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-golang.svg")),
        new("brand-laravel", StacksYRepositorios, "Laravel", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-laravel.svg")),
        new("brand-nodejs", StacksYRepositorios, "Node.js", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-nodejs.svg")),
        new("brand-php", StacksYRepositorios, "PHP", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-php.svg")),
        new("brand-powershell", StacksYRepositorios, "PowerShell", ["ps", "pwsh", "winrm", "powershell"], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-powershell.svg")),
        new("brand-python", StacksYRepositorios, "Python", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-python.svg")),
        new("brand-vue", StacksYRepositorios, "Vue", [], ModoDePintado.Trazo, 24, true, Tabler("icons/outline/brand-vue.svg")),
        new("dotnet", StacksYRepositorios, ".NET", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/dotnet.svg")),
        new("gnubash", StacksYRepositorios, "Bash", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/gnubash.svg")),
        new("openjdk", StacksYRepositorios, "Java", [], ModoDePintado.Relleno, 24, true, SimpleIcons("icons/openjdk.svg")),

    ];

    /// <summary>El icono de esa clave, o null si el catálogo no la tiene.</summary>
    /// <param name="clave">Nombre del archivo SVG, sin extensión.</param>
    public static IconoDelCatalogo? Resolver(string? clave) =>
        clave is null ? null : PorClave.GetValueOrDefault(clave);

    /// <summary>Si el catálogo conoce esa clave.</summary>
    /// <param name="clave">Nombre del archivo SVG, sin extensión.</param>
    public static bool EsValido(string? clave) => Resolver(clave) is not null;

    private static readonly Dictionary<string, IconoDelCatalogo> PorClave =
        Iconos.ToDictionary(i => i.Clave, StringComparer.Ordinal);

    private static OrigenDeIcono Tabler(string ruta) =>
        new("@tabler/icons", "3.47.0", ruta, "MIT");

    private static OrigenDeIcono SimpleIcons(string ruta) =>
        new("simple-icons", "16.31.0", ruta, "CC0-1.0");

    private static OrigenDeIcono Devicon(string ruta) =>
        new("devicon", "2.17.0", ruta, "MIT");

}
