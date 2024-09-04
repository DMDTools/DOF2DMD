# DOF2DMD

![DOF2DMD logo](DOF2DMD-on-dmd.png)

DOF2DMD es una peque�a utilidad para cabinets arcade que muestra marquesinas de juegos, puntuaciones y animaciones en un dispositivo DMD real o emulado..


Cubre los siguientes casos de uso:

- Inicio del juego: muestra la marquesina del juego.
- Puntuaci�n: muestra la puntuaci�n para 1 a 4 jugadores con diferentes disposiciones seg�n el n�mero de jugadores.
- Eventos: muestra im�genes, videos o animaciones en formato GIF basadas en eventos del juego (por ejemplo, derribar un avi�n en 1942 activar� una explosi�n).
- Texto: muestra texto con diferentes fuentes, tama�os y animaciones basadas en eventos.

DOF2DMD ofrece una sencilla API HTTP (ver [API](#api)) para mostrar im�genes, animaciones y puntuaciones.

Un caso de uso importante es la interfaz con [DOFLinx](https://www.vpforums.org/index.php?showforum=104) y su [versi�n modificada de MAME](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH) de [DDH69](https://www.vpforums.org/index.php?showuser=95623) para que el DMD muestre animaciones mientras se juega a MAME.

As� es como se ve con un DMD emulado (usando las extensiones Freezy DMD):

![demo](demo.gif)

DOF2DMD se basa en [FlexDMD](https://github.com/vbousquet/flexdmd), que a su vez utiliza las [extensiones Freezy DMD](https://github.com/freezy/dmd-extensions).

![Architecture](architecture.drawio.png)

## Configuiraci�n

Aqu� tienes la traducci�n al espa�ol:

---

- Descarga e instala .NET 8 "Runtime desktop" desde Microsoft: [https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.6-windows-x64-installer](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.6-windows-x64-installer)
- Descarga DOF2DMD desde la [secci�n de releases](https://github.com/DMDTools/DOF2DMD/releases), crea una carpeta y extrae el contenido del archivo en esta carpeta.
- Ajusta `settings.ini` si es necesario:

    ```ini
    ; Configuraci�n para DOF2DMD
    ; La URL base a la que DOF2DMD escuchar�. Por defecto es http://127.0.0.1:8080
    ; NO COMENTAR, ya que DOFLinx lee settings.ini para determinar a d�nde enviar las solicitudes.
    url_prefix=http://127.0.0.1:8080
    ; Mostrar la puntuaci�n durante x segundos, luego volver a la marquesina. Por defecto, 5 segundos.
    ;display_score_duration_s=5
    ; Salida detallada en el archivo debug.log si debug=true. Por defecto, false.
    ;debug=false
    ; Ruta de las im�genes (relativa a DOF2DMD o absoluta). Por defecto es "artwork".
    ;artwork_path=artwork
    ; Ancho en p�xeles para el DMD. Por defecto, 128
    ;dmd_width=128
    ; Altura en p�xeles para el DMD. Por defecto, 32
    ;dmd_height=32
    ; Imagen a mostrar cuando DOF2DMD se inicia. Por defecto, DOF2DMD (es decir, artwork/DOF2DMD.png o DOF2DMD.gif)
    ;start_picture=DOF2DMD
    ; Activar la visualizaci�n autom�tica del marcador o marquesina despu�s de usar una llamada.
    ;score_dmd=1
    ;marquee_dmd=1
    ; No implementado ---
    ;scene_default=marquee
    ;number_of_dmd=1
    ;animation_dmd=1
    ```
Aqu� tienes la traducci�n al espa�ol:

---

- Inicia DOF2DMD.
- Deber�as ver el logo de DOF2DMD, ya sea en un DMD virtual o en un DMD real si has configurado `DmdDevice.ini`.
- Si utilizas DOFLinx para MAME:
  - Instala [DOFLinx](https://www.vpforums.org/index.php?showforum=104) - consulta [Configuraci�n de DOFLinx para DOF2DMD](#use-in-doflinx).
  - Obt�n la [versi�n modificada de MAME](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH).
  - Inicia DOFLinx (deber�a ejecutarse al iniciar si est�s en un cabinet arcade).
  - Inicia tu juego de MAME. La versi�n modificada de MAME se comunicar� con DOFLinx, que a su vez activar� llamadas a la API de DOF2DMD.
- �Disfruta!

## Im�genes y Animaciones

Las im�genes y animaciones deben estar en la carpeta `artwork` (por defecto en la ruta de DOF2DMD, dentro de la carpeta `artwork`).

> [!NOTA]
> Se proporciona un conjunto b�sico de im�genes, para que puedas probar y comenzar a editar los archivos `.MAME` de DOFLinx. Probablemente necesitar�s m�s im�genes. No preguntes d�nde encontrar im�genes para el DMD. No podemos ayudarte con eso. Sin embargo, hay un paquete que puedes descargar, y m�s que puedes comprar en [Neo-Arcadia](https://www.neo-arcadia.com/forum/viewtopic.php?t=67065). Si posees un [PixelCade](https://pixelcade.org/), entonces tienes derecho a una enorme biblioteca de im�genes DMD de p�xeles perfectos. Para crear tus propias im�genes, puedes usar una herramienta de Pixel Art o un editor de GIFs, como [aseprite](https://www.aseprite.org/). Hay archivos de ejemplo de aseprite en [la carpeta `ingame.src`](/DOF2DMD/artwork/ingame.src/).

## API

DOF2DMD es un servidor que escucha solicitudes HTTP simples. Una vez iniciado, puedes usar las siguientes:

- `[url_prefix]/v1/display/picture?path=<ruta a imagen o video>&animation=<fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<segundos>`  
  Muestra una imagen, animaci�n gif o video.
  - **path**: La ruta al fichero de la imagen o video a ser mostrada (sin extensi�n)
  - **duration**: Si la duraci�n es 0 en una animaci�n/video, se limitar� a la duraci�n del video o animaci�n. Si la duraci�n es -1, se mantendr� permanente
  - **animation**: La animaci�n a aplicar a la escena fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None
- `[url_prefix]/v1/display/score?player=<jugador activor>&score=<puntuaci�n>&cleanbg=<true|false>`  
  Muestra una pantalla de puntuaci�n usando una disposici�n de hasta 4 jugadores
  - **players**: El n�mero de jugadores para la disposici�n de la pantalla de puntuaci�n. Opcional, por defecto 1
  - **player**: El jugador resaltado
  - **score**: El valor de la puntuaci�n para el jugador resaltado
  - **credits**: Cr�ditos insertados en el juego. Opcional
  - **cleanbg**: Borra la pantalla activa (si no se borra, la pantalla de puntuaci�n ser� mostrada sobre la imagen o animaci�n actual)
- `[url_prefix]/v1/display/scorebackground?path=<ruta imagen o video>`  
  A�ade una imagen, animaci�n gif o video como fondo en la pantalla de puntuaci�n. 
  - **path**: La ruta al archivo de la imagen o video a ser mostrada a la pantalla de puntuaci�n (sin extensi�n)
- `[url_prefix]/v1/blank`
  Limpia la pantalla
- `[url_prefix]/v1/exit`
  Sale y cierra la aplicaci�n de DOF2DMD
- `[url_prefix]/v1/version`
  Muestra la versi�n de la aplicaci�n de DOF2DMD
- `[url_prefix]/v1/display/text?text=<texto>?size=XS|S|M|L|XL&color=<hex color>&font=<font>&bordercolor=<hex color>&bordersize=<0|1>&cleanbg=<true|false>&animation=<ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<segundos>`  
  Muestra texto con o sin animaci�n
  - **text**: El texto a ser mostrado (el texto se puede separar en multiples lineas usando | como separador)
  - **size**: Tama�o del texto(Extra Peque�o (XS), Peque�o(S), Medio (M), Grande (L) o Extra Grande (XL))
  - **color**: El color del texto en formato hexadecimal (por ejemplo: color=FFFFFF)
  - **font**: La familia de la fuente a ser usada para el texto (En formato Bitmap Font file, hay algunos ejemplos en la carpeta resources, solo es necesario usar el nombre de la fuente antes del simbolo de subrayado _. Por ejemplo: Matrix o BTTF)
  - **bordercolor**: El color del borde del texto en formato hexadecimal (por ejemplo: color=FFFF00)
  - **bordersize**: El tama�o del borde del texto(0 o 1)
  - **cleanbg**: Borra la pantalla activa. (cuando no se borra el textpo se mostrar� sobre la imagen/animaci�n actual
  - **animation**: Animaci�n para el texto. ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None
  - **duration**: tiempo en segundos para mostrar el texto en el DMD. Si el tiempo es -1 y la animaci�n "none", se mostrar� el texto permanentemente, usando -1 en otra animaci�n presentar� una pantalla negra)
- `[url_prefix]/v1/display/advanced?path=<ruta de la imagen o video>&text=<text>?size=XS|S|M|L|XL&color=<hex color>&font=<font>&bordercolor=<hex color>&bordersize=<0|1>&cleanbg=<true|false>&animationin=<FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None>&animationout=<FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None>&duration=<segundos>`  
  Pantalla avanzada con animaciones. Puede usarse texto con o sin imagen/video/gif animado de fondo o imagen/video/gif animado
  - **text**: El texto a ser mostrado (el texto se puede separar en multiples lineas usando | como separador)
  - **path**: La ruta al archivo de la imagen o video a ser mostrado (sin extensi�n)
  - **size**: Tama�o del texto(Extra Peque�o (XS), Peque�o(S), Medio (M), Grande (L) o Extra Grande (XL))
  - **color**: El color del texto en formato hexadecimal (por ejemplo: color=FFFFFF)
  - **font**: La familia de la fuente a ser usada para el texto (En formato Bitmap Font file, hay algunos ejemplos en la carpeta resources, solo es necesario usar el nombre de la fuente antes del simbolo de subrayado _. Por ejemplo: Matrix o BTTF)
  - **bordercolor**: El color del borde del texto en formato hexadecimal (por ejemplo: color=FFFF00)
  - **bordersize**: El tama�o del borde del texto(0 o 1)
  - **cleanbg**: Borra la pantalla activa. (cuando no se borra el textpo se mostrar� sobre la imagen/animaci�n actual
  - **animationin**: Animaci�n de entrada: `FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None`
  - **animationout**: Animaci�n de salida: `FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None`
  - **duration**: tiempo en segundos para mostrar el texto e imagen en el DMD. Si el tiempo es -1 y la animaci�n "none", se mostrar� el texto permanentemente, usando -1 en otra animaci�n presentar� una pantalla negra)

Aqu� tienes la traducci�n al espa�ol:

---

## Uso en DOFLinx

Para generar efectos, DOFLinx utiliza archivos `.MAME` ubicados en la carpeta de MAME de DOFLinx. DOFLinx puede comunicarse con DOF2DMD utilizando el comando `FF_DMD` de DOFLinx. El comando `FF_DMD` puede llamar a cualquiera de las API de DOF2DMD.

### Archivo `DOFLinx.ini`

Aqu� tienes un archivo `DOFLinx.ini` m�nimo que funcionar� con `DOF2DMD`:

```ini
# ubicaci�n de tus archivos y sistemas
COLOUR_FILE=<ruta de DOFLinx>\config\colours.ini
DIRECTOUTPUTGLOBAL=<ruta de DOFLinx>\config\GlobalConfig_b2sserver.xml
PATH_MAME=<ruta de DOFLinx>\MAME\
MAME_FOLDER=<ruta del ejecutable de MAME (nota: debe ser la versi�n modificada de MAME de DOFLinx)>

# Cu�ndo activar, y m�s espec�ficamente, cu�l es el proceso de MAME para iniciar las cosas
PROCESSES=Mame64
MAME_PROCESS=Mame64

# DOF2DMD
PATH_DOF2DMD=<ubicaci�n del ejecutable de DOF2DMD y settings.ini>
```

Nota:

- `PATH_DOF2DMD`: la ubicaci�n del ejecutable de DOF2DMD y `settings.ini`
- `MAME_FOLDER`: ruta del ejecutable de MAME que debe ser la [versi�n modificada de MAME de DOFLinx](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH)

### Comandos Integrados

DOFLinx generar� autom�ticamente los siguientes comandos:

- Al iniciar DOFLinx:
  - `http://<host:port>/v1/version` - para verificar que DOF2DMD est� activo. DOFLinx intentar� iniciarlo en caso contrario.
  - `http://<host:port>/v1/display/picture?path=mame/DOFLinx` - para mostrar la imagen de bienvenida de DOFLinx
- Al iniciar un juego:
  - `http://<host:port>/v1/display/picture?path=mame/<nombre-rom>&duration=<duraci�n>&animation=<animaci�n>` - para mostrar un PNG para la marquesina
- Al jugar un juego:
  - `http://<host:port>/v1/display/score?player=<jugador activo>&score=<puntuaci�n>&cleanbg=<true|false>` - para mostrar la puntuaci�n del jugador dado
  - `http://<host:port>/v1/display/score?players=<n�mero de jugadores>&player=<jugador activo>&score=<puntuaci�n>&cleanbg=<true|false>&credits=<cr�ditos>` - para mostrar la puntuaci�n del jugador dado indicando el dise�o del marcador en funci�n del n�mero de jugadores
- Al cerrar DOFLinx:
  - `http://<host:port>/v1/display/score?player=1&score=0` - restablecer la puntuaci�n a 0
  - `http://<host:port>/v1/blank` - para borrar el DMD (se pone en negro)
  - `http://<host:port>/v1/exit` - para indicar a DOF2DMD a salir limpiamente

### Sintaxis del comando `FF_DMD` de DOFLinx

Para agregar efectos como mostrar animaciones o texto durante el juego, debes insertar el comando `FF_DMD` en el archivo `<rom>.MAME` que corresponde al juego.

```ascii
FF_DMD,U,<LLAMADA A LA API DE DOF2DMD sin host ni prefijo /v1/>
```

- `FF_DMD` es el comando
- `U` es para un comando de usuario (espec�fico de DOFLinx)
- Luego, la URI para llamar a DOF2DMD sin host ni prefijo /v1/


Ejemplos:

- Mostrar la animaci�n de bonificaci�n en el juego `artwork/ingame/bonus.gif`: `FF_DMD,U,display/picture?path=ingame/bonus&duration=0&animation=none`
- Mostrar una imagen est�tica `artwork/mame/pacman.png`: `FF_DMD,U,display/picture?path=mame/pacman&duration=-1`
- Mostrar un Gif animado si existe, o si no, caer en png: `artwork/mame/pacman.gif`: `FF_DMD,U,display/picture?path=mame/pacman&duration=-1`

Consulta los archivos `.MAME` incluidos en DOFLinx, que ya contienen comandos `FF_DMD`.

## Pruebas

Una vez que DOF2DMD est� iniciado, puedes usar tu navegador para probarlo:

- Mostrar versi�n: [http://127.0.0.1:8080/v1/version](http://127.0.0.1:8080/v1/version)
- Mostrar una imagen en la carpeta de artwork, subcarpeta `mame`, imagen `galaga`: [http://127.0.0.1:8080/v1/display/picture?path=mame/galaga&duration=-1&animation=fade](http://127.0.0.1:8080/v1/display/picture?path=mame/galaga&duration=-1&animation=fade)
- Establecer la puntuaci�n del jugador 1 (por defecto) a 1000 usando el dise�o de 4 jugadores y limpiando la escena actual: [http://127.0.0.1:8080/v1/display/score?score=1000](http://127.0.0.1:8080/v1/display/score?score=1000)
- Establecer la puntuaci�n del jugador 2 a 3998, cr�ditos a 5 usando el dise�o de 2 jugadores sobre la escena actual: [http://127.0.0.1:8080/v1/display/score?players=2&player=2&score=3998&cleanbg=false&credits=5](http://127.0.0.1:8080/v1/display/score?players=2&player=2&score=3998&cleanbg=false&credits=5)
- Establecer el jugador activo en el jugador 2 y la puntuaci�n a 2000 usando el dise�o de 2 jugadores limpiando la escena actual: [http://127.0.0.1:8080/v1/display/score?players=2&activeplayer=2&score=2000](http://127.0.0.1:8080/v1/display/score?players=2&activeplayer=2&score=2000)
- Mostrar texto usando tama�o M con la fuente Back To the Future, color de fuente naranja, color del borde de la fuente rojo y animaci�n de desplazamiento a la derecha durante 10 segundos: [http://127.0.0.1:8080/v1/display/text?text=HELLO|friends&font=BTTF&size=M&color=FFA500&bordersize=1&bordercolor=FF0000&cleanbg=true&animation=scrollright&duration=10](http://127.0.0.1:8080/v1/display/text?text=HELLO|friends&font=BTTF&size=M&color=FFA500&bordersize=1&bordercolor=FF0000&cleanbg=true&animation=scrollright&duration=10)
- Mostrar texto con una imagen de fondo usando la fuente White Rabbit en blanco y borde azul usando una animaci�n de desvanecimiento para entrar y desplazamiento a la derecha como animaci�n de salida y esperando 10 segundos entre animaciones: [http://127.0.0.1:8080/v1/display/advanced?path=mame/DOFLinx&text=Hello%20Friends!!&font=WhiteRabbit&size=M&color=0000ff&bordersize=1&bordercolor=ffffFF&cleanbg=true&animationin=FadeIn&animationout=ScrollOffRight&duration=10](http://127.0.0.1:8080/v1/display/advanced?path=mame/DOFLinx&text=Hello%20Friends!!&font=WhiteRabbit&size=M&color=0000ff&bordersize=1&bordercolor=ffffFF&cleanbg=true&animationin=FadeIn&animationout=ScrollOffRight&duration=10)
- Borrar el DMD: [http://127.0.0.1:8080/v1/blank](http://127.0.0.1:8080/v1/blank)
- Salir de DOF2DMD: [http://127.0.0.1:8080/v1/exit](http://127.0.0.1:8080/v1/exit)

O usa los scripts de PowerShell [`demo.ps1`](/DOF2DMD/demo.ps1) y [`demo2.ps1`](/DOF2DMD/demo2.ps1).

## TODO

Esto es lo que se planea implementar:

- Llamadas a la API que a�n no est�n implementadas
- Todo lo que falta en el `settings.ini`
- Un plugin para [LaunchBox / BigBox](http://pluginapi.launchbox-app.com/) que se interfase con DOF2DMD para mostrar sistemas y marquesinas de juegos al navegar por los juegos (implementado parcialmente)
- Un plugin para [Attract-Mode](https://attractmode.org/) que se interfase con DOF2DMD para mostrar sistemas y marquesinas de juegos al navegar por los juegos (en proceso)

## ??? Preguntas y soporte

Cuento con la comunidad de Pinball y Arcade para ayudarse mutuamente a trav�s de las [discusiones en GitHub](https://github.com/ojacques/DOF2DMD/discussions).
Tambi�n estar� all�.

## Agradecimientos

Gracias a:

- [@ojacques](https://github.com/ojacques) por crear la primera versi�n de este proyecto
- DDH69 por DOFLinx, MAME para DOFLinx y su apoyo en este proyecto. Piensa en [?? donar a DDH69](https://www.paypal.com/donate?hosted_button_id=YEPCTUYFX5KDE) para apoyar su trabajo.
- El equipo de [Pixelcade](https://pixelcade.org/) que nos inspir� a implementar algo para nuestro ZeDMD, incluyendo soporte para otros DMDs. Por favor, �chales un vistazo, me dicen que sus DMDs son de primera calidad, con m�ltiples tama�os, y si tienes uno de ellos, hay una tonelada de arte disponible.
- El creador de ZeDMD - [Zedrummer](https://www.pincabpassion.net/t14798-tuto-installation-du-zedmd), que es un DMD bonito y econ�mico. Puedes comprar ZeDMD en m�ltiples lugares.
- Todos en [Monte Ton Cab (FR)](https://montetoncab.fr/) - �qu� comunidad tan acogedora!