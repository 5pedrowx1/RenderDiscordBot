using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace RenderDiscordBot
{
    public class HttpServer
    {
        private readonly int _port;

        public HttpServer()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("PORT"), out _port))
                _port = 8080;
        }

        public async Task StartAsync()
        {
            try
            {
                var options = new WebApplicationOptions
                {
                    WebRootPath = "wwwroot"
                };

                var builder = WebApplication.CreateBuilder(options);
                builder.Services.AddRouting();

                var app = builder.Build();
                app.UseStaticFiles();
                app.UseRouting();

                app.MapGet("/", context => {
                    context.Response.Redirect("/index.html");
                    return Task.CompletedTask;
                });

                Console.WriteLine($"Servidor HTTP iniciado na porta: {_port}");
                await app.RunAsync($"http://0.0.0.0:{_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao iniciar o servidor HTTP: " + ex.Message);
            }
        }
    }
}
