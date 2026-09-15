using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>What the rich-text interceptor moves into files, and what it deliberately leaves alone.</summary>
/// <remarks>
/// The audit interceptor rides along: the TextImage rows are added by this interceptor, and only the production
/// order proves they still get their stamp.
/// </remarks>
public sealed class RichTextHtmlInterceptorTests
{
    // one transparent pixel, small enough for every base64 branch
    private const string MiniBild = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private sealed class FixedUser(CurrentUserInfo info) : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(info);

        public CurrentUserInfo Get() => info;
    }

    private sealed class InMemoryStorage : ITextImageStorageService
    {
        private readonly Dictionary<string, byte[]> _dateien = new();

        public long MaxBytes { get; set; } = 8 * 1024 * 1024;

        public IReadOnlyDictionary<string, byte[]> Dateien => _dateien;

        public bool IsAllowedType(string contentType)
            => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        public Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var puffer = new MemoryStream();
            content.CopyTo(puffer);
            var name = $"{Guid.NewGuid():N}.png";
            _dateien[name] = puffer.ToArray();
            return Task.FromResult(name);
        }

        public Stream OpenRead(string fileNameSaved) => new MemoryStream(_dateien[fileNameSaved]);

        public void Delete(string fileNameSaved) => _dateien.Remove(fileNameSaved);
    }

    private static (RichTextHtmlInterceptor Interceptor, InMemoryStorage Storage) Aufbau(Action<InMemoryStorage>? einstellen = null)
    {
        var storage = new InMemoryStorage();
        einstellen?.Invoke(storage);
        var services = new ServiceCollection();
        services.AddSingleton<ITextImageStorageService>(storage);
        var provider = services.BuildServiceProvider();
        return (new RichTextHtmlInterceptor(provider.GetRequiredService<IServiceScopeFactory>()), storage);
    }

    private static AppDbContext MitKette(SqliteTestContext ctx, RichTextHtmlInterceptor interceptor)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(
                interceptor,
                new AuditSaveChangesInterceptor(new FixedUser(new CurrentUserInfo("tester", "Tester", false, false, false))))
            .Options);

    private static string DokumentHtml() => $"<p>Lagebild</p><img src=\"data:image/png;base64,{MiniBild}\">";

    [Fact]
    public async Task A_data_uri_becomes_a_file_row_and_a_url()
    {
        using var ctx = new SqliteTestContext();
        var (interceptor, storage) = Aufbau();
        await using (var db = MitKette(ctx, interceptor))
        {
            db.Documents.Add(new Document { Id = "d1", Title = "Lagebild", ContentHtml = DokumentHtml() });
            await db.SaveChangesAsync();
        }

        await using var check = ctx.NewContext();
        var dokument = await check.Documents.SingleAsync();
        var bild = await check.TextImages.SingleAsync();
        Assert.DoesNotContain("data:image", dokument.ContentHtml);
        Assert.Contains($"<img src=\"/dateien/textbilder/{bild.Id}\">", dokument.ContentHtml);
        Assert.Equal(nameof(Document), bild.EntityType);
        Assert.Equal("d1", bild.EntityId);
        Assert.Equal("image/png", bild.ContentType);
        Assert.Equal(Convert.FromBase64String(MiniBild), storage.Dateien[bild.FileNameSaved]);
        // the audit interceptor runs after this one and must still see the added row
        Assert.Equal("tester", bild.CreatedById);
    }

    [Fact]
    public async Task An_image_the_storage_refuses_stays_inline()
    {
        using var ctx = new SqliteTestContext();
        var (interceptor, storage) = Aufbau(s => s.MaxBytes = 4);
        await using (var db = MitKette(ctx, interceptor))
        {
            db.Documents.Add(new Document { Id = "d1", Title = "Lagebild", ContentHtml = DokumentHtml() });
            await db.SaveChangesAsync();
        }

        await using var check = ctx.NewContext();
        Assert.Contains("data:image", (await check.Documents.SingleAsync()).ContentHtml);
        Assert.Empty(storage.Dateien);
        Assert.Equal(0, await check.TextImages.CountAsync());
    }

    [Fact]
    public async Task A_public_carrier_keeps_its_base64()
    {
        // the delivery endpoint is internal-only, so a press row must never hand out a URL
        using var ctx = new SqliteTestContext();
        var (interceptor, storage) = Aufbau();
        await using (var db = MitKette(ctx, interceptor))
        {
            db.Pressemitteilungen.Add(new Pressemitteilung { Id = "p1", DraftHtml = DokumentHtml() });
            await db.SaveChangesAsync();
        }

        await using var check = ctx.NewContext();
        Assert.Contains("data:image", (await check.Pressemitteilungen.SingleAsync()).DraftHtml);
        Assert.Empty(storage.Dateien);
    }

    [Fact]
    public async Task A_non_image_data_uri_is_left_alone()
    {
        using var ctx = new SqliteTestContext();
        var (interceptor, storage) = Aufbau();
        await using (var db = MitKette(ctx, interceptor))
        {
            db.Documents.Add(new Document
            {
                Id = "d1",
                Title = "Anhang",
                ContentHtml = "<img src=\"data:application/pdf;base64,AAAA\">",
            });
            await db.SaveChangesAsync();
        }

        await using var check = ctx.NewContext();
        Assert.Contains("data:application/pdf", (await check.Documents.SingleAsync()).ContentHtml);
        Assert.Empty(storage.Dateien);
    }

    [Fact]
    public async Task An_untouched_column_is_not_rewritten()
    {
        // a row that was saved before this interceptor existed keeps its base64 until its text changes
        using var ctx = new SqliteTestContext();
        await using (var seed = ctx.NewContext())
        {
            seed.Documents.Add(new Document { Id = "d1", Title = "Alt", ContentHtml = DokumentHtml() });
            await seed.SaveChangesAsync();
        }
        var (interceptor, storage) = Aufbau();

        await using (var db = MitKette(ctx, interceptor))
        {
            var dokument = await db.Documents.SingleAsync();
            dokument.Title = "Neu";
            await db.SaveChangesAsync();
        }

        await using var check = ctx.NewContext();
        Assert.Contains("data:image", (await check.Documents.SingleAsync()).ContentHtml);
        Assert.Empty(storage.Dateien);
    }

    [Fact]
    public async Task A_caption_line_becomes_a_figure_on_save()
    {
        // the second save of the same picture has no base64 left, but the caption still has to be folded
        using var ctx = new SqliteTestContext();
        var (interceptor, _) = Aufbau();
        await using (var db = MitKette(ctx, interceptor))
        {
            db.Documents.Add(new Document
            {
                Id = "d1",
                Title = "Lagebild",
                ContentHtml = "<p class=\"ql-align-center\"><img src=\"/dateien/textbilder/a\"></p>"
                    + "<p class=\"noose-bildtext\">Hafen bei Nacht</p>",
            });
            await db.SaveChangesAsync();
        }

        await using var check = ctx.NewContext();
        var html = (await check.Documents.SingleAsync()).ContentHtml;
        Assert.Contains("<figure", html);
        Assert.Contains("<figcaption", html);
        Assert.Contains("Hafen bei Nacht", html);
        Assert.Contains("ql-align-center", html);
        Assert.DoesNotContain("noose-bildtext", html);
    }

    [Fact]
    public async Task A_synchronous_save_takes_the_same_path()
    {
        using var ctx = new SqliteTestContext();
        var (interceptor, storage) = Aufbau();
        await using (var db = MitKette(ctx, interceptor))
        {
            db.AgentNotes.Add(new AgentNote
            {
                Id = "n1",
                AgentId = "agent-1",
                EntryDate = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
                Text = DokumentHtml(),
            });
            db.SaveChanges();
        }

        await using var check = ctx.NewContext();
        Assert.DoesNotContain("data:image", (await check.AgentNotes.SingleAsync()).Text);
        Assert.Single(storage.Dateien);
    }
}

/// <summary>The registry is the allowlist of the read gate; these invariants keep it honest.</summary>
public sealed class RichTextImageFieldsTests
{
    [Fact]
    public void Every_registered_property_exists_in_the_model()
    {
        using var ctx = new SqliteTestContext();
        using var db = ctx.NewContext();

        foreach (var typ in RichTextImageFields.Types)
        {
            var entity = db.Model.FindEntityType(typ);
            Assert.NotNull(entity);
            foreach (var feld in RichTextImageFields.For(typ))
            {
                Assert.NotNull(entity!.FindProperty(feld));
            }
        }
    }

    [Fact]
    public async Task Every_registered_carrier_is_answered_by_the_record_gate()
    {
        // Visibility treats an unknown type as visible, so a type that is not named there would answer every
        // request with "yes" — this asserts each registered carrier decides, by failing on a non-existent row
        using var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        var scope = new ViewerScope(true, true, "ich", null, IsLeadership: true, MayAgenda: true, IsInternalAgent: true);

        foreach (var typ in RichTextImageFields.Types)
        {
            Assert.False(await Visibility.IsRecordVisibleAsync(db, typ.Name, "gibt-es-nicht", scope));
        }
    }

    [Fact]
    public void Public_carriers_stay_unregistered()
    {
        string[] oeffentlich =
        [
            nameof(Pressemitteilung), nameof(OeffentlicheSeite), nameof(OeffentlicheWarnung),
            nameof(OeffentlicherLagebericht), nameof(OeffentlicheFaqEintrag),
            nameof(OeffentlichesFraktionsprofil), nameof(OeffentlicheFahndung),
        ];

        foreach (var name in oeffentlich)
        {
            Assert.False(RichTextImageFields.IsRegistered(name));
        }
    }
}
