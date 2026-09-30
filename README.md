# Netxp:Verein unter CachyOS / Linux mit Wine

Stand: September 2026

Diese Anleitung dokumentiert eine funktionierende Einrichtung von **Netxp:Verein unter CachyOS/KDE** mit einem separaten klassischen 32-Bit-Wine-Prefix und einem Kompatibilitäts-Patch für `MSDN.HtmlEditorControl.dll`.

Der Patch behebt Probleme des alten MSHTML-basierten E-Mail-Editors unter Wine.

## Getesteter Funktionsumfang

Mit dem hier beschriebenen Patch wurden erfolgreich getestet:

-   Start von Netxp:Verein
-   Mitgliederverwaltung
-   Öffnen des Kommunikationsassistenten
-   Wechsel durch die Schritte des Kommunikationsassistenten
-   Eingabe eines Betreffs
-   direkte Texteingabe in den E-Mail-Body
-   HTML-Editor (`HTML bearbeiten`)
-   Übernahme von HTML-Inhalten
-   Anhänge
-   `Ausführen`

> **Hinweis:** Dies ist ein inoffizieller Kompatibilitäts-Patch und keine offizielle Lösung von Netxp. Vor Änderungen immer Backups anlegen.

## Repository-Dateien

``` text
.
├── README.md
├── FixWineInterface.cs
├── TestHtmlElement.cs
├── netxpverein
└── netxpverein.desktop
```

-   `FixWineInterface.cs` -- finaler Mono.Cecil-Patcher
-   `TestHtmlElement.cs` -- kleiner Test für `HtmlElement.SetAttribute()` unter Wine
-   `netxpverein` -- Startwrapper mit festem Wine/Prefix und Hash-Prüfung
-   `netxpverein.desktop` -- KDE-Startmenüeintrag

## 1. Zielstruktur

``` text
~/.local/opt/wine-netxp/
└── bin/
    ├── wine
    └── wineserver

~/.wine-netxp32/
└── drive_c/
    └── NetxpVerein/
        ├── SAMClient.exe
        ├── SAMLibrary.dll
        ├── MSDN.HtmlEditorControl.dll
        └── ...

~/.local/bin/
└── netxpverein

~/.local/share/applications/
└── netxpverein.desktop
```

Netxp wird **nicht mit dem systemweiten `/usr/bin/wine`** gestartet, sondern mit:

``` text
~/.local/opt/wine-netxp/bin/wine
```

Der Prefix ist:

``` text
~/.wine-netxp32
```

Netxp wird direkt über `SAMClient.exe` gestartet. Der Netxp-Starter sollte für diese gepatchte Installation nicht verwendet werden, da er Dateien aktualisieren bzw. ersetzen kann.

## 2. Voraussetzungen

Unter CachyOS / Arch:

``` fish
sudo pacman -S mono
```

Prüfen:

``` fish
command -v mono
command -v mcs
command -v monodis
command -v ilasm
```

Mono.Cecil suchen:

``` fish
find /usr/lib/mono -iname 'Mono.Cecil.dll' -print
```

Auf dem getesteten System wurde verwendet:

``` text
/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll
```

Falls dein Pfad abweicht, passe die `mcs`-Befehle entsprechend an.

## 3. Wine

Die funktionierende Installation verwendet einen klassischen reinen 32-Bit-Prefix:

``` text
~/.wine-netxp32
```

und ein separates Wine unter:

``` text
~/.local/opt/wine-netxp
```

Prüfen:

``` fish
~/.local/opt/wine-netxp/bin/wine --version
file ~/.local/opt/wine-netxp/bin/wine
```

### Aktuelles System-Wine / neuer WoW64-Modus

Ein aktuelles CachyOS-Wine kann im neuen WoW64-Modus gebaut sein. Ein solcher Build kann einen klassischen reinen `WINEARCH=win32`-Prefix ablehnen:

``` text
wine: WINEARCH is set to 'win32' but this is not supported in wow64 mode.
```

Deshalb wird für diese Installation ein separates, funktionierendes Wine fest im Launcher hinterlegt.

> Die genaue Erstellung des separaten klassischen Wine-Builds ist hier noch nicht reproduziert. Für eine Neuinstallation empfiehlt es sich deshalb, `~/.local/opt/wine-netxp` zu sichern.

## 4. Prefix

Mit einem geeigneten klassischen Wine kann ein 32-Bit-Prefix grundsätzlich so angelegt werden:

``` fish
env WINEPREFIX="$HOME/.wine-netxp32" WINEARCH=win32 \
    "$HOME/.local/opt/wine-netxp/bin/wineboot" -u
```

Netxp benötigt Microsoft .NET Framework. Auf der getesteten Installation meldete Netxp:

``` text
Runtime: 4.0.30319.42000
64-Bit-Prozess: False
```

> Die ursprüngliche .NET-Installation und alle Prefix-Abhängigkeiten sind in diesem Repository noch nicht vollständig dokumentiert. Für eine zuverlässige Wiederherstellung den kompletten funktionierenden Prefix `~/.wine-netxp32` sichern.

## 5. Netxp-Verzeichnis

Erwarteter Pfad:

``` text
~/.wine-netxp32/drive_c/NetxpVerein
```

Prüfen:

``` fish
cd ~/.wine-netxp32/drive_c/NetxpVerein

ls SAMClient.exe
ls SAMLibrary.dll
ls MSDN.HtmlEditorControl.dll
```

Direkter Start:

``` fish
env WINEPREFIX="$HOME/.wine-netxp32" WINEARCH=win32 \
    "$HOME/.local/opt/wine-netxp/bin/wine" SAMClient.exe
```

## 6. Ursache des Kommunikationsproblems

Netxp verwendet für den E-Mail-Editor `MSDN.HtmlEditorControl.dll`. Der Editor basiert auf alten IE/MSHTML-COM-Schnittstellen.

Der Originalcode erwartet unter anderem, dass `IHTMLDocument.body` auf `mshtml.HTMLBody` bzw. `mshtml.DispHTMLBody` gecastet werden kann.

Ein Runtime-Test unter Wine ergab:

``` text
mshtml.IHTMLElement   ✓ unterstützt
mshtml.DispHTMLBody   ✗ nicht unterstützt
mshtml.HTMLBody       ✗ nicht unterstützt
```

`IHTMLDocument.body` selbst wird erfolgreich als `IHTMLElement` geliefert. Der erzwungene `HTMLBody`-Cast schlägt anschließend fehl.

Zusätzlich wartet der Editor synchron:

``` text
loading = true
Navigate("about:blank")

while (loading)
{
    Application.DoEvents();
    Thread.Sleep(0);
}
```

Wirft der `DocumentCompleted`-Handler vor `loading = false` eine Exception, bleibt die Initialisierung hängen und der Kommunikationsdialog erscheint nicht.

Weitere problematische IE-spezifische Stellen waren u. a.:

``` text
DispHTMLBody.set_contentEditable(...)
DispHTMLBody.set_scroll(...)
DispHTMLBody.set_noWrap(...)
DispHTMLBody.getElementsByTagName(...)
```

sowie:

``` csharp
document.createElement("<Body></Body>");
```

Wine quittierte den letztgenannten Pfad mit `HRESULT E_FAIL`.

## 7. Portable Strategie des Patches

Der Patch verwendet soweit möglich `IHTMLElement`:

``` text
IHTMLDocument.body
    ↓
IHTMLElement
    ├── innerText
    ├── innerHTML
    └── outerHTML
```

Für `contentEditable` wird der WinForms-DOM-Wrapper verwendet:

``` csharp
editorWebBrowser.Document.Body.SetAttribute(
    "contentEditable",
    "true");
```

Dieser Pfad wurde separat unter Wine getestet und funktionierte:

``` text
DocumentCompleted
Document null: False
Body null: False
TagName: BODY
SetAttribute OK
contentEditable = true
InnerHtml = <b>Wine edit test</b>
Fertig.
```

`RebaseAnchorUrl()` sucht Links über das Dokument:

``` text
document.getElementsByTagName("A")
```

statt über `DispHTMLBody`.

`set_BodyHtml()` verwendet den bereits existierenden Body von
`about:blank` und setzt direkt dessen `innerHTML`, statt
`createElement("<Body></Body>")` aufzurufen.

## 8. Original-DLL sichern

Vor dem Patch:

``` fish
cd ~/.wine-netxp32/drive_c/NetxpVerein

cp -a \
    MSDN.HtmlEditorControl.dll \
    MSDN.HtmlEditorControl.dll.original
```

Hash:

``` fish
sha256sum MSDN.HtmlEditorControl.dll.original
```

Getestetes Original:

``` text
38232ceff70aa673a86c79b9ee4e5524c74724c975749ccff7d3a0018e1d11eb
```

**Wenn dieser Hash abweicht, den Patch nicht blind anwenden.** Eine neue Netxp-Version kann eine andere IL-Struktur besitzen.

## 9. Patcher installieren

`FixWineInterface.cs` aus diesem Repository kopieren:

``` fish
mkdir -p ~/.local/share/netxpverein-fix

cp FixWineInterface.cs \
    ~/.local/share/netxpverein-fix/FixWineInterface.cs
```

## 10. Patcher kompilieren

``` fish
cd ~/.wine-netxp32/drive_c/NetxpVerein

mcs \
    -r:System.Windows.Forms \
    -r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll \
    -out:/tmp/FixWineInterface.exe \
    ~/.local/share/netxpverein-fix/FixWineInterface.cs
```

Patch erzeugen:

``` fish
mono /tmp/FixWineInterface.exe
```

Erwartete Ausgabe enthält unter anderem:

``` text
body: mshtml.HTMLBody -> mshtml.IHTMLElement
Entferne HTMLBody-Cast in BrowserDocumentComplete
Entferne HTMLBody-Cast in set_BodyHtml
set_ReadOnly: contentEditable ueber WinForms HtmlElement.SetAttribute
set_ScrollBars: DispHTMLBody-Zugriff entfernt
set_AutoWordWrap: DispHTMLBody-Zugriff entfernt
set_BodyHtml: portable IHTMLElement-Version
get_BodyHtml: IHTMLElement.get_outerHTML
RebaseAnchorUrl: body.getElementsByTagName -> document.getElementsByTagName
Geschrieben: MSDN.HtmlEditorControl.dll.wineinterface2
```

## 11. Patch installieren

Netxp beenden:

``` fish
env WINEPREFIX="$HOME/.wine-netxp32" \
    "$HOME/.local/opt/wine-netxp/bin/wineserver" -k
```

Dann:

``` fish
cd ~/.wine-netxp32/drive_c/NetxpVerein

cp -f \
    MSDN.HtmlEditorControl.dll.wineinterface2 \
    MSDN.HtmlEditorControl.dll
```

Hashes prüfen:

``` fish
sha256sum \
    MSDN.HtmlEditorControl.dll \
    MSDN.HtmlEditorControl.dll.wineinterface2 \
    MSDN.HtmlEditorControl.dll.original
```

Getestete Version:

``` text
2721cfa6dd413427f01110bf403f77993f3bff53b63fa0762027aa8b0ac54052  MSDN.HtmlEditorControl.dll
2721cfa6dd413427f01110bf403f77993f3bff53b63fa0762027aa8b0ac54052  MSDN.HtmlEditorControl.dll.wineinterface2
38232ceff70aa673a86c79b9ee4e5524c74724c975749ccff7d3a0018e1d11eb  MSDN.HtmlEditorControl.dll.original
```

## 12. Patch optional kontrollieren

`monodis` extrahiert Ressourcen. Deshalb ein frisches temporäres Verzeichnis verwenden:

``` fish
rm -rf /tmp/netxp-check-interface
mkdir /tmp/netxp-check-interface
cd /tmp/netxp-check-interface

monodis \
    --output=patched.il \
    ~/.wine-netxp32/drive_c/NetxpVerein/MSDN.HtmlEditorControl.dll
```

Prüfen:

``` fish
grep -n -E \
    'field.*IHTMLElement body|field.*HTMLBody body|castclass mshtml.HTMLBody' \
    patched.il
```

Erwartet:

``` text
.field private class mshtml.IHTMLElement body
```

Es sollte kein `castclass mshtml.HTMLBody` mehr vorhanden sein.

## 13. Funktionstest

``` fish
cd ~/.wine-netxp32/drive_c/NetxpVerein

env WINEPREFIX="$HOME/.wine-netxp32" WINEARCH=win32 \
    "$HOME/.local/opt/wine-netxp/bin/wine" SAMClient.exe
```

Testablauf:

1.  `Mitglieder`
2.  `Mitgliederliste`
3.  `Kommunikation`
4.  `Weiter`
5.  Betreff eingeben
6.  direkt in den E-Mail-Body klicken und Text schreiben
7.  `HTML bearbeiten`
8.  Inhalt ändern
9.  `OK`
10. Anhang hinzufügen
11. `Ausführen`

Getestet:

``` text
✓ Kommunikationsdialog
✓ Weiter
✓ Betreff
✓ direkte Body-Eingabe
✓ HTML bearbeiten
✓ HTML-Übernahme
✓ Anhänge
✓ Ausführen
```

## 14. Netxp starten

Netxp wird für diese Konfiguration direkt mit dem dafür vorgesehenen Wine und Prefix gestartet:

``` fish
cd ~/.wine-netxp32/drive_c/NetxpVerein

env WINEPREFIX="$HOME/.wine-netxp32" WINEARCH=win32 \
    "$HOME/.local/opt/wine-netxp/bin/wine" SAMClient.exe
```

Die aktuelle Netxp-Software selbst sollte immer von der offiziellen Downloadseite bezogen werden:

**[Netxp:Verein -- offizieller Software-Download](https://www.netxp-verein.de/download/software/windows/)**

## 15. Verhalten nach einem Netxp-Update

Wenn der Launcher wegen eines falschen DLL-Hashes stoppt:

``` fish
cd ~/.wine-netxp32/drive_c/NetxpVerein

sha256sum MSDN.HtmlEditorControl.dll
```

Die neue DLL sichern, bevor sie gepatcht wird:

``` fish
mv MSDN.HtmlEditorControl.dll.original \
   MSDN.HtmlEditorControl.dll.original.previous 2>/dev/null

cp -a \
    MSDN.HtmlEditorControl.dll \
    MSDN.HtmlEditorControl.dll.original
```

Danach prüfen, ob `FixWineInterface.cs` noch auf die neue Version passt.

**Nicht die alte gepatchte DLL über eine neue Netxp-Version kopieren.**

Nach erfolgreicher Prüfung muss außerdem `EXPECTED_HASH` in `netxpverein` auf den Hash der neuen gepatchten DLL aktualisiert werden.

## 16. Backup vor einer CachyOS-Neuinstallation

Besonders wichtig sind:

``` text
~/.local/opt/wine-netxp
~/.wine-netxp32
```

Wine sichern:

``` fish
mkdir -p ~/Netxp-Recovery

tar -C "$HOME" \
    -caf ~/Netxp-Recovery/wine-netxp.tar.zst \
    .local/opt/wine-netxp
```

Kompletten Prefix sichern:

``` fish
tar -C "$HOME" \
    -caf ~/Netxp-Recovery/wine-netxp32-prefix.tar.zst \
    .wine-netxp32
```

Repository bzw. Patcher zusätzlich über GitHub sichern.

## 17. Restore nach Neuinstallation

Wine:

``` fish
cd "$HOME"

tar -xaf \
    ~/Netxp-Recovery/wine-netxp.tar.zst
```

Prefix:

``` fish
cd "$HOME"

tar -xaf \
    ~/Netxp-Recovery/wine-netxp32-prefix.tar.zst
```

Danach aus diesem Repository:

``` fish
mkdir -p ~/.local/bin
cp netxpverein ~/.local/bin/
chmod +x ~/.local/bin/netxpverein

mkdir -p ~/.local/share/applications
cp netxpverein.desktop ~/.local/share/applications/
chmod +x ~/.local/share/applications/netxpverein.desktop

mkdir -p ~/.local/share/netxpverein-fix
cp FixWineInterface.cs ~/.local/share/netxpverein-fix/

kbuildsycoca6
```

## 18. Hinweise für die Netxp-Entwickler

Ein langfristiger Upstream-Fix sollte im Quellcode des HTML-Editors erfolgen.

Empfehlungen:

1.  `IHTMLDocument.body` als das deklarierte `IHTMLElement` behandeln.
2.  Keine zwingenden Casts auf `HTMLBody`/`DispHTMLBody`.
3.  `innerHTML`, `innerText` und `outerHTML` über `IHTMLElement`.
4.  `contentEditable` portabel bzw. über Feature Detection setzen.
5.  `createElement()` nur einen gültigen Elementnamen übergeben.
6.  IE-spezifische COM-Interfaces nur nach erfolgreicher Interface-Prüfung verwenden.
7.  Das interne `loading` auch bei Exceptions garantiert zurücksetzen.
8.  Langfristig die synchrone `Application.DoEvents()`-Warteschleife durch eine ereignisbasierte Initialisierung ersetzen.

## 19. Bekannte Hashes

### Original

``` text
Datei:
MSDN.HtmlEditorControl.dll.original

SHA256:
38232ceff70aa673a86c79b9ee4e5524c74724c975749ccff7d3a0018e1d11eb
```

### Funktionierender Patch

``` text
Datei:
MSDN.HtmlEditorControl.dll.wineinterface2

SHA256:
2721cfa6dd413427f01110bf403f77993f3bff53b63fa0762027aa8b0ac54052
```

Die Hashes gelten ausschließlich für die getestete Netxp-Version.

## Status

``` text
CachyOS / KDE / Wayland
        ↓
separates klassisches Wine
        ↓
32-Bit-Wine-Prefix
        ↓
Microsoft .NET Framework
        ↓
Netxp:Verein
        ↓
gepatchtes MSDN.HtmlEditorControl
        ↓
Kommunikation funktioniert
```

## Lizenz / Hinweis

Dieses Repository enthält **keine Netxp-Binärdateien** und sollte auch keine proprietären Netxp-DLLs verteilen.

`FixWineInterface.cs` verändert eine lokal vorhandene `MSDN.HtmlEditorControl.dll`. Die Original- und gepatchten Netxp-DLLs sollten nicht in das öffentliche Repository committed werden, sofern keine entsprechende Erlaubnis zur Weiterverteilung besteht.
