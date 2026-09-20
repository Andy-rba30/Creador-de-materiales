using System;

namespace TiposBarraPeru.Reglas
{
    /// <summary>
    /// Formato de nombre de los tipos de barra, fijo y documentado en el README:
    ///   nombre = prefijo + nombre del catalogo
    /// Con el prefijo por defecto "Ø" salen: Ø6mm, Ø8mm, Ø3/8", Ø12mm, Ø1/2", Ø5/8",
    /// Ø3/4", Ø1", Ø1 3/8". Son los nombres que usa el add-in RetainingWallRebar en
    /// su config.json (barTypeName).
    /// </summary>
    public static class Nombres
    {
        /// <summary>"Ø" (U+00D8). Se escribe como escape para no depender de la codificacion del archivo.</summary>
        public const string PrefijoPorDefecto = "Ø";

        /// <summary>Comilla de pulgada tal como aparece en el catalogo.</summary>
        public const string SimboloPulgadaCatalogo = "\"";

        /// <summary>Nombre del tipo para una entrada del catalogo.</summary>
        public static string Generar(string prefijo, string nombreCatalogo, string simboloPulgada = SimboloPulgadaCatalogo)
        {
            string n = (nombreCatalogo ?? "").Trim();
            if (simboloPulgada != null && simboloPulgada != SimboloPulgadaCatalogo)
                n = n.Replace(SimboloPulgadaCatalogo, simboloPulgada);
            return (prefijo ?? "") + n;
        }

        /// <summary>Prefijo por defecto de los materiales de concreto: "Concreto f'c " + f'c.</summary>
        public const string PrefijoConcretoPorDefecto = "Concreto f'c ";

        /// <summary>f'c como texto de catalogo, con punto decimal y sin ceros sobrantes: 210 -> "210", 212.5 -> "212.5".</summary>
        public static string NombreCatalogoConcreto(double fcKgCm2) =>
            fcKgCm2.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Nombre del material de concreto: prefijo + f'c ("Concreto f'c 210").</summary>
        public static string GenerarConcreto(string prefijo, double fcKgCm2) => (prefijo ?? "") + NombreCatalogoConcreto(fcKgCm2);

        /// <summary>Comparacion de nombres de tipo: sin distinguir mayusculas ni espacios en los extremos.</summary>
        public static bool Iguales(string a, string b) =>
            string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
