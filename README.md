# eDelphyn · Gestión de hemograma

Aplicación de escritorio Windows Forms en C# / .NET Framework 4.8. Ejecutable: `dist/eDelphyn.Hemograma.exe`.

## Instalación y actualización · versión 1.6.1

Descargue el instalador desde [GitHub Releases](https://github.com/floresroldanluis/MBeDelphynHemograma/releases/latest). Incluye .NET Framework 4.8 sin necesidad de descargarlo al instalar. Si falta este componente, solicita permisos de administrador y puede requerir reiniciar Windows. La aplicación se instala para el usuario actual en Windows 10/11.

El asistente solicita servidor, base, usuario y contraseña SQL, con contraseña oculta y botón **Probar conexión**. Usuario sugerido: eDelphyn. Ingrese la contraseña durante la instalación; no está incorporada en el repositorio ni en el instalador. En otro equipo indique el servidor real, no localhost. La contraseña queda cifrada con Windows DPAPI para el usuario actual en connection.ini. No copie ese archivo a otra cuenta o máquina; configure nuevamente la conexión. Una actualización permite conservar la credencial guardada dejando la contraseña vacía.

El encabezado es **Resultados Inmunohematológicos de Donantes**. Tema claro/oscuro, fechas y checks adaptados al tema; popups modales con esquinas redondeadas. Acerca de muestra logo, versión, resumen y enlace https://diagnosticoual.com/, sin QR.

**Configuración → Actualizaciones** consulta GitHub Releases y muestra la versión actual y la disponible. Si está actualizado indica «Ya tienes la versión más reciente». Para una versión nueva permite descargar, verificar e iniciar el instalador y volver a abrir el programa al pulsar Finalizar, si conserva marcada la casilla Abrir eDelphyn Hemograma. Se verifica tamaño, digest cuando GitHub lo proporciona, firma, editor, producto y versión. No se envían datos de donaciones a GitHub. Cerrar el panel cancela una descarga.

La firma actual es local, no de una autoridad pública. Otros equipos requieren que TI gestione la confianza o una firma corporativa/pública; no garantiza la aceptación por Sophos. El instalador no modifica antivirus ni instala certificados.

Para compilar: build.ps1. Para generar el instalador: build-installer.ps1 (Inno Setup 6 y certificado de firma configurado en signing/thumbprint.txt). El script descarga y verifica el redistribuible oficial si falta. La clave privada nunca debe subirse. Para publicar use publish-release.ps1 desde un checkout Git autenticado después de incrementar las versiones en AssemblyInfo.cs, setup.iss y build-installer.ps1 y ejecutar las verificaciones. El instalador se adjunta a Releases, no al historial Git.

## Uso

1. Abra el ejecutable tras configurar y probar la conexión SQL en el instalador.
2. **Fecha donación** inicia con la fecha actual del equipo en Desde y Hasta, sin casillas. Al introducir **Donación número** en cualquiera de los extremos, ambas fechas quedan vacías, incluidas las seleccionadas manualmente. La búsqueda por número no impone ninguna fecha. Al seleccionar una fecha se vacían automáticamente ambos números. Una fecha vacía no impone límite; Supr o Retroceso vacía el campo enfocado. Puede elegir una fecha en el calendario (incluso hoy) o pulsar Enter sobre un campo vacío para activar la fecha que contiene internamente. Hasta incluye el día completo. Se admite uno o ambos extremos de cada filtro. Los códigos se comparan como texto según la intercalación de SQL Server.
3. Pulse **Buscar**. Cada donación aparece seleccionada por defecto. `***` indica una prueba inexistente y muestra el tooltip «No tiene Prueba»; una celda vacía indica un registro existente sin resultado y muestra «Sin Resultado». Se muestran los valores de DES_RESULT.
   Cada búsqueda se presenta por **Número donación ascendente**. Pulse cualquier encabezado para alternar ascendente/descendente; la flecha indica el sentido. Las fechas se ordenan cronológicamente y los resultados numéricos por valor, admitiendo punto o coma decimal. Resultados de texto, registros sin resultado y pruebas inexistentes se sitúan después de los números. Ordenar conserva las donaciones marcadas.
4. Use las casillas **Seleccionar Todo** y **Retirar Selección Todo** para marcar o desmarcar todas las donaciones; también puede cambiar cada fila. Debajo de la tabla hay nueve casillas de pruebas. Al iniciar, todas están marcadas salvo **Hb**. Puede modificar cualquier prueba; la selección de pruebas se conserva entre búsquedas. Eliminar se habilita cuando hay al menos una donación y una prueba marcadas. Si no existen registros de esas pruebas, al pulsar se muestra un aviso y no se ejecuta ningún borrado.
5. Pulse **Eliminar**. El diálogo muestra donaciones, cantidad de registros y únicamente las pruebas marcadas. **No** cancela; **Sí** ejecuta la eliminación y actualiza la lista. Las pruebas desmarcadas se conservan, incluida Hb si no se selecciona expresamente.

La búsqueda exige al menos un límite y rechaza más de 1000 donaciones: reduzca el rango. Modificar filtros invalida los resultados anteriores.

## Alcance exacto

Se ejecuta DELETE sobre `dbo.DONATION_TEST` para los ID_DONATION seleccionados y solo los ID_TEST marcados, restringidos a este catálogo:

| ID_TEST | Prueba |
|---|---|
| 72 | Rcto. PQ |
| 73 | Rcto. Leu |
| 74 | Neutrófilos |
| 75 | Linfocitos |
| 76 | Monocitos |
| 77 | Eosinófilos |
| 78 | Basófilos |
| 92 | Hto |
| 93 | Hb |

Elimina registros completos, incluidos resultados existentes y validados; no vacía simplemente DES_RESULT. No elimina donaciones ni definiciones del catálogo TEST. El trigger existente `T_DONATION_TEST_MODIFIED` actualiza `DONATION.DAT_MODIFIED` para las donaciones afectadas. Mapeo, clave primaria y trigger comprobados mediante lectura del SQL Server local el 29/10/2026.

## Conexión

La configuración de desarrollo `dist/eDelphyn.Hemograma.exe.config` usa `localhost`, base `eDelphyn` y autenticación integrada de Windows. No contiene contraseñas. Puede cambiar la cadena de conexión con la aplicación cerrada. La cuenta técnica SQL necesita SELECT sobre DONATION, DONATION_TEST, USR, PWD y USERPROFILE; INSERT y DELETE sobre DONATION_TEST y los permisos requeridos por el trigger existente.

## Transacción y respaldo

Antes del DELETE se vuelven a leer todos los campos de las pruebas seleccionadas bajo una transacción SERIALIZABLE con UPDLOCK/HOLDLOCK. Si hay cualquier diferencia respecto de la búsqueda (incluidas altas o bajas de pruebas), se cancela y se exige buscar nuevamente. Consultas parametrizadas; lista de ID_TEST fija.

Los registros completos y los identificadores de las donaciones se guardan como XML con esquema en `%LOCALAPPDATA%\eDelphyn.Hemograma\Respaldos`. El archivo se sincroniza y se relee para comprobarlo antes del DELETE. Si falla el respaldo o la operación SQL, la transacción no se confirma. No se guarda una contraseña. Los respaldos contienen resultados de laboratorio y heredan los permisos de la carpeta del usuario Windows.

Un XML refleja el estado **previo al intento**, y puede existir aunque el intento se haya cancelado. No es prueba de una eliminación confirmada. Ver respaldos muestra una tabla de donación, fecha, prueba, resultado, validación y estado actual. Seleccione un respaldo, marque las pruebas ausentes y pulse Restaurar seleccionadas. Tras confirmar, recupera todos sus campos dentro de una transacción; no sobrescribe registros existentes. Rechaza otro servidor/base, donaciones que no coinciden y esquemas incompatibles. Si una prueba ya existe o falla cualquier inserción/verificación, se cancela todo. El XML original se conserva. Sin conexión se permite consultar el archivo, pero no restaurarlo. No incluye una copia del valor anterior de DAT_MODIFIED. Si se interrumpe la conexión durante COMMIT, vuelva a consultar para determinar el resultado; no repita el borrado a ciegas.

## Compilar y verificar

Ejecute `powershell -ExecutionPolicy Bypass -File .\build.ps1`. Usa el compilador incluido con .NET Framework, sin paquetes externos.

Ejecute `dist\eDelphyn.Hemograma.exe --self-test` y espere al proceso. El informe queda en `dist\test-results\results.txt`. La prueba consulta el rango real C260730–C260740 únicamente en lectura; todas las inserciones, modificaciones y eliminaciones se hacen en tablas temporales locales de sesión (`#DONATION`, `#DONATION_TEST`). Cubre filtros, selección, concurrencia, respaldo, preservación de otras pruebas/donaciones y rollback SQL.

## Validación y distribución

Las verificaciones en tests cubren filtros, ordenamiento, versiones y firma del instalador. La prueba SQL integrada utiliza tablas temporales para las operaciones de escritura. No se modifican donaciones reales durante las pruebas.

La firma local usa la huella pública configurada en Deployment.cs. La clave privada permanece en el almacén de Windows. Para cambiar de editor debe planificarse la migración del verificador de actualizaciones. No hay sello de tiempo: renueve y vuelva a firmar antes de caducar el certificado.

## Instalador privado
La variante -Privado precarga una contraseña oculta con asteriscos. Se genera con build-installer.ps1 -PrivateBuild y un archivo local installer/private-defaults.iss, excluido de Git. Nunca publique esa variante ni su archivo de configuración. La versión pública mantiene la contraseña vacía. Siguiente prueba la conexión y avanza directamente a Listo para instalar; ante un error permanece en la página SQL.


## Inicio de sesión y soporte · 1.6.1

Al abrir se solicita usuario y contraseña de eDelphyn mediante un formulario propio con botón Acceder. La conexión técnica SQL del instalador es independiente de esta cuenta. La validación consulta USR/PWD/USERPROFILE, exige cuenta activa y no bloqueada y respeta DAT_PASSWORD y NUM_EXPIRY. No crea cuentas ni altera contraseñas o contadores de intentos de eDelphyn. El formulario aplica una espera local tras fallos; no sustituye los controles del servidor.

Compatibilidad comprobada mediante inspección del código instalado de eDelphyn: usuario en mayúsculas y SHA-1 hexadecimal en mayúsculas; codificación Windows-1252 como la JVM local. Esto conserva el formato heredado, no introduce un almacenamiento nuevo de contraseñas. Para una instalación con otra codificación, configure appSettings/eDelphynAuthEncoding en el archivo .config. La contraseña del usuario no se guarda ni registra. La autenticación se prueba con cuentas sintéticas en tablas temporales; el ingreso con una cuenta real debe validarse por su titular.

El subtítulo es «Visualización y eliminación de pruebas de hemograma del eDelphyn». Las actualizaciones están únicamente en Configuración. Acerca de y el pie principal incluyen los datos de soporte proporcionados por Diagnóstico UAL; Acerca de se cierra con X o Escape.

Las pruebas de restauración y autenticación escriben exclusivamente en tablas temporales. Los XML de prueba y las capturas locales no se publican. Los nuevos respaldos de eliminación incluyen la identidad eDelphyn autenticada.

## Cierre de popups · 1.6.1

Acerca de y Configuración usan la ventana principal como propietaria modal. La capa oscura no toma el foco y se oculta al cerrar, sin deshabilitar y habilitar de nuevo todos los controles. La prueba verify-popup-stress.ps1 abre/cierra ambos popups repetidamente con 1000 filas, comprueba la limpieza, la habilitación nativa y que el bucle de mensajes siga respondiendo.

## Navegación y distribución visual · 1.6.1

El login comienza en Usuario. Enter en Usuario pasa a Contraseña; Enter en Contraseña ejecuta Acceder. Campos vacíos se validan localmente, conservando el foco en el campo faltante, sin consultas SQL ni penalización de intentos. Los iconos del encabezado se centran por los límites reales del dibujo. El contador de selección aparece encima de Eliminar y Ver respaldos.

## Pruebas configurables (1.6.1)

Edite `eDelphyn.Hemograma.exe.config` en la carpeta instalada y reinicie el programa. En `appSettings`, la clave `Hemograma.Pruebas` contiene la lista en orden:

```xml
<add key="Hemograma.Pruebas" value="Rcto. PQ|true;Rcto. Leu|true;Neutrófilos|true;Linfocitos|true;Monocitos|true;Eosinófilos|true;Basófilos|true;Hto|true;Hb|false" />
```

Cada elemento es `Nombre de prueba|true/false`; `true` marca la casilla al abrir y `false` la deja desmarcada. Separe elementos con `;`. Para agregar o reemplazar una prueba use su nombre abreviado (`SRT_TEST`), nombre completo (`DES_TEST`) o código (`COD_TEST`) de `dbo.TEST`. El programa obtiene el ID automáticamente, ignorando tildes y mayúsculas. Si no existe o coincide con varios IDs, se rechaza la configuración. Máximo 32 pruebas, sin duplicados; use entidades XML para caracteres como `&amp;`. Los cambios se aplican a columnas, casillas, búsqueda y eliminación. Solo se eliminan las pruebas y donaciones marcadas, previa confirmación y respaldo.

La actualización agrega la clave a instalaciones antiguas sin reemplazar las personalizaciones existentes. Para restaurar un respaldo de una prueba que haya retirado de la configuración, vuelva a incluir su nombre y reinicie antes de restaurar.

## Credenciales SQL

`connection.ini` guarda `UserProtected` y `PasswordProtected`: ambos cifrados con DPAPI del usuario de Windows y representados en Base64. Base64 por sí solo es reversible y no constituye cifrado. La aplicación migra automáticamente el campo antiguo `User` al abrir; el instalador también escribe el nuevo formato. No copie estas credenciales a otro equipo o usuario: configure la conexión con el instalador en ese destino. No se publican credenciales en el `.exe.config` ni en GitHub. El instalador privado con contraseña precargada sigue siendo exclusivamente local.

Desde 1.6.1 la configuración predeterminada se convierte al formato sin IDs al instalar o abrir. Las listas antiguas personalizadas `ID|Nombre|true/false` siguen siendo compatibles; no se reescriben automáticamente para conservar posibles alias personalizados. Para pasarlas al formato nuevo utilice nombres reales de TEST.

## Cadena completa protegida · 1.6.2

`connection.ini` contiene únicamente el método de protección y `ConnectionProtected`, con la cadena completa cifrada mediante DPAPI CurrentUser y representada en Base64. Incluye servidor, base de datos, usuario, contraseña y opciones de conexión. Se migran automáticamente los formatos anteriores. La conexión alternativa en `eDelphyn.Hemograma.exe.config` se protege con el prefijo `dpapi:` al instalar o abrir; las pruebas siguen editables y sin cifrar.

El instalador recupera servidor, base y usuario para su pantalla de configuración; nunca muestra la contraseña guardada. Los datos cifrados solo se pueden utilizar bajo el mismo usuario de Windows y equipo. Para otro equipo, configure nuevamente la conexión en el instalador. Base64 no es cifrado por sí solo.

## Configuración visual · 1.6.3

Popup de preferencias con título, sección de actualización, repositorio enlazado, botones redondeados y mensaje de estado bajo las acciones. Mantiene las funciones de consulta, descarga verificada, instalación y reinicio, y los temas claro y oscuro.

## Retención de respaldos y límites del popup · 1.6.4

Solo se muestran y conservan los respaldos generados durante los últimos tres meses calendario, calculados en UTC por la fecha del nombre original (no por la fecha de la donación ni la de modificación del archivo). Se limpian al iniciar sesión, cada hora mientras la aplicación permanece abierta, al abrir Ver respaldos y antes de crear un nuevo respaldo. Si la aplicación está cerrada, la limpieza pendiente se realiza en la siguiente apertura. Archivos bloqueados o sin permisos se omiten del visor y se informa del problema; se reintenta la limpieza posteriormente. Solo se borran archivos con el nombre generado por la aplicación dentro de Respaldos, sin recorrer subcarpetas ni enlaces.

La capa oscura se limita al área interior de la ventana principal. El popup también se ajusta al espacio de esa ventana, y ambos siguen sus cambios de posición y tamaño.

## Búsqueda por número independiente de fechas · 1.6.5

Los números tienen prioridad: escribir o pegar Desde o Hasta limpia los dos campos de fecha. La consulta envía ambos límites de fecha como nulos, incluso si antes había fechas seleccionadas. No se combinan números y fechas en la interfaz. La fecha actual sigue siendo el valor inicial al abrir sin números.

## Filtros excluyentes en ambos sentidos · 1.6.6

Escribir un número vacía ambas fechas. Elegir una fecha (Desde o Hasta) vacía ambos números, incluso al elegir el día que ya contenía internamente un calendario vacío. Los dos grupos permanecen disponibles para cambiar directamente de modo. Solo se aplica el último tipo de filtro utilizado.

## Apertura al finalizar la instalación · 1.6.7

Tanto una instalación normal como una actualización muestran la casilla «Abrir eDelphyn Hemograma» marcada por defecto en la página final. El programa solo se ejecuta al pulsar Finalizar si la casilla sigue marcada. Si se desmarca, el instalador se cierra sin abrir la aplicación. Las instalaciones silenciosas no abren el programa.

## Fecha final automática · 1.6.8

Al pulsar Buscar, si Fecha donación Desde tiene un valor y Hasta está vacío, Hasta se completa con la fecha actual del equipo y se muestra en pantalla. La búsqueda incluye ese día completo. No reemplaza una fecha final elegida por el usuario ni agrega fechas a una búsqueda por número.
