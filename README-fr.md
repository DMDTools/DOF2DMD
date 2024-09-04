# DOF2DMD

![Logo DOF2DMD](DOF2DMD-on-dmd.png)

DOF2DMD est un petit utilitaire pour les bornes d'arcade permettant d'afficher des banières de jeu, des scores et des animations sur un appareil DMD réel ou émulé.

Il couvre les cas d'utilisation suivants :

- Lancement du jeu : affichage de la banière du jeu
- Score : affichage du score pour 1 à 4 joueurs avec des dispositions différentes selon le nombre de joueurs
- Événements : affichage d'images, de vidéos ou d'animations gif en fonction des événements du jeu (par exemple, abattre un avion dans 1942 déclenchera une explosion)
- Texte : affichage de texte avec différentes polices, tailles et animations en fonction des événements

DOF2DMD offre une simple API HTTP (voir [API](#api)) pour afficher des images, des animations et des scores.

Un cas d'utilisation majeur est d'interface
[DOFLinx](https://www.vpforums.org/index.php?showforum=104) et sa
[version modifiée de MAME](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH)
de [DDH69](https://www.vpforums.org/index.php?showuser=95623) pour faire en sorte que le DMD affiche des animations pendant que vous jouez à MAME.

Voici à quoi cela ressemble avec un DMD émulé (en utilisant les extensions Freezy DMD) :

![démo](demo.gif)

DOF2DMD s'appuie sur [FlexDMD](https://github.com/vbousquet/flexdmd), qui utilise lui-même les [extensions Freezy DMD](https://github.com/freezy/dmd-extensions)

![Architecture](architecture.drawio.png)

## Installation

- Téléchargez et installez .NET 8 "Runtime desktop" de Microsoft : https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.6-windows-x64-installer
- Téléchargez DOF2DMD depuis la [section Realease](https://github.com/ojacques/DOF2DMD/releases), créez un dossier et extrayez le contenu de l'archive dans ce dossier
- Ajustez `settings.ini` si nécessaire :

    ```ini
    ; Paramètres pour DOF2DMD
    ; L'URL de base que DOF2DMD écoutera. Par défaut http://127.0.0.1:8080
    ; NE PAS COMMENTER, car DOFLinx lit settings.ini pour déterminer où envoyer les requêtes
    url_prefix=http://127.0.0.1:8080
    ; Affiche le score pendant x secondes, puis revient à la banière. Par défaut 5 secondes.
    ;display_score_duration_s=5
    ; Sortie détaillée dans le fichier debug.log si debug=true. Par défaut false.
    ;debug=false
    ; Chemin vers les images (relatif à DOF2DMD ou absolu). Par défaut "artwork"
    ;artwork_path=artwork
    ; Largeur en pixels pour le DMD. Par défaut 128
    ;dmd_width=128
    ; Hauteur en pixels pour le DMD. Par défaut 32
    ;dmd_height=32
    ; Image à afficher au démarrage de DOF2DMD. Par défaut DOF2DMD (c'est-à-dire artwork/DOF2DMD.png ou DOF2DMD.gif)
    ;start_picture=DOF2DMD
    ;Activez l'affichage automatique du tableau de bord ou de la banière après avoir utilisé un appel
    ;score_dmd=1
    ;marquee_dmd=1
    ; Non implémenté ---
    ;scene_default=marquee
    ;number_of_dmd=1
    ;animation_dmd=1
    ```
- Lancez DOF2DMD
- Vous devriez voir le logo DOF2DMD, soit sur un DMD virtuel, soit sur un DMD réel si vous avez configuré `DmdDevice.ini`
- Si vous utilisez DOFLinx pour MAME :
  - Installez [DOFLinx](https://www.vpforums.org/index.php?showforum=104) - voir [Configuration DOFLinx pour DOF2DMD](#utilisation-avec-doflinx)
  - Obtenez la [version modifiée de MAME](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH)
  - Lancez DOFLinx (cela devrait être au démarrage si vous êtes sur une borne d'arcade).
  - Lancez votre jeu MAME. La version modifiée de MAME communiquera avec
    DOFLinx, qui à son tour déclenchera des appels API vers DOF2DMD.
- Profitez-en !

## Artwork

Les images et animations doivent se trouver dans le dossier `artwork` (par défaut dans le chemin DOF2DMD sous le dossier `artwork`).

> [!NOTE]
> Je fournis un ensemble de base de visuels, afin que vous puissiez tester et commencer à éditer les fichiers `.MAME` de DOFLINX.
Vous aurez probablement besoin de plus de visuels. Ne me demandez pas où trouver des visuels pour le DMD. Je ne peux pas aider
là-dessus. Il existe cependant un pack que vous pouvez télécharger, et d'autres que vous pouvez acheter sur
[Neo-Arcadia](https://www.neo-arcadia.com/forum/viewtopic.php?t=67065). Si vous
possédez un [PixelCade](https://pixelcade.org/), vous avez droit à une énorme
bibliothèque d'art DMD en pixel perfect. Pour créer vos propres visuels, vous pouvez utiliser un
outil de Pixel Art ou un éditeur de Gif, comme [aseprite](https://www.aseprite.org/).
Il existe des fichiers aseprite d'exemple dans le [dossier `ingame.src`](/DOF2DMD/artwork/ingame.src/).

## API

DOF2DMD est un serveur écoutant des requêtes HTTP simples. Une fois démarré, vous pouvez utiliser les éléments suivants :

- `[url_prefix]/v1/display/picture?path=<chemin de l'image ou de la vidéo>&animation=<fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<secondes>`  
  Affiche une image, une animation GIF ou une vidéo.
  - **path**: Le chemin du fichier de l'image ou de la vidéo à afficher
  - **duration**: Si la durée est de 0 pour une animation/vidéo, elle sera limitée à la durée de la vidéo ou de l'animation. Si le temps est de -1, elle sera permanente
  - **animation**: L'animation appliquée à la scène fade|ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None
- `[url_prefix]/v1/display/score?players=<Nombre de joueurs>&player=<joueur actif>&score=<score>&cleanbg=<true|false>`  
  Affiche un tableau de scores avancé avec une disposition de 1 à 4 joueurs et des crédits
  - **players**: le nombre de joueurs pour la disposition des scores. Optionnel, par défaut 1
  - **player**: le joueur mis en avant
  - **score**: La valeur du score à afficher pour le joueur actif
  - **cleanbg**: Nettoie l'écran actif (si non nettoyé, le score sera affiché par-dessus l'image/l'animation actuelle)
  - **credits**: Crédits insérés dans le jeu. Optionnel
- `[url_prefix]/v1/display/scorebackground?path=<chemin de l'image ou de la vidéo>`  
  Ajoute une image, une animation GIF ou une vidéo en tant que fond sur le tableau de scores. 
  - **path**: Le chemin du fichier de l'image ou de la vidéo à afficher/ajouter au tableau de scores
- `[url_prefix]/v1/blank`
  Cet endpoint efface ou vide l'affichage
- `[url_prefix]/v1/exit`
  Cet endpoint quitte ou ferme l'application
- `[url_prefix]/v1/version`
  Cet endpoint renvoie les informations de version de l'application
- `[url_prefix]/v1/display/text?text=<texte>?size=XS|S|M|L|XL&color=<couleur hexadécimale>&font=<police>&bordercolor=<couleur hexadécimale>&bordersize=<0|1>&cleanbg=<true|false>&animation=<ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None>&duration=<secondes>`  
  Affiche du texte avec ou sans animation
  - **text**: Le texte à afficher (le texte peut être divisé en plusieurs lignes en utilisant | comme séparateur)
  - **size**: La taille du texte (Extra Small (XS), Small (S), Medium (M), Large (L) ou Extra Large (XL))
  - **color**: La couleur du texte en format hexadécimal (par exemple : color=FFFFFF)
  - **font**: La famille de police à utiliser pour le texte (fichier de police bitmap, il y a quelques exemples dans le dossier des ressources, il suffit d'utiliser le nom de la police avant le symbole _ . Par exemple : Matrix ou BTTF)
  - **bordercolor**: La couleur de la bordure du texte en format hexadécimal (par exemple : color=FFAAFF)
  - **bordersize**: La taille de la bordure du texte (0 ou 1)
  - **cleanbg**: Nettoie l'écran actif (si non nettoyé, le texte sera affiché par-dessus l'image/l'animation actuelle)
  - **animation**: Animation du texte. ScrollRight|ScrollLeft|ScrollUp|ScrollDown|None
  - **duration**: temps pour afficher le texte sur le DMD (Si une animation est sélectionnée, l'écran restera noir une fois l'animation terminée si le temps est supérieur à l'animation elle-même. Si le temps est de -1 dans une animation de texte None, il sera permanent, utiliser -1 dans une autre animation présente un écran noir)
- `[url_prefix]/v1/display/advanced?path=<chemin de l'image ou de la vidéo>&text=<texte>?size=XS|S|M|L|XL&color=<couleur hexadécimale>&font=<police>&bordercolor=<couleur hexadécimale>&bordersize=<0|1>&cleanbg=<true|false>&animationin=<FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None>&animationout=<FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None>&duration=<secondes>`  
  Affichage avancé avec animations. Le texte avec ou sans image/vidéo/fond gif animé ou image/vidéo/gif animé peut être utilisé
  - **text**: Le texte à afficher (le texte peut être divisé en plusieurs lignes en utilisant | comme séparateur) 
  - **path**: Le chemin du fichier de l'image ou de la vidéo à afficher
  - **size**: La taille du texte (Extra Small (XS), Small (S), Medium (M), Large (L) ou Extra Large (XL))
  - **color**: La couleur du texte en format hexadécimal (par exemple : color=FFFFFF)
  - **font**: La famille de police à utiliser pour le texte (fichier de police bitmap, il y a quelques exemples dans le dossier des ressources, il suffit d'utiliser le nom de la police avant le symbole _ . Par exemple : Matrix ou BTTF)
  - **bordercolor**: La couleur de la bordure du texte en format hexadécimal (par exemple : color=FFAAFF)
  - **bordersize**: La taille de la bordure du texte (0 ou 1)
  - **cleanbg**: Nettoie l'écran actif (si non nettoyé, le texte sera affiché par-dessus l'image/l'animation actuelle)
  - **animationin**: Animation d'affichage : `FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None`
  - **animationout**: Animation d'affichage : `FadeIn|FadeOut|ScrollOffRight|ScrollOffLeft|ScrollOnLeft|ScrollOnRight|ScrollOffUp|ScrollOffDown|ScrollOnUp|ScrollOnDown|FillFadeIn|FillFadeOut|None`
  - **duration**: temps pour afficher la scène sur le DMD (Si une animation est sélectionnée, l'écran restera noir une fois l'animation terminée si le temps est supérieur à l'animation elle-même. Si le temps est de -1, il sera permanent)

## Utilisation dans DOFLinx

Pour générer des effets, DOFLinx utilise des fichiers `.MAME` situés dans le dossier MAME de DOFLinx.
DOFLinx peut communiquer avec DOF2DMD, en utilisant la commande `FF_DMD` de DOFLinx.
La commande `FF_DMD` peut appeler n'importe quelle API de DOF2DMD.

### Fichier `DOFLinx.ini`

Voici un fichier DOFLinx.ini minimal qui fonctionnera avec `DOF2DMD` :

```ini
# emplacement de vos fichiers et systèmes
COLOUR_FILE=<chemin DOFLinx>\config\colours.ini
DIRECTOUTPUTGLOBAL=<chemin DOFLinx>\config\GlobalConfig_b2sserver.xml
PATH_MAME=<chemin DOFLinx>\MAME\
MAME_FOLDER=<chemin de l'exécutable MAME (note : il doit s'agir de la version modifiée de MAME par DOFLinx)>

# Quand activer, et plus précisément quel est le processus MAME pour démarrer les choses
PROCESSES=Mame64
MAME_PROCESS=Mame64

# DOF2DMD
PATH_DOF2DMD=<emplacement de l'exécutable DOF2DMD et du fichier settings.ini>
```

Note :

- `PATH_DOF2DMD` : l'emplacement de l'exécutable DOF2DMD et du fichier settings.ini
- `MAME_FOLDER` : chemin d'accès à l'exécutable MAME qui doit être la [version modifiée de MAME par DOFLinx](https://drive.google.com/drive/folders/1AjJ8EQo3AkmG2mw7w0fLzF9HcOjFoUZH)

### Commandes intégrées

DOFLinx générera automatiquement les commandes suivantes :

- Lors du démarrage de DOFLinx :
  - `http://<hôte:port>/v1/version` - pour vérifier que DOF2DMD est en marche. DOFLinx essaiera de le démarrer sinon.
  - `http://<hôte:port>/v1/display/picture?path=mame/DOFLinx` - pour afficher l'image d'accueil DOFLinx
- Lors du lancement d'un jeu :
  - `http://<hôte:port>/v1/display/picture?path=mame/<nom-rom>&duration=<durée>&animation=<animation>` - pour afficher un PNG pour la bannière
- Lors de la partie :
  - `http://<hôte:port>/v1/display/score?player=<joueur actif>&score=<score>&cleanbg=<true|false>` - pour afficher le score du joueur donné
  - `http://<hôte:port>/v1/display/score?players=<nombre de joueurs>&player=<joueur actif>&score=<score>&cleanbg=<true|false>&credits=<crédits>` - pour afficher le score du joueur donné en indiquant la disposition du tableau de scores en fonction du nombre de joueurs
- Lors de la fermeture de DOFLinx :
  - `http://<hôte:port>/v1/display/score?player=1&score=0` - réinitialiser le score à 0
  - `http://<hôte:port>/v1/blank` - pour effacer le DMD (écran noir)
  - `http://<hôte:port>/v1/exit` - pour demander à DOF2DMD de se fermer proprement

### Syntaxe de la commande `FF_DMD` de DOFLinx

Pour ajouter des effets comme l'affichage d'animations ou de texte pendant le jeu, vous devez insérer la commande `FF_DMD` dans le fichier `<rom>.MAME` qui correspond au jeu.

```ascii
FF_DMD,U,<APPEL API DOF2DMD sans hôte ni préfixe /v1/>
```

- `FF_DMD` est la commande
- `U` est pour une commande utilisateur (spécifique à DOFLinx)
- Ensuite, l'URI pour appeler DOF2DMD sans l'hôte ni le préfixe /v1/

Exemples :

- Afficher l'animation de bonus en jeu `artwork/ingame/bonus.gif` : `FF_DMD,U,display/picture?path=ingame/bonus&duration=0&animation=none`
- Afficher une image statique `artwork/mame/pacman.png` : `FF_DMD,U,display/picture?path=mame/pacman&duration=-1`
- Afficher un Gif animé s'il existe ou revenir au png : `artwork/mame/pacman.gif` : `FF_DMD,U,display/picture?path=mame/pacman&duration=-1`

Consultez les fichiers `.MAME` inclus dans DOFLinx, qui contiennent déjà des commandes `FF_DMD`.

## Tests

Une fois DOF2DMD démarré, vous pouvez utiliser votre navigateur pour le tester :

- Afficher la version [http://127.0.0.1:8080/v1/version](http://127.0.0.1:8080/v1/version) 
- Afficher une image dans le dossier artwork, sous-dossier `mame`, image `galaga` : [http://127.0.0.1:8080/v1/display/picture?path=mame/galaga&duration=-1&animation=fade](http://127.0.0.1:8080/v1/display/picture?path=mame/galaga&duration=-1&animation=fade) 
- Définir le score du joueur 1 (par défaut) à 1000 en utilisant la disposition par défaut pour 4 joueurs et en nettoyant la scène actuelle : [http://127.0.0.1:8080/v1/display/score?score=1000](http://127.0.0.1:8080/v1/display/score?score=1000)
- Définir le score du joueur 2 à 3998, crédits à 5 en utilisant la disposition pour 2 joueurs sur la scène actuelle : [http://127.0.0.1:8080/v1/display/score?players=2&player=2&score=3998&cleanbg=false&credits=5](http://127.0.0.1:8080/v1/display/score?players=4&player=2&score=3998&cleanbg=false&credits=5)
- Définir le joueur actif sur le joueur 2 et définir le score à 2000 en utilisant la disposition pour 2 joueurs et en nettoyant la scène actuelle : [http://127.0.0.1:8080/v1/display/score?players=2&activeplayer=2&score=2000](http://127.0.0.1:8080/v1/display/score?players=2&activeplayer=2&score=2000)
- Afficher du texte en utilisant la taille M avec la police Back To the Future, couleur de police orange, couleur de bordure rouge, et une animation de défilement vers la droite pendant 10 secondes : [http://127.0.0.1:8080/v1/display/text?text=HELLO|friends&font=BTTF&size=M&color=FFA500&bordersize=1&bordercolor=FF0000&cleanbg=true&animation=scrollright&duration=10](http://127.0.0.1:8080/v1/display/text?text=HELLO|friends&font=BTTF&size=M&color=FFA500&bordersize=1&bordercolor=FF0000&cleanbg=true&animation=scrollright&duration=10)
- Afficher du texte avec une image de fond en utilisant la police White Rabbit en blanc et bordure bleue avec une animation de fondu en entrée et un défilement vers la droite en sortie et en attendant 10 secondes entre les animations : [http://127.0.0.1:8080/v1/display/advanced?path=mame/DOFLinx&text=Hello%20Friends!!&font=WhiteRabbit&size=M&color=0000ff&bordersize=1&bordercolor=ffffFF&cleanbg=true&animationin=FadeIn&animationout=ScrollOffRight&duration=10](http://127.0.0.1:8080/v1/display/advanced?path=mame/DOFLinx&text=Hello%20Friends!!&font=WhiteRabbit&size=M&color=0000ff&bordersize=1&bordercolor=ffffFF&cleanbg=true&animationin=FadeIn&animationout=ScrollOffRight&duration=10)
- Effacer le DMD [http://127.0.0.1:8080/v1/blank](http://127.0.0.1:8080/v1/blank)
- Quitter DOF2DMD [http://127.0.0.1:8080/v1/exit](http://127.0.0.1:8080/v1/exit)

ou utilisez les scripts PowerShell [`demo.ps1`](/DOF2DMD/demo.ps1) et [`demo2.ps1`](/DOF2DMD/demo2.ps1).

## TODO

Voici ce que je prévois d'implémenter :

- Des appels d'API qui ne sont pas encore implémentés
- Tout ce qui manque dans le fichier `settings.ini`
- Un plugin pour [Launch box / big box](http://pluginapi.launchbox-app.com/) qui
  s'interface avec DOF2DMD pour afficher les systèmes et les bannières des jeux lors de la navigation
  (partiellement implémenté)
- Un plugin pour [Attract-Mode](https://attractmode.org/) qui
  s'interface avec DOF2DMD pour afficher les systèmes et les bannières des jeux lors de la navigation


## 💬 Questions et support

Je compte sur la communauté Pinball et Arcade pour s'entraider via les [discussions GitHub](https://github.com/ojacques/DOF2DMD/discussions).
Je serai également présent.

## Merci

Merci à

- [@ojacques](https://github.com/ojacques) pour avoir créé la première version de ce projet
- DDH69 pour DOFLinx, MAME pour DOFLinx, et son soutien dans ce projet. Pensez à
  [💲faire un don à DDH69](https://www.paypal.com/donate?hosted_button_id=YEPCTUYFX5KDE) pour soutenir son travail.
- L'équipe [Pixelcade](https://pixelcade.org/) qui m'a inspiré à implémenter
  quelque chose pour mon ZeDMD, y compris le support d'autres DMD. Veuillez les vérifier,
  on m'a dit que leurs DMDs sont de premier ordre, de multiples tailles, et si vous en possédez un,
  il y a une tonne d'œuvres disponibles.
- Le créateur de ZeDMD -
  [Zedrummer](https://www.pincabpassion.net/t14798-tuto-installation-du-zedmd),
  qui est un DMD sympa et bon marché. Vous pouvez acheter ZeDMD dans plusieurs endroits.
- Tous ceux de [Monte Ton Cab (FR)](https://montetoncab.fr/) - quelle communauté accueillante !
