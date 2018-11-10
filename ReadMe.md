# Empyrion ModLoader/Client/Host

## Ziel
Entkoppeln der MOD.DLLs um diese:
- im laufenden Spiel starten/stoppen zu können.
- Mods hinzuzufügen/deaktivieren zu können
- Mods mit >= .NET 4.6 schreiben zu können
- Mods dürfen aus mehreren DLLs bestehen
- Mods können einfach per Debugger inspiziert werden.

## Konfiguration
* ModLoader: "DllNames.txt" mit dem Pfad zur ClientDLL
* Client: "Configuration.xml" Konfiguration des Hostprozesses, der Pipenamen und des Start/Stopverhaltens
	* Wenn eine "stop.txt" Datei in dem Verzeichnis existiert in dem auch die "Configuration.xml" liegt, wird der Host-Prozess 
	heruntergefahren und nicht (automatisch) erneut gestartet.
* Host: "DllNames.txt" Pfade zu den MOD.DLLs welche geladen werden sollen

# Bekannte Fehler
Das wieder Starten des Hostprozesses (durch das entfernen der "stop.txt" Datei), bei einem laufenden Empyrion, lässt diesen abstürzen ?!?!