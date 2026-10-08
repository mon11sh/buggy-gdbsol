using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

class Program
{
    static void Main()
    {
        string[] services = { "AccountsService", "TransactionsService", "UsersService", "AuthService", "AadharService", "CompanyCrvService", "NotificationService", "CentralPaymentGatewayService", "RegistryService", "CentralGatewayService" };

        foreach (var svc in services)
        {
            var programPath = Path.Combine(svc, "Program.cs");
            if (File.Exists(programPath))
            {
                var content = File.ReadAllText(programPath);
                
                // Add filter to AddControllers
                if (!content.Contains("FastApiValidationFilter"))
                {
                    content = content.Replace("builder.Services.AddControllers();", 
                        "builder.Services.AddControllers(options => options.Filters.Add<Gdb.Common.Filters.FastApiValidationFilter>());\nbuilder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);");
                }

                // Add annotations to Swagger
                if (!content.Contains("EnableAnnotations"))
                {
                    content = content.Replace("builder.Services.AddSwaggerGen();", 
                        "builder.Services.AddSwaggerGen(c => { c.EnableAnnotations(); c.SchemaFilter<Gdb.Common.Filters.PythonContractSchemaFilter>(); });");
                }

                File.WriteAllText(programPath, content);
            }
        }
    }
}
