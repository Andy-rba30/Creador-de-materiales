namespace TiposBarraPeru.Reglas
{
    /// <summary>
    /// Un tamano de barra del catalogo (config.json). Los valores estan en mm,
    /// cm2 y kg/m; la conversion a unidades internas de Revit la hace el add-in.
    /// </summary>
    public class BarraCatalogo
    {
        /// <summary>Nombre del catalogo tal cual se escribe en Peru: "6mm", "3/8\"", "1 3/8\"".</summary>
        public string Nombre { get; set; } = "";

        /// <summary>Diametro nominal (mm).</summary>
        public double DiametroMm { get; set; }

        /// <summary>Area nominal de la seccion (cm2). Solo informativa (tabla y README).</summary>
        public double AreaCm2 { get; set; }

        /// <summary>Peso unitario (kg/m). Se escribe en BarMassPerUnitLength del tipo.</summary>
        public double PesoKgM { get; set; }

        /// <summary>true = corrugada (Deformed), false = lisa (Plain).</summary>
        public bool Corrugada { get; set; } = true;

        public BarraCatalogo Clonar() => (BarraCatalogo)MemberwiseClone();
    }
}
