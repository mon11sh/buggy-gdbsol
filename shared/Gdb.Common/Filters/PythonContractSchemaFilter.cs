using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Gdb.Common.Filters;

public class PythonContractSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // FastAPI adds titles to all schemas
        if (string.IsNullOrEmpty(schema.Title) && context.Type != null)
        {
            schema.Title = context.Type.Name;
        }

        // FastAPI represents nullable fields as anyOf: [type, null]
        if (schema.Nullable)
        {
            schema.Nullable = false;
            schema.AnyOf = new List<OpenApiSchema>
            {
                new OpenApiSchema { Type = schema.Type ?? "string" },
                new OpenApiSchema { Type = "null" }
            };
            schema.Type = null;
        }

        // Remove properties that are auto-generated but missing in Python
        if (schema.Properties != null)
        {
            foreach (var prop in schema.Properties.Values)
            {
                if (prop.Nullable)
                {
                    prop.Nullable = false;
                    prop.AnyOf = new List<OpenApiSchema>
                    {
                        new OpenApiSchema { Type = prop.Type ?? "string" },
                        new OpenApiSchema { Type = "null" }
                    };
                    prop.Type = null;
                }
            }
        }
    }
}
