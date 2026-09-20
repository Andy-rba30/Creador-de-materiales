using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using TiposBarraPeru.Reglas;

namespace TiposBarraPeru
{
    /// <summary>
    /// Comando "Tipos de barra Peru": lee los RebarBarType del proyecto, abre la
    /// ventana con el catalogo y crea (o actualiza) los tipos elegidos dentro de
    /// una transaccion. Al terminar muestra el resumen de creados y omitidos.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ComandoTiposBarra : IExternalCommand
    {
        public static string RutaConfig()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(dir, "config.json");
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null)
            {
                message = "No hay ningun documento abierto.";
                return Result.Failed;
            }
            if (doc.IsFamilyDocument)
            {
                message = "Este comando se ejecuta en un proyecto, no en una familia.";
                return Result.Failed;
            }

            // --- 1. Configuracion (config.json junto a la DLL) ---
            Configuracion cfg;
            try { cfg = Configuracion.Cargar(RutaConfig()); }
            catch (Exception ex)
            {
                message = "No se pudo leer config.json (" + RutaConfig() + "): " + ex.Message;
                return Result.Failed;
            }

            // --- 2. Lo que ya hay en el proyecto: tipos de barra y tipos de gancho ---
            List<RebarBarType> tiposBarra = CreadorTipos.TiposBarra(doc);
            var existentes = tiposBarra
                .Select(t => new TipoExistente(t.Name, UnitUtils.ConvertFromInternalUnits(t.BarNominalDiameter, UnitTypeId.Millimeters)))
                .ToList();
            List<GanchoProyecto> ganchos = CreadorTipos.Ganchos(doc);

            // --- 3. Ventana: prefijo, opciones y tabla del catalogo ---
            var win = new VentanaTiposBarra(cfg, existentes, ganchos, RutaConfig());
            try { new WindowInteropHelper(win).Owner = uiapp.MainWindowHandle; } catch { }
            bool? ok = win.ShowDialog();
            if (ok != true || win.Resultado == null) return Result.Cancelled;
            OpcionesEjecucion op = win.Resultado;

            // --- 4. Creacion en Revit ---
            ResultadoCreacion res;
            try { res = CreadorTipos.Ejecutar(doc, cfg, op, tiposBarra, ganchos); }
            catch (Exception ex)
            {
                message = "Error al crear los tipos de barra: " + ex.Message;
                return Result.Failed;
            }

            // --- 5. Resumen ---
            var td = new TaskDialog("Tipos de barra Peru")
            {
                MainInstruction = res.Creados.Count + " tipo(s) creado(s), " + res.Actualizados.Count +
                                  " actualizado(s), " + res.Omitidos.Count + " omitido(s).",
                MainContent = res.Detalle()
            };
            if (res.Errores.Count > 0)
            {
                td.MainInstruction += Environment.NewLine + "ATENCION: " + res.Errores.Count + " tipo(s) con error (ver detalle).";
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
            }
            td.Show();
            return Result.Succeeded;
        }
    }
}
