; Instalador de CafManagerConection.
;
; Empaqueta lo que ya produce `task publish`: la carpeta self-contained para win-x64. No compila
; nada ni sabe de .NET; si la carpeta no esta, falla y lo dice.
;
; Decisiones que vienen de la constitucion del proyecto y no del gusto:
;
;   * Va a %ProgramFiles%, y por eso pide elevacion AL INSTALAR. El Principio VI dice que la
;     aplicacion no debe exigir privilegios de administrador para FUNCIONAR, que es otra cosa:
;     el acceso directo la lanza sin elevar, y en ejecucion no toca nada fuera de %LocalAppData%.
;
;   * El desinstalador NO borra los datos del usuario ni sus credenciales. Las conexiones viven en
;     %LocalAppData%\CafManagerConection y las contrasenas en el Administrador de credenciales de
;     Windows. Desinstalar para reinstalar es lo normal, y llevarse el arbol de servidores de
;     alguien por ese camino no tiene vuelta atras. Se ofrece borrarlos, apagado por omision.

Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

; ---------------------------------------------------------------- identidad

; NOMBRE identifica la instalacion y NO se cambia: es la carpeta de %ProgramFiles%, la clave del
; registro y la clave de desinstalacion. Cambiarlo dejaria las instalaciones existentes
; huerfanas —el instalador nuevo no encontraria al anterior para desinstalarlo primero— y el
; equipo terminaria con dos copias, que es el defecto que arreglo T376.
;
; NOMBRE_VISIBLE es lo unico que ve el usuario: el titulo del instalador, el acceso directo del
; menu Inicio, el del escritorio y el nombre en «Aplicaciones instaladas». Va separado en palabras
; porque «CafManagerConection» todo junto no se lee.
!define NOMBRE         "CafManagerConection"
!define NOMBRE_VISIBLE "Caf Manager Conection"
!define NOMBRE_LARGO   "Caf Manager Conection (CMC)"
!define EJECUTABLE     "cmc.exe"
!define EMPRESA     "CafTech"
!define CLAVE_DESINSTALAR "Software\Microsoft\Windows\CurrentVersion\Uninstall\${NOMBRE}"

; La carpeta publicada y la de salida llegan por linea de comando desde el Taskfile. Los valores
; de aca son los de una corrida a mano desde la raiz del repositorio.
!ifndef ORIGEN
  !define ORIGEN "..\publish\CafManagerConection"
!endif

!ifndef SALIDA
  !define SALIDA "..\publish\CafManagerConection-setup.exe"
!endif

; Con REQUIERE_RUNTIME definido, el instalador empaqueta la version dependiente del framework
; —nueve megas en lugar de ciento ochenta— y comprueba que el runtime de escritorio este en la
; maquina. Sin el flag empaqueta la self-contained y no comprueba nada, que es lo que hace falta
; para un equipo sin internet.
; El tipo instalado queda en el registro para que el aviso de version nueva ofrezca el mismo:
; src/CafManagerConection.App/Services/SelectorDeInstalador.cs lo lee, sin elevacion.
!ifdef REQUIERE_RUNTIME
  !define TIPO_DE_INSTALADOR "liviano"
!else
  !define TIPO_DE_INSTALADOR "completo"
!endif

!define RUNTIME_MAYOR "10.0"
; La pagina de descarga, no el .exe directo: el enlace directo arranca una descarga sin avisar, y
; en un equipo ajeno eso es justo lo que uno no quiere que pase solo. Una pagina deja ver de que se
; trata antes de bajar nada.
;
; El mensaje nombra exactamente cual de las descargas hace falta, que es donde uno se equivoca: la
; pagina ofrece SDK, runtime, runtime de escritorio y ASP.NET, y solo uno de los cuatro sirve.
!define RUNTIME_URL "https://dotnet.microsoft.com/es-es/download/dotnet/10.0"

; La version se lee del ejecutable publicado, no se repite aca: dos numeros que hay que acordarse
; de mover juntos terminan separados.
!getdllversion "${ORIGEN}\${EJECUTABLE}" VERSION_
!define VERSION "${VERSION_1}.${VERSION_2}.${VERSION_3}"

Name "${NOMBRE_LARGO}"
OutFile "${SALIDA}"
InstallDir "$PROGRAMFILES64\${NOMBRE}"
InstallDirRegKey HKLM "Software\${NOMBRE}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${NOMBRE_LARGO} ${VERSION}"

VIProductVersion "${VERSION_1}.${VERSION_2}.${VERSION_3}.0"
VIAddVersionKey "ProductName"     "${NOMBRE_LARGO}"
VIAddVersionKey "CompanyName"     "${EMPRESA}"
VIAddVersionKey "FileDescription" "Instalador de ${NOMBRE_LARGO}"
VIAddVersionKey "FileVersion"     "${VERSION}"
VIAddVersionKey "ProductVersion"  "${VERSION}"
VIAddVersionKey "LegalCopyright"  "${EMPRESA}"

; ---------------------------------------------------------------- apariencia

!define MUI_ABORTWARNING
!define MUI_ICON   "..\src\CafManagerConection.App\Assets\cmc.ico"
!define MUI_UNICON "..\src\CafManagerConection.App\Assets\cmc.ico"

!define MUI_FINISHPAGE_RUN "$INSTDIR\${EJECUTABLE}"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir ${NOMBRE_VISIBLE}"

; El ejecutable se lanza SIN elevar aunque el instalador este elevado: si se abriera como
; administrador, escribiria su base y sus credenciales en el perfil del administrador y no en el
; del usuario, y despues la aplicacion arrancaria vacia.
!define MUI_FINISHPAGE_RUN_FUNCTION AbrirSinElevar

; La pagina de componentes hace falta de verdad y no es un adorno: sin ella, el acceso directo del
; escritorio —declarado apagado por omision— no habria forma de encenderlo.
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
UninstPage custom un.PaginaDatos un.LeerPaginaDatos
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Spanish"

; ---------------------------------------------------------------- instalacion

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "${NOMBRE_VISIBLE} es de 64 bits y este Windows no lo es."
    Abort
  ${EndIf}

  SetRegView 64
FunctionEnd

; Comprueba si la aplicacion esta corriendo, sin complementos.
;
; Se intenta abrir el ejecutable para escritura: Windows lo mantiene bloqueado mientras el proceso
; vive, asi que si el intento falla es que esta abierto. Es la forma que no depende de FindProcDLL
; ni de nsProcess, que no vienen con NSIS y obligarian a instalarlos en cada maquina que arme una
; version.
;
; Hace falta comprobarlo: instalar encima de una copia en uso deja los archivos a medio reemplazar
; y la version vieja corriendo, y eso no da error hasta la proxima vez que alguien la abre.
!macro CerrarSiEstaAbierto un
Function ${un}CerrarSiEstaAbierto
  IfFileExists "$INSTDIR\${EJECUTABLE}" 0 libre

  reintentar:
    ClearErrors
    FileOpen $0 "$INSTDIR\${EJECUTABLE}" a

    IfErrors bloqueado
    FileClose $0
    Goto libre

  bloqueado:
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION \
      "${NOMBRE_VISIBLE} esta abierto.$\r$\n$\r$\nCerralo y volve a intentar." \
      IDRETRY reintentar
    Abort

  libre:
FunctionEnd
!macroend

!insertmacro CerrarSiEstaAbierto ""
!insertmacro CerrarSiEstaAbierto "un."

!ifdef REQUIERE_RUNTIME

Var TieneRuntime

; Comprueba si esta el runtime de escritorio de .NET, sin complementos.
;
; Dos caminos, porque uno solo deja fuera casos reales:
;
;   1. La carpeta donde lo deja el instalador oficial. Es donde esta el 99% de las veces y no
;      cuesta nada mirarla.
;   2. Preguntarle al propio `dotnet`, para cuando esta instalado en otro lado —DOTNET_ROOT, o una
;      copia puesta a mano—. Se usa findstr y se mira el estado de salida, que evita tener que
;      buscar dentro de una cadena y con eso un complemento mas.
;
; Se pregunta por la familia y no por una version exacta: .NET avanza al parche mas nuevo por su
; cuenta, asi que cualquier 10.x sirve, y exigir una puntual convertiria cada actualizacion de
; Windows Update en un instalador roto.
Function ComprobarRuntime
  StrCpy $TieneRuntime "0"

  FindFirst $0 $1 "$PROGRAMFILES64\dotnet\shared\Microsoft.WindowsDesktop.App\${RUNTIME_MAYOR}.*"
  FindClose $0

  ${If} $1 != ""
    StrCpy $TieneRuntime "1"
    Return
  ${EndIf}

  nsExec::ExecToStack 'cmd /c dotnet --list-runtimes | findstr /C:"Microsoft.WindowsDesktop.App ${RUNTIME_MAYOR}."'
  Pop $0
  Pop $1

  ${If} $0 == "0"
    StrCpy $TieneRuntime "1"
  ${EndIf}
FunctionEnd

; Si falta, se ofrece la descarga oficial y se corta.
;
; Se abre el navegador en lugar de descargar desde el instalador: el complemento que trae NSIS para
; descargar —NSISdl— no habla HTTPS, y la direccion oficial de Microsoft solo atiende por HTTPS.
; Agregar inetc resolveria eso a cambio de un complemento que hay que instalar en cada maquina que
; arme una version, y para algo que se hace una vez por equipo no vale la pena.
Function ExigirRuntime
  Call ComprobarRuntime

  ${If} $TieneRuntime == "1"
    Return
  ${EndIf}

  MessageBox MB_YESNO|MB_ICONEXCLAMATION \
    "${NOMBRE_VISIBLE} necesita el Escritorio de .NET ${RUNTIME_MAYOR} y este equipo no lo \
tiene.$\r$\n$\r$\nEn la pagina de Microsoft, descarga esto:$\r$\n$\r$\n        .NET Desktop \
Runtime ${RUNTIME_MAYOR}$\r$\n        Windows  ·  x64$\r$\n$\r$\nInstalalo y volve a ejecutar \
este instalador.$\r$\n$\r$\n¿Abrir la pagina de descarga?" \
    IDNO cortar

  ExecShell "open" "${RUNTIME_URL}"

  cortar:
    Abort
FunctionEnd

!endif

Function AbrirSinElevar
  ; ShellExecute desde el Explorador hereda el token del usuario, no el del instalador elevado.
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${EJECUTABLE}"'
FunctionEnd

; Quita la instalacion anterior antes de copiar la nueva.
;
; Hace falta porque `File /r` copia encima y no borra nada de lo que sobra, y las dos variantes del
; paquete no tienen los mismos archivos: la completa trae el runtime entero al lado del ejecutable
; —coreclr.dll, hostfxr.dll, PresentationFramework.dll— y la liviana no trae ninguno, porque usa el
; runtime compartido del sistema.
;
; Instalar la liviana encima de una completa dejaba la carpeta mezclada: el runtimeconfig.json nuevo
; pidiendo el runtime compartido, y al lado el hostfxr.dll viejo de la instalacion anterior. El
; ejecutable encuentra primero el de al lado, no coincide con lo que pide el runtimeconfig, la
; resolucion falla y Windows ofrece descargar .NET —aunque este instalado—. El sintoma no apunta a
; ninguna parte: parece que falta el runtime cuando lo que sobra es el anterior.
;
; Se corre el desinstalador anterior en lugar de borrar la carpeta a mano porque el desinstalador
; sabe exactamente que dejo puesto. Con `/S` no pregunta nada y deja los datos del usuario, que
; viven fuera de la carpeta de programa. El `_?=` es lo que lo hace esperar en lugar de copiarse al
; temporal y volver en el acto; a cambio no se borra solo, y por eso se lo borra a mano despues.
Function QuitarInstalacionAnterior
  ReadRegStr $0 HKLM "Software\${NOMBRE}" "InstallDir"

  ${If} $0 == ""
    Return
  ${EndIf}

  ${IfNot} ${FileExists} "$0\Desinstalar.exe"
    Return
  ${EndIf}

  DetailPrint "Quitando la version anterior..."
  ExecWait '"$0\Desinstalar.exe" /S _?=$0'

  Delete "$0\Desinstalar.exe"
  RMDir "$0"
FunctionEnd

Section "Aplicación" SeccionPrincipal
  SectionIn RO

  ; Se comprueba aca y no en .onInit porque aca $INSTDIR ya es la carpeta que el usuario eligio.
  Call CerrarSiEstaAbierto

!ifdef REQUIERE_RUNTIME
  Call ExigirRuntime
!endif

  ; Antes de copiar, y despues de haber comprobado el runtime: si falta, no tiene sentido haber
  ; desinstalado lo que andaba.
  Call QuitarInstalacionAnterior

  SetOutPath "$INSTDIR"

  ; Recursivo: la publicacion self-contained trae subcarpetas de recursos por idioma.
  File /r "${ORIGEN}\*.*"

  WriteRegStr HKLM "Software\${NOMBRE}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\${NOMBRE}" "Version" "${VERSION}"
  WriteRegStr HKLM "Software\${NOMBRE}" "TipoDeInstalador" "${TIPO_DE_INSTALADOR}"

  ; Entrada en «Aplicaciones instaladas». EstimatedSize va en KiB y lo calcula NSIS sobre lo que
  ; quedo en disco: escribirlo a mano seria un numero que envejece en la primera version.
  WriteRegStr   HKLM "${CLAVE_DESINSTALAR}" "DisplayName"     "${NOMBRE_LARGO}"
  WriteRegStr   HKLM "${CLAVE_DESINSTALAR}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKLM "${CLAVE_DESINSTALAR}" "Publisher"       "${EMPRESA}"
  WriteRegStr   HKLM "${CLAVE_DESINSTALAR}" "DisplayIcon"     "$INSTDIR\${EJECUTABLE}"
  WriteRegStr   HKLM "${CLAVE_DESINSTALAR}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${CLAVE_DESINSTALAR}" "UninstallString" '"$INSTDIR\Desinstalar.exe"'
  WriteRegStr   HKLM "${CLAVE_DESINSTALAR}" "QuietUninstallString" '"$INSTDIR\Desinstalar.exe" /S'
  WriteRegDWORD HKLM "${CLAVE_DESINSTALAR}" "NoModify" 1
  WriteRegDWORD HKLM "${CLAVE_DESINSTALAR}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${CLAVE_DESINSTALAR}" "EstimatedSize" "$0"

  WriteUninstaller "$INSTDIR\Desinstalar.exe"
SectionEnd

Section "Acceso directo en el menú Inicio" SeccionMenu
  ; El acceso directo lleva el nombre visible, separado en palabras: es el texto que se busca
  ; tipeando en el menu Inicio y el que se lee abajo del icono.
  ;
  ; Se borra el del nombre viejo antes de crear el nuevo: sin esto, actualizar desde una version
  ; anterior deja dos accesos directos al mismo programa en el menu.
  Delete "$SMPROGRAMS\${NOMBRE}.lnk"
  CreateShortCut "$SMPROGRAMS\${NOMBRE_VISIBLE}.lnk" "$INSTDIR\${EJECUTABLE}"
SectionEnd

Section /o "Acceso directo en el escritorio" SeccionEscritorio
  Delete "$DESKTOP\${NOMBRE}.lnk"
  CreateShortCut "$DESKTOP\${NOMBRE_VISIBLE}.lnk" "$INSTDIR\${EJECUTABLE}"
SectionEnd

; ---------------------------------------------------------------- desinstalacion

Var BorrarDatos
Var CasillaDatos
Var DialogoDatos

Function un.PaginaDatos
  !insertmacro MUI_HEADER_TEXT "Datos del usuario" \
    "Qué hacer con las conexiones guardadas."

  nsDialogs::Create 1018
  Pop $DialogoDatos

  ${If} $DialogoDatos == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 0 100% 48u \
    "Las conexiones y la configuración quedan en tu perfil, y las contraseñas en el \
Administrador de credenciales de Windows. Por omisión no se tocan: así podés reinstalar sin \
perder nada.$\r$\n$\r$\nMarcá la casilla sólo si querés borrarlas para siempre."

  ${NSD_CreateCheckbox} 0 56u 100% 12u "Borrar también mis conexiones y mi configuración"
  Pop $CasillaDatos

  nsDialogs::Show
FunctionEnd

Function un.LeerPaginaDatos
  ${NSD_GetState} $CasillaDatos $BorrarDatos
FunctionEnd

Function un.onInit
  SetRegView 64
  StrCpy $BorrarDatos 0
FunctionEnd

Section "Uninstall"
  Call un.CerrarSiEstaAbierto

  ; Borrado recursivo de la carpeta de programa. El comentario que habia aca decia lo contrario
  ; —que se borraba archivo por archivo para no llevarse lo ajeno— y no era lo que hacia el codigo:
  ; conviene que diga la verdad, porque de eso depende que se pueda reusar desde el instalador.
  ;
  ; Recursivo y no por lista: las dos variantes del paquete traen archivos distintos —la completa,
  ; el runtime entero— y una lista fija dejaria afuera justo los que sobran al cambiar de variante.
  ; El riesgo de llevarse algo ajeno es real solo si alguien elige una carpeta que ya tenia cosas,
  ; y para eso la carpeta por omision es una propia dentro de Archivos de programa.
  ;
  ; Los datos del usuario no estan aca: viven en $LOCALAPPDATA y solo se tocan mas abajo, si lo
  ; pidio expresamente.
  Delete "$INSTDIR\Desinstalar.exe"
  RMDir /r "$INSTDIR"

  ; Los dos nombres: el visible de ahora y el de las versiones anteriores, para no dejar un
  ; acceso directo huerfano apuntando a un ejecutable que ya no esta.
  Delete "$SMPROGRAMS\${NOMBRE_VISIBLE}.lnk"
  Delete "$SMPROGRAMS\${NOMBRE}.lnk"
  Delete "$DESKTOP\${NOMBRE_VISIBLE}.lnk"
  Delete "$DESKTOP\${NOMBRE}.lnk"

  DeleteRegKey HKLM "${CLAVE_DESINSTALAR}"
  DeleteRegKey HKLM "Software\${NOMBRE}"

  ${If} $BorrarDatos == ${BST_CHECKED}
    ; Sólo la carpeta de datos. Las credenciales del Administrador de Windows no se tocan ni
    ; siquiera acá: son del usuario, no de esta aplicación, y borrarlas desde un desinstalador
    ; es meterse en un almacén compartido con todo lo demás que tenga guardado.
    RMDir /r "$LOCALAPPDATA\${NOMBRE}"
  ${EndIf}
SectionEnd
