using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DataServer
{
    public class Startup
    {
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
                    // Генерируем поток из произвольных байтов

                    var sizeInMb = int.Parse(context.Request.Query["size"][0]);

                    var bytes = new byte[1024 * 1024];
                    var random = new Random();
                    for (var i = 0; i < sizeInMb; i++)
                    {
                        random.NextBytes(bytes);
                        await context.Response.BodyWriter.WriteAsync(bytes);
                    }
                });

                endpoints.MapPost("/", async context =>
                {
                    // Просто вычитываем весь запрос

                    var buffer = new byte[1024 * 1024];
                    int bytesRead;
                    do
                    {
                        bytesRead = await context.Request.Body.ReadAsync(buffer);
                    } while (bytesRead > 0);
                });
            });
        }
    }
}