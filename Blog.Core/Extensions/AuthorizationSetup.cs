﻿using Blog.Core.AuthHelper;
using Blog.Core.Common;
using Blog.Core.Common.AppConfig;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Blog.Core.Extensions
{
    /// <summary>
    /// Db 启动服务
    /// </summary>
    public static class AuthorizationSetup
    {
        public static void AddAuthorizationSetup(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // 简单角色授权版本
            // [Authorize(Roles = "Admin,System")]


            // 多角色授权策略 [Authorize(Policy = "Admin")]
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Client", policy => policy.RequireRole("Client").Build());
                options.AddPolicy("Admin", policy => policy.RequireRole("Admin").Build());
                options.AddPolicy("SystemOrAdmin", policy => policy.RequireRole("Admin", "System"));
                options.AddPolicy("A_S_O", policy => policy.RequireRole("Admin", "System", "Others"));
            });


            #region 参数
            //读取配置文件
            var symmetricKeyAsBase64 = AppSecretConfig.Audience_Secret_String;
            var keyByteArray = Encoding.ASCII.GetBytes(symmetricKeyAsBase64);
            var signingKey = new SymmetricSecurityKey(keyByteArray);
            var Issuer = Appsettings.app(new string[] { "Audience", "Issuer" });
            var Audience = Appsettings.app(new string[] { "Audience", "Audience" });

            var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            // 如果要数据库动态绑定，这里先留个空，后边处理器里动态赋值
            var permission = new List<PermissionItem>();

            // 角色与接口的权限要求参数
            var permissionRequirement = new PermissionRequirement(
                "/api/denied",// 拒绝授权的跳转地址（目前无用）
                permission,
                ClaimTypes.Role,//基于角色的授权
                Issuer,//发行人
                Audience,//听众
                signingCredentials,//签名凭据
                expiration: TimeSpan.FromSeconds(60 * 60)//接口的过期时间
                );


            // 令牌验证参数
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidIssuer = Issuer,//发行人
                ValidateAudience = true,
                ValidAudience = Audience,//订阅人
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                RequireExpirationTime = true,
            };

            #endregion


            // 3、复杂的策略授权
            services.AddAuthorization(options =>
            {
                options.AddPolicy(Permissions.Name,
                         policy => policy.Requirements.Add(permissionRequirement));
            });


            //【认证】
            services.AddAuthentication(o =>
            {
                o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = nameof(ApiResponseHandler);
                o.DefaultForbidScheme = nameof(ApiResponseHandler);
            })

             // ids4和jwt切换

             // 1.添加JwtBearer认证服务
             //.AddJwtBearer(o =>
             //{
             //    o.TokenValidationParameters = tokenValidationParameters;
             //    o.Events = new JwtBearerEvents
             //    {
             //        OnChallenge = context =>
             //        {
             //            context.Response.Headers.Add("Token-Error", context.ErrorDescription);
             //            return Task.CompletedTask;
             //        },
             //        OnAuthenticationFailed = context =>
             //        {
             //            var token = context.Request.Headers["Authorization"].ObjToString().Replace("Bearer ", "");
             //            var jwtToken = (new JwtSecurityTokenHandler()).ReadJwtToken(token);

             //            if (jwtToken.Issuer != Issuer)
             //            {
             //                context.Response.Headers.Add("Token-Error-Iss", "issuer is wrong!");
             //            }

             //            if (jwtToken.Audiences.FirstOrDefault() != Audience)
             //            {
             //                context.Response.Headers.Add("Token-Error-Aud", "Audience is wrong!");
             //            }


             //            // 如果过期，则把<是否过期>添加到，返回头信息中
             //            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
             //            {
             //                context.Response.Headers.Add("Token-Expired", "true");
             //            }
             //            return Task.CompletedTask;
             //        }
             //    };
             //})

             // 2.添加Identityserver4认证
             .AddIdentityServerAuthentication(options =>
             {
                 options.Authority = Appsettings.app(new string[] { "Startup", "IdentityServer4", "AuthorizationUrl" });
                 options.RequireHttpsMetadata = false;
                 options.ApiName = "blog.core.api";
                 options.SupportedTokens = IdentityServer4.AccessTokenValidation.SupportedTokens.Jwt;
                 options.ApiSecret = "api_secret";

             })
             .AddScheme<AuthenticationSchemeOptions, ApiResponseHandler>(nameof(ApiResponseHandler), o => { });



            // 这里冗余写了一次,因为很多人看不到
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            // 注入权限处理器
            services.AddScoped<IAuthorizationHandler, PermissionHandler>();
            services.AddSingleton(permissionRequirement);
        }
    }
}
