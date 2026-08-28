# Augen im Dunkeln — 2D-Stealth-Spiel (C# / Windows Forms)

Ein vollständiges 2D-Stealth-Spiel, in dem der **Mauszeiger selbst die Spielfigur** ist.
Bewege dich durch prozedural generierte, aber immer lösbare Level, weiche den
beobachtenden Augen aus und erreiche den leuchtenden Ausgang.

## Voraussetzungen

* Windows
* [.NET 8 SDK](https://dotnet.microsoft.com/download) (Windows Forms ist Windows-only,
  daher lässt sich das Spiel nicht unter Linux/macOS ausführen — nur bauen für Windows)

## Starten

```bash
cd StealthEyeGame
dotnet run
```

Oder in Visual Studio: `StealthEyeGame.csproj` öffnen und mit F5 starten.

Ein Release-Build erzeugt eine eigenständige `.exe`:

```bash
dotnet build -c Release
```
Die ausführbare Datei liegt danach unter `bin/Release/net8.0-windows/`.

## Steuerung

* **Maus bewegen** → Spielfigur (leuchtender Punkt) bewegt sich dorthin, begrenzt
  durch Geschwindigkeit und Wände.
* **Mausklick** → nur relevant auf dem Game-Over-Bildschirm, um den "Neustart"-Button
  zu treffen. Keine Tastatur wird für die Bewegung benötigt.

## Projektstruktur

```
Core/            GameConstants, MathUtil, GameManager (zentrale Spiellogik/State Machine)
Entities/        Player, Eye (Augen-Gegner)
Levels/          Level (Raster + Kollision/Sichtlinie), LevelGenerator, DifficultySettings
Rendering/       Renderer (reine Zeichenlogik, GDI+)
MainForm.cs      WinForms-Fenster, Game-Loop-Timer, Mauseingabe
Program.cs       Einstiegspunkt
```

Spiellogik, Levelgenerierung, Entities und Rendering sind bewusst in getrennte
Namespaces/Ordner aufgeteilt und kommunizieren nur über einfache Datentypen bzw.
den `GameManager` — es gibt keine WinForms-Abhängigkeiten außerhalb von
`MainForm.cs` und `Program.cs`.

## Spielregeln — kurz

* Jedes Auge durchläuft einen Zustandsautomaten: **Idle → Investigation/Alert →
  Searching → Returning → Idle** (siehe `Entities/EyeState.cs` und `Entities/Eye.cs`).
* Im Idle-Modus pendelt die Pupille organisch zwischen zufälligen Blickrichtungen,
  mit unterschiedlich langen Haltezeiten und gelegentlicher Rückkehr zur Mitte.
* Ein Auge erkennt dich, wenn du **in Reichweite**, **im Sichtwinkel** und
  **nicht durch eine Wand verdeckt** bist (echtes Raycasting, keine Näherung) —
  das gilt in jedem Zustand, das Auge kennt deine Position nie ohne echten Sichtkontakt.
* Verlierst du die Sicht, merkt sich das Auge nur die **letzte bekannte Position**
  und läuft dorthin (sichtbar, mit Kollision — kein Teleportieren). Dort sucht es
  3–5 Sekunden lang mit einem organischen Umschau-Muster, bevor es zu seinem
  ursprünglichen Standort zurückkehrt.
* **Explosionen** (durch Dynamit) lösen ein Geräusch-Event aus: Augen in der Nähe
  wechseln in den Investigation-Modus und laufen zur Explosionsposition, auch wenn
  sie dich nie gesehen haben. Augen im direkten Explosionsradius nehmen Schaden
  und können zerstört werden (Belohnung: Coins).
* **Dynamit** wird im Shop gekauft, im Spiel per Mausklick platziert (Button in
  der Top-Leiste aktiviert den Platzier-Modus) und explodiert nach 3 Sekunden
  Zündzeit automatisch. Es zerstört **angeknackste Wände** im Radius — normale
  Wände bleiben unversehrt. Zerstörte Wände blockieren weder Bewegung noch Sicht.
* **Coins** werden für Levelabschluss (+25) und zerstörte Augen (+10) vergeben
  und bleiben über den Tod hinweg erhalten (`Systems/PersistentProgress.cs`).
* Nach Game Over öffnet sich auf Wunsch ein **Shop** (Dynamit, mehr HP, stärkeres
  Dynamit) — komplett mausbedienbar. Gekaufte Items und Coins bleiben für den
  nächsten Run erhalten.
* Jedes Level ist garantiert lösbar — ein Korridor zwischen Start und Ausgang
  wird beim Generieren immer freigeschnitten, unabhängig vom Zufallsraster und
  unabhängig davon, ob angeknackste Wände gesprengt werden.
* Mit jedem Level steigen Augenanzahl, Sichtreichweite, Sichtwinkel, Schaden
  und Wanddichte leicht an (siehe `Levels/DifficultySettings.cs`).
* 0 HP → Game Over → Shop oder direkter Neustart bei Level 1 (Coins/Items bleiben).

## Steuerung

* **Maus bewegen** → Spielfigur bewegt sich dorthin.
* **Dynamit-Button** (Top-Leiste) klicken → Platzier-Modus an/aus.
* **Klick im Spielfeld** (bei aktivem Platzier-Modus) → Dynamit dort platzieren.
* **Klicks auf Game-Over-/Shop-Buttons** → Shop öffnen, kaufen, neuen Run starten.

## Projektstruktur

```
Core/            GameConstants, MathUtil, NoiseEvent, GameManager (State Machine)
Entities/        Player, Eye, EyeState, Dynamite, Explosion
Levels/          Level, WallType, LevelGenerator, DifficultySettings
Systems/         PersistentProgress, ShopItem, ShopItemType, ShopCatalog
Rendering/       Renderer (reine Zeichenlogik, GDI+)
MainForm.cs      WinForms-Fenster, Game-Loop-Timer, Mauseingabe/Klicks
Program.cs       Einstiegspunkt
```

## Bekannte Einschränkungen dieser Erweiterung

* Die Eye-KI nutzt für die Bewegung dieselbe einfache Zielverfolgung mit
  Wandkollision wie der Spieler (kein A*/Pathfinding) — für die Levelgrößen
  dieses Spiels ausreichend, kann aber in sehr verwinkelten Layouts gelegentlich
  an einer Wand "kleben bleiben", statt sie zu umgehen.
* Nur 3 Shop-Items sind aktuell implementiert (Dynamit, Mehr HP, Stärkeres
  Dynamit). Die Architektur (`ShopItemType`, `ShopItem`, `ShopCatalog`) ist
  bewusst so gebaut, dass weitere Items (Medkit, Unsichtbarkeit, Noise Maker,
  EMP, Smoke Bomb) ohne Änderungen am Shop-System selbst ergänzt werden können.

## Bekannte Designentscheidungen

* Rasterbasierte Level (30×20 Zellen, 32px) — Kollision und Sichtlinienprüfung
  laufen direkt auf dem Wand-Raster, nicht über Rechteck-Listen, das ist
  performant genug für viele Augen gleichzeitig.
* Der Sichtkegel wird pro Frame per Raycasting an den Wänden "abgeschnitten"
  (kein einfaches Dreieck) — dadurch stimmt die visuelle Darstellung exakt mit
  der tatsächlichen Erkennungslogik überein.
