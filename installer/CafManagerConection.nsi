; Instalador de CafManagerConection.
;
; Empaqueta lo que ya produce `task publish`: la carpeta self-contained para win-x64. No compila
; nada ni sabe de .NET; si la carpeta no esta, falla y lo dice.

Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

; ---------------------------------------------------------------- identidad

; NOMBRE va al registro y a la clave de desinstalacion: cambiarlo deja huerfanas las
; instalaciones existentes y el equipo termina con dos copias.
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

; src/CafManagerConection.App/Services/SelectorDeInstalador.cs lo lee, sin elevacion.
!ifdef REQUIERE_RUNTIME
  !define TIPO_DE_INSTALADOR "liviano"
!else
  !define TIPO_DE_INSTALADOR "completo"
!endif

!define RUNTIME_MAYOR "10.0"
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

; Dos caminos: la carpeta donde lo deja el instalador oficial, que es donde esta el 99% de las
; veces, y preguntarle al propio `dotnet` para cuando esta en otro lado —DOTNET_ROOT o una copia
; puesta a mano—.
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

; Hace falta porque `File /r` copia encima y no borra lo que sobra, y las dos variantes no tienen
; los mismos archivos: la completa trae el runtime al lado del ejecutable —coreclr.dll,
; hostfxr.dll, PresentationFramework.dll— y la liviana usa el compartido del sistema.
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
    "Las conexiones, la configuración y las contraseñas cifradas quedan en tu perfil. Por \
omisión no se tocan: así podés reinstalar sin perder nada.$\r$\n$\r$\nMarcá la casilla sólo \
si querés borrarlas para siempre."

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
    RMDir /r "$LOCALAPPDATA\${NOMBRE}"
  ${EndIf}
SectionEnd
