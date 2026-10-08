using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CentralGatewayService.Config;

public class ProxyDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var pathItem = new OpenApiPathItem();
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter { Name = "service", In = ParameterLocation.Path, Required = true, Schema = new OpenApiSchema { Type = "string" } },
                new OpenApiParameter { Name = "path", In = ParameterLocation.Path, Required = true, Schema = new OpenApiSchema { Type = "string" } }
            }
        };
        pathItem.AddOperation(OperationType.Get, operation);
        pathItem.AddOperation(OperationType.Post, operation);
        pathItem.AddOperation(OperationType.Put, operation);
        pathItem.AddOperation(OperationType.Delete, operation);
        pathItem.AddOperation(OperationType.Patch, operation);
        pathItem.AddOperation(OperationType.Options, operation);

        swaggerDoc.Paths.Add("/{service}/{path}", pathItem);
    }
}
