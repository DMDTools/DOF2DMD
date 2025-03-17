# DOF2DMD

![DOF2DMD logo](DOF2DMD-on-dmd.png)

DOF2DMD is a small utility for arcade cabinets to display game marquees, score
and animations on a real or emulated DMD device.

It covers the following use cases:

- Starting the game: showing the game marquee
- Score: showing the score for 1 up to 4 players with diferent layouts depending of the number of players
- Events: showing images, videos or gif animations based on events in the game (eg shooting down a plane in 1942 will trigger an explosion)
- Text: showing text with diferent fonts, sizes and animations based on events

DOF2DMD offers a simple HTTP API (see [API](#api)) to display pictures, animations and scores.

One big use case is to interface
[DOFLinx](https://www.vpforums.org/index.php?showforum=104) and its
[modified version of MAME](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH)
from [DDH69](https://www.vpforums.org/index.php?showuser=95623) to get the DMD
to show animations while playing MAME.

Here is how it looks like with an emulated DMD (using Freezy DMD extensions):

![demo](demo.gif)

DOF2DMD relies on a modified version of [FlexDMD][(https://github.com/gustavoalara/flexdmd/)], which itself
uses [Freezy DMD extensions](https://github.com/freezy/dmd-extensions)

![Architecture](architecture.drawio.png)

## Setup

- Download and install dotnet 8 "Runtime desktop" from Microsoft: https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.6-windows-x64-installer
- Download DOF2DMD from [Release section](https://github.com/DMDTools/DOF2DMD/releases), create a folder and extract the content of the archive in this folder
- Tweak `settings.ini` if needed:

    ```ini
    ; Settings for DOF2DMD
    ; The base URL that DOF2DMD will listen to. Defaults to http://127.0.0.1:8080
    ; DO NOT COMMENT OUT, as DOFLinx reads settings.ini to determine where to send requests to
    url_prefix=http://127.0.0.1:8080
    ; Display the score for x seconds, then back to marquee. Defaults to 5 seconds.
    ;display_score_duration_s=5
    ; Verbose output in debug.log file if debug=true. Defaults to false.
    ;debug=false
    ; Path of the artwork (relative to DOF2DMD or absolute). Defaults to "artwork"
    ;artwork_path=artwork
    ; Width in pixels for DMD. Defaults to 128
    ;dmd_width=128
    ; Height in pixels for DMD. Defaults to 32
    ;dmd_height=32
    ; Picture to display when DOF2DMD starts. Defaults to DOF2DMD (that is artwork/DOF2DMD.png or DOF2DMD.gif)
    ;start_picture=DOF2DMD
    ;Activate the autoshow of the Scoreboard or Marquee after using a call
    ;score_dmd=1
    ;marquee_dmd=1
    ;Uses hi2txt to show game highscores, needs MAME path to read highscores (and MAME highscore plugin activated)
    ;hi2txt_enabled=false
    ;hi2txt_path=c:\hi2txt
    ;mame_path=
    ; Not implemented ---
    ;scene_default=marquee
    ;number_of_dmd=1
    ;animation_dmd=1

    ```
- Launch DOF2DMD
- You should see the DOF2DMD logo, either on a Virtual DMD, or real DMD if you have configured `DmdDevice.ini`
- If using DOFLinx for MAME
  - Install [DOFLinx](https://www.vpforums.org/index.php?showforum=104) - see [DOFLinx setup for DOF2DMD](#use-in-doflinx)
  - Get [modified MAME version](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH)
  - Launch DOFLinx (should be at startup if you are on an Arcade cabinet).
  - Launch your MAME game. The modified version of MAME will communicate with
    DOFLinx, which in turn will trigger API calls to DOF2DMD.
- Enjoy!

## Artwork

The images and animations must be in the `artwork` folder (by default in the DOF2DMD path under the `artwork` folder).

> [!NOTE]
> I provide a basic set of artwork, so that you can test and start editing DOFLINX's `.MAME` files.
You probably need more artwork. I created a tool which may help - see [artwork4DMD](https://github.com/DMDTools/artwork4DMD).
In addition, there is a pack you can download, and more you can buy from
[Neo-Arcadia](https://www.neo-arcadia.com/forum/viewtopic.php?t=67065). If you
own a [PixelCade](https://pixelcade.org/), then you are entitled to a massive
pixel perfect DMD artwork library. To create your own artwork, you can use a 
Pixel Art tool or Gif editor, like [aseprite](https://www.aseprite.org/).
There are example aseprite files in [the `ingame.src` folder](/DOF2DMD/artwork/ingame.src/).

## API

DOF2DMD is a server listening to simple http request. Once it has started, you can use the following :

- `[url_prefix]/v1/display/picture?path=<image or video path>&animation=<fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<seconds>&queue&cleanbg=<true|false>`  
  Display an image, gif animation or video.
  - **path**: The file path of the image or video to be displayed
  - **duration**:
    - 0: picture will be displayed indefinitely, and animation will be displayed for the duration of the video or animation. 
    - >0: picture or animation will be displayed for the specified time in seconds
    - <0: picture or animation will be looped indefinitely
  - **animation**: The animation applied to the scene fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|Right2Right|Right2Left|Left2Left|Left2Right|Top2Top|Top2Bottom|Bottom2Bottom|Bottom2Top|None
    - The "Scroll<direction>" animations are smooth scrolls in the specified direction, with a total duration equal to the "duration" parameter
    - The "<direction2direction>" animations move the image from the specified direction, stay on screen for the time specified by "duration," and then exit in the indicated direction.
  - **queue**: If present, the image will be queued to be displayed after the current image is finished. If not present, the current image will be replaced immediately by the new one
  - **cleanbg**: Clean the active screen (when not cleaned the picture will showed over the current image/animation/text on the DMD)
- `[url_prefix]/v1/display/score?players=<number of players>&player=<active player>&score=<score>&cleanbg=<true|false>`  
  Display a score board using a layout from 1 to 4 players and credits**
  - **players**: the number of players for score layout. Optional, default 1
  - **player**: the highlighted player
  - **score**: The score value to be displayed for active player
  - **credits**: Credits inserted in the game. Optional
  - **cleanbg**: Clean the active screen (when not cleaned the score will showed over the current image/animation/text)
- `[url_prefix]/v1/display/scorebackground?path=<image or video path>`  
  Add an image, gif animation or video as background on the Score board. 
  - **path**: The file path of the image or video to be displayed/added to de Score Board
- `[url_prefix]/v1/blank`
  This endpoint clears or blanks the display
- `[url_prefix]/v1/exit`
  This endpoint exits or closes the application
- `[url_prefix]/v1/version`
  This endpoint returns the version information of the application
- `[url_prefix]/v1/loopstop`
  This endpoint stops an active infinite scroll text
- `[url_prefix]/v1/display/text?text=<text>?size=XS|S|M|L|XL&color=<hex color>&font=<font>&bordercolor=<hex color>&bordersize=<0|1>&cleanbg=<true|false>&animation=<ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<seconds>&loop=<true|false>&queue`  
  Display some text with or without animation
  - **text**: The text to be displayed (the text can be split into multiple lines using | as a separator)
  - **size**: The size of the text (Extra Small (XS), Small (S), Medium (M), Large (L) or Extra Large (XL))
  - **color**: The color of the text in hexadecimal format (for example: color=FFFFFF)
  - **font**: The font family to be used for the text (Bitmap Font file, there are some samples on resources folder, only is needed to use the Font name before the _ symbol. For example: Matrix or BTTF)
  - **bordercolor**: The color of the text border in hexadecimal format (for example: color=FFAAFF)
  - **bordersize**: The size of the text border (0 or 1)
  - **cleanbg**: Clean the active screen (when not cleaned the text will showed over the current image/animation
  - **animation**: Text animation. ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None
  - **duration**: time to present the text in the DMD (If an animation is selected, the screen will remain black once the animation ends if the time is longer than the animation itself. If the time is -1 in none text animation, it will be permanent, using -1 in another animation presents a black screen)
  - **loop**: enable text scroll infinite loop
  - **queue**: If present, the text will be queued to be displayed after the current object on DMD is finished. If not present, the current object in the DMD will be replaced immediately by the new text
- `[url_prefix]/v1/display/advanced?path=<image or video path>&text=<text>?size=XS|S|M|L|XL&color=<hex color>&font=<font>&bordercolor=<hex color>&bordersize=<0|1>&cleanbg=<true|false>&animationin=<FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None>&animationout=<FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None>&duration=<seconds>`  
  Advanced display with animations. Text with or without background picture/video/animated gif or picture/video/animated gif can be used
  - **text**: The text to be displayed (the text can be split into multiple lines using | as a separator) 
  - **path**: The file path of the image or video to be displayed
  - **size**: The size of the text (Extra Small (XS), Small (S), Medium (M), Large (L) or Extra Large (XL))
  - **color**: The color of the text in hexadecimal format (for example: color=FFFFFF)
  - **font**: The font family to be used for the text (Bitmap Font file, there are some samples on resources folder, only is needed to use the Font name before the _ simbol. For example: Matrix or BTTF)
  - **bordercolor**: The color of the text border in hexadecimal format (for example: color=FFAAFF)
  - **bordersize**: The size of the text border (0 or 1)
  - **cleanbg**: Clean the active screen (when not cleaned the text will showed over the current image/animation
  - **animationin**: Display animation: `FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None`
  - **animationout**: Display animation: `FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None`
  - **duration**: time to present the scene in the DMD (If an animation is selected, the screen will remain black once the animation ends if the time is longer than the animation itself. If the time is -1, it will be permanent)
- `[url_prefix]/v1/display/highscores?game=<gamename>?size=XS|S|M|L|XL&color=<hex color>&font=<font>&bordercolor=<hex color>&bordersize=<0|1>&cleanbg=<true|false>&animation=<ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<seconds>&loop=<true|false>&queue`  
  Display scrolling highscores using MAME Highscores plugin 
  - **game**: The name of the MAME hiscore game without the .hi as appears in MAME\hiscore folder 
  - **size**: The size of the text (Extra Small (XS), Small (S), Medium (M), Large (L) or Extra Large (XL)) (Default M)
  - **color**: The color of the text in hexadecimal format (for example: color=FFFFFF) (Default white)
  - **font**: The font family to be used for the text (Bitmap Font file, there are some samples on resources folder, only is needed to use the Font name before the _ symbol. For example: Matrix or BTTF) (Default Consolas)
  - **bordercolor**: The color of the text border in hexadecimal format (for example: color=FFAAFF)
  - **bordersize**: The size of the text border (0 or 1) (Default 0)
  - **cleanbg**: Clean the active screen (when not cleaned the text will showed over the current image/animation) (Default: true)
  - **animation**: Highscore animation. ScrollUp|ScrollDown
  - **duration**: time to present the highscore list in the DMD 
  - **loop**: enable highscore scroll infinite loop
  - **queue**: If present, the highscore will be queued to be displayed after the current object on DMD is finished. If not present, the current object in the DMD will be replaced immediately by the highscore

## Using MAME highscores

![hiscores](hiscores.gif)


DOF2DMD now has the ability to display the MAME high score list as well. To enable this, MAME must have the Highscores plugin activated, and Hi2txt (https://greatstoneex.github.io/hi2txt-doc/) must be installed.  
If you want to use this feature, you need to enable it in *settings.ini*, specify the path to your MAME installation, and provide the path to the *hi2txt* executable, as shown below:

```ini
hi2txt_enabled=true
hi2txt_path=c:\hi2txt
mame_path=C:\MAME
```

Since DOFLinx also supports MAME highscores, future releases will add DOF2DMD to send highscores from DOFLinx. So, if you prefer to have DOFLinx handle the highscores, it will not be necessary to activate them in DOF2DMD 

## Use in DOFLinx

To generate effects, DOFLinx uses `.MAME` files located in DOFLinx's MAME
folder. DOFLinx can communicate with DOF2DMD, using DOFLinx `FF_DMD` command.
The `FF_DMD` command can call any of the DOF2DMD APIs.

### `DOFLinx.ini` file

Here is a minimal DOFLinx.ini file which will work with `DOF2DMD`:

```ini
# location of your files and systems
COLOUR_FILE=<DOFLinx path>\config\colours.ini
DIRECTOUTPUTGLOBAL=<DOFLinx path>\config\GlobalConfig_b2sserver.xml
PATH_MAME=<DOFLinx path>\MAME\
MAME_FOLDER=<MAME executable path (note: it must be DOFLinx modified MAME version)>

# When to activate, and more specifically what is the MAME process to kick things off
PROCESSES=Mame64
MAME_PROCESS=Mame64

# DOF2DMD
PATH_DOF2DMD=<location of DOF2DMD executable and settings.ini>
```

Note:

- `PATH_DOF2DMD`: the location of DOF2DMD executable and settings.ini
- `MAME_FOLDER`: MAME executable path which must be DOFLinx's [modified version of MAME](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH)

### Embedded commands

DOFLinx will generate the following commands automatically:

- When starting DOFLinx:
  - `http://<host:port>/v1/version` - to check that DOF2DMD is up. DOFLinx will attempt to start it otherwise.
  - `http://<host:port>/v1/display/picture?path=mame/DOFLinx` - to display the DOFLinx welcome picture
- When starting a game:
  - `http://<host:port>/v1/display/picture?path=mame/<rom-name>&duration=<duration>&animation=<animation>` - to display a PNG for the marquee
- When playing a game:
  - `http://<host:port>/v1/display/score?player=<active player>&score=<score>&cleanbg=<true|false>` - to display score of the given player
  - `http://<host:port>/v1/display/score?players=<number of players>&player=<active player>&score=<score>&cleanbg=<true|false>&credits=<credits>` - to display score of the given player inidicating the score board layout based on the number of players
- When closing DOFLinx:
  - `http://<host:port>/v1/display/score?player=1&score=0` - reset score to 0
  - `http://<host:port>/v1/blank` - to clear the DMD (goes to black)
  - `http://<host:port>/v1/exit` - to instruct DOF2DMD to exit cleanly

### Syntax of `FF_DMD` DOFLinx command

To add effects like showing animations or text during the game, you must insert
the `FF_DMD` command in the `<rom>.MAME` file which corresponds to the game.

```ascii
FF_DMD,U,<DOF2DMD API CALL without host nor /v1/ prefix>
```

- `FF_DMD` is the command
- `U` is for a user command (DOFLinx specific)
- Then the URI to call DOF2DMD without host nor /v1/ prefix

Examples :

- Display the ingame bonus animation `artwork/ingame/bonus.gif` : `FF_DMD,U,display/picture?path=ingame/bonus&duration=0&animation=none`
- Display a static picture `artwork/mame/pacman.png` : `FF_DMD,U,display/picture?path=mame/pacman&duration=-1`
- Display an animated Gif if it exists or falls back to png : `artwork/mame/pacman.gif` : `FF_DMD,U,display/picture?path=mame/pacman&duration=-1`

Check the `.MAME` files included in DOFLinx, which already contain `FF_DMD` commands.

## Testing

Once DOF2DMD is started, you can use your browser to test it:

- Show version [http://127.0.0.1:8080/v1/version](http://127.0.0.1:8080/v1/version) 
- Display picture in the artwork folder, subfolder `mame`, picture `galaga`: [http://127.0.0.1:8080/v1/display/picture?path=mame/galaga&duration=-1&animation=fade](http://127.0.0.1:8080/v1/display/picture?path=mame/galaga&duration=-1&animation=fade) 
- Set score of player 1 (default) to 1000 using default 4 player layout and cleaning the current scene: [http://127.0.0.1:8080/v1/display/score?score=1000](http://127.0.0.1:8080/v1/display/score?score=1000)
- Set score of player 2 to 3998, credits to 5 using 2 player layout over the current scene: [http://127.0.0.1:8080/v1/display/scorev2?players=2&activeplayer=2&score=3998&cleanbg=false&credits=5](http://127.0.0.1:8080/v1/display/score?players=4&player=2&score=3998&cleanbg=false&credits=5)
- Set active player to player 2 and set score to 2000 using 2 players layout cleaning the current scene: [http://127.0.0.1:8080/v1/display/score?players=2&player=2&score=2000](http://127.0.0.1:8080/v1/display/score?players=2&player=2&score=2000)
- Show text using M size with Back To the Future Font, orange font color, red border font color and scroll right animation during 10 seconds: [http://127.0.0.1:8080/v1/display/text?text=HELLO|friends&font=BTTF&size=M&color=FFA500&bordersize=1&bordercolor=FF0000&cleanbg=true&animation=scrollright&duration=10](http://127.0.0.1:8080/v1/display/text?text=HELLO|friends&font=BTTF&size=M&color=FFA500&bordersize=1&bordercolor=FF0000&cleanbg=true&animation=scrollright&duration=10)
- Show text with a background image using White Rabbit font in white and blue border using a fade animation in and a scroll right as animation out and waiting 10 seconds betwwen animations [http://127.0.0.1:8080/v1/display/advanced?path=mame/DOFLinx&text=Hello%20Friends!!&font=WhiteRabbit&size=M&color=0000ff&bordersize=1&bordercolor=ffffFF&cleanbg=true&animationin=FadeIn&animationout=ScrollOffRight&duration=10](http://127.0.0.1:8080/v1/display/advanced?path=mame/DOFLinx&text=Hello%20Friends!!&font=WhiteRabbit&size=M&color=0000ff&bordersize=1&bordercolor=ffffFF&cleanbg=true&animationin=FadeIn&animationout=ScrollOffRight&duration=10)
- Blank the DMD [http://127.0.0.1:8080/v1/blank](http://127.0.0.1:8080/v1/blank)
- Exit DOF2DMD [http://127.0.0.1:8080/v1/exit](http://127.0.0.1:8080/v1/exit)

or use the [`demo.ps1`](/DOF2DMD/demo.ps1) and [`demo2.ps1`](/DOF2DMD/demo2.ps1) PowerShell script.

## Frontends plugin

- A plugin for [Attract-Mode](https://attractmode.org/) which interfaces with DOF2DMD to show systems, game marquees and info when browsing
  games is done and can found in [Attract/DMDPlayer](https://github.com/DMDTools/DOF2DMD/tree/main/Attract/Plugins/DMDPlayer)


## TODO

Here is what I plan to implement : 

- API calls which are not implemented yet
- Everything missing from the `settings.ini`
- A plugin for [Launch box / big box](http://pluginapi.launchbox-app.com/) which
  interfaces with DOF2DMD to show systems and game marquees when browsing
  games (partially implemented)


## 💬 Questions and support

I count on the Pinball and Arcade community to help each other through the [GitHub discussions](https://github.com/ojacques/DOF2DMD/discussions).
I will be there too.

## Thank you

Thanks to

- [@ojacques](https://github.com/ojacques) for creating the first version of this project
- DDH69 for DOFLinx, MAME for DOFLinx, and his support in this project. Think of
  [💲donating to DDH69](https://www.paypal.com/donate?hosted_button_id=YEPCTUYFX5KDE) to support his work.
- [Pixelcade](https://pixelcade.org/) team who inspired me in implementing
  something for my ZeDMD, including support for other DMDs. Please, check them
  out, I am told their DMDs are top notch, multiple sizes, and if you own one of
  them, there is a ton of artwork available.
- The creator of ZeDMD -
  [Zedrummer](https://www.pincabpassion.net/t14798-tuto-installation-du-zedmd),
  which is a nice and cheap DMD. You can buy ZeDMD in multiple places.
- Everyone at [Monte Ton Cab (FR)](https://montetoncab.fr/) - what a welcoming
  community!
