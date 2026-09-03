using CafManagerConection.Monitoring;

namespace CafManagerConection.Monitoring.Tests;

/// <summary>Copia textual de la salida de tres servidores reales: servidor-uno (Ubuntu 22.04,
/// VMware, xfs, Docker), servidor-dos (Ubuntu 24.04, LVM sobre NVMe, VPN) y servidor-arm (Ubuntu
/// 24.04 aarch64 en Oracle Cloud, Docker Swarm).</summary>
public sealed class DatosRealesTests
{
    // servidor 1: servidor-uno

    private const string DfServidor1 = @"Filesystem                        Type         1-blocks         Used    Available Capacity Mounted on
tmpfs                             tmpfs       3365228544      1499136   3363729408       1% /run
/dev/sda4                         xfs        33266601984  26341982208   6924619776      80% /
tmpfs                             tmpfs      16826142720        36864  16826105856       1% /dev/shm
tmpfs                             tmpfs          5242880            0      5242880       0% /run/lock
/dev/sda3                         ext4        1020702720    272265216    677974016      29% /boot
/dev/sdb1                         xfs       107346923520  28386455552  78960467968      27% /app
tmpfs                             tmpfs       3365228544         4096   3365224448       1% /run/user/1000";

    private const string LinkServidor1 = @"1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN mode DEFAULT group default qlen 1000\    link/loopback 00:00:00:00:00:00 brd 00:00:00:00:00:00
2: ens160: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc mq state UP mode DEFAULT group default qlen 1000\    link/ether 00:00:5e:00:53:01 brd ff:ff:ff:ff:ff:ff\    altname enp3s0
5: docker0: <NO-CARRIER,BROADCAST,MULTICAST,UP> mtu 1500 qdisc noqueue state DOWN mode DEFAULT group default \    link/ether 00:00:5e:00:53:05 brd ff:ff:ff:ff:ff:ff
966: br-0a0aa59171f2: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc noqueue state UP mode DEFAULT group default \    link/ether 00:00:5e:00:53:06 brd ff:ff:ff:ff:ff:ff
993: veth1b11b10@if2: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc noqueue master br-0a0aa59171f2 state UP mode DEFAULT group default \    link/ether 00:00:5e:00:53:04 brd ff:ff:ff:ff:ff:ff link-netnsid 0";

    private const string AddrServidor1 = @"1: lo    inet 127.0.0.1/8 scope host lo\       valid_lft forever preferred_lft forever
2: ens160    inet 192.0.2.200/26 brd 192.0.2.255 scope global ens160\       valid_lft forever preferred_lft forever
5: docker0    inet 198.18.0.1/16 brd 198.18.255.255 scope global docker0\       valid_lft forever preferred_lft forever
966: br-0a0aa59171f2    inet 198.19.0.1/16 brd 198.19.255.255 scope global br-0a0aa59171f2\       valid_lft forever preferred_lft forever";

    private const string RutasServidor1 = @"default via 192.0.2.193 dev ens160 proto static
198.19.0.0/16 dev br-0a0aa59171f2 proto kernel scope link src 198.19.0.1
198.18.0.0/16 dev docker0 proto kernel scope link src 198.18.0.1 linkdown
192.0.2.192/26 dev ens160 proto kernel scope link src 192.0.2.200";

    // servidor 2: servidor-dos

    private const string DfServidor2 = @"Filesystem                        Type         1-blocks         Used    Available Capacity Mounted on
tmpfs                             tmpfs       819515392      1171456    818343936       1% /run
efivarfs                          efivarfs       196608        27982       163506      15% /sys/firmware/efi/efivars
/dev/mapper/ubuntu--vg-ubuntu--lv ext4     500235124736 165938720768 312831528960      35% /
tmpfs                             tmpfs      4097572864   2027167744   2070405120      50% /dev/shm
tmpfs                             tmpfs         5242880            0      5242880       0% /run/lock
/dev/nvme0n1p2                    ext4       2040373248    205094912   1711128576      11% /boot
/dev/nvme0n1p1                    vfat       1124999168      6438912   1118560256       1% /boot/efi
tmpfs                             tmpfs       819511296        12288    819499008       1% /run/user/1000";

    private const string AddrServidor2 = @"1: lo    inet 127.0.0.1/8 scope host lo\       valid_lft forever preferred_lft forever
1: lo    inet6 ::1/128 scope host noprefixroute \       valid_lft forever preferred_lft forever
3: enp2s0    inet 203.0.113.2/24 brd 203.0.113.255 scope global enp2s0\       valid_lft forever preferred_lft forever
3: enp2s0    inet6 2001:db8:a::4bf/128 scope global noprefixroute \       valid_lft forever preferred_lft forever
3: enp2s0    inet6 2001:db8:a:0:2d0:dff:fe00:2b0b/64 scope global mngtmpaddr noprefixroute 
3: enp2s0    inet6 fe80::2d0:dff:fe00:2b0b/64 scope link \       valid_lft forever preferred_lft forever
18: tun0    inet 198.18.6.12/16 brd 198.18.255.255 scope global tun0\       valid_lft forever preferred_lft forever
18: tun0    inet6 fe80::836d:bc7:e921:af16/64 scope link stable-privacy \       valid_lft forever preferred_lft forever";

    private const string LinkServidor2 = @"1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN mode DEFAULT group default qlen 1000\    link/loopback 00:00:00:00:00:00 brd 00:00:00:00:00:00
2: enp1s0: <BROADCAST,MULTICAST> mtu 1500 qdisc noop state DOWN mode DEFAULT group default qlen 1000\    link/ether 00:00:5e:00:53:02 brd ff:ff:ff:ff:ff:ff
3: enp2s0: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc fq_codel state UP mode DEFAULT group default qlen 1000\    link/ether 00:00:5e:00:53:03 brd ff:ff:ff:ff:ff:ff
18: tun0: <POINTOPOINT,MULTICAST,NOARP,UP,LOWER_UP> mtu 1500 qdisc fq_codel state UNKNOWN mode DEFAULT group default qlen 500\    link/none";

    private const string Rutas6Servidor2 = @"2001:db8:a::/64 dev enp2s0 proto ra metric 1024 mtu 1500 hoplimit 64 pref medium
unreachable 2001:db8:a:4::/62 dev lo proto dhcp metric 1024 pref medium
2001:db8:a::/48 via fe80::9222:6ff:fe01:283 dev enp2s0 proto ra metric 1024 mtu 1500 hoplimit 64 pref medium
fe80::/64 dev enp2s0 proto kernel metric 256 pref medium";

    private const string TopCpuServidor2 = @"    PID    PPID   UID %CPU %MEM   RSS ELAPSED STAT NLWP COMMAND
    907     730  1000  341 16.2 1298188 704488 SLl   82 malva
  39971   39906  1000  100  0.0  4344       0 R+      1 ps
    822       1   107 12.6  1.8 148456 704489 Ssl    23 mariadbd
     71       2     0  0.2  0.0     0  704494 S       1 kcompactd0
  37535       2     0  0.1  0.0     0   40227 I       1 kworker/0:0-events";

    private const string PresionCpuServidor2 =
        "some avg10=22.85 avg60=24.53 avg300=23.09 total=88740689665\nfull avg10=0.00 avg60=0.00 avg300=0.00 total=0";

    private const string SensoresServidor2 = @"pch_cannonlake-virtual-0
Adapter: Virtual device
temp1:
  temp1_input: 49.000

coretemp-isa-0000
Adapter: ISA adapter
Package id 0:
  temp1_input: 56.000
  temp1_max: 100.000
  temp1_crit: 100.000
  temp1_crit_alarm: 0.000
Core 0:
  temp2_input: 54.000
  temp2_max: 100.000";

    // servidor 3: servidor-arm (aarch64)

    private const string DfServidor3 = @"Filesystem     Type        1-blocks        Used   Available Capacity Mounted on
tmpfs          tmpfs     1250680832     2842624  1247838208       1% /run
efivarfs       efivarfs      262044       13908      248136       6% /sys/firmware/efi/efivars
/dev/sda1      ext4     93455634432 54177390592 39261466624      58% /
tmpfs          tmpfs     6253400064           0  6253400064       0% /dev/shm
tmpfs          tmpfs        5242880           0     5242880       0% /run/lock
/dev/sda15     vfat       102195200     6638080    95557120       7% /boot/efi
tmpfs          tmpfs     1250676736        8192  1250668544       1% /run/user/1001";

    private const string RutasServidor3 = @"default via 192.0.2.1 dev enp0s6 proto dhcp src 192.0.2.65 metric 100
192.0.2.0/24 dev enp0s6 proto kernel scope link src 192.0.2.65 metric 100
169.254.169.254 dev enp0s6 proto dhcp scope link src 192.0.2.65 metric 100
198.18.0.0/16 dev docker0 proto kernel scope link src 198.18.0.1
198.19.0.0/16 dev br-585a66b44058 proto kernel scope link src 198.19.0.1 linkdown
203.0.113.0/24 dev docker_gwbridge proto kernel scope link src 203.0.113.1 linkdown";

    private const string TopCpuServidor3 = @"    PID    PPID   UID %CPU %MEM   RSS ELAPSED STAT NLWP COMMAND
1314541    2632    82 10.4  0.5 70376       2 S       1 php
1314234    2642    82  2.8  0.5 66112      12 S       1 php
   4746    4723   999  0.9  2.9 359072  83025 Ssl    22 mariadbd
1053268 1053156    82  0.5  0.4 56816   14148 S       1 php
   5119    5072    82  0.4  1.6 206816  83016 Sl     17 frankenphp";

    private const string DiskstatsServidor3 = @"   7       0 loop0 41 0 688 60 0 0 0 0 0 57 60 0 0 0 0 0 0
   8       0 sda 348653 34975 15760795 813523 990042 750336 44746892 2156249 0 788788 3020379 61339 20990 33771480 50606 0 0
   8       1 sda1 348345 34420 15733917 813227 990040 750336 44746890 2156247 0 833578 3020081 61339 20990 33771480 50606 0 0
   8      15 sda15 212 555 23118 192 2 0 2 2 0 130 194 0 0 0 0 0 0";

    private const string MeminfoSinSwap = @"MemTotal:       12213676 kB
SwapTotal:             0 kB
SwapFree:              0 kB";

    private const string MeminfoConSwap = @"MemTotal:       32863560 kB
SwapTotal:      12407800 kB
SwapFree:       12398072 kB";

    [Fact]
    public void Los_discos_del_servidor_1_son_los_tres_reales()
    {
        var discos = DiskUsageParser.Parse(DfServidor1);

        Assert.Equal(3, discos.Count);
        Assert.Equal(["/", "/boot", "/app"], discos.Select(d => d.MountPoint));
        Assert.Equal(["xfs", "ext4", "xfs"], discos.Select(d => d.Type));

        var raiz = discos[0];
        Assert.Equal("/dev/sda4", raiz.FileSystem);
        Assert.Equal(33266601984, raiz.TotalBytes);
        Assert.InRange(raiz.UsedPercent, 79, 80);
    }

    [Fact]
    public void Los_discos_del_servidor_2_incluyen_el_LVM_y_el_arranque_EFI()
    {
        var discos = DiskUsageParser.Parse(DfServidor2);

        Assert.Equal(["/", "/boot", "/boot/efi"], discos.Select(d => d.MountPoint));
        Assert.Equal("/dev/mapper/ubuntu--vg-ubuntu--lv", discos[0].FileSystem);
        Assert.Equal("vfat", discos[2].Type);

        // efivarfs: 196608 bytes de variables de firmware al 15%, sin ser almacenamiento real.
        Assert.DoesNotContain("/sys/firmware/efi/efivars", discos.Select(d => d.MountPoint));
    }

    [Fact]
    public void Los_discos_del_servidor_ARM_son_los_dos_reales()
    {
        var discos = DiskUsageParser.Parse(DfServidor3);

        Assert.Equal(["/", "/boot/efi"], discos.Select(d => d.MountPoint));
        Assert.Equal("ext4", discos[0].Type);
    }

    [Fact]
    public void La_salida_sin_columna_de_tipo_se_sigue_leyendo()
    {
        var sinTipo = @"Filesystem         1-blocks        Used   Available Capacity Mounted on
/dev/sda4       33266601984 26341982208  6924619776      80% /
/dev/sdb1      107346923520 28386455552 78960467968      27% /app";

        var discos = DiskUsageParser.Parse(sinTipo);

        Assert.Equal(["/", "/app"], discos.Select(d => d.MountPoint));
        Assert.All(discos, d => Assert.Null(d.Type));
        Assert.Equal(33266601984, discos[0].TotalBytes);
    }

    [Fact]
    public void La_particion_no_se_cuenta_aparte_del_disco()
    {
        var antes = DiskIoParser.Parse(DiskstatsServidor3);
        var lista = antes.Select(a => a.Device).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("loop0", lista);
        Assert.Contains("sda", lista);

        Assert.True(DiskIoParser.EsParticionDe("sda1", lista));
        Assert.True(DiskIoParser.EsParticionDe("sda15", lista));
        Assert.False(DiskIoParser.EsParticionDe("sda", lista));
    }

    /// <remarks><c>dm-0</c> (LVM) y <c>md0</c> (RAID) cuentan la misma entrada y salida que el
    /// disco físico de abajo; el nombre solo no alcanza para resolverlo, hace falta
    /// <c>lsblk</c>.</remarks>
    [Fact]
    public void Un_volumen_logico_no_cuenta_aparte_del_disco_fisico()
    {
        var lsblk = @"nvme0n1 disk
nvme0n1p1 part
nvme0n1p2 part
nvme0n1p3 part
ubuntu--vg-ubuntu--lv lvm
dm-0 lvm
loop0 loop";

        var discos = DiskIoParser.DiscosEnteros(lsblk);

        Assert.Equal(["nvme0n1"], discos);

        var antes = new List<DiskIoSample> { new("nvme0n1", 0, 0, 0), new("dm-0", 0, 0, 0) };

        var despues = new List<DiskIoSample>
        {
            new("nvme0n1", 100, 100, 10),
            new("dm-0", 100, 100, 10),
        };

        var io = DiskIoParser.Between(antes, despues, 1, discos);

        Assert.Equal("nvme0n1", Assert.Single(io).Device);
    }

    /// <remarks>Respaldo para servidores sin <c>lsblk</c>: una máquina real con once volúmenes
    /// lógicos perdía <c>dm-10</c> del panel al adivinar por sufijo numérico.</remarks>
    [Fact]
    public void Adivinar_por_el_nombre_no_confunde_dm_10_con_dm_1()
    {
        var nombres = new HashSet<string>(StringComparer.Ordinal) { "dm-0", "dm-1", "dm-10" };

        Assert.False(DiskIoParser.EsParticionDe("dm-10", nombres));
        Assert.False(DiskIoParser.EsParticionDe("dm-1", nombres));
        Assert.False(DiskIoParser.EsParticionDe("dm-0", nombres));
    }

    [Fact]
    public void Adivinar_por_el_nombre_no_confunde_md127_con_md1()
    {
        var nombres = new HashSet<string>(StringComparer.Ordinal) { "md0", "md1", "md127" };

        Assert.False(DiskIoParser.EsParticionDe("md127", nombres));
    }

    [Fact]
    public void Sin_lsblk_se_sigue_filtrando_por_el_nombre()
    {
        var antes = new List<DiskIoSample> { new("sda", 0, 0, 0), new("sda1", 0, 0, 0) };

        var despues = new List<DiskIoSample>
        {
            new("sda", 100, 0, 10),
            new("sda1", 100, 0, 10),
        };

        var io = DiskIoParser.Between(antes, despues, 1, DiskIoParser.DiscosEnteros(string.Empty));

        Assert.Equal("sda", Assert.Single(io).Device);
    }

    /// <remarks>Firefox usa «Web Content» como nombre de proceso.</remarks>
    [Fact]
    public void Un_nombre_de_proceso_con_espacio_llega_entero()
    {
        var salida = @"    PID    PPID   UID %CPU %MEM   RSS ELAPSED STAT NLWP COMMAND
   4711    1234 operador              2.0  1.0 65536     600 Sl     12 Web Content";

        var proceso = Assert.Single(TopProcessesParser.Parse(salida));

        Assert.Equal("Web Content", proceso.Command);
        Assert.Equal(12, proceso.Threads);
    }

    /// <remarks><c>df</c> traduce su encabezado (Type→Tipo) en un servidor con locale en
    /// español.</remarks>
    [Fact]
    public void Un_servidor_con_locale_en_espanol_igual_muestra_sus_discos()
    {
        var enEspanol = @"S.ficheros     Tipo         1-bloques        Usados Disponibles Capacidad Montado en
/dev/sda4      xfs        33266601984   26341982208  6924619776       80% /
/dev/sdb1      xfs       107346923520   28386455552 78960467968       27% /app";

        var discos = DiskUsageParser.Parse(enEspanol);

        Assert.Equal(["/", "/app"], discos.Select(d => d.MountPoint));
        Assert.Equal("xfs", discos[0].Type);
    }

    [Fact]
    public void Una_salida_sin_encabezado_no_pierde_su_primera_fila()
    {
        var sinEncabezado = @"/dev/sda4      xfs        33266601984   26341982208  6924619776       80% /
/dev/sdb1      xfs       107346923520   28386455552 78960467968       27% /app";

        Assert.Equal(2, DiskUsageParser.Parse(sinEncabezado).Count);
    }

    [Fact]
    public void Un_punto_de_montaje_con_espacio_llega_entero()
    {
        var conEspacio = @"Filesystem     Type        1-blocks        Used   Available Capacity Mounted on
/dev/sdc1      ext4     93455634432 54177390592 39261466624      58% /mnt/disco viejo";

        var disco = Assert.Single(DiskUsageParser.Parse(conEspacio));

        Assert.Equal("/mnt/disco viejo", disco.MountPoint);
    }

    [Fact]
    public void Un_montaje_que_solo_empieza_parecido_no_se_descarta()
    {
        var salida = @"Filesystem     Type        1-blocks        Used   Available Capacity Mounted on
/dev/sdd1      xfs      93455634432 54177390592 39261466624      58% /snapshots
/dev/sde1      xfs      93455634432 54177390592 39261466624      58% /runtime
/dev/sdf1      xfs      93455634432 54177390592 39261466624      58% /snap/core20/1";

        var discos = DiskUsageParser.Parse(salida);

        Assert.Equal(["/snapshots", "/runtime"], discos.Select(d => d.MountPoint));
    }

    [Fact]
    public void Una_particion_NVMe_se_reconoce_como_tal()
    {
        var nombres = new HashSet<string>(StringComparer.Ordinal) { "nvme0n1", "nvme0n1p1", "nvme0n1p3" };

        Assert.True(DiskIoParser.EsParticionDe("nvme0n1p3", nombres));
        Assert.False(DiskIoParser.EsParticionDe("nvme0n1", nombres));
    }

    [Fact]
    public void La_velocidad_de_disco_sale_de_la_diferencia_entre_dos_lecturas()
    {
        var antes = new List<DiskIoSample> { new("sda", 1000, 2000, 500) };
        var despues = new List<DiskIoSample> { new("sda", 3000, 6000, 1500) };

        var io = DiskIoParser.Between(antes, despues, 2);

        var sda = Assert.Single(io);

        Assert.Equal(512_000, sda.ReadBytesPerSecond);
        Assert.Equal(1_024_000, sda.WriteBytesPerSecond);
        Assert.Equal(50, sda.BusyPercent);
    }

    [Fact]
    public void Un_contador_que_retrocede_no_da_velocidad_negativa()
    {
        var antes = new List<DiskIoSample> { new("sda", 9000, 9000, 9000) };
        var despues = new List<DiskIoSample> { new("sda", 10, 10, 10) };

        Assert.Empty(DiskIoParser.Between(antes, despues, 2));
    }

    [Fact]
    public void La_interfaz_del_servidor_1_trae_su_IP_su_MAC_y_su_MTU()
    {
        var interfaces = InterfacesParser.Parse(LinkServidor1, AddrServidor1);

        var ens160 = interfaces.Single(i => i.Name == "ens160");

        Assert.Equal("00:00:5e:00:53:01", ens160.MacAddress);
        Assert.Equal(1500, ens160.Mtu);
        Assert.Equal("UP", ens160.State);
        Assert.True(ens160.IsUp);
        Assert.Equal(["192.0.2.200/26"], ens160.IPv4);
        Assert.False(ens160.EsDeContenedor);
    }

    [Fact]
    public void Una_interfaz_sin_portador_no_figura_levantada()
    {
        var interfaces = InterfacesParser.Parse(LinkServidor1, AddrServidor1);

        var docker0 = interfaces.Single(i => i.Name == "docker0");

        Assert.False(docker0.IsUp);
        Assert.Equal("DOWN", docker0.State);
        Assert.True(docker0.EsDeContenedor);
    }

    [Fact]
    public void El_otro_extremo_del_par_no_es_parte_del_nombre()
    {
        var interfaces = InterfacesParser.Parse(LinkServidor1, AddrServidor1);

        var veth = interfaces.Single(i => i.Name.StartsWith("veth", StringComparison.Ordinal));

        Assert.Equal("veth1b11b10", veth.Name);
        Assert.Equal("br-0a0aa59171f2", veth.Master);
        Assert.True(veth.EsDeContenedor);
    }

    [Fact]
    public void El_tunel_VPN_no_se_confunde_con_una_interfaz_de_contenedor()
    {
        var interfaces = InterfacesParser.Parse(LinkServidor2, AddrServidor2);

        var tun0 = interfaces.Single(i => i.Name == "tun0");

        Assert.False(tun0.EsDeContenedor);
        Assert.Equal(["198.18.6.12/16"], tun0.IPv4);
        Assert.Null(tun0.MacAddress);
        Assert.Equal("UNKNOWN", tun0.State);
        Assert.True(tun0.IsUp);
    }

    [Fact]
    public void Una_interfaz_con_varias_IPv6_las_trae_todas()
    {
        var interfaces = InterfacesParser.Parse(LinkServidor2, AddrServidor2);

        var enp2s0 = interfaces.Single(i => i.Name == "enp2s0");

        Assert.Equal(["203.0.113.2/24"], enp2s0.IPv4);
        Assert.Equal(3, enp2s0.IPv6.Count);
        Assert.Contains("2001:db8:a::4bf/128", enp2s0.IPv6);
    }

    [Fact]
    public void Una_interfaz_sin_ninguna_direccion_igual_aparece()
    {
        var interfaces = InterfacesParser.Parse(LinkServidor2, AddrServidor2);

        var enp1s0 = interfaces.Single(i => i.Name == "enp1s0");

        Assert.Empty(enp1s0.IPv4);
        Assert.Empty(enp1s0.IPv6);
        Assert.False(enp1s0.IsUp);
    }

    [Fact]
    public void La_ruta_predeterminada_se_reconoce_con_su_puerta()
    {
        var rutas = RoutesParser.Parse(RutasServidor1, string.Empty);

        var predeterminada = Assert.Single(rutas, r => r.EsPredeterminada);

        Assert.Equal("192.0.2.193", predeterminada.Gateway);
        Assert.Equal("ens160", predeterminada.Device);
        Assert.False(predeterminada.LinkDown);
    }

    [Fact]
    public void Una_ruta_sobre_una_interfaz_caida_queda_marcada()
    {
        var rutas = RoutesParser.Parse(RutasServidor3, string.Empty);

        Assert.Equal(2, rutas.Count(r => r.LinkDown));
        Assert.Contains(rutas, r => r.Device == "docker_gwbridge" && r.LinkDown);

        var predeterminada = rutas.Single(r => r.EsPredeterminada);
        Assert.Equal(100, predeterminada.Metric);
        Assert.Equal("192.0.2.65", predeterminada.Source);
    }

    /// <remarks><c>ip -6 route</c>: <c>unreachable 2001:db8:…/62 dev lo</c> trae el tipo de ruta
    /// como primer campo, no el destino.</remarks>
    [Fact]
    public void El_tipo_de_ruta_no_se_confunde_con_el_destino()
    {
        var rutas = RoutesParser.Parse(string.Empty, Rutas6Servidor2);

        Assert.DoesNotContain(rutas, r => r.Destination == "unreachable");
        Assert.Contains(rutas, r => r.Destination == "2001:db8:a:4::/62");
        Assert.All(rutas, r => Assert.True(r.IsIPv6));

        var conPuerta = rutas.Single(r => r.Gateway is not null);
        Assert.Equal("fe80::9222:6ff:fe01:283", conPuerta.Gateway);
    }

    /// <remarks>341% es correcto: proceso con 82 hilos en una máquina de 8 núcleos.</remarks>
    [Fact]
    public void Un_proceso_con_muchos_hilos_puede_pasar_del_cien_por_ciento()
    {
        var procesos = TopProcessesParser.Parse(TopCpuServidor2);

        var malva = procesos[0];

        Assert.Equal(907, malva.Pid);
        Assert.Equal(730, malva.ParentPid);
        Assert.Equal("1000", malva.User);
        Assert.Equal(341, malva.CpuPercent);
        Assert.Equal(16.2, malva.MemoryPercent);
        Assert.Equal(1298188L * 1024, malva.ResidentBytes);
        Assert.Equal(82, malva.Threads);
        Assert.Equal("malva", malva.Command);
        Assert.Equal(TimeSpan.FromSeconds(704488), malva.Elapsed);
    }

    /// <remarks>El UID de un usuario de contenedor no está en el <c>/etc/passwd</c> del
    /// servidor.</remarks>
    [Fact]
    public void Un_UID_sin_nombre_se_muestra_como_numero()
    {
        var procesos = TopProcessesParser.Parse(TopCpuServidor3);

        Assert.Equal("82", procesos[0].User);
        Assert.Equal("php", procesos[0].Command);
        Assert.Equal(10.4, procesos[0].CpuPercent);

        var conNombres = TopProcessesParser.ConNombres(
            procesos, DatosDeSistemaParser.UsuariosPorUid("root:0"));

        Assert.Equal("82", conNombres[0].User);
    }

    [Fact]
    public void El_UID_se_reemplaza_por_el_nombre_cuando_passwd_lo_tiene()
    {
        var passwd = @"root:0
ubuntu:1000
mysql:107";

        var usuarios = DatosDeSistemaParser.UsuariosPorUid(passwd);
        var procesos = TopProcessesParser.ConNombres(
            TopProcessesParser.Parse(TopCpuServidor2), usuarios);

        Assert.Equal("ubuntu", procesos[0].User);
        Assert.Equal("mysql", procesos.Single(p => p.Command == "mariadbd").User);
        Assert.Equal("root", procesos.Single(p => p.Command == "kcompactd0").User);
    }

    /// <remarks>Defecto que motivó pedir el UID en vez del nombre: un nombre con espacio corría
    /// todas las columnas siguientes.</remarks>
    [Fact]
    public void Ningun_campo_de_la_izquierda_puede_tener_espacios()
    {
        Assert.DoesNotContain("user", TopProcessesParser.Formato);
        Assert.Contains("uid", TopProcessesParser.Formato);
        Assert.EndsWith("comm", TopProcessesParser.Formato);
    }

    [Fact]
    public void Un_proceso_del_nucleo_sin_memoria_residente_no_rompe_la_lista()
    {
        var procesos = TopProcessesParser.Parse(TopCpuServidor2);

        var kcompactd = procesos.Single(p => p.Command == "kcompactd0");

        Assert.Equal(0, kcompactd.ResidentBytes);
        Assert.Equal("S", kcompactd.State);
    }

    [Fact]
    public void El_encabezado_de_ps_no_se_toma_como_un_proceso()
    {
        var procesos = TopProcessesParser.Parse(TopCpuServidor2);

        Assert.DoesNotContain(procesos, p => p.Command == "COMMAND");
        Assert.Equal(5, procesos.Count);
    }

    [Fact]
    public void El_nombre_de_un_proceso_del_nucleo_llega_entero()
    {
        var procesos = TopProcessesParser.Parse(TopCpuServidor2);

        Assert.Contains(procesos, p => p.Command == "kworker/0:0-events");
    }

    [Fact]
    public void La_presion_de_CPU_se_lee_del_promedio_de_diez_segundos()
    {
        var presion = Assert.NotNull(PressureParser.Una(PresionCpuServidor2));

        Assert.Equal(22.85, presion.Some);
        Assert.Equal(0, presion.Full);
    }

    /// <remarks>Es por recurso y no una bandera única: en un contenedor con lxcfs puede estar
    /// visible <c>/proc/pressure/cpu</c> y enmascarados los otros dos.</remarks>
    [Fact]
    public void Un_recurso_sin_presion_informada_no_dice_cero()
    {
        var mezcla = PressureParser.Parse(PresionCpuServidor2, string.Empty, string.Empty);

        Assert.True(mezcla.Disponible);
        Assert.NotNull(mezcla.Cpu);
        Assert.Null(mezcla.Io);
        Assert.Null(mezcla.Memory);
    }

    /// <remarks>Sin <c>CONFIG_PSI</c> los archivos no existen y los tramos llegan vacíos.</remarks>
    [Fact]
    public void Un_nucleo_sin_presion_se_informa_como_no_disponible()
    {
        var sin = PressureParser.Parse(string.Empty, string.Empty, string.Empty);

        Assert.False(sin.Disponible);

        var con = PressureParser.Parse(PresionCpuServidor2, string.Empty, string.Empty);

        Assert.True(con.Disponible);
        Assert.Equal(22.85, Assert.NotNull(con.Cpu).Some);
    }

    /// <remarks><c>grep 'model name' /proc/cpuinfo</c> no devuelve nada en aarch64.</remarks>
    [Fact]
    public void En_x86_el_modelo_de_CPU_sale_de_cpuinfo()
    {
        var cpuinfo = "model name      : Intel(R) Xeon(R) Silver 4216 CPU @ 2.10GHz";

        Assert.Equal(
            "Intel(R) Xeon(R) Silver 4216 CPU @ 2.10GHz",
            DatosDeSistemaParser.ModeloDeCpu(cpuinfo));
    }

    [Fact]
    public void En_aarch64_sin_model_name_se_cae_a_lscpu()
    {
        var lscpu = "Model name:                           Neoverse-N1";

        Assert.Equal("Neoverse-N1", DatosDeSistemaParser.ModeloDeCpu(string.Empty, lscpu));
    }

    [Fact]
    public void En_aarch64_sin_lscpu_se_arma_con_implementador_y_parte()
    {
        var cpuinfo = "CPU implementer : 0x41\nCPU part        : 0xd0c";

        Assert.Equal("ARM 0xd0c", DatosDeSistemaParser.ModeloDeCpu(cpuinfo));
    }

    [Fact]
    public void Sin_ninguna_fuente_el_modelo_de_CPU_es_nulo()
    {
        Assert.Null(DatosDeSistemaParser.ModeloDeCpu(string.Empty, string.Empty));
    }

    [Fact]
    public void El_DNS_y_el_dominio_de_busqueda_salen_de_resolv_conf()
    {
        var resolv = @"# generado por systemd-resolved
nameserver 127.0.0.53
options edns0 trust-ad
search example.com";

        var (servidores, busqueda) = DatosDeSistemaParser.Dns(resolv);

        Assert.Equal(["127.0.0.53"], servidores);
        Assert.Equal("example.com", busqueda);
    }

    [Fact]
    public void Un_servidor_sin_swap_no_informa_uso_ni_divide_por_cero()
    {
        var swap = DatosDeSistemaParser.Swap(MeminfoSinSwap);

        Assert.False(swap.Existe);
        Assert.Equal(0, swap.UsedPercent);
    }

    [Fact]
    public void El_swap_usado_es_el_total_menos_el_libre()
    {
        var swap = DatosDeSistemaParser.Swap(MeminfoConSwap);

        Assert.True(swap.Existe);
        Assert.Equal(12407800L * 1024, swap.TotalBytes);
        Assert.Equal((12407800L - 12398072L) * 1024, swap.UsedBytes);
        Assert.InRange(swap.UsedPercent, 0.07, 0.09);
    }

    /// <remarks><c>temp1_max</c> y <c>temp1_crit</c> son configuración del sensor (100°C acá);
    /// colarlos daría una temperatura que no existe.</remarks>
    [Fact]
    public void Las_temperaturas_se_leen_con_el_nombre_de_su_bloque()
    {
        var temperaturas = DatosDeSistemaParser.Temperaturas(SensoresServidor2);

        Assert.Equal(3, temperaturas.Count);
        Assert.Contains(temperaturas, t => t.Sensor == "Package id 0" && t.Celsius == 56);
        Assert.Contains(temperaturas, t => t.Sensor == "Core 0" && t.Celsius == 54);
        Assert.DoesNotContain(temperaturas, t => t.Celsius == 100);
    }

    [Fact]
    public void Un_servidor_sin_lm_sensors_no_informa_ninguna_temperatura()
    {
        Assert.Empty(DatosDeSistemaParser.Temperaturas("sin lm-sensors"));
    }
}
