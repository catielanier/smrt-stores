using SmrtStores.Dtos;
using SmrtStores.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmrtStores.Services
{
  public class ShippingService
  {
    private readonly string _canadaPostApiUsername;
    private readonly string _canadaPostApiPassword;
    private readonly string _canadaPostUri;
    private readonly string _upsClientId;
    private readonly string _upsClientSecret;
    private readonly string _upsUri;
    private readonly string _fedexClientId;
    private readonly string _fedexClientSecret;
    private readonly string _fedexAccountNumber;
    private readonly string _fedexUri;
    private readonly string _purolatorClientId;
    private readonly string _purolatorClientSecret;
    private readonly string _purolatorUri;
    private readonly string _originPostalCode;

    public ShippingService(IConfiguration configuration)
    {
      _canadaPostApiUsername = configuration["CANADA_POST_API_USERNAME"] ?? throw new Exception("no canada post api username");
      _canadaPostApiPassword = configuration["CANADA_POST_API_PASSWORD"] ?? throw new Exception("no canada post api password");
      _canadaPostUri = configuration["CANADA_POST_URI"] ?? throw new Exception("no canada post uri");
      _upsClientId = configuration["UPS_CLIENT_ID"] ?? throw new Exception("no ups client id");
      _upsClientSecret = configuration["UPS_CLIENT_SECRET"] ?? throw new Exception("no ups client id");
      _upsUri = configuration["UPS_URI"] ?? throw new Exception("no ups uri");
      _fedexClientId = configuration["FEDEX_CLIENT_ID"] ?? throw new Exception("no fedex client id");
      _fedexClientSecret = configuration["FEDEX_CLIENT_SECRET"] ?? throw new Exception("no fedex client secret");
      _fedexAccountNumber = configuration["FEDEX_ACCOUNT_NUMBER"] ?? throw new Exception("no fedex account number");
      _fedexUri = configuration["FEDEX_URI"] ?? throw new Exception("no fedex uri");
      _purolatorClientId = configuration["PUROLATOR_CLIENT_ID"] ?? throw new Exception("no purolator client id");
      _purolatorClientSecret = configuration["PUROLATOR_CLIENT_SECRET"] ?? throw new Exception("no purolator client secret");
      _purolatorUri = configuration["PUROLATOR_URI"] ?? throw new Exception("no purolator uri");
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
      case "DOM.RP": return "Regular Parcel";          // Regular Parcel
      case "DOM.EP": return "Expedited Parcel";          // Expedited Parcel
      case "DOM.XP": return "Xpresspost";             // Xpresspost
      // USA
      case "USA.EP": return "Expedited Parcel";          // Expedited Parcel USA
      case "USA.TP": return "Tracked Parcel";             // Tracked Packet USA
      case "USA.XP": return "Xpresspost";             // Xpresspost USA
      // International
      case "INT.IP.AIR": return "Air";         // International Parcel Air
      case "INT.TP": return "Tracked Parcel";             // Tracked Packet International
      case "INT.XP": return "Xpresspost";             // Xpresspost International
    }

    var n = (serviceName ?? "").ToLowerInvariant();
    if (n.Contains("xpress") || n.Contains("priority") || n.Contains("air")) return "Air";
    if (n.Contains("expedited") || n.Contains("regular") || n.Contains("ground") || n.Contains("parcel")) return "Ground";
    return "Unknown";
  }


    public async Task<List<ShippingReturnDto>> GenerateUpsShippingQuote(ShippingCreateDto shippingQuote)
    {
      // base URL like "https://wwwcie.ups.com" (test) or "https://onlinetools.ups.com" (prod) in _upsUri
      string token = await GetUpsAccessTokenAsync(_upsUri, _upsClientId, _upsClientSecret);

      // If your Weight is grams, convert to kg:
      decimal weightKg = shippingQuote.Weight / 1000m;

      string Clean(string s) => (s ?? string.Empty).Replace(" ", "").ToUpperInvariant();

      var body = new
      {
        RateRequest = new
        {
          Request = new { TransactionReference = new { CustomerContext = "RateShop" } },
          Shipment = new
          {
            Shipper = new
            {
              Name = "Shipper",
              Address = new { PostalCode = Clean(_originPostalCode), CountryCode = "CA" }
            },
            ShipFrom = new { Name = "Origin", Address = new { PostalCode = Clean(_originPostalCode), CountryCode = "CA" } },
            ShipTo = new
            {
              Name = "Consignee",
              Address = new { PostalCode = Clean(shippingQuote.PostalCode), CountryCode = (shippingQuote.CountryCode ?? "").ToUpperInvariant() }
            },
            ShipmentTotalWeight = new
            {
              UnitOfMeasurement = new { Code = "KGS", Description = "Kilograms" },
              Weight = weightKg.ToString("0.###", CultureInfo.InvariantCulture)
            },
            Package = new[]
            {
              new {
                PackagingType = new { Code = "02", Description = "Package" }, // Customer-supplied
                PackageWeight = new {
                  UnitOfMeasurement = new { Code = "KGS", Description = "Kilograms" },
                  Weight = weightKg.ToString("0.###", CultureInfo.InvariantCulture)
                }
              }
            },
            // Uncomment for negotiated rates if you have a shipper number:
            // ShipmentRatingOptions = new { NegotiatedRatesIndicator = "Y" }
          }
        }
      };

      var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

      using var req = new HttpRequestMessage(HttpMethod.Post, $"{_upsUri}/api/rating/v2403/shop?additionalinfo=timeintransit");
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Headers.Accept.Clear();
      req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      req.Content = new StringContent(json, Encoding.UTF8, "application/json");

      using var res = await client.SendAsync(req);
      var resBody = await res.Content.ReadAsStringAsync();
      if (!res.IsSuccessStatusCode)
        throw new HttpRequestException($"UPS Rating error {(int)res.StatusCode}: {resBody}");

      using var doc = JsonDocument.Parse(resBody);
      var results = new List<ShippingReturnDto>();

      if (doc.RootElement.TryGetProperty("RateResponse", out var rr) &&
          rr.TryGetProperty("RatedShipment", out var rs) &&
          rs.ValueKind == JsonValueKind.Array)
      {
        foreach (var item in rs.EnumerateArray())
        {
          // service
          string code = item.GetProperty("Service").GetProperty("Code").GetString() ?? "";
          string name = item.GetProperty("Service").GetProperty("Description").GetString() ?? code;

          // price (prefer Negotiated if present)
          decimal amount = 0m;
          string currency = "CAD";

          if (item.TryGetProperty("NegotiatedRateCharges", out var nrc))
          {
            var nTotal = nrc.GetProperty("TotalCharge");
            currency = nTotal.GetProperty("CurrencyCode").GetString() ?? currency;
            decimal.TryParse(nTotal.GetProperty("MonetaryValue").GetString() ?? "0",
                            NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
          }
          else
          {
            var total = item.GetProperty("TotalCharges");
            currency = total.GetProperty("CurrencyCode").GetString() ?? currency;
            decimal.TryParse(total.GetProperty("MonetaryValue").GetString() ?? "0",
                            NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
          }

          // ETA (if provided)
          int? days = null;
          if (item.TryGetProperty("GuaranteedDelivery", out var gd) &&
              gd.TryGetProperty("BusinessDaysInTransit", out var bdit))
          {
            if (int.TryParse(bdit.GetString(), out var d)) days = d;
          }

          results.Add(new ShippingReturnDto
          {
            ShippingMethod = ShippingMethod.UPS,
            ShippingType = MapUpsShippingType(code, name),
            ShippingCost = (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
            Currency = string.IsNullOrWhiteSpace(shippingQuote.Currency) ? currency : shippingQuote.Currency,
            ApproxShippingDaysMin = days ?? 0,
            ApproxShippingDaysMax = days ?? 0
          });
        }
      }

      return results.OrderBy(r => r.ShippingCost).ToList();
    }

    // OAuth client credentials → bearer token
    private static async Task<string> GetUpsAccessTokenAsync(string baseUrl, string clientId, string clientSecret)
    {
      using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/security/v1/oauth/token");
      var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
      req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
      req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      req.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
        { "grant_type", "client_credentials" }
      });

      using var res = await client.SendAsync(req);
      var body = await res.Content.ReadAsStringAsync();
      if (!res.IsSuccessStatusCode)
        throw new HttpRequestException($"UPS OAuth error {(int)res.StatusCode}: {body}");

      using var doc = JsonDocument.Parse(body);
      return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new Exception("UPS OAuth: access_token missing");
    }

    // map UPS service → type bucket
    private static string MapUpsShippingType(string code, string name)
    {
      var n = (name ?? "").ToLowerInvariant();
      if (n.Contains("ground")) return "Ground";
      if (n.Contains("saver") || n.Contains("air") || n.Contains("express") || n.Contains("expedited") || n.Contains("day")) return "Air";

      // common codes (expand as needed)
      return code switch
      {
        "03" => "Ground", // UPS Ground
        "11" => "Standard", // UPS Standard (intl ground-ish)
        "01" => "Next Day Air",    // Next Day Air
        "02" => "2nd Day Air",    // 2nd Day Air
        "12" => "3 Day Select",    // 3 Day Select
        "13" => "Next Day Air Saver",    // Next Day Air Saver
        "14" => "Next Day Air Early",    // Next Day Air Early
        "65" => "Worldwide Saver",    // Worldwide Saver
        _ => "Unknown"
      };
    }

    public async Task<List<ShippingReturnDto>> GenerateFedexShippingQuote(ShippingCreateDto shippingQuote)
    {
      // 1) OAuth token
      var token = await GetFedexAccessTokenAsync(_fedexUri, _fedexClientId, _fedexClientSecret);

      // 2) Build request body
      // If Weight is grams in your system, change to: decimal weightKg = shippingQuote.Weight / 1000m;
      decimal weightKg = shippingQuote.Weight;
      string Clean(string s) => (s ?? "").Replace(" ", "").ToUpperInvariant();

      var body = new
      {
        accountNumber = new { value = _fedexAccountNumber },
        rateRequestControlParameters = new {
          returnTransitTimes = true,
          rateSortOrder = "LOWEST_TO_HIGHEST"
        },
        requestedShipment = new
        {
          shipper = new {
            address = new {
              postalCode = Clean(_originPostalCode),
              countryCode = "CA"
            }
          },
          recipient = new {
            address = new {
              postalCode = Clean(shippingQuote.PostalCode),
              countryCode = (shippingQuote.CountryCode ?? "").ToUpperInvariant(),
              residential = false
            }
          },
          // “DROPOFF_AT_FEDEX_LOCATION” or “REGULAR_PICKUP” etc.
          pickupType = "DROPOFF_AT_FEDEX_LOCATION",
          // Ask for both ACCOUNT and LIST rates; you can switch to just ACCOUNT
          rateRequestType = new[] { "ACCOUNT", "LIST" },
          requestedPackageLineItems = new[] {
            new {
              weight = new {
                units = "KG",
                value = weightKg.ToString("0.###", CultureInfo.InvariantCulture)
              }
              // dimensions = new { length="10", width="10", height="10", units="CM" } // optional, improves accuracy
            }
          },
          // optional carrier filter: FDXE (Express), FDXG (Ground)
          // carrierCodes = new[] { "FDXE", "FDXG" }
        }
      };

      var json = JsonSerializer.Serialize(body, new JsonSerializerOptions {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
      });

      // 3) Call Rates API
      using var req = new HttpRequestMessage(HttpMethod.Post, $"{_fedexUri}/rate/v1/rates/quotes");
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      req.Content = new StringContent(json, Encoding.UTF8, "application/json");

      using var res = await client.SendAsync(req);
      var resBody = await res.Content.ReadAsStringAsync();
      if (!res.IsSuccessStatusCode)
        throw new HttpRequestException($"FedEx Rating error {(int)res.StatusCode}: {resBody}");

      // 4) Parse response → list
      using var doc = JsonDocument.Parse(resBody);
      var results = new List<ShippingReturnDto>();

      // Shape per docs: output -> rateReplyDetails[]; each has serviceType, carrierCode, ratedShipmentDetails[]
      if (doc.RootElement.TryGetProperty("output", out var output) &&
          output.TryGetProperty("rateReplyDetails", out var details) &&
          details.ValueKind == JsonValueKind.Array)
      {
        foreach (var d in details.EnumerateArray())
        {
          string serviceType = d.GetProperty("serviceType").GetString() ?? "";
          string carrierCode = d.GetProperty("carrierCode").GetString() ?? ""; // FDXE / FDXG

          // Choose the lowest total charge from ratedShipmentDetails (use ACCOUNT if present, else LIST)
          decimal best = decimal.MaxValue;
          string currency = shippingQuote.Currency ?? "CAD";
          int? days = null;

          if (d.TryGetProperty("ratedShipmentDetails", out var rsd) && rsd.ValueKind == JsonValueKind.Array)
          {
            foreach (var sd in rsd.EnumerateArray())
            {
              // totalNetCharge or shipmentRateDetails -> totalNetCharge
              if (sd.TryGetProperty("totalNetCharge", out var tnc))
              {
                currency = tnc.GetProperty("currency").GetString() ?? currency;
                if (decimal.TryParse(tnc.GetProperty("amount").GetString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
                  best = Math.Min(best, amt);
              }
              else if (sd.TryGetProperty("shipmentRateDetails", out var srd) &&
                      srd.TryGetProperty("totalNetCharge", out var tnc2))
              {
                currency = tnc2.GetProperty("currency").GetString() ?? currency;
                if (decimal.TryParse(tnc2.GetProperty("amount").GetString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var amt2))
                  best = Math.Min(best, amt2);
              }
            }
          }

          // Transit time (commitment / transitTime)
          if (d.TryGetProperty("commitment", out var comm))
          {
            if (comm.TryGetProperty("transitTime", out var ttElem))
            {
              // often like "TWO_DAYS" / "THREE_DAYS"
              var tt = ttElem.GetString();
              days = ParseFedexTransitDays(tt);
            }
            else if (comm.TryGetProperty("minTransitTime", out var minTT) &&
                    comm.TryGetProperty("maxTransitTime", out var maxTT))
            {
              var min = ParseFedexTransitDays(minTT.GetString());
              var max = ParseFedexTransitDays(maxTT.GetString());
              days = max ?? min;
            }
          }

          if (best != decimal.MaxValue)
          {
            results.Add(new ShippingReturnDto
            {
              ShippingMethod = ShippingMethod.FedEx,
              ShippingType = MapFedexShippingType(carrierCode, serviceType),
              ShippingCost = (int)Math.Round(best * 100m, MidpointRounding.AwayFromZero),
              Currency = string.IsNullOrWhiteSpace(shippingQuote.Currency) ? currency : shippingQuote.Currency,
              ApproxShippingDaysMin = days ?? 0,
              ApproxShippingDaysMax = days ?? 0
            });
          }
        }
      }

      return results.OrderBy(r => r.ShippingCost).ToList();
    }

    // --- OAuth (client_credentials) ---
    private static async Task<string> GetFedexAccessTokenAsync(string baseUrl, string clientId, string clientSecret)
    {
      using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/oauth/token");
      req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      req.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
        { "grant_type", "client_credentials" },
        { "client_id", clientId },
        { "client_secret", clientSecret }
      });

      using var res = await client.SendAsync(req);
      var body = await res.Content.ReadAsStringAsync();
      if (!res.IsSuccessStatusCode)
        throw new HttpRequestException($"FedEx OAuth error {(int)res.StatusCode}: {body}");

      using var doc = JsonDocument.Parse(body);
      return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new Exception("FedEx OAuth: access_token missing");
    }

    // --- Helpers ---
    private static string MapFedexShippingType(string carrierCode, string serviceType)
    {
      // Carrier: FDXG=Ground (incl. Home Delivery/International Ground), FDXE=Express (air)
      if (string.Equals(carrierCode, "FDXG", StringComparison.OrdinalIgnoreCase)) return "Ground";
      if (string.Equals(carrierCode, "FDXE", StringComparison.OrdinalIgnoreCase)) return "Air";

      var s = (serviceType ?? "").ToUpperInvariant();
      if (s.Contains("GROUND") || s.Contains("HOME_DELIVERY")) return "Ground";
      return "Air"; // everything else (PRIORITY_OVERNIGHT, 2_DAY, INTL_PRIORITY, etc.)
    }

    private static int? ParseFedexTransitDays(string? transit)
    {
      if (string.IsNullOrWhiteSpace(transit)) return null;
      // e.g., "TWO_DAYS", "THREE_DAYS", "ONE_DAY"
      var t = transit.ToUpperInvariant();
      if (t.Contains("ONE")) return 1;
      if (t.Contains("TWO")) return 2;
      if (t.Contains("THREE")) return 3;
      if (t.Contains("FOUR")) return 4;
      if (t.Contains("FIVE")) return 5;
      if (t.Contains("SIX")) return 6;
      if (t.Contains("SEVEN")) return 7;
      return null;
    }

    public async Task<List<ShippingReturnDto>> GeneratePurolatorShippingQuote(ShippingCreateDto shippingQuote)
    {
        // Purolator only handles Canadian domestic here
        if (!string.Equals(shippingQuote.CountryCode, "CA", StringComparison.OrdinalIgnoreCase))
            return new List<ShippingReturnDto>();

        // Get OAuth token
        var token = await GetPurolatorAccessTokenAsync(_purolatorUri, _purolatorClientId, _purolatorClientSecret);

        decimal weightKg = shippingQuote.Weight; // if grams, convert: / 1000m
        string CleanPostal(string s) => (s ?? "").Replace(" ", "").ToUpperInvariant();

        // Build XML QuickEstimate request
        // Available services: PurolatorExpress, PurolatorExpress9AM, PurolatorExpress10:30AM, PurolatorExpressEvening, PurolatorGround, PurolatorGround9AM, etc.
        var ns = XNamespace.Get("http://purolator.com/pws/datatypes/v2");
        var requestXml = new XDocument(
            new XElement(ns + "GetQuickEstimateRequest",
                new XElement(ns + "BillingAccountNumber", ""), // optional if just quoting
                new XElement(ns + "SenderPostalCode", CleanPostal(_originPostalCode)),
                new XElement(ns + "ReceiverPostalCode", CleanPostal(shippingQuote.PostalCode)),
                new XElement(ns + "PackageType", "ExpressBox"), // generic package type
                new XElement(ns + "TotalWeight",
                    new XElement(ns + "Value", weightKg.ToString("0.###", CultureInfo.InvariantCulture)),
                    new XElement(ns + "WeightUnit", "kg")
                ),
                new XElement(ns + "ShowAlternativeServicesIndicator", "true")
            )
        );

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_purolatorUri}/EWS/V2/Estimating/EstimatingService.asmx");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        req.Content = new StringContent(requestXml.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "application/xml");

        using var res = await client.SendAsync(req);
        var xml = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Purolator Estimating error {(int)res.StatusCode}: {xml}");

        var doc = XDocument.Parse(xml);
        var results = new List<ShippingReturnDto>();

        // Parse each Service in the reply
        foreach (var svc in doc.Descendants(ns + "Service"))
        {
            var code = svc.Element(ns + "ServiceID")?.Value ?? "";
            var name = svc.Element(ns + "Name")?.Value ?? code;
            var total = svc.Element(ns + "TotalPrice")?.Value ?? "0";
            decimal.TryParse(total, NumberStyles.Any, CultureInfo.InvariantCulture, out var price);

            // Transit days
            int? days = null;
            var estTransit = svc.Element(ns + "EstimatedTransitDays")?.Value;
            if (int.TryParse(estTransit, out var d)) days = d;

            results.Add(new ShippingReturnDto
            {
                ShippingMethod = ShippingMethod.Purolator,
                ShippingType = MapPurolatorShippingType(code, name),
                ShippingCost = (int)Math.Round(price * 100m, MidpointRounding.AwayFromZero),
                Currency = string.IsNullOrWhiteSpace(shippingQuote.Currency) ? "CAD" : shippingQuote.Currency,
                ApproxShippingDaysMin = days ?? 0,
                ApproxShippingDaysMax = days ?? 0
            });
        }

        return results.OrderBy(r => r.ShippingCost).ToList();
    }

    // --- OAuth for Purolator ---
    private static async Task<string> GetPurolatorAccessTokenAsync(string baseUrl, string clientId, string clientSecret)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/EWS/V1/OAuth/AccessToken");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string> {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "client_secret", clientSecret }
        });

        using var res = await client.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Purolator OAuth error {(int)res.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()
              ?? throw new Exception("Purolator OAuth: access_token missing");
    }

    // --- Service code → type mapping ---
    private static string MapPurolatorShippingType(string code, string name)
    {
        var n = (name ?? "").ToLowerInvariant();
        if (n.Contains("ground")) return "Ground";
        if (n.Contains("express")) return "Express";
        return "Unknown";
    }
  }
}