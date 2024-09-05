///////////////////////////////////////////////////
//
// Attract-Mode Frontend - DMD Player for ZeDMD plugin v1.0
//  PapaGustavoKratos
///////////////////////////////////////////////////

///////////////////////////////////////////////////
// Define use configurable settings
//
class UserConfig </ help="Attract-Mode plug-in (v1.0) for use with DMD Led RGB Matrix through FlexDMD" /> {

	</ label="Game Info", help="Enable show game info when slected over the game marquee", order=1, options="Yes,No" />
	game_info="Yes";
	</ label="External DOF2DMD server", help="Select if DOF2DMD server is launched outside Attract-Mode otherwise run it from plugin folder", order=2, options="Yes,No" />
	external_dof2dmd="No";
	</ label="DOF2DMD server path", help="Select DOF2DMD server path if not running from DOFLinx or similar", order=3, options="Yes,No" />
	path_dof2dmd = fe.script_dir + "dof2dmd.exe"; 
}

class DMDPlayer
{
	command_server = config["path_dof2dmd"];
	url = "http://127.0.0.1:8080/v1/"; 
	command_dmdplay = "curl --url "
	config = null;
	last_transition = "none";
	debug_mode = true;
	printprefix = ">>>>>> ";

	constructor()
	{
		config = fe.get_config();
		fe.add_transition_callback( this, "on_transition" );	
		
		//We'll use a persistent value in the fe.nv table to determine if this is the first time the plugin is loaded.
		//Values in fe.nv are persisted in the script.nv file.
		//Note: Attract-Mode seems to reload each plugin multiple times, 
		//twice at startup and each time the Displays list is active. Not sure why?
		if (fe.nv.rawin("DMDPlayerLastTransision")) { //If global exists then this is not the first time we've loaded the plugin.
			last_transition = fe.nv["DMDPlayerLastTransision"]; //Get last transition from global.
		}
		else { //First Run
			fe.nv["DMDPlayerLastTransision"] <- last_transition; //Add persistent global to store last transition. Global will be deleted when FE quits.
		
			//Load DMDPlayer
			if ( debug_mode ) print( printprefix + "EVENT_FE_START" + "\n" );
			if(config["external_dof2dmd"] == "No")
			{
				fe.plugin_command_bg( command_dmdplay , "\"" + url + "exit\"");
				fe.plugin_command_bg( command_server , "");
			}
			fe.plugin_command_bg( command_dmdplay , "\"" + url + "display/picture?path=./attract&duration=-1&animation=none\"");
				
		}
	}
	function getFilenameWithoutExtension(filename) 
	{
		local splitname = split(filename, ".");
		local nameWithoutExtension = "";

		for(local i = 0; i < splitname.len() - 1; i++) {
			nameWithoutExtension += splitname[i];
			if (i < splitname.len() - 2) {
				nameWithoutExtension += ".";
			}
		}

		return nameWithoutExtension;
	}
function urlEncodePath(path) {
// Crear una expresión regular para buscar caracteres especiales
    local re = regexp("[\\s!\"#$%&'()*+,./:;<=>?@[\\]^_`{|}~]");

    // Crear un diccionario de caracteres a reemplazar con sus secuencias de escape
    local replacements = {
        " ": "%20",
        "!": "%21",
        "\"": "%22",
        "#": "%23",
        "$": "%24",
        "%": "%25",
        "&": "%26",
        "'": "%27",
        "(": "%28",
        ")": "%29",
        "*": "%2A",
        "+": "%2B",
        ",": "%2C",
        "-": "%2D",
        ".": "%2E",
        "/": "%2F",
        ":": "%3A",
        ";": "%3B",
        "<": "%3C",
        "=": "%3D",
        ">": "%3E",
        "?": "%3F",
        "@": "%40",
        "[": "%5B",
        "\\": "%5C",
        "]": "%5D",
        "^": "%5E",
        "_": "%5F",
        "`": "%60",
        "{": "%7B",
        "|": "%7C",
        "}": "%7D",
        "~": "%7E"
    };

    // Inicializar una cadena para construir el resultado
    local encodedPath = "";
    local start = 0;

    while (true) {
        // Buscar la siguiente coincidencia de caracteres especiales
        local match = re.search(path, start);
        if (match == null) break; // No hay más coincidencias

        // Obtener el carácter especial encontrado
        local specialChar = path.slice(match.begin, match.end);

        // Agregar la parte de la cadena antes del carácter especial y su reemplazo
        encodedPath += path.slice(start, match.begin) + replacements[specialChar];
        start = match.end;
    }

    // Agregar el resto de la cadena después del último carácter especial
    encodedPath += path.slice(start);

    return encodedPath;
}


	
	function on_transition( ttype, var, ttime )
	{
		if ( debug_mode ) print( printprefix + "ttype: [" + ttype + "] var: [" + var + "] ttime: [" + ttime + "] rom: [" + fe.game_info( Info.Name ) + 
			"] emu: [" + fe.game_info( Info.Emulator ) + "] list: [" + fe.list.name + "] list index: [" + fe.list.display_index +
			"] last ttype: [" + last_transition + "]\n" );
			
		
			switch( ttype )
			{
			case Transition.ToGame:
				
				fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
				local splitmarquee = split(fe.get_art( "marquee" ), ".");
				local marqueeextension = splitmarquee[splitmarquee.len()-1];
				local marquefilename = getFilenameWithoutExtension(fe.get_art( "marquee" ));
				if ( debug_mode ) print( printprefix + "ToGame " + command_dmdplay  + "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"\n" );
				fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"");
				if (config["game_info"] == "Yes" && !(fe.list.name == "Displays Menu"))
				{
					if ( debug_mode ) print( printprefix + "ToGame " + command_dmdplay + "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"\n" );
					fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"");
				}	
				break;
				
			case Transition.ToNewSelection:
				if ( debug_mode ) print( printprefix + "ToNewSelection " + command_dmdplay  + "\"" + url + "blank\"\n");
				fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
					
				break;

			case Transition.FromGame:
				fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
				local splitmarquee = split(fe.get_art( "marquee" ), ".");
				local marqueeextension = splitmarquee[splitmarquee.len()-1];
				local marquefilename = getFilenameWithoutExtension(fe.get_art( "marquee" ));
				if ( debug_mode ) print( printprefix + "FromGame " + command_dmdplay  + "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"\n" );
				fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"");
				if (config["game_info"] == "Yes" && !(fe.list.name == "Displays Menu"))
				{
					if ( debug_mode ) print( printprefix + "FromGame " + command_dmdplay + "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"\n" );
					fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"");
				}
				break;

			case Transition.StartLayout:
				fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
				/*
				local splitmarquee = split(fe.get_art( "marquee" ), ".");
				local marqueeextension = splitmarquee[splitmarquee.len()-1];
				local marquefilename = getFilenameWithoutExtension(fe.get_art( "marquee" ));
				if ( debug_mode ) print( printprefix + "StartLayout " + command_dmdplay  + "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"\n" );
				fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"");
				if (config["game_info"] == "Yes" && !(fe.list.name == "Displays Menu"))
				{
					if ( debug_mode ) print( printprefix + "StartLayout " + command_dmdplay + "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"\n" );
					fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"");
				} */
				break;

			case Transition.HideOverlay:
				fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
				local splitmarquee = split(fe.get_art( "marquee" ), ".");
				local marqueeextension = splitmarquee[splitmarquee.len()-1];
				local marquefilename = getFilenameWithoutExtension(fe.get_art( "marquee" ));
				if ( debug_mode ) print( printprefix + "HideOverlay " + command_dmdplay  + "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"\n" );
				fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"");
				if (config["game_info"] == "Yes" && !(fe.list.name == "Displays Menu"))
				{
					if ( debug_mode ) print( printprefix + "HideOverlay " + command_dmdplay + "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"\n" );
					fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"");
				}
				break;

			case Transition.EndLayout:
				switch( var )
				{
				case FromTo.ScreenSaver: //Starting screensaver
					//aqui enviar comando de apagar la marquee	
					
					fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
					break;

				case FromTo.Frontend: //Ending FE
					if ( debug_mode ) print( printprefix + "EVENT_FE_QUIT" + "\n" );
					if (fe.nv.rawin("DMDPlayerLastTransision")) delete fe.nv[ "DMDPlayerLastTransision" ]; //Don't want to persist DMDPlayer global after quiting.
					//Enviar comando para matar al server
					
					if(config["external_dof2dmd"] == "No")
					{		
						
						fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\" & timeout 5 > NUL & curl --url \"http://127.0.0.1:8080/v1/exit\"");

					}
					else
					{
						fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
					}
			
					break;
				}
				break;
			case Transition.ShowOverlay:
				if ( debug_mode ) print( printprefix + "ShowOverlay " + var + "\n" );
				fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
				if( var == Overlay.Filters )
					{
						if ( debug_mode ) print( printprefix + "ShowOverlay Filter " + command_dmdplay + "\"" + url + "display/advanced?text=Filters%20Menu&size=L&color=FFFFFF&font=Matrix&bordercolor=000000&bordersize=0&cleanbg=true&animationout=ScrollOffUp&animationin=ScrollOnUp&duration=15\"\n" );
						fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/advanced?text=Filters%20Menu&size=L&color=FFFFFF&font=Matrix&bordercolor=000000&bordersize=0&cleanbg=true&animationout=ScrollOffUp&animationin=ScrollOnUp&duration=15\"");
					
					}
				if( var == Overlay.Exit )
					{
						if ( debug_mode ) print( printprefix + "ShowOverlay Exit " + command_dmdplay + "\"" + url + "display/advanced?text=Exit%20Menu&size=L&color=FFFFFF&font=Matrix&bordercolor=000000&bordersize=0&cleanbg=true&animationout=ScrollOffUp&animationin=ScrollOnUp&duration=15\"" );
						fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/advanced?text=Exit%20Menu&size=L&color=FFFFFF&font=Matrix&bordercolor=000000&bordersize=0&cleanbg=true&animationout=ScrollOffUp&animationin=ScrollOnUp&duration=15\"");
					}
				if( var == Overlay.Displays )
					{
						if ( debug_mode ) print( printprefix + "ShowOverlay Displays " + command_dmdplay + "\"" + url + "display/advanced?text=Displays%20Menu&size=L&color=FFFFFF&font=Matrix&bordercolor=000000&bordersize=0&cleanbg=true&animationout=ScrollOffUp&animationin=ScrollOnUp&duration=15\"" );
						fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/advanced?text=Displays%20Menu&size=L&color=FFFFFF&font=Matrix&bordercolor=000000&bordersize=0&cleanbg=true&animationout=ScrollOffUp&animationin=ScrollOnUp&duration=15\"");
					
					}
			
			break;
			
			case Transition.ToNewList:
				//fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
				local splitmarquee = split(fe.get_art( "marquee" ), ".");
				local marqueeextension = splitmarquee[splitmarquee.len()-1];
				local marquefilename = getFilenameWithoutExtension(fe.get_art( "marquee" ));
				if ( debug_mode ) print( printprefix + "ToNewList " + command_dmdplay  + "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"\n" );
				fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"");
				if (config["game_info"] == "Yes" && !(fe.list.name == "Displays Menu"))
				{
					if ( debug_mode ) print( printprefix + "ToNewList " + command_dmdplay + "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"\n" );
					fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"");
				}
				break;
				
			case Transition.FromOldSelection:
				//fe.plugin_command_bg( command_dmdplay , "\"" + url + "blank\"");
				local splitmarquee = split(fe.get_art( "marquee" ), ".");
				local marqueeextension = splitmarquee[splitmarquee.len()-1];
				local marquefilename = getFilenameWithoutExtension(fe.get_art( "marquee" ));
				if ( debug_mode ) print( printprefix + "FromOldSelection " + command_dmdplay  + "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"\n" );
				fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/picture?path=" + urlEncodePath(marquefilename) + "&duration=-1&animation=none\"");
				if (config["game_info"] == "Yes" && !(fe.list.name == "Displays Menu"))
				{
					if ( debug_mode ) print( printprefix + "FromOldSelection " + command_dmdplay + "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"\n" );
					fe.plugin_command_bg( command_dmdplay  , "\"" + url + "display/text?text=" + urlEncodePath(  fe.game_info(Info.Emulator) + " - " + fe.game_info(Info.Title)  + "|" + fe.game_info(Info.Year) + "  " + fe.game_info(Info.Manufacturer)) + "&size=S&color=FFFFFF&font=Consolas&bordercolor=000000&bordersize=1&cleanbg=false&animation=ScrollLeft&duration=20\"");
				}
				break;
			}
		
		//Retain last transition
		if ( last_transition != ttype + "," + var ) {
			last_transition = ttype + "," + var;
			if (fe.nv.rawin("DMDPlayerLastTransision")) fe.nv[ "DMDPlayerLastTransision" ] = last_transition; //Save transition in global.
		}
		
		return false;
	}
}

fe.plugin[ "DMDPlayer" ] <- DMDPlayer();
