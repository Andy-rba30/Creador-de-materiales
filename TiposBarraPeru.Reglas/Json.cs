using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiposBarraPeru.Reglas
{
    /// <summary>
    /// Opciones de System.Text.Json compartidas por config.json (barras) y
    /// materiales.json (concreto): camelCase, comentarios y comas finales al leer,
    /// y escritura legible sin escapar "Ø", comillas de pulgada ni acentos.
    /// </summary>
    internal static class Json
    {
        public static JsonSerializerOptions Lectura() => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static JsonSerializerOptions Escritura() => new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}
