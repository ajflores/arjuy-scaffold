namespace ArjuyScaffold.Support;

/// <summary>Source of the ApiControllerBase that maps MResult&lt;T&gt;.ErrorType to HTTP status codes.</summary>
public static class ApiControllerBaseTemplate
{
    public static string RelativePath(string namespaceRoot)
    {
        return FeaturePaths.ApiConfig(namespaceRoot, "ApiControllerBase").Replace("ConfigServices", "Controllers");
    }

    public static string Build(string namespaceRoot)
    {
        return "using " + namespaceRoot + ".Domain.Common;\n"
            + "using Microsoft.AspNetCore.Mvc;\n\n"
            + "namespace " + namespaceRoot + ".Api.Controllers;\n\n"
            + "/// <summary>\n"
            + "/// Clase base de los controllers de la API. Convierte un MResult&lt;T&gt; en la respuesta HTTP según su\n"
            + "/// ErrorType (400/401/403/404/409/423/500); el cuerpo es SIEMPRE el MResult. Nunca escribir a mano\n"
            + "/// 'return Ok(result)' para un resultado fallido.\n"
            + "/// </summary>\n"
            + "[ApiController]\n"
            + "public abstract class ApiControllerBase : ControllerBase\n"
            + "{\n"
            + "    protected IActionResult ToActionResult<T>(MResult<T> result)\n"
            + "    {\n"
            + "        if (result.IsSuccess)\n"
            + "        {\n"
            + "            return Ok(result);\n"
            + "        }\n\n"
            + "        return StatusCode(StatusCodeFor(result.ErrorType), result);\n"
            + "    }\n\n"
            + "    protected static int StatusCodeFor(ErrorType errorType)\n"
            + "    {\n"
            + "        return errorType switch\n"
            + "        {\n"
            + "            ErrorType.Validation => StatusCodes.Status400BadRequest,\n"
            + "            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,\n"
            + "            ErrorType.Forbidden => StatusCodes.Status403Forbidden,\n"
            + "            ErrorType.NotFound => StatusCodes.Status404NotFound,\n"
            + "            ErrorType.Conflict => StatusCodes.Status409Conflict,\n"
            + "            ErrorType.Locked => StatusCodes.Status423Locked,\n"
            + "            _ => StatusCodes.Status500InternalServerError\n"
            + "        };\n"
            + "    }\n"
            + "}\n";
    }
}
