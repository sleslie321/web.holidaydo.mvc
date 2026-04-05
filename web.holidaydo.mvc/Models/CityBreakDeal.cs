namespace web.holidaydo.mvc.Models
{
    public sealed class CityBreakDeal
    {
        public decimal? DiscountPercentageIncludingAdminFee { get; set; }
        public string? UrlPath { get; set; }
        public string? Headline { get; set; }
        public string? Title { get; set; }
        public decimal? Price { get; set; }
    }
}