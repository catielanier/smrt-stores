using SmrtStores.Dtos;

namespace SmrtStores.Services
{
  public class ShippingService
  {
    private readonly string _canadaPostApiUsername;
    private readonly string _canadaPostApiPassword;
    private readonly string _upsApiKey;
    private readonly string _fedexApiKey;
    private readonly string _purolatorApiKey;
    private readonly string _originPostalCode;

    public ShippingService(IConfiguration configuration)
    {
      _canadaPostApiUsername = configuration["CANADA_POST_API_USERNAME"] ?? throw new Exception("no canada post api username");
      _canadaPostApiPassword = configuration["CANADA_POST_API_PASSWORD"] ?? throw new Exception("no canada post api password");
      _upsApiKey = configuration["UPS_API_KEY"] ?? throw new Exception("no ups api key");
      _fedexApiKey = configuration["FEDEX_API_KEY"] ?? throw new Exception("no fedex api key");
      _purolatorApiKey = configuration["PUROLATOR_API_KEY"] ?? throw new Exception("no purolator api key");
      _originPostalCode = configuration["ORIGIN_POSTAL_CODE"] ?? throw new Exception("no origin postal code");
    }

    private static HttpClient client = new HttpClient();

    public async Task<ShippingReturnDto> GenerateCanadaPostShippingQuote(ShippingCreateDto shippingQuote)
    {
      var plainTextBytes = System.Text.Encoding.UTF8.GetBytes($"{_canadaPostApiUsername}:{_canadaPostApiPassword}");
      var base64Token = System.Convert.ToBase64String(plainTextBytes);
      List<string> usServiceCodes = new List<string>
      {
        "USA.EP",
        "USA.TP",
        "USA.XP"
      };

      List<string> canadaServiceCodes = new List<string>
      {
        "DOM.RP",
        "DOM.EP",
        "DOM.XP"
      };

      List<string> intlServiceCodes = new List<string>
      {
        "INT.XP",
        "INT.IP.AIR",
        "INT.TP"
      };
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