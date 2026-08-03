namespace NOOSE_Website.Tests;

/// <summary>Serializes tests that touch the static EnumLabelText store: while this collection runs, no other collection runs (and vice versa), so display-class assertions elsewhere never see an override.</summary>
[CollectionDefinition("EnumLabels", DisableParallelization = true)]
public class EnumLabelCollection;
