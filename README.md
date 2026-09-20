# Tipos de barra Perú — add-in Revit 2027

Crea en el proyecto abierto los **tipos de barra de armadura** (`RebarBarType`) con los
diámetros del catálogo peruano (ASTM A615 Grado 60) y los **diámetros de doblado y
ganchos de la norma E.060** (Concreto Armado, artículos 7.1 y 7.2, que sigue ACI 318).

Al lanzarlo lee los tipos que ya existen y abre una ventana con el catálogo: una fila
por tamaño con casilla "crear", nombre resultante, diámetro, área, peso, diámetros de
doblado y estado ("ya existe" / "se creará"). Es **idempotente**: por defecto solo se
crean los que faltan; con la casilla "actualizar los que ya existen" se reescriben los
valores de los que ya tienen ese nombre. Al terminar muestra un resumen de lo creado,
lo actualizado y lo omitido.

Los nombres de los tipos son los que usa el add-in
[RetainingWallRebar](https://github.com/Andy-rba30/Acero-automatico) en su
`config.json` (`barTypeName`). Ver [Formato de nombre](#formato-de-nombre).

## Qué escribe en cada tipo

| Propiedad de `RebarBarType` | Valor |
|---|---|
| `Name` | prefijo + nombre del catálogo (`Ø3/8"`, `Ø12mm`, ...) |
| `BarNominalDiameter`, `BarModelDiameter` | diámetro nominal del catálogo |
| `StandardBendDiameter` | diámetro interior de doblado de barras principales (tabla 7.2) |
| `StandardHookBendDiameter` | diámetro de doblado de los ganchos estándar (misma tabla 7.2) |
| `StirrupTieBendDiameter` | diámetro de doblado de estribos (7.2.2) |
| `MaximumBendRadius` | `radioMaximoDobladoMm` del config (10 000 mm por defecto) |
| `DeformationType` | `Deformed` si `corrugada` es true, `Plain` si es false |
| `BarMassPerUnitLength` | peso unitario kg/m del catálogo (propiedad nativa desde Revit 2027) |
| Longitudes de gancho | `SetAutoCalcHookLengths(true)` por defecto; opción E.060, ver más abajo |
| Permiso de ganchos | todos los tipos de gancho del proyecto quedan permitidos |

Todos los diámetros se asignan a la vez con `SetBarTypeDiameters(BarTypeDiameterOptions)`,
que es como la API los valida (nominal menor que los de doblado). Las unidades se
convierten desde mm con `UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters)` y
el peso con `UnitTypeId.KilogramsPerMeter`.

Cada tipo se crea dentro de su propia `SubTransaction` en una única `Transaction`: si uno
falla (nombre rechazado, valor fuera de rango) se deshace solo ese y aparece en el
apartado "Errores" del resumen; los demás se crean igual.

## Formato de nombre

```
nombre del tipo = prefijo + nombre del catálogo
```

El prefijo por defecto es `Ø` (`prefijoNombre` en config.json, editable en la ventana).
Con el catálogo por defecto salen exactamente estos nueve nombres, que son los que hay
que escribir en `barTypeName` del otro add-in:

| Nombre | Ø nominal (mm) | Área (cm²) | Peso (kg/m) |
|---|---|---|---|
| `Ø6mm` | 6.0 | 0.28 | 0.222 |
| `Ø8mm` | 8.0 | 0.50 | 0.395 |
| `Ø3/8"` | 9.5 | 0.71 | 0.560 |
| `Ø12mm` | 12.0 | 1.13 | 0.888 |
| `Ø1/2"` | 12.7 | 1.29 | 0.994 |
| `Ø5/8"` | 15.9 | 2.00 | 1.552 |
| `Ø3/4"` | 19.1 | 2.84 | 2.235 |
| `Ø1"` | 25.4 | 5.10 | 3.973 |
| `Ø1 3/8"` | 35.8 | 10.06 | 7.907 |

La comparación con los tipos existentes es por nombre exacto sin distinguir mayúsculas.
Si ya hay un tipo con ese nombre pero otro diámetro, la columna "estado" lo avisa
("ya existe (con diámetro 10 mm)"). El nombre se valida con `NamingUtils.IsValidName`
antes de crear; si Revit no admitiera la comilla de pulgada, `simboloPulgada` en
config.json permite escribirla de otra forma (por ejemplo `in` → `Ø3/8in`).

## Reglas E.060 aplicadas

Las reglas se aplican **por umbral de diámetro**, no por nombre, así 6, 8 y 12 mm caen
solos en su tramo. Todo está parametrizado en `reglasE060` de config.json.

### Diámetros mínimos de doblado (art. 7.2)

| Barras | Regla | Config |
|---|---|---|
| Barras principales y sus ganchos, hasta 1" (25.4 mm), incluidos 6, 8 y 12 mm | 6 db | `dobladoBarras` |
| Barras principales 1 1/8" a 1 3/8" (hasta 35.8 mm) | 8 db | `dobladoBarras` |
| Barras principales mayores | 10 db | `dobladoBarras` |
| Estribos hasta 5/8" (15.9 mm), incluidos 6, 8 y 12 mm | 4 db | `dobladoEstribos` |
| Estribos de 3/4" y mayores | 6 db | `dobladoEstribos` |

Resultado con el catálogo por defecto (mm):

| Tamaño | Ø | Doblado barra y gancho | Doblado estribo |
|---|---|---|---|
| 6mm | 6.0 | 36.0 | 24.0 |
| 8mm | 8.0 | 48.0 | 32.0 |
| 3/8" | 9.5 | 57.0 | 38.0 |
| 12mm | 12.0 | 72.0 | 48.0 |
| 1/2" | 12.7 | 76.2 | 50.8 |
| 5/8" | 15.9 | 95.4 | 63.6 |
| 3/4" | 19.1 | 114.6 | 114.6 |
| 1" | 25.4 | 152.4 | 152.4 |
| 1 3/8" | 35.8 | 286.4 | 214.8 |

### Ganchos estándar (art. 7.1)

| Gancho | Extensión recta tras el doblez | Config |
|---|---|---|
| 180° en barras principales (7.1.1) | 4 db, mínimo 65 mm | `ganchos.estandar180` |
| 90° en barras principales (7.1.2) | 12 db | `ganchos.estandar90` |
| Estribos a 90°, hasta 5/8" (7.1.3 a) | 6 db | `ganchos.estribo90` |
| Estribos a 90°, 3/4" y 1" (7.1.3 b) | 12 db | `ganchos.estribo90` |
| Estribos a 135° (7.1.3 c) | 6 db | `ganchos.estribo135` |

Con `"estribo135": { "multiplicador": 6, "minimoMm": 75 }` se obtiene el gancho sísmico
del art. 21.1 (6 db, no menor de 75 mm).

### Cómo se aplican las longitudes de gancho

Por defecto las longitudes de gancho se dejan en **cálculo automático de Revit**
(`SetAutoCalcHookLengths(true)`), que usa el multiplicador de cada tipo de gancho.

Con la casilla **"Longitudes de gancho según E.060"** (o `longitudesGanchoSegunE060: true`
en config.json) el add-in recorre los `RebarHookType` del proyecto y clasifica cada uno
por su estilo y ángulo:

| Estilo del gancho | Ángulo | Regla |
|---|---|---|
| Estándar | 180° | `estandar180` |
| Estándar | 90° | `estandar90` |
| Estribo/amarre | 90° | `estribo90` |
| Estribo/amarre | 135° o 180° | `estribo135` |
| cualquier otro | | se deja en cálculo automático y se avisa |

Revit no documenta si su "longitud de gancho" incluye el tramo curvo, así que la
extensión se fija por **calibración**: se lee la longitud automática y la extensión
recta que Revit deduce de ella (`RebarHookType.GetHookExtensionLength`), se aplica esa
diferencia a la extensión E.060, se asigna con `SetHookLength` y se vuelve a leer la
extensión para comprobar que coincide (tolerancia 0.1 mm). Si no coincide, el resumen
lo avisa para revisarlo en Revit. La ventana muestra, para cada gancho del proyecto, la
regla que se le aplicará.

## Otros diámetros

Debajo de la tabla, el grupo **"Otro diámetro"** permite crear tipos que no están en el
catálogo peruano (por ejemplo `10mm`, `1 1/4"`, `#6`). Se escribe el nombre y el diámetro
nominal en mm; área y peso se rellenan solos y se pueden corregir a mano si el catálogo
del fabricante da otro valor:

```
área (cm²)  = π · d² / 4          con d en mm, dividido entre 100
peso (kg/m) = área (cm²) × 0,785   es decir área × 7,85 kg/dm³ pasado a kg/m
```

Con **"Añadir a la lista"** el diámetro pasa a ser una fila más de la tabla, con su
casilla "crear", su nombre con el prefijo y el mismo estado que las demás, y se guarda en
`catalogoExtra` de config.json para que aparezca la próxima vez. El botón **"Quitar"** de
la fila lo borra de la lista. El catálogo peruano original no se toca.

Todos los parámetros de norma del diámetro nuevo salen de las mismas reglas por umbral,
sin escribir nada más:

| Parámetro | Regla por diámetro nominal d | Config |
|---|---|---|
| Doblado de barra y de gancho estándar | 6 db hasta 25.4 mm, 8 db hasta 35.8 mm, 10 db por encima | `dobladoBarras` |
| Doblado de estribo | 4 db hasta 15.9 mm, 6 db por encima | `dobladoEstribos` |
| Gancho 180° | 4 db, mínimo 65 mm | `ganchos.estandar180` |
| Gancho 90° | 12 db | `ganchos.estandar90` |
| Estribo 90° | 6 db hasta 15.9 mm, 12 db por encima | `ganchos.estribo90` |
| Estribo 135° | 6 db | `ganchos.estribo135` |
| Rango cubierto por la norma | 6 mm a 57 mm (2 1/4", barra #18) | `diametroMinimoNormaMm`, `diametroMaximoNormaMm` |

Un diámetro fuera de ese rango se rechaza al añadirlo, y si ya estaba en `catalogoExtra`
la fila muestra "no se crea (menor de 6 mm, fuera de la norma)" y queda sin casilla. Un
extra cuyo nombre repita uno del catálogo se descarta al cargar, y dos filas que produzcan
el mismo nombre final se marcan "nombre repetido en la tabla". Al añadir se comprueba
además que el nombre con prefijo sea válido para Revit y que no exista ya en el proyecto.

Ejemplo, `10mm` con el prefijo por defecto:

| | Valor |
|---|---|
| Nombre del tipo | `Ø10mm` |
| Área / peso calculados | 0.79 cm² / 0.617 kg/m |
| Doblado de barra y gancho | 6 db = 60 mm |
| Doblado de estribo | 4 db = 40 mm |
| Gancho 180° / 90° | 65 mm (mínimo) / 120 mm |
| Estribo 90° / 135° | 60 mm / 60 mm |

Los valores quedan entre los de `Ø3/8"` (9.5 mm) y `Ø12mm`, que tienen los mismos
multiplicadores. La consola de pruebas comprueba esto para 10 mm, 1 1/4" (31.8 mm,
8 db de doblado como `Ø1 3/8"`), #6 (19.05 mm, como `Ø3/4"`), y rechaza 5 mm y 60 mm.

## Instalación

Carpeta de add-ins de Revit 2027: `%AppData%\Autodesk\Revit\Addins\2027\`

```
%AppData%\Autodesk\Revit\Addins\2027\
├── TiposBarraPeru.addin
└── TiposBarraPeru\
    ├── TiposBarraPeru.dll
    ├── TiposBarraPeru.Reglas.dll
    └── config.json
```

1. Compila (ver abajo) o copia los archivos de `TiposBarraPeru\bin\Release\net10.0-windows\`.
2. Copia `TiposBarraPeru.addin` a la carpeta de add-ins y las dos DLL más `config.json` a la
   subcarpeta `TiposBarraPeru`.
3. Arranca Revit 2027. Aparece la pestaña **Perú**, panel **Armadura**, botón
   **Tipos de barra Perú**. El mismo comando está también en
   Complementos ▸ Herramientas externas.

Compilando en **Debug** en Windows, el proyecto copia solo estos archivos a la carpeta de
add-ins (target `CopyToRevit` del csproj).

### Compilación

Requisitos: SDK de .NET 10. Los paquetes `Nice3point.Revit.Api.RevitAPI` y
`Nice3point.Revit.Api.RevitAPIUI` 2027.* se descargan de NuGet; no hace falta tener Revit
instalado para compilar.

```
dotnet build -c Release -p:EnableWindowsTargeting=true
```

`EnableWindowsTargeting` solo hace falta fuera de Windows (Linux/macOS); en Windows basta
`dotnet build -c Release` o abrir `TiposBarraPeru.sln` en Visual Studio.

Proyectos de la solución:

| Proyecto | Marco | Contenido |
|---|---|---|
| `TiposBarraPeru.Reglas` | net10.0 | Catálogo, config.json, nombres, reglas E.060, planificador. Sin Revit ni WPF. |
| `TiposBarraPeru` | net10.0-windows | Add-in: cinta, comando, ventana WPF construida en código, creación de tipos. |
| `TiposBarraPeru.Pruebas` | net10.0 (consola) | Comprobaciones de las reglas puras, ejecutables sin Revit. |

## Cómo editar el catálogo

`config.json` está junto a la DLL. Se lee con System.Text.Json (camelCase; admite
comentarios `//` y comas finales). Si falta o está vacío se usan los valores por defecto,
que son exactamente los del archivo distribuido.

```jsonc
{
  "prefijoNombre": "Ø",             // prefijo del nombre (editable en la ventana)
  "simboloPulgada": "\"",           // cómo se escribe la pulgada en el nombre
  "actualizarExistentes": false,    // valor inicial de la casilla
  "longitudesGanchoSegunE060": false,
  "radioMaximoDobladoMm": 10000,
  "toleranciaDiametroMm": 0.05,     // para avisar de existentes con otro diámetro
  "catalogo": [
    { "nombre": "6mm",   "diametroMm": 6.0, "areaCm2": 0.28, "pesoKgM": 0.222, "corrugada": true },
    { "nombre": "3/8\"", "diametroMm": 9.5, "areaCm2": 0.71, "pesoKgM": 0.560, "corrugada": true }
    // ...
  ],
  "reglasE060": { /* tablas de multiplicadores, ver arriba */ }
}
```

- Los diámetros añadidos desde la ventana viven en `catalogoExtra`, con el mismo formato;
  también se pueden editar a mano. Un extra que repita un nombre del catálogo se ignora.
- Para **añadir o quitar tamaños** del catálogo base edita `catalogo`. `nombre` es lo que va tras el
  prefijo; `diametroMm` decide los multiplicadores E.060; `areaCm2` es solo informativa
  (tabla y README); `pesoKgM` se escribe en el tipo; `corrugada: false` crea la barra como
  lisa (Plain).
- Para **cambiar las reglas** edita los tramos: cada tabla es una lista de
  `{ "hastaDiametroMm": ..., "multiplicador": ... }` que se evalúa en orden (se ordena por
  diámetro al cargar) y el último tramo cubre todo lo que quede por encima. Los ganchos
  llevan además `minimoMm` (0 = sin mínimo).
- El botón **"Guardar opciones en config.json"** de la ventana escribe el prefijo y las
  dos casillas como nuevos valores por defecto. "Añadir a la lista" y "Quitar" también
  guardan el archivo. Al guardar se pierden los comentarios del archivo.

## Comprobaciones sin Revit

```
dotnet run --project TiposBarraPeru.Pruebas -c Release
```

Verifica, sobre la DLL de reglas que usa el add-in: el catálogo por defecto, la lectura
de `config.json` (el distribuido, uno con comentarios y comas finales, archivo
inexistente, JSON vacío e inválido, ida y vuelta), los nueve nombres exactos y las
variantes de prefijo y símbolo de pulgada, los diámetros de doblado de barra, gancho y
estribo de cada tamaño, los límites de los tramos, las extensiones de gancho con sus
mínimos, la clasificación de ganchos del proyecto, el planificador (idempotencia,
actualización, tipos con otro diámetro, nombres no válidos) y los otros diámetros
(fórmulas de área y peso contrastadas con el catálogo, rango de la norma, parámetros
E.060 de 10 mm, 1 1/4" y #6 frente a los tamaños del catálogo que los rodean, rechazo de
5 mm y 60 mm, lectura y limpieza de `catalogoExtra`, nombres repetidos y validación de
una barra nueva). Resultado actual: **295 comprobaciones superadas, 0 fallidas**.

## Limitaciones y lo no probado

- **No se ha ejecutado dentro de Revit.** Se ha compilado contra la API 2027 (paquetes
  Nice3point 2027.2.0, nombres de miembros verificados en su documentación) y se han
  probado las reglas puras, pero queda por comprobar en Revit: la creación real de los
  tipos y sus validaciones de rango, el comportamiento de `GetHookExtensionLength` en la
  calibración de ganchos, que Revit admita la comilla `"` en el nombre (si no, usar
  `simboloPulgada`), el icono de la cinta y la ventana WPF, incluido el grupo "Otro
  diámetro" y el botón "Quitar" (su lógica de validación y planificación sí está cubierta
  por la consola).
- El área (cm²) solo se muestra en la ventana; no se escribe en el tipo. El peso sí,
  mediante la propiedad nativa `BarMassPerUnitLength` de Revit 2027. Si hiciera falta el
  área en tablas de planificación, el siguiente paso sería un parámetro compartido de
  tipo vinculado a la categoría Armadura estructural.
- Los ganchos se clasifican por estilo y ángulo con ±1°. Ángulos distintos de 90°, 135° y
  180° se dejan en cálculo automático.
- Las barras mayores de 1" solo tienen regla E.060 de doblado (8 db / 10 db); para sus
  estribos a 90° se aplica el último tramo de `estribo90` (12 db).
- La ventana no crea tipos de gancho (`RebarHookType`); usa los que ya tenga la plantilla.
- Solo proyectos, no familias.
