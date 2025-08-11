using SmrtStores.Dtos;
using SmrtStores.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace SmrtStores.Services
{
  public class ShippingService
  {
    private readonly string _canadaPostApiUsername;
    private readonly string _canadaPostApiPassword;
    private readonly string _canadaPostUri;
    private readonly string _upsApiKey;
    private readonly string _fedexApiKey;
    private readonly string _purolatorApiKey;
    private readonly string _originPostalCode;

    public ShippingService(IConfiguration configuration)
    {
      _canadaPostApiUsername = configuration["CANADA_POST_API_USERNAME"] ?? throw new Exception("no canada post api username");
      _canadaPostApiPassword = configuration["CANADA_POST_API_PASSWORD"] ?? throw new Exception("no canada post api password");
      _canadaPostUri = configuration["CANADA_POST_URI"] ?? throw new Exception("no canada post uri");
      _upsApiKey = configuration["UPS_API_KEY"] ?? throw new Exception("no ups api key");
      _fedexApiKey = configuration["FEDEX_API_KEY"] ?? throw new Exception("no fedex api key");
      _purolatorApiKey = configuration["PUROLATOR_API_KEY"] ?? throw new Exception("no purolator api key");
      _originPostalCode = configuration["ORIGIN_POSTAL_CODE"] ?? throw new Exception("no origin postal code");
    }

    private static HttpClient client = new HttpClient();

  public async Task<List<ShippingReturnDto>> GenerateCanadaPostShippingQuote(ShippingCreateDto shippingQuote)
  {
    // --- Allowed service codes by region (whitelists) ---
    var usServiceCodes = new HashSet<string> { "USA.EP", "USA.TP", "USA.XP" };
    var canadaServiceCodes = new HashSet<string> { "DOM.RP", "DOM.EP", "DOM.XP" };
    var intlServiceCodes = new HashSet<string> { "INT.XP", "INT.IP.AIR", "INT.TP" };

    var regionSet = shippingQuote.CountryCode?.ToUpperInvariant() switch
    {
      "CA" => canadaServiceCodes,
      "US" => usServiceCodes,
      _    => intlServiceCodes
    };

    // --- Auth header ---
    var plainTextBytes = Encoding.UTF8.GetBytes($"{_canadaPostApiUsername}:{_canadaPostApiPassword}");
    var base64Token = Convert.ToBase64String(plainTextBytes);

    // --- XML namespace + helpers ---
    var ns = (XNamespace)"http://www.canadapost.ca/ws/ship/rate-v4";
    string CleanPostal(string s) => (s ?? string.Empty).Replace(" ", "").ToUpperInvariant();

    // If your Weight is grams, convert to kg: decimal weightKg = shippingQuote.Weight / 1000m;
    decimal weightKg = shippingQuote.Weight;

    // --- Destination element varies by CA/US/INT ---
    var destElem = shippingQuote.CountryCode?.ToUpperInvariant() switch
    {
      "CA" => new XElement(ns + "destination",
              new XElement(ns + "domestic",
                new XElement(ns + "postal-code", CleanPostal(shippingQuote.PostalCode)))),
      "US" => new XElement(ns + "destination",
              new XElement(ns + "united-states",
                new XElement(ns + "zip-code", CleanPostal(shippingQuote.PostalCode)))),
      _    => new XElement(ns + "destination",
              new XElement(ns + "international",
                new XElement(ns + "country-code", shippingQuote.CountryCode?.ToUpperInvariant())))
    };

    // --- Build mailing-scenario XML (counter rates: omit customer-number) ---
    var mailingScenario = new XDocument(
      new XElement(ns + "mailing-scenario",
        new XElement(ns + "parcel-characteristics",
          new XElement(ns + "weight", weightKg.ToString("0.###", CultureInfo.InvariantCulture))
        ),
        new XElement(ns + "origin-postal-code", CleanPostal(_originPostalCode)),
        destElem
      )
    );

    // --- POST to Canada Post ---
    using var http = new HttpClient();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);
    http.DefaultRequestHeaders.Accept.Clear();
    http.DefaultRequestHeaders.Accept.Add(
      new MediaTypeWithQualityHeaderValue("application/vnd.cpc.ship.rate-v4+xml"));
    http.DefaultRequestHeaders.AcceptLanguage.Clear();
    http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-CA"));

    var content = new StringContent(
      mailingScenario.ToString(SaveOptions.DisableFormatting),
      Encoding.UTF8,
      "application/vnd.cpc.ship.rate-v4+xml"
    );

    using var resp = await http.PostAsync(_canadaPostUri, content);
    var xml = await resp.Content.ReadAsStringAsync();

    if (!resp.IsSuccessStatusCode)
      throw new HttpRequestException($"Canada Post rating error {(int)resp.StatusCode}: {xml}");

    // --- Parse response ---
    var doc = XDocument.Parse(xml);

    // tolerate slight structure variations
    var priceQuotes = doc.Root?.Elements(ns + "price-quote")
                    ?? doc.Descendants(ns + "price-quote");

    var results = new List<ShippingReturnDto>();

    foreach (var pq in priceQuotes)
    {
      var code = pq.Element(ns + "service-code")?.Value ?? string.Empty;
      if (!regionSet.Contains(code)) continue; // only include your allowlist

      var name = pq.Element(ns + "service-name")?.Value ?? code;

      // Price: use <due> (tax-in total); fallback to <price> if needed
      decimal due = 0m;
      var priceDetails = pq.Element(ns + "price-details");
      if (priceDetails != null)
      {
        var dueStr = priceDetails.Element(ns + "due")?.Value
                    ?? priceDetails.Element(ns + "price")?.Value
                    ?? "0";
        decimal.TryParse(dueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out due);
      }

      // Transit time
      int? days = null;
      var serviceStandard = pq.Element(ns + "service-standard");
      if (serviceStandard != null)
      {
        var tt = serviceStandard.Element(ns + "expected-transit-time")?.Value
                ?? serviceStandard.Element(ns + "transit-time")?.Value;
        if (int.TryParse(tt, out var d)) days = d;
      }

      // Map to Ground/Air/etc.
      var type = MapCanadaPostShippingType(code, name);

      results.Add(new ShippingReturnDto
      {
        ShippingMethod = ShippingMethod.CanadaPost,
        ShippingType = type,
        ShippingCost = (int)Math.Round(due * 100m, MidpointRounding.AwayFromZero), // cents
        Currency = string.IsNullOrWhiteSpace(shippingQuote.Currency) ? "CAD" : shippingQuote.Currency,
        ApproxShippingDaysMin = days ?? 0,
        ApproxShippingDaysMax = days ?? 0
      });
    }

    // sort by cost ascending so cheapest shows first
    return results.OrderBy(r => r.ShippingCost).ToList();
  }

// Simple mapper for shipping "type" buckets
  private static string MapCanadaPostShippingType(string serviceCode, string serviceName)
  {
    switch (serviceCode)
    {
      // Canada
      case "DOM.RP": return "Ground";          // Regular Parcel
      case "DOM.EP": return "Ground";          // Expedited Parcel
      case "DOM.XP": return "Air";             // Xpresspost
      // USA
      case "USA.EP": return "Ground";          // Expedited Parcel USA
      case "USA.TP": return "Air";             // Tracked Packet USA
      case "USA.XP": return "Air";             // Xpresspost USA
      // International
      case "INT.IP.AIR": return "Air";         // International Parcel Air
      case "INT.TP": return "Air";             // Tracked Packet International
      case "INT.XP": return "Air";             // Xpresspost International
    }

    var n = (serviceName ?? "").ToLowerInvariant();
    if (n.Contains("xpress") || n.Contains("priority") || n.Contains("air")) return "Air";
    if (n.Contains("expedited") || n.Contains("regular") || n.Contains("ground") || n.Contains("parcel")) return "Ground";
    return "Unknown";
  }


    public async Task<ShippingReturnDto> GenerateUpsShippingQuote (ShippingCreateDto shippingQuote)
    {
      
    }

    public async Task<ShippingReturnDto> GenerateFedexShippingQuote(ShippingCreateDto shippingQuote)
    {
      
    }

    public async Task<ShippingReturnDto> GeneratePurolatorShippingQuote(ShippingCreateDto shippingQuote)
    {
      
    }
  }
}