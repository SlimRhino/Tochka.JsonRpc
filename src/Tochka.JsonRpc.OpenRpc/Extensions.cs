using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using Tochka.JsonRpc.ApiExplorer;
using Tochka.JsonRpc.Common;
using Tochka.JsonRpc.OpenRpc.Models;
using Tochka.JsonRpc.Server.Settings;

namespace Tochka.JsonRpc.OpenRpc
{
    public static class Extensions
    {
        public static IServiceCollection AddOpenRpc(this IServiceCollection services, Assembly xmldocAssembly, Action<OpenRpcOptions> configureOptions = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException();
            }

            services.TryAddSingleton<ITypeEmitter, TypeEmitter>();
            services.AddSingleton<IStartupFilter, OpenRpcStartupFilter>();
            services.TryAddTransient<ContentDescriptorGenerator>();
            services.TryAddTransient<OpenRpcGenerator>();
            if (services.All(x => x.ImplementationType != typeof(JsonRpcDescriptionProvider)))
            {
                // add by interface if not present
                services.AddTransient<IApiDescriptionProvider, JsonRpcDescriptionProvider>();
            }

            services.Configure(configureOptions ?? (options => { }));

            var xmlFile = $"{xmldocAssembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (!File.Exists(xmlPath))
            {
                // sanity check to enforce users set up their projects properly
                throw new FileNotFoundException("OpenRpc requires generated XMLdoc file! Add <GenerateDocumentationFile>true</GenerateDocumentationFile> to your csproj or disable OpenRpc integration", xmlPath);
            }

            return services;
        }

        public static IServiceCollection AddDefaultOpenRpcDocument(this IServiceCollection services, Assembly xmldocAssembly)
        {
            services.Configure<OpenRpcOptions>(options =>
            {
                var title = xmldocAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? string.Empty;  // returns assembly name, not what Rider shows in Csproj>Properties>Nuget>Title
                var description = xmldocAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
                options.Docs.Add(JsonRpcConstants.ApiDocumentName, new OpenApiInfo()
                {
                    Title = title,
                    Description = description,
                    Version = ApiExplorerConstants.DefaultApiVersion
                });
            });
            return services;
        }

        internal static bool IsObsoleteTransitive(this ApiDescription description)
        {
            var method = (description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
            var methodAttr = method?.GetCustomAttribute<ObsoleteAttribute>();
            var typeAttr = method?.DeclaringType?.GetCustomAttribute<ObsoleteAttribute>();
            return (methodAttr ?? typeAttr) != null;

        }

        internal static MethodObjectParamStructure ToParamStructure(this BindingStyle bindingStyle)
        {
            switch (bindingStyle)
            {
                case BindingStyle.Default:
                    return MethodObjectParamStructure.Either;
                case BindingStyle.Object:
                    return MethodObjectParamStructure.ByName;
                case BindingStyle.Array:
                    return MethodObjectParamStructure.ByPosition;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bindingStyle), bindingStyle, null);
            }
        }
    }
}
