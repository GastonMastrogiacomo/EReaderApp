using Supabase;
using Supabase.Storage;

namespace EReaderApp.Services
{
    public class StorageService
    {
        private readonly Supabase.Client? _supabase;
        private readonly ILogger<StorageService> _logger;
        private const string BOOKS_BUCKET = "books";
        private const string IMAGES_BUCKET = "images";

        public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
        {
            _logger = logger;

            try
            {
                // Get Supabase configuration from environment or appsettings
                var url = Environment.GetEnvironmentVariable("SUPABASE_URL")
                    ?? configuration["Supabase:Url"];
                var key = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")
                    ?? configuration["Supabase:AnonKey"];

                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
                {
                    _logger.LogWarning("Supabase configuration is missing. Storage service will use local fallback.");
                    _supabase = null;
                    return;
                }

                var options = new Supabase.SupabaseOptions
                {
                    AutoConnectRealtime = false // Disable realtime for better performance
                };

                _supabase = new Supabase.Client(url, key, options);
                _supabase.InitializeAsync().Wait();

                _logger.LogInformation("Supabase storage service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Supabase storage service. Will use local fallback.");
                _supabase = null;
            }
        }

        public async Task<string?> UploadPdfAsync(IFormFile file, string fileName)
        {
            if (_supabase == null)
            {
                _logger.LogWarning("Supabase not available, skipping cloud upload for PDF");
                return null;
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();

                var path = $"pdfs/{Guid.NewGuid()}_{fileName}";

                await _supabase.Storage
                    .From(BOOKS_BUCKET)
                    .Upload(bytes, path);

                var publicUrl = _supabase.Storage
                    .From(BOOKS_BUCKET)
                    .GetPublicUrl(path);

                _logger.LogInformation("Successfully uploaded PDF to Supabase: {Path}", path);
                return publicUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading PDF to Supabase: {FileName}", fileName);
                return null;
            }
        }

        public async Task<string?> UploadImageAsync(IFormFile file, string folder)
        {
            if (_supabase == null)
            {
                _logger.LogWarning("Supabase not available, skipping cloud upload for image");
                return null;
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();

                var path = $"{folder}/{Guid.NewGuid()}_{file.FileName}";

                await _supabase.Storage
                    .From(IMAGES_BUCKET)
                    .Upload(bytes, path);

                var publicUrl = _supabase.Storage
                    .From(IMAGES_BUCKET)
                    .GetPublicUrl(path);

                _logger.LogInformation("Successfully uploaded image to Supabase: {Path}", path);
                return publicUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image to Supabase: {FileName}", file.FileName);
                return null;
            }
        }
    }
}