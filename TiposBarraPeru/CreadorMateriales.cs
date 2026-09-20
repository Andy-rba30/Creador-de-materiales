using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using TiposBarraPeru.Reglas;

namespace TiposBarraPeru
{
    /// <summary>Lo que el usuario eligio en la ventana de materiales.</summary>
    public class OpcionesMateriales
    {
        public string Prefijo;
        public bool ActualizarExistentes;
        /// <summary>Filas de la tabla con la casilla "crear" ya resuelta.</summary>
        public List<FilaPlanConcreto> Filas = new List<FilaPlanConcreto>();
    }

    /// <summary>Tramas y apariencia de referencia encontradas en el proyecto (o no).</summary>
    public class RecursosProyecto
    {
        public ElementId TramaCorte = ElementId.InvalidElementId;
        public string NombreTramaCorte;
        public ElementId TramaSuperficie = ElementId.InvalidElementId;
        public string NombreTramaSuperficie;
        public ElementId Apariencia = ElementId.InvalidElementId;
        /// <summary>Material del proyecto del que se copia la apariencia.</summary>
        public string MaterialApariencia;

        public bool HayTramaCorte => TramaCorte != null && TramaCorte != ElementId.InvalidElementId;
        public bool HayTramaSuperficie => TramaSuperficie != null && TramaSuperficie != ElementId.InvalidElementId;
        public bool HayApariencia => Apariencia != null && Apariencia != ElementId.InvalidElementId;

        /// <summary>Lineas para la ventana y el resumen.</summary>
        public List<string> Describir(ConfiguracionMateriales cfg)
        {
            var l = new List<string>
            {
                "Trama de corte: " + (HayTramaCorte ? "\"" + NombreTramaCorte + "\"" : "no encontrada (" + string.Join(", ", cfg.TramaCorte) + "); se crean sin trama de corte."),
                "Trama de superficie: " + (HayTramaSuperficie ? "\"" + NombreTramaSuperficie + "\"" : "no encontrada (" + string.Join(", ", cfg.TramaSuperficie) + "); se crean sin trama de superficie."),
                "Apariencia: " + (HayApariencia
                    ? (cfg.DuplicarApariencia ? "copia de la del material \"" : "la misma que el material \"") + MaterialApariencia + "\""
                    : "ningun material de referencia (" + string.Join(", ", cfg.MaterialApariencia) + ") ni de clase concreto con apariencia; se crean sin apariencia.")
            };
            return l;
        }
    }

    /// <summary>
    /// Parte que toca Revit para los materiales de concreto: lee los materiales del
    /// proyecto, busca tramas y apariencia de referencia, y crea o actualiza cada
    /// material con sus activos fisico y termico calculados por ReglasConcreto.
    /// </summary>
    public static class CreadorMateriales
    {
        private static readonly string[] ClasesConcreto = { "Concreto", "Concrete", "Hormigón", "Hormigon" };

        /// <summary>Materiales del proyecto ordenados por nombre.</summary>
        public static List<Material> Materiales(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Material)).Cast<Material>()
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<MaterialExistente> Existentes(IEnumerable<Material> materiales) =>
            materiales.Select(m => new MaterialExistente(m.Name, ClaseDe(m))).ToList();

        private static string ClaseDe(Material m)
        {
            try { return m.MaterialClass ?? ""; } catch { return ""; }
        }

        /// <summary>Busca por nombre las tramas y el material de referencia de la apariencia.</summary>
        public static RecursosProyecto BuscarRecursos(Document doc, ConfiguracionMateriales cfg, IList<Material> materiales)
        {
            var r = new RecursosProyecto();
            FillPatternElement corte = BuscarTrama(doc, cfg.TramaCorte);
            if (corte != null) { r.TramaCorte = corte.Id; r.NombreTramaCorte = corte.Name; }
            FillPatternElement sup = BuscarTrama(doc, cfg.TramaSuperficie);
            if (sup != null) { r.TramaSuperficie = sup.Id; r.NombreTramaSuperficie = sup.Name; }

            Material referencia = null;
            foreach (string nombre in cfg.MaterialApariencia)
            {
                referencia = materiales.FirstOrDefault(m => Nombres.Iguales(m.Name, nombre) && TieneApariencia(m));
                if (referencia != null) break;
            }
            if (referencia == null)
                referencia = materiales.FirstOrDefault(m => TieneApariencia(m) && ClasesConcreto.Any(c => Nombres.Iguales(ClaseDe(m), c)));
            if (referencia != null) { r.Apariencia = referencia.AppearanceAssetId; r.MaterialApariencia = referencia.Name; }
            return r;
        }

        private static bool TieneApariencia(Material m)
        {
            try { return m.AppearanceAssetId != null && m.AppearanceAssetId != ElementId.InvalidElementId; }
            catch { return false; }
        }

        private static FillPatternElement BuscarTrama(Document doc, IEnumerable<string> nombres)
        {
            foreach (string n in nombres)
            {
                foreach (FillPatternTarget destino in new[] { FillPatternTarget.Drafting, FillPatternTarget.Model })
                {
                    try
                    {
                        FillPatternElement fp = FillPatternElement.GetFillPatternElementByName(doc, destino, n);
                        if (fp != null) return fp;
                    }
                    catch { /* nombre no valido o no encontrado */ }
                }
            }
            return null;
        }

        private static double A(double valor, ForgeTypeId unidad) => UnitUtils.ConvertToInternalUnits(valor, unidad);
        private static string F(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>
        /// Crea o actualiza los materiales marcados, cada uno en su propia
        /// subtransaccion: si uno falla se deshace solo ese y los demas siguen.
        /// </summary>
        public static ResultadoCreacion Ejecutar(Document doc, ConfiguracionMateriales cfg, OpcionesMateriales op,
                                                 IList<Material> materiales, RecursosProyecto recursos)
        {
            var res = new ResultadoCreacion();
            if (op.Filas.Any(f => f.Crear))
            {
                if (!recursos.HayTramaCorte) res.Avisos.Add("Sin trama de corte: ninguna trama del proyecto se llama " + string.Join(", ", cfg.TramaCorte) + ".");
                if (!recursos.HayTramaSuperficie) res.Avisos.Add("Sin trama de superficie: ninguna trama del proyecto se llama " + string.Join(", ", cfg.TramaSuperficie) + ".");
                if (!recursos.HayApariencia) res.Avisos.Add("Sin apariencia: no hay material de referencia (" + string.Join(", ", cfg.MaterialApariencia) + ") ni de clase concreto con apariencia.");
            }

            using (var tx = new Transaction(doc, "Crear materiales de concreto Peru"))
            {
                tx.Start();
                foreach (FilaPlanConcreto fila in op.Filas)
                {
                    string nombre = fila.Nombre;
                    if (!fila.Crear)
                    {
                        res.Omitidos.Add(nombre + " -> " + (fila.Seleccionable ? "sin marcar" : fila.TextoEstado));
                        continue;
                    }

                    using (var sub = new SubTransaction(doc))
                    {
                        sub.Start();
                        try
                        {
                            Material m;
                            bool nuevo;
                            if (fila.Existente != null)
                            {
                                m = materiales.FirstOrDefault(x => Nombres.Iguales(x.Name, nombre));
                                if (m == null) throw new InvalidOperationException("el material existente ya no esta en el proyecto");
                                nuevo = false;
                            }
                            else
                            {
                                m = doc.GetElement(Material.Create(doc, nombre)) as Material;
                                if (m == null) throw new InvalidOperationException("Material.Create no devolvio un material");
                                nuevo = true;
                            }

                            Configurar(doc, m, fila, cfg, recursos, res.Avisos);
                            sub.Commit();

                            ValoresConcreto v = fila.Valores;
                            string desc = nombre + "  (f'c " + F(v.FcKgCm2) + " kg/cm2 = " + F(v.FcMPa, "0.0") + " MPa, E " + F(v.EKgCm2, "0") +
                                          " kg/cm2 = " + F(v.EMPa, "0") + " MPa, " + F(v.DensidadKgM3) + " kg/m3" + (v.Ligero ? ", ligero" : "") + ")";
                            (nuevo ? res.Creados : res.Actualizados).Add(desc);
                        }
                        catch (Exception ex)
                        {
                            sub.RollBack();
                            res.Errores.Add(nombre + " -> " + ex.Message);
                        }
                    }
                }
                tx.Commit();
            }
            return res;
        }

        /// <summary>Escribe identidad, graficos, apariencia y los activos fisico y termico del material.</summary>
        private static void Configurar(Document doc, Material m, FilaPlanConcreto fila, ConfiguracionMateriales cfg,
                                       RecursosProyecto recursos, List<string> avisos)
        {
            ValoresConcreto v = fila.Valores;
            string nombre = m.Name;

            // --- identidad ---
            m.MaterialClass = cfg.ClaseMaterial ?? "";
            if (!EscribirTexto(m, new[] { BuiltInParameter.PROPERTY_SET_DESCRIPTION, BuiltInParameter.ALL_MODEL_DESCRIPTION },
                               new[] { "Description", "Descripción", "Descripcion" }, v.Descripcion))
                avisos.Add(nombre + ": no se encontro el parametro de descripcion; queda sin descripcion.");
            if (!EscribirTexto(m, new[] { BuiltInParameter.PROPERTY_SET_KEYWORDS }, new[] { "Keywords", "Palabras clave" }, cfg.PalabrasClave ?? ""))
                avisos.Add(nombre + ": no se encontro el parametro de palabras clave; queda sin ellas.");

            // --- graficos ---
            m.Color = new Color(v.Gris, v.Gris, v.Gris);
            m.UseRenderAppearanceForShading = false;
            if (recursos.HayTramaCorte) m.CutForegroundPatternId = recursos.TramaCorte;
            if (recursos.HayTramaSuperficie) m.SurfaceForegroundPatternId = recursos.TramaSuperficie;

            // --- apariencia: la API no crea apariencias desde cero; se copia o se comparte la de referencia ---
            if (recursos.HayApariencia)
            {
                ElementId id = recursos.Apariencia;
                if (cfg.DuplicarApariencia)
                {
                    AppearanceAssetElement propia = AppearanceAssetElement.GetAppearanceAssetElementByName(doc, nombre);
                    if (propia == null)
                    {
                        var origen = doc.GetElement(recursos.Apariencia) as AppearanceAssetElement;
                        if (origen != null) propia = origen.Duplicate(nombre);
                    }
                    if (propia != null) id = propia.Id;
                    else avisos.Add(nombre + ": no se pudo duplicar la apariencia; se comparte la de referencia.");
                }
                m.AppearanceAssetId = id;
            }

            // --- activo fisico (StructuralAsset, clase Concreto, isotropo) ---
            PropertySetElement fisico = ActivoFisico(doc, nombre, v);
            m.SetMaterialAspectByPropertySet(MaterialAspect.Structural, fisico.Id);

            // --- activo termico (ThermalAsset, solido) ---
            PropertySetElement termico = ActivoTermico(doc, nombre + " (termico)", v);
            m.SetMaterialAspectByPropertySet(MaterialAspect.Thermal, termico.Id);
        }

        private static PropertySetElement ActivoFisico(Document doc, string nombre, ValoresConcreto v)
        {
            var a = new StructuralAsset(nombre, StructuralAssetClass.Concrete)
            {
                Behavior = StructuralBehavior.Isotropic,
                Density = A(v.DensidadKgM3, UnitTypeId.KilogramsPerCubicMeter),
                ConcreteCompression = A(v.FcMPa, UnitTypeId.Megapascals),
                ConcreteShearStrengthReduction = v.FactorCorte,
                Lightweight = v.Ligero
            };
            a.SetYoungModulus(A(v.EMPa, UnitTypeId.Megapascals));
            a.SetPoissonRatio(v.Poisson);
            a.SetShearModulus(A(v.GMPa, UnitTypeId.Megapascals));
            a.SetThermalExpansionCoefficient(A(v.DilatacionPorC, UnitTypeId.InverseDegreesCelsius));
            return Conjunto(doc, nombre, () => PropertySetElement.Create(doc, a), p => p.SetStructuralAsset(a));
        }

        private static PropertySetElement ActivoTermico(Document doc, string nombre, ValoresConcreto v)
        {
            PropiedadesTermicasCfg t = v.Termico;
            var a = new ThermalAsset(nombre, ThermalMaterialType.Solid)
            {
                Behavior = StructuralBehavior.Isotropic,
                ThermalConductivity = A(t.ConductividadWmK, UnitTypeId.WattsPerMeterKelvin),
                SpecificHeat = A(t.CalorEspecificoJgC, UnitTypeId.JoulesPerGramDegreeCelsius),
                Density = A(v.DensidadKgM3, UnitTypeId.KilogramsPerCubicMeter),
                Emissivity = t.Emisividad,
                Permeability = A(t.PermeabilidadNgPaSm2, UnitTypeId.NanogramsPerPascalSecondSquareMeter),
                Porosity = t.Porosidad,
                Reflectivity = t.Reflectividad,
                ElectricalResistivity = A(t.ResistividadOhmM, UnitTypeId.OhmMeters),
                TransmitsLight = t.TransmiteLuz
            };
            return Conjunto(doc, nombre, () => PropertySetElement.Create(doc, a), p => p.SetThermalAsset(a));
        }

        /// <summary>
        /// Los nombres de PropertySetElement son unicos en el proyecto: si ya hay uno
        /// con ese nombre (material que se actualiza, o activo que quedo de una
        /// ejecucion anterior) se le reescribe el activo; si no, se crea.
        /// </summary>
        private static PropertySetElement Conjunto(Document doc, string nombre, Func<PropertySetElement> crear, Action<PropertySetElement> reemplazar)
        {
            PropertySetElement existente = new FilteredElementCollector(doc)
                .OfClass(typeof(PropertySetElement)).Cast<PropertySetElement>()
                .FirstOrDefault(p => Nombres.Iguales(p.Name, nombre));
            if (existente != null)
            {
                reemplazar(existente);
                return existente;
            }
            return crear();
        }

        /// <summary>
        /// Escribe un parametro de texto probando primero los parametros integrados y
        /// luego el nombre visible (ingles y espanol). Devuelve false si no hay ninguno editable.
        /// </summary>
        private static bool EscribirTexto(Element el, BuiltInParameter[] integrados, string[] nombres, string valor)
        {
            foreach (BuiltInParameter bip in integrados)
            {
                try
                {
                    Parameter p = el.get_Parameter(bip);
                    if (p != null && !p.IsReadOnly && p.StorageType == StorageType.String) { p.Set(valor); return true; }
                }
                catch { /* parametro no aplicable */ }
            }
            foreach (Parameter p in el.Parameters)
            {
                try
                {
                    if (p.Definition == null || p.IsReadOnly || p.StorageType != StorageType.String) continue;
                    if (nombres.Any(n => Nombres.Iguales(p.Definition.Name, n))) { p.Set(valor); return true; }
                }
                catch { }
            }
            return false;
        }
    }
}
