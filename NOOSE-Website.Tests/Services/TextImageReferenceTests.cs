using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services;

/// <summary>A text image resolves like a source: its carrier decides the secrecy, the href points at the endpoint.</summary>
public class TextImageReferenceTests
{
    private static async Task<(SqliteTestContext Ctx, Person Person, string ImageId)> BuildAsync(bool classified)
    {
        var ctx = new SqliteTestContext();
        var person = Seed.Person(configure: p => p.IsClassified = classified);
        var image = new TextImage
        {
            EntityType = nameof(Person),
            EntityId = person.Id,
            FileNameSaved = "bild.png",
            ContentType = "image/png",
        };
        await using var db = ctx.NewContext();
        db.People.Add(person);
        db.TextImages.Add(image);
        await db.SaveChangesAsync();
        return (ctx, person, image.Id);
    }

    [Fact]
    public async Task Resolve_PointsAtTheDeliveryEndpointAndInheritsAnOpenCarrier()
    {
        var (ctx, _, imageId) = await BuildAsync(classified: false);
        using (ctx)
        {
            await using var db = ctx.NewContext();

            var map = await RecordsReference.ResolveAsync(db, [(nameof(TextImage), imageId)]);

            var resolution = Assert.Contains((nameof(TextImage), imageId), map);
            Assert.Equal("Bild", resolution.Display);
            Assert.False(resolution.Classified);
            Assert.Equal($"/dateien/textbilder/{imageId}", resolution.Href);
        }
    }

    [Fact]
    public async Task Resolve_MarksTheImageClassifiedWhenItsCarrierIs()
    {
        var (ctx, _, imageId) = await BuildAsync(classified: true);
        using (ctx)
        {
            await using var db = ctx.NewContext();

            var map = await RecordsReference.ResolveAsync(db, [(nameof(TextImage), imageId)]);

            Assert.True(Assert.Contains((nameof(TextImage), imageId), map).Classified);
        }
    }

    [Fact]
    public async Task ResolveMentions_HidesTheImageOfAClassifiedCarrierAndKeepsItsHrefOffThePage()
    {
        var (ctx, _, imageId) = await BuildAsync(classified: true);
        using (ctx)
        {
            // ResolveAsync never touches the search service; a picture is resolved, not searched for
            var mentions = new MentionService(ctx.Factory, null!);
            var text = $"Siehe {MentionParser.Token(nameof(TextImage), imageId)}";

            var blind = await mentions.ResolveAsync(text, isLeadership: false, meId: "agent-1");
            var seeing = await mentions.ResolveAsync(text, isLeadership: true, meId: "agent-1");

            var hidden = Assert.Single(blind.Where(s => s.IsReference));
            Assert.True(hidden.Hidden);
            Assert.Null(hidden.Href);
            var shown = Assert.Single(seeing.Where(s => s.IsReference));
            Assert.False(shown.Hidden);
            Assert.Equal($"/dateien/textbilder/{imageId}", shown.Href);
        }
    }
}
