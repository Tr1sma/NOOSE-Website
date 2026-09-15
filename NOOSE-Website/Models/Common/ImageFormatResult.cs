namespace NOOSE_Website.Models.Common;

/// <summary>Picture format taken from the image dialog and applied by the editor.</summary>
/// <param name="Width">CSS width of the picture, empty for the original size.</param>
/// <param name="Alignment">Line alignment: center, right or justify; empty for the default.</param>
/// <param name="Alt">Alternative text.</param>
/// <param name="Caption">Caption below the picture; empty removes the caption line.</param>
/// <param name="Remove">True removes the picture with its caption.</param>
public sealed record ImageFormatResult(string? Width, string? Alignment, string? Alt, string? Caption, bool Remove);
