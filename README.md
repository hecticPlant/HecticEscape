# HecticEscape v0.0.1-alpha

## Überblick

Dieses Tool hilft dabei, die tägliche Zeit am PC bewusster zu nutzen und Ablenkungen zu reduzieren. Es basiert auf einem einfachen Zyklus aus Freizeit und Pause, mit zusätzlicher Begrenzung für ausgewählte Apps. 

* **Freizeit**: Alle Apps und Websites sind uneingeschränkt verfügbar, solange ihr tägliches Zeitlimit nicht überschritten ist.
* **Pausezeit**: Ausgewählte Apps werden blockiert, unabhängig von ihrem täglichen Zeitlimit.
* **Tägliches Zeitlimit**: Bestimmte Apps (z. B. Browser, Spiele) haben ein individuelles tägliches Zeitbudget. Ist dieses aufgebraucht, wird die App automatisch blockiert – unabhängig vom aktuellen Modus.

## Für Tester

### Installation

1. Den Installer ausführen (durch Doppelklick).
2. Einfach "Weiter" drücken, bis die Installation abgeschlossen ist.
3. Beim ersten Start wird gefragt, ob ein Zertifikat installiert werden darf. Das ist derzeit noch nicht notwendig, wird aber bei jedem Start erneut gefragt, solange es nicht akzeptiert wurde. Am besten akzeptieren – es passiert nichts Schädliches.

### Testen

Für den Test sollte der **Debug-Modus** aktiviert sein. Bitte **nicht den "Verbose"-Modus** aktivieren, da dieser sehr viele (oft irrelevante) Zeilen erzeugt, was das Auffinden von Problemen erschwert. In der Statusleiste sollte **"Debug an" (in Rot)** stehen, und **"Verbose"** sollte **nicht** angezeigt werden.

Wenn dir etwas auffällt, das komisch wirkt – sei es Rechtschreibfehler, umständliche Bedienung oder anderes –, sag mir bitte Bescheid. Am besten beschreibst du, **wann** es passiert ist, **was** passiert ist, und schickst mir den **"Log"-Ordner**. Wie du diesen findest, steht im nächsten Abschnitt.

### Den "Log"-Ordner finden

Der Ordner befindet sich im appdata-Ordner (Wie bei Minecraft)
1. **Windows-Taste + R** gleichzeitig drücken.

   * Alternativ in der Windows-Suchleiste **"Ausführen"** eingeben und anklicken.
2. Im Fenster **`%appdata%`** eingeben und mit Enter bestätigen.
3. Es öffnet sich ein Ordner. Dort den Ordner **"HecticEscape"** suchen.
4. Darin befindet sich der Ordner **"Log"**, den ich benötige.
5. Bitte den kompletten Ordner oder die Datei mit passendem Datum und Uhrzeit schicken (z. B. `app_2025-06-06_17-37-17.log` für den Start am 6.6.25 um 17:37 Uhr) – am besten über Discord.

## Bedienung

### Timer-Tab

Hier werden die Zeiten für Freizeit und Pausen festgelegt.

1. Timer im Dropdown auswählen. Die Zeit des jeweiligen Timers wird daneben angezeigt.

   > \[!WARNING]
   > **TimerCheck** wird im Debug-Modus gelistet und sollte **nicht verändert** werden.
2. Neue Zeit neben dem „Zeit setzen“-Knopf eingeben und auf **„Zeit setzen“** klicken.
3. **„Start Timer“** und **„Stop Timer“** sind Debug-Knöpfe und sollten nicht verwendet werden.

### Prozesse

Hier werden Programme und ihre Nutzungszeiten verwaltet.

> \[!IMPORTANT]
> Der **Prozesse-Tab** kann nur geöffnet werden, wenn **„App-Blocking aktiv“** im **„Steuerung“-Tab** aktiviert ist.
> App-Blocken ist nur aktiv, wenn der Freizeit/Pause-Ablauf läuft (siehe Abschnitt **Stuereung**).

#### Programm hinzufügen

1. Das gewünschte Programm muss bereits laufen.
2. **„Laufende Prozesse“** klicken.
3. Alle laufenden Prozesse werden im Kasten darunter angezeigt.
4. Gewünschten Prozess auswählen.
5. **„Speichere Prozess“** klicken.
6. Weitere Programme können hinzugefügt oder über **„Blockierte Apps anzeigen“** verwaltet werden.

#### Tägliche Zeit setzen

1. **„Blockierte Apps anzeigen“** klicken.
2. Programm auswählen.
3. Gewünschte Zeit eingeben.
4. **„Speichere tägliche Zeit“** klicken.
5. Die neue Zeit wird bei „Heute verbleiben:“ angezeigt.

### Gruppen

Es können Gruppen (Presets) für Apps erstellt werden. Nur Programme aus **aktiven Gruppen** werden blockiert.

#### Gruppen aktivieren/deaktivieren

1. Gruppe auswählen.
2. **„Gruppe aktivieren“** bzw. **„Gruppe deaktivieren“** klicken.

### Steuerung

> \[!TIP]
> Das Overlay lässt sich unter „Debug“ an- oder ausschalten.

Steuerelemente und Einstellungen:

* **„Start“**: Startet den Freizeit-/Pausen-Ablauf.
* **„App beim Windows-Start ausführen“**: Startet die App automatisch mit Windows.
* **„Timer“-Knöpfe**: Nur für Debug-Zwecke – bitte nicht verwenden.
* **„Timer beim Start automatisch starten“**: Startet den Ablauf automatisch beim Programmstart.
* **„Timer im Overlay anzeigen“**: Zeigt verbleibende Zeit der aktuellen Phase oben links im Bildschirm.
* **„App-Blocking aktiv“**: Aktiviert das Blockieren und schaltet den Prozesse-Tab frei.
* **„Web“-Knöpfe**: In dieser Version noch nicht relevant.
* **„Sprache“**: Sprache auswählen und mit **„Sprache wechseln“** bestätigen. Programm muss neu gestartet werden.
* **Debug-Knöpfe**: Nur für Entwickler/Testzwecke.
* **„Check for Updates“**: Sucht beim Start nach Updates.

  > \[!WARNING]
  > Diese Funktion führt derzeit zu einer Warnmeldung, da der Update-Server noch offline ist. Am besten deaktivieren.

### Programm schließen

Wenn das Programm über das „X“ oben rechts im Fenster geschlossen wird, wird es nicht beendet, sondern in den System-Tray (das Symbolfeld unten rechts in der Windows-Taskleiste, hinter dem kleinen Pfeil) minimiert.

1. Um das Programm vollständig zu beenden:
2. Im System-Tray auf den kleinen Pfeil klicken, um alle Symbole anzuzeigen.
3. Mit der rechten Maustaste auf das grüne Symbol mit weißem "H" klicken.
4. Im Kontextmenü „Beenden“ auswählen.





