## ADDED Requirements

### Requirement: Escala del escritorio remoto

El sistema SHALL permitir fijar, por conexión, la escala con la que el servidor dibuja su escritorio,
para que en pantallas de alta densidad el texto y los controles del servidor no queden diminutos. La
escala SHALL ofrecerse como una lista fija de los valores que admite el protocolo (100, 125, 150,
175, 200), sin entrada libre, y SHALL viajar al control como `DesktopScaleFactor` a través de
`IMsRdpExtendedSettings`.

El acceso a esa interfaz SHALL pasar por un ámbito nuevo, `AmbitoDeRdp.Extendida`, resuelto en
`Aplicar()` como los ámbitos Avanzados y Asegurados, y SHALL conservar el diagnóstico existente: si
la interfaz no está en la versión instalada, el ajuste cae en `PropiedadesNoAceptadas` y no impide
conectar.

El ajuste SHALL ser heredable por carpeta con la semántica de tres estados de los demás ajustes RDP.

#### Scenario: El usuario fija la escala al 150 %

- **WHEN** una conexión fija la escala del escritorio en 150 %
- **THEN** `PlanDeSesionRdp` incluye un ajuste `DesktopScaleFactor` con valor 150 en el ámbito
  Extendida

#### Scenario: La versión del control no expone la interfaz extendida

- **WHEN** el control instalado no expone `IMsRdpExtendedSettings`
- **THEN** la conexión se establece igual y `DesktopScaleFactor` queda en `PropiedadesNoAceptadas`

#### Scenario: La conexión no fija escala

- **WHEN** ni la conexión ni sus ancestros fijan escala
- **THEN** `PlanDeSesionRdp` no incluye `DesktopScaleFactor` y el servidor dibuja a escala 100 %

#### Scenario: El editor sólo ofrece los valores de la lista fija

- **WHEN** el usuario elige la escala del escritorio en el editor
- **THEN** sólo puede elegir entre 100, 125, 150, 175 o 200, sin poder escribir un valor libre

### Requirement: Escala del dispositivo

El sistema SHALL permitir fijar la escala del dispositivo (`DeviceScaleFactor`, valores 100, 140 o
180) junto con la del escritorio, por la misma interfaz extendida, para las aplicaciones del servidor
que respetan el DPI del dispositivo.

#### Scenario: Se fija escala de dispositivo válida

- **WHEN** una conexión fija la escala de dispositivo en 140
- **THEN** `PlanDeSesionRdp` incluye `DeviceScaleFactor` con valor 140 en el ámbito Extendida

#### Scenario: Se pide un valor de escala de dispositivo fuera de rango

- **WHEN** se intenta fijar una escala de dispositivo distinta de 100, 140 o 180
- **THEN** la validación de la conexión SHALL rechazar el valor antes de intentar aplicarlo
