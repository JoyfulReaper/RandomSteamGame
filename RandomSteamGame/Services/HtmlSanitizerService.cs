/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Ganss.Xss;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Services;

public class HtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer = new();

    public HtmlSanitizerService(IOptions<ApplicationOptions> applicationOptions)
    {
        if (!applicationOptions.Value.RemoteBrowserAssetsAllowed)
        {
            // Keep semantic formatting and clickable links, but no resource-loading
            // elements or inline CSS that could fetch an image or font automatically.
            _sanitizer.AllowedTags.IntersectWith(
            [
                "a", "abbr", "b", "blockquote", "br", "caption", "code", "col", "colgroup",
                "dd", "del", "div", "dl", "dt", "em", "figcaption", "figure",
                "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "ins", "kbd", "li",
                "ol", "p", "pre", "q", "s", "samp", "small", "span", "strong", "sub",
                "sup", "table", "tbody", "td", "tfoot", "th", "thead", "tr", "u", "ul", "var"
            ]);
            _sanitizer.AllowedAttributes.IntersectWith(["href", "title", "colspan", "rowspan"]);
        }
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }
}
