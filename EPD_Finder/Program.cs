using EPD_Finder.Services;
using EPD_Finder.Services.IServices;
using System.Net;

namespace EPD_Finder
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var timeout = TimeSpan.FromSeconds(3);
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
            //HttpMessageHandler CreateHandler() => new HttpClientHandler
            //{
            //    AllowAutoRedirect = true
            //};
            HttpMessageHandler CreateHandler() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 50,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            };
            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpClient<IEpdService, EpdService>(c =>
            {
                c.Timeout = timeout;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }).ConfigurePrimaryHttpMessageHandler(CreateHandler);

            builder.Services.AddHttpClient<AhlsellSearch>(c =>
            {
                c.Timeout = timeout;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }).ConfigurePrimaryHttpMessageHandler(CreateHandler);

            builder.Services.AddHttpClient<EnummersokSearch>(c =>
            {
                c.Timeout = timeout;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }).ConfigurePrimaryHttpMessageHandler(CreateHandler);

            builder.Services.AddHttpClient<SolarSearch>(c =>
            {
                c.Timeout = timeout;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }).ConfigurePrimaryHttpMessageHandler(CreateHandler);

            builder.Services.AddHttpClient<SoneparSearch>(c =>
            {
                c.Timeout = timeout;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }).ConfigurePrimaryHttpMessageHandler(CreateHandler);

            builder.Services.AddHttpClient<RexelSearch>(c =>
            {
                c.Timeout = timeout;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }).ConfigurePrimaryHttpMessageHandler(CreateHandler);

            //var cookieContainer = new CookieContainer();
            //builder.Services.AddSingleton(cookieContainer);
            //builder.Services.AddHttpClient<OnninenSearch>(c =>
            //{
            //    c.Timeout = timeout;
            //    c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            //    c.DefaultRequestHeaders.Referrer = new Uri("https://www.onninen.se/");
            //}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            //{
            //    CookieContainer = cookieContainer,
            //    AllowAutoRedirect = true,
                
                
            //});
            builder.Services.AddHttpClient<Schneider>(c =>
            {
                c.Timeout = timeout;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }).ConfigurePrimaryHttpMessageHandler(CreateHandler);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
