# eDelphyn · Gestión de hemograma

Aplicación de escritorio Windows Forms en C# / .NET Framework 4.8. Ejecutable: `dist/eDelphyn.Hemograma.exe`.

## Instalación y actualización · versión 1.4.2

Descargue el instalador desde [GitHub Releases](https://github.com/floresroldanluis/MBeDelphynHemograma/releases/latest). Incluye .NET Framework 4.8 sin necesidad de descargarlo al instalar. Si falta este componente, solicita permisos de administrador y puede requerir reiniciar Windows. La aplicación se instala para el usuario actual en Windows 10/11.

El asistente solicita servidor, base, usuario y contraseña SQL, con contraseña oculta y botón **Probar conexión**. Usuario sugerido: eDelphyn. Ingrese la contraseña durante la instalación; no está incorporada en el repositorio ni en el instalador. En otro equipo indique el servidor real, no localhost. La contraseña queda cifrada con Windows DPAPI para el usuario actual en connection.ini. No copie ese archivo a otra cuenta o máquina; configure nuevamente la conexión. Una actualización permite conservar la credencial guardada dejando la contraseña vacía.

El encabezado es **Resultados Inmunohematológicos de Donantes**. Tema claro/oscuro, fechas y checks adaptados al tema; popups modales con esquinas redondeadas. Acerca de muestra logo, versión, resumen y enlace https://diagnosticoual.com/, sin QR.

**Configuración → Actualizaciones** consulta GitHub Releases y muestra la versión actual y la disponible. Si está actualizado indica «Ya tienes la versión más reciente». Para una versión nueva permite descargar, verificar e iniciar el instalador y reiniciar al terminar. Se verifica tamaño, digest cuando GitHub lo proporciona, firma, editor, producto y versión. No se envían datos de donaciones a GitHub. Cerrar el panel cancela una descarga.

La firma actual es local, no de una autoridad pública. Otros equipos requieren que TI gestione la confianza o una firma corporativa/pública; no garantiza la aceptación por Sophos. El instalador no modifica antivirus ni instala certificados.

Para compilar: build.ps1. Para generar el instalador: build-installer.ps1 (Inno Setup 6 y certificado de firma configurado en signing/thumbprint.txt). El script descarga y verifica el redistribuible oficial si falta. La clave privada nunca debe subirse. Para publicar use publish-release.ps1 desde un checkout Git autenticado después de incrementar las versiones en AssemblyInfo.cs, setup.iss y build-installer.ps1 y ejecutar las verificaciones. El instalador se adjunta a Releases, no al historial Git.

## Uso

1. Abra el ejecutable tras configurar y probar la conexión SQL en el instalador.
2. **Fecha donación** inicia con la fecha actual del equipo en Desde y Hasta, sin casillas. Al introducir **Donación número**, las fechas predeterminadas quedan vacías y dejan de filtrar. Si selecciona fechas expresamente, se combinan con el número mediante AND y se conservan al editar los números. Una fecha vacía no impone límite; Supr o Retroceso vacía el campo enfocado. Puede elegir una fecha en el calendario (incluso hoy) o pulsar Enter sobre un campo vacío para activar la fecha que contiene internamente. Hasta incluye el día completo. Se admite uno o ambos extremos de cada filtro. Los códigos se comparan como texto según la intercalación de SQL Server.
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

La configuración de desarrollo `dist/eDelphyn.Hemograma.exe.config` usa `localhost`, base `eDelphyn` y autenticación integrada de Windows. No contiene contraseñas. Puede cambiar la cadena de conexión con la aplicación cerrada. La cuenta necesita SELECT sobre DONATION y DONATION_TEST, DELETE sobre DONATION_TEST y los permisos requeridos por el trigger existente.

## Transacción y respaldo

Antes del DELETE se vuelven a leer todos los campos de las pruebas seleccionadas bajo una transacción SERIALIZABLE con UPDLOCK/HOLDLOCK. Si hay cualquier diferencia respecto de la búsqueda (incluidas altas o bajas de pruebas), se cancela y se exige buscar nuevamente. Consultas parametrizadas; lista de ID_TEST fija.

Los registros completos y los identificadores de las donaciones se guardan como XML con esquema en `%LOCALAPPDATA%\eDelphyn.Hemograma\Respaldos`. El archivo se sincroniza y se relee para comprobarlo antes del DELETE. Si falla el respaldo o la operación SQL, la transacción no se confirma. No se guarda una contraseña. Los respaldos contienen resultados de laboratorio y heredan los permisos de la carpeta del usuario Windows.

Un XML refleja el estado **previo al intento**, y puede existir aunque el intento se haya cancelado. No es prueba de una eliminación confirmada. La restauración no es automática: un administrador debe revisar el XML y el estado actual antes de restaurar registros. No incluye una copia del valor anterior de DAT_MODIFIED. Si se interrumpe la conexión durante COMMIT, vuelva a consultar para determinar el resultado; no repita el borrado a ciegas.

## Compilar y verificar

Ejecute `powershell -ExecutionPolicy Bypass -File .\build.ps1`. Usa el compilador incluido con .NET Framework, sin paquetes externos.

Ejecute `dist\eDelphyn.Hemograma.exe --self-test` y espere al proceso. El informe queda en `dist\test-results\results.txt`. La prueba consulta el rango real C260730–C260740 únicamente en lectura; todas las inserciones, modificaciones y eliminaciones se hacen en tablas temporales locales de sesión (`#DONATION`, `#DONATION_TEST`). Cubre filtros, selección, concurrencia, respaldo, preservación de otras pruebas/donaciones y rollback SQL.

## Validación y distribución

Las verificaciones en tests cubren filtros, ordenamiento, versiones y firma del instalador. La prueba SQL integrada utiliza tablas temporales para las operaciones de escritura. No se modifican donaciones reales durante las pruebas.

La firma local usa la huella pública configurada en Deployment.cs. La clave privada permanece en el almacén de Windows. Para cambiar de editor debe planificarse la migración del verificador de actualizaciones. No hay sello de tiempo: renueve y vuelva a firmar antes de caducar el certificado.

## Instalador privado
La variante -Privado precarga una contraseña oculta con asteriscos. Se genera con build-installer.ps1 -PrivateBuild y un archivo local installer/private-defaults.iss, excluido de Git. Nunca publique esa variante ni su archivo de configuración. La versión pública mantiene la contraseña vacía. Siguiente prueba la conexión y avanza directamente a Listo para instalar; ante un error permanece en la página SQL.

