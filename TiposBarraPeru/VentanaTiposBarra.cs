using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NamingUtils = Autodesk.Revit.DB.NamingUtils;
using TiposBarraPeru.Reglas;

namespace TiposBarraPeru
{
    /// <summary>
    /// Ventana del catalogo: prefijo de nombre editable, opciones y una fila por
    /// tamano con casilla "crear", nombre resultante, diametro, area, peso, diametros
    /// de doblado E.060 y estado ("ya existe" / "se creara"). Construida en codigo
    /// (sin XAML) para no depender del compilador de XAML.
    /// </summary>
    public sealed class VentanaTiposBarra : Window
    {
        private readonly Configuracion _cfg;
        private readonly IList<TipoExistente> _existentes;
        private readonly IList<GanchoProyecto> _ganchos;
        private readonly string _rutaConfig;
        private readonly ReglasE060 _reglas;

        private TextBox _prefijo;
        private CheckBox _actualizar, _ganchosE060;
        private Grid _tabla;
        private Button _crear;
        private TextBlock _mensaje;

        // grupo "Otro diametro"
        private TextBox _nuevoNombre, _nuevoDiametro, _nuevaArea, _nuevoPeso;
        private TextBlock _nuevoMensaje;

        private sealed class Fila
        {
            public FilaPlan Plan;
            public CheckBox Crear;
            public TextBlock Nombre, Estado;
        }

        private readonly List<Fila> _filas = new List<Fila>();
        /// <summary>Tamanos que el usuario desmarco a mano, para respetarlos al replanificar.</summary>
        private readonly HashSet<string> _desmarcados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _construyendo = true;

        /// <summary>Opciones finales si el usuario pulso "Crear"; null si cancelo.</summary>
        public OpcionesEjecucion Resultado { get; private set; }

        private static readonly Thickness Pad = new Thickness(6, 2, 6, 2);
        private static readonly Brush BrochaExiste = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
        private static readonly Brush BrochaCrear = new SolidColorBrush(Color.FromRgb(0x1E, 0x7B, 0x34));
        private static readonly Brush BrochaAviso = new SolidColorBrush(Color.FromRgb(0xB0, 0x3A, 0x2E));

        public VentanaTiposBarra(Configuracion cfg, IList<TipoExistente> existentes, IList<GanchoProyecto> ganchos, string rutaConfig)
        {
            _cfg = cfg;
            _existentes = existentes;
            _ganchos = ganchos;
            _rutaConfig = rutaConfig;
            _reglas = new ReglasE060(cfg.ReglasE060);

            Title = "Tipos de barra Peru (E.060)";
            Width = 900;
            Height = 640;
            MinWidth = 760;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            FontSize = 12;

            Content = ConstruirRaiz();
            _construyendo = false;
            Replanificar();
        }

        // ------------------------------------------------------------------
        // Construccion de la interfaz
        // ------------------------------------------------------------------
        private UIElement ConstruirRaiz()
        {
            var raiz = new Grid { Margin = new Thickness(10) };
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            UIElement opciones = ConstruirOpciones();
            Grid.SetRow(opciones, 0);
            raiz.Children.Add(opciones);

            var cuerpo = new StackPanel();
            _tabla = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            cuerpo.Children.Add(new GroupBox { Header = "Catalogo (config.json) y diametros anadidos (catalogoExtra)", Padding = new Thickness(4), Content = _tabla });
            cuerpo.Children.Add(ConstruirOtroDiametro());
            cuerpo.Children.Add(ConstruirGanchos());

            var scroll = new ScrollViewer
            {
                Content = cuerpo,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 6, 0, 6)
            };
            Grid.SetRow(scroll, 1);
            raiz.Children.Add(scroll);

            UIElement botones = ConstruirBotones();
            Grid.SetRow(botones, 2);
            raiz.Children.Add(botones);
            return raiz;
        }

        private UIElement ConstruirOpciones()
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Tipos de barra del proyecto: " + _existentes.Count + ". Por defecto solo se crean los que faltan; " +
                       "los valores de doblado y ganchos siguen la norma E.060 (art. 7.1 y 7.2).",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var linea = new StackPanel { Orientation = Orientation.Horizontal };
            linea.Children.Add(new TextBlock { Text = "Prefijo del nombre:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            _prefijo = new TextBox { Text = _cfg.PrefijoNombre, Width = 70, VerticalAlignment = VerticalAlignment.Center };
            _prefijo.ToolTip = "nombre del tipo = prefijo + nombre del catalogo (Ø6mm, Ø3/8\", Ø1 3/8\" ...)";
            _prefijo.TextChanged += (s, e) => Replanificar();
            linea.Children.Add(_prefijo);

            _actualizar = new CheckBox
            {
                Content = "Actualizar los que ya existen",
                IsChecked = _cfg.ActualizarExistentes,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0),
                ToolTip = "Reescribe diametros, doblados, peso y ganchos de los tipos que ya tienen ese nombre. Desactivado: se omiten."
            };
            _actualizar.Checked += (s, e) => Replanificar();
            _actualizar.Unchecked += (s, e) => Replanificar();
            linea.Children.Add(_actualizar);

            _ganchosE060 = new CheckBox
            {
                Content = "Longitudes de gancho segun E.060",
                IsChecked = _cfg.LongitudesGanchoSegunE060,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0),
                ToolTip = "Desactivado: Revit calcula las longitudes de gancho. Activado: se fijan segun E.060 para cada tipo de gancho del proyecto."
            };
            linea.Children.Add(_ganchosE060);
            panel.Children.Add(linea);
            return panel;
        }

        private static readonly string[] Cabeceras =
            { "Crear", "Nombre", "Ø (mm)", "Area (cm2)", "Peso (kg/m)", "Doblado barra (mm)", "Doblado estribo (mm)", "Estado", "" };

        /// <summary>Indice de la columna "Estado", la unica que se estira.</summary>
        private const int ColumnaEstado = 7;

        private void ConstruirTabla(List<FilaPlan> plan)
        {
            _tabla.Children.Clear();
            _tabla.RowDefinitions.Clear();
            _tabla.ColumnDefinitions.Clear();
            _filas.Clear();

            for (int c = 0; c < Cabeceras.Length; c++)
                _tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = c == ColumnaEstado ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            _tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < Cabeceras.Length; c++)
                Celda(new TextBlock { Text = Cabeceras[c], FontWeight = FontWeights.SemiBold, Margin = Pad }, 0, c);

            int r = 1;
            foreach (FilaPlan p in plan)
            {
                _tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var fila = new Fila { Plan = p };

                fila.Crear = new CheckBox
                {
                    IsChecked = p.Crear && !_desmarcados.Contains(p.Barra.Nombre),
                    IsEnabled = p.Seleccionable,
                    Margin = Pad,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                string nombreCat = p.Barra.Nombre;
                fila.Crear.Checked += (s, e) => { _desmarcados.Remove(nombreCat); ActualizarBoton(); };
                fila.Crear.Unchecked += (s, e) => { if (!_construyendo) _desmarcados.Add(nombreCat); ActualizarBoton(); };
                Celda(fila.Crear, r, 0);

                fila.Nombre = new TextBlock { Text = p.Nombre, FontWeight = FontWeights.SemiBold, Margin = Pad, VerticalAlignment = VerticalAlignment.Center };
                Celda(fila.Nombre, r, 1);
                Celda(Num(p.Barra.DiametroMm), r, 2);
                Celda(Num(p.Barra.AreaCm2), r, 3);
                Celda(Num(p.Barra.PesoKgM, "0.000"), r, 4);
                var db = Num(p.Valores.DobladoBarraMm);
                db.ToolTip = Fmt(p.Valores.MultiplicadorBarra) + " db (barras principales y sus ganchos)";
                Celda(db, r, 5);
                var de = Num(p.Valores.DobladoEstriboMm);
                de.ToolTip = Fmt(p.Valores.MultiplicadorEstribo) + " db (estribos)";
                Celda(de, r, 6);

                fila.Estado = new TextBlock { Text = p.TextoEstado, Margin = Pad, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
                bool problema = p.Estado == EstadoFila.NombreInvalido || p.Estado == EstadoFila.FueraDeNorma ||
                                p.Estado == EstadoFila.NombreDuplicado || !string.IsNullOrEmpty(p.Aviso);
                fila.Estado.Foreground = p.Estado == EstadoFila.SeCreara ? BrochaCrear : problema ? BrochaAviso : BrochaExiste;
                fila.Estado.ToolTip = "Ganchos E.060 (extension recta): 180 grados " + Fmt(p.Valores.GanchoEstandar180Mm) + " mm, 90 grados " +
                                      Fmt(p.Valores.GanchoEstandar90Mm) + " mm, estribo 90 " + Fmt(p.Valores.GanchoEstribo90Mm) +
                                      " mm, estribo 135 " + Fmt(p.Valores.GanchoEstribo135Mm) + " mm";
                Celda(fila.Estado, r, ColumnaEstado);

                if (p.EsExtra)
                {
                    var quitar = new Button { Content = "Quitar", Padding = new Thickness(8, 1, 8, 1), Margin = Pad, VerticalAlignment = VerticalAlignment.Center };
                    quitar.ToolTip = "Borra este diametro de catalogoExtra en config.json";
                    BarraCatalogo barra = p.Barra;
                    quitar.Click += (s, e) => AlQuitar(barra);
                    Celda(quitar, r, 8);
                }

                _filas.Add(fila);
                r++;
            }
        }

        private UIElement ConstruirOtroDiametro()
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Los parametros de norma del diametro nuevo salen de las mismas reglas por umbral que el catalogo. " +
                       "Area y peso se calculan (pi*d^2/4 y 7.85 kg/dm3) y se pueden corregir a mano.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var linea = new StackPanel { Orientation = Orientation.Horizontal };
            _nuevoNombre = Campo(linea, "Nombre:", "", 80);
            _nuevoNombre.ToolTip = "Lo que va tras el prefijo: 10mm, 1 1/4\", #6 ...";
            _nuevoDiametro = Campo(linea, "Ø nominal (mm):", "", 60);
            _nuevaArea = Campo(linea, "Area (cm2):", "", 60);
            _nuevoPeso = Campo(linea, "Peso (kg/m):", "", 60);
            _nuevoDiametro.TextChanged += (s, e) => RellenarAreaYPeso();

            var anadir = new Button { Content = "Anadir a la lista", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            anadir.Click += AlAnadir;
            linea.Children.Add(anadir);
            panel.Children.Add(linea);

            _nuevoMensaje = new TextBlock { Foreground = BrochaAviso, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            panel.Children.Add(_nuevoMensaje);

            return new GroupBox { Header = "Otro diametro", Padding = new Thickness(4), Margin = new Thickness(0, 6, 0, 0), Content = panel };
        }

        private static TextBox Campo(Panel padre, string etiqueta, string valor, double ancho)
        {
            padre.Children.Add(new TextBlock { Text = etiqueta, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var caja = new TextBox { Text = valor, Width = ancho, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            padre.Children.Add(caja);
            return caja;
        }

        /// <summary>Numero escrito por el usuario; admite coma o punto decimal. null si no es un numero.</summary>
        private static double? Leer(TextBox caja)
        {
            string t = (caja.Text ?? "").Trim().Replace(',', '.');
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return v;
            return null;
        }

        private void RellenarAreaYPeso()
        {
            double? d = Leer(_nuevoDiametro);
            if (d == null || d.Value <= 0) return;
            double area = PropiedadesBarra.RedondearArea(PropiedadesBarra.AreaCm2(d.Value));
            _nuevaArea.Text = Fmt(area);
            _nuevoPeso.Text = Fmt(PropiedadesBarra.RedondearPeso(PropiedadesBarra.PesoKgM(area)), "0.###");
        }

        private void AlAnadir(object sender, RoutedEventArgs e)
        {
            _nuevoMensaje.Foreground = BrochaAviso;
            double? d = Leer(_nuevoDiametro);
            if (d == null || d.Value <= 0) { _nuevoMensaje.Text = "Escribe el diametro nominal en mm."; return; }

            string error = Planificador.ValidarNuevaBarra(_cfg, _prefijo.Text, _nuevoNombre.Text, d.Value, _existentes, NombreValido);
            if (error != null) { _nuevoMensaje.Text = error; return; }

            double? area = Leer(_nuevaArea);
            double? peso = Leer(_nuevoPeso);
            if (area == null || area.Value <= 0) area = PropiedadesBarra.RedondearArea(PropiedadesBarra.AreaCm2(d.Value));
            if (peso == null || peso.Value <= 0) peso = PropiedadesBarra.RedondearPeso(PropiedadesBarra.PesoKgM(area.Value));

            _cfg.CatalogoExtra.Add(new BarraCatalogo
            {
                Nombre = _nuevoNombre.Text.Trim(),
                DiametroMm = d.Value,
                AreaCm2 = area.Value,
                PesoKgM = peso.Value,
                Corrugada = true
            });
            GuardarCatalogoExtra();
            _nuevoNombre.Text = "";
            _nuevoDiametro.Text = "";
            _nuevaArea.Text = "";
            _nuevoPeso.Text = "";
            Replanificar();
        }

        private void AlQuitar(BarraCatalogo barra)
        {
            _cfg.CatalogoExtra.Remove(barra);
            _desmarcados.Remove(barra.Nombre);
            GuardarCatalogoExtra();
            Replanificar();
        }

        /// <summary>Guarda config.json con la lista de extras. Si falla, la lista sigue valida en esta sesion.</summary>
        private void GuardarCatalogoExtra()
        {
            try
            {
                _cfg.Guardar(_rutaConfig);
                _nuevoMensaje.Foreground = BrochaCrear;
                _nuevoMensaje.Text = "Lista guardada en config.json (" + _cfg.CatalogoExtra.Count + " diametro(s) anadido(s)).";
            }
            catch (Exception ex)
            {
                _nuevoMensaje.Foreground = BrochaAviso;
                _nuevoMensaje.Text = "No se pudo guardar config.json: " + ex.Message + ". La lista solo vale para esta sesion.";
            }
        }

        private UIElement ConstruirGanchos()
        {
            var panel = new StackPanel();
            if (_ganchos.Count == 0)
            {
                panel.Children.Add(new TextBlock { Text = "El proyecto no tiene tipos de gancho (RebarHookType).", TextWrapping = TextWrapping.Wrap });
            }
            else
            {
                foreach (GanchoProyecto g in _ganchos)
                {
                    TipoGancho? t = g.Tipo;
                    string regla = t.HasValue ? _reglas.Describir(t.Value) : "sin regla E.060: calculo automatico";
                    panel.Children.Add(new TextBlock
                    {
                        Text = g.Nombre + "  (" + (g.EsEstribo ? "estribo" : "estandar") + ", " + Fmt(g.AnguloGrados) + " grados)  ->  " + regla,
                        Margin = new Thickness(0, 1, 0, 1),
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
            return new GroupBox
            {
                Header = "Ganchos del proyecto y regla E.060 que se aplica con la casilla \"Longitudes de gancho segun E.060\"",
                Padding = new Thickness(4),
                Margin = new Thickness(0, 6, 0, 0),
                Content = panel
            };
        }

        private UIElement ConstruirBotones()
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var guardar = new Button { Content = "Guardar opciones en config.json", Padding = new Thickness(10, 4, 10, 4) };
            guardar.ToolTip = "Escribe el prefijo y las casillas como valores por defecto en " + _rutaConfig;
            guardar.Click += AlGuardar;
            Grid.SetColumn(guardar, 0);
            panel.Children.Add(guardar);

            _mensaje = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0), Foreground = BrochaAviso, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(_mensaje, 1);
            panel.Children.Add(_mensaje);

            _crear = new Button { Content = "Crear", IsDefault = true, Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(6, 0, 6, 0) };
            _crear.Click += AlCrear;
            Grid.SetColumn(_crear, 2);
            panel.Children.Add(_crear);

            var cancelar = new Button { Content = "Cancelar", IsCancel = true, Padding = new Thickness(16, 4, 16, 4) };
            Grid.SetColumn(cancelar, 3);
            panel.Children.Add(cancelar);
            return panel;
        }

        // ------------------------------------------------------------------
        // Logica
        // ------------------------------------------------------------------
        private void Replanificar()
        {
            if (_construyendo) return;
            bool actualizar = _actualizar.IsChecked == true;
            List<FilaPlan> plan = Planificador.Planificar(_cfg, _prefijo.Text, _existentes, actualizar, NombreValido);
            _construyendo = true;
            ConstruirTabla(plan);
            _construyendo = false;
            ActualizarBoton();
        }

        private static bool NombreValido(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return false;
            try { return NamingUtils.IsValidName(nombre); }
            catch { return true; }
        }

        private int Marcados() => _filas.Count(f => f.Crear.IsChecked == true && f.Plan.Seleccionable);

        private void ActualizarBoton()
        {
            if (_crear == null) return;
            int n = Marcados();
            int nuevos = _filas.Count(f => f.Crear.IsChecked == true && f.Plan.Estado == EstadoFila.SeCreara);
            int act = n - nuevos;
            _crear.Content = act > 0 ? "Crear " + nuevos + " y actualizar " + act : "Crear " + n + " tipo(s)";
            _crear.IsEnabled = n > 0;
            int invalidos = _filas.Count(f => f.Plan.Estado == EstadoFila.NombreInvalido);
            int fuera = _filas.Count(f => f.Plan.Estado == EstadoFila.FueraDeNorma || f.Plan.Estado == EstadoFila.NombreDuplicado);
            var avisos = new List<string>();
            if (invalidos > 0) avisos.Add(invalidos + " nombre(s) no admitido(s) por Revit: cambia el prefijo o \"simboloPulgada\" en config.json.");
            if (fuera > 0) avisos.Add(fuera + " fila(s) fuera de la norma o con nombre repetido: no se crean (ver estado).");
            _mensaje.Text = string.Join(" ", avisos);
        }

        private void AlCrear(object sender, RoutedEventArgs e)
        {
            var op = new OpcionesEjecucion
            {
                Prefijo = _prefijo.Text,
                ActualizarExistentes = _actualizar.IsChecked == true,
                GanchosE060 = _ganchosE060.IsChecked == true
            };
            foreach (Fila f in _filas)
            {
                f.Plan.Crear = f.Plan.Seleccionable && f.Crear.IsChecked == true;
                op.Filas.Add(f.Plan);
            }
            Resultado = op;
            DialogResult = true;
        }

        private void AlGuardar(object sender, RoutedEventArgs e)
        {
            try
            {
                _cfg.PrefijoNombre = _prefijo.Text;
                _cfg.ActualizarExistentes = _actualizar.IsChecked == true;
                _cfg.LongitudesGanchoSegunE060 = _ganchosE060.IsChecked == true;
                _cfg.Guardar(_rutaConfig);
                _mensaje.Foreground = BrochaCrear;
                _mensaje.Text = "Opciones guardadas en config.json (los comentarios del archivo se pierden al guardar).";
            }
            catch (Exception ex)
            {
                _mensaje.Foreground = BrochaAviso;
                _mensaje.Text = "No se pudo guardar: " + ex.Message;
            }
        }

        // ------------------------------------------------------------------
        // Ayudas
        // ------------------------------------------------------------------
        private void Celda(UIElement el, int fila, int col)
        {
            Grid.SetRow(el, fila);
            Grid.SetColumn(el, col);
            _tabla.Children.Add(el);
        }

        private static string Fmt(double v, string formato = "0.##") => v.ToString(formato, CultureInfo.InvariantCulture);

        private static TextBlock Num(double v, string formato = "0.##") => new TextBlock
        {
            Text = Fmt(v, formato),
            Margin = Pad,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}
