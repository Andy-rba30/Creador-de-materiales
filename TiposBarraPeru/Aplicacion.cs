using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace TiposBarraPeru
{
    /// <summary>
    /// Punto de entrada de la aplicacion: anade a la cinta la pestana "Peru" con el
    /// panel "Armadura" y el boton "Tipos de barra Peru". El icono se dibuja en
    /// codigo para no depender de archivos de recursos.
    /// </summary>
    public class Aplicacion : IExternalApplication
    {
        public const string NombrePestana = "Peru";
        public const string NombrePanel = "Armadura";

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                try { app.CreateRibbonTab(NombrePestana); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { /* la pestana ya existe (otro add-in) */ }

                RibbonPanel panel = null;
                foreach (RibbonPanel p in app.GetRibbonPanels(NombrePestana))
                    if (p.Name == NombrePanel) { panel = p; break; }
                if (panel == null) panel = app.CreateRibbonPanel(NombrePestana, NombrePanel);

                string ruta = Assembly.GetExecutingAssembly().Location;
                var datos = new PushButtonData("TiposBarraPeru", "Tipos de barra\nPeru", ruta, typeof(ComandoTiposBarra).FullName)
                {
                    ToolTip = "Crea los tipos de barra del catalogo peruano con los diametros de doblado y ganchos de la norma E.060.",
                    LongDescription = "Lee los tipos de barra que ya existen, muestra el catalogo (config.json junto a la DLL) " +
                                      "y crea solo los que faltan, salvo que se marque \"actualizar los que ya existen\"."
                };
                try
                {
                    datos.LargeImage = CrearIcono(32);
                    datos.Image = CrearIcono(16);
                }
                catch { /* sin icono el boton sigue funcionando */ }

                panel.AddItem(datos);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Tipos de barra Peru", "No se pudo crear el boton de la cinta: " + ex.Message +
                                Environment.NewLine + "El comando sigue disponible en Complementos > Herramientas externas.");
                return Result.Succeeded;
            }
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        /// <summary>Icono cuadrado azul con el simbolo de diametro en blanco.</summary>
        private static BitmapSource CrearIcono(int tam)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var fondo = new SolidColorBrush(Color.FromRgb(0x2B, 0x57, 0x9A));
                double r = tam * 0.18;
                dc.DrawRoundedRectangle(fondo, null, new Rect(0, 0, tam, tam), r, r);
                var texto = new FormattedText(
                    "Ø", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    tam * 0.78, Brushes.White, 1.0);
                dc.DrawText(texto, new Point((tam - texto.Width) / 2, (tam - texto.Height) / 2));
            }
            var bmp = new RenderTargetBitmap(tam, tam, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
    }
}
