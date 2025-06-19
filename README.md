# HecticEscape v0.1.3-alpha

## Überblick

Dieses Tool hilft dabei, die tägliche Zeit am PC bewusster zu nutzen und Ablenkungen zu reduzieren. Es basiert auf einem einfachen Zyklus aus Freizeit und Pause, mit zusätzlicher Begrenzung für ausgewählte Programme.

- **Freizeit**: Alle Programme und Websites sind uneingeschränkt verfügbar, solange das tägliche Zeitlimit nicht überschritten ist.
- **Pausezeit**: Ausgewählte Programme werden blockiert, unabhängig vom täglichen Zeitlimit.
- **Tägliches Zeitlimit**: Bestimmte Programme (z. B. Browser, Spiele) haben ein individuelles tägliches Zeitbudget. Ist dieses aufgebraucht, wird das Programm automatisch blockiert – unabhängig vom aktuellen Modus.

### Installation

1. Den Installer ausführen (durch Doppelklick).  
2. Einfach "Weiter" drücken, bis die Installation abgeschlossen ist.  
3. Beim ersten Start wird gefragt, ob ein Zertifikat installiert werden darf. Das ist derzeit noch nicht notwendig, wird aber bei jedem Start erneut abgefragt, solange es nicht akzeptiert wurde. Am besten akzeptieren – es passiert nichts Schädliches.

### Testen

- Bitte aktiviere "Suche nach Updates".  
- Für den Test sollte der **Debug-Modus** aktiviert sein. Bitte **nicht den "Verbose"-Modus** aktivieren, da dieser sehr viele (oft irrelevante) Zeilen erzeugt, was das Auffinden von Problemen erschwert. In der Statusleiste sollte **"Debug an"** (in Rot) stehen, und **"Verbose"** sollte **nicht** angezeigt werden.  
- Wenn dir etwas auffällt, das ungewöhnlich wirkt – sei es Rechtschreibfehler, umständliche Bedienung oder anderes – sag bitte Bescheid. Beschreibe dabei möglichst konkret: **Wann** es passiert ist, **was** passiert ist, und sende den **Log-Ordner**. Wie du diesen findest, steht im nächsten Abschnitt.

### Den "Log"-Ordner finden

Der Ordner befindet sich im AppData-Ordner (wie bei Minecraft):

1. **Windows-Taste + R** gleichzeitig drücken.  
   - Alternativ in der Windows-Suchleiste "Ausführen" eingeben und anklicken.  
2. Im Fenster `%appdata%` eingeben und mit Enter bestätigen.  
3. Es öffnet sich ein Ordner. Dort den Ordner **HecticEscape** suchen.  
4. Darin befindet sich der Ordner **Log**, den benötigt wird.  
5. Bitte den kompletten Ordner oder die Datei mit passendem Datum und Uhrzeit schicken (z. B. `app_2025-06-06_17-37-17.log` für den Start am 6. Juni 2025 um 17:37 Uhr) – am besten über Discord.

---

## Bedienung

### Timer-Tab

Hier werden die Zeiten für Freizeit und Pausen festgelegt.

1. Timer im Dropdown auswählen. Die Zeit des jeweiligen Timers wird daneben angezeigt.  
   > **Warnung:** Der Timer "TimerCheck" wird im Debug-Modus gelistet und sollte nicht verändert werden.  
2. Neue Zeit neben dem "Zeit setzen"-Knopf eingeben und auf **"Zeit setzen"** klicken.  
3. **"Start Timer"** und **"Stop Timer"** sind Debug-Knöpfe und sollten nicht verwendet werden.

### Programme

Hier werden Programme der aktuellen Gruppe und ihre Nutzungszeiten verwaltet. Die aktuelle Gruppe wird unter **Gruppen** ausgewählt.

> **Wichtig:**  
> Der Prozesse-Tab kann nur geöffnet werden, wenn **„App-Blocking aktiv“** im Steuerung-Tab aktiviert ist.  
> App-Blocking tritt nur in Kraft, wenn der Freizeit-/Pause-Ablauf läuft (siehe Abschnitt Steuerung).

#### Programm hinzufügen

1. Das gewünschte Programm muss bereits laufen.  
2. Auf **"Laufende Prozesse"** klicken.  
3. Alle laufenden Programme werden im Kasten darunter angezeigt.
  > **Wichtig:**  'Wenn Nur Prozesse mit Fenster anzeigen' nicht aktiviert ist, wird hier sehr viel angezeigt.
5. Gewünschtes Programm auswählen.  
6. Auf **"Speichere Prozess"** klicken.  
7. Weitere Programme können hinzugefügt oder über **"Blockierte Programme anzeigen"** verwaltet werden.

#### Programm hinzufügen
1. **'Blockierte Apps anzeigen'** klicken.
2. Das gewünschte Programm auswählen.
3. **'Lösche Prozess'** klicken.

### Installierte Spiele finden

- Diese Funktion sucht nach installierten Spielen und fügt sie der aktuellen Gruppe hinzu.

### Nur Programme mit Fenster anzeigen

- Wenn aktiviert, werden bei "Laufende Programme" nur Programme mit einem Fenster angezeigt. So ist es einfacher, das gesuchte Programm zu finden.

#### Tägliche Zeit setzen

1. Auf **"Blockierte Programme anzeigen"** klicken.  
2. Programm auswählen.  
3. Gewünschte Zeit eingeben.  
4. Auf **"Tägliche Zeit speichern"** klicken.  
5. Die neue Zeit wird bei **"Heute verbleiben:"** angezeigt.

### Gruppenverwaltung

Mit der Gruppenfunktion können mehrere Programme zu einer Gruppe zusammengefasst und gemeinsam blockiert oder freigegeben werden. Nur Programme aus **aktiven Gruppen** werden im Blockierungsprozess berücksichtigt.
Es wird die Gruppe bearbeitet, die unter **Gruppen-Verwaltung** ausgweählt ist.

#### Eine Gruppe erstellen

1. Im Hauptfenster zum Tab **"Programme"** wechseln (nur verfügbar, wenn "Programm-Blocking aktiv" eingeschaltet und der Ablauf gestartet wurde).  
2. Auf **"Neue Gruppe erstellen"** klicken.  
3. Programme hinzufügen:  
   - Sicherstellen, dass das gewünschte Programm bereits läuft.  
   - Auf **"Laufende Programme"** klicken, ein Programm auswählen und auf **"Zu Gruppe hinzufügen"** klicken.  
   - Vorgang für alle gewünschten Programme wiederholen.

### Gruppen-Blockierung

Wenn die Gruppen-Blockierung aktiviert ist, können die Programme in dieser Gruppe nur gestartet werden, solange die Gruppe noch Zeit übrig hat.

#### Eine Gruppe aktivieren / deaktivieren

1. Im Gruppen-Tab die gewünschte Gruppe auswählen.  
2. Auf **"Gruppe aktivieren"** klicken, um alle darin enthaltenen Programme in der nächsten Blockierungsphase zu sperren.  
3. Auf **"Gruppe deaktivieren"** klicken, um sie aus dem Blockierungsprozess zu entfernen.  
4. Im Overlay und in den Logs wird unter **"Aktive Gruppe"** der Name angezeigt.

> **Hinweis zur Priorität:**  
> - Wenn ein Programm in mehreren aktiven Gruppen ist, wird es nur einmal blockiert.  
> - Individuelle Zeitlimits gelten weiterhin: Wenn das Tageslimit bereits abgelaufen ist, wird das Programm unabhängig vom Gruppenstatus blockiert. Umgekehrt kann ein Programm in einer Gruppe blockiert sein, obwohl das Tageslimit noch nicht erreicht wurde.

### Steuerung

> **Tipp:**  
> Das Overlay lässt sich unter "Debug" an- oder ausschalten.

Verfügbare Steuerelemente und Einstellungen:

- **"Start"**: Startet den Freizeit-/Pausen-Ablauf.  
- **"App beim Windows-Start ausführen"**: Startet das Programm automatisch mit Windows.  
- **"Timer"-Knöpfe**: Nur für Debug-Zwecke – bitte nicht verwenden.  
- **"Timer beim Start automatisch starten"**: Startet den Ablauf automatisch beim Programmstart.  
- **"Timer im Overlay anzeigen"**: Zeigt die verbleibende Zeit der aktuellen Phase oben links im Bildschirm an.  
- **"AppTimer im Overlay anzeigen"**: Wenn einem Programm weniger als eine Stunde übrig bleibt, erscheint ein roter Timer, der die verbleibende Zeit anzeigt.  
- **"App-Blocking aktiv"**: Aktiviert das Blockieren und schaltet den Programme-Tab frei.  
- **"Web"-Knöpfe**: In dieser Version noch nicht relevant.  
- **"Sprache"**: Sprache auswählen und mit **"Sprache wechseln"** bestätigen. Das Programm muss neu gestartet werden.  
- **Debug-Knöpfe**: Nur für Entwickler-/Testzwecke.  
- **"Check for Updates"**: Sucht beim Start nach Updates.

### Programm schließen

Wenn das Programm über das "X" oben rechts im Fenster geschlossen wird, wird es nicht beendet, sondern in den System-Tray (Symbolfeld unten rechts in der Windows-Taskleiste, hinter dem kleinen Pfeil) minimiert.

1. Um das Programm vollständig zu beenden:  
   1. Im System-Tray auf den kleinen Pfeil klicken, um alle Symbole anzuzeigen.  
   2. Mit der rechten Maustaste auf das grüne Symbol mit weißem "H" klicken.  
   3. Im Kontextmenü **"Beenden"** auswählen.
