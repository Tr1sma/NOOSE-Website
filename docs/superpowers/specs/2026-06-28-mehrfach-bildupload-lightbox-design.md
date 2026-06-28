# Mehrfach-Bildupload & Lightbox — Design

**Datum:** 2026-06-28
**Status:** Freigegeben (Design)
**Branch-Kontext:** `feature/demo-modus`

## Ziel

Zwei zusammenhängende Verbesserungen über alle Bild-Flächen der Seite:

1. **Mehrere Bilder auf einmal hochladen**, wo aktuell nur Einzel-Upload möglich ist (Quellen, Bibliothek). Die Foto-Galerien können das bereits.
2. **Bilder direkt auf der Website öffnen** über ein gemeinsames **Lightbox/Overlay** (Vollbild im Seiten-Overlay, Weiter/Zurück, Zähler, ESC), statt Bilder herunterzuladen oder nur als Thumbnail ohne Vergrößerung zu zeigen.

## Scope

**In Scope** (vom Nutzer bestätigt):

- Gemeinsame `ImageLightbox`-Komponente, verdrahtet an: Foto-Galerien (Person + Fraktion), Einzelbild-Karten (Mugshot in `IdentityCard`, Fraktions-Titelbild in `FactionCard`), Quellen-Bildanhänge (`SourcesPanel`), Bibliothek-Bilder (`LibraryPanel`).
- Inline-Serving für Quellen- und Bibliotheks-Endpoints (Foto-Endpoints liefern schon inline).
- Mehrfach-Upload bei Quellen (N `Source`-Rows) und Bibliothek (geteilte Metadaten: gemeinsame Kategorie + VS-Stufe, Titel je Datei = Dateiname).

**Out of Scope:**

- Eingebettete `<img>` in Rich-Text (`DocumentView`, `DocumentPrint`, `BewerbungMessagePanel`) per Klick zoombar machen — bräuchte einen JS-Klick-Delegate auf sanitiertem HTML; separates Feature.
- Bild-Einfügen in den Quill-RichText-Editor (Image-Button ist nicht aktiviert).
- Dedup der vier Copy-Paste-Storage-Services — optionaler Cleanup, hier nicht nötig.
- Bewerbungs-Anhang (`/dateien/bewerbungen/anhang`) — kein Mehrfach-/Bild-Anwendungsfall.

## Bestandsaufnahme (Ist-Zustand)

### Storage (`Infrastructure/Storage/`)

Vier Services ohne gemeinsame Basis (Copy-Paste), konfiguriert über `FileUploadOptions`:

| Service | Pfad | MaxBytes | Erlaubte Typen |
|---|---|---|---|
| `FileStorageService` (Person) | `App_Data/uploads/personen` | 10 MB | jpeg/png/webp/gif |
| `FactionPhotoStorageService` | `App_Data/uploads/fraktionen` | 10 MB | jpeg/png/webp/gif |
| `SourcesStorageService` | `App_Data/uploads/quellen` | 25 MB | PDF, Office, jpeg/png/webp/gif, txt/csv, zip |
| `LibraryStorageService` | `App_Data/uploads/bibliothek` | 25 MB | wie Quellen |

Dateinamen: `{Guid:N}{ext}`, flach im Basisverzeichnis. `FilePathHelper.SafePath` schützt vor Path-Traversal. Bild-Services: `SaveAsync(stream, contentType)`. Dokument-Services: `SaveAsync(stream, originalName)`.

### Serving-Endpoints

| Route | Disposition | Auth / Sichtbarkeit |
|---|---|---|
| `/dateien/personen/foto/{id}` | **inline** (kein Dateiname) | `ActiveAgent` + `ViewerScope` |
| `/dateien/fraktionen/foto/{id}` | **inline** | `ActiveAgent` + `ViewerScope` |
| `/dateien/quellen/{id}` | **attachment** (Download) | `ActiveAgent` + `ViewerScope` |
| `/dateien/bibliothek/{id}` | **attachment** | `ActiveAgent` + `InternalAgent` + `DocumentViewerScope` |
| `/dateien/bewerbungen/anhang/{id}` | attachment | owner / HRB / Führung |

Attachment entsteht durch Übergabe des Dateinamens als 3. Argument an `Results.File(...)`. Inline = ohne Dateiname.

### Datenmodell

Durchgängig **eine Zeile pro Datei**:

- `Source` (Tabelle `Quellen`): polymorph über `(EntityType, EntityId)`; Datei-Felder direkt auf der Row (`FileNameSaved`, `OriginalName`, `ContentType`, `SizeBytes`); `Type` = Link/Upload/Internal/Document/FreeText. → Mehrere Bilder = **N Upload-Rows, keine Migration**.
- `PersonPhoto` / `FactionPhoto`: eine Row je Foto, schon Multi.
- `LibraryFile`: eine Row je Datei.

### Bild-Flächen (Klick-Verhalten heute)

| Fläche | Datei | Heute |
|---|---|---|
| Person-Galerie | `PhotoGallery.razor` | Thumbnails, Klick = nichts |
| Mugshot | `IdentityCard.razor` | statisch |
| Fraktion-Galerie | `FactionPhotoGallery.razor` | Thumbnails, Klick = nichts |
| Fraktions-Titelbild | `FactionCard.razor` | statisch |
| Quellen | `SourcesPanel.razor` | Download-Link (`_blank`) |
| Bibliothek | `LibraryPanel.razor` | Download-Link (`_blank`) |
| Rich-HTML | `DocumentView/Print`, `BewerbungMessagePanel` | Autor-`<img>`, Default — *out of scope* |

## Architektur & Komponenten

### 1. `ImageLightbox` (neu)

`Components/Common/Shared/ImageLightbox.razor` — reines Blazor über `MudDialog`, **kein neues JS-Modul** (also kein `?v=`-Cache-Buster-Thema).

```csharp
public sealed record LightboxImage(string Url, string? Caption);

// Parameter
[Parameter, EditorRequired] public IReadOnlyList<LightboxImage> Images { get; set; }
[Parameter] public int StartIndex { get; set; }
```

- Vollbild-Overlay: `MudImage ObjectFit="ObjectFit.Contain"`, auf Viewport-Höhe begrenzt (z. B. `max-height:85vh`).
- Navigation: Weiter/Zurück-Buttons **und** Pfeiltasten (`@onkeydown` auf fokussiertem Container, `tabindex="0"`, Autofokus). Index wird an den Enden geklemmt (Buttons dort `Disabled`).
- Zähler „aktuell / gesamt", Caption unten (falls vorhanden), Schließen-Button oben rechts, ESC schließt (`CloseOnEscapeKey`), Backdrop-Klick schließt.
- Statischer Helfer spiegelt vorhandenes Idiom (`ConfirmDialog`/`FocalPointDialog`/`SourceDialog`):

```csharp
public static Task ShowAsync(IDialogService dialog,
    IReadOnlyList<LightboxImage> images, int startIndex = 0)
```

mit `DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Large, CloseOnEscapeKey = true }`. Leere Liste ⇒ kein Aufruf (Guard in den Aufrufern).

### 2. Inline-Serving (Endpoints anpassen)

Foto-Endpoints unverändert. `SourcesFileEndpointRouteBuilderExtensions` und `LibraryFileEndpointRouteBuilderExtensions` bekommen einen optionalen `?inline=1`:

```csharp
group.MapGet("/{sourceId}", async (string sourceId, [FromQuery] bool inline, /* … */) =>
{
    // … bestehende Sichtbarkeits-/NotFound-Logik unverändert …
    var isImage = source.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
    return inline && isImage
        ? Results.File(stream, source.ContentType!, enableRangeProcessing: true)               // inline
        : Results.File(stream, source.ContentType ?? "application/octet-stream",
                       source.OriginalName, enableRangeProcessing: true);                       // download
});
```

- Auth/Sichtbarkeit (`ViewerScope` bzw. `DocumentViewerScope`), `AccessLog`, Range-Processing **unverändert**.
- **Sicherheit:** Inline nur, wenn Content-Type `image/*`. Erlaubte Bildtypen sind jpeg/png/webp/gif (kein SVG/HTML) → kein Inline-XSS. Bibliothek erlaubt zwar PDF/Office/zip, die werden aber weiter als Attachment ausgeliefert.

### 3. Mehrfach-Upload — Quellen

`SourcesPanel.razor`: zusätzlicher Button „Bilder hochladen" (nur Write) neben „Quelle hinzufügen":

```razor
<MudFileUpload T="IReadOnlyList<IBrowserFile>" FilesChanged="UploadImagesAsync"
               Accept="image/*" MaximumFileCount="20"> … </MudFileUpload>
```

- `UploadImagesAsync` validiert pro Datei Typ/Größe via injiziertem `ISourcesStorageService` (`IsAllowedType`, `MaxBytes`), baut je gültige Datei ein `SourceInput { Type = Upload, Title = Dateiname, FileContent, OriginalName, ContentType, SizeBytes }` und ruft `SourceService.CreateAsync` **je Bild**. Ungültige Dateien überspringen + Snackbar-Warnung (Muster wie Galerie). Anschließend `LoadAsync`.
- **Keine Service-Änderung** (`CreateAsync` legt schon genau eine Upload-Quelle an). „Quelle hinzufügen" bleibt für Einzel-/Rich-Quellen (Link, FreeText, Document, Internal).

### 4. Mehrfach-Upload — Bibliothek

`LibraryFileDialog.razor`:

- `MudFileUpload` → `T="IReadOnlyList<IBrowserFile>"` mit `MaximumFileCount`.
- Bei **>1** Datei: Titel-Feld ausblenden (Titel je Datei = Dateiname ohne Endung); Kategorie + VS-Stufe bleiben sichtbar und gelten geteilt für alle. Bei genau 1 Datei: bisheriges Verhalten (Titel editierbar). Edit-Modus (`Preset != null`) bleibt strikt Einzeldatei.
- Result-Record trägt die Liste:

```csharp
public sealed record Result(string? Title, string? Category,
    DocumentClassification Classification, IReadOnlyList<IBrowserFile> Files);
```

`LibraryPanel.UploadAsync`: über `result.Files` loopen; je Datei puffern (`MemoryStream`, da Browser-Stream nicht seekbar), Titel = `Files.Count == 1 ? result.Title : Path.GetFileNameWithoutExtension(file.Name)`, gemeinsame Kategorie/Klassifikation, `LibraryService.UploadAsync(...)` je Datei. Snackbar-Zusammenfassung (z. B. „3 Dateien hochgeladen."), dann `LoadAsync`. **Keine Service-Änderung** (`UploadAsync` legt schon genau eine Datei an).

### 5. Lightbox-Verdrahtung pro Fläche

- **`PhotoGallery` / `FactionPhotoGallery`:** `MudImage` klickbar machen (`@onclick`), Liste aus allen Fotos der Akte bauen (`Url = /dateien/{personen|fraktionen}/foto/{id}`, `Caption = OriginalName`), `StartIndex` = Position des geklickten Fotos. Edit/Delete/Titelbild/Focal-Buttons mit `@onclick:stopPropagation`, damit sie den Lightbox nicht öffnen.
- **`IdentityCard` (Mugshot) / `FactionCard` (Titelbild):** vorhandenes `MudImage` mit `@onclick` versehen → `ImageLightbox.ShowAsync` mit **einem** `LightboxImage`. Cursor-Pointer als Affordanz.
- **`SourcesPanel`:** Upload-Quellen mit `ContentType` `image/*` als **Thumbnail** rendern (`src = /dateien/quellen/{id}?inline=1`) statt Download-Link; Klick → Lightbox über **alle** Bild-Quellen der Akte, Start am geklickten. Nicht-Bild-Uploads behalten den Download-Link.
- **`LibraryPanel`:** Bild-Zeilen (`ContentType` `image/*`) per Klick (Titel-Link/Icon) im Lightbox öffnen, Liste = alle Bilder der aktuell gefilterten Tabelle (`src = /dateien/bibliothek/{id}?inline=1`). Nicht-Bilder behalten Download. Der separate Download-Button bleibt für alle Zeilen erhalten.

## Datenfluss

**Upload (Quellen, mehrere Bilder):** Browser-Dateiauswahl → `UploadImagesAsync` validiert je Datei → `SourceInput` → `SourceService.CreateAsync` → `SourcesStorageService.SaveAsync` schreibt Datei, `Source`-Row wird eingefügt (Rollback der Datei bei DB-Fehler) → Panel lädt neu.

**Anzeige (Lightbox):** Klick auf Thumbnail/Bild → Aufrufer baut `IReadOnlyList<LightboxImage>` + Index → `ImageLightbox.ShowAsync` → `MudImage` lädt jedes Bild per `<img src>` über den jeweiligen `/dateien/...`-Endpoint (Sources/Library mit `?inline=1`). Bilder laufen **über HTTP, nicht über SignalR**.

## Fehlerbehandlung & Edge Cases

- Mehrfach-Upload: ungültige Datei (Typ/Größe) wird übersprungen, Warnung pro Datei, gültige laufen weiter; Erfolgs-Snackbar nur wenn ≥1 hochgeladen.
- Lightbox bei leerer Liste nicht öffnen.
- Klassifizierte Bilder bleiben serverseitig über die bestehenden Scopes gated — Inline-Variante ändert die Sichtbarkeitsprüfung nicht.
- Range-Processing (`enableRangeProcessing: true`) bleibt erhalten.
- Tastatur-Navigation nur bei Fokus des Lightbox-Containers; ESC/Backdrop schließen.
- `Results.File` disposed den Stream selbst — kein `using`.

## Betroffene Dateien

**Neu:**

- `Components/Common/Shared/ImageLightbox.razor`

**Geändert:**

- `Components/Common/SourcesFileEndpointRouteBuilderExtensions.cs` — `?inline=1`
- `Components/Common/LibraryFileEndpointRouteBuilderExtensions.cs` — `?inline=1`
- `Components/Common/Shared/SourcesPanel.razor` — Mehrfach-Bild-Upload + Bild-Thumbnails + Lightbox
- `Components/Pages/Documents/Shared/LibraryFileDialog.razor` — Mehrfachauswahl, geteilte Metadaten, `Result.Files`
- `Components/Pages/Documents/Shared/LibraryPanel.razor` — Loop-Upload + Lightbox für Bilder
- `Components/Pages/People/Shared/PhotoGallery.razor` — Lightbox
- `Components/Pages/Factions/Shared/FactionPhotoGallery.razor` — Lightbox
- `Components/Pages/People/Shared/IdentityCard.razor` — Mugshot-Lightbox
- `Components/Pages/Factions/Shared/FactionCard.razor` — Titelbild-Lightbox

Keine Migration, keine `Program.cs`-DI-Änderung, keine neue JS-Datei.

## Verifikation (manuell — kein Test-Projekt)

1. Person-/Fraktion-Akte: mehrere Fotos hochladen (bereits möglich), Thumbnail klicken → Lightbox, Weiter/Zurück per Tasten und Buttons, Zähler stimmt, ESC schließt.
2. Mugshot & Fraktions-Titelbild: Klick öffnet Einzelbild-Lightbox.
3. Quelle: „Bilder hochladen" mit 3 Bildern → 3 Quellen-Rows; Bild-Quelle als Thumbnail; Klick → Lightbox über alle Bild-Quellen. Nicht-Bild-Quelle (PDF) bleibt Download.
4. Bibliothek: mehrere Bilder mit geteilter Kategorie/VS-Stufe hochladen → je Datei eine Row mit Dateiname-Titel; Bild-Zeile klicken → Lightbox; Download-Button funktioniert weiter; PDF bleibt Download.
5. Klassifizierte Akte: Bild bleibt für Nicht-Berechtigte unsichtbar (Inline-Endpoint respektiert Scope).
6. `dotnet build NOOSE-Website.slnx` grün.

## Offene Punkte

Keine — Architektur, Scope und Metadaten-Verhalten sind entschieden.
