# Mehrfach-Bildupload & Lightbox Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mehrere Bilder auf einmal hochladen (Quellen, Bibliothek) und Bilder überall per gemeinsamem Lightbox-Overlay direkt auf der Seite öffnen.

**Architecture:** Eine wiederverwendbare `ImageLightbox`-Komponente (reines Blazor über `MudDialog`, statischer `ShowAsync`-Helfer wie `ConfirmDialog`). Quellen-/Bibliotheks-Serving-Endpoints bekommen einen `?inline=1`-Modus (nur für `image/*`), damit Bilder im `<img>`-Tag inline laden statt als Download. Mehrfach-Upload erzeugt N vorhandene Rows (`Source` bzw. `LibraryFile`) per Loop — keine Migration, keine Service-Änderung.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server), MudBlazor 9.5, EF Core 9 (Pomelo). Kein Test-Projekt im Repo.

## Global Constraints

- **Kein Test-Projekt** — Verifikation je Task = `dotnet build NOOSE-Website.slnx` erfolgreich + manuelle Prüfung. KEIN `dotnet test`.
- **Vor `dotnet build` den Dev-Server stoppen** (bin-Lock), falls `dotnet watch`/`run` läuft.
- **Kommentare nur Englisch**, inline `//` 2–3 Wörter (das *Warum*); `catch { }` → `/* best effort */`; `/// <summary>` einzeilig Englisch. Keine „Phase X"-Verweise.
- **Razor-Konventionen:** kein Code-Behind (`@code` inline), private Felder `_camelCase`, Dark-Mode bleibt.
- **Nur Bild-Typen inline servieren** (`ContentType` beginnt mit `image/`) — erlaubte Bildtypen sind jpeg/png/webp/gif (kein SVG/HTML), daher Inline-XSS ausgeschlossen. Nicht-Bilder bleiben Attachment-Download.
- **Kein neues JS-Modul** → kein `?v=`-Cache-Buster nötig.
- **Build aus dem Repo-Root.** Branch: `feature/demo-modus`.

---

### Task 1: `ImageLightbox`-Komponente

**Files:**
- Create: `NOOSE-Website/Components/Common/Shared/ImageLightbox.razor`

**Interfaces:**
- Produces:
  - `public sealed record ImageLightbox.LightboxImage(string Url, string? Caption)`
  - `public static Task ImageLightbox.ShowAsync(IDialogService dialog, IReadOnlyList<LightboxImage> images, int startIndex = 0)` — öffnet das Overlay; bei leerer Liste No-Op.

- [ ] **Step 1: Komponente anlegen**

`NOOSE-Website/Components/Common/Shared/ImageLightbox.razor`:

```razor
@* Shared full-screen image viewer with prev/next navigation. *@

<MudDialog>
    <TitleContent>
        <MudStack Row="true" AlignItems="AlignItems.Center" Justify="Justify.SpaceBetween" Style="width:100%;">
            <MudText Typo="Typo.body2" Class="mud-text-secondary">@(_index + 1) / @Images.Count</MudText>
            <MudIconButton Icon="@Icons.Material.Filled.Close" Color="Color.Default" OnClick="Close" aria-label="Schließen" />
        </MudStack>
    </TitleContent>
    <DialogContent>
        <div tabindex="0" @ref="_container" @onkeydown="OnKeyDown"
             style="outline:none;display:flex;align-items:center;justify-content:center;gap:8px;">
            <MudIconButton Icon="@Icons.Material.Filled.ChevronLeft" Size="Size.Large"
                           Disabled="@(_index == 0)" OnClick="Prev" aria-label="Vorheriges Bild" />
            <MudStack AlignItems="AlignItems.Center" Spacing="2" Style="flex:1;min-width:0;">
                <MudImage Src="@Current.Url" ObjectFit="ObjectFit.Contain"
                          Style="max-height:80vh;max-width:100%;" Class="rounded" />
                @if (!string.IsNullOrWhiteSpace(Current.Caption))
                {
                    <MudText Typo="Typo.caption" Class="mud-text-secondary">@Current.Caption</MudText>
                }
            </MudStack>
            <MudIconButton Icon="@Icons.Material.Filled.ChevronRight" Size="Size.Large"
                           Disabled="@(_index >= Images.Count - 1)" OnClick="Next" aria-label="Nächstes Bild" />
        </div>
    </DialogContent>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter, EditorRequired] public IReadOnlyList<LightboxImage> Images { get; set; } = default!;
    [Parameter] public int StartIndex { get; set; }

    private int _index;
    private ElementReference _container;

    private LightboxImage Current => Images[_index];

    protected override void OnInitialized()
        => _index = Math.Clamp(StartIndex, 0, Math.Max(0, Images.Count - 1));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // focus so arrow keys work immediately
            try { await _container.FocusAsync(); } catch { /* best effort */ }
        }
    }

    private void Prev()
    {
        if (_index > 0) { _index--; }
    }

    private void Next()
    {
        if (_index < Images.Count - 1) { _index++; }
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowLeft": Prev(); break;
            case "ArrowRight": Next(); break;
            case "Escape": Close(); break;
        }
    }

    private void Close() => MudDialog.Close();

    /// <summary>One image in the viewer.</summary>
    public sealed record LightboxImage(string Url, string? Caption);

    /// <summary>Opens the viewer over the given images, starting at the index.</summary>
    public static Task ShowAsync(IDialogService dialog, IReadOnlyList<LightboxImage> images, int startIndex = 0)
    {
        if (images is null || images.Count == 0)
        {
            return Task.CompletedTask;
        }
        var parameters = new DialogParameters<ImageLightbox>
        {
            { x => x.Images, images },
            { x => x.StartIndex, startIndex },
        };
        var options = new DialogOptions
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Large,
            CloseOnEscapeKey = true,
            CloseButton = false,
        };
        return dialog.ShowAsync<ImageLightbox>(null, parameters, options);
    }
}
```

- [ ] **Step 2: Build prüfen**

Run: `dotnet build NOOSE-Website.slnx`
Expected: `Build succeeded` ohne Fehler. (Falls `KeyboardEventArgs` unbekannt: `@using Microsoft.AspNetCore.Components.Web` oben in die Datei ergänzen — normalerweise global in `_Imports.razor` vorhanden.)

- [ ] **Step 3: Commit**

```bash
git add NOOSE-Website/Components/Common/Shared/ImageLightbox.razor
git commit -m "feat: shared ImageLightbox viewer component"
```

---

### Task 2: Inline-Serving für Quellen- und Bibliotheks-Endpoints

**Files:**
- Modify: `NOOSE-Website/Components/Common/SourcesFileEndpointRouteBuilderExtensions.cs`
- Modify: `NOOSE-Website/Components/Common/LibraryFileEndpointRouteBuilderExtensions.cs`

**Interfaces:**
- Produces: `GET /dateien/quellen/{id}?inline=1` und `GET /dateien/bibliothek/{id}?inline=1` liefern Bilder (`image/*`) mit Inline-Disposition; ohne `inline` oder für Nicht-Bilder unverändert Attachment-Download.

- [ ] **Step 1: Sources-Endpoint anpassen**

In `SourcesFileEndpointRouteBuilderExtensions.cs` die Lambda-Signatur um `[FromQuery] bool inline` erweitern und den `return` ersetzen.

Signatur (neuer Parameter nach `sourceId`):

```csharp
        group.MapGet("/{sourceId}", async (
            string sourceId,
            [FromQuery] bool inline,
            [FromServices] ISourceService sourceService,
            [FromServices] ISourcesStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
```

Den bisherigen `return Results.File(...)` (mit `source.OriginalName`) ersetzen durch:

```csharp
            await access.LogViewAsync(nameof(Source), sourceId, cancellationToken);

            // inline only for images; everything else stays a download
            var isImage = source.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            return inline && isImage
                ? Results.File(stream, source.ContentType!, enableRangeProcessing: true)
                : Results.File(stream, source.ContentType ?? "application/octet-stream",
                               source.OriginalName, enableRangeProcessing: true);
```

- [ ] **Step 2: Library-Endpoint anpassen**

In `LibraryFileEndpointRouteBuilderExtensions.cs` die Lambda-Signatur um `[FromQuery] bool inline` erweitern (nach `fileId`):

```csharp
        group.MapGet("/{fileId}", async (
            string fileId,
            [FromQuery] bool inline,
            [FromServices] ILibraryService library,
            [FromServices] ILibraryStorageService storage,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
```

Den bisherigen `return Results.File(stream, file.ContentType, file.OriginalName, enableRangeProcessing: true);` ersetzen durch:

```csharp
            await access.LogViewAsync(nameof(LibraryFile), fileId, cancellationToken);

            // inline only for images; everything else stays a download
            var isImage = file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            return inline && isImage
                ? Results.File(stream, file.ContentType, enableRangeProcessing: true)
                : Results.File(stream, file.ContentType, file.OriginalName, enableRangeProcessing: true);
```

(Hinweis: Die bestehende `access.LogViewAsync`-Zeile bleibt — nur die `return`-Zeile wird getauscht. `System` ist via globalem `using` verfügbar; `StringComparison` braucht kein zusätzliches `using`.)

- [ ] **Step 3: Build prüfen**

Run: `dotnet build NOOSE-Website.slnx`
Expected: `Build succeeded`.

- [ ] **Step 4: Manuell verifizieren**

App starten (`dotnet run --project NOOSE-Website/NOOSE-Website.csproj`), einloggen. Eine Quelle mit Bild-Upload anlegen, dann im Browser `/(...)/dateien/quellen/{id}?inline=1` aufrufen → Bild wird **angezeigt** (kein Download-Dialog). Ohne `?inline=1` → Download wie bisher. Gleiches für `/dateien/bibliothek/{id}?inline=1`.

- [ ] **Step 5: Commit**

```bash
git add NOOSE-Website/Components/Common/SourcesFileEndpointRouteBuilderExtensions.cs NOOSE-Website/Components/Common/LibraryFileEndpointRouteBuilderExtensions.cs
git commit -m "feat: inline serving variant for source and library images"
```

---

### Task 3: Lightbox in Foto-Galerien (Person + Fraktion)

**Files:**
- Modify: `NOOSE-Website/Components/Pages/People/Shared/PhotoGallery.razor`
- Modify: `NOOSE-Website/Components/Pages/Factions/Shared/FactionPhotoGallery.razor`

**Interfaces:**
- Consumes: `ImageLightbox.ShowAsync`, `ImageLightbox.LightboxImage` (Task 1).

- [ ] **Step 1: PhotoGallery — Bild klickbar machen**

In `PhotoGallery.razor` das `<MudImage .../>` (aktuell direkt in `MudPaper`) in einen klickbaren `div` wickeln:

```razor
                <MudPaper Class="pa-1" Elevation="2" Style="width:160px;">
                    <div @onclick="@(() => OpenLightboxAsync(photo))" style="cursor:pointer;">
                        <MudImage Src="@($"/dateien/personen/foto/{photo.Id}")" Alt="@photo.OriginalName"
                                  Height="120" Width="150" ObjectFit="ObjectFit.Cover" Class="rounded"
                                  Style="@($"object-position:{photo.FocalPointX}% {photo.FocalPointY}%")" />
                    </div>
```

(Die `MudStack` mit Caption + Edit/Delete-Buttons bleibt unverändert direkt darunter — sie ist Geschwister des Bild-`div`, kein `stopPropagation` nötig.)

- [ ] **Step 2: PhotoGallery — Handler ergänzen**

Im `@code`-Block (z. B. nach `OnInitializedAsync`) hinzufügen:

```csharp
    private async Task OpenLightboxAsync(PersonPhoto photo)
    {
        var images = Photos
            .Select(p => new ImageLightbox.LightboxImage($"/dateien/personen/foto/{p.Id}", p.OriginalName))
            .ToList();
        var start = Photos.IndexOf(photo);
        await ImageLightbox.ShowAsync(DialogService, images, Math.Max(0, start));
    }
```

(`@inject IDialogService DialogService` ist in dieser Datei bereits vorhanden.)

- [ ] **Step 3: FactionPhotoGallery — Bild klickbar machen**

In `FactionPhotoGallery.razor` den vorhandenen `<div style="position:relative;">` (der `MudImage` + Titelbild-Badge enthält) klickbar machen:

```razor
                    <div @onclick="@(() => OpenLightboxAsync(photo))" style="position:relative;cursor:pointer;">
                        <MudImage Src="@($"/dateien/fraktionen/foto/{photo.Id}")" Alt="@photo.OriginalName"
                                  Height="120" Width="150" ObjectFit="ObjectFit.Cover" Class="rounded" />
                        @if (photo.IsTitleImage)
                        {
                            <MudChip T="string" Size="Size.Small" Color="Color.Primary" Variant="Variant.Filled"
                                     Icon="@Icons.Material.Filled.Star"
                                     Style="position:absolute;top:4px;left:4px;margin:0;">Titelbild</MudChip>
                        }
                    </div>
```

- [ ] **Step 4: FactionPhotoGallery — Handler ergänzen**

Im `@code`-Block hinzufügen:

```csharp
    private async Task OpenLightboxAsync(FactionPhoto photo)
    {
        var images = _photos
            .Select(p => new ImageLightbox.LightboxImage($"/dateien/fraktionen/foto/{p.Id}", p.OriginalName))
            .ToList();
        var start = _photos.IndexOf(photo);
        await ImageLightbox.ShowAsync(DialogService, images, Math.Max(0, start));
    }
```

(`@inject IDialogService DialogService` ist bereits vorhanden.)

- [ ] **Step 5: Build prüfen**

Run: `dotnet build NOOSE-Website.slnx`
Expected: `Build succeeded`.

- [ ] **Step 6: Manuell verifizieren**

Personen-Akte mit ≥2 Fotos öffnen → Thumbnail klicken → Vollbild-Overlay; Pfeiltasten/Buttons blättern, Zähler stimmt, ESC/X schließt. Gleiches in einer Fraktions-Akte. Edit/Delete/Titelbild-Buttons öffnen weiterhin NICHT den Lightbox.

- [ ] **Step 7: Commit**

```bash
git add NOOSE-Website/Components/Pages/People/Shared/PhotoGallery.razor NOOSE-Website/Components/Pages/Factions/Shared/FactionPhotoGallery.razor
git commit -m "feat: open photo galleries in image lightbox"
```

---

### Task 4: Lightbox für Einzelbild-Karten (Mugshot + Fraktions-Titelbild)

**Files:**
- Modify: `NOOSE-Website/Components/Pages/People/Shared/IdentityCard.razor`
- Modify: `NOOSE-Website/Components/Pages/Factions/Shared/FactionCard.razor`

**Interfaces:**
- Consumes: `ImageLightbox.ShowAsync`, `ImageLightbox.LightboxImage` (Task 1).

- [ ] **Step 1: IdentityCard — Mugshot klickbar**

In `IdentityCard.razor` den `@if (photo is not null)`-Zweig so ändern, dass das `MudImage` in einen klickbaren `div` gewickelt ist:

```razor
        @if (photo is not null)
        {
            <div @onclick="@(() => OpenLightboxAsync(photo))" style="cursor:pointer;">
                <MudImage Src="@($"/dateien/personen/foto/{photo.Id}")" Alt="@Person.Name"
                          Height="220" ObjectFit="ObjectFit.Cover" Class="rounded"
                          Style="@($"width:100%; object-position:{photo.FocalPointX}% {photo.FocalPointY}%")" />
            </div>
        }
```

- [ ] **Step 2: IdentityCard — Inject + Handler**

`IDialogService` injizieren (zu den vorhandenen `[Inject]`-Feldern im `@code`-Block):

```csharp
    [Inject] private IDialogService DialogService { get; set; } = default!;
```

Und Handler ergänzen:

```csharp
    private async Task OpenLightboxAsync(PersonPhoto photo)
        => await ImageLightbox.ShowAsync(DialogService,
            new[] { new ImageLightbox.LightboxImage($"/dateien/personen/foto/{photo.Id}", Person.Name) });
```

- [ ] **Step 3: FactionCard — Titelbild klickbar**

In `FactionCard.razor` den `@if (!string.IsNullOrEmpty(TitleImagePhotoId))`-Zweig anpassen:

```razor
        @if (!string.IsNullOrEmpty(TitleImagePhotoId))
        {
            <div @onclick="OpenLightboxAsync" style="cursor:pointer;">
                <MudImage Src="@($"/dateien/fraktionen/foto/{TitleImagePhotoId}")" Alt="Titelbild"
                          Height="120" ObjectFit="ObjectFit.Cover" Class="rounded" Style="width:100%;" />
            </div>
        }
```

- [ ] **Step 4: FactionCard — Inject + Handler**

`IDialogService` injizieren:

```csharp
    [Inject] private IDialogService DialogService { get; set; } = default!;
```

Handler ergänzen:

```csharp
    private async Task OpenLightboxAsync()
        => await ImageLightbox.ShowAsync(DialogService,
            new[] { new ImageLightbox.LightboxImage($"/dateien/fraktionen/foto/{TitleImagePhotoId}", Faction.Name) });
```

- [ ] **Step 5: Build prüfen**

Run: `dotnet build NOOSE-Website.slnx`
Expected: `Build succeeded`.

- [ ] **Step 6: Manuell verifizieren**

Personen-Detailseite: Mugshot anklicken → Einzelbild-Overlay (beide Pfeile deaktiviert, „1 / 1"), schließbar. Fraktions-Detailseite mit Titelbild: Titelbild anklicken → Overlay. Karten ohne Bild (Avatar/Banner-Fallback) bleiben nicht-klickbar.

- [ ] **Step 7: Commit**

```bash
git add NOOSE-Website/Components/Pages/People/Shared/IdentityCard.razor NOOSE-Website/Components/Pages/Factions/Shared/FactionCard.razor
git commit -m "feat: open mugshot and faction title image in lightbox"
```

---

### Task 5: Quellen — Mehrfach-Bildupload + Thumbnails + Lightbox

**Files:**
- Modify: `NOOSE-Website/Components/Common/Shared/SourcesPanel.razor`

**Interfaces:**
- Consumes: `ImageLightbox.ShowAsync`/`LightboxImage` (Task 1); `?inline=1`-Endpoint (Task 2); `ISourcesStorageService` (`MaxBytes`, `IsAllowedType`), `SourceService.CreateAsync`, `SourceInput`.

- [ ] **Step 1: Storage-Service injizieren**

Oben in `SourcesPanel.razor` zu den `@inject`-Zeilen ergänzen:

```razor
@inject ISourcesStorageService SourcesStorage
```

- [ ] **Step 2: „Bilder hochladen"-Button neben „Quelle hinzufügen"**

Den Header-Block (`@if (_mayWrite) { <MudButton ... NewAsync>Quelle hinzufügen</MudButton> }`) ersetzen durch beide Buttons nebeneinander:

```razor
        @if (_mayWrite)
        {
            <MudStack Row="true" Spacing="2" AlignItems="AlignItems.Center">
                <MudFileUpload T="IReadOnlyList<IBrowserFile>" FilesChanged="UploadImagesAsync"
                               Accept="image/*" MaximumFileCount="20">
                    <CustomContent>
                        <MudButton Variant="Variant.Outlined" Color="Color.Primary" StartIcon="@Icons.Material.Filled.Image"
                                   OnClick="@context.OpenFilePickerAsync" Disabled="_load">
                            Bilder hochladen
                        </MudButton>
                    </CustomContent>
                    <SelectedTemplate />
                </MudFileUpload>
                <MudButton Variant="Variant.Filled" Color="Color.Primary" StartIcon="@Icons.Material.Filled.Add" OnClick="NewAsync">
                    Quelle hinzufügen
                </MudButton>
            </MudStack>
        }
```

- [ ] **Step 3: Upload-Quellen mit Bild als Thumbnail rendern**

Im `@switch (q.Type)` den `case SourceType.Upload:` ersetzen, sodass Bilder als klickbares Thumbnail erscheinen und Nicht-Bilder den Download-Link behalten:

```razor
                    case SourceType.Upload when IsImage(q.ContentType):
                        <div @onclick="@(() => OpenImageAsync(q))" style="cursor:pointer;width:max-content;" class="mt-1">
                            <MudImage Src="@($"/dateien/quellen/{q.Id}?inline=1")" Alt="@q.OriginalName"
                                      Height="96" ObjectFit="ObjectFit.Cover" Class="rounded" />
                        </div>
                        break;
                    case SourceType.Upload:
                        <MudLink Href="@($"/dateien/quellen/{q.Id}")" Target="_blank" Typo="Typo.body2" Class="d-block mt-1">
                            <MudIcon Icon="@Icons.Material.Filled.Download" Size="Size.Small" Class="mr-1" />@(q.OriginalName ?? "Datei herunterladen")
                        </MudLink>
                        break;
```

- [ ] **Step 4: Handler + Helfer ergänzen**

Im `@code`-Block hinzufügen:

```csharp
    private static bool IsImage(string? contentType)
        => contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    private async Task OpenImageAsync(Source clicked)
    {
        var imgs = _sources.Where(s => s.Type == SourceType.Upload && IsImage(s.ContentType)).ToList();
        var list = imgs
            .Select(s => new ImageLightbox.LightboxImage($"/dateien/quellen/{s.Id}?inline=1", s.Title))
            .ToList();
        var start = imgs.FindIndex(s => s.Id == clicked.Id);
        await ImageLightbox.ShowAsync(DialogService, list, Math.Max(0, start));
    }

    private async Task UploadImagesAsync(IReadOnlyList<IBrowserFile> files)
    {
        _load = true;
        try
        {
            var uploaded = 0;
            foreach (var file in files)
            {
                if (!IsImage(file.ContentType) || !SourcesStorage.IsAllowedType(file.ContentType))
                {
                    Snackbar.Add($"„{file.Name}“: kein erlaubtes Bild.", Severity.Warning);
                    continue;
                }
                if (file.Size > SourcesStorage.MaxBytes)
                {
                    Snackbar.Add($"„{file.Name}“: zu groß (max. {SourcesStorage.MaxBytes / (1024 * 1024)} MB).", Severity.Warning);
                    continue;
                }

                using var ms = new MemoryStream();
                await file.OpenReadStream(SourcesStorage.MaxBytes).CopyToAsync(ms);
                var input = new SourceInput
                {
                    Type = SourceType.Upload,
                    Title = file.Name,
                    FileContent = ms.ToArray(),
                    OriginalName = file.Name,
                    ContentType = file.ContentType,
                    SizeBytes = file.Size,
                };
                await SourceService.CreateAsync(EntityType, EntityId, input, User);
                uploaded++;
            }

            if (uploaded > 0)
            {
                Snackbar.Add(uploaded == 1 ? "Bild hochgeladen." : $"{uploaded} Bilder hochgeladen.", Severity.Success);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Upload fehlgeschlagen: {ex.Message}", Severity.Error);
        }
        finally
        {
            _load = false;
        }
    }
```

(`@inject ISourceService SourceService`, `IDialogService DialogService`, `ISnackbar Snackbar` sind bereits vorhanden; `MemoryStream`/`IBrowserFile` via globalem `using`.)

- [ ] **Step 5: Build prüfen**

Run: `dotnet build NOOSE-Website.slnx`
Expected: `Build succeeded`.

- [ ] **Step 6: Manuell verifizieren**

In einer Akte (z. B. Person) unter „Quellen & Anhänge" → „Bilder hochladen" mit 3 Bildern → 3 Einträge erscheinen, jeweils als Thumbnail. Thumbnail klicken → Lightbox über alle Bild-Quellen, Blättern funktioniert. Eine Nicht-Bild-Quelle (PDF über „Quelle hinzufügen") bleibt Download-Link. Eine zu große/falsche Datei → Snackbar-Warnung, Rest läuft durch.

- [ ] **Step 7: Commit**

```bash
git add NOOSE-Website/Components/Common/Shared/SourcesPanel.razor
git commit -m "feat: multi-image upload and lightbox in sources panel"
```

---

### Task 6: Bibliothek — Mehrfach-Upload (geteilte Metadaten) + Lightbox

**Files:**
- Modify: `NOOSE-Website/Components/Pages/Documents/Shared/LibraryFileDialog.razor`
- Modify: `NOOSE-Website/Components/Pages/Documents/Shared/LibraryPanel.razor`

**Interfaces:**
- Consumes: `ImageLightbox.ShowAsync`/`LightboxImage` (Task 1); `?inline=1`-Endpoint (Task 2); `LibraryService.UploadAsync` (eine Datei pro Aufruf).
- Produces: `LibraryFileDialog.Result(string? Title, string? Category, DocumentClassification Classification, IReadOnlyList<IBrowserFile> Files)`.

- [ ] **Step 1: Dialog — Upload auf Mehrfachauswahl umstellen**

In `LibraryFileDialog.razor` den `<MudFileUpload>`-Block ersetzen:

```razor
            @if (!IsProcessing)
            {
                <MudFileUpload T="IReadOnlyList<IBrowserFile>" FilesChanged="FilesSelected" MaximumFileCount="20">
                    <CustomContent>
                        <MudButton Variant="Variant.Outlined" Color="Color.Primary" StartIcon="@Icons.Material.Filled.UploadFile"
                                   OnClick="@context.OpenFilePickerAsync">
                            @(_files.Count == 0 ? "Dateien wählen…" : (_files.Count == 1 ? _files[0].Name : $"{_files.Count} Dateien gewählt"))
                        </MudButton>
                    </CustomContent>
                </MudFileUpload>
                <MudText Typo="Typo.caption" Class="mud-text-secondary">
                    Erlaubt: PDF, Bilder, Office-Dokumente, Text, ZIP – max. @(Storage.MaxBytes / (1024 * 1024)) MB.
                </MudText>
                @if (_warning is not null)
                {
                    <MudAlert Severity="Severity.Warning" Dense="true">@_warning</MudAlert>
                }
            }
```

- [ ] **Step 2: Dialog — Titel-Feld nur bei Einzeldatei/Edit zeigen**

Das vorhandene Titel-`<MudTextField @bind-Value="_title" ...>` in eine Bedingung setzen und für Mehrfach einen Hinweis zeigen:

```razor
            @if (IsProcessing || _files.Count <= 1)
            {
                <MudTextField @bind-Value="_title" Label="Titel" Variant="Variant.Outlined" Required="true"
                              RequiredError="Titel ist erforderlich" />
            }
            else
            {
                <MudAlert Severity="Severity.Info" Dense="true" Variant="Variant.Outlined">
                    @_files.Count Dateien – Titel je Datei = Dateiname; Kategorie und Verschluss-Stufe gelten für alle.
                </MudAlert>
            }
```

- [ ] **Step 3: Dialog — Save-Disabled-Logik + Felder + Handler**

Den `Save`-Button-`Disabled` ersetzen:

```razor
        <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="Save"
                   Disabled="@SaveDisabled">
            Speichern
        </MudButton>
```

Im `@code`-Block: `_file`-Feld durch `_files` ersetzen und `FileSelected` durch `FilesSelected`, plus `SaveDisabled` und angepasstes `Result`/`Save`:

```csharp
    private IReadOnlyList<IBrowserFile> _files = Array.Empty<IBrowserFile>();
    private string? _warning;

    // edit/single-upload require a title; multi-upload derives titles from filenames
    private bool SaveDisabled =>
        (!IsProcessing && _files.Count == 0)
        || ((IsProcessing || _files.Count == 1) && string.IsNullOrWhiteSpace(_title));

    private void FilesSelected(IReadOnlyList<IBrowserFile> files)
    {
        _warning = null;
        var valid = new List<IBrowserFile>();
        var rejected = new List<string>();
        foreach (var file in files)
        {
            if (file.Size > Storage.MaxBytes || !Storage.IsAllowedType(file.ContentType))
            {
                rejected.Add(file.Name);
                continue;
            }
            valid.Add(file);
        }
        _files = valid;
        if (rejected.Count > 0)
        {
            _warning = $"Übersprungen (zu groß oder Typ nicht erlaubt): {string.Join(", ", rejected)}";
        }
        if (_files.Count == 1 && string.IsNullOrWhiteSpace(_title))
        {
            // prefill title from the single file's name
            _title = Path.GetFileNameWithoutExtension(_files[0].Name);
        }
    }

    private void Save()
        => MudDialog.Close(DialogResult.Ok(new Result(
            string.IsNullOrWhiteSpace(_title) ? null : _title.Trim(), _category, _classification, _files)));
```

Den `Result`-Record ersetzen:

```csharp
    /// <summary>Dialog result: shared metadata plus the chosen files (upload only).</summary>
    public sealed record Result(string? Title, string? Category, DocumentClassification Classification, IReadOnlyList<IBrowserFile> Files);
```

(Das alte `FileSelected(IBrowserFile? file)` und das Feld `private IBrowserFile? _file;` vollständig entfernen.)

- [ ] **Step 4: Panel — Loop-Upload mit geteilten Metadaten**

In `LibraryPanel.razor` `UploadAsync` ersetzen:

```csharp
    private async Task UploadAsync()
    {
        var result = await LibraryFileDialog.ShowAsync(DialogService, "Dateien hochladen", DocumentViewerScope.AssignableOptions(_user));
        if (result is null || result.Files.Count == 0)
        {
            return;
        }
        try
        {
            var uploaded = 0;
            foreach (var file in result.Files)
            {
                // browser stream has no seek; buffer per file
                await using var buffer = new MemoryStream();
                await file.OpenReadStream(Storage.MaxBytes).CopyToAsync(buffer);
                buffer.Position = 0;
                var title = result.Files.Count == 1 && !string.IsNullOrWhiteSpace(result.Title)
                    ? result.Title!
                    : Path.GetFileNameWithoutExtension(file.Name);
                await LibraryService.UploadAsync(title, result.Category, result.Classification,
                    buffer, file.Name, file.ContentType, file.Size, _user);
                uploaded++;
            }
            Snackbar.Add(uploaded == 1 ? "Datei hochgeladen." : $"{uploaded} Dateien hochgeladen.", Severity.Success);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }
```

Im `EditAsync` bleibt alles gleich (nutzt nur `result.Title/Category/Classification`) — `result.Title` ist jetzt `string?`; `LibraryService.RefreshAsync` erwartet einen `string`. Daher dort absichern:

```csharp
        await LibraryService.RefreshAsync(file.Id, result.Title ?? file.Title, result.Category, result.Classification, _user);
```

- [ ] **Step 5: Panel — Bild-Zeilen per Lightbox öffnen**

Im `RowTemplate` die Titel-Zelle so anpassen, dass Bilder den Lightbox öffnen, Nicht-Bilder den Download-Link behalten:

```razor
            <MudTd DataLabel="Titel">
                <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
                    <MudIcon Icon="@FileIcon(context.ContentType)" Size="Size.Small" Color="Color.Primary" />
                    @if (IsImage(context.ContentType))
                    {
                        <MudLink Style="cursor:pointer;" @onclick="@(() => OpenLightboxAsync(context))">@context.Title</MudLink>
                    }
                    else
                    {
                        <MudLink Href="@($"/dateien/bibliothek/{context.Id}")" Target="_blank">@context.Title</MudLink>
                    }
                </MudStack>
                <MudText Typo="Typo.caption" Class="mud-text-secondary d-block">@context.OriginalName</MudText>
            </MudTd>
```

- [ ] **Step 6: Panel — Helfer ergänzen**

Im `@code`-Block hinzufügen:

```csharp
    private static bool IsImage(string contentType)
        => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private async Task OpenLightboxAsync(LibraryFile file)
    {
        var imgs = _filtered.Where(f => IsImage(f.ContentType)).ToList();
        var list = imgs
            .Select(f => new ImageLightbox.LightboxImage($"/dateien/bibliothek/{f.Id}?inline=1", f.Title))
            .ToList();
        var start = imgs.FindIndex(f => f.Id == file.Id);
        await ImageLightbox.ShowAsync(DialogService, list, Math.Max(0, start));
    }
```

(`@inject IDialogService DialogService` ist bereits vorhanden.)

- [ ] **Step 7: Build prüfen**

Run: `dotnet build NOOSE-Website.slnx`
Expected: `Build succeeded`.

- [ ] **Step 8: Manuell verifizieren**

Dokumente → Bibliothek → „Dateien hochladen": 1 Datei → Titel editierbar wie bisher. Mehrere Bilder zugleich → Titel-Feld weg, Info-Hinweis; nach Upload je Datei eine Zeile mit Dateiname-Titel, gemeinsame Kategorie/VS-Stufe. Bild-Zeile per Titel klicken → Lightbox über alle Bilder der gefilterten Liste; Download-Button je Zeile funktioniert weiter; PDF-Zeile bleibt Download. Metadaten bearbeiten (Edit) unverändert.

- [ ] **Step 9: Commit**

```bash
git add NOOSE-Website/Components/Pages/Documents/Shared/LibraryFileDialog.razor NOOSE-Website/Components/Pages/Documents/Shared/LibraryPanel.razor
git commit -m "feat: multi-file library upload with shared metadata and image lightbox"
```

---

## Self-Review

**Spec coverage:**
- ImageLightbox (Lightbox/Overlay, prev/next, Zähler, ESC) → Task 1 ✅
- Inline-Serving Quellen + Bibliothek → Task 2 ✅
- Lightbox Foto-Galerien → Task 3 ✅
- Lightbox Einzelkarten (Mugshot, Fraktions-Titelbild) → Task 4 ✅
- Quellen Mehrfach-Bildupload + Thumbnails + Lightbox → Task 5 ✅
- Bibliothek Mehrfach-Upload geteilte Metadaten + Lightbox → Task 6 ✅
- Galerien-Multi-Upload (schon vorhanden) → keine Arbeit nötig ✅
- Out of Scope (Rich-Text-`<img>`, Storage-Dedup, Bewerbungs-Anhang) → nicht eingeplant ✅

**Type consistency:** `ImageLightbox.LightboxImage(string Url, string? Caption)` und `ImageLightbox.ShowAsync(IDialogService, IReadOnlyList<LightboxImage>, int=0)` werden in Tasks 3–6 identisch verwendet. `LibraryFileDialog.Result` ist in Task 6 Schritt 3 (Definition) und Schritt 4 (Konsum in `UploadAsync`/`EditAsync`) konsistent (`Title` nullable, `Files` Liste). `IsImage` ist pro Komponente lokal definiert (SourcesPanel: `string?`; LibraryPanel: `string`, da `LibraryFile.ContentType` non-null) — bewusst, kein Konflikt.

**Placeholder scan:** Keine TBD/TODO; jeder Code-Step enthält vollständigen Code.

## Open Items

Keine.
