namespace web.holidaydo.mvc.Models
{
    public sealed class SearchProductsResponse
    {
        public List<Product> Products { get; set; } = [];
    }

    public sealed class Product
    {
        public string? Title { get; set; }
        public string? ProductUrl { get; set; }
        public List<string>? Flags { get; set; }
        public List<ProductImage>? Images { get; set; }
        public ProductReviews? Reviews { get; set; }
        public ProductDuration? Duration { get; set; }
        public ProductPricing? Pricing { get; set; }
    }

    public sealed class ProductImage
    {
        public List<ImageVariant>? Variants { get; set; }
    }

    public sealed class ImageVariant
    {
        public string? Url { get; set; }
    }

    public sealed class ProductReviews
    {
        public double? CombinedAverageRating { get; set; }
        public int? TotalReviews { get; set; }
    }

    public sealed class ProductDuration
    {
        public int? FixedDurationInMinutes { get; set; }
        public int? VariableDurationFromMinutes { get; set; }
        public int? VariableDurationToMinutes { get; set; }
    }

    public sealed class ProductPricing
    {
        public PricingSummary? Summary { get; set; }
    }

    public sealed class PricingSummary
    {
        public decimal? FromPrice { get; set; }
    }
}