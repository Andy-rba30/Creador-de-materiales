using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using TiposBarraPeru.Reglas;

namespace TiposBarraPeru
{
    /// <summary>
    /// Comando "Materiales de concreto": lee los materiales del proyecto, abre la
    /// ventana con el catalogo de resistencias y crea (o actualiza) los materiales
    /// elegidos con sus activos fisico y termico segun E.060. Al terminar muestra el
    /// resumen de creados, actualizados, omitidos y errores.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ComandoMaterialesConcreto : IExternalCommand
    {
        public const string Titulo = "Materiales de concreto Peru";

        public static string RutaConfig()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(dir, "materiales.json");
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

            // --- 1. Configuracion (materiales.json junto a la DLL) ---
            ConfiguracionMateriales cfg;
            try { cfg = ConfiguracionMateriales.Cargar(RutaConfig()); }
            catch (Exception ex)
            {
                message = "No se pudo leer materiales.json (" + RutaConfig() + "): " + ex.Message;
                return Result.Failed;
            }

            // --- 2. Lo que ya hay en el proyecto: materiales, tramas y apariencia de referencia ---
            List<Material> materiales = CreadorMateriales.Materiales(doc);
            List<MaterialExistente> existentes = CreadorMateriales.Existentes(materiales);
            RecursosProyecto recursos = CreadorMateriales.BuscarRecursos(doc, cfg, materiales);

            // --- 3. Ventana ---
            var win = new VentanaMaterialesConcreto(cfg, existentes, recursos, RutaConfig());
            try { new WindowInteropHelper(win).Owner = uiapp.MainWindowHandle; } catch { }
            bool? ok = win.ShowDialog();
            if (ok != true || win.Resultado == null) return Result.Cancelled;
            OpcionesMateriales op = win.Resultado;

            // --- 4. Creacion en Revit ---
            ResultadoCreacion res;
            try { res = CreadorMateriales.Ejecutar(doc, cfg, op, materiales, recursos); }
            catch (Exception ex)
            {
                message = "Error al crear los materiales: " + ex.Message;
                return Result.Failed;
            }

            // --- 5. Resumen ---
            var td = new TaskDialog(Titulo)
            {
                MainInstruction = res.Creados.Count + " material(es) creado(s), " + res.Actualizados.Count +
                                  " actualizado(s), " + res.Omitidos.Count + " omitido(s).",
                MainContent = res.Detalle()
            };
            if (res.Errores.Count > 0)
            {
                td.MainInstruction += Environment.NewLine + "ATENCION: " + res.Errores.Count + " material(es) con error (ver detalle).";
                td.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
            }
            td.Show();
            return Result.Succeeded;
        }
    }
}
