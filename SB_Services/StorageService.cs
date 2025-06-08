using Supabase;
using Supabase.Storage;

namespace EReaderApp.Services
{
    public class StorageService
    {
        private readonly Supabase.Client _supabase;
        private const string BOOKS_BUCKET = "books";
        private const string IMAGES_BUCKET = "images";

        public StorageService(IConfiguration configuration)
        {
            var url = configuration["Supabase:Url"];
            var key = configuration["Supabase:AnonKey"];

            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            _supabase = new Supabase.Client(url, key, options);
            _supabase.InitializeAsync().Wait();
        }

        public async Task<string> UploadPdfAsync(IFormFile file, string fileName)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();

                var path = $"pdfs/{Guid.NewGuid()}_{fileName}";

                await _supabase.Storage
                    .From(BOOKS_BUCKET)
                    .Upload(bytes, path);

                // Return public URL
                return _supabase.Storage
                    .From(BOOKS_BUCKET)
                    .GetPublicUrl(path);
            }
            catch (Exception ex)
            {
                // Log error and return local path as fallback
                Console.WriteLine($"Error uploading to Supabase: {ex.Message}");
                return null;
            }
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();

                var path = $"{folder}/{Guid.NewGuid()}_{file.FileName}";

                await _supabase.Storage
                    .From(IMAGES_BUCKET)
                    .Upload(bytes, path);

                return _supabase.Storage
                    .From(IMAGES_BUCKET)
                    .GetPublicUrl(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading image to Supabase: {ex.Message}");
                return null;
            }
        }
    }
}