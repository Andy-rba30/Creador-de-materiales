using System.Globalization;
using System.Text.Json.Serialization;

namespace TiposBarraPeru.Reglas
{
    /// <summary>
    /// Una resistencia del catalogo de concreto (materiales.json). f'c en kg/cm2 y
    /// densidad en kg/m3; el nombre del material sale de f'c (prefijo + f'c), por
    /// eso no lleva campo de nombre.
    /// </summary>
    public class ConcretoCatalogo
    {
        /// <summary>Resistencia especificada a compresion f'c (kg/cm2).</summary>
        public double FcKgCm2 { get; set; }

        /// <summary>Peso unitario wc (kg/m3). 2400 = concreto armado de peso normal.</summary>
        public double DensidadKgM3 { get; set; } = ReglasConcretoCfg.DensidadNormalPorDefecto;

        /// <summary>Lo que va tras el prefijo: f'c sin decimales sobrantes ("210", "212.5").</summary>
        [JsonIgnore]
        public string NombreCatalogo => Nombres.NombreCatalogoConcreto(FcKgCm2);

        public ConcretoCatalogo() { }
        public ConcretoCatalogo(double fcKgCm2, double densidadKgM3 = ReglasConcretoCfg.DensidadNormalPorDefecto)
        {
            FcKgCm2 = fcKgCm2;
            DensidadKgM3 = densidadKgM3;
        }

        public ConcretoCatalogo Clonar() => (ConcretoCatalogo)MemberwiseClone();
    }
}
