using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using TiposBarraPeru.Reglas;

namespace TiposBarraPeru
{
    /// <summary>Un RebarHookType del proyecto, con lo que hace falta para clasificarlo segun E.060.</summary>
    public class GanchoProyecto
    {
        public ElementId Id;
        public string Nombre;
        public bool EsEstribo;
        public double AnguloGrados;

        /// <summary>Regla E.060 que le corresponde, o null si se deja en calculo automatico.</summary>
        public TipoGancho? Tipo => ReglasE060.Clasificar(EsEstribo, AnguloGrados);
    }

    /// <summary>Lo que el usuario eligio en la ventana.</summary>
    public class OpcionesEjecucion
    {
        public string Prefijo;
        public bool ActualizarExistentes;
        public bool GanchosE060;
        /// <summary>Filas de la tabla con la casilla "crear" ya resuelta.</summary>
        public List<FilaPlan> Filas = new List<FilaPlan>();
    }

    /// <summary>Resumen de la ejecucion, para el dialogo final.</summary>
    public class ResultadoCreacion
    {
        public List<string> Creados = new List<string>();
        public List<string> Actualizados = new List<string>();
        public List<string> Omitidos = new List<string>();
        public List<string> Errores = new List<string>();
        public List<string> Avisos = new List<string>();

        public string Detalle()
        {
            var l = new List<string>();
            void Bloque(string titulo, List<string> items)
            {
                if (items.Count == 0) return;
                if (l.Count > 0) l.Add("");
                l.Add(titulo + ":");
                foreach (string s in items) l.Add("  " + s);
            }
            Bloque("Creados", Creados);
            Bloque("Actualizados", Actualizados);
            Bloque("Omitidos", Omitidos);
            Bloque("Errores", Errores);
            Bloque("Avisos", Avisos);
            return string.Join(Environment.NewLine, l);
        }
    }

    /// <summary>
    /// Parte que toca Revit: lee los tipos existentes y crea o actualiza los tipos
    /// de barra con los valores E.060 calculados por la biblioteca de reglas.
    /// </summary>
    public static class CreadorTipos
    {
        /// <summary>Tolerancia al comprobar la extension de gancho fijada (mm).</summary>
        private const double ToleranciaGanchoMm = 0.1;

        /// <summary>Tipos de barra del proyecto ordenados por nombre.</summary>
        public static List<RebarBarType> TiposBarra(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Tipos de gancho del proyecto (estilo y angulo), ordenados por nombre.</summary>
        public static List<GanchoProyecto> Ganchos(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType)).Cast<RebarHookType>()
                .Select(h => new GanchoProyecto
                {
                    Id = h.Id,
                    Nombre = h.Name,
                    EsEstribo = h.Style == RebarStyle.StirrupTie,
                    AnguloGrados = h.HookAngle * 180.0 / Math.PI
                })
                .OrderBy(g => g.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static double Mm(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        private static double AMm(double interno) => UnitUtils.ConvertFromInternalUnits(interno, UnitTypeId.Millimeters);
        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// Crea o actualiza los tipos marcados, cada uno en su propia subtransaccion:
        /// si uno falla se deshace solo ese y los demas siguen.
        /// </summary>
        public static ResultadoCreacion Ejecutar(Document doc, Configuracion cfg, OpcionesEjecucion op,
                                                 IList<RebarBarType> tiposBarra, IList<GanchoProyecto> ganchos)
        {
            var res = new ResultadoCreacion();
            var reglas = new ReglasE060(cfg.ReglasE060);

            if (op.GanchosE060)
            {
                foreach (GanchoProyecto g in ganchos.Where(g => g.Tipo == null))
                    res.Avisos.Add("Gancho \"" + g.Nombre + "\" (" + (g.EsEstribo ? "estribo" : "estandar") + ", " + F(g.AnguloGrados) +
                                   " grados) no encaja en ninguna regla E.060: se deja en calculo automatico.");
            }

            using (var tx = new Transaction(doc, "Crear tipos de barra Peru"))
            {
                tx.Start();
                foreach (FilaPlan fila in op.Filas)
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
                            RebarBarType bt;
                            bool nuevo;
                            if (fila.Existente != null)
                            {
                                bt = tiposBarra.FirstOrDefault(t => Nombres.Iguales(t.Name, nombre));
                                if (bt == null) throw new InvalidOperationException("el tipo existente ya no esta en el proyecto");
                                nuevo = false;
                            }
                            else
                            {
                                bt = RebarBarType.Create(doc);
                                bt.Name = nombre;
                                nuevo = true;
                            }

                            Configurar(doc, bt, fila, cfg, reglas, op.GanchosE060, ganchos, res.Avisos);
                            sub.Commit();

                            string desc = nombre + "  (" + F(fila.Barra.DiametroMm) + " mm, doblado " + F(fila.Valores.DobladoBarraMm) +
                                          " / estribo " + F(fila.Valores.DobladoEstriboMm) + " mm, " + F(fila.Barra.PesoKgM) + " kg/m)";
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

        /// <summary>Escribe en el tipo todos los valores: diametros, doblados, peso, deformacion y ganchos.</summary>
        private static void Configurar(Document doc, RebarBarType bt, FilaPlan fila, Configuracion cfg, ReglasE060 reglas,
                                       bool ganchosE060, IList<GanchoProyecto> ganchos, List<string> avisos)
        {
            BarraCatalogo barra = fila.Barra;
            ValoresE060 v = fila.Valores;
            double db = Mm(barra.DiametroMm);
            double masa = UnitUtils.ConvertToInternalUnits(barra.PesoKgM, UnitTypeId.KilogramsPerMeter);

            // Todos los diametros a la vez: la API valida nominal < doblados, y asignarlos
            // uno a uno puede fallar segun el orden (p. ej. 35.8 mm con los doblados por defecto).
            var opciones = new BarTypeDiameterOptions
            {
                BarNominalDiameter = db,
                BarModelDiameter = db,
                StandardBendDiameter = Mm(v.DobladoBarraMm),
                StandardHookBendDiameter = Mm(v.DobladoGanchoMm),
                StirrupTieBendDiameter = Mm(v.DobladoEstriboMm),
                BarMassPerUnitLength = masa
            };
            bt.SetBarTypeDiameters(opciones);
            // Peso unitario: propiedad nativa desde Revit 2027 (kg/m convertido a unidades internas).
            bt.BarMassPerUnitLength = masa;

            bt.MaximumBendRadius = Mm(cfg.RadioMaximoDobladoMm);
            bt.DeformationType = barra.Corrugada ? RebarDeformationType.Deformed : RebarDeformationType.Plain;

            foreach (GanchoProyecto g in ganchos)
                ConfigurarGancho(doc, bt, g, barra, reglas, ganchosE060, avisos);
        }

        /// <summary>
        /// Longitud de gancho de un tipo de gancho. Por defecto, calculo automatico de
        /// Revit. En modo E.060 se fija por calibracion: Revit no documenta si su
        /// "longitud de gancho" incluye el tramo curvo, asi que se lee la longitud
        /// automatica y la extension que resulta de ella, se aplica esa diferencia a
        /// la extension E.060 y se comprueba que la extension leida coincide.
        /// </summary>
        private static void ConfigurarGancho(Document doc, RebarBarType bt, GanchoProyecto g, BarraCatalogo barra,
                                             ReglasE060 reglas, bool ganchosE060, List<string> avisos)
        {
            // Todos los ganchos del proyecto quedan permitidos para el tipo.
            try { bt.SetHookPermission(g.Id, true); } catch { }

            TipoGancho? tipo = g.Tipo;
            if (!ganchosE060 || tipo == null)
            {
                bt.SetAutoCalcHookLengths(g.Id, true);
                return;
            }

            var hook = doc.GetElement(g.Id) as RebarHookType;
            if (hook == null)
            {
                bt.SetAutoCalcHookLengths(g.Id, true);
                return;
            }

            double objetivo = Mm(reglas.ExtensionGanchoMm(tipo.Value, barra.DiametroMm));
            try
            {
                bt.SetAutoCalcHookLengths(g.Id, true);
                double longitudAuto = bt.GetHookLength(g.Id);
                double extensionAuto = hook.GetHookExtensionLength(bt);
                double diferencia = longitudAuto - extensionAuto;

                bt.SetAutoCalcHookLengths(g.Id, false);
                bt.SetHookLength(g.Id, objetivo + diferencia);
                double leida = hook.GetHookExtensionLength(bt);
                if (Math.Abs(leida - objetivo) > Mm(ToleranciaGanchoMm))
                {
                    // segunda correccion por si la relacion no fuera exactamente un desplazamiento
                    bt.SetHookLength(g.Id, objetivo + diferencia + (objetivo - leida));
                    leida = hook.GetHookExtensionLength(bt);
                }
                if (Math.Abs(leida - objetivo) > Mm(ToleranciaGanchoMm))
                    avisos.Add(bt.Name + ", gancho \"" + g.Nombre + "\": extension pedida " + F(AMm(objetivo)) +
                               " mm, leida " + F(AMm(leida)) + " mm. Revisar en Revit.");
            }
            catch (Exception ex)
            {
                try { bt.SetAutoCalcHookLengths(g.Id, true); } catch { }
                avisos.Add(bt.Name + ", gancho \"" + g.Nombre + "\": no se pudo fijar la longitud E.060 (" + ex.Message +
                           "); se deja en calculo automatico.");
            }
        }
    }
}
