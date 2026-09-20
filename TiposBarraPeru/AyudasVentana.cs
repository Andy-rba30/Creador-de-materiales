using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NamingUtils = Autodesk.Revit.DB.NamingUtils;

namespace TiposBarraPeru
{
    /// <summary>
    /// Ayudantes comunes a las ventanas de tipos de barra y de materiales de
    /// concreto: margenes, colores de estado, formato de numeros, celdas de la
    /// tabla, campos de texto y validacion de nombres con Revit.
    /// </summary>
    internal static class AyudasVentana
    {
        public static readonly Thickness Pad = new Thickness(6, 2, 6, 2);
        public static readonly Brush BrochaExiste = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
        public static readonly Brush BrochaCrear = new SolidColorBrush(Color.FromRgb(0x1E, 0x7B, 0x34));
        public static readonly Brush BrochaAviso = new SolidColorBrush(Color.FromRgb(0xB0, 0x3A, 0x2E));

        public static string Fmt(double v, string formato = "0.##") => v.ToString(formato, CultureInfo.InvariantCulture);

        public static TextBlock Num(double v, string formato = "0.##") => new TextBlock
        {
            Text = Fmt(v, formato),
            Margin = Pad,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        public static void Celda(Grid tabla, UIElement el, int fila, int col)
        {
            Grid.SetRow(el, fila);
            Grid.SetColumn(el, col);
            tabla.Children.Add(el);
        }

        public static TextBox Campo(Panel padre, string etiqueta, string valor, double ancho)
        {
            padre.Children.Add(new TextBlock { Text = etiqueta, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var caja = new TextBox { Text = valor, Width = ancho, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            padre.Children.Add(caja);
            return caja;
        }

        /// <summary>Numero escrito por el usuario; admite coma o punto decimal. null si no es un numero.</summary>
        public static double? Leer(TextBox caja)
        {
            string t = (caja.Text ?? "").Trim().Replace(',', '.');
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return v;
            return null;
        }

        /// <summary>Validador de nombres de Revit (NamingUtils.IsValidName). Si la API no esta disponible, acepta.</summary>
        public static bool NombreValido(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return false;
            try { return NamingUtils.IsValidName(nombre); }
            catch { return true; }
        }
    }
}
