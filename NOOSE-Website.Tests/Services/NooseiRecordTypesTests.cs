using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using NOOSE_Website.Services.Llm.Tools;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Tests.Services;

/// <summary>The capability table. Two failure modes matter: an English CLR name reaching the model, which it reads
/// as a record kind it may open, and a flag that promises a tool something the service behind it cannot do.</summary>
public class NooseiRecordTypesTests
{
    private static string SourceRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website"));

    private static string Source(params string[] parts)
    {
        var file = Path.Combine([SourceRoot(), .. parts]);
        Assert.True(File.Exists(file), $"Quelldatei nicht gefunden: {file}");
        return File.ReadAllText(file);
    }

    /// <summary>Every part of a class split over several files, so a scan cannot miss the half it forgot about.</summary>
    private static string Sources(string directory, string pattern)
    {
        var dir = Path.Combine(SourceRoot(), directory);
        var files = Directory.GetFiles(dir, pattern);
        Assert.NotEmpty(files);
        return string.Concat(files.Select(File.ReadAllText));
    }

    /// <summary>Last identifier of every <c>nameof(...)</c> in a file, however it was qualified.</summary>
    private static HashSet<string> NamedTypes(string source)
        => Regex.Matches(source, @"nameof\(([\w.]+)\)")
            .Select(m => m.Groups[1].Value.Split('.')[^1])
            .ToHashSet(StringComparer.Ordinal);

    [Theory]
    [InlineData("Person", "Person")]
    [InlineData("Faction", "Fraktion")]
    [InlineData("Case", "Vorgang")]
    [InlineData("Law", "Gesetz")]
    [InlineData("Job", "Aufgabe")]
    [InlineData("Appointment", "Termin")]
    [InlineData("KassenBuchung", "Kassenbuchung")]
    public void German_NamesTheTypeInGerman(string clr, string expected)
        => Assert.Equal(expected, NooseiRecordTypes.German(clr));

    [Fact]
    public void German_NeverFallsThroughToTheClrName()
    {
        // "Eintrag" is a deliberate dead end: vague, but it cannot be mistaken for a type the model may ask for
        Assert.Equal("Eintrag", NooseiRecordTypes.German("SomeTypeNobodyMapped"));
        Assert.Equal("Eintrag", NooseiRecordTypes.German("PersonPhoto"));
    }

    [Fact]
    public void German_ComesFromTheSearchCatalog_ForEveryCategoryItCarries()
    {
        // one label table, not two: the names the model sees are the names the result rows carry
        Assert.All(SearchCatalog.Categories, c =>
        {
            Assert.Equal(c.German, NooseiRecordTypes.German(c.Clr));
            Assert.Equal(c.Plural, NooseiRecordTypes.Plural(c.Clr));
        });
    }

    [Fact]
    public void EveryConfiguredCapability_NamesACategoryTheCatalogCarries()
    {
        // a capability keyed on a type the catalog dropped is granted to nothing at all, silently
        var known = SearchCatalog.Categories.Select(c => c.Clr).ToHashSet(StringComparer.Ordinal);

        Assert.All(NooseiRecordTypes.ConfiguredClrs, clr => Assert.Contains(clr, known));
    }

    [Fact]
    public void Plural_FallsBackToTheSingular_ForAnUnknownType()
    {
        Assert.Equal("Personen", NooseiRecordTypes.Plural("Person"));
        Assert.Equal("Eintrag", NooseiRecordTypes.Plural("SomeTypeNobodyMapped"));
    }

    [Theory]
    [InlineData("Person")]
    [InlineData("Faction")]
    [InlineData("Law")]
    [InlineData("Document")]
    public void IsReadable_CoversTheTypesLiesAkteOffers(string clr)
    {
        Assert.True(NooseiRecordTypes.IsReadable(clr));
        Assert.Contains($"\"{NooseiRecordTypes.German(clr)}\"", NooseiRecordTypes.EnumJson);
    }

    /// <summary>The content children: reached through <c>lies_akteninhalt</c> on the record they hang off, never
    /// opened as a record of their own. They have no page either — a hit on one routes through its parent.</summary>
    [Theory]
    [InlineData("Source")]
    [InlineData("Comment")]
    [InlineData("Link")]
    [InlineData("CustomFieldValue")]
    public void IsReadable_IsFalseForTypesLiesAkteWouldReject(string clr)
    {
        Assert.False(NooseiRecordTypes.IsReadable(clr));
        Assert.DoesNotContain($"\"{NooseiRecordTypes.German(clr)}\"", NooseiRecordTypes.EnumJson);
    }

    [Fact]
    public void Clr_WithoutACapability_TranslatesEveryNameInTheTable()
    {
        Assert.Equal("Faction", NooseiRecordTypes.Clr("Fraktion"));
        Assert.Equal("Job", NooseiRecordTypes.Clr("Aufgabe"));
        Assert.Null(NooseiRecordTypes.Clr("Hausmeister"));
        Assert.Null(NooseiRecordTypes.Clr(null));
    }

    [Fact]
    public void Clr_WithACapability_RefusesATypeTheToolCannotHandle()
    {
        // the gate that matters: Visibility.IsRecordVisibleAsync treats an unknown type as visible to everyone,
        // so a type reaching lies_akte or an anchor without a Read flag would answer past its own helper
        Assert.Equal("Taskforce", NooseiRecordTypes.Clr("Taskforce", NooseiUse.Read));
        Assert.Equal("Job", NooseiRecordTypes.Clr("Aufgabe", NooseiUse.Read));

        // an appointment is openable but has no plain list service, so the filter tool must keep rejecting it
        Assert.Null(NooseiRecordTypes.Clr("Termin", NooseiUse.List));
        Assert.Equal("Appointment", NooseiRecordTypes.Clr("Termin", NooseiUse.Read));

        // a comment is a search category, but it is read through its parent, never opened as a record of its own
        Assert.Equal("Comment", NooseiRecordTypes.Clr("Kommentar", NooseiUse.Search));
        Assert.Null(NooseiRecordTypes.Clr("Kommentar", NooseiUse.Read));
        Assert.Null(NooseiRecordTypes.Clr("Kommentar", NooseiUse.List));
    }

    [Fact]
    public void Document_IsNowBothReadableAndSearchable()
    {
        // inverted deliberately: the search emitted no document category at all, so offering it as a filter
        // answered every such question with a false "no hits". There is a document provider now.
        Assert.True(NooseiRecordTypes.Can("Document", NooseiUse.Read));
        Assert.True(NooseiRecordTypes.Can("Document", NooseiUse.Search));
        Assert.Contains("Dokument", NooseiRecordTypes.SearchableEnumJson);
    }

    [Fact]
    public void ACategoryTheAssistantMayNarrowTo_IsOneTheSearchCanFill()
    {
        // the inverse of the old document trap, asserted for every type at once
        var searchable = NooseiRecordTypes.Names(NooseiUse.Search)
            .Select(n => NooseiRecordTypes.Clr(n)!)
            .ToArray();

        Assert.All(searchable, clr => Assert.True(
            SearchCatalog.Has(clr, SearchTraits.Assistant),
            $"{clr} is offered to suche_akten but the catalog does not mark it as an assistant category"));
    }

    [Theory]
    [InlineData(NooseiUse.Read)]
    [InlineData(NooseiUse.List)]
    [InlineData(NooseiUse.Search)]
    [InlineData(NooseiUse.Chronicle)]
    public void EveryEnumJson_IsAJsonArrayOfTheNamesCarryingThatCapability(NooseiUse use)
    {
        var json = use switch
        {
            NooseiUse.Read => NooseiRecordTypes.EnumJson,
            NooseiUse.List => NooseiRecordTypes.ListableEnumJson,
            NooseiUse.Search => NooseiRecordTypes.SearchableEnumJson,
            _ => NooseiRecordTypes.ChronicleEnumJson,
        };

        var parsed = JsonSerializer.Deserialize<string[]>(json)!;

        Assert.NotEmpty(parsed);
        Assert.Equal(NooseiRecordTypes.Names(use), parsed);
        // the tool block is the cached prompt prefix, so the order is part of the contract
        Assert.Equal("Person", parsed[0]);
    }

    /// <summary>Drift guard: a type flagged <see cref="NooseiUse.Read" /> without a dossier branch answers
    /// <c>lies_akte</c> with "not found" — indistinguishable from a record the agent may not see.</summary>
    [Fact]
    public void EveryReadableType_HasADossierBuilder()
    {
        var builder = NamedTypes(Sources(Path.Combine("Services", "Llm"), "DossierContextBuilder*.cs"));

        var missing = NooseiRecordTypes.Names(NooseiUse.Read)
            .Select(n => NooseiRecordTypes.Clr(n)!)
            .Where(clr => !builder.Contains(clr))
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>Drift guard: a readable type without an arm in the central gate falls through its `_ => true` tail
    /// and is answered as visible to everyone who asks for it by id.</summary>
    /// <remarks>The most load-bearing of these scans. Two types were already only apparently gated — Job and
    /// Appointment were existence checks while their real rules sat in JobVisibility/AppointmentVisibility — and
    /// that stayed harmless only because neither was readable.</remarks>
    [Fact]
    public void EveryReadableType_HasAnArmInTheVisibilityGate()
    {
        var gate = NamedTypes(Source("Services", "Visibility.cs"));

        var missing = NooseiRecordTypes.Names(NooseiUse.Read)
            .Select(n => NooseiRecordTypes.Clr(n)!)
            .Where(clr => !gate.Contains(clr))
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>Drift guard: a type flagged <see cref="NooseiUse.List" /> without a branch in the filter tool is
    /// answered with "no attribute search available" — a round spent on a promise the schema made.</summary>
    [Fact]
    public void EveryListableType_HasABranchInTheFilterTool()
    {
        var tool = NamedTypes(Source("Services", "Llm", "Tools", "FilterRecordsTool.cs"));

        var missing = NooseiRecordTypes.Names(NooseiUse.List)
            .Select(n => NooseiRecordTypes.Clr(n)!)
            .Where(clr => !tool.Contains(clr))
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>Drift guard: a searchable type the search emits no category for turns every restricted query into
    /// a false "no hits".</summary>
    /// <remarks>Asserted against <see cref="SearchCatalog"/> rather than by scraping the service source. The
    /// categories used to be inline blocks in one file; they are rows in the catalog now, and an object-level
    /// assertion is both stronger and immune to how the providers happen to be written.</remarks>
    [Fact]
    public void EverySearchableType_IsACategoryTheSearchEmits()
    {
        var emitted = SearchCatalog.Categories.Select(c => c.Clr).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(emitted);

        var missing = NooseiRecordTypes.Names(NooseiUse.Search)
            .Select(n => NooseiRecordTypes.Clr(n)!)
            .Where(clr => !emitted.Contains(clr))
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>Drift guard the other way round: every category the global search can emit must have a German
    /// label on both tables, or the next new one reaches the model under its English CLR name.</summary>
    [Fact]
    public void EveryTypeTheSearchEmits_HasAGermanLabel()
    {
        Assert.NotEmpty(SearchCatalog.Categories);

        var unlabelled = SearchCatalog.Categories
            .Where(c => c.German == "Eintrag" || string.IsNullOrWhiteSpace(c.German))
            .Select(c => c.Clr)
            .ToArray();
        Assert.Empty(unlabelled);

        // the assistant reads NooseiRecordTypes, so a category it may narrow to needs a label there too
        var unknownToTheModel = SearchCatalog.Clrs(SearchTraits.Assistant)
            .Where(clr => NooseiRecordTypes.German(clr) == "Eintrag")
            .ToArray();
        Assert.Empty(unknownToTheModel);
    }

    /// <summary>The assistant's searchable set and the catalog's must not drift apart.</summary>
    [Fact]
    public void NooseiSearchFlag_AndTheCatalogAssistantTrait_AgreeExactly()
    {
        var catalog = SearchCatalog.Clrs(SearchTraits.Assistant).ToHashSet(StringComparer.Ordinal);
        var noosei = NooseiRecordTypes.Names(NooseiUse.Search)
            .Select(n => NooseiRecordTypes.Clr(n)!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(catalog, noosei);
    }

    /// <summary>Drift guard: a chronicle filter the chronicle never fills reports "no changes" for a type that
    /// changes constantly.</summary>
    [Fact]
    public void EveryChronicledType_IsCollectedByTheChronicle()
    {
        var collected = NamedTypes(Source("Services", "GlobalChronikService.cs"));
        Assert.NotEmpty(collected);

        var missing = NooseiRecordTypes.Names(NooseiUse.Chronicle)
            .Select(n => NooseiRecordTypes.Clr(n)!)
            .Where(clr => !collected.Contains(clr))
            .ToArray();

        Assert.Empty(missing);
    }
}
