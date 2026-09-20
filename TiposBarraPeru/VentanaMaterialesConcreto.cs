using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TiposBarraPeru.Reglas;
using static TiposBarraPeru.AyudasVentana;

namespace TiposBarraPeru
{
    /// <summary>
    /// Ventana de materiales de concreto, gemela de VentanaTiposBarra: prefijo de
    /// nombre editable, casilla de actualizar, tabla con una fila por resistencia
    /// (crear, nombre, f'c en kg/cm2 y MPa, E, densidad, estado), grupo "Otra
    /// resistencia" con E calculado en vivo, y recursos del proyecto encontrados.
    /// Construida en codigo (sin XAML).
    /// </summary>
    public sealed class VentanaMaterialesConcreto : Window
    {
        private readonly ConfiguracionMateriales _cfg;
        private readonly IList<MaterialExistente> _existentes;
        private readonly RecursosProyecto _recursos;
        private readonly string _rutaConfig;
        private readonly ReglasConcreto _reglas;

        private TextBox _prefijo;
        private CheckBox _actualizar;
        private Grid _tabla;
        private Button _crear;
        private TextBlock _mensaje;

        // grupo "Otra resistencia"
        private TextBox _nuevoFc, _nuevaDensidad;
        private TextBlock _nuevoE, _nuevoMensaje;

        private sealed class Fila
        {
            public FilaPlanConcreto Plan;
            public CheckBox Crear;
            public TextBlock Nombre, Estado;
        }

        private readonly List<Fila> _filas = new List<Fila>();
        /// <summary>Resistencias que el usuario desmarco a mano, para respetarlas al replanificar.</summary>
        private readonly HashSet<string> _desmarcados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _construyendo = true;

        /// <summary>Opciones finales si el usuario pulso "Crear"; null si cancelo.</summary>
        public OpcionesMateriales Resultado { get; private set; }

        public VentanaMaterialesConcreto(ConfiguracionMateriales cfg, IList<MaterialExistente> existentes, RecursosProyecto recursos, string rutaConfig)
        {
            _cfg = cfg;
            _existentes = existentes;
            _recursos = recursos;
            _rutaConfig = rutaConfig;
            _reglas = new ReglasConcreto(cfg.Reglas, cfg.Termico);

            Title = "Materiales de concreto Peru (E.060)";
            Width = 960;
            Height = 660;
            MinWidth = 800;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            FontSize = 12;

            Content = ConstruirRaiz();
            _construyendo = false;
            Replanificar();
            ActualizarNuevoE();
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
            cuerpo.Children.Add(new GroupBox { Header = "Catalogo (materiales.json) y resistencias anadidas (catalogoExtra)", Padding = new Thickness(4), Content = _tabla });
            cuerpo.Children.Add(ConstruirOtraResistencia());
            cuerpo.Children.Add(ConstruirRecursos());

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
                Text = "Materiales del proyecto: " + _existentes.Count + ". Por defecto solo se crean los que faltan. " +
                       "E = 15000 raiz(f'c) kg/cm2 para peso normal (E.060 art. 8.5); con otra densidad, E = wc^1.5 * 0.043 raiz(f'c) MPa. " +
                       "Poisson " + Fmt(_cfg.Reglas.Poisson) + ", dilatacion " + _cfg.Reglas.DilatacionTermicaPorC.ToString("0.0E0", System.Globalization.CultureInfo.InvariantCulture) +
                       " /C, ligero por debajo de " + Fmt(_cfg.Reglas.DensidadLigeroKgM3) + " kg/m3.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var linea = new StackPanel { Orientation = Orientation.Horizontal };
            linea.Children.Add(new TextBlock { Text = "Prefijo del nombre:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            _prefijo = new TextBox { Text = _cfg.PrefijoNombre, Width = 140, VerticalAlignment = VerticalAlignment.Center };
            _prefijo.ToolTip = "nombre del material = prefijo + f'c (Concreto f'c 210, Concreto f'c 280 ...)";
            _prefijo.TextChanged += (s, e) => Replanificar();
            linea.Children.Add(_prefijo);

            _actualizar = new CheckBox
            {
                Content = "Actualizar los que ya existen",
                IsChecked = _cfg.ActualizarExistentes,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0),
                ToolTip = "Reescribe identidad, color, tramas, apariencia y activos fisico y termico de los materiales que ya tienen ese nombre. Desactivado: se omiten."
            };
            _actualizar.Checked += (s, e) => Replanificar();
            _actualizar.Unchecked += (s, e) => Replanificar();
            linea.Children.Add(_actualizar);
            panel.Children.Add(linea);
            return panel;
        }

        private static readonly string[] Cabeceras =
            { "Crear", "Nombre", "f'c (kg/cm2)", "f'c (MPa)", "E (kg/cm2)", "E (MPa)", "Densidad (kg/m3)", "Estado", "" };

        /// <summary>Indice de la columna "Estado", la unica que se estira.</summary>
        private const int ColumnaEstado = 7;

        private void ConstruirTabla(List<FilaPlanConcreto> plan)
        {
            _tabla.Children.Clear();
            _tabla.RowDefinitions.Clear();
            _tabla.ColumnDefinitions.Clear();
            _filas.Clear();

            for (int c = 0; c < Cabeceras.Length; c++)
                _tabla.ColumnDefinitions.Add(new ColumnDefinition { Width = c == ColumnaEstado ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            _tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < Cabeceras.Length; c++)
                Celda(_tabla, new TextBlock { Text = Cabeceras[c], FontWeight = FontWeights.SemiBold, Margin = Pad }, 0, c);

            int r = 1;
            foreach (FilaPlanConcreto p in plan)
            {
                _tabla.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var fila = new Fila { Plan = p };
                ValoresConcreto v = p.Valores;

                fila.Crear = new CheckBox
                {
                    IsChecked = p.Crear && !_desmarcados.Contains(p.Concreto.NombreCatalogo),
                    IsEnabled = p.Seleccionable,
                    Margin = Pad,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                string clave = p.Concreto.NombreCatalogo;
                fila.Crear.Checked += (s, e) => { _desmarcados.Remove(clave); ActualizarBoton(); };
                fila.Crear.Unchecked += (s, e) => { if (!_construyendo) _desmarcados.Add(clave); ActualizarBoton(); };
                Celda(_tabla, fila.Crear, r, 0);

                fila.Nombre = new TextBlock { Text = p.Nombre, FontWeight = FontWeights.SemiBold, Margin = Pad, VerticalAlignment = VerticalAlignment.Center };
                fila.Nombre.ToolTip = v.Descripcion;
                Celda(_tabla, fila.Nombre, r, 1);
                Celda(_tabla, Num(v.FcKgCm2), r, 2);
                Celda(_tabla, Num(v.FcMPa, "0.0"), r, 3);
                var eKg = Num(v.EKgCm2, "#,0");
                eKg.ToolTip = v.FormulaE + "; G = E / (2 (1 + " + Fmt(v.Poisson) + ")) = " + Fmt(v.GKgCm2, "#,0") + " kg/cm2";
                Celda(_tabla, eKg, r, 4);
                var eMPa = Num(v.EMPa, "#,0");
                eMPa.ToolTip = "G = " + Fmt(v.GMPa, "#,0") + " MPa";
                Celda(_tabla, eMPa, r, 5);
                var dens = Num(v.DensidadKgM3);
                dens.ToolTip = v.Ligero ? "ligero: factor de corte " + Fmt(v.FactorCorte) + ", juego termico \"ligero\""
                                        : "peso normal: factor de corte " + Fmt(v.FactorCorte) + ", juego termico \"normal\"";
                Celda(_tabla, dens, r, 6);

                fila.Estado = new TextBlock { Text = p.TextoEstado, Margin = Pad, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
                bool problema = p.Estado == EstadoFila.NombreInvalido || p.Estado == EstadoFila.FueraDeNorma ||
                                p.Estado == EstadoFila.NombreDuplicado || !string.IsNullOrEmpty(p.Aviso);
                fila.Estado.Foreground = p.Estado == EstadoFila.SeCreara ? BrochaCrear : problema ? BrochaAviso : BrochaExiste;
                fila.Estado.ToolTip = "Termico: conductividad " + Fmt(v.Termico.ConductividadWmK, "0.###") + " W/(m K), calor especifico " +
                                      Fmt(v.Termico.CalorEspecificoJgC, "0.###") + " J/(g C), emisividad " + Fmt(v.Termico.Emisividad) +
                                      ", permeabilidad " + Fmt(v.Termico.PermeabilidadNgPaSm2) + " ng/(Pa s m2), porosidad " + Fmt(v.Termico.Porosidad) +
                                      ". Color gris " + v.Gris + ".";
                Celda(_tabla, fila.Estado, r, ColumnaEstado);

                if (p.EsExtra)
                {
                    var quitar = new Button { Content = "Quitar", Padding = new Thickness(8, 1, 8, 1), Margin = Pad, VerticalAlignment = VerticalAlignment.Center };
                    quitar.ToolTip = "Borra esta resistencia de catalogoExtra en materiales.json";
                    ConcretoCatalogo concreto = p.Concreto;
                    quitar.Click += (s, e) => AlQuitar(concreto);
                    Celda(_tabla, quitar, r, 8);
                }

                _filas.Add(fila);
                r++;
            }
        }

        private UIElement ConstruirOtraResistencia()
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Cualquier f'c entre " + Fmt(_cfg.Reglas.FcMinimoKgCm2) + " y " + Fmt(_cfg.Reglas.FcMaximoKgCm2) +
                       " kg/cm2. Con la densidad normal (" + Fmt(_cfg.Reglas.DensidadNormalKgM3) + " kg/m3) E = 15000 raiz(f'c); con otra densidad, entre " +
                       Fmt(_cfg.Reglas.DensidadMinimaKgM3) + " y " + Fmt(_cfg.Reglas.DensidadMaximaKgM3) + " kg/m3, la formula general de E.060.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var linea = new StackPanel { Orientation = Orientation.Horizontal };
            _nuevoFc = Campo(linea, "f'c (kg/cm2):", "", 70);
            _nuevaDensidad = Campo(linea, "Densidad (kg/m3):", Fmt(_cfg.Reglas.DensidadNormalKgM3), 70);
            _nuevoFc.TextChanged += (s, e) => ActualizarNuevoE();
            _nuevaDensidad.TextChanged += (s, e) => ActualizarNuevoE();
            _nuevoE = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            linea.Children.Add(_nuevoE);

            var anadir = new Button { Content = "Anadir a la lista", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            anadir.Click += AlAnadir;
            linea.Children.Add(anadir);
            panel.Children.Add(linea);

            _nuevoMensaje = new TextBlock { Foreground = BrochaAviso, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            panel.Children.Add(_nuevoMensaje);

            return new GroupBox { Header = "Otra resistencia", Padding = new Thickness(4), Margin = new Thickness(0, 6, 0, 0), Content = panel };
        }

        /// <summary>E calculado en vivo con lo escrito en f'c y densidad.</summary>
        private void ActualizarNuevoE()
        {
            if (_nuevoE == null) return;
            double? fc = Leer(_nuevoFc);
            double? d = Leer(_nuevaDensidad);
            if (fc == null || fc.Value <= 0 || d == null || d.Value <= 0) { _nuevoE.Text = "E = ..."; return; }
            ValoresConcreto v = _reglas.Calcular(fc.Value, d.Value);
            _nuevoE.Text = "E = " + Fmt(v.EKgCm2, "#,0") + " kg/cm2 (" + Fmt(v.EMPa, "#,0") + " MPa), " + (v.Ligero ? "ligero" : "peso normal") +
                           (v.DensidadNormal ? "" : ", formula general");
        }

        private void AlAnadir(object sender, RoutedEventArgs e)
        {
            _nuevoMensaje.Foreground = BrochaAviso;
            double? fc = Leer(_nuevoFc);
            if (fc == null || fc.Value <= 0) { _nuevoMensaje.Text = "Escribe la resistencia f'c en kg/cm2."; return; }
            double? d = Leer(_nuevaDensidad);
            if (d == null || d.Value <= 0) { _nuevoMensaje.Text = "Escribe la densidad en kg/m3."; return; }

            string error = PlanificadorConcreto.ValidarNuevoConcreto(_cfg, _prefijo.Text, fc.Value, d.Value, _existentes, NombreValido);
            if (error != null) { _nuevoMensaje.Text = error; return; }

            _cfg.CatalogoExtra.Add(new ConcretoCatalogo(fc.Value, d.Value));
            GuardarCatalogoExtra();
            _nuevoFc.Text = "";
            _nuevaDensidad.Text = Fmt(_cfg.Reglas.DensidadNormalKgM3);
            Replanificar();
        }

        private void AlQuitar(ConcretoCatalogo concreto)
        {
            _cfg.CatalogoExtra.Remove(concreto);
            _desmarcados.Remove(concreto.NombreCatalogo);
            GuardarCatalogoExtra();
            Replanificar();
        }

        /// <summary>Guarda materiales.json con la lista de extras. Si falla, la lista sigue valida en esta sesion.</summary>
        private void GuardarCatalogoExtra()
        {
            try
            {
                _cfg.Guardar(_rutaConfig);
                _nuevoMensaje.Foreground = BrochaCrear;
                _nuevoMensaje.Text = "Lista guardada en materiales.json (" + _cfg.CatalogoExtra.Count + " resistencia(s) anadida(s)).";
            }
            catch (Exception ex)
            {
                _nuevoMensaje.Foreground = BrochaAviso;
                _nuevoMensaje.Text = "No se pudo guardar materiales.json: " + ex.Message + ". La lista solo vale para esta sesion.";
            }
        }

        private UIElement ConstruirRecursos()
        {
            var panel = new StackPanel();
            foreach (string linea in _recursos.Describir(_cfg))
                panel.Children.Add(new TextBlock { Text = linea, Margin = new Thickness(0, 1, 0, 1), TextWrapping = TextWrapping.Wrap });
            return new GroupBox
            {
                Header = "Tramas y apariencia del proyecto (nombres en materiales.json)",
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

            var guardar = new Button { Content = "Guardar opciones en materiales.json", Padding = new Thickness(10, 4, 10, 4) };
            guardar.ToolTip = "Escribe el prefijo y la casilla como valores por defecto en " + _rutaConfig;
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
            List<FilaPlanConcreto> plan = PlanificadorConcreto.Planificar(_cfg, _prefijo.Text, _existentes, actualizar, NombreValido);
            _construyendo = true;
            ConstruirTabla(plan);
            _construyendo = false;
            ActualizarBoton();
        }

        private int Marcados() => _filas.Count(f => f.Crear.IsChecked == true && f.Plan.Seleccionable);

        private void ActualizarBoton()
        {
            if (_crear == null) return;
            int n = Marcados();
            int nuevos = _filas.Count(f => f.Crear.IsChecked == true && f.Plan.Estado == EstadoFila.SeCreara);
            int act = n - nuevos;
            _crear.Content = act > 0 ? "Crear " + nuevos + " y actualizar " + act : "Crear " + n + " material(es)";
            _crear.IsEnabled = n > 0;
            int invalidos = _filas.Count(f => f.Plan.Estado == EstadoFila.NombreInvalido);
            int fuera = _filas.Count(f => f.Plan.Estado == EstadoFila.FueraDeNorma || f.Plan.Estado == EstadoFila.NombreDuplicado);
            var avisos = new List<string>();
            if (invalidos > 0) avisos.Add(invalidos + " nombre(s) no admitido(s) por Revit: cambia el prefijo.");
            if (fuera > 0) avisos.Add(fuera + " fila(s) fuera de rango o con nombre repetido: no se crean (ver estado).");
            _mensaje.Text = string.Join(" ", avisos);
        }

        private void AlCrear(object sender, RoutedEventArgs e)
        {
            var op = new OpcionesMateriales
            {
                Prefijo = _prefijo.Text,
                ActualizarExistentes = _actualizar.IsChecked == true
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
                _cfg.Guardar(_rutaConfig);
                _mensaje.Foreground = BrochaCrear;
                _mensaje.Text = "Opciones guardadas en materiales.json (los comentarios del archivo se pierden al guardar).";
            }
            catch (Exception ex)
            {
                _mensaje.Foreground = BrochaAviso;
                _mensaje.Text = "No se pudo guardar: " + ex.Message;
            }
        }
    }
}
