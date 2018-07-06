﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.WebEncoders;
using Microsoft.AspNetCore.Mvc;
using COMCMS.Common;
using COMCMS.Web.Models;
using COMCMS.Web.Common;
using COMCMS.Web.ExceptionHandler;
using Lib.Core.MiddlewareExtension.Extension;
using Senparc.Weixin.Entities;
using Senparc.CO2NET;
using Senparc.CO2NET.RegisterServices;
using Senparc.Weixin;
using Senparc.CO2NET.Cache;
using System.Configuration;
using Microsoft.Extensions.Options;

namespace COMCMS.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //注入自己的HttpContext
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            //services.AddMvc().AddSessionStateTempDataProvider();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                //options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMyHttpContextAccessor();

            services.AddDistributedMemoryCache();
            //添加Session 服务
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromSeconds(120);
                options.Cookie.HttpOnly = true;
                
            });
            //部分系统配置
            services.Configure<SystemSetting>(Configuration.GetSection("SystemSetting"));

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1); ;
            //防止汉字被自动编码
            services.Configure<WebEncoderOptions>(options =>
            {
                options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
            });
            //记录错误
            services.AddMvc(options =>
            {
                options.Filters.Add<HttpGlobalExceptionFilter>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider svp, IOptions<SenparcSetting> senparcSetting, IOptions<SenparcWeixinSetting> senparcWeixinSetting)
        {
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
                await next();
            });

            if (env.IsDevelopment())
            {

                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            

            //其他错误页面处理
            app.UseStatusCodePagesWithReExecute("/StatusCode/{0}");
            //启用Session
            app.UseSession();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseStaticHttpContext();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                  name: "areas",
                  template: "{area:exists}/{controller=Index}/{action=Index}/{id?}"
                );

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");


            });

            //使用环境变量
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseMiddlewareExtension(new ResultExceptionHandler());
            RegisterService.Start(env, senparcSetting.Value)
                   .UseSenparcWeixin(senparcWeixinSetting.Value, false/*此处为单独用于微信的调试状态*/, () => GetExContainerCacheStrategies());
            //加入HttpContext
            //MyHttpContext.ServiceProvider = svp;
        }

        #region Senparc 缓存扩展策略
        /// <summary>
        /// 获取Container扩展缓存策略
        /// </summary>
        /// <returns></returns>
        private IList<IDomainExtensionCacheStrategy> GetExContainerCacheStrategies()
        {
            var exContainerCacheStrategies = new List<IDomainExtensionCacheStrategy>();

            //如果有配置，可以去掉下面注释

            ////判断Redis是否可用
            //var redisConfiguration = ConfigurationManager.AppSettings["Cache_Redis_Configuration"];
            //if ((!string.IsNullOrEmpty(redisConfiguration) && redisConfiguration != "Redis配置"))
            //{
            //    exContainerCacheStrategies.Add(RedisContainerCacheStrategy.Instance);
            //}

            ////判断Memcached是否可用
            //var memcachedConfiguration = ConfigurationManager.AppSettings["Cache_Memcached_Configuration"];
            //if ((!string.IsNullOrEmpty(memcachedConfiguration) && memcachedConfiguration != "Memcached配置"))
            //{
            //    exContainerCacheStrategies.Add(MemcachedContainerCacheStrategy.Instance);
            //}

            //也可扩展自定义的缓存策略

            return exContainerCacheStrategies;
        }
        #endregion
    }
}
