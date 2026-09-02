using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Infrastructure;

/// <summary>Marks everything outside the public routes as noindex.</summary>
/// <remarks>
/// Second layer next to <c>robots.txt</c>: that file asks a crawler not to fetch internal paths, this header tells
/// one that fetched anyway not to index it. A URL can be indexed from a link without ever being crawled, so the
/// polite request alone is not enough.
/// </remarks>
public sealed class PublicIndexingMiddleware(RequestDelegate next)
{
    private const string Header = "X-Robots-Tag";
    private const string NoIndex = "noindex, nofollow";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!PublicRoutes.IsPublic(context.Request.Path.Value))
        {
            // set, not append. Note the re-execute does NOT actually reach this middleware again: it is
            // registered upstream of UseStatusCodePagesWithReExecute, and re-execution restarts the pipeline
            // downstream of the re-executing middleware. So the header describes the ORIGINAL path, which is
            // what it should describe, and assigning rather than appending is cheap insurance either way.
            context.Response.Headers[Header] = NoIndex;
        }

        await next(context);
    }
}
