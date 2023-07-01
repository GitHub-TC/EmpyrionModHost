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

## Nitrado (und ggf andere Gamehoster)
Gamehoster nutzen oft eine "Whitelist" der erlaubten Prozessnamen (z.B. EmpyrionPlayfieldServer.exe), daher musst man für diese den Hostprozess 
umbenennen.
Hierfür bitte die "...\ModLoader\Client\Configuration.xml" Datei wie folgt ändern
```
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <PathToModHost>..\Host\EmpyrionModHost.exe</PathToModHost>
  <AutostartModHost>true</AutostartModHost>
```
in
```
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <PathToModHost>..\Host\EmpyrionPlayfieldServer.exe</PathToModHost>
  <AutostartModHost>true</AutostartModHost>
```
und die Datei "...\ModLoader\Host\EmpyrionModHost.exe.bin" in "...\ModLoader\Host\EmpyrionPlayfieldServer.exe.bin"
umbenennen bzw falls die ".bin" Datei nicht existiert die Datei "...\ModLoader\Host\EmpyrionModHost.exe" in "...\ModLoader\Host\EmpyrionPlayfieldServer.exe.bin"


# English version

# Empyrion ModLoader / Client / Host

## Goal
Decoupling the MOD.DLLs by:
- to be able to start / stop in the current game.
- Add / disable mods
- be able to write mods with> = .NET 4.6
- Mods may consist of several DLLs
- Mods can be easily inspected by debugger.

## Configuration
* ModLoader: "DllNames.txt" with the path to the ClientDLL
* Client: "Configuration.xml" Configuration of the host process, the pipe names and the start / stop behavior
* If there is a "stop.txt" file in the directory in which also the "Configuration.xml" is located, the host process becomes
Shut down and not (automatically) restarted.
* Host: "DllNames.txt" paths to the MOD.DLLs which should be loaded

## Nitrado (and possibly other gamehosters)
Gamehosts often use a "whitelist" of allowed process names (e.g. EmpyrionPlayfieldServer.exe), so you have to rename the host process for them. 
rename the host process.
To do this, please change the "...\ModLoader\Client\Configuration.xml" file as follows
```
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <PathToModHost>..\Host\EmpyrionModHost.exe</PathToModHost>
  <AutostartModHost>true</autostartModHost>.
```
in
```
<Configuration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <PathToModHost>..\Host\EmpyrionPlayfieldServer.exe</PathToModHost>
  <AutostartModHost>true</autostartModHost>.
```
and rename the file "...\ModLoader\Host\EmpyrionModHost.exe.bin" to "...\ModLoader\Host\EmpyrionPlayfieldServer.exe.bin".
rename or if the ".bin" file does not exist rename the file "...\ModLoader\Host\EmpyrionModHost.exe" to "...\ModLoader\Host\EmpyrionPlayfieldServer.exe.bin".
