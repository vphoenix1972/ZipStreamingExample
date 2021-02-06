using System.IO;
using System.IO.Compression;
using System.Net.Http;
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
        private static readonly string[] Servers = {"localhost:5010", "localhost:5011"};

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
                endpoints.MapGet("/zip", async context =>
                {
                    var sizeInMb = int.Parse(context.Request.Query["size"][0]);

                    context.Features.Get<IHttpBodyControlFeature>().AllowSynchronousIO = true;

                    using var archive = new ZipArchive(context.Response.Body, ZipArchiveMode.Create, true);
                    foreach (var server in Servers)
                    {
                        var file = archive.CreateEntry(ReplaceInvalidChars(server));
                        await using var fileStream = file.Open();

                        await using var dataStream = await GetDataAsync(server, sizeInMb);
                        await dataStream.CopyToAsync(fileStream);
                    }
                });
            });
        }

        private async Task<Stream> GetDataAsync(string serverUrl, int size)
        {
            var httpClient = new HttpClient();

            var request = new HttpRequestMessage(HttpMethod.Get, $"http://{serverUrl}?size={size}");

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            return await response.Content.ReadAsStreamAsync();
        }

        private string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}