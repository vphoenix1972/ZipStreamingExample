using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ZippingServer
{
    public class Startup
    {
        private static readonly string[] DataServers = {"localhost:5010", "localhost:5011"};

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    // Получаем требуемый размер данных для генерации
                    var sizeInMb = int.Parse(context.Request.Query["size"][0]);

                    // Разрешаем synchronous IO, так как ZipArhive пока не умеет с ним работать
                    context.Features.Get<IHttpBodyControlFeature>().AllowSynchronousIO = true;

                    // Создаем ZipArchive, указывая в качестве выходного потока Response.Body
                    using var archive = new ZipArchive(context.Response.Body, ZipArchiveMode.Create, true);
                    foreach (var server in DataServers)
                    {
                        // Создаем запись в архиве
                        var file = archive.CreateEntry(ReplaceInvalidChars(server));
                        await using var fileStream = file.Open();

                        // Получаем поток запроса на DataServer и пишем в архив напрямую, без буферизации
                        await using var dataStream = await GetDataAsync(server, sizeInMb);
                        await dataStream.CopyToAsync(fileStream);
                    }
                });

                endpoints.MapPost("/", async context =>
                {
                    // Создаем временный файл и копируем туда запрос, иначе ZipArchive поместит все тело запроса в память
                    // из-за того что Request.Body не Seekable
                    // https://github.com/dotnet/runtime/issues/32577
                    await using var tempFile = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.Delete, 1024 * 16,
                        FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan);
                    await context.Request.BodyReader.CopyToAsync(tempFile);
                    tempFile.Seek(0, SeekOrigin.Begin);

                    using var archive = new ZipArchive(tempFile, ZipArchiveMode.Read, true);

                    foreach (var server in DataServers)
                    {
                        // Открываем запись в архиве
                        var file = archive.GetEntry(ReplaceInvalidChars(server));
                        await using var fileStream = file.Open();

                        // и отправляем ее серверу через потоки
                        await SendDataAsync(server, fileStream);
                    }
                });
            });
        }

        private async Task<Stream> GetDataAsync(string serverUrl, int size)
        {
            var httpClient = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Get, $"http://{serverUrl}?size={size}");

            // Получаем данные с сервера с указанием HttpCompletionOption.ResponseHeadersRead,
            // чтобы httpClient не буферизовал ответ в памяти.
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            return await response.Content.ReadAsStreamAsync();
        }

        private async Task SendDataAsync(string serverUrl, Stream stream)
        {
            var httpClient = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Post, $"http://{serverUrl}");

            var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content = content;

            await httpClient.SendAsync(request);
        }

        private string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}