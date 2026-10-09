using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using System.Xml.Linq;

namespace RandomSteamGame.Controllers;

public sealed class SeoController(
    CanonicalUrlService canonicalUrls,
    IOptions<ApplicationOptions> applicationSettings) : ControllerBase
{
    [HttpGet("/robots.txt")]
    [HttpHead("/robots.txt")]
    public ContentResult Robots()
    {
        var content = applicationSettings.Value.NetworkMode == NetworkMode.AltNet
            ? "User-agent: *\nDisallow: /\n"
            : $"User-agent: *\nAllow: /\n\nSitemap: {canonicalUrls.GetCanonicalUrl("/sitemap.xml")}\n";

        return Content(content, "text/plain; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    [HttpHead("/sitemap.xml")]
    public ContentResult Sitemap()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        string[] paths = ["/", "/support", "/contributors", "/library-export"];
        var sitemap = new XDocument(
            new XElement(ns + "urlset", paths.Select(path =>
                new XElement(ns + "url", new XElement(ns + "loc",
                    path == "/" ? canonicalUrls.GetCanonicalUrl() + "/" : canonicalUrls.GetCanonicalUrl(path))))));

        return Content("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + sitemap, "application/xml; charset=utf-8");
    }
}
