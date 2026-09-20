using System;

namespace TiposBarraPeru.Reglas
{
    /// <summary>
    /// Propiedades geometricas de una barra redonda a partir de su diametro nominal,
    /// para rellenar area y peso de los diametros que no estan en el catalogo.
    /// El usuario puede corregirlos a mano si el catalogo del fabricante da otro valor.
    /// </summary>
    public static class PropiedadesBarra
    {
        /// <summary>Densidad del acero (kg/dm3).</summary>
        public const double DensidadAceroKgDm3 = 7.85;

        /// <summary>Area de la seccion: pi * d^2 / 4, con d en mm, resultado en cm2.</summary>
        public static double AreaCm2(double diametroMm)
        {
            if (diametroMm <= 0) return 0;
            return Math.PI * diametroMm * diametroMm / 4.0 / 100.0;
        }

        /// <summary>
        /// Peso unitario en kg/m a partir del area en cm2:
        /// area (cm2) / 100 = dm2; por 7.85 kg/dm3 = kg/dm; por 10 dm/m = kg/m.
        /// Es decir, kg/m = area_cm2 * 0.785.
        /// </summary>
        public static double PesoKgM(double areaCm2)
        {
            if (areaCm2 <= 0) return 0;
            return areaCm2 / 100.0 * DensidadAceroKgDm3 * 10.0;
        }

        /// <summary>Peso unitario (kg/m) directamente desde el diametro (mm).</summary>
        public static double PesoKgMDesdeDiametro(double diametroMm) => PesoKgM(AreaCm2(diametroMm));

        /// <summary>Redondeo para presentar area (2 decimales) y peso (3 decimales) como en el catalogo.</summary>
        public static double RedondearArea(double areaCm2) => Math.Round(areaCm2, 2, MidpointRounding.AwayFromZero);
        public static double RedondearPeso(double pesoKgM) => Math.Round(pesoKgM, 3, MidpointRounding.AwayFromZero);
    }
}
