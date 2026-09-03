#Requires -Version 7
<#
.SYNOPSIS
    Levanta el servidor OpenSSH contra el que corren las pruebas de integración.

.DESCRIPTION
    Hay cosas de SSH que no se pueden comprobar con dobles: que el saludo prospere, que la
    verificación de la clave del host corte antes de mandar la contraseña, que el canal
    interactivo entregue datos, y que el cambio de tamaño llegue al otro lado. Todo eso vive
    en la conversación con un servidor.

    Sin el contenedor las pruebas se omiten con instrucciones, no fallan.

.NOTES
    Por qué se toca la configuración del servidor:

    - PerSourcePenalties no
      Desde OpenSSH 9.8 el servidor castiga a las direcciones que fallan al autenticar, y las
      descarta durante un rato. Es una defensa contra fuerza bruta y está bien que exista, pero
      una de las pruebas se autentica mal a propósito —hay que comprobar que eso da un rechazo
      de credenciales y no otra cosa— y a partir de ahí sshd cortaba las conexiones siguientes.
      Se veía como dos fallos desconcertantes: una huella de host nula y esperas de quince
      segundos en pruebas que no tenían nada que ver. La pista fue el tiempo: fallaban en 10 ms,
      cuando un saludo SSH honesto tarda cerca de un segundo. No se conectaba nada.

    - AllowTcpForwarding yes
      La imagen lo trae apagado, y sin esto no se puede probar ningún túnel.
#>

[CmdletBinding()]
param(
    [switch]$Down
)

$ErrorActionPreference = 'Stop'

$nombre = 'cmc-sshd'
$imagen = 'lscr.io/linuxserver/openssh-server:latest'
$config = '/config/sshd/sshd_config'

function Esperar-Listo {
    for ($i = 0; $i -lt 40; $i++) {
        $log = docker logs $nombre 2>&1 | Out-String

        if ($log -match 'sshd is listening on port') {
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "El contenedor $nombre no llegó a escuchar. Revisá: docker logs $nombre"
}

docker rm -f $nombre 2>&1 | Out-Null

if ($Down) {
    Write-Host "Servidor de prueba dado de baja." -ForegroundColor Cyan
    exit 0
}

Write-Host "Levantando $nombre…" -ForegroundColor Cyan

function Nueva-Clave {
    -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
}

# Al azar y en cada arranque: una contraseña escrita en el repositorio queda publicada para
# siempre en el historial, y acá no hace falta porque el guion la imprime al terminar.
$usuario = 'prueba'
$clave = Nueva-Clave

# Segundo usuario, con sudo que sí pide contraseña. Con uno solo quedaba sin probar la mitad
# de la escalada: el defecto de CreateInputStream vivió porque «sudo -n» nunca fallaba acá.
$usuarioConClave = 'pruebaclave'
$claveConClave = Nueva-Clave

docker run -d --name $nombre `
    -e PASSWORD_ACCESS=true `
    -e USER_NAME=$usuario `
    -e USER_PASSWORD=$clave `
    -e SUDO_ACCESS=true `
    -p 2222:2222 `
    $imagen | Out-Null

Esperar-Listo

# Las dos opciones van juntas y el servidor se reinicia una sola vez.
$ajustes = @(
    'PerSourcePenalties no'
    'AllowTcpForwarding yes'
)

foreach ($ajuste in $ajustes) {
    $opcion = ($ajuste -split ' ')[0]
    docker exec $nombre sh -c "sed -i '/^$opcion/d' $config; echo '$ajuste' >> $config" | Out-Null
}

docker exec $nombre sh -c @"
adduser -D -s /bin/sh $usuarioConClave
echo '$usuarioConClave ALL=(ALL) PASSWD: ALL' > /etc/sudoers.d/cmc-$usuarioConClave
chmod 440 /etc/sudoers.d/cmc-$usuarioConClave
"@ | Out-Null

# Por la entrada estándar y no en la línea de comandos: ahí la leería el ps del contenedor.
"${usuarioConClave}:${claveConClave}" | docker exec -i $nombre chpasswd | Out-Null

docker restart $nombre | Out-Null
Esperar-Listo

Write-Host "Listo: $usuario@127.0.0.1:2222" -ForegroundColor Green
Write-Host ""
Write-Host "Para que corran las pruebas de integracion, en esta misma consola:" -ForegroundColor Cyan
Write-Host "  `$env:CMC_SSH_PRUEBA_HOST      = '127.0.0.1'"
Write-Host "  `$env:CMC_SSH_PRUEBA_PUERTO    = '2222'"
Write-Host "  `$env:CMC_SSH_PRUEBA_USUARIO   = '$usuario'"
Write-Host "  `$env:CMC_SSH_PRUEBA_CONTRASENA = '$clave'"
Write-Host "  `$env:CMC_SSH_PRUEBA_USUARIO_SUDO   = '$usuarioConClave'"
Write-Host "  `$env:CMC_SSH_PRUEBA_CONTRASENA_SUDO = '$claveConClave'"
Write-Host ""
Write-Host "Sin esas variables las pruebas se omiten con el motivo, no fallan." -ForegroundColor DarkGray
